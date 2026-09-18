using System.Collections.Generic;
using System.IO;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds a playable Last Orders scene and the Tier 1 starting area.
    ///
    /// Generated rather than hand-authored because a .unity file is a poor thing to review:
    /// the layout below reads as a grid of characters, which is how it is actually designed,
    /// and the whole scene can be regenerated after any change to the systems it wires up.
    ///
    /// The starting area is The Shack from 6.2 - an 8x8 plot, one door, one Hearth. It is
    /// deliberately not a good layout. It has a single door on the north wall facing the
    /// road, which is the arrangement the player has to discover is a problem.
    /// </summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/LastOrders_Start.unity";
        const string BalancePath = "Assets/Game Assets/Content/LastOrders/GameBalance.asset";

        /// <summary>
        /// The Shack, 8x8 (GDD 6.2 Tier 1). North is +Z, toward the road.
        ///
        /// W wall   D door   H hearth (indestructible)   T table
        /// P pantry   C cellar   G garden plot   .  open floor
        /// o  continuation of the multi-tile object whose origin is below-left of it
        ///
        /// The 'o' cells matter: a Table is 3x3 in this project, not 1x1, and an origin sits
        /// at the minimum corner of its footprint. Writing the footprint out means the layout
        /// below is what the plot actually looks like rather than a diagram that happens to
        /// place one table and silently drop three.
        ///
        /// Two 4-tops fill the entire north half of the interior. That is the whole point of
        /// Tier 1: eight seats and a single door, with no room left for defense. The player
        /// is supposed to feel the plot is too small before they are allowed to expand.
        /// </summary>
        static readonly string[] Layout =
        {
            "WWWDWWWW", // z = 7  north, the road side. The one door faces the highway.
            "WooooooW", // z = 6  table footprints
            "WooooooW", // z = 5  table footprints
            "WTooTooW", // z = 4  two 3x3 tables, origins at x=1 and x=4
            "WH.....W", // z = 3  the Hearth, indestructible
            "WP.....W", // z = 2  pantry - what the Glutton comes for
            "WC....GW", // z = 1  cellar, and a garden plot in the corner
            "WWWWWWWW", // z = 0  south
        };

        [MenuItem("Last Orders/Build Starting Scene", priority = 2)]
        public static void Build()
        {
            GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
            if (balance == null)
            {
                EditorUtility.DisplayDialog(
                    "Last Orders",
                    "No GameBalance asset found.\n\nRun 'Last Orders / Generate Starting Content' " +
                    "and 'Configure Buildables' first.",
                    "OK");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            GridBuildingSystem3D grid = BuildGrid(balance);
            BuildComposition(balance, grid);

            // Placed after the composition root exists, because placement goes through the
            // build grid the composition root initialises.
            Transform restaurant = null;
            int placed = PlaceStartingArea(out restaurant);

            // The grid is static and does not survive into play mode, so the authored objects
            // need putting back on a fresh one at runtime.
            BuildGridInitializer(grid, balance, restaurant);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[Last Orders] Built {ScenePath} with {placed} starting objects.\n" +
                      "Press Play, then F1 for the debug overlay and F2 to cycle grid layers.");

            EditorUtility.DisplayDialog(
                "Last Orders",
                $"Scene built at {ScenePath}.\n\n{placed} objects placed in The Shack (8x8).\n\n" +
                "Press Play to run the cycle.\nF1 toggles the debug overlay, F2 cycles grid layers.",
                "OK");
        }

        // --- Environment --------------------------------------------------------

        static void BuildEnvironment()
        {
            // 5: fixed-angle perspective at roughly 52 degrees, no rotation. The hard
            // constraint is that the whole plot fits on screen at the maximum expansion tier,
            // so this is framed for a 16x14 grid even though Tier 1 is 8x8.
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";

            camera.transform.position = new Vector3(8f, 14f, -6f);
            camera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            camera.fieldOfView = 45f;
            camera.backgroundColor = new Color(0.18f, 0.21f, 0.26f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            // 21: warm saturated inside, desaturated cool grey-blue beyond the walls. The
            // restaurant should read as the only colourful object on screen.
            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.5f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.36f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.19f, 0.18f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(8f, -0.01f, 8f);
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            // GridBuildingSystem3D.Update calls UIGame.IsPointerOverUI every frame, which
            // needs an EventSystem to exist. Cheaper to put one here than to discover it as
            // a null reference on first play.
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // --- Systems ------------------------------------------------------------

        static GridBuildingSystem3D BuildGrid(GameBalanceConfig balance)
        {
            var gridObject = new GameObject("GridBuildingSystem");
            var grid = gridObject.AddComponent<GridBuildingSystem3D>();

            PlotTierDefinition tier = FirstTier(balance);
            int width = tier != null ? tier.gridWidth : 8;
            int height = tier != null ? tier.gridHeight : 8;

            // Initialised here, in the editor, so the starting area can be placed into a real
            // grid and saved with the scene rather than appearing only at runtime.
            grid.Initialize(width, height, 1f, Vector3.zero);

            return grid;
        }

        static void BuildComposition(GameBalanceConfig balance, GridBuildingSystem3D grid)
        {
            var root = new GameObject("GameLifetimeScope");
            var scope = root.AddComponent<GameLifetimeScope>();

            var pools = new GameObject("Pools");
            pools.transform.SetParent(root.transform, false);

            // Assigned through SerializedObject because the fields are private and serialized.
            var serialized = new SerializedObject(scope);
            serialized.FindProperty("balance").objectReferenceValue = balance;
            serialized.FindProperty("buildGrid").objectReferenceValue = grid;
            serialized.FindProperty("poolRoot").objectReferenceValue = pools.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 24.2: the grid debug overlay gets built on day one, not when something breaks.
            var overlayObject = new GameObject("DebugOverlay");
            var overlay = overlayObject.AddComponent<LastOrdersDebugOverlay>();

            var overlaySerialized = new SerializedObject(overlay);
            overlaySerialized.FindProperty("scope").objectReferenceValue = scope;
            overlaySerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static PlotTierDefinition FirstTier(GameBalanceConfig balance)
        {
            if (balance.plotTiers == null) return null;

            for (int i = 0; i < balance.plotTiers.Length; i++)
                if (balance.plotTiers[i] != null && balance.plotTiers[i].tier == 1)
                    return balance.plotTiers[i];

            return null;
        }

        // --- Starting area --------------------------------------------------------

        static void BuildGridInitializer(GridBuildingSystem3D grid, GameBalanceConfig balance,
                                         Transform restaurant)
        {
            var host = new GameObject("SceneGridInitializer");
            var initializer = host.AddComponent<SceneGridInitializer>();

            var serialized = new SerializedObject(initializer);
            serialized.FindProperty("buildGrid").objectReferenceValue = grid;
            serialized.FindProperty("plotTier").objectReferenceValue = FirstTier(balance);
            serialized.FindProperty("restaurantRoot").objectReferenceValue = restaurant;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static int PlaceStartingArea(out Transform restaurantRoot)
        {
            Dictionary<char, PlacedObjectTypeSO> palette = BuildPalette();
            var parent = new GameObject("Restaurant").transform;
            restaurantRoot = parent;

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null)
            {
                Debug.LogError("[Last Orders] Build grid was not initialised. Nothing placed.");
                return 0;
            }

            int placed = 0;

            for (int row = 0; row < Layout.Length; row++)
            {
                // Row 0 of the layout string is the north edge, which is the highest z.
                int z = Layout.Length - 1 - row;
                string line = Layout[row];

                for (int x = 0; x < line.Length; x++)
                {
                    char symbol = line[x];

                    // '.' is open floor, 'o' is a tile already covered by a multi-tile
                    // object's footprint. Neither places anything.
                    if (symbol == '.' || symbol == 'o') continue;

                    if (!palette.TryGetValue(symbol, out PlacedObjectTypeSO type) || type == null)
                        continue;

                    if (PlaceOne(grid, type, new Vector2Int(x, z), parent))
                    {
                        placed++;
                        continue;
                    }

                    // Loud on purpose. A silent skip here means the layout and the assets'
                    // footprints have drifted apart, and the result is a restaurant that
                    // looks subtly wrong and plays very wrong.
                    Debug.LogWarning(
                        $"[Last Orders] Could not place '{type.nameString}' " +
                        $"({type.width}x{type.height}) at ({x}, {z}). The layout and this " +
                        "asset's footprint disagree.");
                }
            }

            return placed;
        }

        /// <summary>
        /// Places one object and registers it on every tile of its footprint.
        ///
        /// Goes through the same primitives the runtime PlaceBuildableCommand uses rather
        /// than through the command itself, because a command debits materials and
        /// clock-minutes from services that do not exist until play mode.
        /// </summary>
        static bool PlaceOne(GridXZ<GridBuildingSystem3D.GridObject> grid, PlacedObjectTypeSO type,
                             Vector2Int origin, Transform parent)
        {
            List<Vector2Int> tiles = type.GetGridPositionList(origin, PlacedObjectTypeSO.Dir.Down);

            for (int i = 0; i < tiles.Count; i++)
            {
                GridBuildingSystem3D.GridObject cell = grid.GetGridObject(tiles[i].x, tiles[i].y);
                if (cell == null || cell.GetPlacedObject() != null) return false;
            }

            if (type.prefab == null)
            {
                Debug.LogWarning($"[Last Orders] '{type.nameString}' has no prefab. Skipped.");
                return false;
            }

            Vector3 world = grid.GetWorldPosition(origin.x, origin.y);

            // Instantiated directly rather than via PlacedObject_Done.Create, which pulls from
            // the type's runtime pool. Pools do not exist at edit time.
            //
            // InstantiatePrefab keeps the scene object linked to its prefab, which is what we
            // want, but it returns null if the reference is not a prefab root. Some of these
            // assets point at a child transform, so fall back to a plain clone rather than
            // dropping the object.
            var instance = PrefabUtility.InstantiatePrefab(type.prefab.gameObject) as GameObject;
            if (instance == null) instance = Object.Instantiate(type.prefab.gameObject);
            instance.transform.SetParent(parent, false);
            instance.transform.position = world;
            instance.transform.rotation = Quaternion.Euler(0f, type.GetRotationAngle(PlacedObjectTypeSO.Dir.Down), 0f);
            instance.name = type.nameString;

            var placedObject = instance.GetComponent<PlacedObject_Done>();
            if (placedObject != null)
            {
                // Without this the object sits on the grid with a null type internally, and
                // every system that asks a tile what is on it silently skips it - the
                // restaurant would look built and behave as though it were empty.
                placedObject.ConfigurePlacement(type, origin, PlacedObjectTypeSO.Dir.Down);

                for (int i = 0; i < tiles.Count; i++)
                    grid.GetGridObject(tiles[i].x, tiles[i].y)?.SetPlacedObject(placedObject);
            }

            // Everything that can be hurt needs the component that tracks it, including the
            // Hearth - it can be Damaged, it simply can never be disabled.
            if (instance.GetComponent<Consequence.DamageableStructure>() == null && placedObject != null)
                instance.AddComponent<Consequence.DamageableStructure>();

            if (type.isDoor && instance.GetComponent<Perimeter.DoorController>() == null)
                instance.AddComponent<Perimeter.DoorController>();

            if (type.wallMaterial != null && type.wallMaterial.flammable &&
                instance.GetComponent<Consequence.StructureFire>() == null)
                instance.AddComponent<Consequence.StructureFire>();

            return true;
        }

        static Dictionary<char, PlacedObjectTypeSO> BuildPalette()
        {
            var byName = new Dictionary<string, PlacedObjectTypeSO>();

            foreach (string guid in AssetDatabase.FindAssets("t:PlacedObjectTypeSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(path);
                if (asset != null) byName[asset.name] = asset;
            }

            PlacedObjectTypeSO Find(params string[] names)
            {
                foreach (string name in names)
                    if (byName.TryGetValue(name, out PlacedObjectTypeSO asset)) return asset;

                Debug.LogWarning($"[Last Orders] None of [{string.Join(", ", names)}] found.");
                return null;
            }

            return new Dictionary<char, PlacedObjectTypeSO>
            {
                ['W'] = Find("Wall"),
                ['D'] = Find("Door"),
                ['H'] = Find("SoupStove", "BreadStove"),
                ['T'] = Find("Table"),
                ['P'] = Find("Build_Pantry", "BarrelProp"),
                ['C'] = Find("Build_Cellar", "WellProp"),
                ['G'] = Find("PlantPot"),
            };
        }
    }
}
