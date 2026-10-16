using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Kitchen
{
    /// <summary>
    /// What is on the serving pass, plate by plate, and who is coming for it.
    ///
    /// The kitchen's dishes are still items in the level's inventory - <see cref="MealStock"/> explains
    /// why that must stay the one count - and this is a view of them: every ready dish the counter has
    /// room for stands on it as a plate. When a station finishes a dish it says where from
    /// (<see cref="Incoming"/>), and the plate hops from the station onto the nearest free place; one
    /// that turns up any other way (a save loaded, a dish put back) just appears.
    ///
    /// Waiters promise dishes before they set off (<see cref="Reserve"/>), so two of them never walk to
    /// the same plate, and take them off the pass when they get there (<see cref="TakeDish"/>), which
    /// is when the dish leaves the stock. The plate itself goes with them to the table and comes back
    /// here when the customer has eaten.
    ///
    /// One per level, made by the first pass that comes up if the scene has none. It does nothing a
    /// frame unless the inventory has changed, and builds no objects once its pool is warm.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServingPassBoard : MonoBehaviour
    {
        struct Spot
        {
            public ServingPass Pass;
            public Transform Point;
            public PassDish Dish;
        }

        static ServingPassBoard instance;

        [Tooltip("What the dishes look like. Taken from the passes when not set.")]
        [SerializeField] PassDishSet dishes;

        [Tooltip("Plates made up front, so the first dishes of the day build nothing.")]
        [SerializeField] int prewarm = 12;

        [Header("A dish coming out of the kitchen")]
        [SerializeField] float hopSeconds = 0.6f;
        [SerializeField] float hopHeight = 0.9f;

        [Tooltip("How far a plate is turned either way on the counter, so a row of them looks set down by hand.")]
        [SerializeField] float jitterDegrees = 9f;

        readonly List<ServingPass> passes = new List<ServingPass>();
        readonly List<Spot> spots = new List<Spot>();
        readonly Stack<PassDish> pool = new Stack<PassDish>();
        readonly Dictionary<ItemType, int> reserved = new Dictionary<ItemType, int>();
        readonly Dictionary<ItemType, Queue<Vector3>> incoming = new Dictionary<ItemType, Queue<Vector3>>();

        // Scratch for Reconcile, kept to build nothing a call.
        readonly Dictionary<ItemType, int> stock = new Dictionary<ItemType, int>();
        readonly Dictionary<ItemType, int> shown = new Dictionary<ItemType, int>();
        readonly Dictionary<ItemType, int> target = new Dictionary<ItemType, int>();
        readonly List<ItemType> kinds = new List<ItemType>();

        bool dirty = true;
        float resync;
        int placed;

        /// <summary>The level's board, or null when it has no pass.</summary>
        public static ServingPassBoard Instance => instance;

        /// <summary>True when cooked <paramref name="type"/> goes onto a pass rather than into the storehouse.</summary>
        public static bool Receives(ItemType type) => instance != null && instance.spots.Count > 0 && MealStock.IsDish(type);

        PassDishSet Dishes
        {
            get
            {
                if (dishes != null) return dishes;
                foreach (ServingPass pass in passes)
                    if (pass != null && pass.Dishes != null) return dishes = pass.Dishes;
                return null;
            }
        }

        // --- Passes -----------------------------------------------------------------------------------

        internal static void Join(ServingPass pass)
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ServingPassBoard>();
                if (instance == null) new GameObject("Serving Pass Board").AddComponent<ServingPassBoard>();
            }

            instance.Add(pass);
        }

        internal static void Leave(ServingPass pass)
        {
            if (instance != null) instance.Remove(pass);
        }

        void Add(ServingPass pass)
        {
            if (pass == null || passes.Contains(pass)) return;

            passes.Add(pass);
            IReadOnlyList<Transform> points = pass.Slots;
            if (points != null)
                foreach (Transform point in points)
                    if (point != null) spots.Add(new Spot { Pass = pass, Point = point });

            dirty = true;
        }

        void Remove(ServingPass pass)
        {
            if (!passes.Remove(pass)) return;

            for (int i = spots.Count - 1; i >= 0; i--)
            {
                if (spots[i].Pass != pass) continue;

                PassDish dish = spots[i].Dish;
                spots.RemoveAt(i);
                if (dish != null)
                {
                    dish.Spot = -1;
                    Recycle(dish);
                }
            }

            // Later places have moved down the list.
            for (int i = 0; i < spots.Count; i++)
                if (spots[i].Dish != null) spots[i].Dish.Spot = i;

            dirty = true;
        }

        // --- Life -------------------------------------------------------------------------------------

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
        }

        void OnEnable()
        {
            InventorySystem.onInventoryChangedCallback += MarkDirty;
            dirty = true;
        }

        void OnDisable() => InventorySystem.onInventoryChangedCallback -= MarkDirty;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Start()
        {
            for (int i = pool.Count; i < prewarm; i++) Recycle(MakeDish());
        }

        void MarkDirty() => dirty = true;

        void Update()
        {
            // The inventory says when it changes, but not every path through it does: once a second
            // the pass is checked anyway. Cheap - a walk over a score of items and eight places.
            resync -= Time.deltaTime;
            if (resync <= 0f)
            {
                resync = 1f;
                dirty = true;
            }

            if (!dirty) return;

            dirty = false;
            Reconcile();
        }

        // --- The kitchen ------------------------------------------------------------------------------

        /// <summary>
        /// A dish of <paramref name="type"/> is about to be added to the stock, cooked at
        /// <paramref name="from"/>: its plate hops from there onto the pass.
        /// </summary>
        public void Incoming(ItemType type, Vector3 from)
        {
            if (!incoming.TryGetValue(type, out Queue<Vector3> queue)) incoming[type] = queue = new Queue<Vector3>(2);
            queue.Enqueue(from);
            dirty = true;
        }

        // --- The waiters ------------------------------------------------------------------------------

        /// <summary>How many of <paramref name="type"/> are cooked, whether or not the counter has room to show them.</summary>
        public static int Stock(ItemType type)
        {
            InventorySystem inventory = InventorySystem.instance;
            return inventory != null ? inventory.ItemCount(type) : 0;
        }

        /// <summary>How many of <paramref name="type"/> are cooked and not promised to a waiter already.</summary>
        public int Available(ItemType type) => Mathf.Max(0, Stock(type) - Reserved(type));

        int Reserved(ItemType type) => reserved.TryGetValue(type, out int n) ? n : 0;

        /// <summary>Promises <paramref name="amount"/> of <paramref name="type"/> to a waiter, if there are that many free.</summary>
        public bool Reserve(ItemType type, int amount)
        {
            if (amount <= 0 || Available(type) < amount) return false;

            reserved[type] = Reserved(type) + amount;
            return true;
        }

        /// <summary>Lets go of a promise made with <see cref="Reserve"/>.</summary>
        public void Unreserve(ItemType type, int amount)
        {
            int left = Reserved(type) - amount;
            if (left > 0) reserved[type] = left;
            else reserved.Remove(type);
        }

        /// <summary>
        /// Where a waiter near <paramref name="from"/> goes for <paramref name="type"/>: in front of the
        /// pass with that dish on it, the nearest - or the nearest pass at all when the dish is stock the
        /// counter has no room to show.
        /// </summary>
        public Transform PickUpPoint(ItemType type, Vector3 from)
        {
            int spot = NearestSpotWith(type, from);
            ServingPass pass = spot >= 0 ? spots[spot].Pass : NearestPass(from);
            return pass != null ? pass.NearestServicePoint(from) : null;
        }

        /// <summary>
        /// Lifts a plate of <paramref name="type"/> off the pass for a waiter at <paramref name="near"/>,
        /// ending their promise: the <paramref name="amount"/> leaves the stock now. Null when the stock
        /// has gone somewhere else meanwhile - nothing is taken then.
        /// </summary>
        public PassDish TakeDish(ItemType type, int amount, Vector3 near)
        {
            Unreserve(type, amount);

            InventorySystem inventory = InventorySystem.instance;
            if (inventory == null || !inventory.TryConsume(type, amount)) return null;

            int spot = NearestSpotWith(type, near);
            PassDish dish;
            if (spot >= 0)
            {
                dish = spots[spot].Dish;
                ClearSpot(spot);
                dish.Settle();
            }
            else
            {
                // Stock the counter had no room for: out from under it.
                dish = Rent(type);
                ServingPass pass = NearestPass(near);
                dish.transform.position = pass != null ? pass.transform.position + Vector3.up * 0.9f : near + Vector3.up;
            }

            dirty = true;
            return dish;
        }

        /// <summary>
        /// A dish a waiter could not deliver goes back: <paramref name="amount"/> into the stock again,
        /// its plate hopping from their hands back onto the pass.
        /// </summary>
        public void PutBack(PassDish dish, int amount)
        {
            ItemType type = dish.Type;
            Vector3 from = dish.transform.position;
            Recycle(dish);

            InventorySystem inventory = InventorySystem.instance;
            ItemDataSO data = MealStock.DishData(type);
            if (inventory == null || data == null) return;

            for (int i = 0; i < amount; i++)
            {
                Incoming(type, from);
                inventory.AddItem(new Item(data, 1));
            }
        }

        /// <summary>Puts a plate back in the pool: eaten from, or no longer wanted on the pass.</summary>
        public void Recycle(PassDish dish)
        {
            if (dish == null) return;

            if (dish.Spot >= 0 && dish.Spot < spots.Count && spots[dish.Spot].Dish == dish) ClearSpot(dish.Spot);

            dish.Spot = -1;
            dish.Settle();
            dish.transform.SetParent(transform, false);
            dish.gameObject.SetActive(false);
            if (!pool.Contains(dish)) pool.Push(dish);
        }

        // --- Keeping the counter true -----------------------------------------------------------------

        /// <summary>
        /// Makes the plates on the pass match the stock: a plate per dish while there is room, shared out
        /// a plate at a time across the dishes so a run of bread never hides the one bowl of soup.
        /// Plates already standing stay where they are.
        /// </summary>
        void Reconcile()
        {
            KeepPlatesOnSpots();

            stock.Clear();
            shown.Clear();
            target.Clear();
            kinds.Clear();

            InventorySystem inventory = InventorySystem.instance;
            if (inventory != null)
            {
                List<Item> items = inventory.GetItemList();
                for (int i = 0; i < items.Count; i++)
                {
                    Item item = items[i];
                    if (item == null || item.itemsAmount <= 0 || !MealStock.IsDish(item.type)) continue;

                    if (!stock.ContainsKey(item.type)) kinds.Add(item.type);
                    stock[item.type] = (stock.TryGetValue(item.type, out int n) ? n : 0) + item.itemsAmount;
                }
            }

            int room = spots.Count, given = 0;
            for (bool more = true; more && given < room;)
            {
                more = false;
                foreach (ItemType kind in kinds)
                {
                    if (given >= room) break;

                    int t = target.TryGetValue(kind, out int n) ? n : 0;
                    if (t >= stock[kind]) continue;

                    target[kind] = t + 1;
                    given++;
                    more = true;
                }
            }

            for (int i = 0; i < spots.Count; i++)
            {
                PassDish dish = spots[i].Dish;
                if (dish != null) shown[dish.Type] = (shown.TryGetValue(dish.Type, out int n) ? n : 0) + 1;
            }

            // Too many of a dish: the right-most go first, so what stays reads from the left.
            for (int i = spots.Count - 1; i >= 0; i--)
            {
                PassDish dish = spots[i].Dish;
                if (dish == null) continue;

                int want = target.TryGetValue(dish.Type, out int t) ? t : 0;
                int have = shown[dish.Type];
                if (have <= want) continue;

                shown[dish.Type] = have - 1;
                ClearSpot(i);
                dish.PopOut();
            }

            foreach (ItemType kind in kinds)
            {
                int want = target.TryGetValue(kind, out int t) ? t : 0;
                int have = shown.TryGetValue(kind, out int h) ? h : 0;

                for (; have < want; have++)
                {
                    Queue<Vector3> from = incoming.TryGetValue(kind, out Queue<Vector3> q) && q.Count > 0 ? q : null;
                    Vector3 origin = from != null ? from.Dequeue() : default;

                    int spot = from != null ? FreeSpotNearest(origin) : FreeSpotNearest(null);
                    if (spot < 0) break;

                    Place(kind, spot, from != null, origin);
                }
            }

            // Anything cooked the counter had no room for comes up later on its own, without a hop
            // from a station long since finished with it.
            foreach (Queue<Vector3> queue in incoming.Values) queue.Clear();
        }

        void Place(ItemType type, int spot, bool hop, Vector3 from)
        {
            Spot s = spots[spot];
            PassDish dish = Rent(type);
            Quaternion facing = s.Point.rotation * Quaternion.Euler(0f, Jitter(placed++) * jitterDegrees, 0f);

            dish.Spot = spot;
            s.Dish = dish;
            spots[spot] = s;

            if (hop)
            {
                dish.transform.SetPositionAndRotation(from, facing * Quaternion.Euler(0f, 160f, 0f));
                dish.HopTo(s.Point.position, facing, hopSeconds * Mathf.Clamp(Vector3.Distance(from, s.Point.position) / 3f, 0.7f, 1.4f), hopHeight);
            }
            else
            {
                dish.transform.SetPositionAndRotation(s.Point.position, facing);
                dish.PopIn();
            }
        }

        /// <summary>
        /// Stands every plate that is not on its way somewhere back on its spot: the counter under it
        /// was carried, back with the kitchen when the restaurant grew. A plate still hopping is left
        /// to land, and stood right on the next pass of this.
        /// </summary>
        public void KeepPlatesOnSpots()
        {
            for (int i = 0; i < spots.Count; i++)
            {
                Spot s = spots[i];
                if (s.Dish == null || s.Point == null || s.Dish.IsMoving) continue;

                Transform plate = s.Dish.transform;
                if ((plate.position - s.Point.position).sqrMagnitude > 1e-6f) plate.position = s.Point.position;
            }
        }

        void ClearSpot(int spot)
        {
            Spot s = spots[spot];
            if (s.Dish != null) s.Dish.Spot = -1;
            s.Dish = null;
            spots[spot] = s;
        }

        int FreeSpotNearest(Vector3? point)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].Dish != null || spots[i].Point == null) continue;
                if (point == null) return i;

                float d = (spots[i].Point.position - point.Value).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            return best;
        }

        int NearestSpotWith(ItemType type, Vector3 near)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < spots.Count; i++)
            {
                PassDish dish = spots[i].Dish;
                if (dish == null || dish.Type != type) continue;

                float d = (spots[i].Point.position - near).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            return best;
        }

        ServingPass NearestPass(Vector3 from)
        {
            ServingPass best = null;
            float bestDistance = float.MaxValue;

            foreach (ServingPass pass in passes)
            {
                if (pass == null) continue;

                float d = (pass.transform.position - from).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pass;
                }
            }

            return best;
        }

        // --- The pool ---------------------------------------------------------------------------------

        PassDish Rent(ItemType type)
        {
            PassDish dish = pool.Count > 0 ? pool.Pop() : MakeDish();
            dish.Dress(type, Dishes);
            dish.gameObject.SetActive(true);
            return dish;
        }

        PassDish MakeDish()
        {
            PassDish dish = PassDish.Create(transform);
            dish.Board = this;
            return dish;
        }

        static float Jitter(int key)
        {
            unchecked
            {
                uint h = (uint)key * 2654435761u;
                h ^= h >> 15;
                return (h % 2001) / 1000f - 1f;
            }
        }
    }
}
