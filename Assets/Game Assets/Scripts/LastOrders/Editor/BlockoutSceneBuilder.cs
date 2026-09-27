using System.Collections.Generic;
using System.IO;
using LastOrders.Blockout;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Footprint = LastOrders.EditorTools.BlockoutSchedule.Footprint;
using Kind = LastOrders.EditorTools.BlockoutSchedule.Kind;
using Palette = LastOrders.EditorTools.BlockoutGeometry.Palette;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the measured starting area of The Last Restaurant v0.2, sections 15 to 18, as a
    /// standalone scene.
    ///
    /// Standalone rather than derived from Main Level, which is what the existing SceneBuilder
    /// does. That one is left alone and still works: it is the path that keeps the project's
    /// terrain, art, shop UI and GameHandler bootstrap. But this plan is a measured 48 x 40 m
    /// site with its own coordinates, and laying it over a level that already has terrain and a
    /// save-driven object list would mean fighting both. A clean scene is also what makes the
    /// validator meaningful - every object in it came from the schedule, so anything that fails
    /// a check is a real discrepancy rather than a leftover.
    ///
    /// Nothing here invents a coordinate. Every number comes from BlockoutSchedule.
    /// </summary>
    public static class BlockoutSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/LastOrders_Blockout.unity";
        const string BalancePath = "Assets/Game Assets/Content/LastOrders/GameBalance.asset";
        const string TypeFolder = "Assets/Game Assets/Content/LastOrders/BlockoutTypes";

        /// <summary>Created lazily, one per distinct footprint shape rather than per object.</summary>
        static readonly Dictionary<string, PlacedObjectTypeSO> TypeCache =
            new Dictionary<string, PlacedObjectTypeSO>();

        [MenuItem("Last Orders/Build Blockout Scene", priority = 3)]
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

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BlockoutGeometry.ResetCache();
            TypeCache.Clear();
            ContentGenerator.EnsureFolder(TypeFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            GridBuildingSystem3D grid = BuildGridHost();
            Transform blockout = BuildBlockout();

            BuildRoutes();
            Transform markers = BuildMarkers();

            WireSceneGridInitializer(grid, blockout);
            BuildNavigation();

            GameLifetimeScope scope = BuildComposition(balance, grid);
            BuildEnemySpawner(scope, markers);
            BuildDebugOverlay(scope);
            int hudElements = BuildHud(scope);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();

            Debug.Log($"[Blockout] Built {ScenePath} from BlockoutSchedule.\n" +
                      $"World {BlockoutSchedule.WorldSizeX} x {BlockoutSchedule.WorldSizeZ} m on a " +
                      $"{BlockoutSchedule.CellSize} m grid ({BlockoutSchedule.GridWidth} x " +
                      $"{BlockoutSchedule.GridHeight} cells). " +
                      $"{BlockoutSchedule.ExpectedFenceSections} fence sections, " +
                      $"{BlockoutSchedule.Outdoor.Length} outdoor objects, " +
                      $"{BlockoutSchedule.Interior.Length} interior pieces, " +
                      $"{hudElements} HUD elements.\n" +
                      "Run 'Last Orders / Validate Blockout Scene' to check it against the schedule.");

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }

        /// <summary>
        /// Builds and then immediately checks, in one editor session.
        ///
        /// Separate from Build because the validator reads the OPEN scene: run from a second
        /// batch invocation it would be checking whatever scene happened to load. This is also
        /// the entry point worth pointing a CI step at.
        /// </summary>
        public static void BuildAndValidate()
        {
            Build();
            BlockoutValidator.Validate();
        }

        // --- Environment ------------------------------------------------------------

        static void BuildEnvironment()
        {
            // Ground. Slightly below zero so the route quads at y = 0.02 sit clear of it and
            // footprint boxes resting on Y = 0 are not z-fighting their own shadow plane.
            var ground = BlockoutGeometry.Box("Ground", null,
                new Vector3(BlockoutSchedule.WorldSizeX * 0.5f, -0.05f, BlockoutSchedule.WorldSizeZ * 0.5f),
                new Vector3(BlockoutSchedule.WorldSizeX, 0.1f, BlockoutSchedule.WorldSizeZ),
                Palette.Ground);
            ground.isStatic = true;

            // 15: the public road lies south of the compound. Visual only - the customer and
            // enemy routes along it are marker chains, not surfaces.
            BlockoutGeometry.GroundQuad("Road", null,
                new Rect(0f, 0f, BlockoutSchedule.WorldSizeX, 4f), Palette.Structure, 0.03f);

            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            BuildCamera();

            var events = new GameObject("EventSystem");
            events.AddComponent<UnityEngine.EventSystems.EventSystem>();
            events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = BlockoutSchedule.DefaultOrthoSize;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.21f, 0.26f);

            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<BlockoutCameraRig>();
            rig.Configure(
                BlockoutSchedule.CameraPitch,
                BlockoutSchedule.CameraYaw,
                new Rect(0f, 0f, BlockoutSchedule.WorldSizeX, BlockoutSchedule.WorldSizeZ),
                BlockoutSchedule.ViewBorder,
                BlockoutSchedule.MinOrthoSize,
                BlockoutSchedule.MaxOrthoSize);

            // Framed on the blocked entrance at build time, per 13: "Start with the camera
            // framed on the blocked entrance and both survivors just inside the compound gate."
            rig.Pivot = new Vector3(20f, 0f, 14f);
        }

        // --- Grid host ---------------------------------------------------------------

        static GridBuildingSystem3D BuildGridHost()
        {
            var go = new GameObject("GridBuildingSystem");
            return go.AddComponent<GridBuildingSystem3D>();
        }

        static void WireSceneGridInitializer(GridBuildingSystem3D grid, Transform blockout)
        {
            var go = new GameObject("SceneGridInitializer");
            var initializer = go.AddComponent<SceneGridInitializer>();

            var so = new SerializedObject(initializer);
            so.FindProperty("buildGrid").objectReferenceValue = grid;
            so.FindProperty("cellSize").floatValue = BlockoutSchedule.CellSize;
            so.FindProperty("gridWidthOverride").intValue = BlockoutSchedule.GridWidth;
            so.FindProperty("gridHeightOverride").intValue = BlockoutSchedule.GridHeight;

            // Pointed at a real root, unlike the Main Level path where objects arrive from a
            // save file and the initializer has to sweep the scene. Hierarchy order is
            // deterministic; FindObjectsByType order is not, and a floor registered after a
            // wall on the same tile would silently convert that wall to walkable ground.
            so.FindProperty("restaurantRoot").objectReferenceValue = blockout;
            so.FindProperty("plotTier").objectReferenceValue = null;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildNavigation()
        {
            var go = new GameObject("NavigationSystem");
            var nav = go.AddComponent<FoodStreet.Navigation.NavigationSystem>();

            // The grid is four times the linear size of the campaign's largest plot tier, and
            // GridService rebuilds clearance and tightness across every cell regardless of how
            // many are dirty. Halving the regeneration rate keeps that off the frame budget.
            var so = new SerializedObject(nav);
            SerializedProperty rate = so.FindProperty("regenerationsPerSecond");
            if (rate != null)
            {
                if (rate.propertyType == SerializedPropertyType.Float) rate.floatValue = 2f;
                else if (rate.propertyType == SerializedPropertyType.Integer) rate.intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // --- The plan ------------------------------------------------------------------

        static Transform BuildBlockout()
        {
            var root = new GameObject("Blockout").transform;

            Transform compound = Child(root, "Compound");
            BuildFence(Child(compound, "Fence"));
            BuildGate(compound);

            BuildDiner(Child(root, "Diner"));

            Transform farm = Child(root, "Farm");
            Transform camp = Child(root, "Camp");
            Transform defense = Child(root, "Defense");
            Transform debris = Child(root, "Debris");
            Transform outside = Child(root, "Outside");

            foreach (Footprint f in BlockoutSchedule.Outdoor)
            {
                switch (f.Id)
                {
                    // Handled by their own builders: the diner is a shell with a doorway, and
                    // the gate has to land on the same grid row as the south fence.
                    case "R1":
                    case "G1":
                        continue;

                    case "F1":
                    case "F2":
                        PlaceSolid(f, farm, Palette.Prop);
                        break;

                    case "W1":
                        // 7: "Water is environmental support in P0: the barrel is not another
                        // consumable or refill timer." Scenery, so it never blocks a route.
                        BlockoutGeometry.Footprint(f, farm, Palette.Prop);
                        break;

                    case "C1":
                        PlaceSolid(f, farm, Palette.Locked);
                        break;

                    case "S1":
                        PlaceSolid(f, camp, Palette.Prop);
                        break;

                    case "S2":
                        PlaceSolid(f, camp, Palette.Furniture);
                        break;

                    case "D1":
                        PlaceSolid(f, defense, Palette.Locked, hp: 180f);
                        BuildGuardPostRange(defense, f);
                        break;

                    case "B1":
                        BuildDebris(f, debris);
                        break;

                    case "X1":
                        PlaceSolid(f, outside, Palette.Locked);
                        break;

                    default:
                        PlaceSolid(f, root, Palette.Prop);
                        break;
                }
            }

            return root;
        }

        static void BuildFence(Transform parent)
        {
            int emitted = 0;

            foreach (BlockoutSchedule.FenceRun run in BlockoutSchedule.Fence)
            {
                Transform runRoot = Child(parent, run.Id);
                int sections = run.SectionCount;

                for (int i = 0; i < sections; i++)
                {
                    float from = run.From + i * BlockoutSchedule.FenceSectionLength;
                    float to = from + BlockoutSchedule.FenceSectionLength;

                    // Shortened by half a post where a run terminates at a compound corner, so
                    // the corner post fills the join instead of overlapping it. 16 asks for the
                    // posts to be joined "without duplicate collision", and two colliders in
                    // the same volume is exactly what that forbids.
                    float lo = TouchesCorner(run, from) ? from + BlockoutSchedule.FenceThickness * 0.5f : from;
                    float hi = TouchesCorner(run, to) ? to - BlockoutSchedule.FenceThickness * 0.5f : to;

                    Rect area = BlockoutGeometry.StraddlingWall(
                        run.AlongX, run.Line, lo, hi, BlockoutSchedule.FenceThickness);

                    GameObject go = BlockoutGeometry.BoxForRect(
                        $"Fence_{run.Id}_{i:00}", runRoot, area, BlockoutSchedule.FenceHeight,
                        Palette.Structure);

                    // 10: fence sections are 150 HP destructible barriers with no damage
                    // output, and perimeter objects are the only ones that may be Destroyed
                    // rather than merely Wrecked.
                    //
                    // The tile across the wall's thickness comes from the schedule's outward
                    // rule rather than from the box's own bounds: a 0.25 m wall straddles a
                    // cell boundary, and letting each run pick the nearer cell would put the
                    // west and south walls outside the compound and the east and north walls
                    // inside it.
                    int tile = BlockoutSchedule.FenceTile(run);

                    RegisterOnGrid(go, area, ObjectType.Wall, ObjectCategory.Decoration,
                                   hp: 150f, isWall: true, isPerimeter: true,
                                   forceRow: run.AlongX ? tile : int.MinValue,
                                   forceColumn: run.AlongX ? int.MinValue : tile);

                    emitted++;
                }
            }

            BuildCornerPosts(Child(parent, "CornerPosts"));

            if (emitted != BlockoutSchedule.ExpectedFenceSections)
            {
                Debug.LogError($"[Blockout] Emitted {emitted} fence sections, expected " +
                               $"{BlockoutSchedule.ExpectedFenceSections}. The fence runs in " +
                               "BlockoutSchedule no longer sum to the documented total.");
            }
        }

        /// <summary>True when a run end coincides with one of the four compound corners.</summary>
        static bool TouchesCorner(BlockoutSchedule.FenceRun run, float at)
        {
            foreach (Vector2 corner in BlockoutSchedule.FenceCorners)
            {
                float along = run.AlongX ? corner.x : corner.y;
                float across = run.AlongX ? corner.y : corner.x;

                if (Mathf.Abs(along - at) < 1e-3f && Mathf.Abs(across - run.Line) < 1e-3f) return true;
            }

            return false;
        }

        static void BuildCornerPosts(Transform parent)
        {
            float t = BlockoutSchedule.FenceThickness;

            for (int i = 0; i < BlockoutSchedule.FenceCorners.Length; i++)
            {
                Vector2 c = BlockoutSchedule.FenceCorners[i];
                var area = new Rect(c.x - t * 0.5f, c.y - t * 0.5f, t, t);

                GameObject go = BlockoutGeometry.BoxForRect(
                    $"CornerPost_{i}", parent, area, BlockoutSchedule.FenceHeight, Palette.Structure);

                Vector2Int tile = BlockoutSchedule.CornerTile(c);

                RegisterOnGrid(go, area, ObjectType.Wall, ObjectCategory.Decoration,
                               hp: 150f, isWall: true, isPerimeter: true,
                               forceRow: tile.y, forceColumn: tile.x);
            }
        }

        static void BuildGate(Transform parent)
        {
            Footprint g = Find(BlockoutSchedule.Outdoor, "G1");
            var area = new Rect(g.CornerX, g.CornerZ, g.SizeX, g.SizeZ);

            GameObject go = BlockoutGeometry.BoxForRect("G1_Gate", parent, area, g.Height, Palette.Furniture);

            var facing = new GameObject("Facing");
            facing.transform.SetParent(go.transform, false);
            facing.transform.localScale = new Vector3(1f / g.SizeX, 1f / g.Height, 1f / g.SizeZ);
            facing.transform.localRotation = Quaternion.Euler(0f, BlockoutSchedule.YawOf(g.Face), 0f);

            // 10: 240 HP, 4 m, blocks enemies when closed. Registered as a Door so it seeds the
            // door flow field and so SceneGridInitializer attaches a DoorController; marked as
            // perimeter so it shares the fence's destruction rules.
            // Forced onto the same row as the south fence so the perimeter shell has no gap
            // at the one point enemies are meant to attack.
            RegisterOnGrid(go, area, ObjectType.Door, ObjectCategory.Decoration,
                           hp: 240f, isDoor: true, isPerimeter: true,
                           forceRow: BlockoutSchedule.WallTileOutward(BlockoutSchedule.CompoundMinZ, true));
        }

        static void BuildDiner(Transform parent)
        {
            Footprint r = Find(BlockoutSchedule.Outdoor, "R1");
            float t = BlockoutSchedule.DinerWallThickness;
            float h = BlockoutSchedule.DinerWallHeight;

            Transform walls = Child(parent, "Walls");
            var southRenderers = new List<Renderer>();

            // The east and west walls run the full depth; the north and south runs are trimmed
            // by one wall thickness at each end so they butt against them rather than sharing
            // corner volume. Same rule as the fence corner posts, and for the same reason: two
            // colliders occupying the same space is a thing that is invisible until it matters.
            float innerX0 = r.CornerX + t;
            float innerX1 = r.MaxX - t;

            // South wall, split around the 2 m doorway. 17: the door "is a visual open doorway
            // in P0, not another defensive barrier" - so it is absent geometry, not an object.
            Rect southA = BlockoutGeometry.InsetWall(true, r.CornerZ, innerX0, BlockoutSchedule.DinerDoorMinX, t, true);
            Rect southB = BlockoutGeometry.InsetWall(true, r.CornerZ, BlockoutSchedule.DinerDoorMaxX, innerX1, t, true);

            southRenderers.Add(DinerWall("Wall_South_West", walls, southA, h));
            southRenderers.Add(DinerWall("Wall_South_East", walls, southB, h));

            DinerWall("Wall_North", walls, BlockoutGeometry.InsetWall(true, r.MaxZ, innerX0, innerX1, t, false), h);
            DinerWall("Wall_West", walls, BlockoutGeometry.InsetWall(false, r.CornerX, r.CornerZ, r.MaxZ, t, true), h);
            DinerWall("Wall_East", walls, BlockoutGeometry.InsetWall(false, r.MaxX, r.CornerZ, r.MaxZ, t, false), h);

            // The doorway itself, as a Door tile. Registering it means customers have something
            // to path to: RefreshDerivedSeeds seeds the door flow field from Door-typed tiles.
            // isDoor stays false so no DoorController is attached - it is an opening, not a gate.
            var throat = new Rect(BlockoutSchedule.DinerDoorMinX, r.CornerZ,
                                  BlockoutSchedule.DinerDoorMaxX - BlockoutSchedule.DinerDoorMinX, t);

            var doorway = new GameObject("Doorway");
            doorway.transform.SetParent(walls, false);
            doorway.transform.position = new Vector3(throat.center.x, 0f, throat.center.y);
            RegisterOnGrid(doorway, throat, ObjectType.Door, ObjectCategory.Decoration,
                           hp: 500f, isDoor: false);

            BuildRoof(parent, r, h, southRenderers);
            BuildInterior(Child(parent, "Interior"));
        }

        static Renderer DinerWall(string name, Transform parent, Rect area, float height)
        {
            GameObject go = BlockoutGeometry.BoxForRect(name, parent, area, height, Palette.Structure);

            // 10: the diner is the primary protected objective at 500 HP. Not perimeter, so it
            // can be Wrecked but never Destroyed - 15 is explicit that a wrecked restaurant
            // still looks like the player's restaurant at dawn.
            RegisterOnGrid(go, area, ObjectType.Wall, ObjectCategory.Decoration,
                           hp: 500f, isWall: true, isPerimeter: false);

            return go.GetComponent<Renderer>();
        }

        static void BuildRoof(Transform parent, in Footprint r, float wallHeight, List<Renderer> southWalls)
        {
            var roof = new GameObject("Roof");
            roof.transform.SetParent(parent, false);

            BlockoutGeometry.Box("RoofSlab", roof.transform,
                new Vector3(r.CornerX + r.SizeX * 0.5f, wallHeight + 0.125f, r.CornerZ + r.SizeZ * 0.5f),
                new Vector3(r.SizeX, 0.25f, r.SizeZ), Palette.Structure, collider: false);

            // 17: "Hide its roof and fade camera-facing walls in normal gameplay." Off by
            // default, because a blockout whose interior cannot be seen is not much of one.
            roof.SetActive(false);

            var cutaway = parent.gameObject.AddComponent<DinerCutaway>();
            cutaway.roof = roof;
            cutaway.cameraFacingWalls = southWalls;
        }

        static void BuildInterior(Transform parent)
        {
            foreach (Footprint f in BlockoutSchedule.Interior)
            {
                if (f.Role == Kind.Ground)
                {
                    // A table set's 3 x 3 m is chairs and seating animation space (18), not
                    // collision. Drawn so the reservation is visible and checkable, but left
                    // off the grid - see TableCores for what actually blocks.
                    BlockoutGeometry.GroundQuad($"{f.Id}_Envelope", parent,
                        BlockoutSchedule.RectOf(f), Palette.Route, 0.03f);
                    continue;
                }

                // 8: the pantry is where prepared food lives. Typed Inventory because
                // NavigationSystem.RefreshDerivedSeeds seeds the Pantry, Breach and
                // WeakestPoint flow fields from Inventory tiles - with no such tile in the
                // scene all three fields have empty seed lists and enemies never move.
                ObjectType type = f.Id == "K3" ? ObjectType.Inventory : ObjectType.Table1;

                GameObject go = BlockoutGeometry.Footprint(f, parent, Palette.Furniture);
                RegisterOnGrid(go, BlockoutSchedule.RectOf(f), type, ObjectCategory.Decoration, hp: 60f);
            }

            foreach (Footprint f in BlockoutSchedule.TableCores)
            {
                GameObject go = BlockoutGeometry.Footprint(f, parent, Palette.Furniture);

                // 2: one initially active seat and one repairable seat. T2 is the damaged one.
                RegisterOnGrid(go, BlockoutSchedule.RectOf(f), ObjectType.Table1,
                               ObjectCategory.Decoration, hp: 60f, seatCapacity: 1);
            }

            foreach (Footprint f in BlockoutSchedule.Chairs)
                BlockoutGeometry.Footprint(f, parent, Palette.Furniture, collider: false);
        }

        static void BuildDebris(in Footprint f, Transform parent)
        {
            GameObject go = BlockoutGeometry.Footprint(f, parent, Palette.Prop);

            var clearable = go.AddComponent<BlockoutClearable>();
            clearable.woodReward = 8;

            // Deliberately NOT registered on the grid. B1 fills the whole 2 m door throat, so
            // registering it would make the diner unreachable from the moment the scene loads
            // and every reachability check would fail on day 1 by design. The validator lifts
            // it explicitly for the post-clearing pass; the clearing job, when it exists, will
            // do the same through BlockoutClearable.Clear.
        }

        /// <summary>
        /// 15 shows the guard post's 9 m range as a dashed circle. Drawn here as a gizmo rather
        /// than geometry so it reads as information, not as an object occupying ground.
        /// </summary>
        static void BuildGuardPostRange(Transform parent, in Footprint post)
        {
            var go = new GameObject("D1_Range");
            go.transform.SetParent(parent, false);
            go.transform.position = BlockoutSchedule.CentreOf(post);

            var marker = go.AddComponent<BlockoutMarker>();
            marker.id = "GuardPostRange";
            marker.gizmoColour = new Color(1f, 0.4f, 0.3f, 0.9f);

            // 10: 9 m range. 11 checks it against the gate at roughly 6.5 m.
            marker.gizmoRadius = 9f;
            marker.note = "10: 9 m range, 12 damage every second. 11: the gate sits ~6.5 m away.";
        }

        // --- Routes and markers ----------------------------------------------------------

        static void BuildRoutes()
        {
            var root = new GameObject("Routes").transform;

            foreach (BlockoutSchedule.Strip s in BlockoutSchedule.Routes)
            {
                BlockoutGeometry.GroundQuad($"Route_{s.Id}", root,
                    BlockoutSchedule.RectOf(s), Palette.Route);
            }
        }

        static Transform BuildMarkers()
        {
            var root = new GameObject("Markers").transform;

            EmitMarkers(Child(root, "Customers"), BlockoutSchedule.CustomerNodes,
                        new Color(0.3f, 0.9f, 1f));

            EmitMarkers(Child(root, "Survivors"), BlockoutSchedule.SurvivorNodes,
                        new Color(0.5f, 1f, 0.5f));

            Transform enemies = Child(root, "Enemies");
            EmitMarkers(enemies, BlockoutSchedule.EnemyRoute, new Color(1f, 0.4f, 0.35f));

            EmitMarkers(Child(root, "Interaction"), BlockoutSchedule.InteractionNodes,
                        new Color(1f, 0.85f, 0.4f));

            return enemies;
        }

        static void EmitMarkers(Transform parent, BlockoutSchedule.Marker[] nodes, Color colour)
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

        // --- Systems -----------------------------------------------------------------------

        static GameLifetimeScope BuildComposition(GameBalanceConfig balance, GridBuildingSystem3D grid)
        {
            var root = new GameObject("LastOrders");
            var scope = root.AddComponent<GameLifetimeScope>();

            var pools = new GameObject("Pools");
            pools.transform.SetParent(root.transform, false);

            var so = new SerializedObject(scope);
            so.FindProperty("balance").objectReferenceValue = balance;
            so.FindProperty("buildGrid").objectReferenceValue = grid;
            so.FindProperty("poolRoot").objectReferenceValue = pools.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return scope;
        }

        static void BuildEnemySpawner(GameLifetimeScope scope, Transform enemyMarkers)
        {
            var go = new GameObject("EnemySpawner");
            var spawner = go.AddComponent<Night.EnemySpawner>();

            var so = new SerializedObject(spawner);
            so.FindProperty("scope").objectReferenceValue = scope;

            // 11 authors the Night 1 approach explicitly: spawn at (42, 2), west to (20, 2),
            // north through (20, 6) to the gate. The spawner's procedural lane fallback derives
            // positions from grid bounds, which on a 48 x 40 world puts lane 0 outside the map
            // entirely - so the authored route has to win.
            SerializedProperty lanes = so.FindProperty("laneOverrides");
            if (lanes != null && enemyMarkers != null)
            {
                Transform spawn = enemyMarkers.Find("EnemySpawn");
                lanes.arraySize = spawn != null ? 1 : 0;
                if (spawn != null) lanes.GetArrayElementAtIndex(0).objectReferenceValue = spawn;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildDebugOverlay(GameLifetimeScope scope)
        {
            var go = new GameObject("LastOrdersDebugOverlay");
            var overlay = go.AddComponent<LastOrdersDebugOverlay>();

            var so = new SerializedObject(overlay);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static int BuildHud(GameLifetimeScope scope)
        {
            var go = new GameObject("Canvas");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            // 1 and 18 specify a mobile LANDSCAPE presentation, so the canvas is authored that
            // way. Matching on height rather than width keeps the top bar a constant share of
            // vertical space across the aspect ratios 18 asks to be verified.
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();

            UI.LastOrdersHUD hud = HudBuilder.Build(canvas, new GameLifetimeScopeRef { Scope = scope });

            // Flagged rather than silently patched. HudBuilder lays out against a 1080-wide
            // portrait reference - a full-width top bar of 150, a fixed 560 x 22 Heat bar, a
            // clock ring at x = 320 - because the GDD it was written for is portrait-locked.
            // Which document is authoritative is a design decision, not one a scene builder
            // gets to make by quietly rewriting the layout.
            Debug.LogWarning(
                "[Blockout] HUD built from HudBuilder, which authors against a portrait " +
                "1080x1920 reference, into a landscape 1920x1080 canvas. Fixed pixel offsets " +
                "will not sit correctly. TODO: parameterise HudBuilder.Build with a reference " +
                "resolution. See the portrait/landscape conflict noted in MechanicsAudit_v0.2.md.");

            return hud.GetComponentsInChildren<Graphic>(true).Length;
        }

        // --- Grid registration ---------------------------------------------------------------

        static void PlaceSolid(in Footprint f, Transform parent, Palette palette,
                               float hp = 100f, int seatCapacity = 0)
        {
            GameObject go = BlockoutGeometry.Footprint(f, parent, palette);
            RegisterOnGrid(go, BlockoutSchedule.RectOf(f), ObjectType.Table1,
                           ObjectCategory.Decoration, hp, seatCapacity: seatCapacity);
        }

        /// <summary>
        /// Makes an authored box visible to the grid, and therefore to navigation.
        ///
        /// GridService derives the whole navigation world from what the build grid reports on
        /// each tile, so this is the only thing that separates a blockout you can walk through
        /// from one you cannot. The type carries the classification: Wall and Door become
        /// CellFlags.Wall and CellFlags.Door, and anything else reads as solid.
        /// </summary>
        static void RegisterOnGrid(GameObject go, Rect area, ObjectType type, ObjectCategory category,
                                   float hp, bool isWall = false, bool isDoor = false,
                                   bool isPerimeter = false, int seatCapacity = 0,
                                   int forceRow = int.MinValue, int forceColumn = int.MinValue)
        {
            int originX = forceColumn != int.MinValue ? forceColumn : BlockoutSchedule.ColumnOf(area.xMin);
            int originZ = forceRow != int.MinValue ? forceRow : BlockoutSchedule.RowOf(area.yMin);

            // A 0.25 m wall is thinner than a 0.5 m cell, so it still has to claim one whole
            // tile - a wall that occupies no tile is a wall navigation cannot see.
            int tilesX = Mathf.Max(1, Mathf.RoundToInt(area.width / BlockoutSchedule.CellSize));
            int tilesZ = Mathf.Max(1, Mathf.RoundToInt(area.height / BlockoutSchedule.CellSize));

            PlacedObjectTypeSO so = TypeFor(type, category, tilesX, tilesZ, hp,
                                            isWall, isDoor, isPerimeter, seatCapacity);

            var placed = go.AddComponent<PlacedObject_Done>();
            placed.shopItemType = type;
            placed.ConfigurePlacement(so, new Vector2Int(originX, originZ), PlacedObjectTypeSO.Dir.Down);
        }

        /// <summary>
        /// One type asset per distinct shape, not per object.
        ///
        /// The grid contract needs a PlacedObjectTypeSO to know a footprint's tile extent, and
        /// the blockout has around a dozen distinct shapes across some ninety objects. Caching
        /// by shape keeps that to a dozen assets whose only job is to describe tiles.
        /// </summary>
        static PlacedObjectTypeSO TypeFor(ObjectType type, ObjectCategory category, int w, int h,
                                          float hp, bool isWall, bool isDoor, bool isPerimeter,
                                          int seatCapacity)
        {
            string key = $"{type}_{w}x{h}_{Mathf.RoundToInt(hp)}" +
                         $"{(isWall ? "_w" : string.Empty)}{(isDoor ? "_d" : string.Empty)}" +
                         $"{(isPerimeter ? "_p" : string.Empty)}{(seatCapacity > 0 ? "_s" : string.Empty)}";

            if (TypeCache.TryGetValue(key, out PlacedObjectTypeSO cached) && cached != null) return cached;

            string path = $"{TypeFolder}/Blk_{key}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PlacedObjectTypeSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.nameString = key;
            asset.categoryType = category;
            asset.type = type;
            asset.width = w;
            asset.height = h;
            asset.maxHealth = hp;
            asset.isWall = isWall;
            asset.isDoor = isDoor;
            asset.isPerimeter = isPerimeter;
            asset.seatCapacity = seatCapacity;
            asset.buildCategory = isWall || isPerimeter ? BuildCategory.Structure : BuildCategory.Service;

            EditorUtility.SetDirty(asset);
            TypeCache[key] = asset;
            return asset;
        }

        // --- Small helpers ---------------------------------------------------------------------

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Footprint Find(Footprint[] set, string id)
        {
            foreach (Footprint f in set)
                if (f.Id == id) return f;

            Debug.LogError($"[Blockout] No footprint '{id}' in the schedule.");
            return default;
        }
    }
}
