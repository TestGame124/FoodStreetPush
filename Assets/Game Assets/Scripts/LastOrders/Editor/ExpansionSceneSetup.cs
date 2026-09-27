using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Expansion;
using LastOrders.Jobs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the first expansion area into the open scene: the overgrown courtyard beyond the
    /// compound's north wall, the main character who clears it, and the way out to it.
    ///
    /// Works on the level's own art rather than dropping in new props. The junk, rubble and
    /// sandbags already lying out there become the debris, grouped where pieces overlap so one
    /// heap is one job rather than five. The low concrete row across the yard and the stretch
    /// of wall in front of it become what disappears when the land is claimed. The compound
    /// wall carried round the yard's outer edge, in sections, is what the expansion builds -
    /// so the boundary visibly moves out.
    ///
    /// Safe to re-run. Debris already adopted is left as it is; everything else is found by
    /// name and brought back into line.
    ///
    /// Re-running 'Food Street / Visitors / Set Up Visitor System' stretches the north wall
    /// back across the gap, because that tool re-lays every boundary run. Run this again
    /// afterwards.
    /// </summary>
    public static class ExpansionSceneSetup
    {
        const string AreaName = "Area_Courtyard";

        // The yard, as a world rectangle, and the gap in the wall that leads to it. The east edge
        // continues the compound's east wall line so the new wall meets it - measured from that
        // wall on every run, because the compound can be widened after the yard is laid out.
        const float YardWestX = 21.5f;
        const float YardSouthZ = 35.7f;
        const float YardNorthZ = 48f;

        /// <summary>Where the east edge goes when the scene has no east wall to continue.</summary>
        const float YardEastFallbackX = 37.95f;

        static Rect Yard = Rect.MinMaxRect(YardWestX, YardSouthZ, YardEastFallbackX, YardNorthZ);

        const float GapFromX = 29f;
        const float GapToX = 32f;
        const float GapCentreX = (GapFromX + GapToX) * 0.5f;

        static readonly Vector3 MainCharacterHome = new Vector3(GapCentreX, 0f, 31f);
        static readonly Vector3 GapInside = new Vector3(GapCentreX, 0f, 33.6f);
        static readonly Vector3 GapOutside = new Vector3(GapCentreX, 0f, 36.8f);

        /// <summary>Art within this distance of each other, centre to centre, is one heap.</summary>
        const float ClusterDistance = 2.6f;

        static readonly string[] DebrisGroups = { "Rubble", "NearWallDebris", "Barricades" };

        [MenuItem("Last Orders/Expansion/Set Up Courtyard (Current Scene)", priority = 30)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would drive the expansion.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                "- Adds a MainCharacter worker\n" +
                "- Turns the junk and rubble beyond the north wall into clearable debris\n" +
                "- Puts zombies on the yard for a worker sent there to fight\n" +
                "- Opens a gap in the north wall and lays out the walls expanding will build\n\n" +
                "Back the scene up first if it is not under version control.",
                "Set up", "Cancel");

            if (!proceed) return;

            Apply(scope, save: true);
        }

        /// <summary>
        /// The setup against the open scene's scope, with no dialogs - for automation, which may
        /// not be able to reference the scope's type directly.
        /// </summary>
        public static string ApplyToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null
                ? Apply(scope, save)
                : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        /// <summary>The whole setup, with no dialogs, so it can also be driven from a script.</summary>
        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;
            K.Reset();

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance")
                .objectReferenceValue as GameBalanceConfig;

            ExpansionContentGenerator.Generate();
            WorldUiPrefabGenerator.Ensure(balance);

            WorkerAgent main = EnsureMainCharacter(scope);

            Yard = Rect.MinMaxRect(YardWestX, YardSouthZ, MeasureEastWall(), YardNorthZ);

            Transform areasRoot = FindOrCreateRoot(scene, "ExpansionAreas");
            Transform areaRoot = FindOrCreateChild(areasRoot, AreaName);
            MoveKeepingChildren(areaRoot, new Vector3(Yard.center.x, 0f, Yard.center.y));

            var area = areaRoot.GetComponent<ExpansionArea>() ?? areaRoot.gameObject.AddComponent<ExpansionArea>();

            Transform debrisRoot = FindOrCreateChild(areaRoot, "Debris");
            int adopted = debrisRoot.GetComponentsInChildren<Debris>(true).Length == 0
                ? AdoptDebris(scope, debrisRoot)
                : RewireDebris(scope, debrisRoot);

            Transform[] route = BuildRoute(areaRoot);

            var locked = new List<GameObject>();
            locked.AddRange(SplitNorthWall(areaRoot));
            locked.AddRange(BarrierRowInYard());

            Transform centre = FindOrCreateChild(areaRoot, "Centre");
            centre.position = new Vector3(Yard.center.x, 1f, Yard.center.y);

            ExpansionBuildPlan boundary = BuildNewBoundary(areaRoot, out int sections);
            GameObject[] unlocked = BuildClaimedContent(areaRoot);

            area.Configure(scope, ExpansionContentGenerator.LoadCourtyard(), route, centre, locked.ToArray(),
                           boundary, EffectPrefabGenerator.EnsureBuildDust(), unlocked);
            EditorUtility.SetDirty(area);

            // The zombies a worker sent out here has to deal with before the debris.
            if (ZombieContentGenerator.LoadWanderer() == null) ZombieContentGenerator.Generate(rebuild: false);
            FieldWorkSceneSetup.EnsureZombies(scope, area, ZombieContentGenerator.LoadWanderer());

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            string summary =
                $"[Last Orders] Courtyard set up in '{scene.name}'. MainCharacter: {(main != null ? main.name : "missing")}, " +
                $"{adopted} debris, {locked.Count} objects removed on claim, {sections} boundary sections to build, " +
                $"wall gap at X {GapFromX}-{GapToX}.\n" +
                "If 'Set Up Visitor System' is run again it will close the gap; re-run this afterwards.";

            Debug.Log(summary);
            return summary;
        }

        // --- The main character -------------------------------------------------------------

        static WorkerAgent EnsureMainCharacter(GameLifetimeScope scope)
        {
            foreach (WorkerAgent agent in Object.FindObjectsByType<WorkerAgent>(FindObjectsSortMode.None))
            {
                if (agent.Role != WorkerRole.MainCharacter) continue;

                if (agent.GetComponent<WorkerToolSwing>() == null) agent.gameObject.AddComponent<WorkerToolSwing>();
                return agent;
            }

            // Beside the other workers in the hierarchy, wherever those are.
            Transform parent = null;
            foreach (WorkerAgent agent in Object.FindObjectsByType<WorkerAgent>(FindObjectsSortMode.None))
            {
                parent = agent.transform.parent;
                break;
            }

            WorkerAgent made = MainLevelSetup.MakeWorker(scope, WorkerRole.MainCharacter, "MainCharacter", 0,
                                                         MainCharacterHome, parent);
            made.gameObject.AddComponent<WorkerToolSwing>();
            return made;
        }

        // --- Debris -------------------------------------------------------------------------

        sealed class Piece
        {
            public Transform Art;
            public string Group;
            public Bounds Bounds;
        }

        /// <summary>
        /// Gathers the loose art lying in the yard, groups what overlaps, and wraps each group as
        /// one piece of debris.
        /// </summary>
        static int AdoptDebris(GameLifetimeScope scope, Transform debrisRoot)
        {
            List<Piece> pieces = FindArtInYard();
            List<List<Piece>> heaps = Cluster(pieces);

            // Ordered by position, so the IDs come out the same on every machine that runs this.
            heaps.Sort((a, b) =>
            {
                Vector3 ca = Centre(a), cb = Centre(b);
                int byZ = ca.z.CompareTo(cb.z);
                return byZ != 0 ? byZ : ca.x.CompareTo(cb.x);
            });

            var counts = new Dictionary<string, int>();

            foreach (List<Piece> heap in heaps)
            {
                DebrisDefinition type = TypeOf(heap, out string label);

                counts.TryGetValue(label, out int n);
                counts[label] = n + 1;

                MakeDebris(scope, debrisRoot, heap, type, $"courtyard/{label}_{n + 1}", $"Debris_{label}_{n + 1}");
            }

            // A yard with nothing in it would have nothing to claim it; give it something to do.
            if (heaps.Count == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 at = new Vector3(Yard.xMin + 4f + i * 4.5f, 0f, Yard.center.y + 2f);
                    Transform pile = K.Group($"PrimitivePile_{i}", debrisRoot);
                    pile.position = at;
                    K.Box("Stones", pile, Vector3.zero, new Vector3(1.8f, 0.8f, 1.4f), K.Stone, 20f * i);
                    K.Box("Planks", pile, new Vector3(0.3f, 0.8f, 0.1f), new Vector3(1.6f, 0.2f, 0.3f), K.WoodDark, 35f);

                    var piece = new Piece { Art = pile, Group = "Rubble", Bounds = BoundsOf(pile) };
                    MakeDebris(scope, debrisRoot, new List<Piece> { piece }, ExpansionContentGenerator.LoadRubble(),
                               $"courtyard/rubble_{i + 1}", $"Debris_rubble_{i + 1}");
                }

                return 3;
            }

            return heaps.Count;
        }

        static List<Piece> FindArtInYard()
        {
            var pieces = new List<Piece>();
            GameObject environment = GameObject.Find("ApocalypseEnvironment");
            if (environment == null) return pieces;

            foreach (string groupName in DebrisGroups)
            {
                Transform group = environment.transform.Find(groupName);
                if (group == null) continue;

                foreach (Transform art in group)
                {
                    // The barrier row is a single object sixteen metres long; it is handled as
                    // part of the boundary, not as a heap.
                    if (art.name.StartsWith("Barrier_")) continue;

                    Bounds bounds = BoundsOf(art);
                    if (bounds.size == Vector3.zero) continue;
                    if (!Yard.Contains(new Vector2(bounds.center.x, bounds.center.z))) continue;

                    pieces.Add(new Piece { Art = art, Group = groupName, Bounds = bounds });
                }
            }

            return pieces;
        }

        /// <summary>Single-link grouping on centre distance: anything close to a heap joins it.</summary>
        static List<List<Piece>> Cluster(List<Piece> pieces)
        {
            var heaps = new List<List<Piece>>();
            var assigned = new HashSet<Piece>();

            foreach (Piece seed in pieces)
            {
                if (assigned.Contains(seed)) continue;

                var heap = new List<Piece> { seed };
                assigned.Add(seed);

                for (int i = 0; i < heap.Count; i++)
                {
                    foreach (Piece other in pieces)
                    {
                        if (assigned.Contains(other)) continue;

                        Vector2 a = new Vector2(heap[i].Bounds.center.x, heap[i].Bounds.center.z);
                        Vector2 b = new Vector2(other.Bounds.center.x, other.Bounds.center.z);
                        if (Vector2.Distance(a, b) > ClusterDistance) continue;

                        heap.Add(other);
                        assigned.Add(other);
                    }
                }

                heaps.Add(heap);
            }

            return heaps;
        }

        static DebrisDefinition TypeOf(List<Piece> heap, out string label)
        {
            bool barricade = false, rubble = false;

            foreach (Piece piece in heap)
            {
                barricade |= piece.Group == "Barricades";
                rubble |= piece.Group == "Rubble";
            }

            if (barricade)
            {
                label = "barricade";
                return ExpansionContentGenerator.LoadBarricade();
            }

            if (rubble)
            {
                label = "rubble";
                return ExpansionContentGenerator.LoadRubble();
            }

            label = "junk";
            return ExpansionContentGenerator.LoadJunk();
        }

        static void MakeDebris(GameLifetimeScope scope, Transform parent, List<Piece> heap, DebrisDefinition type,
                               string id, string name)
        {
            Bounds bounds = heap[0].Bounds;
            foreach (Piece piece in heap) bounds.Encapsulate(piece.Bounds);

            Transform root = K.Group(name, parent);
            root.position = new Vector3(bounds.center.x, 0f, bounds.center.z);

            // Everything that collapses together hangs off one pivot on the ground.
            Transform visual = K.Group("Visual", root);
            visual.localPosition = Vector3.zero;

            foreach (Piece piece in heap)
            {
                Undo.SetTransformParent(piece.Art, visual, "Adopt debris");
                ClearBatchingStatic(piece.Art);
            }

            // One box around the whole heap, a little taller than the art so a quick tap lands.
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, Mathf.Max(0.6f, bounds.max.y * 0.5f), 0f);
            box.size = new Vector3(bounds.size.x, Mathf.Max(1.2f, bounds.max.y), bounds.size.z);

            Transform anchor = K.Group("UiAnchor", root);
            anchor.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

            Transform stand = K.Group("WorkPoint", root);
            stand.position = WorkPointFor(bounds);

            var debris = root.gameObject.AddComponent<Debris>();
            debris.Configure(scope, id, type, stand, anchor, visual);
            EditorUtility.SetDirty(debris);
        }

        /// <summary>
        /// The edge of the heap nearest the way in, so the character works from the side they
        /// arrive on rather than walking round to the far side.
        /// </summary>
        static Vector3 WorkPointFor(Bounds bounds)
        {
            Vector3 centre = new Vector3(bounds.center.x, 0f, bounds.center.z);
            Vector3 toGap = GapOutside - centre;
            toGap.y = 0f;
            if (toGap.sqrMagnitude < 1e-4f) toGap = Vector3.back;
            toGap.Normalize();

            // How far the box reaches along that direction, plus room to swing.
            float reach = Mathf.Abs(toGap.x) * bounds.extents.x + Mathf.Abs(toGap.z) * bounds.extents.z;
            return centre + toGap * (reach + 0.5f);
        }

        /// <summary>Re-points already adopted debris at this scene's scope and the current assets.</summary>
        static int RewireDebris(GameLifetimeScope scope, Transform debrisRoot)
        {
            Debris[] all = debrisRoot.GetComponentsInChildren<Debris>(true);

            foreach (Debris debris in all)
            {
                var so = new SerializedObject(debris);
                so.FindProperty("scope").objectReferenceValue = scope;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return all.Length;
        }

        // --- The boundary ---------------------------------------------------------------------

        /// <summary>
        /// Cuts the north wall back to the start of the yard and puts the yard's stretch of it -
        /// with the gap - into two pieces that are removed on claim.
        ///
        /// The new pieces live under the area, not under OuterBoundries: the visitor setup
        /// sorts every run under that parent into north, south, east and west, and would treat
        /// any extra piece as part of the south wall.
        /// </summary>
        static List<GameObject> SplitNorthWall(Transform areaRoot)
        {
            var pieces = new List<GameObject>();

            GameObject wall = GameObject.Find("Restaurant/Zone/OuterBoundries/Cube (5)");
            if (wall == null)
            {
                Debug.LogWarning("[Last Orders] No north wall at Restaurant/Zone/OuterBoundries/Cube (5), so " +
                                 "no gap was opened. The main character will walk through whatever is there.");
                return pieces;
            }

            float wallEast = WallExtent(wall.transform, out float wallWest);

            Transform lockedRoot = FindOrCreateChild(areaRoot, "LockedUntilClaimed");

            GameObject west = FindOrCreateWallPiece(lockedRoot, "NorthWall_YardWest", wall);
            GameObject east = FindOrCreateWallPiece(lockedRoot, "NorthWall_YardEast", wall);

            SetRun(west.transform, Yard.xMin, GapFromX);
            SetRun(east.transform, GapToX, Mathf.Max(GapToX + 0.5f, wallEast));

            Undo.RecordObject(wall.transform, "Shorten north wall");
            SetRun(wall.transform, wallWest, Yard.xMin);

            pieces.Add(west);
            pieces.Add(east);
            return pieces;
        }

        /// <summary>
        /// The wall's east end, and through <paramref name="west"/> its west end. Measured once:
        /// on a re-run the wall is already short, so the recorded full length on the yard piece
        /// is the truth.
        /// </summary>
        static float WallExtent(Transform wall, out float west)
        {
            float half = wall.localScale.z * 0.5f;
            west = wall.position.x - half;
            float east = wall.position.x + half;

            // Already shortened by an earlier run: the east end is where the yard's east piece ends.
            if (east <= Yard.xMin + 0.01f)
            {
                GameObject yardEast = GameObject.Find($"ExpansionAreas/{AreaName}/LockedUntilClaimed/NorthWall_YardEast");
                if (yardEast != null) east = yardEast.transform.position.x + yardEast.transform.localScale.z * 0.5f;
            }

            return east;
        }

        static GameObject FindOrCreateWallPiece(Transform parent, string name, GameObject template)
        {
            Transform found = parent.Find(name);
            if (found != null) return found.gameObject;

            GameObject piece = Object.Instantiate(template, parent);
            piece.name = name;
            piece.transform.rotation = template.transform.rotation;
            piece.transform.localScale = template.transform.localScale;
            piece.transform.position = template.transform.position;

            Undo.RegisterCreatedObjectUndo(piece, "Wall piece");
            return piece;
        }

        /// <summary>Lays a wall that runs along X - yawed 90 degrees, length on its Z scale - between two points.</summary>
        static void SetRun(Transform wall, float fromX, float toX)
        {
            Vector3 position = wall.position;
            position.x = (fromX + toX) * 0.5f;
            wall.position = position;

            Vector3 scale = wall.localScale;
            scale.z = Mathf.Max(0.1f, toX - fromX);
            wall.localScale = scale;
        }

        /// <summary>
        /// The blocks of the concrete barrier row that stand inside the yard. They were the
        /// outer line while the yard was outside; once it is claimed the line has moved out to
        /// the new wall, so they go with the old one.
        /// </summary>
        static IEnumerable<GameObject> BarrierRowInYard()
        {
            GameObject row = GameObject.Find("ApocalypseEnvironment/Barricades/Barrier_North");
            if (row == null) yield break;

            foreach (Transform block in row.transform)
            {
                Bounds bounds = BoundsOf(block);
                if (bounds.size == Vector3.zero) continue;

                // Anything reaching the new wall line goes too; a block left half inside the
                // new wall reads as a mistake rather than as the old line.
                if (bounds.max.x < Yard.xMin - 0.5f || bounds.min.x > Yard.xMax + 0.5f) continue;

                yield return block.gameObject;
            }
        }

        static Transform[] BuildRoute(Transform areaRoot)
        {
            Transform routeRoot = FindOrCreateChild(areaRoot, "Route");

            Transform inside = FindOrCreateChild(routeRoot, "GapInside");
            inside.position = GapInside;

            Transform outside = FindOrCreateChild(routeRoot, "GapOutside");
            outside.position = GapOutside;

            return new[] { inside, outside };
        }

        /// <summary>Target length of one boundary section. Short enough that the wall visibly
        /// grows through the build, long enough that it is not a picket fence of cubes.</summary>
        const float SectionLength = 2.5f;

        /// <summary>
        /// The yard's new boundary: the compound wall carried round its west, north and east
        /// sides, cut into sections in the order they go up - out along the west side, across
        /// the north, back down the east. Built in the compound wall's own look and switched
        /// off; the expansion raises them.
        /// </summary>
        static ExpansionBuildPlan BuildNewBoundary(Transform areaRoot, out int sections)
        {
            Transform existing = areaRoot.Find("NewBoundary");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            Transform root = K.Group("NewBoundary", areaRoot);
            root.position = Vector3.zero;

            // The compound's own wall is the pattern: same material, height and thickness.
            GameObject template = GameObject.Find("Restaurant/Zone/OuterBoundries/Cube (5)");

            var pieces = new List<Transform>();

            // The side walls start at the old wall's outer face, so the new line meets it.
            float south = Yard.yMin;

            AddRun(pieces, root, template, alongX: false, line: Yard.xMin, from: south, to: Yard.yMax);
            AddRun(pieces, root, template, alongX: true, line: Yard.yMax, from: Yard.xMin, to: Yard.xMax);
            AddRun(pieces, root, template, alongX: false, line: Yard.xMax, from: Yard.yMax, to: south);

            foreach (Transform piece in pieces) piece.gameObject.SetActive(false);

            var plan = root.gameObject.AddComponent<ExpansionBuildPlan>();
            plan.Configure(pieces.ToArray());
            EditorUtility.SetDirty(plan);

            sections = pieces.Count;
            return plan;
        }

        /// <summary>One side of the boundary, in sections, walked from <paramref name="from"/> to <paramref name="to"/>.</summary>
        static void AddRun(List<Transform> pieces, Transform parent, GameObject template, bool alongX,
                           float line, float from, float to)
        {
            float length = Mathf.Abs(to - from);
            int count = Mathf.Max(1, Mathf.CeilToInt(length / SectionLength));
            float step = (to - from) / count;

            for (int i = 0; i < count; i++)
            {
                float mid = from + step * (i + 0.5f);
                Vector3 centre = alongX ? new Vector3(mid, 0f, line) : new Vector3(line, 0f, mid);

                Transform section = Section(parent, template, $"Wall_{pieces.Count:00}");

                // Length on Z scale, like the compound's own runs: yawed a quarter turn to run along X.
                Vector3 scale = section.localScale;
                section.rotation = Quaternion.Euler(0f, alongX ? 90f : 0f, 0f);
                section.localScale = new Vector3(scale.x, scale.y, Mathf.Abs(step));
                section.position = new Vector3(centre.x, template != null ? template.transform.position.y : 0.5f, centre.z);

                pieces.Add(section);
            }
        }

        static Transform Section(Transform parent, GameObject template, string name)
        {
            if (template == null)
            {
                // No compound wall to copy: a plain block the same shape.
                return K.Cube(name, parent, Vector3.zero, new Vector3(1f, 1f, 1f), K.Stone, 0f, collider: true).transform;
            }

            GameObject section = Object.Instantiate(template, parent);
            section.name = name;
            section.transform.localScale = new Vector3(template.transform.localScale.x, template.transform.localScale.y, 1f);
            return section.transform;
        }

        /// <summary>
        /// What the claimed land is for: two beds where the kitchen garden will go. Built
        /// switched off; it rises into place when the expansion finishes.
        /// </summary>
        static GameObject[] BuildClaimedContent(Transform areaRoot)
        {
            Transform existing = areaRoot.Find("ShownWhenClaimed");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            Transform root = K.Group("ShownWhenClaimed", areaRoot);
            root.position = Vector3.zero;

            Transform garden = K.Group("GardenPlaceholder", root);
            for (int i = 0; i < 2; i++)
            {
                Vector3 bed = new Vector3(26f + i * 6f, 0f, 44.5f);
                K.Box($"Bed_{i}", garden, bed, new Vector3(3.6f, 0.3f, 1.6f), K.Soil);

                for (int p = 0; p < 4; p++)
                {
                    Vector3 plant = bed + new Vector3(-1.2f + p * 0.8f, 0.45f, 0f);
                    K.Sphere($"Plant_{i}_{p}", garden, plant, 0.45f, p % 2 == 0 ? K.Leaf : K.LeafDeep);
                }
            }

            root.gameObject.SetActive(false);
            return new[] { root.gameObject };
        }

        // --- Plumbing ---------------------------------------------------------------------------

        /// <summary>
        /// The compound's east wall line: the boundary run that travels along Z furthest east.
        /// Falls back to where it stood when the yard was first laid out.
        /// </summary>
        static float MeasureEastWall()
        {
            GameObject boundary = GameObject.Find("Restaurant/Zone/OuterBoundries");
            if (boundary == null) return YardEastFallbackX;

            float east = float.NegativeInfinity;
            foreach (Transform wall in boundary.transform)
            {
                if (!wall.gameObject.activeSelf) continue;
                if (Mathf.Abs(wall.forward.x) > Mathf.Abs(wall.forward.z)) continue;

                east = Mathf.Max(east, wall.position.x);
            }

            return float.IsInfinity(east) ? YardEastFallbackX : east;
        }

        /// <summary>
        /// Re-centres the area on the yard without dragging what is in it. The debris, the
        /// zombies and the wall pieces hang off this root at world positions of their own, and
        /// moving the root with them would shift every one of them by however far the yard's
        /// centre moved.
        /// </summary>
        static void MoveKeepingChildren(Transform root, Vector3 position)
        {
            Vector3 delta = position - root.position;
            if (delta.sqrMagnitude < 1e-8f) return;

            Undo.RecordObject(root, "Re-centre expansion area");
            root.position = position;

            foreach (Transform child in root)
            {
                Undo.RecordObject(child, "Re-centre expansion area");
                child.position -= delta;
            }
        }

        static Vector3 Centre(List<Piece> heap)
        {
            Bounds bounds = heap[0].Bounds;
            foreach (Piece piece in heap) bounds.Encapsulate(piece.Bounds);
            return bounds.center;
        }

        static Bounds BoundsOf(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>
        /// Static batching bakes a mesh into place at build time, and a baked heap cannot shake
        /// or collapse. Only that flag is cleared; lighting flags are left alone so the baked
        /// lighting still applies.
        /// </summary>
        static void ClearBatchingStatic(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
            }
        }

        static Transform FindOrCreateRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go.transform;
        }

        static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null) return found;

            Transform created = K.Group(name, parent);
            Undo.RegisterCreatedObjectUndo(created.gameObject, name);
            return created;
        }
    }
}
