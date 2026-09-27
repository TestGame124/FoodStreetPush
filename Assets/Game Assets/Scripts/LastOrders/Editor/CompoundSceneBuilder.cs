using System.Collections.Generic;
using System.IO;
using LastOrders.Blockout;
using LastOrders.Interaction;
using LastOrders.Jobs;
using LastOrders.Kitchen;
using LastOrders.Core;
using LastOrders.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Footprint = LastOrders.EditorTools.BlockoutSchedule.Footprint;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the starting area of sections 15-18 out of primitives, on a duplicate of Main
    /// Level.
    ///
    /// Two dressed passes with the project's art packs came out wrong, and the reason was not
    /// the placement maths in the end - it was that PolygonFarm, the VOXEL furniture and the
    /// city-park props are authored at three different scales and three different levels of
    /// detail. Fitting them to a shared plan makes that disagreement more obvious, not less.
    ///
    /// So this builds from cubes, spheres and cylinders in one palette instead. 20 asks for
    /// "rounded, chunky 3D shapes with warm diner colours and slightly cooler surroundings",
    /// which is a description of exactly this, and it has the property the art packs could not
    /// offer: nothing can be the wrong size relative to anything else, because every shape is
    /// authored in metres against the same schedule.
    ///
    /// Main Level still supplies the ground, the lighting, the Canvas and the old bootstrap.
    /// Its own level dressing is deactivated rather than deleted.
    /// </summary>
    public static class CompoundSceneBuilder
    {
        const string SourceScene = "Assets/GridBuildingSystem/Main Level.unity";
        const string ScenePath = "Assets/Scenes/LastOrders_Compound.unity";
        const string BalancePath = "Assets/Game Assets/Content/LastOrders/GameBalance.asset";

        /// <summary>
        /// Main Level roots parked in this scene. Switched off, not removed.
        ///
        /// Two groups. The first is level dressing standing on the compound's ground. The
        /// second is the old bootstrap: GameHandler runs a fifteen-step Start that initialises
        /// the shop database, the inventory, the order board and LevelController - and
        /// LevelController reaches straight into the Zone this scene parks, so leaving it
        /// running throws on entering play and takes everything after it down with it.
        ///
        /// Deliberately NOT including "Geometry": its single child is the level's Ground, the
        /// mesh everything stands on. Parking it left the compound floating over bare terrain -
        /// a reminder that "level dressing" and "the floor" can share a tidy-looking root.
        /// </summary>
        static readonly string[] ClashingRoots =
        {
            "Zone", "Floors", "AllPots", "Roads",
            "Cube", "Cube (1)", "Cube (2)", "Cube (3)", "Cube (4)",
            "GameHandler", "ScriptsHandler", "VisitorSpawners", "BuildingGhost",
        };

        /// <summary>
        /// Children of GameHandler that the scene cannot lose when it is parked.
        ///
        /// Both the Main Camera and the Directional Light are parented under the old bootstrap,
        /// so switching it off without rescuing them first leaves a scene that is correct,
        /// complete, and entirely black.
        /// </summary>
        static readonly string[] RescueFromLegacy = { "Main Camera", "Directional Light" };

        static DeterministicRandom rng;

        // Pieces the level systems have to be wired to. Captured as they are built rather
        // than found by name afterwards: a name search would silently pick up the legacy
        // level's own fixtures the moment somebody re-enables a parked root.
        static Transform stoveRoot;
        static Transform stoveStand;
        static Transform counterRoot;
        static GameObject[] counterServings;
        static Transform customerMarkers;

        [MenuItem("Last Orders/Build Compound Scene (from Main Level)", priority = 5)]
        public static void Build()
        {
            GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
            if (balance == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "No GameBalance asset found.\n\nRun 'Last Orders / Generate Starting Content' first.",
                    "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    $"Could not find the source scene:\n{SourceScene}", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            rng = new DeterministicRandom(20260919);
            K.Reset();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourceScene, ScenePath))
            {
                Debug.LogError($"[Compound] Could not copy {SourceScene} to {ScenePath}.");
                return;
            }

            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int parked = ParkClashingContent(scene);

            var root = new GameObject("LastOrders_Compound").transform;

            BuildGround(root);
            BuildFence(root);
            BuildDiner(root);
            BuildFarm(root);
            BuildCamp(root);
            BuildDefence(root);
            BuildOutside(root);

            // Where the customers come from. Built before the markers so the road screen is
            // already standing when the route nodes are placed against it.
            BuildWestRoad(root);

            BuildActors(root);
            BuildScatter(root);

            Transform enemyMarkers = BuildMarkers(root);

            GridBuildingSystem3D grid = Object.FindFirstObjectByType<GridBuildingSystem3D>();
            GameLifetimeScope scope = AddSystems(balance, grid, enemyMarkers);

            AddLevelSystems(scope, balance);

            FrameCamera();
            int hud = CompoundHudBuilder.Build(scope);

            int shapes = root.GetComponentsInChildren<MeshRenderer>(true).Length;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Compound] Built {ScenePath} from {SourceScene}.\n" +
                      $"{shapes} primitives placed from BlockoutSchedule, {hud} HUD elements, " +
                      $"{parked} Main Level roots parked (deactivated, not deleted).\n" +
                      "Re-enable any parked root to get the old level back.");

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }

        static int ParkClashingContent(Scene scene)
        {
            var byName = new Dictionary<string, GameObject>();
            foreach (GameObject go in scene.GetRootGameObjects()) byName[go.name] = go;

            // Rescue first, park second. The other order loses the camera and the light.
            foreach (string keep in RescueFromLegacy)
            {
                Transform found = FindDeep(scene, keep);
                if (found == null)
                {
                    Debug.LogWarning($"[Compound] Could not find '{keep}' to rescue before parking " +
                                     "the old bootstrap. The scene may come up dark or with no camera.");
                    continue;
                }

                found.SetParent(null, worldPositionStays: true);
            }

            int parked = 0;
            foreach (string name in ClashingRoots)
            {
                if (!byName.TryGetValue(name, out GameObject go) || go == null) continue;

                go.SetActive(false);
                parked++;
            }

            return parked;
        }

        static Transform FindDeep(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            }

            return null;
        }

        // --- Ground -------------------------------------------------------------------

        static void BuildGround(Transform parent)
        {
            Transform root = K.Group("Ground", parent);

            // 15: the public road lies south of the compound. It runs off BOTH ends of the
            // map, because a road that stops at the world rectangle is a corridor - and the
            // whole point of the visitor system is that these people are going somewhere.
            float roadWest = BlockoutSchedule.SettlementRoadX - 12f;
            float roadEast = BlockoutSchedule.RoadEastExitX + 6f;
            float roadZ = BlockoutSchedule.RoadCentreZ;

            K.Cube("Road", root, new Vector3((roadWest + roadEast) * 0.5f, 0.02f, roadZ),
                   new Vector3(roadEast - roadWest, 0.04f, BlockoutSchedule.RoadWidth), K.Asphalt);

            for (float x = roadWest + 1f; x < roadEast; x += 3.2f)
                K.Cube("RoadLine", root, new Vector3(x, 0.05f, roadZ), new Vector3(1.4f, 0.02f, 0.16f), K.RoadLine);

            // Verge under everything west of the map. Without it the settlement stands on the
            // skybox, which is a detail nobody notices until the first screenshot.
            K.Cube("WestVerge", root, new Vector3((roadWest - 2f) * 0.5f, 0.0f, 4f),
                   new Vector3(-roadWest + 4f, 0.03f, 24f), K.Grass);

            // Worn dirt where people actually walk. 19's placement test is "who uses this and
            // how do they get there" - a path is that answer made visible.
            DirtPath(root, "Path_GateToPorch", new Rect(18.4f, 4f, 3.2f, 13f));
            DirtPath(root, "Path_Farm", new Rect(10f, 14.4f, 8f, 1.4f));
            DirtPath(root, "Path_East", new Rect(22f, 14.4f, 12f, 1.4f));
            DirtPath(root, "Path_Crop", new Rect(10.2f, 16f, 1.4f, 12f));
        }

        static void DirtPath(Transform parent, string name, Rect area)
        {
            K.Cube(name, parent, new Vector3(area.center.x, 0.03f, area.center.y),
                   new Vector3(area.width, 0.05f, area.height), K.Soil);
        }

        // --- Perimeter ----------------------------------------------------------------

        static void BuildFence(Transform parent)
        {
            Transform root = K.Group("Fence", parent);

            foreach (BlockoutSchedule.FenceRun run in BlockoutSchedule.Fence)
                K.Fence(run.Id, root, run.AlongX, run.Line, run.From, run.To);

            // G1. 16 leaves the opening empty until it is installed, and 13 step 8 is the player
            // installing it - so the gate is built and switched off, ready for that step.
            Footprint g = Find(BlockoutSchedule.Outdoor, "G1");
            Transform gate = K.Group("G1_Gate", root);

            float cz = g.CornerZ + g.SizeZ * 0.5f;
            K.Box("PostL", gate, new Vector3(g.CornerX, 0f, cz), new Vector3(0.24f, 1.6f, 0.24f), K.WoodDark);
            K.Box("PostR", gate, new Vector3(g.MaxX, 0f, cz), new Vector3(0.24f, 1.6f, 0.24f), K.WoodDark);

            for (int i = 0; i < 3; i++)
            {
                K.Cube($"Bar_{i}", gate, new Vector3(g.CornerX + g.SizeX * 0.5f, 0.45f + i * 0.4f, cz),
                       new Vector3(g.SizeX, 0.14f, 0.12f), K.Wood);
            }

            gate.gameObject.SetActive(false);
        }

        // --- The diner ----------------------------------------------------------------

        static void BuildDiner(Transform parent)
        {
            Footprint r = Find(BlockoutSchedule.Outdoor, "R1");
            Transform root = K.Group("Diner", parent);

            const float h = 3f;
            float t = BlockoutSchedule.DinerWallThickness;
            float doorMin = BlockoutSchedule.DinerDoorMinX;
            float doorMax = BlockoutSchedule.DinerDoorMaxX;

            // Floor, with a few tiles of the cooler colour so the interior reads as a room
            // rather than a slab. Not a full checkerboard: at this camera height that is noise.
            K.Cube("Floor", root, new Vector3(r.CornerX + r.SizeX * 0.5f, 0.04f, r.CornerZ + r.SizeZ * 0.5f),
                   new Vector3(r.SizeX, 0.08f, r.SizeZ), K.DinerFloor);

            Transform tiles = K.Group("Tiles", root);
            for (float x = r.CornerX + 1f; x < r.MaxX - 1f; x += 2f)
            {
                for (float z = r.CornerZ + 1f; z < r.MaxZ - 1f; z += 2f)
                {
                    if (((int)x + (int)z) % 4 != 0) continue;
                    K.Cube("Tile", tiles, new Vector3(x, 0.09f, z), new Vector3(1f, 0.02f, 1f), K.DinerTile);
                }
            }

            Transform walls = K.Group("Walls", root);

            // Open-topped: 17 says hide the roof, and the reference image shows the interior
            // read from a fixed overhead camera.
            Wall(walls, "South_West", true, r.CornerZ + t * 0.5f, r.CornerX, doorMin, h, t);
            Wall(walls, "South_East", true, r.CornerZ + t * 0.5f, doorMax, r.MaxX, h, t);
            Wall(walls, "North", true, r.MaxZ - t * 0.5f, r.CornerX, r.MaxX, h, t);
            Wall(walls, "West", false, r.CornerX + t * 0.5f, r.CornerZ, r.MaxZ, h, t);
            Wall(walls, "East", false, r.MaxX - t * 0.5f, r.CornerZ, r.MaxZ, h, t);

            BuildFrontage(root, r, h, t, doorMin, doorMax);
            BuildInterior(K.Group("Interior", root));
        }

        /// <summary>A wall run, with the red base band and teal cap that make it read as a diner.</summary>
        static void Wall(Transform parent, string name, bool alongX, float line, float from, float to,
                         float height, float thickness)
        {
            Transform root = K.Group($"Wall_{name}", parent);

            float mid = (from + to) * 0.5f;
            float span = to - from;

            Vector3 centre = alongX ? new Vector3(mid, 0f, line) : new Vector3(line, 0f, mid);
            Vector3 size = alongX ? new Vector3(span, height, thickness) : new Vector3(thickness, height, span);

            K.Box("Body", root, centre, size, K.DinerWall, collider: true);

            // 20: preserve a recognisable silhouette. The band and cap are what say "diner"
            // rather than "shed", and they cost two boxes per run.
            Vector3 band = alongX ? new Vector3(span, 0.5f, thickness * 1.25f)
                                  : new Vector3(thickness * 1.25f, 0.5f, span);

            K.Cube("Skirt", root, centre + Vector3.up * 0.25f, band, K.DinerAccent);

            Vector3 cap = alongX ? new Vector3(span, 0.26f, thickness * 1.6f)
                                 : new Vector3(thickness * 1.6f, 0.26f, span);

            K.Cube("Cap", root, centre + Vector3.up * (height - 0.06f), cap, K.DinerTrim);
        }

        /// <summary>The south face: windows either side of the doorway, and the sign above it.</summary>
        static void BuildFrontage(Transform parent, in Footprint r, float height, float t,
                                  float doorMin, float doorMax)
        {
            Transform root = K.Group("Frontage", parent);
            float z = r.CornerZ + t * 0.5f;

            foreach (float x in new[] { r.CornerX + 2.2f, r.CornerX + 4.4f, doorMax + 1.4f, doorMax + 3.6f })
            {
                K.Cube("Window", root, new Vector3(x, 1.85f, z - t * 0.35f),
                       new Vector3(1.5f, 1.1f, t * 0.5f), K.Glass);

                K.Cube("WindowFrame", root, new Vector3(x, 1.85f, z - t * 0.3f),
                       new Vector3(1.7f, 1.3f, t * 0.35f), K.DinerTrim);
            }

            float doorCentre = (doorMin + doorMax) * 0.5f;

            // The sign, built as a board on a pivot so the Chef can physically turn it round.
            // Letters would be six more boxes each; two faces and a lamp read at this size,
            // and the ACT of turning it is what the player is meant to remember.
            Transform sign = K.Group("Sign", root);
            sign.position = new Vector3(doorCentre, 2.55f, z - t * 0.6f);

            Transform board = K.Group("Board", sign);

            K.Cube("Back", board, Vector3.zero, new Vector3(3.2f, 0.72f, t * 0.6f), K.DinerAccent);

            // Two faces. The camera looks north, so whichever face sits at local -Z is the one
            // being read; a 180 degree turn swaps them, which is the whole mechanism.
            K.Cube("ClosedFace", board, new Vector3(0f, 0f, -t * 0.4f),
                   new Vector3(2.7f, 0.4f, t * 0.3f), K.DinerWall);
            K.Cube("ClosedBar", board, new Vector3(0f, 0f, -t * 0.55f),
                   new Vector3(1.7f, 0.16f, t * 0.2f), K.Dark);

            K.Cube("OpenFace", board, new Vector3(0f, 0f, t * 0.4f),
                   new Vector3(2.7f, 0.4f, t * 0.3f), K.DinerWall);
            K.Cube("OpenBar", board, new Vector3(0f, 0f, t * 0.55f),
                   new Vector3(2.0f, 0.2f, t * 0.2f), K.Lock);

            var signHit = sign.gameObject.AddComponent<BoxCollider>();
            signHit.size = new Vector3(3.4f, 1.1f, 0.7f);

            // Porch canopy over the entrance.
            K.Cube("Canopy", root, new Vector3(doorCentre, 3.05f, z - 0.75f),
                   new Vector3(4.2f, 0.18f, 1.6f), K.DinerTrim);

            K.Cylinder("CanopyPostL", root, new Vector3(doorMin - 0.1f, 0f, z - 1.4f), 0.14f, 3f, K.MetalDark);
            K.Cylinder("CanopyPostR", root, new Vector3(doorMax + 0.1f, 0f, z - 1.4f), 0.14f, 3f, K.MetalDark);

            // The porch light, off until the restaurant opens. A lit porch at the end of a
            // road that has been dark all game is the entire invitation.
            Transform lamp = K.Group("PorchLight", root);
            lamp.position = new Vector3(doorCentre, 0f, z - 0.75f);

            K.Cylinder("Shade", lamp, new Vector3(0f, 2.80f, 0f), 0.34f, 0.16f, K.MetalDark);
            K.Sphere("Bulb", lamp, new Vector3(0f, 2.76f, 0f), 0.26f, K.Lock);

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(lamp, false);
            glow.transform.localPosition = new Vector3(0f, 2.7f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.86f, 0.62f);
            glow.intensity = 3.5f;
            glow.range = 11f;

            lamp.gameObject.SetActive(false);
        }

        static void BuildInterior(Transform parent)
        {
            Stove(parent, Find(BlockoutSchedule.Interior, "K1"));
            Fridge(parent, Find(BlockoutSchedule.Interior, "K2"));
            Pantry(parent, Find(BlockoutSchedule.Interior, "K3"));

            Footprint crate = Find(BlockoutSchedule.Interior, "K4");
            K.Box("K4_Supply", parent, BlockoutSchedule.CentreOf(crate),
                  new Vector3(crate.SizeX, 0.9f, crate.SizeZ), K.Wood);
            K.Cube("K4_Band", parent, BlockoutSchedule.CentreOf(crate) + Vector3.up * 0.45f,
                   new Vector3(crate.SizeX + 0.04f, 0.1f, crate.SizeZ + 0.04f), K.WoodDark);

            Counter(parent, Find(BlockoutSchedule.Interior, "K5"));

            // 2 makes T2 the repairable seat, so it is the damaged one.
            TableSet(parent, "T1", Find(BlockoutSchedule.TableCores, "T1c"), damaged: false);
            TableSet(parent, "T2", Find(BlockoutSchedule.TableCores, "T2c"), damaged: true);
        }

        static void Stove(Transform parent, in Footprint f)
        {
            Transform root = K.Group("K1_Stove", parent);
            Vector3 c = BlockoutSchedule.CentreOf(f);

            stoveRoot = root;

            // One collider over the whole station: 4 asks for a touch target of at least 48
            // logical pixels, and four burners is a target that punishes aiming.
            var hit = root.gameObject.AddComponent<BoxCollider>();
            hit.center = c + Vector3.up * 0.6f;
            hit.size = new Vector3(f.SizeX + 0.2f, 1.2f, f.SizeZ + 0.4f);

            // Where the Chef stands to cook. The kitchen circulation band runs along Z 24,
            // south of the stations, which is also the side the player is looking from.
            stoveStand = K.Group("K1_Stand", root);
            stoveStand.position = new Vector3(c.x, 0f, f.CornerZ - 0.9f);

            K.Box("Body", root, c, new Vector3(f.SizeX, 0.95f, f.SizeZ), K.Metal);
            K.Cube("Top", root, c + Vector3.up * 0.98f, new Vector3(f.SizeX, 0.06f, f.SizeZ), K.Dark);

            // Four burners and the knobs on the interaction side, which 17 puts to the south.
            for (int i = 0; i < 4; i++)
            {
                float dx = (i % 2 == 0 ? -0.45f : 0.45f);
                float dz = (i < 2 ? -0.3f : 0.3f);

                K.Cylinder($"Burner_{i}", root, c + new Vector3(dx, 1.01f, dz), 0.42f, 0.04f, K.MetalDark);
                K.Sphere($"Knob_{i}", root, c + new Vector3(-0.6f + i * 0.4f, 0.78f, -f.SizeZ * 0.52f), 0.11f, K.DinerAccent);
            }

            K.Cylinder("Pot", root, c + new Vector3(-0.45f, 1.03f, 0.3f), 0.42f, 0.3f, K.MetalDark);
        }

        static void Fridge(Transform parent, in Footprint f)
        {
            Transform root = K.Group("K2_Fridge", parent);
            Vector3 c = BlockoutSchedule.CentreOf(f);

            K.Box("Body", root, c, new Vector3(f.SizeX, 2.05f, f.SizeZ), K.DinerTrim);
            K.Cube("Seam", root, c + Vector3.up * 1.25f, new Vector3(f.SizeX + 0.02f, 0.05f, f.SizeZ + 0.02f), K.DinerWall);
            K.Cube("Handle", root, c + new Vector3(f.SizeX * 0.32f, 1.5f, -f.SizeZ * 0.52f), new Vector3(0.08f, 0.5f, 0.08f), K.Metal);
        }

        static void Pantry(Transform parent, in Footprint f)
        {
            Transform root = K.Group("K3_Pantry", parent);
            Vector3 c = BlockoutSchedule.CentreOf(f);

            K.Box("Body", root, c, new Vector3(f.SizeX, 1.8f, f.SizeZ), K.Wood);

            for (int i = 0; i < 2; i++)
            {
                K.Cube($"Shelf_{i}", root, c + Vector3.up * (0.7f + i * 0.5f),
                       new Vector3(f.SizeX + 0.03f, 0.07f, f.SizeZ + 0.03f), K.WoodDark);

                // 20 asks ingredients to read as recognisable icons; at this distance a couple
                // of coloured jars is the same idea in three dimensions.
                for (int j = 0; j < 3; j++)
                {
                    K.Cylinder($"Jar_{i}{j}", root, c + new Vector3(-0.6f + j * 0.6f, 0.74f + i * 0.5f, 0f),
                               0.2f, 0.26f, K.FlowerHues[j % K.FlowerHues.Length]);
                }
            }
        }

        static void Counter(Transform parent, in Footprint f)
        {
            Transform root = K.Group("K5_Counter", parent);
            Vector3 c = BlockoutSchedule.CentreOf(f);

            K.Box("Body", root, c, new Vector3(f.SizeX, 1.05f, f.SizeZ), K.Wood, collider: true);
            K.Cube("Top", root, c + Vector3.up * 1.08f, new Vector3(f.SizeX + 0.2f, 0.1f, f.SizeZ + 0.3f), K.DinerWall);
            K.Cube("Kick", root, c + Vector3.up * 0.12f, new Vector3(f.SizeX + 0.04f, 0.24f, f.SizeZ + 0.04f), K.DinerAccent);

            // Up to four ready meals wait at the counter, and each one is switched on only
            // when the pass actually holds it. A counter permanently laid with four plates
            // would tell the player the restaurant has food all morning, which is precisely
            // the condition the door is gated on.
            counterServings = new GameObject[4];

            for (int i = 0; i < counterServings.Length; i++)
            {
                Transform serving = K.Group($"Serving_{i}", root);
                serving.localPosition = c + new Vector3(-2.6f + i * 1.7f, 0f, 0f);

                K.Cylinder("Plate", serving, new Vector3(0f, 1.13f, 0f), 0.42f, 0.05f, K.DinerWall);
                K.Sphere("Meal", serving, new Vector3(0f, 1.22f, 0f), 0.3f, K.DinerAccent);

                serving.gameObject.SetActive(false);
                counterServings[i] = serving.gameObject;
            }

            counterRoot = root;
        }

        /// <summary>
        /// A table, its chairs, and the seats the visitor system reserves.
        ///
        /// Authored in LOCAL space under a root that stands at the table. It used to be
        /// authored in world space under a root parked at the scene origin, which was fine
        /// until the damaged table leaned - a 13 degree turn about the origin swings a table
        /// twenty-three metres away right across the room. Placing the root at the table makes
        /// that class of mistake impossible rather than merely avoided.
        /// </summary>
        static void TableSet(Transform parent, string id, in Footprint core, bool damaged)
        {
            Vector3 c = BlockoutSchedule.CentreOf(core);

            Transform root = K.Group($"{id}_TableSet", parent);
            root.position = c;

            // 2 calls the second seat a visible future goal, and 20 wants damaged states to
            // keep a recognisable silhouette - so it leans, it does not disappear.
            if (damaged) root.rotation = Quaternion.Euler(0f, 13f, 0f);

            Color top = damaged ? K.WoodDark : K.DinerWall;

            K.Cylinder("Stem", root, Vector3.zero, 0.22f, 0.72f, K.MetalDark);
            K.Cylinder("Foot", root, Vector3.zero, 0.7f, 0.06f, K.MetalDark);
            K.Cylinder("Top", root, Vector3.up * 0.72f, 1.15f, 0.1f, top);

            // 18: chairs north and south, clear of the reserved service edges. A damaged table
            // has both seats registered and neither usable, so the repair that fixes it has
            // something to switch on rather than something to create.
            Seat(root, id, "N", 0.95f, 180f, knocked: damaged, usable: !damaged);
            Seat(root, id, "S", -0.95f, 0f, knocked: false, usable: !damaged);

            if (!damaged) return;

            K.Cube("Crack", root, new Vector3(0.2f, 0.78f, 0f), new Vector3(0.9f, 0.02f, 0.06f), K.Dark);
        }

        /// <summary>One chair, its sit point and its plate. Offsets are along the table's
        /// local Z, so the whole set leans as one.</summary>
        static void Seat(Transform parent, string tableId, string suffix, float alongZ, float yaw,
                         bool knocked, bool usable)
        {
            Transform chair = K.Group($"Chair_{suffix}", parent);
            chair.localPosition = new Vector3(0f, 0f, alongZ);
            chair.localRotation = Quaternion.Euler(knocked ? 74f : 0f, yaw, 0f);

            K.Box("Seat", chair, Vector3.zero, new Vector3(0.52f, 0.45f, 0.52f), K.DinerAccent);
            K.Cube("Back", chair, new Vector3(0f, 0.68f, -0.22f), new Vector3(0.52f, 0.5f, 0.09f), K.DinerAccent);

            // The sit point hangs off the TABLE rather than off the chair, so a knocked-over
            // chair does not hand the customer a 74 degree tilt when the table is repaired.
            Transform sit = K.Group($"SitPoint_{suffix}", parent);
            sit.localPosition = new Vector3(0f, 0f, alongZ);
            sit.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Transform plate = K.Group($"Plate_{suffix}", parent);
            plate.localPosition = new Vector3(0f, 0.83f, alongZ * 0.35f);

            K.Cylinder("Dish", plate, Vector3.zero, 0.34f, 0.04f, K.DinerWall);
            K.Sphere("Food", plate, Vector3.up * 0.09f, new Vector3(0.24f, 0.14f, 0.24f), K.DinerAccent);

            // Hidden until somebody is served. Authored rather than pooled: one plate per seat
            // is a fixed, tiny number, and pooling it would be ceremony with no payoff.
            plate.gameObject.SetActive(false);
        }

        // --- Farm ---------------------------------------------------------------------

        static void BuildFarm(Transform parent)
        {
            Transform root = K.Group("Farm", parent);

            foreach (string id in new[] { "F1", "F2" })
            {
                Footprint f = Find(BlockoutSchedule.Outdoor, id);
                Transform bed = K.Group($"{id}_CropBed", root);
                Vector3 c = BlockoutSchedule.CentreOf(f);

                K.Box("Soil", bed, c, new Vector3(f.SizeX, 0.28f, f.SizeZ), K.Soil);

                // A frame of four planks, which is what makes it read as a bed and not a patch.
                K.Cube("FrameN", bed, c + new Vector3(0f, 0.2f, f.SizeZ * 0.5f), new Vector3(f.SizeX + 0.16f, 0.24f, 0.16f), K.Wood);
                K.Cube("FrameS", bed, c + new Vector3(0f, 0.2f, -f.SizeZ * 0.5f), new Vector3(f.SizeX + 0.16f, 0.24f, 0.16f), K.Wood);
                K.Cube("FrameW", bed, c + new Vector3(-f.SizeX * 0.5f, 0.2f, 0f), new Vector3(0.16f, 0.24f, f.SizeZ), K.Wood);
                K.Cube("FrameE", bed, c + new Vector3(f.SizeX * 0.5f, 0.2f, 0f), new Vector3(0.16f, 0.24f, f.SizeZ), K.Wood);

                // 16 leaves both beds empty: step 5 is the player planting them.
                for (int i = 0; i < 3; i++)
                {
                    K.Cube($"Row_{i}", bed, c + new Vector3(0f, 0.3f, -f.SizeZ * 0.3f + i * (f.SizeZ * 0.3f)),
                           new Vector3(f.SizeX - 0.5f, 0.06f, 0.3f), K.SoilDark);
                }
            }

            // 7 is explicit that water is environmental support in P0, not a refill timer.
            Footprint w = Find(BlockoutSchedule.Outdoor, "W1");
            Vector3 wc = BlockoutSchedule.CentreOf(w);

            K.Cylinder("W1_Barrel", root, wc, 1.05f, 1.2f, K.Water);
            K.Cylinder("W1_Rim", root, wc + Vector3.up * 1.18f, 1.15f, 0.1f, K.MetalDark);
            K.Cylinder("W1_Band", root, wc + Vector3.up * 0.55f, 1.1f, 0.1f, K.MetalDark);

            K.Box("W1_Can", root, wc + new Vector3(1.1f, 0f, -0.5f), new Vector3(0.4f, 0.4f, 0.32f), K.Metal);
            K.Log("W1_Spout", root, wc + new Vector3(1.4f, 0.32f, -0.5f), 0.1f, 0.5f, K.Metal, 0f, 55f);

            Coop(root, Find(BlockoutSchedule.Outdoor, "C1"));
        }

        static void Coop(Transform parent, in Footprint f)
        {
            Transform root = K.Group("C1_Coop", parent);
            Vector3 c = BlockoutSchedule.CentreOf(f);

            // Locked, so 20's muted materials rather than the warm wood used inside the wire.
            K.Box("Body", root, c, new Vector3(f.SizeX, 1.5f, f.SizeZ), K.WoodDark);

            K.Cube("RoofA", root, c + new Vector3(0f, 1.85f, -0.6f), new Vector3(f.SizeX + 0.4f, 0.16f, 2f), K.Stone, 0f);
            root.Find("RoofA").rotation = Quaternion.Euler(-24f, 0f, 0f);

            K.Cube("RoofB", root, c + new Vector3(0f, 1.85f, 0.6f), new Vector3(f.SizeX + 0.4f, 0.16f, 2f), K.Stone, 0f);
            root.Find("RoofB").rotation = Quaternion.Euler(24f, 0f, 0f);

            K.Cube("Door", root, c + new Vector3(0f, 0.55f, -f.SizeZ * 0.52f), new Vector3(0.8f, 1.1f, 0.08f), K.Dark);

            // The empty enclosure 19 asks for: the coop has to look like it is waiting.
            Transform run = K.Group("Run", root);
            for (int i = 0; i < 6; i++)
            {
                float x = c.x - f.SizeX * 0.5f + i * (f.SizeX / 5f);
                K.Cylinder($"Wire_{i}", run, new Vector3(x, 0f, c.z - f.SizeZ * 0.5f - 1.6f), 0.07f, 0.9f, K.Metal);
            }

            Lock(root, c + Vector3.up * 2.4f, "C1_Lock");
        }

        // --- Camp ----------------------------------------------------------------------

        static void BuildCamp(Transform parent)
        {
            Transform root = K.Group("Camp", parent);

            Footprint t = Find(BlockoutSchedule.Outdoor, "S1");
            Transform tent = K.Group("S1_Tent", root);
            Vector3 c = BlockoutSchedule.CentreOf(t);

            // An A-frame from two leaning slabs. 19: the tent indicates who lives here.
            var left = K.Cube("SideW", tent, c + new Vector3(-t.SizeX * 0.24f, 1f, 0f),
                              new Vector3(0.14f, 2.4f, t.SizeZ + 0.4f), K.Cloth);
            left.transform.rotation = Quaternion.Euler(0f, 0f, 32f);

            var right = K.Cube("SideE", tent, c + new Vector3(t.SizeX * 0.24f, 1f, 0f),
                               new Vector3(0.14f, 2.4f, t.SizeZ + 0.4f), K.Cloth);
            right.transform.rotation = Quaternion.Euler(0f, 0f, -32f);

            K.Log("Ridge", tent, c + new Vector3(0f, 2.05f, 0f), 0.12f, t.SizeZ + 0.5f, K.WoodDark, 0f);
            K.Cube("Mouth", tent, c + new Vector3(0f, 0.75f, -t.SizeZ * 0.5f - 0.18f), new Vector3(1.5f, 1.5f, 0.08f), K.Dark);

            Footprint s2 = Find(BlockoutSchedule.Outdoor, "S2");
            Vector3 sc = BlockoutSchedule.CentreOf(s2);

            // 19: the supply crates sit close to the helper route, which is what explains them.
            K.Box("S2_CrateA", root, sc + new Vector3(-0.35f, 0f, 0f), new Vector3(0.9f, 0.9f, 0.9f), K.Wood, 12f);
            K.Box("S2_CrateB", root, sc + new Vector3(0.5f, 0f, 0.35f), new Vector3(0.8f, 0.8f, 0.8f), K.WoodLight, -18f);
            K.Box("S2_CrateC", root, sc + new Vector3(0.1f, 0.9f, 0.1f), new Vector3(0.7f, 0.7f, 0.7f), K.Wood, 26f);
            K.Cylinder("S2_Sack", root, sc + new Vector3(-1.1f, 0f, -0.6f), 0.55f, 0.6f, K.Cloth);
        }

        // --- Defence --------------------------------------------------------------------

        static void BuildDefence(Transform parent)
        {
            Transform root = K.Group("D1_GuardPost", parent);
            Footprint d = Find(BlockoutSchedule.Outdoor, "D1");
            Rect area = BlockoutSchedule.RectOf(d);
            Vector3 c = BlockoutSchedule.CentreOf(d);

            // "Unfinished" per 16: four uprights, a part-laid platform, and the materials for
            // the rest. 13 step 8 is the player finishing it.
            foreach (var corner in new[]
                     {
                         new Vector2(area.xMin + 0.3f, area.yMin + 0.3f),
                         new Vector2(area.xMax - 0.3f, area.yMin + 0.3f),
                         new Vector2(area.xMin + 0.3f, area.yMax - 0.3f),
                         new Vector2(area.xMax - 0.3f, area.yMax - 0.3f),
                     })
            {
                K.Box("Upright", root, new Vector3(corner.x, 0f, corner.y),
                      new Vector3(0.22f, 2.4f, 0.22f), K.WoodDark);
            }

            for (int i = 0; i < 3; i++)
            {
                K.Cube($"Deck_{i}", root, c + new Vector3(-0.8f + i * 0.8f, 1.15f, 0f),
                       new Vector3(0.7f, 0.1f, area.height - 0.8f), K.Wood);
            }

            K.Box("Materials", root, c + new Vector3(0f, 0f, 0.4f), new Vector3(1f, 0.5f, 0.8f), K.WoodLight, 14f);
            for (int i = 0; i < 4; i++)
                K.Log($"Timber_{i}", root, c + new Vector3(-0.9f, 0.12f + i * 0.2f, -0.7f), 0.2f, 1.8f, K.Wood, 8f);

            K.Box("Toolbox", root, c + new Vector3(0.95f, 0f, -0.6f), new Vector3(0.5f, 0.3f, 0.3f), K.DinerAccent, 40f);
        }

        // --- Outside the wire --------------------------------------------------------------

        static void BuildOutside(Transform parent)
        {
            Transform root = K.Group("Outside", parent);

            Footprint x = Find(BlockoutSchedule.Outdoor, "X1");
            Transform shop = K.Group("X1_Workshop", root);
            Vector3 c = BlockoutSchedule.CentreOf(x);

            K.Box("Body", shop, c, new Vector3(x.SizeX, 3.2f, x.SizeZ), K.Stone);
            K.Cube("Roof", shop, c + Vector3.up * 3.35f, new Vector3(x.SizeX + 0.5f, 0.3f, x.SizeZ + 0.5f), K.MetalDark);
            K.Cube("Door", shop, c + new Vector3(-x.SizeX * 0.5f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 2.4f), K.Dark);

            // 19: the workshop entrance is on its west side, facing the compound.
            K.Cube("Sign", shop, c + new Vector3(-x.SizeX * 0.5f - 0.1f, 2.9f, 0f), new Vector3(0.1f, 0.6f, 1.6f), K.Rust);
            Lock(shop, c + new Vector3(-x.SizeX * 0.5f, 2.2f, 0f), "X1_Lock");

            for (int i = 0; i < 3; i++)
            {
                K.Cylinder($"Tyre_{i}", root, c + new Vector3(-4.2f + i * 0.35f, i * 0.28f, -2.6f - i * 0.4f),
                           1f, 0.28f, K.Dark);
            }

            K.Cylinder("Drum", root, c + new Vector3(-4.4f, 0f, 2.6f), 0.8f, 1.1f, K.Rust);
            K.Sphere("Scrap", root, c + new Vector3(-3.8f, 0.3f, 1.4f), new Vector3(1.6f, 0.6f, 1.4f), K.MetalDark);

            // 19: the rusted car explains the setting without consuming build space.
            Car(root, new Vector3(7.5f, 0f, 5.6f), 74f);

            // "Good Food, Stronger Tomorrow" - the tone the whole compound argues for.
            Transform sign = K.Group("RoadSign", root);
            K.Cylinder("Post", sign, new Vector3(40f, 0f, 6.4f), 0.16f, 1.9f, K.WoodDark);
            K.Cube("Board", sign, new Vector3(40f, 2.1f, 6.4f), new Vector3(2.1f, 1.1f, 0.12f), K.WoodLight, 200f);
        }

        static void Car(Transform parent, Vector3 at, float yaw)
        {
            Transform root = K.Group("RustedCar", parent);
            root.position = at;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);

            K.Box("Body", root, Vector3.zero, new Vector3(4.4f, 0.85f, 1.9f), K.Rust);
            K.Box("Cabin", root, new Vector3(-0.2f, 0.85f, 0f), new Vector3(2.2f, 0.8f, 1.7f), K.Denim);
            K.Cube("Glass", root, new Vector3(-0.2f, 1.28f, 0f), new Vector3(2.0f, 0.4f, 1.72f), K.Glass);

            foreach (float dx in new[] { -1.4f, 1.4f })
            {
                foreach (float dz in new[] { -0.95f, 0.95f })
                {
                    K.Log("Wheel", root, new Vector3(dx, 0.42f, dz), 0.85f, 0.35f, K.Dark, 0f);
                }
            }
        }

        // --- The road west ---------------------------------------------------------------
        //
        // Where the customers come from, and the reason none of them is ever seen appearing.
        //
        // Two problems are solved here at once. The fiction needs somewhere for travellers to
        // be travelling FROM, or the restaurant is a shop with a spawn point outside it. And
        // the camera shows the whole world at its default zoom, so "spawn off screen" is not
        // available - the arrival point has to be far enough west to be outside the default
        // frame AND screened by something at wider aspect ratios.
        //
        // The settlement is the first answer and the screen is the second, and they are the
        // same objects: a gate across the road, two shacks, a watchtower, a burnt-out coach
        // on the north verge and a hoarding on the south one. Sixteen metres of road between
        // the spawn and the site plan's arrival marker turns the remaining gap into a walk.

        static void BuildWestRoad(Transform parent)
        {
            Transform root = K.Group("WestRoad", parent);

            foreach (Footprint f in BlockoutSchedule.West)
            {
                switch (f.Id)
                {
                    case "WS3": Watchtower(root, f); break;
                    case "WS4": SettlementFence(root, f); break;
                    case "SC1": Hoarding(root, f); break;
                    case "SC2": Coach(root, f); break;
                    default: Shack(root, f); break;
                }
            }

            SettlementGate(root);

            // Dead trees on both verges. Bare rather than leafy: a canopy this close to the
            // camera would read as the compound's own planting rather than as the edge of the
            // map, and the silhouette of bare branches is a stronger screen per shape.
            Transform trees = K.Group("Treeline", root);
            foreach (Vector2 at in BlockoutSchedule.WestTreeline) DeadTree(trees, at);
        }

        static void Shack(Transform parent, in Footprint f)
        {
            Transform root = K.Group($"{f.Id}_Shack", parent);
            Vector3 c = BlockoutSchedule.CentreOf(f);

            K.Box("Body", root, c, new Vector3(f.SizeX, f.Height, f.SizeZ), K.WoodDark);
            K.Cube("Roof", root, c + Vector3.up * (f.Height + 0.12f),
                   new Vector3(f.SizeX + 0.5f, 0.22f, f.SizeZ + 0.5f), K.MetalDark);

            // A lit window each. Two warm squares at the far end of a dark road say "people
            // live here" more cheaply than any amount of detail.
            K.Cube("Window", root, c + new Vector3(f.SizeX * 0.5f, f.Height * 0.55f, 0f),
                   new Vector3(0.08f, 0.5f, 0.7f), K.Lock);

            K.Cylinder("Flue", root, c + new Vector3(f.SizeX * 0.3f, f.Height, f.SizeZ * 0.3f),
                       0.18f, 0.9f, K.Rust);
        }

        static void Watchtower(Transform parent, in Footprint f)
        {
            Transform root = K.Group($"{f.Id}_Watchtower", parent);
            Vector3 c = BlockoutSchedule.CentreOf(f);

            float legs = f.Height - 1.4f;

            foreach (float dx in new[] { -f.SizeX * 0.4f, f.SizeX * 0.4f })
            {
                foreach (float dz in new[] { -f.SizeZ * 0.4f, f.SizeZ * 0.4f })
                    K.Cylinder("Leg", root, c + new Vector3(dx, 0f, dz), 0.2f, legs, K.WoodDark);
            }

            K.Cube("Deck", root, c + Vector3.up * (legs + 0.1f),
                   new Vector3(f.SizeX + 0.6f, 0.2f, f.SizeZ + 0.6f), K.Wood);
            K.Box("Rail", root, c + Vector3.up * (legs + 0.2f),
                  new Vector3(f.SizeX + 0.6f, 0.7f, f.SizeZ + 0.6f), K.WoodDark);
            K.Cube("Roof", root, c + Vector3.up * (f.Height + 0.1f),
                   new Vector3(f.SizeX + 1f, 0.18f, f.SizeZ + 1f), K.MetalDark);
        }

        static void SettlementFence(Transform parent, in Footprint f)
        {
            K.Fence($"{f.Id}_Fence", parent, alongX: false, line: f.CornerX + f.SizeX * 0.5f,
                    from: f.CornerZ, to: f.MaxZ, height: f.Height);
        }

        /// <summary>
        /// The settlement's gate across the road.
        ///
        /// Cheap, and it carries most of the fiction on its own: a traveller who walks through
        /// a gate came from a place that has one, which is exactly what "nearby safe
        /// settlements" is supposed to mean.
        /// </summary>
        static void SettlementGate(Transform parent)
        {
            Transform root = K.Group("Settlement_Gate", parent);

            float x = BlockoutSchedule.SettlementGateX;
            float half = BlockoutSchedule.RoadWidth * 0.5f + 0.6f;

            foreach (float dz in new[] { -half, half })
                K.Box("Post", root, new Vector3(x, 0f, BlockoutSchedule.RoadCentreZ + dz),
                      new Vector3(0.36f, 4.2f, 0.36f), K.WoodDark);

            K.Cube("Lintel", root, new Vector3(x, 4.1f, BlockoutSchedule.RoadCentreZ),
                   new Vector3(0.3f, 0.34f, half * 2f + 0.4f), K.Wood);

            // A board over the road. Nothing legible at this distance, and it does not need to
            // be: a sign on a gate is read as a place name whatever it says.
            K.Cube("NameBoard", root, new Vector3(x - 0.2f, 3.6f, BlockoutSchedule.RoadCentreZ),
                   new Vector3(0.1f, 0.6f, 3.2f), K.WoodLight);
        }

        static void Hoarding(Transform parent, in Footprint f)
        {
            Transform root = K.Group($"{f.Id}_Hoarding", parent);
            Vector3 c = BlockoutSchedule.CentreOf(f);

            foreach (float dx in new[] { -f.SizeX * 0.35f, f.SizeX * 0.35f })
                K.Cylinder("Post", root, c + new Vector3(dx, 0f, 0f), 0.24f, f.Height, K.MetalDark);

            K.Cube("Board", root, c + Vector3.up * (f.Height - 1.3f),
                   new Vector3(f.SizeX, 2.4f, 0.16f), K.Cloth);
            K.Cube("Frame", root, c + Vector3.up * (f.Height - 1.3f),
                   new Vector3(f.SizeX + 0.2f, 2.6f, 0.1f), K.Rust);
        }

        static void Coach(Transform parent, in Footprint f)
        {
            Transform root = K.Group($"{f.Id}_Coach", parent);
            Vector3 c = BlockoutSchedule.CentreOf(f);

            K.Box("Body", root, c, new Vector3(f.SizeX, f.Height - 0.5f, f.SizeZ), K.Rust);
            K.Cube("Roof", root, c + Vector3.up * (f.Height - 0.45f),
                   new Vector3(f.SizeX - 0.3f, 0.3f, f.SizeZ - 0.2f), K.MetalDark);

            for (int i = 0; i < 5; i++)
            {
                K.Cube($"Window_{i}", root,
                       c + new Vector3(-f.SizeX * 0.4f + i * f.SizeX * 0.2f, 1.9f, -f.SizeZ * 0.5f),
                       new Vector3(1.1f, 0.8f, 0.1f), K.Dark);
            }

            foreach (float dx in new[] { -f.SizeX * 0.33f, f.SizeX * 0.33f })
            {
                foreach (float dz in new[] { -f.SizeZ * 0.42f, f.SizeZ * 0.42f })
                    K.Log("Wheel", root, c + new Vector3(dx, 0.5f, dz), 1.0f, 0.4f, K.Dark, 0f);
            }
        }

        static void DeadTree(Transform parent, Vector2 at)
        {
            Transform root = K.Group("DeadTree", parent);
            var ground = new Vector3(at.x, 0f, at.y);

            float height = rng.Range(4.2f, 6.4f);
            K.Cylinder("Trunk", root, ground, 0.42f, height, K.WoodDark);

            int branches = rng.Range(3, 6);
            for (int i = 0; i < branches; i++)
            {
                float y = height * (0.5f + i * 0.12f);
                float yaw = rng.Range(0f, 360f);

                K.Log($"Branch_{i}", root, ground + new Vector3(0f, y, 0f),
                      0.18f, rng.Range(1.6f, 2.8f), K.WoodDark, yaw, tilt: rng.Range(-34f, -14f));
            }
        }

        /// <summary>
        /// Wires the level systems to the scene it has just been given.
        ///
        /// Everything here is a reference the runtime cannot reasonably find for itself: which
        /// pot is the stove, which counter holds the servings. Assigned at build time so that a
        /// scene which came out of this builder is playable without a single field dragged by
        /// hand.
        ///
        /// The customer half of this used to live here too. Visitors arrive by road in vehicles
        /// now and are set up by 'Food Street / Visitors / Set Up Visitor System', which reads
        /// the parking and the chairs off whatever scene it is pointed at rather than being
        /// hand-wired per builder.
        /// </summary>
        static void AddLevelSystems(GameLifetimeScope scope, GameBalanceConfig balance)
        {
            var host = new GameObject("LevelSystems").transform;

            if (stoveRoot != null)
            {
                var kitchen = stoveRoot.gameObject.AddComponent<DinerKitchen>();
                kitchen.Configure(scope, HouseSoup(balance), GreyboxDish(), stoveStand);
            }

            // Test controls and a sun that follows the phase, so the cycle can be seen.
            host.gameObject.AddComponent<Debugging.TimeControls>()
                .Configure(scope, null, null, null, null);
            host.gameObject.AddComponent<DayNightLighting>().Configure(scope, Sun());

            if (counterRoot != null)
            {
                var counter = counterRoot.gameObject.AddComponent<CounterDisplay>();
                counter.Configure(scope, counterServings);
            }
        }

        /// <summary>A customer route node, by the id the schedule gives it.</summary>
        static Transform Node(string id)
        {
            Transform node = customerMarkers != null ? customerMarkers.Find(id) : null;

            if (node == null)
                Debug.LogError($"[Compound] No customer route node '{id}'. Visitors will not walk.");

            return node;
        }

        /// <summary>
        /// What the grey-box stove puts out.
        ///
        /// Taken from the project's item database rather than invented, so the dish a customer
        /// is served has a name, an icon for the bubble and a coin value to pay.
        /// </summary>
        static ItemDataSO GreyboxDish()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemsDatabase");

            for (int i = 0; i < guids.Length; i++)
            {
                var database = AssetDatabase.LoadAssetAtPath<ItemsDatabase>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));

                if (database != null && database.AllRecipies != null && database.AllRecipies.Length > 0)
                    return database.AllRecipies[0];
            }

            Debug.LogWarning("[Compound] No ItemsDatabase with recipe items, so the stove has " +
                             "nothing to cook and customers will never be served.");

            return null;
        }

        /// <summary>The scene's brightest directional light. What the phase lighting drives.</summary>
        static Light Sun()
        {
            Light best = null;

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (best == null || light.intensity > best.intensity) best = light;
            }

            return best;
        }

        /// <summary>
        /// The house soup: the cheapest dish available from the start.
        ///
        /// Chosen by cost rather than by name because the recipe table comes from a design
        /// document that is still moving, and a kitchen that stops working when a dish is
        /// renamed is a kitchen that will stop working.
        /// </summary>
        static RecipeDefinition HouseSoup(GameBalanceConfig balance)
        {
            RecipeDefinition[] all = balance.recipes;
            if (all == null) return null;

            RecipeDefinition best = null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].unlockedFromStart) continue;
                if (best == null || all[i].coinValue < best.coinValue) best = all[i];
            }

            return best;
        }

        // --- Actors and the first objective --------------------------------------------------

        static void BuildActors(Transform parent)
        {
            Transform root = K.Group("Survivors", parent);

            // 16 stands them just inside the gate, facing the diner - which is where 13 step 1
            // sends them.
            Transform chef = K.Person("Chef", root, new Vector3(19f, 0f, 10f), K.ChefWhite, chefHat: true, yaw: 0f);
            Transform helper = K.Person("Helper", root, new Vector3(21f, 0f, 10f), K.Denim, chefHat: false, yaw: 0f);

            // 6: the Helper takes the clearing, building, farming and carrying work; the Chef
            // is restaurant-only and has no queue to draw from until the kitchen exists.
            Worker(helper, WorkerRole.Helper);
            Worker(chef, WorkerRole.Chef);

            // B1: the first thing the player touches (13 step 1), and where its 8 wood comes from.
            Footprint b = Find(BlockoutSchedule.Outdoor, "B1");
            Transform debris = K.Group("B1_EntranceDebris", parent);
            Vector3 c = BlockoutSchedule.CentreOf(b);

            for (int i = 0; i < 7; i++)
            {
                float row = i < 4 ? 0f : 1f;
                float along = (i < 4 ? -0.55f + i * 0.37f : -0.4f + (i - 4) * 0.4f);

                K.Log($"Log_{i}", debris, c + new Vector3(along, 0.18f + row * 0.32f, rng.Range(-0.12f, 0.12f)),
                      0.34f, b.SizeX * 0.82f, i % 2 == 0 ? K.Wood : K.WoodDark,
                      90f + rng.Range(-6f, 6f));
            }

            // One box collider over the whole pile rather than seven capsule-ish ones: 4 asks
            // for a touch target of at least 48 logical pixels, and seven thin logs is a target
            // that punishes aiming.
            var hit = debris.gameObject.AddComponent<BoxCollider>();
            hit.center = c + Vector3.up * 0.45f;
            hit.size = new Vector3(b.SizeX + 0.3f, 0.95f, b.SizeZ + 0.5f);

            debris.gameObject.AddComponent<ClearableRubble>();
        }

        static void BuildScatter(Transform parent)
        {
            Transform root = K.Group("Scatter", parent);

            // 15: grass and flowers decorate unused ground but cannot consume required path
            // clearance, so every candidate is tested against the footprints and routes first.
            Transform flora = K.Group("Flora", root);

            int placed = 0;
            for (int attempt = 0; attempt < 700 && placed < 80; attempt++)
            {
                float px = rng.Range(1f, BlockoutSchedule.WorldSizeX - 1f);
                float pz = rng.Range(5f, BlockoutSchedule.WorldSizeZ - 1f);

                if (!IsFreeGround(px, pz)) continue;

                var at = new Vector3(px, 0f, pz);

                if (rng.Chance(0.62f))
                {
                    Color hue = K.FlowerHues[rng.Range(0, K.FlowerHues.Length)];
                    K.Cylinder($"Stem_{placed:000}", flora, at, 0.05f, 0.24f, K.LeafDeep);
                    K.Sphere($"Flower_{placed:000}", flora, at + Vector3.up * 0.3f, 0.18f, hue);
                }
                else
                {
                    float r = rng.Range(0.25f, 0.55f);
                    K.Sphere($"Rock_{placed:000}", flora, at + Vector3.up * (r * 0.3f),
                             new Vector3(r, r * 0.65f, r * 0.85f), K.Stone);
                }

                placed++;
            }

            // Trees outside the wire only: inside, they would shadow the interactions 20 asks
            // to keep readable.
            Transform trees = K.Group("Trees", root);
            Vector2[] spots =
            {
                new Vector2(1.8f, 35f), new Vector2(8f, 37.5f), new Vector2(17f, 37f),
                new Vector2(27f, 37.5f), new Vector2(34f, 36f), new Vector2(42f, 34f),
                new Vector2(45.5f, 27f), new Vector2(45f, 18f), new Vector2(1.8f, 21f),
                new Vector2(1.8f, 28f), new Vector2(43f, 9f), new Vector2(11f, 5.5f),
            };

            foreach (Vector2 s in spots)
                K.Tree("Tree", trees, new Vector3(s.x, 0f, s.y), rng.Range(0.85f, 1.25f), rng);
        }

        /// <summary>True when a point is clear of every footprint, route and fence line.</summary>
        static bool IsFreeGround(float x, float z)
        {
            var p = new Vector2(x, z);

            foreach (Footprint f in BlockoutSchedule.Outdoor)
                if (Inflate(BlockoutSchedule.RectOf(f), 1f).Contains(p)) return false;

            foreach (BlockoutSchedule.Strip s in BlockoutSchedule.Routes)
                if (Inflate(BlockoutSchedule.RectOf(s), 0.4f).Contains(p)) return false;

            foreach (BlockoutSchedule.FenceRun run in BlockoutSchedule.Fence)
            {
                float d = run.AlongX ? Mathf.Abs(z - run.Line) : Mathf.Abs(x - run.Line);
                float along = run.AlongX ? x : z;

                if (d < 0.9f && along >= run.From - 0.5f && along <= run.To + 0.5f) return false;
            }

            return true;
        }

        static Rect Inflate(Rect r, float by) =>
            new Rect(r.xMin - by, r.yMin - by, r.width + by * 2f, r.height + by * 2f);

        // --- Markers and systems ------------------------------------------------------------

        static Transform BuildMarkers(Transform parent)
        {
            Transform root = K.Group("Markers", parent);

            customerMarkers = K.Group("Customers", root);
            Emit(customerMarkers, BlockoutSchedule.CustomerNodes, new Color(0.3f, 0.9f, 1f));
            Emit(K.Group("Survivors", root), BlockoutSchedule.SurvivorNodes, new Color(0.5f, 1f, 0.5f));
            Emit(K.Group("Interaction", root), BlockoutSchedule.InteractionNodes, new Color(1f, 0.85f, 0.4f));

            Transform enemies = K.Group("Enemies", root);
            Emit(enemies, BlockoutSchedule.EnemyRoute, new Color(1f, 0.4f, 0.35f));

            return enemies;
        }

        static void Emit(Transform parent, BlockoutSchedule.Marker[] nodes, Color colour)
        {
            foreach (BlockoutSchedule.Marker m in nodes)
            {
                var go = new GameObject(m.Id);
                go.transform.SetParent(parent, false);
                go.transform.position = m.Position;

                var marker = go.AddComponent<BlockoutMarker>();
                marker.id = m.Id;
                marker.note = m.Note;
                marker.gizmoColour = colour;
            }
        }

        static void Lock(Transform parent, Vector3 at, string name)
        {
            // 20: a locked structure reads as muted materials plus a small lock.
            Transform root = K.Group(name, parent);

            K.Cube("Body", root, at, new Vector3(0.4f, 0.34f, 0.16f), K.Lock);
            K.Log("Shackle", root, at + Vector3.up * 0.26f, 0.22f, 0.16f, K.Lock, 0f);

            var marker = root.gameObject.AddComponent<BlockoutMarker>();
            marker.id = name;
            marker.gizmoColour = K.Lock;
            marker.gizmoRadius = 0.3f;
            marker.note = "Locked. 20: muted materials and a small lock; exact prerequisite on tap.";
        }

        static GameLifetimeScope AddSystems(GameBalanceConfig balance, GridBuildingSystem3D grid,
                                            Transform enemyMarkers)
        {
            GameLifetimeScope existing = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject("LastOrders");
            var scope = root.AddComponent<GameLifetimeScope>();

            var pools = new GameObject("Pools");
            pools.transform.SetParent(root.transform, false);

            var so = new SerializedObject(scope);
            so.FindProperty("balance").objectReferenceValue = balance;
            so.FindProperty("buildGrid").objectReferenceValue = grid;
            so.FindProperty("poolRoot").objectReferenceValue = pools.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var spawnerHost = new GameObject("EnemySpawner");
            var spawner = spawnerHost.AddComponent<Night.EnemySpawner>();

            var sso = new SerializedObject(spawner);
            sso.FindProperty("scope").objectReferenceValue = scope;

            SerializedProperty lanes = sso.FindProperty("laneOverrides");
            Transform spawn = enemyMarkers != null ? enemyMarkers.Find("EnemySpawn") : null;
            if (lanes != null)
            {
                lanes.arraySize = spawn != null ? 1 : 0;
                if (spawn != null) lanes.GetArrayElementAtIndex(0).objectReferenceValue = spawn;
            }

            sso.ApplyModifiedPropertiesWithoutUndo();

            var overlayHost = new GameObject("LastOrdersDebugOverlay");
            var overlay = overlayHost.AddComponent<Debugging.LastOrdersDebugOverlay>();

            var oso = new SerializedObject(overlay);
            oso.FindProperty("scope").objectReferenceValue = scope;
            oso.ApplyModifiedPropertiesWithoutUndo();

            return scope;
        }

        /// <summary>
        /// Points Main Level's camera at the compound: orthographic, landscape, 60 degrees (18).
        /// </summary>
        static void FrameCamera()
        {
            Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogWarning("[Compound] No camera found to frame.");
                return;
            }

            foreach (MonoBehaviour behaviour in cam.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name.Contains("Cinemachine"))
                    behaviour.enabled = false;
            }

            cam.orthographic = true;
            cam.orthographicSize = BlockoutSchedule.DefaultOrthoSize;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;

            var rig = cam.GetComponent<BlockoutCameraRig>() ?? cam.gameObject.AddComponent<BlockoutCameraRig>();
            rig.Configure(BlockoutSchedule.CameraPitch, BlockoutSchedule.CameraYaw,
                          new Rect(0f, 0f, BlockoutSchedule.WorldSizeX, BlockoutSchedule.WorldSizeZ),
                          BlockoutSchedule.ViewBorder, BlockoutSchedule.MinOrthoSize,
                          BlockoutSchedule.MaxOrthoSize);

            // 13: the opening frames the blocked entrance with both survivors in shot.
            rig.Pivot = new Vector3(20f, 0f, 15f);

            // 4: tap selection lives with the camera, because it is the camera's ray.
            if (cam.GetComponent<TapSelector>() == null) cam.gameObject.AddComponent<TapSelector>();
        }

        static void Worker(Transform who, WorkerRole role)
        {
            var agent = who.gameObject.AddComponent<WorkerAgent>();

            var so = new SerializedObject(agent);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Footprint Find(Footprint[] set, string id)
        {
            foreach (Footprint f in set)
                if (f.Id == id) return f;

            Debug.LogError($"[Compound] No footprint '{id}' in the schedule.");
            return default;
        }
    }
}
