using LastOrders.Consequence;
using LastOrders.Data;
using LastOrders.Perimeter;
using UnityEngine;

namespace LastOrders.Core
{
    /// <summary>
    /// Rebuilds the grid at runtime and puts the scene's authored objects back on it.
    ///
    /// This is needed because the build grid is a static field. A scene authored in the
    /// editor has its objects registered against the grid that existed at authoring time,
    /// and that grid is gone by the time the scene plays - domain reload clears it. The
    /// GameObjects survive in the scene, so without this the restaurant looks completely
    /// built and behaves as though the plot were empty: no seats, no walls, no doors, and a
    /// navigation layer that thinks every tile is open ground.
    ///
    /// Runs in Awake, which is before any VContainer entry point's Start, so the grid is
    /// populated before the composition root reads it.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SceneGridInitializer : MonoBehaviour
    {
        [SerializeField] GridBuildingSystem3D buildGrid;

        [Tooltip("The plot tier that decides the grid dimensions. Tier 1 is The Shack at 8x8.")]
        [SerializeField] PlotTierDefinition plotTier;

        [Tooltip("Parent of the scene-authored restaurant. Everything under here is " +
                 "re-registered onto the fresh grid.")]
        [SerializeField] Transform restaurantRoot;

        [SerializeField] float cellSize = 1f;

        /// <summary>How many authored objects were restored. Read by the debug overlay; a
        /// zero here with a visibly built restaurant is the signature of this having failed.</summary>
        public int RestoredCount { get; private set; }

        void Awake()
        {
            if (buildGrid == null) buildGrid = FindFirstObjectByType<GridBuildingSystem3D>();
            if (buildGrid == null)
            {
                Debug.LogError("[SceneGridInitializer] No GridBuildingSystem3D in the scene.", this);
                return;
            }

            int width = plotTier != null ? plotTier.gridWidth : 8;
            int height = plotTier != null ? plotTier.gridHeight : 8;

            buildGrid.Initialize(width, height, cellSize, Vector3.zero);

            RestoredCount = ReregisterAuthoredObjects();
        }

        int ReregisterAuthoredObjects()
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null) return 0;

            PlacedObject_Done[] authored = restaurantRoot != null
                ? restaurantRoot.GetComponentsInChildren<PlacedObject_Done>(true)
                : FindObjectsByType<PlacedObject_Done>(FindObjectsSortMode.None);

            int restored = 0;

            for (int i = 0; i < authored.Length; i++)
            {
                PlacedObject_Done placed = authored[i];
                if (placed == null || placed.PlacedObjectTypeSO == null) continue;

                var tiles = placed.GetGridPositionList();
                for (int t = 0; t < tiles.Count; t++)
                    grid.GetGridObject(tiles[t].x, tiles[t].y)?.SetPlacedObject(placed);

                EnsureRuntimeComponents(placed);
                restored++;
            }

            return restored;
        }

        /// <summary>
        /// Adds the components the consequence and perimeter systems expect.
        ///
        /// Done here as well as at authoring time because a prefab edited after the scene was
        /// built would otherwise come back missing them, and a wall with no
        /// DamageableStructure is a wall that silently cannot be attacked.
        /// </summary>
        static void EnsureRuntimeComponents(PlacedObject_Done placed)
        {
            PlacedObjectTypeSO type = placed.PlacedObjectTypeSO;

            if (placed.GetComponent<DamageableStructure>() == null)
                placed.gameObject.AddComponent<DamageableStructure>();

            if (type.isDoor && placed.GetComponent<DoorController>() == null)
                placed.gameObject.AddComponent<DoorController>();

            bool flammable = type.wallMaterial == null || type.wallMaterial.flammable;
            if (flammable && placed.GetComponent<StructureFire>() == null)
                placed.gameObject.AddComponent<StructureFire>();
        }
    }
}
