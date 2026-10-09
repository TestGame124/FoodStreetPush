using System.Collections.Generic;
using System.Text;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Effects;
using LastOrders.Expansion;
using LastOrders.Perimeter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Turns the compound's outer boundary into a proper wall: sections a couple of metres high
    /// with their own health, that zombies stop at and smash, and an automatic gate in every gap.
    ///
    /// The level's own boundary cubes stay where they are - the visitor setup and the expansion
    /// setup both measure and move them - and only stop being drawn. The wall is laid along
    /// their lines, in sections of about four metres, so a zombie breaks through one section
    /// rather than a whole side. Where land to claim lies across the defense ring, the stretch of
    /// wall facing it is laid along the expansion's own pieces, with the small gate out to it, and
    /// stands for good; the land's own new wall comes up round it as it is built.
    ///
    /// A gap between two pieces on the same line is a way in, and gets a gate: the south gate
    /// on the road, and the small ones out to the yard and the plots.
    ///
    /// Safe to re-run: everything it made is thrown away and laid again from the boundary as it
    /// stands, so run it again after 'Set Up Visitor System' or the expansion setup has moved
    /// the boundary.
    ///
    /// The wall, its gates and its bastions are built from <see cref="CompoundWallModels"/>; the
    /// boxes over the concrete-block texture they were first built from can be had back with
    /// <see cref="PreviousLook"/>.
    /// </summary>
    public static partial class CompoundWallSceneSetup
    {
        const string RootName = "CompoundWalls";
        const string BoundaryName = "OuterBoundries";
        const string PreviousLookKey = "LastOrders.CompoundWalls.PreviousLook";

        /// <summary>
        /// True to build the wall, gates and bastions as they first were - boxes over the concrete-block
        /// texture - instead of the modelled ones. Remembered by the editor, so every setup that lays the
        /// wall again lays it in the same look.
        /// </summary>
        public static bool PreviousLook
        {
            get => EditorPrefs.GetBool(PreviousLookKey, false);
            set => EditorPrefs.SetBool(PreviousLookKey, value);
        }

        const string WallMaterialPath = "Assets/Game Assets/Content/LastOrders/Walls/Wall_Brick.asset";
        const string DustPath = "Assets/Game Assets/Content/LastOrders/Prefabs/Effects/Fx_BuildDust.prefab";
        const string LampMaterialPath = "Assets/Game Assets/Content/LastOrders/Materials/Gate_Lamp.mat";

        /// <summary>Roughly how long one section is. Each run is cut into equal sections near this.</summary>
        const float SectionLength = 4f;

        const float Height = 2.1f;
        const float Thickness = 0.5f;
        const float FootingHeight = 0.22f;
        const float CapHeight = 0.12f;

        /// <summary>A gap this wide or wider between two pieces on one line is a way in.</summary>
        const float MinGap = 1.5f;

        /// <summary>Wider than this is not a gap but the end of the wall.</summary>
        const float MaxGap = 12f;

        const float MainGateHealth = 450f;
        const float SmallGateHealth = 300f;

        static readonly Color Panel = K.Hex("B7B2A6");
        static readonly Color Footing = K.Hex("7F7B72");
        static readonly Color Cap = K.Hex("9C978C");
        static readonly Color Pillar = K.Hex("8E8A82");
        static readonly Color Band = K.Hex("A29D91");
        static readonly Color Crack = K.Hex("4A4640");
        static readonly Color Post = K.Hex("5A5750");
        static readonly Color GateFrame = K.Hex("4F585E");
        static readonly Color GateSheet = K.Hex("8B5B3E");
        static readonly Color Hazard = K.Hex("E8B93C");

        /// <summary>One straight piece of the level's boundary, as a line.</summary>
        sealed class WallRun
        {
            public GameObject Source;

            /// <summary>What the piece exists only while: itself, when it belongs to an expansion.</summary>
            public GameObject TiedTo;

            public bool AlongX;
            public float Line;
            public float From;
            public float To;

            /// <summary>The middle of whichever area it walls in, for telling outside from inside.</summary>
            public Vector3 Interior;

            public string Label;

            /// <summary>The expansion the piece belongs to, by its wall label ("Yard"); null for the compound's own.</summary>
            public string Owner;

            public float Length => To - From;

            /// <summary>
            /// Pieces that can make a gate between them: the compound's own, or one expansion's pieces of
            /// one kind. Two plots' walls on the same line overlap, and a gap in one must not be read as
            /// filled by the other.
            /// </summary>
            public Object Group => TiedTo != null && TiedTo.transform.parent != null ? TiedTo.transform.parent : null;

            public WallRun Cut(float from, float to) => new WallRun
            {
                Source = Source, TiedTo = TiedTo, AlongX = AlongX, Line = Line, From = from, To = to,
                Interior = Interior, Label = Label, Owner = Owner,
            };

            public Vector3 Point(float along) =>
                AlongX ? new Vector3(along, 0f, Line) : new Vector3(Line, 0f, along);
        }

        [MenuItem("Last Orders/Compound Walls/Set Up Walls & Gates (Current Scene)", priority = 75)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would run the walls.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                "- Lays a proper wall with health along the compound boundary, in sections\n" +
                "- Puts an automatic gate in each gap: it opens for staff, visitors and cars, never for zombies\n" +
                "- Marks the ground inside the walls, so zombies getting in sends the staff to the shelters\n" +
                "- Stops drawing the old boundary cubes (they stay in the scene)\n\n" +
                "Re-run it after 'Set Up Visitor System' or the expansion setup moves the boundary.",
                "Set up", "Cancel");

            if (!proceed) return;

            Debug.Log(Apply(scope, save: true));
        }

        [MenuItem("Last Orders/Compound Walls/Rebuild Wall, Gate & Bastion Models (Current Scene)", priority = 76)]
        static void RebuildModels() => RunLook(previous: false);

        [MenuItem("Last Orders/Compound Walls/Restore Previous Walls, Gates & Bastions (Current Scene)", priority = 77)]
        static void RestorePrevious() => RunLook(previous: true);

        static void RunLook(bool previous)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Last Orders] Leave Play mode before rebuilding the walls.");
                return;
            }

            Debug.Log(ApplyLook(previous));
        }

        /// <summary>
        /// Lays the walls and gates again in the modelled look, or the previous one, re-dresses every
        /// bastion in the open scene in place and rebuilds the shop's bastion with the shooters that carry
        /// it. No dialogs; the scene is left unsaved.
        /// </summary>
        public static string ApplyLook(bool previous)
        {
            PreviousLook = previous;

            var report = new StringBuilder(ApplyToActiveScene(save: false));
            Scene scene = SceneManager.GetActiveScene();

            // The wall sites' bastions, in place: the sites and the defenses on them keep pointing at them.
            // A shooter bought from the shop carries the prefab's bastion, rebuilt below.
            int bastions = 0;
            foreach (GameObject top in scene.GetRootGameObjects())
            {
                foreach (CompoundWall wall in top.GetComponentsInChildren<CompoundWall>(true))
                {
                    if (wall.name != "Bastion" || PrefabUtility.IsPartOfPrefabInstance(wall)) continue;

                    RedressBastion(wall, (wall.transform.parent != null ? wall.transform.parent.GetSiblingIndex() : 0) + 1);
                    bastions++;
                }
            }

            report.Append($"  {bastions} wall-site bastion(s) re-dressed in place.\n");

            try
            {
                DefenseShopSetup.BuildItems();
                report.Append($"  The shop's bastion ({DefenseShopSetup.BastionPath}) and the shooters carrying it rebuilt.\n");
            }
            catch (System.Exception e)
            {
                report.Append($"  WARNING: the shop's bastion was not rebuilt ({e.Message}); run 'Set Up Defense Shop'.\n");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            report.Append(previous ? "  Look: the previous boxes and texture.\n" : "  Look: modelled.\n");
            return report.ToString();
        }

        /// <summary>The setup against the open scene's scope, with no dialogs - for automation.</summary>
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
            var report = new StringBuilder("[Last Orders] Compound walls set up.\n");

            Transform boundary = FindInScene(scene, BoundaryName);
            if (boundary == null)
                return $"[Last Orders] No '{BoundaryName}' in '{scene.name}', so there is no boundary to wall. Nothing was changed.";

            // --- Where the wall goes ---------------------------------------------------------------

            var runs = new List<WallRun>();

            for (int i = 0; i < boundary.childCount; i++)
            {
                Transform child = boundary.GetChild(i);
                if (child.gameObject.activeSelf && TryRun(child, null, out WallRun run)) runs.Add(run);
            }

            if (runs.Count == 0)
                return $"[Last Orders] '{BoundaryName}' has no wall pieces under it. Nothing was changed.";

            Rect compound = Extent(runs);
            Vector3 compoundCentre = new Vector3(compound.center.x, 0f, compound.center.y);

            foreach (WallRun run in runs)
            {
                run.Interior = compoundCentre;
                run.Label = SideOf(run, compound) + " wall";
            }

            var yardZones = new List<(Rect area, GameObject tiedTo, string name)>();
            var openings = new List<WallRun>();

            foreach (ExpansionArea area in Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (area.gameObject.scene != scene) continue;
                CollectExpansion(area, compound, runs, yardZones, openings);
            }

            // An expansion's front stands in for the compound's own wall where they overlap - the
            // whole stretch, gate gap and all - so the boundary cubes never need cutting up.
            int replaced = CutOpenings(runs, openings);
            if (replaced > 0) report.Append($"  {replaced} stretches of the compound wall stand in front of land to claim.\n");

            JoinAdjacent(yardZones);

            List<(WallRun left, WallRun right)> gaps = FindGaps(runs);

            // Where a run ends at a gate, the gate's pillar stands in for the wall's own post.
            var gateEnds = new HashSet<(WallRun run, bool atTo)>();
            foreach ((WallRun left, WallRun right) in gaps)
            {
                gateEnds.Add((left, true));
                gateEnds.Add((right, false));
            }

            // --- Build -------------------------------------------------------------------------------

            K.Reset();
            EnvKit.Reset();
            CompoundWallModels.Begin();
            painted = new[] { BuildingKit.PaintedMaterial() };
            if (PreviousLook) blockWallDark = SurfaceKit.CompoundWallDark();

            var material = AssetDatabase.LoadAssetAtPath<WallMaterialDefinition>(WallMaterialPath);
            if (material == null)
                report.Append($"  WARNING: no wall material at {WallMaterialPath}; sections use their own 350 health.\n");

            PooledEffect dust = AssetDatabase.LoadAssetAtPath<GameObject>(DustPath)?.GetComponent<PooledEffect>();

            Transform root = FreshRoot(scene);

            int sections = 0;
            var counters = new Dictionary<string, int>();

            foreach (WallRun run in runs)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(run.Length / SectionLength));
                float length = run.Length / count;

                for (int i = 0; i < count; i++)
                {
                    counters.TryGetValue(run.Label, out int n);
                    counters[run.Label] = n + 1;

                    Vector3 centre = run.Point(run.From + length * (i + 0.5f));
                    string name = $"{run.Label.Replace(' ', '_')}_{n:00}";

                    BuildSection(root, name, run, centre, length, first: i == 0, last: i == count - 1,
                                 gateAtStart: i == 0 && gateEnds.Contains((run, false)),
                                 gateAtEnd: i == count - 1 && gateEnds.Contains((run, true)),
                                 seed: sections, scope, material, dust);
                    sections++;
                }

                HideSource(run.Source);
            }

            report.Append($"  {sections} wall sections along {runs.Count} runs, {(material != null ? material.health : 350f):0} health each.\n");

            foreach ((WallRun left, WallRun right) in gaps)
            {
                CompoundGate gate = BuildGate(root, left, right, compound, compoundCentre, scope, dust);
                report.Append($"  {gate.DisplayName}: {gate.Length:0.0} m wide at {gate.transform.position}.\n");
            }

            if (gaps.Count == 0) report.Append("  WARNING: no gaps found, so there is no gate. Nobody can drive in.\n");

            // --- Inside ------------------------------------------------------------------------------

            float inset = Thickness * 0.5f + 0.05f;
            Rect inside = Rect.MinMaxRect(compound.xMin + inset, compound.yMin + inset, compound.xMax - inset, compound.yMax - inset);

            AddZone(root, "Inside_Compound", inside, null, scope);
            report.Append($"  Inside the walls: X {inside.xMin:0.0}-{inside.xMax:0.0}, Z {inside.yMin:0.0}-{inside.yMax:0.0}.\n");

            foreach ((Rect area, GameObject tiedTo, string name) in yardZones)
            {
                AddZone(root, $"Inside_{name}", area, tiedTo, scope);
                report.Append($"  Inside {name} once claimed: X {area.xMin:0.0}-{area.xMax:0.0}, Z {area.yMin:0.0}-{area.yMax:0.0}.\n");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return report.ToString();
        }

        // --- Reading the boundary ------------------------------------------------------------------

        /// <summary>
        /// The level's boundary pieces are cubes stretched along one axis. Their mesh, scale and turn
        /// give the line they stand on; anything that is not a straight axis-aligned piece is left out.
        /// </summary>
        static bool TryRun(Transform piece, GameObject tiedTo, out WallRun run)
        {
            run = null;

            var filter = piece.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || piece.GetComponent<MeshRenderer>() == null) return false;

            Bounds local = filter.sharedMesh.bounds;
            Vector3 centre = piece.TransformPoint(local.center);
            Vector3 alongLocalX = piece.TransformVector(Vector3.right * local.size.x);
            Vector3 alongLocalZ = piece.TransformVector(Vector3.forward * local.size.z);
            alongLocalX.y = 0f;
            alongLocalZ.y = 0f;

            Vector3 along = alongLocalX.sqrMagnitude >= alongLocalZ.sqrMagnitude ? alongLocalX : alongLocalZ;
            float length = along.magnitude;
            if (length < 0.5f) return false;

            bool alongX = Mathf.Abs(along.x) >= Mathf.Abs(along.z);
            float half = length * 0.5f;

            run = new WallRun
            {
                Source = piece.gameObject,
                TiedTo = tiedTo,
                AlongX = alongX,
                Line = alongX ? centre.z : centre.x,
                From = (alongX ? centre.x : centre.z) - half,
                To = (alongX ? centre.x : centre.z) + half,
            };

            return true;
        }

        /// <summary>
        /// An expansion's share of the wall: the stretch of the compound's wall in front of it, with
        /// the gate out to it, and the new wall its build raises. Its ground counts as inside once
        /// claimed.
        ///
        /// The stretch in front stands for good when its pieces are under the area's
        /// <see cref="LandPlotSetup.CompoundFrontName"/> group: the land is out across the defense
        /// ring and walled on its own once claimed. Wall pieces among what is shown until the land is
        /// claimed instead go when it is - land that, claimed, joins the compound.
        /// </summary>
        static void CollectExpansion(ExpansionArea area, Rect compound, List<WallRun> runs,
                                     List<(Rect, GameObject, string)> zones, List<WallRun> openings)
        {
            var so = new SerializedObject(area);
            string owner = area.Definition != null ? area.Definition.WallLabel : "Yard";
            var compoundCentre = new Vector3(compound.center.x, 0f, compound.center.y);

            var front = new List<WallRun>();

            Transform standing = area.transform.Find(LandPlotSetup.CompoundFrontName);
            for (int i = 0; standing != null && i < standing.childCount; i++)
            {
                if (!TryRun(standing.GetChild(i), null, out WallRun run)) continue;

                run.Interior = compoundCentre;
                run.Label = SideOf(run, compound) + " wall";
                run.Owner = owner;
                runs.Add(run);
                front.Add(run);
            }

            SerializedProperty locked = so.FindProperty("lockedVisuals");
            for (int i = 0; locked != null && i < locked.arraySize; i++)
            {
                var piece = locked.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (piece == null || !piece.name.Contains("Wall")) continue;

                if (!TryRun(piece.transform, piece, out WallRun run)) continue;

                run.Interior = compoundCentre;
                run.Label = SideOf(run, compound) + " wall";
                run.Owner = owner;
                runs.Add(run);
                front.Add(run);
            }

            openings.AddRange(Spans(front));

            // The new wall round the yard.
            var plan = so.FindProperty("buildPlan").objectReferenceValue as ExpansionBuildPlan;
            if (plan == null) return;

            var planSo = new SerializedObject(plan);
            SerializedProperty pieces = planSo.FindProperty("pieces");

            var yardRuns = new List<WallRun>();
            for (int i = 0; pieces != null && i < pieces.arraySize; i++)
            {
                var piece = pieces.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (piece == null || !TryRun(piece, piece.gameObject, out WallRun run)) continue;

                run.Label = owner + " wall";
                run.Owner = owner;
                yardRuns.Add(run);
            }

            if (yardRuns.Count == 0) return;

            Rect yard = Extent(yardRuns);
            Vector3 yardCentre = new Vector3(yard.center.x, 0f, yard.center.y);
            foreach (WallRun run in yardRuns) run.Interior = yardCentre;
            runs.AddRange(yardRuns);

            // Its ground, reaching back into the compound on the side the two meet, so the strip
            // where the old wall stood is inside too once it has gone.
            float inset = Thickness * 0.5f + 0.05f;
            Rect ground = Rect.MinMaxRect(yard.xMin + inset, yard.yMin + inset, yard.xMax - inset, yard.yMax - inset);

            if (Mathf.Abs(ground.yMin - compound.yMax) < 1.5f) ground.yMin = compound.yMax - inset;
            if (Mathf.Abs(ground.yMax - compound.yMin) < 1.5f) ground.yMax = compound.yMin + inset;
            if (Mathf.Abs(ground.xMin - compound.xMax) < 1.5f) ground.xMin = compound.xMax - inset;
            if (Mathf.Abs(ground.xMax - compound.xMin) < 1.5f) ground.xMax = compound.xMin + inset;

            SerializedProperty unlocked = so.FindProperty("unlockedContent");
            GameObject claimed = unlocked != null && unlocked.arraySize > 0
                ? unlocked.GetArrayElementAtIndex(0).objectReferenceValue as GameObject
                : null;

            if (claimed == null)
            {
                Debug.LogWarning($"[Last Orders] {area.name} shows nothing once claimed, so its ground is never " +
                                 "counted as inside the walls.", area);
                return;
            }

            zones.Add((ground, claimed, area.name.Replace("Area_", "")));
        }

        /// <summary>
        /// Two pieces on one line with a gap between them a gate fits in. Only pieces of one group
        /// are paired: where two plots' walls share a line, one plot's gate gap is not closed by the
        /// other's wall, which never stands at the same time.
        /// </summary>
        static List<(WallRun, WallRun)> FindGaps(List<WallRun> runs)
        {
            var gaps = new List<(WallRun, WallRun)>();
            var groups = new Dictionary<Object, List<WallRun>>();
            var compound = new List<WallRun>();

            foreach (WallRun run in runs)
            {
                Object key = run.Group;
                if (key == null)
                {
                    compound.Add(run);
                    continue;
                }

                if (!groups.TryGetValue(key, out List<WallRun> list)) groups[key] = list = new List<WallRun>();
                list.Add(run);
            }

            FindGapsIn(compound, gaps);
            foreach (List<WallRun> group in groups.Values) FindGapsIn(group, gaps);
            return gaps;
        }

        static void FindGapsIn(List<WallRun> runs, List<(WallRun, WallRun)> gaps)
        {
            var sorted = new List<WallRun>(runs);
            sorted.Sort((a, b) =>
            {
                int axis = a.AlongX.CompareTo(b.AlongX);
                if (axis != 0) return axis;

                int line = Mathf.RoundToInt(a.Line * 4f).CompareTo(Mathf.RoundToInt(b.Line * 4f));
                return line != 0 ? line : a.From.CompareTo(b.From);
            });

            for (int i = 0; i + 1 < sorted.Count; i++)
            {
                WallRun a = sorted[i];
                WallRun b = sorted[i + 1];

                if (a.AlongX != b.AlongX || Mathf.Abs(a.Line - b.Line) > 0.3f) continue;

                float gap = b.From - a.To;
                if (gap >= MinGap && gap <= MaxGap) gaps.Add((a, b));
            }
        }

        /// <summary>The stretch each line of an expansion's front covers, end to end, gap included.</summary>
        static IEnumerable<WallRun> Spans(List<WallRun> front)
        {
            var done = new List<WallRun>();

            foreach (WallRun run in front)
            {
                WallRun span = null;
                foreach (WallRun d in done)
                    if (d.AlongX == run.AlongX && Mathf.Abs(d.Line - run.Line) < 0.3f) span = d;

                if (span == null) done.Add(run.Cut(run.From, run.To));
                else
                {
                    span.From = Mathf.Min(span.From, run.From);
                    span.To = Mathf.Max(span.To, run.To);
                }
            }

            return done;
        }

        /// <summary>
        /// Takes each opening out of the compound's own runs on its line, keeping whatever is left
        /// either side of it. Returns how many runs were cut.
        /// </summary>
        static int CutOpenings(List<WallRun> runs, List<WallRun> openings)
        {
            int cut = 0;

            foreach (WallRun opening in openings)
            {
                for (int i = runs.Count - 1; i >= 0; i--)
                {
                    // Only the compound's own boundary is cut: never an expansion's piece, standing for good or not.
                    WallRun run = runs[i];
                    if (run.TiedTo != null || run.Owner != null) continue;
                    if (run.AlongX != opening.AlongX || Mathf.Abs(run.Line - opening.Line) > 0.3f) continue;
                    if (run.To <= opening.From + 0.05f || run.From >= opening.To - 0.05f) continue;

                    runs.RemoveAt(i);
                    cut++;

                    // What stands either side of the opening is still the compound's.
                    if (opening.From - run.From > 0.3f) runs.Add(run.Cut(run.From, opening.From));
                    if (run.To - opening.To > 0.3f) runs.Add(run.Cut(opening.To, run.To));
                }
            }

            return cut;
        }

        /// <summary>
        /// Two pieces of land claimed side by side are one ground: where two zones' edges are within a
        /// wall's width of each other, both are carried to the line between, so nothing walking from one
        /// into the other is ever outside for a step.
        /// </summary>
        static void JoinAdjacent(List<(Rect area, GameObject tiedTo, string name)> zones)
        {
            const float Reach = 1.5f;

            for (int i = 0; i < zones.Count; i++)
            {
                for (int j = 0; j < zones.Count; j++)
                {
                    if (i == j) continue;

                    Rect a = zones[i].area, b = zones[j].area;
                    bool overlapZ = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > 0.5f;
                    bool overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > 0.5f;

                    // a's east edge meets b's west edge; a's north edge meets b's south edge. The
                    // other two ways round come up when i and j swap.
                    if (overlapZ && Mathf.Abs(a.xMax - b.xMin) < Reach && a.xMax < b.xMax)
                    {
                        float mid = (a.xMax + b.xMin) * 0.5f;
                        a.xMax = mid;
                        b.xMin = mid;
                    }

                    if (overlapX && Mathf.Abs(a.yMax - b.yMin) < Reach && a.yMax < b.yMax)
                    {
                        float mid = (a.yMax + b.yMin) * 0.5f;
                        a.yMax = mid;
                        b.yMin = mid;
                    }

                    zones[i] = (a, zones[i].tiedTo, zones[i].name);
                    zones[j] = (b, zones[j].tiedTo, zones[j].name);
                }
            }
        }

        static Rect Extent(List<WallRun> runs)
        {
            float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;

            foreach (WallRun run in runs)
            {
                foreach (Vector3 p in new[] { run.Point(run.From), run.Point(run.To) })
                {
                    xMin = Mathf.Min(xMin, p.x);
                    xMax = Mathf.Max(xMax, p.x);
                    zMin = Mathf.Min(zMin, p.z);
                    zMax = Mathf.Max(zMax, p.z);
                }
            }

            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        static string SideOf(WallRun run, Rect compound)
        {
            if (run.AlongX)
                return Mathf.Abs(run.Line - compound.yMin) < Mathf.Abs(run.Line - compound.yMax) ? "South" : "North";

            return Mathf.Abs(run.Line - compound.xMin) < Mathf.Abs(run.Line - compound.xMax) ? "West" : "East";
        }

        /// <summary>Degrees about Y that turn local X to run along the piece.</summary>
        static float YawAlong(WallRun run) => run.AlongX ? 0f : -90f;

        // --- Wall sections -------------------------------------------------------------------------

        /// <summary>The modelled pieces' material, set at the start of each build.</summary>
        static Material[] painted;

        static void BuildSection(Transform root, string name, WallRun run, Vector3 centre, float length, bool first,
                                 bool last, bool gateAtStart, bool gateAtEnd, int seed, GameLifetimeScope scope,
                                 WallMaterialDefinition material, PooledEffect dust)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, YawAlong(run), 0f));

            // Which local face looks out, so the patching goes on the side a zombie sees.
            Vector3 outward = centre - run.Interior;
            float outside = Vector3.Dot(go.transform.rotation * Vector3.back, outward) > 0f ? -1f : 1f;

            Transform visual = K.Group("Visual", go.transform);
            GameObject intact, damaged, rubble;

            if (PreviousLook)
            {
                BuildBakedLooks(visual, name, run, centre, length, first, last, outside, seed, out intact, out damaged, out rubble);
            }
            else
            {
                // Every section puts up the post at its start, the last of a run the one at its end too;
                // where a gate is, its pillar stands there instead.
                CompoundWallModels.End start = gateAtStart ? CompoundWallModels.End.Gate : CompoundWallModels.End.Post;
                CompoundWallModels.End end = gateAtEnd ? CompoundWallModels.End.Gate
                                           : last ? CompoundWallModels.End.Post
                                           : CompoundWallModels.End.Open;

                // The models are built with +Z out; a section whose outside is its -Z has them turned round.
                // Its make is picked by where it stands, not its name: the runs are numbered in whatever
                // order the scene hands them over, and a rebuild must not patch a different stretch.
                bool turned = outside < 0f;
                string spot = $"{Mathf.RoundToInt(centre.x * 10f)},{Mathf.RoundToInt(centre.z * 10f)}";
                CompoundWallModels.Section section = CompoundWallModels.SectionFor(spot, length, start, end, turned);

                intact = ModelledLook(visual, "Intact", section, CompoundWallModels.State.Intact, turned);
                damaged = ModelledLook(visual, "Damaged", section, CompoundWallModels.State.Damaged, turned);
                rubble = ModelledLook(visual, "Rubble", section, CompoundWallModels.State.Rubble, turned);
                damaged.SetActive(false);
                rubble.SetActive(false);
            }

            Transform bar = K.Group("HealthBarAnchor", go.transform);
            bar.localPosition = new Vector3(0f, 0.3f, 0f);

            var wall = go.AddComponent<CompoundWall>();
            wall.ConfigureStructure(scope, run.Label, length, Thickness, run.TiedTo, visual.gameObject, bar, dust);
            wall.ConfigureWall(material, intact, damaged, rubble);

            // As it will look when the game starts: an expansion's piece is only there once it is.
            if (run.TiedTo != null && !run.TiedTo.activeInHierarchy) visual.gameObject.SetActive(false);
        }

        /// <summary>One of a section's looks: its bays and posts under a group, turned round with them when its outside is its -Z.</summary>
        static GameObject ModelledLook(Transform visual, string name, CompoundWallModels.Section section, CompoundWallModels.State state, bool turned)
        {
            Transform group = K.Group(name, visual);
            if (turned) group.localRotation = Quaternion.Euler(0f, 180f, 0f);

            CompoundWallModels.Build(group, section, state, painted);
            return group.gameObject;
        }

        /// <summary>
        /// The previous look: each state built from boxes, then baked to one renderer with the wall
        /// texture running along the wall.
        /// </summary>
        static void BuildBakedLooks(Transform visual, string name, WallRun run, Vector3 centre, float length, bool first, bool last,
                                    float outside, int seed, out GameObject intact, out GameObject damaged, out GameObject rubble)
        {
            var rng = new System.Random(seed * 7919 + 17);
            float along = run.AlongX ? centre.x : centre.z;
            blockWall = SurfaceKit.CompoundWall(PanelFor(name));

            Transform whole = K.Group("Intact", visual);
            BuildIntact(whole, length, first, last, outside, seed);
            MeshBaker.Bake(whole, WallRemap, along, TextureGenerator.WallTileLength, TextureGenerator.WallHeight);

            Transform broken = K.Group("Damaged", visual);
            BuildDamaged(broken, length, first, last, outside, rng);
            MeshBaker.Bake(broken, WallRemap, along, TextureGenerator.WallTileLength, TextureGenerator.WallHeight);
            broken.gameObject.SetActive(false);

            Transform fallen = K.Group("Rubble", visual);
            BuildRubble(fallen, length, first, last, rng);
            MeshBaker.Bake(fallen, WallRemap, along, TextureGenerator.WallTileLength, TextureGenerator.WallHeight);
            fallen.gameObject.SetActive(false);

            intact = whole.gameObject;
            damaged = broken.gameObject;
            rubble = fallen.gameObject;
        }

        static void BuildIntact(Transform p, float length, bool first, bool last, float outside, int seed)
        {
            float panelHeight = Height - FootingHeight - CapHeight;

            K.Box("Footing", p, Vector3.zero, new Vector3(length, FootingHeight, Thickness + 0.14f), Footing);
            K.Box("Panel", p, new Vector3(0f, FootingHeight, 0f), new Vector3(length, panelHeight, Thickness), Panel);
            K.Box("Cap", p, new Vector3(0f, Height - CapHeight, 0f), new Vector3(length + 0.02f, CapHeight, Thickness + 0.1f), Cap);

            // The blocks and their courses are in the texture; the course line that used to be a
            // mesh of its own is not needed.

            if (first) BuildPillar(p, -length * 0.5f, Height);
            if (last) BuildPillar(p, length * 0.5f, Height);

            // Every third section has been patched with planks: a wall people have been holding.
            if (seed % 3 != 1) return;

            float z = outside * (Thickness * 0.5f + 0.03f);
            GameObject low = K.Box("Board_0", p, new Vector3(-length * 0.12f, 0.55f, z), new Vector3(1.2f, 0.22f, 0.05f), K.Wood);
            low.transform.localRotation = Quaternion.Euler(0f, 0f, 6f);

            GameObject high = K.Box("Board_1", p, new Vector3(-length * 0.08f, 0.85f, z), new Vector3(1.0f, 0.2f, 0.05f), K.WoodLight);
            high.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
        }

        static Material blockWall;
        static Material blockWallDark;

        /// <summary>
        /// What a section is made of: concrete slabs mostly, about one in five patched with
        /// timber and one in six with tin, as a wall held for a year would be. Decided by the
        /// section's name, so a rebuild - or a restyle of a scene built before panels came in -
        /// gives every section the same panel again.
        /// </summary>
        public static TextureGenerator.WallPanel PanelFor(string sectionName)
        {
            // FNV-1a: string.GetHashCode is not promised to be the same from one run to the next.
            uint hash = 2166136261;
            foreach (char c in sectionName) hash = (hash ^ c) * 16777619;

            uint roll = hash % 100;
            return roll < 19 ? TextureGenerator.WallPanel.Planks
                 : roll < 35 ? TextureGenerator.WallPanel.Tin
                 : TextureGenerator.WallPanel.Concrete;
        }

        /// <summary>
        /// Which of a section's flat colours become the wall texture: the panel, footing, cap and
        /// band all do - the texture has a band for each at its height - and the pillars take the
        /// darker concrete. Planks, rebar and cracks keep their own materials and UVs.
        /// </summary>
        static Material WallRemap(Material source, out bool project)
        {
            project = true;
            if (source == K.MaterialFor(Panel) || source == K.MaterialFor(Footing) || source == K.MaterialFor(Cap) ||
                source == K.MaterialFor(Band))
                return blockWall;

            if (source == K.MaterialFor(Pillar)) return blockWallDark;

            project = false;
            return source;
        }

        static void BuildPillar(Transform p, float x, float height)
        {
            K.Box($"Pillar_{(x < 0f ? "Start" : "End")}", p, new Vector3(x, 0f, 0f),
                  new Vector3(0.55f, height + 0.2f, Thickness + 0.2f), Pillar);

            if (height >= Height - 0.01f)
            {
                K.Box($"PillarCap_{(x < 0f ? "Start" : "End")}", p, new Vector3(x, height + 0.2f, 0f),
                      new Vector3(0.66f, 0.08f, Thickness + 0.3f), Cap);
            }
        }

        /// <summary>Below half: the panel broken down to three jagged stumps, a crack, and chunks at its foot.</summary>
        static void BuildDamaged(Transform p, float length, bool first, bool last, float outside, System.Random rng)
        {
            float panelHeight = Height - FootingHeight - CapHeight;

            K.Box("Footing", p, Vector3.zero, new Vector3(length, FootingHeight, Thickness + 0.14f), Footing);

            float[] heights = { 0.86f, 0.5f, 0.7f };
            int shift = rng.Next(3);
            float third = length / 3f;

            for (int k = 0; k < 3; k++)
            {
                float h = panelHeight * heights[(k + shift) % 3];
                float x = -length * 0.5f + third * (k + 0.5f);

                K.Box($"Stump_{k}", p, new Vector3(x, FootingHeight, 0f), new Vector3(third + 0.01f, h, Thickness), Panel);

                GameObject jag = K.Cube($"Jag_{k}", p, new Vector3(x + third * 0.15f, FootingHeight + h, 0f),
                                        new Vector3(third * 0.55f, 0.2f, Thickness * 0.9f), Panel);
                jag.transform.localRotation = Quaternion.Euler(0f, 0f, (k % 2 == 0 ? 1f : -1f) * 14f);
            }

            if (first) BuildPillar(p, -length * 0.5f, Height * 0.9f);
            if (last) BuildPillar(p, length * 0.5f, Height * 0.9f);

            GameObject crack = K.Cube("Crack", p, new Vector3(-length * 0.2f, 0.75f, outside * (Thickness * 0.5f + 0.01f)),
                                      new Vector3(0.05f, 0.8f, 0.02f), Crack);
            crack.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);

            for (int k = 0; k < 3; k++)
            {
                float x = (float)(rng.NextDouble() - 0.5) * length * 0.8f;
                float z = outside * (Thickness * 0.5f + 0.25f + (float)rng.NextDouble() * 0.4f);
                float s = 0.25f + (float)rng.NextDouble() * 0.2f;

                K.Box($"Chunk_{k}", p, new Vector3(x, 0f, z), new Vector3(s * 1.3f, s, s), Panel, yaw: rng.Next(0, 90));
            }
        }

        /// <summary>Fallen: the footing, a stump of pillar, a heap of blocks and some bent rebar.</summary>
        static void BuildRubble(Transform p, float length, bool first, bool last, System.Random rng)
        {
            K.Box("Footing", p, Vector3.zero, new Vector3(length, 0.16f, Thickness + 0.14f), Footing);

            if (first) K.Box("Stump_Start", p, new Vector3(-length * 0.5f, 0f, 0f), new Vector3(0.55f, 0.7f, Thickness + 0.2f), Pillar);
            if (last) K.Box("Stump_End", p, new Vector3(length * 0.5f, 0f, 0f), new Vector3(0.55f, 0.7f, Thickness + 0.2f), Pillar);

            Color[] colours = { Panel, Footing, Cap };

            for (int k = 0; k < 6; k++)
            {
                float x = (float)(rng.NextDouble() - 0.5) * (length - 0.6f);
                float z = (float)(rng.NextDouble() - 0.5) * 1.2f;
                var size = new Vector3(0.4f + (float)rng.NextDouble() * 0.4f, 0.2f + (float)rng.NextDouble() * 0.25f,
                                       0.3f + (float)rng.NextDouble() * 0.3f);

                GameObject block = K.Box($"Block_{k}", p, new Vector3(x, 0.08f, z), size, colours[k % colours.Length]);
                block.transform.localRotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 24f, rng.Next(0, 180), 0f);
            }

            for (int k = 0; k < 2; k++)
            {
                float x = (k == 0 ? -0.25f : 0.2f) * length;
                K.Log($"Rebar_{k}", p, new Vector3(x, 0.4f, 0f), 0.04f, 0.8f, K.MetalDark, yaw: rng.Next(0, 180), tilt: 35f);
            }
        }

        static void HideSource(GameObject source)
        {
            var renderer = source != null ? source.GetComponent<MeshRenderer>() : null;
            if (renderer == null || !renderer.enabled) return;

            Undo.RecordObject(renderer, "Hide old boundary");
            renderer.enabled = false;
        }

        // --- Gates ---------------------------------------------------------------------------------

        static CompoundGate BuildGate(Transform root, WallRun left, WallRun right, Rect compound, Vector3 compoundCentre,
                                      GameLifetimeScope scope, PooledEffect dust)
        {
            float width = right.From - left.To;
            Vector3 centre = left.Point((left.To + right.From) * 0.5f);

            // Local +Z faces in, so the leaves are drawn the right way round from inside.
            float yaw = YawAlong(left);
            Vector3 inward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            if (Vector3.Dot(inward, compoundCentre - centre) < 0f) yaw += 180f;

            // The main gates are the compound's own; one out to a piece of land is a small gate named
            // for it, whether it stands for good or comes and goes with the land.
            GameObject tiedTo = left.TiedTo != null ? left.TiedTo : right.TiedTo;
            string owner = left.Owner ?? right.Owner;
            bool main = tiedTo == null && owner == null;

            string label = main ? SideOf(left, compound) + " gate" : $"{owner ?? "Yard"} gate";

            var go = new GameObject(label.Replace(' ', '_'));
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));

            Transform visual = K.Group("Visual", go.transform);

            Material lampMaterial = LampMaterial();
            var lamps = new List<Renderer>();

            Transform standing, broken, leftLeaf, rightLeaf;
            float leafWidth;

            if (PreviousLook)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = side * width * 0.5f;
                    string tag = side < 0f ? "L" : "R";

                    K.Box($"Post_{tag}", visual, new Vector3(x, 0f, 0f), new Vector3(0.75f, Height + 0.55f, Thickness + 0.3f), Post);
                    K.Box($"PostCap_{tag}", visual, new Vector3(x, Height + 0.55f, 0f), new Vector3(0.86f, 0.1f, Thickness + 0.4f), Cap);

                    GameObject lamp = K.Cube($"Lamp_{tag}", visual, new Vector3(x, Height + 0.83f, 0f), new Vector3(0.3f, 0.3f, 0.3f), Hazard);
                    var renderer = lamp.GetComponent<Renderer>();
                    if (lampMaterial != null) renderer.sharedMaterial = lampMaterial;
                    lamps.Add(renderer);
                }

                standing = K.Group("Standing", visual);
                K.Box("Track", standing, Vector3.zero, new Vector3(width + 0.4f, 0.03f, 0.18f), K.MetalDark);

                leafWidth = width * 0.5f + 0.05f;
                float leafHeight = Height - 0.12f;

                leftLeaf = K.Group("Leaf_L", standing);
                leftLeaf.localPosition = new Vector3(-width * 0.25f, 0f, 0f);
                BuildLeaf(leftLeaf, leafWidth, leafHeight);

                rightLeaf = K.Group("Leaf_R", standing);
                rightLeaf.localPosition = new Vector3(width * 0.25f, 0f, 0f);
                BuildLeaf(rightLeaf, leafWidth, leafHeight);

                broken = K.Group("Broken", visual);
                BuildBrokenGate(broken, width, leafWidth);
            }
            else
            {
                // The pillars, threshold and rail stay whatever happens to the leaves.
                CompoundWallModels.Part(visual, "Pillars", CompoundWallModels.GatePillars(width), painted);

                Material[] glass = { lampMaterial };
                foreach (float side in new[] { -1f, 1f })
                {
                    GameObject lamp = CompoundWallModels.Part(visual, side < 0f ? "Lamp_L" : "Lamp_R", CompoundWallModels.GateLens(), glass);
                    lamp.transform.localPosition = new Vector3(side * width * 0.5f, CompoundWallModels.LampHeight, 0f);

                    var renderer = lamp.GetComponent<Renderer>();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lamps.Add(renderer);
                }

                // Two leaves meeting in the middle, a hair apart.
                leafWidth = width * 0.5f - 0.01f;
                Mesh leftMesh = CompoundWallModels.GateLeaf(leafWidth, left: true);
                Mesh rightMesh = CompoundWallModels.GateLeaf(leafWidth, left: false);

                standing = K.Group("Standing", visual);
                leftLeaf = CompoundWallModels.Part(standing, "Leaf_L", leftMesh, painted).transform;
                leftLeaf.localPosition = new Vector3(-(width * 0.25f + 0.005f), 0f, 0f);
                rightLeaf = CompoundWallModels.Part(standing, "Leaf_R", rightMesh, painted).transform;
                rightLeaf.localPosition = new Vector3(width * 0.25f + 0.005f, 0f, 0f);

                broken = K.Group("Broken", visual);
                CompoundWallModels.Part(broken, "Wreck", CompoundWallModels.GateWreck(width), painted);
                Transform fallenLeft = CompoundWallModels.Part(broken, "Fallen_L", leftMesh, painted).transform;
                Transform fallenRight = CompoundWallModels.Part(broken, "Fallen_R", rightMesh, painted).transform;
                CompoundWallModels.FallenLeaves(fallenLeft, fallenRight, width);
            }

            broken.gameObject.SetActive(false);

            Transform bar = K.Group("HealthBarAnchor", go.transform);
            bar.localPosition = new Vector3(0f, 0.3f, 0f);

            var gate = go.AddComponent<CompoundGate>();
            gate.ConfigureStructure(scope, label, width, Thickness, tiedTo, visual.gameObject, bar, dust);

            // Each leaf slides its whole width into the wall beside it, like a pocket door.
            gate.ConfigureGate(main ? MainGateHealth : SmallGateHealth, leftLeaf, rightLeaf, leafWidth + 0.15f,
                               standing.gameObject, broken.gameObject, lamps.ToArray(), wait: main ? 2f : 1.2f);

            if (tiedTo != null && !tiedTo.activeInHierarchy) visual.gameObject.SetActive(false);

            return gate;
        }

        /// <summary>One sliding leaf: a rusted sheet along the bottom with a hazard stripe, bars above.</summary>
        static void BuildLeaf(Transform leaf, float width, float height)
        {
            K.Box("Sheet", leaf, new Vector3(0f, 0.08f, 0f), new Vector3(width, 0.8f, 0.07f), GateSheet);
            K.Box("Stripe", leaf, new Vector3(0f, 0.6f, 0f), new Vector3(width, 0.12f, 0.09f), Hazard);

            K.Cube("TopRail", leaf, new Vector3(0f, height - 0.05f, 0f), new Vector3(width, 0.1f, 0.1f), GateFrame);
            K.Cube("MidRail", leaf, new Vector3(0f, 0.92f, 0f), new Vector3(width, 0.08f, 0.1f), GateFrame);
            K.Box("Stile_L", leaf, new Vector3(-width * 0.5f + 0.05f, 0.04f, 0f), new Vector3(0.1f, height - 0.04f, 0.1f), GateFrame);
            K.Box("Stile_R", leaf, new Vector3(width * 0.5f - 0.05f, 0.04f, 0f), new Vector3(0.1f, height - 0.04f, 0.1f), GateFrame);

            int bars = Mathf.Max(2, Mathf.RoundToInt(width / 0.42f));
            for (int i = 0; i < bars; i++)
            {
                float x = -width * 0.5f + (i + 0.5f) * width / bars;
                K.Box($"Bar_{i:00}", leaf, new Vector3(x, 0.92f, 0f), new Vector3(0.05f, height - 0.97f, 0.05f), GateFrame);
            }
        }

        /// <summary>Knocked down: both leaves flat on the ground, one bent up at the end, and loose bars.</summary>
        static void BuildBrokenGate(Transform p, float width, float leafWidth)
        {
            GameObject a = K.Box("Fallen_L", p, new Vector3(-width * 0.25f, 0.02f, -0.9f), new Vector3(leafWidth, 0.1f, 1.7f), GateSheet, yaw: 8f);
            a.transform.localRotation *= Quaternion.Euler(4f, 0f, 0f);

            GameObject b = K.Box("Fallen_R", p, new Vector3(width * 0.28f, 0.02f, -1.1f), new Vector3(leafWidth * 0.9f, 0.1f, 1.7f), GateFrame, yaw: -12f);
            b.transform.localRotation *= Quaternion.Euler(0f, 0f, 10f);

            for (int i = 0; i < 3; i++)
                K.Log($"Bar_{i}", p, new Vector3(-width * 0.2f + i * width * 0.18f, 0.06f, -0.3f - i * 0.25f), 0.05f, 1.4f, GateFrame, yaw: 30f + i * 50f);
        }

        /// <summary>Unlit, so the gate lamps read at night however dark the scene is. The colour is set per lamp at runtime.</summary>
        static Material LampMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(LampMaterialPath);
            if (material != null) return material;

            ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(LampMaterialPath).Replace('\\', '/'));

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;

            material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Hazard);
            material.color = Hazard;

            AssetDatabase.CreateAsset(material, LampMaterialPath);
            return material;
        }

        // --- Inside --------------------------------------------------------------------------------

        static void AddZone(Transform root, string name, Rect area, GameObject tiedTo, GameLifetimeScope scope)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(area.center.x, 0f, area.center.y);

            go.AddComponent<CompoundZone>().Configure(scope, area, tiedTo);
        }

        // --- Plumbing ------------------------------------------------------------------------------

        /// <summary>The root, emptied: everything a previous run made is thrown away.</summary>
        static Transform FreshRoot(Scene scene)
        {
            foreach (GameObject existing in scene.GetRootGameObjects())
            {
                if (existing.name != RootName) continue;

                Undo.DestroyObjectImmediate(existing);
                break;
            }

            var go = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, RootName);
            return go.transform;
        }

        static Transform FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            }

            return null;
        }
    }
}
