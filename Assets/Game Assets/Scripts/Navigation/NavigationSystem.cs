using System;
using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Navigation
{
    /// <summary>
    /// Owns the navigation stack and decides when it is allowed to do work.
    ///
    /// Structural edits mark tiles dirty as they happen; this component folds them in at
    /// <see cref="regenerationsPerSecond"/> and no faster. Dragging out twenty floor tiles
    /// fires twenty change events and costs one rebuild, which is the whole reason the
    /// dirty flag exists rather than rebuilding at the call site.
    ///
    /// Also holds the agent registry that the steering layer's separation reads from.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationSystem : MonoBehaviour
    {
        public static NavigationSystem Instance { get; private set; }

        [Header("Regeneration")]
        [Tooltip("Upper bound on grid + flow field rebuilds per second. Structural changes " +
                 "queue up between ticks instead of each paying for its own rebuild.")]
        [SerializeField, Range(1f, 20f)] float regenerationsPerSecond = 5f;

        [Header("Separation")]
        [Tooltip("Side of one spatial-hash bucket, in world units. Wants to be a little " +
                 "larger than the distance agents actually push each other at.")]
        [SerializeField] float separationCellSize = 1.5f;

        public GridService Grid { get; private set; }
        public AStarPathfinder Pathfinder { get; private set; }

        /// <summary>Raised after a rebuild that changed the grid. Followers listening to
        /// this repath instead of polling.</summary>
        public event Action OnGridChanged;

        readonly Dictionary<FlowFieldId, FlowField> fields = new Dictionary<FlowFieldId, FlowField>();
        readonly Dictionary<FlowFieldId, List<Vector2Int>> seeds = new Dictionary<FlowFieldId, List<Vector2Int>>();
        readonly HashSet<FlowFieldId> staleFields = new HashSet<FlowFieldId>();

        /// <summary>Fields whose seeds came from gameplay rather than from the grid.
        /// <see cref="RefreshDerivedSeeds"/> leaves these alone.</summary>
        readonly HashSet<FlowFieldId> externallySeeded = new HashSet<FlowFieldId>();

        readonly List<SteeringAgent> agents = new List<SteeringAgent>();
        readonly Dictionary<long, List<SteeringAgent>> buckets = new Dictionary<long, List<SteeringAgent>>();
        readonly Stack<List<SteeringAgent>> bucketPool = new Stack<List<SteeringAgent>>();

        GridXZ<GridBuildingSystem3D.GridObject> source;
        float regenTimer;
        bool initialized;

        void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Finds the system in the scene, or creates one, and initialises it.
        ///
        /// Created rather than required in every scene because there are six level scenes
        /// and nothing about this component needs authoring — it reads its whole world from
        /// the build grid. Call once, after <see cref="GridBuildingSystem3D.Initialize"/>
        /// and before anything places an object, or the first edits go unheard.
        /// </summary>
        public static NavigationSystem Bootstrap()
        {
            NavigationSystem system = Instance;
            if (system == null) system = FindFirstObjectByType<NavigationSystem>();
            if (system == null)
            {
                GameObject host = new GameObject("NavigationSystem");
                system = host.AddComponent<NavigationSystem>();
            }

            Instance = system;
            system.Initialize();
            return system;
        }

        void OnDestroy()
        {
            if (source != null) source.OnGridObjectChanged -= HandleGridObjectChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Must run after <see cref="GridBuildingSystem3D.Initialize"/> — the build grid has
        /// to exist before its dimensions can be mirrored.
        /// </summary>
        public void Initialize()
        {
            source = GridBuildingSystem3D.GetGrid();
            if (source == null)
            {
                Debug.LogError("[NavigationSystem] Build grid missing. Initialize it first.");
                return;
            }

            NavGrid navGrid = new NavGrid(
                source.GetWidth(),
                source.GetHeight(),
                source.GetCellSize(),
                source.GetOriginPosition());

            Grid = new GridService(navGrid, source);
            Pathfinder = new AStarPathfinder(Grid);

            foreach (FlowFieldId id in (FlowFieldId[])Enum.GetValues(typeof(FlowFieldId)))
            {
                fields[id] = new FlowField(id, Grid);
                seeds[id] = new List<Vector2Int>();
                staleFields.Add(id);
            }

            source.OnGridObjectChanged += HandleGridObjectChanged;

            initialized = true;
            RefreshDerivedSeeds();
            RegenerateStaleFields();
        }

        void HandleGridObjectChanged(object sender, GridXZ<GridBuildingSystem3D.GridObject>.OnGridObjectChangedEventArgs e)
        {
            Grid?.MarkTileDirty(e.x, e.z);
        }

        void Update()
        {
            if (!initialized) return;

            RebuildIfDue();
            RebuildSpatialHash();
        }

        void RebuildIfDue()
        {
            if (!Grid.HasDirtyTiles && staleFields.Count == 0) return;

            regenTimer -= Time.deltaTime;
            if (regenTimer > 0f) return;
            regenTimer = 1f / Mathf.Max(regenerationsPerSecond, 0.01f);

            if (Grid.Rebuild())
            {
                RefreshDerivedSeeds();

                // Every field is generated against a grid version; a structural change
                // invalidates all of them at once.
                foreach (FlowFieldId id in fields.Keys) staleFields.Add(id);

                OnGridChanged?.Invoke();
            }

            RegenerateStaleFields();
        }

        #region Flow fields

        public FlowField GetFlowField(FlowFieldId id)
        {
            if (!initialized) return null;
            return fields.TryGetValue(id, out FlowField field) ? field : null;
        }

        /// <summary>
        /// Points a field at a new set of objectives. Seeds are build-tile coordinates.
        /// The regeneration happens on the next tick, not here.
        /// </summary>
        public void SetFlowFieldSeeds(FlowFieldId id, IEnumerable<Vector2Int> tiles)
        {
            if (!initialized) return;

            List<Vector2Int> list = seeds[id];
            list.Clear();
            if (tiles != null) list.AddRange(tiles);

            externallySeeded.Add(id);
            staleFields.Add(id);
        }

        /// <summary>Convenience for callers holding world positions rather than tiles.</summary>
        public void SetFlowFieldTargets(FlowFieldId id, IEnumerable<Vector3> worldPositions)
        {
            if (!initialized) return;

            List<Vector2Int> list = seeds[id];
            list.Clear();

            if (worldPositions != null)
            {
                foreach (Vector3 world in worldPositions)
                {
                    Vector2Int tile = Grid.Grid.WorldToBuildTile(world);
                    if (Grid.Grid.BuildTileInBounds(tile.x, tile.y)) list.Add(tile);
                }
            }

            externallySeeded.Add(id);
            staleFields.Add(id);
        }

        void RegenerateStaleFields()
        {
            if (staleFields.Count == 0) return;

            foreach (FlowFieldId id in staleFields)
            {
                fields[id].Generate(seeds[id]);
            }
            staleFields.Clear();
        }

        static readonly List<Vector2Int> scratchTiles = new List<Vector2Int>(64);

        /// <summary>
        /// Re-derives the seeds the grid can work out for itself: doorways and the stores.
        /// The attack fields (<see cref="FlowFieldId.Breach"/>,
        /// <see cref="FlowFieldId.WeakestPoint"/>) are left to whatever set them — they
        /// point at an objective the grid has no opinion about — but they fall back to the
        /// pantry so they are never empty.
        /// </summary>
        void RefreshDerivedSeeds()
        {
            if (!externallySeeded.Contains(FlowFieldId.Doors))
            {
                CollectTiles(ObjectType.Door, scratchTiles);
                SetSeedsInternal(FlowFieldId.Doors, scratchTiles);
            }

            CollectTiles(ObjectType.Inventory, scratchTiles);

            if (!externallySeeded.Contains(FlowFieldId.Pantry))
                SetSeedsInternal(FlowFieldId.Pantry, scratchTiles);
            if (!externallySeeded.Contains(FlowFieldId.Breach))
                SetSeedsInternal(FlowFieldId.Breach, scratchTiles);
            if (!externallySeeded.Contains(FlowFieldId.WeakestPoint))
                SetSeedsInternal(FlowFieldId.WeakestPoint, scratchTiles);
        }

        void SetSeedsInternal(FlowFieldId id, List<Vector2Int> tiles)
        {
            List<Vector2Int> list = seeds[id];
            list.Clear();
            list.AddRange(tiles);
            staleFields.Add(id);
        }

        void CollectTiles(ObjectType type, List<Vector2Int> into)
        {
            into.Clear();

            NavGrid navGrid = Grid.Grid;
            for (int tz = 0; tz < navGrid.BuildHeight; tz++)
            {
                for (int tx = 0; tx < navGrid.BuildWidth; tx++)
                {
                    Vector2Int cell = navGrid.BuildTileToCellOrigin(tx, tz);
                    PlacedObject_Done placed = Grid.GetOccupant(cell.x, cell.y);
                    if (placed == null) continue;
                    if (placed.shopItemType != type) continue;

                    into.Add(new Vector2Int(tx, tz));
                }
            }
        }

        #endregion

        #region Agent registry and separation

        public static void RegisterAgent(SteeringAgent agent)
        {
            if (Instance == null) return;
            if (Instance.agents.Contains(agent)) return;
            Instance.agents.Add(agent);
        }

        public static void UnregisterAgent(SteeringAgent agent)
        {
            Instance?.agents.Remove(agent);
        }

        void RebuildSpatialHash()
        {
            foreach (List<SteeringAgent> bucket in buckets.Values)
            {
                bucket.Clear();
                bucketPool.Push(bucket);
            }
            buckets.Clear();

            for (int i = 0; i < agents.Count; i++)
            {
                SteeringAgent agent = agents[i];
                if (agent == null) continue;

                long key = BucketKey(agent.transform.position);
                if (!buckets.TryGetValue(key, out List<SteeringAgent> bucket))
                {
                    bucket = bucketPool.Count > 0 ? bucketPool.Pop() : new List<SteeringAgent>(8);
                    buckets[key] = bucket;
                }
                bucket.Add(agent);
            }
        }

        long BucketKey(Vector3 world)
        {
            int x = Mathf.FloorToInt(world.x / separationCellSize);
            int z = Mathf.FloorToInt(world.z / separationCellSize);
            return ((long)x << 32) ^ (uint)z;
        }

        /// <summary>
        /// Appends every registered agent in the nine buckets around <paramref name="world"/>,
        /// which is a superset of everything within <see cref="separationCellSize"/>.
        /// The caller does the distance test; this only narrows the candidates.
        /// </summary>
        public void CollectNeighbours(Vector3 world, List<SteeringAgent> into)
        {
            into.Clear();

            int baseX = Mathf.FloorToInt(world.x / separationCellSize);
            int baseZ = Mathf.FloorToInt(world.z / separationCellSize);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    long key = ((long)(baseX + dx) << 32) ^ (uint)(baseZ + dz);
                    if (buckets.TryGetValue(key, out List<SteeringAgent> bucket))
                    {
                        into.AddRange(bucket);
                    }
                }
            }
        }

        #endregion
    }
}
