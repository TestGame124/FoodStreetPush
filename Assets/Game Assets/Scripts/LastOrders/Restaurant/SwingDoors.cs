using System.Collections.Generic;
using FoodStreet.Visitors;
using LastOrders.Core;
using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Restaurant
{
    /// <summary>
    /// The diner's front doors: a pair of swing doors, one on each jamb, that open for whoever
    /// walks up to them and flap shut behind them.
    ///
    /// They swing either way, like a diner's kitchen doors - away from the person coming through,
    /// into the room for someone arriving and out to the yard for someone leaving - and stay open
    /// while anyone is still in the doorway. Then they swing back on a loose spring: past closed,
    /// back, a smaller swing the other way, and still. The two leaves are hung a little
    /// differently, so they never move as one.
    ///
    /// Only people open them - visitors, the workers and the waiters. A zombie at the door gets
    /// nothing but a rattle: each blow on the restaurant near the doorway jolts both leaves in
    /// their frame.
    ///
    /// No physics: the doors look for people near the doorway themselves, a few times a second,
    /// and follow the ones close enough to matter every frame. Nothing about anyone's route
    /// changes - the doorway is open to the nav grid either way.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwingDoors : MonoBehaviour
    {
        [Tooltip("The doorway: its middle at floor level, Z pointing into the restaurant, X across it.")]
        [SerializeField] Transform doorway;

        [Tooltip("The leaf hung on the left jamb (looking in), modelled shut.")]
        [SerializeField] Transform leftLeaf;

        [Tooltip("The leaf hung on the right jamb (looking in), modelled shut.")]
        [SerializeField] Transform rightLeaf;

        [Tooltip("Half the doorway's width, metres.")]
        [SerializeField] float halfWidth = 0.85f;

        [Tooltip("Degrees a leaf swings open, either way.")]
        [Range(30f, 95f)] [SerializeField] float openDegrees = 84f;

        [Tooltip("Metres in front of or behind the doorway from which someone walking at it opens it.")]
        [SerializeField] float reach = 1.8f;

        [Tooltip("Metres either side of the doorway that still count as walking at it.")]
        [SerializeField] float sideMargin = 0.55f;

        [Tooltip("Metres from the doorway's line inside which someone keeps it open whichever way they face.")]
        [SerializeField] float inDoorway = 0.8f;

        [Tooltip("Seconds the doors stay open after the last person has gone through.")]
        [SerializeField] float holdOpenSeconds = 0.35f;

        [Header("Swing")]
        [Tooltip("How hard a leaf is pushed open, and how quickly that settles. Settles with a little bounce off the stop.")]
        [SerializeField] float openStiffness = 110f;
        [SerializeField] float openDamping = 15f;

        [Tooltip("Degrees a second the leaves are flung with as someone first pushes through.")]
        [SerializeField] float pushSpeed = 240f;

        [Tooltip("The spring that swings them shut. Loose, so they flap past closed and back before they settle.")]
        [SerializeField] float closeStiffness = 42f;
        [SerializeField] float closeDamping = 4.2f;

        [Tooltip("The right leaf's springs as a share of the left's, so the pair never moves as one.")]
        [Range(0.6f, 1.2f)] [SerializeField] float rightLeafStiffness = 0.86f;

        [Header("Zombies at the door")]
        [Tooltip("Degrees a second a blow on the doors jolts them by. They are shut: it only rattles them in the frame.")]
        [SerializeField] float rattleKick = 150f;

        [Tooltip("Metres from the doorway's edge a blow on the restaurant still rattles the doors.")]
        [SerializeField] float rattleReach = 0.9f;

        [SerializeField] GameLifetimeScope scope;

        [Tooltip("The doorway's width before the double doors were fitted, for putting the old door back.")]
        [SerializeField, HideInInspector] float previousDoorway;

        /// <summary>One leaf: how far open it is (degrees, + into the room) and how fast it is going.</summary>
        sealed class Leaf
        {
            public Transform Transform;
            public Quaternion Shut;
            public float Sign;
            public float Stiffness = 1f;
            public float Angle, Speed;
            public float Rattle, RattleSpeed;
        }

        /// <summary>Someone the doors are keeping an eye on, and where they were last frame.</summary>
        struct Watched
        {
            public Transform Body;
            public Vector3 Last;
        }

        const float RefreshSeconds = 0.4f;
        const float WatchRadius = 7f;
        const float RattleStiffness = 600f;
        const float RattleDamping = 18f;

        readonly Leaf left = new Leaf();
        readonly Leaf right = new Leaf();
        readonly List<Watched> watched = new List<Watched>(16);

        GameComposition composition;
        RestaurantService restaurant;

        /// <summary>The way the doors are open or opening: +1 into the room, -1 out to the yard, 0 shut.</summary>
        int swing;

        float clearFor;
        float refreshIn;
        bool ready;

        /// <summary>+1 open into the room, -1 out to the yard, 0 shut or swinging shut.</summary>
        public int Swing => swing;

        /// <summary>Degrees each leaf stands open; + into the room.</summary>
        public float LeftAngle => left.Angle + left.Rattle;
        public float RightAngle => right.Angle + right.Rattle;

        /// <summary>Somebody is in or walking at the doorway, so the doors are held open.</summary>
        public bool Occupied => clearFor <= 0f;

        public float PreviousDoorway => previousDoorway;

        void Awake() => Capture();

        /// <summary>The leaves as modelled, shut, and which way each turns to open into the room.</summary>
        void Capture()
        {
            if (ready || doorway == null) return;

            Vector3 inward = Flat(doorway.forward);
            Hang(left, leftLeaf, inward, 1f);
            Hang(right, rightLeaf, inward, rightLeafStiffness);
            clearFor = holdOpenSeconds;
            ready = true;
        }

        static void Hang(Leaf leaf, Transform t, Vector3 inward, float stiffness)
        {
            leaf.Transform = t;
            leaf.Stiffness = stiffness;
            if (t == null) return;

            leaf.Shut = t.localRotation;

            // Whichever way round the hinge carries the leaf's free edge into the room.
            Vector3 along = Flat(t.forward);
            Vector3 turned = Quaternion.AngleAxis(10f, Vector3.up) * along;
            leaf.Sign = Vector3.Dot(turned - along, inward) >= 0f ? 1f : -1f;
        }

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;
            restaurant = composition != null ? composition.Restaurant : null;
            if (restaurant != null) restaurant.OnHit += HandleHit;
        }

        void OnDestroy()
        {
            if (restaurant != null) restaurant.OnHit -= HandleHit;
        }

        void OnDisable()
        {
            // Shut and still, should it ever be switched back on.
            foreach (Leaf leaf in new[] { left, right })
            {
                leaf.Angle = leaf.Speed = leaf.Rattle = leaf.RattleSpeed = 0f;
                if (leaf.Transform != null && ready) leaf.Transform.localRotation = leaf.Shut;
            }

            swing = 0;
        }

        void Update()
        {
            if (!ready)
            {
                Capture();
                if (!ready) return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            refreshIn -= dt;
            if (refreshIn <= 0f)
            {
                refreshIn = RefreshSeconds;
                Refresh();
            }

            int wanted = Look(dt);

            if (wanted != 0)
            {
                clearFor = 0f;

                // Pushed open from shut: flung that way. Already open: they stay the way they are.
                if (swing == 0 || (Mathf.Abs(left.Angle) < 12f && Mathf.Abs(right.Angle) < 12f && swing != wanted))
                {
                    swing = wanted;
                    Push(left, wanted);
                    Push(right, wanted);
                }
            }
            else
            {
                clearFor += dt;
            }

            bool closing = clearFor > holdOpenSeconds;
            float target = closing ? 0f : swing * openDegrees;

            // A few steps a frame, so a slow frame cannot throw a spring off.
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.012f), 1, 8);
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Spring(left, target, closing, step);
                Spring(right, target, closing, step);
            }

            // Back in the frame and as good as still: shut.
            if (closing && swing != 0 && Settled(left) && Settled(right)) swing = 0;

            Apply(left);
            Apply(right);
        }

        /// <summary>The way someone at the doorway needs the doors to open: +1 into the room, -1 out, 0 nobody.</summary>
        int Look(float dt)
        {
            Vector3 centre = doorway.position;
            Vector3 inward = Flat(doorway.forward);
            Vector3 across = Flat(doorway.right);

            int wanted = 0;
            float nearest = float.MaxValue;

            for (int i = watched.Count - 1; i >= 0; i--)
            {
                Watched w = watched[i];
                if (w.Body == null || !w.Body.gameObject.activeInHierarchy)
                {
                    watched.RemoveAt(i);
                    continue;
                }

                Vector3 here = w.Body.position;
                Vector3 velocity = (here - w.Last) / dt;
                w.Last = here;
                watched[i] = w;

                Vector3 offset = here - centre;
                int side = SideToOpen(Vector3.Dot(offset, across), Vector3.Dot(offset, inward), Vector3.Dot(velocity, inward),
                                      halfWidth + sideMargin, reach, inDoorway);
                if (side == 0) continue;

                float distance = Mathf.Abs(Vector3.Dot(offset, inward));
                if (distance < nearest)
                {
                    nearest = distance;
                    wanted = side;
                }
            }

            return wanted;
        }

        /// <summary>
        /// Which way the doors must swing for someone <paramref name="across"/> metres to the side
        /// of the doorway's middle and <paramref name="through"/> metres into the room (negative:
        /// out in the yard), walking into the room at <paramref name="speedIn"/> m/s: away from
        /// them, +1 into the room or -1 out to the yard; 0 if they are not coming through - off to
        /// the side, too far away, or walking away from it.
        /// </summary>
        public static int SideToOpen(float across, float through, float speedIn, float halfSpan, float reach, float inDoorway)
        {
            if (Mathf.Abs(across) > halfSpan || Mathf.Abs(through) > reach) return 0;

            const float walking = 0.15f;
            bool inIt = Mathf.Abs(through) < inDoorway;
            bool coming = through < 0f ? speedIn > walking : speedIn < -walking;
            if (!inIt && !coming) return 0;

            // Right on the line: whichever way they are walking.
            if (Mathf.Abs(through) < 0.15f) return speedIn < -walking ? -1 : 1;

            return through < 0f ? 1 : -1;
        }

        void Push(Leaf leaf, int way)
        {
            if (Mathf.Abs(leaf.Speed) < pushSpeed * 0.5f) leaf.Speed = way * pushSpeed * Random.Range(0.85f, 1.1f);
        }

        void Spring(Leaf leaf, float target, bool closing, float dt)
        {
            float k = (closing ? closeStiffness : openStiffness) * leaf.Stiffness;
            float c = (closing ? closeDamping : openDamping) * Mathf.Sqrt(leaf.Stiffness);

            leaf.Speed += (k * (target - leaf.Angle) - c * leaf.Speed) * dt;
            leaf.Angle += leaf.Speed * dt;

            // The stop: no further than a few degrees past open, and it bounces off it.
            float stop = openDegrees + 6f;
            if (Mathf.Abs(leaf.Angle) > stop)
            {
                leaf.Angle = Mathf.Sign(leaf.Angle) * stop;
                leaf.Speed *= -0.3f;
            }

            leaf.RattleSpeed += (-RattleStiffness * leaf.Rattle - RattleDamping * leaf.RattleSpeed) * dt;
            leaf.Rattle += leaf.RattleSpeed * dt;
        }

        static bool Settled(Leaf leaf) => Mathf.Abs(leaf.Angle) < 1.5f && Mathf.Abs(leaf.Speed) < 8f;

        static void Apply(Leaf leaf)
        {
            if (leaf.Transform == null) return;
            leaf.Transform.localRotation = leaf.Shut * Quaternion.AngleAxis((leaf.Angle + leaf.Rattle) * leaf.Sign, Vector3.up);
        }

        /// <summary>A blow on the restaurant: near the doorway, the doors jolt in their frame.</summary>
        void HandleHit(Vector3 at)
        {
            if (doorway == null) return;

            Vector3 offset = at - doorway.position;
            float side = Mathf.Abs(Vector3.Dot(offset, Flat(doorway.right)));
            float through = Mathf.Abs(Vector3.Dot(offset, Flat(doorway.forward)));
            if (side > halfWidth + rattleReach || through > 1.2f) return;

            Rattle(1f);
        }

        /// <summary>Jolts both leaves in the frame, as a blow on them would - into the room, away from whoever struck.</summary>
        public void Rattle(float strength)
        {
            left.RattleSpeed += rattleKick * strength * Random.Range(0.7f, 1.2f);
            right.RattleSpeed += rattleKick * strength * Random.Range(0.7f, 1.2f);
        }

        /// <summary>Everyone who might come through soon: the people within a few metres of the door.</summary>
        void Refresh()
        {
            Vector3 centre = doorway.position;

            Consider(FindObjectsByType<Visitor>(FindObjectsSortMode.None), centre);
            Consider(FindObjectsByType<WorkerAgent>(FindObjectsSortMode.None), centre);
            Consider(FindObjectsByType<Waiter>(FindObjectsSortMode.None), centre);

            // Those who have wandered off are dropped.
            for (int i = watched.Count - 1; i >= 0; i--)
            {
                Transform body = watched[i].Body;
                if (body == null || FlatDistanceSq(body.position, centre) > WatchRadius * WatchRadius * 1.3f)
                    watched.RemoveAt(i);
            }
        }

        void Consider<T>(T[] people, Vector3 centre) where T : Component
        {
            for (int i = 0; i < people.Length; i++)
            {
                Transform body = people[i].transform;
                if (FlatDistanceSq(body.position, centre) > WatchRadius * WatchRadius) continue;
                if (IsWatched(body)) continue;

                watched.Add(new Watched { Body = body, Last = body.position });
            }
        }

        bool IsWatched(Transform body)
        {
            for (int i = 0; i < watched.Count; i++)
                if (watched[i].Body == body) return true;
            return false;
        }

        /// <summary>The direction along the ground.</summary>
        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward;
        }

        static float FlatDistanceSq(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }

        /// <summary>Editor wiring, for the door builder.</summary>
        public void Configure(Transform middle, Transform leafLeft, Transform leafRight, float half, float doorwayBefore)
        {
            doorway = middle;
            leftLeaf = leafLeft;
            rightLeaf = leafRight;
            halfWidth = half;
            if (doorwayBefore > 0f) previousDoorway = doorwayBefore;
            ready = false;
        }
    }
}
