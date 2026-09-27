using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Interaction;
using LastOrders.Jobs;
using LastOrders.Progression;
using UnityEngine;

namespace LastOrders.Kitchen
{
    /// <summary>
    /// The stove, as something the player can tap.
    ///
    /// Deliberately the smallest kitchen that makes the customer loop real: tap it, the Chef
    /// walks over, a bowl of soup appears on the pass, and travellers now have a reason to
    /// turn off the road. Cooking as a system - stations, queues, signature dishes, spoilage -
    /// is a separate piece of work, and building a speculative version of it here would make
    /// that work harder rather than easier.
    ///
    /// What it does own properly is the two things the rest of the game reads: the meal count
    /// the door is gated on, and the one-time flag that says the first soup has been cooked,
    /// which is half of what unlocks the sign.
    /// </summary>
    public sealed class DinerKitchen : MonoBehaviour, ITappable, IInteractableObject
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("Stable identity for the job queue, so a second tap says the work is already " +
                 "in hand rather than queueing it twice.")]
        [SerializeField] string targetId = "K1_stove";

        [Tooltip("The house soup. The cheapest unlocked recipe when left empty.")]
        [SerializeField] RecipeDefinition recipe;

        [Tooltip("What lands on the pass. Required: a grey-box level has no item kitchen of " +
                 "its own, so this is the only thing that says what a cooked meal IS.")]
        [SerializeField] ItemDataSO dish;

        [Tooltip("Seconds of work after travel.")]
        [SerializeField] float cookSeconds = 6f;

        [Tooltip("Where the Chef stands. In the kitchen circulation band, not inside the " +
                 "stove's own collision.")]
        [SerializeField] Transform interactionPoint;

        [SerializeField] string displayName = "Stove";

        GameComposition composition;
        Job job;

        public string DisplayName => displayName;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[DinerKitchen] {name} found no GameLifetimeScope.", this);
                return;
            }

            if (recipe == null) recipe = CheapestUnlockedRecipe();
        }


        /// <summary>
        /// The same tap, arriving from the project's own Mouse3D instead of the grey-box
        /// selector. Both input layers end up in the same method; only the plumbing differs.
        /// </summary>
        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (composition == null) return "The game systems are not running.";

            if (!composition.Progression.Has(ProgressionFlags.EntranceCleared))
                return "Clear the entrance first. Nobody can reach the door.";

            if (recipe == null) return "No recipe. The menu is empty.";

            if (job != null && !job.IsFinished)
            {
                return job.State == JobState.Working
                    ? $"Cooking {recipe.displayName}... {job.Progress:P0}"
                    : "Chef is on the way to the stove.";
            }

            if (!composition.Ledger.CanAfford(recipe.ingredients))
                return $"Not enough for {recipe.displayName}. {Shortfall()}";

            Vector3 point = interactionPoint != null ? interactionPoint.position : WorkPoint();

            job = composition.Jobs.Queue(targetId, transform, WorkerRole.Chef, point,
                                         cookSeconds, $"Cook {recipe.displayName}");

            job.OnCompleted += HandleCooked;
            return $"Chef is cooking {recipe.displayName}.";
        }

        void HandleCooked(Job completed)
        {
            completed.OnCompleted -= HandleCooked;

            // Paid at completion rather than at the tap. A cook cancelled by a phase change
            // should cost nothing, and charging up front would make cancellation a way to
            // lose ingredients rather than a way to change your mind.
            if (!composition.Ledger.TrySpend(recipe.ingredients)) return;

            if (dish == null)
            {
                Debug.LogWarning("[DinerKitchen] No dish assigned, so the cook produced " +
                                 "nothing. Assign one on this component.", this);
                return;
            }

            // Added rather than noted: a grey-box level has no inventory for the dish to land
            // in, so the pass has to hold it itself.
            composition.Meals.Add(dish.Type);

            // Grilled and smoked dishes pass double, which is what makes the menu itself a
            // risk decision rather than a pure upgrade path.
            composition.Heat.AddDishCooked(recipe.heatMultiplier);

            composition.Progression.Set(ProgressionFlags.FirstMealCooked);
        }

        /// <summary>
        /// The cheapest dish available from the start.
        ///
        /// Chosen rather than named so the kitchen keeps working if the recipe set is
        /// regenerated with different names - which it will be, because the recipe table comes
        /// from a design document that is still moving.
        /// </summary>
        RecipeDefinition CheapestUnlockedRecipe()
        {
            RecipeDefinition[] all = composition.Balance.recipes;
            if (all == null) return null;

            RecipeDefinition best = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].unlockedFromStart) continue;
                if (best == null || all[i].coinValue < best.coinValue) best = all[i];
            }

            return best;
        }

        string Shortfall()
        {
            if (recipe.ingredients == null) return string.Empty;

            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                ResourceAmount need = recipe.ingredients[i];
                int have = composition.Ledger.Get(need.type);

                if (have < need.amount) return $"Needs {need.amount} {need.type}, have {have}.";
            }

            return string.Empty;
        }

        /// <summary>Stand on the aisle side. The kitchen band runs along Z 24, south of the
        /// stations, which is also the side the player is looking from.</summary>
        Vector3 WorkPoint() => transform.position + new Vector3(0f, 0f, -1.2f);

        public void Configure(GameLifetimeScope lifetimeScope, RecipeDefinition houseRecipe,
                              ItemDataSO servedDish, Transform standAt)
        {
            scope = lifetimeScope;
            recipe = houseRecipe;
            dish = servedDish;
            interactionPoint = standAt;
        }
    }
}
