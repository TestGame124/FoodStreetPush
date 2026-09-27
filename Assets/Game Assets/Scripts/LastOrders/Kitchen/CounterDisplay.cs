using LastOrders.Core;
using UnityEngine;

namespace LastOrders.Kitchen
{
    /// <summary>
    /// Shows what is actually on the pass.
    ///
    /// Small, and not optional. Travellers only turn off the road when there is food to sell
    /// them, so "is there anything ready" is the single most important piece of state in the
    /// day loop - and a counter with four plates permanently sitting on it tells the player
    /// the opposite of the truth for most of the morning. A gate the player cannot see is a
    /// gate they experience as the game ignoring them.
    ///
    /// Authored plates switched on and off rather than pooled instances: the counter holds a
    /// fixed handful, and a pool for four cubes would be ceremony with no payoff.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    public sealed class CounterDisplay : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("One per serving the counter can show. More meals than this are real but " +
                 "not drawn, which is the right way round: the counter is a readout, not the " +
                 "stock itself.")]
        [SerializeField] GameObject[] servings;

        GameComposition composition;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                // Not an error. The counter is authored into the scene and is worth looking at
                // without the systems running.
                Show(0);
                return;
            }

            composition.Meals.OnPassChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (composition != null) composition.Meals.OnPassChanged -= Refresh;
        }

        void Refresh() => Show(composition != null ? composition.Meals.TotalReady : 0);

        void Show(int ready)
        {
            if (servings == null) return;

            for (int i = 0; i < servings.Length; i++)
            {
                if (servings[i] != null) servings[i].SetActive(i < ready);
            }
        }

        public void Configure(GameLifetimeScope lifetimeScope, GameObject[] plates)
        {
            scope = lifetimeScope;
            servings = plates;
        }
    }
}
