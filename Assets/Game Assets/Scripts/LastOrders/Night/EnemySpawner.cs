using System.Collections.Generic;
using FoodStreet.Navigation;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.DayCycle.States;
using UnityEngine;

namespace LastOrders.Night
{
    /// <summary>
    /// Turns the night's schedule into agents on the map.
    ///
    /// NightState decides what arrives and when; this decides where from and takes the
    /// instances out of the pool. Splitting it this way keeps the schedule testable without
    /// a scene, and means the lane geometry can change without touching the wave logic.
    ///
    /// Spawn positions come from the nav grid's own bounds rather than from authored markers,
    /// so the approach lanes stay correct through all five plot expansions (6.2) without
    /// anyone having to remember to move a transform.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] GameLifetimeScope scope;

        [Tooltip("How far outside the plot edge enemies appear. Far enough that they walk in " +
                 "rather than materialise on the perimeter.")]
        [SerializeField] float spawnMargin = 3f;

        [Tooltip("Instances kept ready per archetype. A wave should never instantiate mid-siege.")]
        [SerializeField] int poolPrewarm = 16;

        [Tooltip("Authored spawn points, indexed by lane. When a lane has an entry here it " +
                 "wins over the procedural placement below. A measured site plan puts the " +
                 "approach where the designer drew it, not where the grid's bounding box " +
                 "happens to fall.")]
        [SerializeField] Transform[] laneOverrides;

        [Tooltip("Metres either side of a lane's point that each enemy may appear, across the way it " +
                 "comes in. Zero: every one from the point itself, so they all reach the wall at one " +
                 "place. Wider: a lane is a side the night comes from, not a single path, and the " +
                 "whole length of that wall is in play.")]
        [Min(0f)] [SerializeField] float laneSpread;

        [Tooltip("The warning over the road a few seconds before a group arrives.")]
        [SerializeField] Color warningColour = new Color(1f, 0.4f, 0.3f);

        [Tooltip("Metres above the lane the warning floats, so it clears the scenery by the road.")]
        [SerializeField] float warningHeight = 2.5f;

        GameComposition composition;
        NightState night;

        // Which lane each zombie out tonight came up, for the HUD's approach alerts. Pooled agents
        // are written over when they come out again, and dropped when they go back.
        readonly Dictionary<EnemyAgent, int> laneOf = new Dictionary<EnemyAgent, int>();

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();

            composition = scope != null ? scope.Composition : null;
            if (composition == null)
            {
                Debug.LogError("[EnemySpawner] No GameLifetimeScope found. Nothing will spawn.", this);
                enabled = false;
                return;
            }

            night = composition.Night;
            night.OnSpawnRequested += HandleSpawnRequested;
            night.OnGroupIncoming += HandleGroupIncoming;
            night.OnRetreat += HandleRetreat;

            // The attacks are planned against the sides this spawner can actually send them from.
            DescribeLanes();
        }

        void OnDestroy()
        {
            if (night == null) return;

            night.OnSpawnRequested -= HandleSpawnRequested;
            night.OnGroupIncoming -= HandleGroupIncoming;
            night.OnRetreat -= HandleRetreat;
        }

        /// <summary>
        /// Tells the attack cycle how many sides there are and what to call them. An authored lane's
        /// name says where it is - "Lane1_West" is the west - which is what the warnings read out.
        /// </summary>
        void DescribeLanes()
        {
            if (composition.Encounters == null) return;

            int authored = 0;
            if (laneOverrides != null)
                for (int i = 0; i < laneOverrides.Length; i++)
                    if (laneOverrides[i] != null) authored = i + 1;

            if (authored == 0) return;

            var names = new string[authored];
            for (int i = 0; i < authored; i++)
            {
                Transform lane = laneOverrides[i];
                if (lane == null) continue;

                string label = lane.name;
                int underscore = label.LastIndexOf('_');
                names[i] = underscore >= 0 && underscore < label.Length - 1 ? label.Substring(underscore + 1) : label;
            }

            composition.Encounters.LaneCount = authored;
            composition.Encounters.LaneNames = names;
        }

        /// <summary>Where a lane's zombies come in: the breach they appear at.</summary>
        public Vector3 LanePoint(int lane) => LanePosition(lane);

        /// <summary>The lane <paramref name="agent"/> came up, if this spawner sent it.</summary>
        public bool TryGetLane(EnemyAgent agent, out int lane)
        {
            lane = -1;
            return agent != null && laneOf.TryGetValue(agent, out lane);
        }

        /// <summary>Morning: whatever is still standing turns its back on the restaurant and goes.</summary>
        void HandleRetreat(System.Collections.Generic.IReadOnlyList<EnemyAgent> leaving)
        {
            Vector3 home = RestaurantCentre();

            for (int i = 0; i < leaving.Count; i++)
                if (leaving[i] != null && leaving[i].isActiveAndEnabled) leaving[i].Retreat(home);
        }

        /// <summary>What every zombie is ultimately after: the restaurant's door, or its middle before
        /// the door has been measured.</summary>
        Vector3 Objective()
        {
            var restaurant = composition.Restaurant;
            return restaurant != null && restaurant.HasEntrance ? restaurant.Entrance : RestaurantCentre();
        }

        Vector3 RestaurantCentre()
        {
            var restaurant = composition.Restaurant;
            if (restaurant != null && restaurant.HasFootprint)
                return new Vector3(restaurant.Footprint.center.x, 0f, restaurant.Footprint.center.y);

            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Grid == null) return transform.position;

            NavGrid grid = nav.Grid.Grid;
            return grid.Origin + new Vector3(grid.BuildWidth * grid.BuildCellSize, 0f, grid.BuildHeight * grid.BuildCellSize) * 0.5f;
        }

        /// <summary>The warning, placed where they will appear: the player looks at the road
        /// before anything is on it.</summary>
        void HandleGroupIncoming(WaveGroup group)
        {
            Vector3 at = LanePosition(group.Lane) + Vector3.up * warningHeight;
            composition.WorldUi.ShowFloatingText(at, $"{group.Label} coming!", warningColour);
        }

        void HandleSpawnRequested(EnemyDefinition definition, int lane)
        {
            if (definition == null) return;

            if (definition.prefab == null)
            {
                // Loud, because the alternative is a night where the wave director reports
                // spawns, the debug overlay counts them, and nothing appears on screen.
                Debug.LogError(
                    $"[EnemySpawner] '{definition.displayName}' has no prefab assigned. " +
                    "Run 'Last Orders / Generate Starting Content' to build the grey-box prefabs.",
                    definition);
                return;
            }

            var agentPrefab = definition.prefab.GetComponent<EnemyAgent>();
            if (agentPrefab == null)
            {
                Debug.LogError(
                    $"[EnemySpawner] '{definition.prefab.name}' has no EnemyAgent component.",
                    definition.prefab);
                return;
            }

            Vector3 position = Spread(LanePosition(lane));

            EnemyAgent agent = composition.Pools.Take(
                agentPrefab, prewarm: Mathf.Max(poolPrewarm, definition.poolPrewarm), maxSize: 256);

            if (agent == null) return;

            agent.Configure(definition, position);
            if (definition.tintBody) Tint(agent, definition.silhouetteColour);

            // The horde decides whether it hunts the defenses, smashes the yard once over the wall,
            // or keeps on for the restaurant - and lets go of it all once the night's caps are reached.
            // Asked through the noise maker, which overrides it for any zombie in earshot while it sounds.
            EncounterTuning tuning = composition.Encounters != null ? composition.Encounters.Tuning : null;
            agent.ConfigureOrders(composition.ZombieOrders,
                                  tuning != null ? Mathf.Max(tuning.raidGiveUpSeconds, tuning.Targeting.giveUpSeconds) : 20f);
            composition.Horde.Enlist(agent, definition);

            // Wherever the flow field cannot say, it makes for the restaurant's door.
            agent.SetObjective(Objective());

            laneOf[agent] = lane;
            night.Register(agent);

            // Released back to the pool once the body has fallen and sunk, rather than destroyed.
            // The subscription is cleared in the agent's own reset path, so a pooled instance
            // cannot carry this handler into its next life.
            agent.OnFallen += HandleFallen;
        }

        void HandleFallen(EnemyAgent agent)
        {
            agent.OnFallen -= HandleFallen;
            laneOf.Remove(agent);
            composition.Pools.Return(agent);
        }

        /// <summary>
        /// Places a lane evenly around the plot's perimeter, outside it.
        ///
        /// Lanes are telegraphed during the 45-second dusk warning, so what matters is that
        /// the same lane index always means the same direction - a player who repositioned
        /// against an arrow must find the threat where the arrow said.
        /// </summary>
        Vector3 LanePosition(int lane)
        {
            // An authored point wins outright. The procedural fallback derives a radius from
            // the grid's longest side, which is right for a square plot growing through its
            // tiers and wrong for a wide authored world - on a 48 x 40 m map it places lane 0
            // several metres outside the map entirely.
            if (laneOverrides != null && lane >= 0 && lane < laneOverrides.Length
                && laneOverrides[lane] != null)
            {
                return laneOverrides[lane].position;
            }

            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Grid == null) return transform.position;

            NavGrid grid = nav.Grid.Grid;

            float width = grid.BuildWidth * grid.BuildCellSize;
            float height = grid.BuildHeight * grid.BuildCellSize;
            Vector3 origin = grid.Origin;
            Vector3 centre = origin + new Vector3(width * 0.5f, 0f, height * 0.5f);

            int lanes = Mathf.Max(1, composition.Context.TonightsWave?.LaneCount ?? 1);
            float angle = (lane % lanes) / (float)lanes * Mathf.PI * 2f;

            // The road is north (+Z), and 9.1 makes the point that the thing which feeds the
            // restaurant feeds the horde too. Lane 0 therefore starts on the road.
            float radius = Mathf.Max(width, height) * 0.5f + spawnMargin;

            return centre + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }

        /// <summary>
        /// A spot up to <see cref="laneSpread"/> either side of <paramref name="point"/>, across the
        /// line from it to the middle of the plot - along the edge the lane comes in from. Each enemy
        /// then walks straight in from its own spot, so a group fans out along the wall it reaches.
        /// </summary>
        Vector3 Spread(Vector3 point)
        {
            if (laneSpread <= 0f) return point;

            NavigationSystem nav = NavigationSystem.Instance;
            if (nav == null || nav.Grid == null) return point;

            NavGrid grid = nav.Grid.Grid;
            Vector3 centre = grid.Origin + new Vector3(grid.BuildWidth * grid.BuildCellSize, 0f, grid.BuildHeight * grid.BuildCellSize) * 0.5f;

            Vector3 inward = centre - point;
            inward.y = 0f;
            if (inward.sqrMagnitude < 1e-4f) return point;

            Vector3 across = Vector3.Cross(Vector3.up, inward.normalized);
            return point + across * Random.Range(-laneSpread, laneSpread);
        }

        /// <summary>
        /// Applies the archetype's hue to the instance.
        ///
        /// 14 requires every archetype be identifiable by silhouette and hue alone at roughly
        /// 40px. Until there are eight distinct models, the hue is doing all of that work, so
        /// it is applied per spawn rather than baked into a prefab.
        /// </summary>
        static void Tint(Component instance, Color colour)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                // The blob shadow is a sprite. Tinted, it is a green puddle under every zombie.
                if (renderers[i] is SpriteRenderer) continue;

                // MaterialPropertyBlock rather than .material, which would instantiate a new
                // material per agent and defeat batching at 120 concurrent enemies.
                var block = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, colour);
                block.SetColor(ColorId, colour);
                renderers[i].SetPropertyBlock(block);
            }
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
    }
}
