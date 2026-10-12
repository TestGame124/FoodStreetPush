using System.Collections.Generic;
using System.Text;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Core;
using LastOrders.Expansion;
using LastOrders.Night;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the world round the compound: the open ground it can grow into, and the edges
    /// the night comes from - trees along the back, abandoned suburbs left, right and in front,
    /// the survivors' checkpoint on the road west, and the fog it all ends in.
    ///
    /// Then points the level at it: the night's lanes spawn at the edges and walk in down the
    /// trails, the town's daytime zombies wander the suburbs' streets, the road runs out into the
    /// fog at both ends, and the camera can pan far enough to see the edges but never past them.
    ///
    /// Safe to re-run. Everything it builds lives under one root, which is thrown away and laid
    /// again from the same seed, so the same seed always gives the same world. The old
    /// ApocalypseEnvironment is left switched off and untouched, apart from the courtyard's
    /// locked border, which still lived inside it and is moved out to where the courtyard keeps
    /// its other locked pieces.
    /// </summary>
    public static class EnvironmentSceneSetup
    {
        const string RootName = "WorldEnvironment";
        const string OldEnvironment = "ApocalypseEnvironment";

        [MenuItem("Last Orders/Environment/Build World Edges (Current Scene)", priority = 80)]
        public static void Run() => RunWithSeed(L.DefaultSeed);

        [MenuItem("Last Orders/Environment/Build World Edges (New Seed)", priority = 81)]
        public static void RunNewSeed() => RunWithSeed(System.Environment.TickCount & 0x7FFFFFFF);

        static void RunWithSeed(int seed)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope.\n\nOpen the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scope.gameObject.scene.name}' in place (seed {seed}):\n\n" +
                "- Builds the world edge: trees to the north, abandoned suburbs west, east and south, fog round it all\n" +
                "- Scatters small obstacles over the open ground round the compound, grouped by side\n" +
                "- Runs the old perimeter fence round the edge, broken open where each lane comes through\n" +
                "- Puts generated grass on the ground plane (Geometry/Ground keeps its mesh and collider)\n" +
                "- Adds the survivors' checkpoint on the road west, where the customers come from\n" +
                "- Moves the night's spawn lanes to the edges, with trails to the walls\n" +
                "- Moves the town's daytime zombies onto the suburbs' streets\n" +
                "- Runs the road out into the fog at both ends and widens the camera's pan limits\n\n" +
                "The scene is not saved; save it when you are happy with it.",
                "Build", "Cancel");

            if (!proceed) return;

            Debug.Log(Apply(scope, save: false, seed));
        }

        /// <summary>The build against the open scene, with no dialogs - for automation.</summary>
        public static string ApplyToActiveScene(bool save, int seed = L.DefaultSeed)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null
                ? Apply(scope, save, seed)
                : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        public static string Apply(GameLifetimeScope scope, bool save, int seed)
        {
            Scene scene = scope.gameObject.scene;
            var report = new StringBuilder($"[Last Orders] World edge built (seed {seed}).\n");

            K.Reset();
            E.Reset();
            E.ResetPacks();

            List<Rect> keepOut = SuburbKeepOut(scene);

            Transform root = FreshRoot(scene);

            // --- The ground under everything ------------------------------------------------------------------

            Transform ground = K.Group("Ground", root);
            report.Append($"  {GroundBuilder.Build(scene, ground)}\n");

            // --- The edges -----------------------------------------------------------------------------------------

            var forestRng = new DeterministicRandom(seed ^ DeterministicRandom.StableHash("forest"));
            ForestBuilder.Result forest = ForestBuilder.Build(K.Group("Forest_North", root), L.Lanes[3], ref forestRng);
            report.Append($"  Forest: {forest.Trees} trees, {forest.Undergrowth} undergrowth.\n");

            var suburbRng = new DeterministicRandom(seed ^ DeterministicRandom.StableHash("suburbs"));
            NeighborhoodBuilder.Result suburbs = NeighborhoodBuilder.Build(
                K.Group("Neighborhood_West", root), K.Group("Neighborhood_South", root), K.Group("Neighborhood_East", root),
                keepOut, ref suburbRng);
            report.Append($"  Suburbs: {suburbs.Lots} lots, {suburbs.Houses} houses, {suburbs.EmptyLots} empty lots.\n");

            // --- The ring round the compound ------------------------------------------------------------------

            var ringKeepOut = new List<Rect>(keepOut);
            foreach (L.Street street in L.Streets) ringKeepOut.Add(NeighborhoodBuilder.Inflate(street.Surface, L.SidewalkWidth + 0.5f));

            var ringRng = new DeterministicRandom(seed ^ DeterministicRandom.StableHash("outskirts"));
            OutskirtsBuilder.Result ring = OutskirtsBuilder.Build(K.Group("Outskirts", root), ringKeepOut,
                                                                  new List<Rect> { L.NavGrid }, ref ringRng);
            report.Append($"  Open ground: {ring.Obstacles} obstacles ({Join(ring.PerSector)}), {ring.Patches} ground patches.\n");

            var fenceRng = new DeterministicRandom(seed ^ DeterministicRandom.StableHash("fence"));
            PerimeterFenceBuilder.Result fence = PerimeterFenceBuilder.Build(K.Group("PerimeterFence", root), ref fenceRng);
            report.Append($"  Old perimeter fence: {fence.Modules} modules, broken open in {fence.Breaches} places where the night comes through.\n");

            // --- The checkpoint and the fog -------------------------------------------------------------------

            var checkpointRng = new DeterministicRandom(seed ^ DeterministicRandom.StableHash("checkpoint"));
            SafeCheckpointBuilder.Build(root, ref checkpointRng);

            var fogRng = new DeterministicRandom(seed ^ DeterministicRandom.StableHash("fog"));
            int mist = EdgeFogBuilder.Build(K.Group("Fog", root), ref fogRng);
            report.Append($"  Fog: {L.FogLayers.Length} layers, {mist} mist drifts at the trail mouths.\n");

            // --- Toon trees ------------------------------------------------------------------------------------

            report.Append($"  Foliage: {FoliageKit.Apply(root)} tree, bush and grass renderers on the toon foliage shader.\n");

            // --- Cheap to draw ------------------------------------------------------------------------------------

            report.Append($"  {Finish(root)}\n");

            if (E.Missing.Count > 0)
                report.Append($"  Pack models missing, shapes used instead: {string.Join(", ", E.Missing)}.\n");

            // --- Pointing the level at it ---------------------------------------------------------------------------

            report.Append($"  {SetLanes()}\n");
            report.Append($"  {SetTownPatches()}\n");
            report.Append($"  {SetRoad(scene)}\n");
            report.Append($"  {SurfaceKit.RestyleYard(FindRoot(scene, "VisitorSystem")?.transform)}\n");
            report.Append($"  {SetCamera()}\n");
            report.Append($"  {RehomeCourtyardBorder(scene)}\n");

            AssetDatabase.SaveAssets();

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return report.ToString();
        }

        // --- Measuring what to keep clear of ---------------------------------------------------------------------

        /// <summary>What the suburbs and the ring keep off: the compound, the checkpoint and the town zombies' patches.</summary>
        internal static List<Rect> SuburbKeepOut(Scene scene)
        {
            var keepOut = new List<Rect>(MeasureCompound(scene))
            {
                L.CheckpointZone,
            };

            foreach (TownZombies.Area patch in L.TownPatches)
                keepOut.Add(new Rect(new Vector2(patch.centre.x, patch.centre.z) - patch.size * 0.5f - Vector2.one, patch.size + Vector2.one * 2f));

            return keepOut;
        }

        /// <summary>
        /// The compound as it actually stands, root by root, plus the layout's own numbers - whichever
        /// is bigger wins, so walls moved by hand are never buried under junk.
        /// </summary>
        static List<Rect> MeasureCompound(Scene scene)
        {
            // The defense ring all round the walls is the player's to build on, never the junk's.
            var rects = new List<Rect>
            {
                L.Compound, L.Courtyard, ZoneLayout.Apron, ZoneLayout.Drive, PerimeterStripLayout.RingBounds,
            };

            foreach (string name in new[] { "Restaurant", "CompoundWalls", "ExpansionAreas", "Defenses", "Facilities", "Shelters" })
            {
                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    if (go.name != name || !go.activeInHierarchy) continue;
                    if (TryMeasure(go.transform, out Rect rect)) rects.Add(rect);
                }
            }

            GameObject visitors = FindRoot(scene, "VisitorSystem");
            if (visitors != null)
            {
                foreach (string child in new[] { "Parking", "Drive" })
                {
                    Transform t = visitors.transform.Find(child);
                    if (t != null && TryMeasure(t, out Rect rect)) rects.Add(rect);
                }
            }

            return rects;
        }

        static bool TryMeasure(Transform t, out Rect rect)
        {
            rect = default;
            bool any = false;
            Bounds bounds = default;

            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (any) rect = Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
            return any;
        }

        // --- Making it cheap ------------------------------------------------------------------------------------------

        /// <summary>
        /// Everything that never moves is marked for static batching, and anything flat or small stops
        /// casting shadows: a thousand pebbles each drawn again into the shadow map is cost with nothing to show.
        /// </summary>
        static string Finish(Transform root)
        {
            int renderers = 0;
            int shadowless = 0;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;

                bool moving = go.GetComponent<ParticleSystem>() != null || go.GetComponent<TMP_Text>() != null
                              || go.name.StartsWith("FogLayer");

                if (!moving) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                renderers++;
                if (moving) continue;

                Bounds b = renderer.bounds;
                if (b.size.y < 0.2f || b.size.magnitude < 0.9f)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    shadowless++;
                }
            }

            return $"{renderers} renderers, all static but the fog and mist; {shadowless} flat or small ones cast no shadow.";
        }

        // --- Wiring ---------------------------------------------------------------------------------------------------

        static string SetLanes()
        {
            EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            if (spawner == null) return "WARNING: no EnemySpawner in the scene, so the night still has no lanes.";

            var keep = new HashSet<string>();
            foreach (L.Lane lane in L.Lanes) keep.Add(lane.Name);

            // The old road lanes go: they are replaced, not added to.
            var stale = new List<GameObject>();
            foreach (Transform child in spawner.transform)
                if (child.name.StartsWith("Lane") && !keep.Contains(child.name)) stale.Add(child.gameObject);
            foreach (GameObject go in stale) Undo.DestroyObjectImmediate(go);

            var so = new SerializedObject(spawner);
            SerializedProperty lanes = so.FindProperty("laneOverrides");
            lanes.arraySize = L.Lanes.Length;

            // A group fans out across its lane only as far as its corridor allows.
            SerializedProperty spread = so.FindProperty("laneSpread");
            if (spread != null) spread.floatValue = L.LaneSpread;

            for (int i = 0; i < L.Lanes.Length; i++)
            {
                Transform point = spawner.transform.Find(L.Lanes[i].Name);
                if (point == null)
                {
                    point = K.Group(L.Lanes[i].Name, spawner.transform);
                    Undo.RegisterCreatedObjectUndo(point.gameObject, "Place approach lane");
                }
                else
                {
                    Undo.RecordObject(point, "Place approach lane");
                }

                point.position = L.Lanes[i].Spawn;
                lanes.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }

            so.ApplyModifiedProperties();
            return $"Night lanes: {string.Join(", ", System.Array.ConvertAll(L.Lanes, l => $"{l.Name} {l.Spawn}"))}, spread {L.LaneSpread:0.#} m.";
        }

        static string SetTownPatches()
        {
            TownZombies town = Object.FindFirstObjectByType<TownZombies>(FindObjectsInactive.Include);
            if (town == null) return "No TownZombies in the scene; no daytime wanderers to move.";

            var so = new SerializedObject(town);
            SerializedProperty areas = so.FindProperty("areas");
            areas.arraySize = L.TownPatches.Length;

            for (int i = 0; i < L.TownPatches.Length; i++)
            {
                SerializedProperty area = areas.GetArrayElementAtIndex(i);
                area.FindPropertyRelative("name").stringValue = L.TownPatches[i].name;
                area.FindPropertyRelative("centre").vector3Value = L.TownPatches[i].centre;
                area.FindPropertyRelative("size").vector2Value = L.TownPatches[i].size;
                area.FindPropertyRelative("count").intValue = L.TownPatches[i].count;
            }

            so.ApplyModifiedProperties();
            return $"Town zombies wander {L.TownPatches.Length} suburban streets by day.";
        }

        static string SetRoad(Scene scene)
        {
            GameObject visitors = FindRoot(scene, "VisitorSystem");
            if (visitors == null) return "WARNING: no VisitorSystem, so the road was not run out into the fog.";

            Transform road = visitors.transform.Find("Road");
            if (road != null)
            {
                var old = new List<GameObject>();
                foreach (Transform child in road) old.Add(child.gameObject);
                foreach (GameObject go in old) Undo.DestroyObjectImmediate(go);

                VisitorSceneSetup.BuildRoad(road);
                foreach (Transform child in road) Undo.RegisterCreatedObjectUndo(child.gameObject, "Lay road");
            }

            Transform route = visitors.transform.Find("Route");
            string ends = "";
            if (route != null)
            {
                foreach ((string name, Vector3 at) in new[] { ("RoadStart", ZoneLayout.RoadStart), ("RoadEnd", ZoneLayout.RoadEnd) })
                {
                    Transform node = route.Find(name);
                    if (node == null) continue;

                    Undo.RecordObject(node, "Move route end");
                    node.position = at;
                    ends += $" {name} {at}";
                }

                // Where a car turns off the road for the gate: on the road, square with the gate wherever that stands.
                Transform turn = route.Find("GateOutside");
                if (turn != null)
                {
                    Undo.RecordObject(turn, "Move route turn");
                    turn.position = new Vector3(turn.position.x, 0f, ZoneLayout.GateOutside.z);
                    ends += $" GateOutside {turn.position}";
                }
            }

            // The drive from the road to the car park, and the shuttle's stop on the road, follow it.
            Transform drive = visitors.transform.Find("Drive");
            Transform oldDrive = drive != null ? drive.Find("Drive") : null;
            if (oldDrive != null)
            {
                int index = oldDrive.GetSiblingIndex();
                bool active = oldDrive.gameObject.activeSelf;
                Undo.DestroyObjectImmediate(oldDrive.gameObject);

                // Left switched off if it was: the car park (Build Car Park) lays its own drive.
                GameObject slab = SurfaceKit.Slab(drive, "Drive", ZoneLayout.Drive, 0.045f, 0.04f, SurfaceKit.Asphalt());
                slab.transform.SetSiblingIndex(index);
                slab.SetActive(active);
                Undo.RegisterCreatedObjectUndo(slab, "Lay drive");
            }

            CarParkModel.RelayDriveToGate(visitors.transform);

            Transform stop = visitors.transform.Find("ShuttleStop");
            if (stop != null)
            {
                Undo.RecordObject(stop, "Move shuttle stop");
                stop.position = StartingThreatSceneSetup.ShuttleStop;
            }

            return $"Road runs x {ZoneLayout.RoadWestEnd:0} to {ZoneLayout.RoadEastEnd:0} along z {ZoneLayout.RoadCentreZ:0.##};{ends}" +
                   $"{(stop != null ? $"; shuttle stop {stop.position}" : "")}.";
        }

        static string SetCamera()
        {
            CameraPanZoomRig rig = Object.FindFirstObjectByType<CameraPanZoomRig>(FindObjectsInactive.Include);
            if (rig == null) return "No CameraPanZoomRig in the scene; pan limits left alone.";

            Rect bounds = L.CameraBounds;
            var so = new SerializedObject(rig);
            so.FindProperty("boundsCenter").vector2Value = bounds.center;
            so.FindProperty("boundsSize").vector2Value = bounds.size;
            so.ApplyModifiedProperties();

            return $"Camera pans x {bounds.xMin:0} to {bounds.xMax:0}, z {bounds.yMin:0} to {bounds.yMax:0}.";
        }

        /// <summary>
        /// The courtyard's locked border was made from the old environment's barricade row, and
        /// still lived inside it. With that environment switched off the border was invisible, so
        /// the pieces move to the courtyard's own locked group. The references do not change.
        /// </summary>
        static string RehomeCourtyardBorder(Scene scene)
        {
            int moved = 0;

            foreach (ExpansionArea area in Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (area.gameObject.scene != scene) continue;

                SerializedProperty locked = new SerializedObject(area).FindProperty("lockedVisuals");
                if (locked == null) continue;

                Transform home = area.transform.Find("LockedUntilClaimed");
                for (int i = 0; i < locked.arraySize; i++)
                {
                    var go = locked.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (go == null || !UnderOldEnvironment(go.transform)) continue;

                    if (home == null)
                    {
                        home = K.Group("LockedUntilClaimed", area.transform);
                        Undo.RegisterCreatedObjectUndo(home.gameObject, "Courtyard border");
                    }

                    Undo.SetTransformParent(go.transform, home, "Courtyard border");
                    moved++;
                }
            }

            return moved > 0
                ? $"Courtyard border: {moved} pieces moved out of the old environment so they show again."
                : "Courtyard border already outside the old environment.";
        }

        static bool UnderOldEnvironment(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (p.name == OldEnvironment && p.parent == null) return true;
            return false;
        }

        // --- Plumbing ------------------------------------------------------------------------------------------------------

        static Transform FreshRoot(Scene scene)
        {
            GameObject existing = FindRoot(scene, RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var go = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, RootName);
            return go.transform;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        static string Join(Dictionary<string, int> counts)
        {
            var parts = new List<string>();
            foreach (var kv in counts) parts.Add($"{kv.Key} {kv.Value}");
            return string.Join(", ", parts);
        }
    }
}
