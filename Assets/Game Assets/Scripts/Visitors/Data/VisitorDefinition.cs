using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// One kind of person who drives up, eats and leaves.
    ///
    /// Everything that varies between visitors lives here rather than in the director, so a new
    /// kind of customer is a new asset and nothing else. The director knows only that it needs
    /// somebody: which body, which car, what they want and what they pay for it are all read
    /// off whichever definition it drew.
    ///
    /// Orders are a weighted table rather than a single dish because the whole interaction is
    /// one tap, and a tap is only a decision if the player might not be able to make it. Asking
    /// for three of something when two are in stock is what turns "tap the customer" into
    /// "keep the kitchen ahead of the car park".
    /// </summary>
    [CreateAssetMenu(menuName = "Food Street/Visitors/Visitor", fileName = "Visitor_")]
    public sealed class VisitorDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Visitor";

        [Tooltip("Which of the project's character models this one wears. Wrapped, so a number " +
                 "past the end of the set is the start of it rather than an error.")]
        public int characterModel;

        [Tooltip("Optional headwear prefab name under the project's character folder. Empty " +
                 "for none.")]
        public string headwear;

        [Header("Arrival")]
        public VehicleDefinition vehicle;

        [Tooltip("Relative chance of being the one who turns up. Zero never arrives, which is " +
                 "how a visitor is shelved without deleting the asset.")]
        [Min(0f)] public float spawnWeight = 1f;

        [Header("Order")]
        [Tooltip("What this visitor might ask for, weighted within this list. An empty list " +
                 "means they can never be served, which is logged rather than left silent.")]
        public ItemRequest[] orders;

        [Tooltip("Real seconds at the table once they are handed what they asked for. Short: " +
                 "the meal is a beat, not a wait.")]
        public Vector2 eatSeconds = new Vector2(3f, 5f);

        [Tooltip("Coins paid when the order is filled. Nothing is paid for a visitor who is " +
                 "never served, because they never leave until they are.")]
        [Min(0)] public int payment = 25;

        [Header("Movement")]
        [Tooltip("Metres per second on foot, between the car and the chair.")]
        [Min(0.1f)] public float walkSpeed = 1.6f;

        [Min(1f)] public float turnSpeed = 540f;

        [Min(0.05f)] public float arriveRadius = 0.25f;

        [Header("Prefab")]
        [Tooltip("Built by 'Food Street / Visitors / Generate Visitor Content'. A prefab " +
                 "assigned by hand is respected and never overwritten.")]
        public GameObject prefab;

        /// <summary>
        /// Rolls one order.
        ///
        /// Returns an empty order rather than throwing when the table is missing or every
        /// weight is zero. A visitor who wants nothing sits down, shows no bubble and is
        /// reported once - which is a mistake somebody can see and fix, where an exception on
        /// the spawn path is a system that simply stops.
        /// </summary>
        public VisitorOrder RollOrder()
        {
            if (orders == null || orders.Length == 0) return default;

            float total = 0f;
            for (int i = 0; i < orders.Length; i++)
                if (IsUnlocked(orders[i].item)) total += Mathf.Max(0f, orders[i].weight);

            if (total <= 0f) return default;

            float pick = Random.value * total;

            for (int i = 0; i < orders.Length; i++)
            {
                if (!IsUnlocked(orders[i].item)) continue;
                pick -= Mathf.Max(0f, orders[i].weight);
                if (pick > 0f) continue;

                ItemRequest request = orders[i];

                int low = Mathf.Max(1, Mathf.Min(request.amountRange.x, request.amountRange.y));
                low = Mathf.Min(low, 2);
                int high = Mathf.Clamp(Mathf.Max(request.amountRange.x, request.amountRange.y), low, 2);

                return new VisitorOrder(request.item, Random.Range(low, high + 1));
            }

            return default;
        }

        public float RollEatSeconds() => Random.Range(eatSeconds.x, eatSeconds.y);

        static bool IsUnlocked(ItemType type) => !LastOrders.Economy.EconomyRules.TryDish(type, out var dish) || LevelUpSystem.GetLevel() >= dish.Level;

        void OnValidate()
        {
            eatSeconds.x = Mathf.Max(0.1f, eatSeconds.x);
            eatSeconds.y = Mathf.Max(eatSeconds.x, eatSeconds.y);
        }
    }
}
