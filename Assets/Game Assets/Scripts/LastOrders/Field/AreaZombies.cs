using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.DayCycle;
using LastOrders.Expansion;
using LastOrders.Night;
using UnityEngine;

namespace LastOrders.Field
{
    /// <summary>
    /// The zombies standing on one expansion area: how many, where they amble, and how hard
    /// they are. Put it on the area, or anywhere under it.
    ///
    /// Each one has a stable ID from the area and its number, and a zombie killed once is never
    /// made again - recalling a worker halfway through the fight, or losing them to it, leaves
    /// the dead ones dead and the land that much closer to clear.
    ///
    /// By day they are out on the land; after dark they are gone, like the town's, and nobody
    /// is out there to meet them anyway. Once the land is claimed the new wall is round it and
    /// anything left standing goes with the old one.
    /// </summary>
    public sealed class AreaZombies : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("The area they stand on. The one this is under when left empty.")]
        [SerializeField] ExpansionArea area;

        [Tooltip("One zombie body with no agent on it: the town's wanderer.")]
        [SerializeField] GameObject zombiePrefab;

        [Tooltip("Where they amble: the middle of the patch, world space.")]
        [SerializeField] Vector3 patchCentre;

        [Tooltip("Width (X) and depth (Z) of the patch in metres. Keep it inside the land, clear " +
                 "of the wall gap, so none of them wanders into the compound.")]
        [SerializeField] Vector2 patchSize = new Vector2(10f, 6f);

        [Min(0)] [SerializeField] int count = 4;

        [Header("Each zombie")]
        [Tooltip("A worker at 10 a blow takes five blows, a little over three seconds, to put one down.")]
        [SerializeField] float health = 50f;

        [Tooltip("Per hit. At 6 every 2 seconds, all four on one fresh worker at once leave them " +
                 "just standing at the end - and past the warning line with two still up, several " +
                 "seconds before they would go down. Met one or two at a time, they never get close.")]
        [SerializeField] float damage = 6f;

        [SerializeField] float attackInterval = 2f;
        [SerializeField] float attackRange = 1.2f;

        [Tooltip("How close a worker has to come before they notice.")]
        [SerializeField] float aggroRadius = 7f;

        [SerializeField] float chaseSpeed = 1.4f;
        [SerializeField] Vector2 wanderSpeed = new Vector2(0.35f, 0.7f);
        [SerializeField] Vector2 wanderPause = new Vector2(1.5f, 5f);

        [Tooltip("Metres past the patch edge they will follow somebody before giving up.")]
        [SerializeField] float leash = 1.5f;

        [Tooltip("Their skin, multiplied over the character art. The same hue as the town's zombies.")]
        [SerializeField] Color tint = new Color(0.55f, 0.65f, 0.45f);

        readonly List<AreaZombie> zombies = new List<AreaZombie>();

        GameComposition composition;
        string areaId;

        public int Alive
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < zombies.Count; i++)
                    if (zombies[i] != null && zombies[i].IsAlive) alive++;

                return alive;
            }
        }

        Rect Patch => new Rect(patchCentre.x - patchSize.x * 0.5f, patchCentre.z - patchSize.y * 0.5f, patchSize.x, patchSize.y);

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;
            if (area == null) area = GetComponentInParent<ExpansionArea>();

            if (composition == null || area == null || string.IsNullOrEmpty(area.AreaId))
            {
                Debug.LogError($"[AreaZombies] {name} needs a GameLifetimeScope and an ExpansionArea with a " +
                               "definition, so no zombies stand on the land.", this);
                enabled = false;
                return;
            }

            if (zombiePrefab == null)
            {
                Debug.LogError($"[AreaZombies] {name} has no zombie prefab. Run 'Last Orders / Expansion / " +
                               "Set Up Area Clearing (Current Scene)'.", this);
                enabled = false;
                return;
            }

            areaId = area.AreaId;

            AreaState state = composition.Expansion.StateOf(areaId);
            if (state != AreaState.Claimed && state != AreaState.Expanding) Spawn();

            composition.Machine.OnPhaseChanged += HandlePhaseChanged;
            composition.Expansion.OnAreaStateChanged += HandleAreaStateChanged;

            Show(composition.Machine.CurrentPhase);
        }

        void OnDestroy()
        {
            if (composition == null) return;

            composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
            composition.Expansion.OnAreaStateChanged -= HandleAreaStateChanged;
        }

        void Spawn()
        {
            var block = new MaterialPropertyBlock();
            Rect patch = Patch;

            var numbers = new AreaZombie.Tuning
            {
                Health = health,
                Damage = damage,
                AttackInterval = attackInterval,
                AttackRange = attackRange,
                AggroRadius = aggroRadius,
                ChaseSpeed = chaseSpeed,
                WanderSpeed = wanderSpeed,
                WanderPause = wanderPause,
                Patch = patch,
                Leash = leash,
            };

            for (int i = 0; i < count; i++)
            {
                string id = $"{areaId}/zombie_{i + 1}";
                if (composition.Field.IsKilled(id)) continue;

                // Spread along the patch rather than bunched: one wanders near the gap first,
                // and the fight comes a zombie or two at a time unless the player waits.
                float along = count > 1 ? (i + 0.5f) / count : 0.5f;
                Vector3 at = new Vector3(Mathf.Lerp(patch.xMin, patch.xMax, along), patchCentre.y,
                                         Random.Range(patch.yMin, patch.yMax));

                GameObject body = Instantiate(zombiePrefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                body.name = $"AreaZombie_{i + 1}";
                TownZombies.Tint(body, tint, block);

                AreaZombie zombie = body.AddComponent<AreaZombie>();
                zombie.Configure(composition, id, areaId, numbers);
                zombies.Add(zombie);
            }
        }

        void HandlePhaseChanged(DayPhase previous, DayPhase current) => Show(current);

        /// <summary>Out by day and at dusk; gone at night and through the tally.</summary>
        void Show(DayPhase phase)
        {
            bool show = phase != DayPhase.Night && phase != DayPhase.Resolution;

            for (int i = 0; i < zombies.Count; i++)
            {
                AreaZombie zombie = zombies[i];
                if (zombie == null || !zombie.IsAlive) continue;

                if (zombie.gameObject.activeSelf != show) zombie.gameObject.SetActive(show);
            }
        }

        void HandleAreaStateChanged(string id, AreaState state)
        {
            if (id != areaId || state != AreaState.Claimed && state != AreaState.Expanding) return;

            for (int i = 0; i < zombies.Count; i++)
                if (zombies[i] != null && zombies[i].IsAlive) zombies[i].Vanish();
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, ExpansionArea land, GameObject prefab,
                              Vector3 centre, Vector2 size, int howMany)
        {
            scope = lifetimeScope;
            area = land;
            zombiePrefab = prefab;
            patchCentre = centre;
            patchSize = size;
            count = howMany;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(patchCentre + Vector3.up * 0.1f, new Vector3(patchSize.x, 0.2f, patchSize.y));

            Gizmos.color = new Color(0.8f, 0.3f, 0.3f, 0.3f);
            Gizmos.DrawWireCube(patchCentre + Vector3.up * 0.1f,
                                new Vector3(patchSize.x + leash * 2f, 0.2f, patchSize.y + leash * 2f));
        }
    }
}
