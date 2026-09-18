using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// A dish. 8 keeps the kitchen deliberately shallow: this is a builder, not Cooking
    /// Fever, and depth here would compete with the systems that differentiate the game.
    ///
    /// The one real decision a recipe carries is its Heat. Grilled and smoked dishes are
    /// worth more and generate double Heat, which makes the menu itself a risk decision.
    /// A player running a salad bar is safe and poor.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Recipe", fileName = "Recipe_")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Grilled Skewers";
        public Sprite icon;

        [Header("Cost")]
        [Tooltip("Ingredients consumed. Drawn from the five of 8: Meat, Grain, Veg, Spice, Luxury.")]
        public ResourceAmount[] ingredients;

        [Tooltip("Real seconds to cook when the station is staffed. Stations cook automatically.")]
        public float cookSeconds = 12f;

        [Header("Value")]
        public int coinValue = 20;

        [Tooltip("Reputation weight. Better dishes pull better clientele.")]
        public float reputationWeight = 1f;

        [Header("Heat - the menu is a risk decision (8)")]
        [Tooltip("Multiplies the per-dish Heat. Grilled and smoked dishes are 2.")]
        public float heatMultiplier = 1f;

        [Header("Signature Dish")]
        [Tooltip("Eligible for the roughly 4-second tap-timing prompt that doubles the dish's " +
                 "value. Fully skippable: it rewards attention without demanding it.")]
        public bool allowsSignature = true;

        public float signatureMultiplier = 2f;

        [Header("Unlock")]
        [Tooltip("Recipes are unlocked, never crafted (8). Once earned they are banked " +
                 "permanently and can never be lost (15.1).")]
        public bool unlockedFromStart;

        public int requiredReputation;

        [Tooltip("Which station types can cook this. Empty means any.")]
        public string[] stationTags;
    }
}
