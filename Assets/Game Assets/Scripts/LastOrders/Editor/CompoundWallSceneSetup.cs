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
    /// rather than a whole side. Pieces tied to an expansion follow it: the stretch across the
    /// yard's opening, and its gate, go when the yard is claimed; the yard's own new wall comes
    /// up as it is built.
    ///
    /// A gap between two pieces on the same line is a way in, and gets a gate: the south gate
    /// on the road, and the small one out to the yard.
    ///
    /// Safe to re-run: everything it made is thrown away and laid again from the boundary as it
    /// stands, so run it again after 'Set Up Visitor System' or the expansion setup has moved
    /// the boundary.
    /// </summary>
    public static class CompoundWallSceneSetup
    {
        const string RootName = "CompoundWalls";
        const string BoundaryName = "OuterBoundries";

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

            public float Length => To - From;

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

            foreach (ExpansionArea area in Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (area.gameObject.scene != scene) continue;
                CollectExpansion(area, compound, runs, yardZones);
            }

            List<(WallRun left, WallRun right)> gaps = FindGaps(runs);

            // --- Build -------------------------------------------------------------------------------

            K.Reset();

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
        /// An expansion's share of the wall: the pieces across its opening, which go when it is
        /// claimed, and the new wall its build raises. Its ground counts as inside once claimed.
        /// </summary>
        static void CollectExpansion(ExpansionArea area, Rect compound, List<WallRun> runs,
                                     List<(Rect, GameObject, string)> zones)
        {
            var so = new SerializedObject(area);

            // Across the opening: the wall pieces among what is shown until the land is claimed.
            SerializedProperty locked = so.FindProperty("lockedVisuals");
            for (int i = 0; locked != null && i < locked.arraySize; i++)
            {
                var piece = locked.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (piece == null || !piece.name.Contains("Wall")) continue;

                if (!TryRun(piece.transform, piece, out WallRun run)) continue;

                run.Interior = new Vector3(compound.center.x, 0f, compound.center.y);
                run.Label = SideOf(run, compound) + " wall";
                runs.Add(run);
            }

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

                run.Label = "Yard wall";
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

        /// <summary>Two pieces on one line with a gap between them a gate fits in.</summary>
        static List<(WallRun, WallRun)> FindGaps(List<WallRun> runs)
        {
            var gaps = new List<(WallRun, WallRun)>();
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

            return gaps;
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

        static void BuildSection(Transform root, string name, WallRun run, Vector3 centre, float length, bool first,
                                 bool last, int seed, GameLifetimeScope scope, WallMaterialDefinition material,
                                 PooledEffect dust)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, YawAlong(run), 0f));

            // Which local face looks out, so the patching goes on the side a zombie sees.
            Vector3 outward = centre - run.Interior;
            float outside = Vector3.Dot(go.transform.rotation * Vector3.back, outward) > 0f ? -1f : 1f;

            var rng = new System.Random(seed * 7919 + 17);

            Transform visual = K.Group("Visual", go.transform);

            Transform intact = K.Group("Intact", visual);
            BuildIntact(intact, length, first, last, outside, seed);

            Transform damaged = K.Group("Damaged", visual);
            BuildDamaged(damaged, length, first, last, outside, rng);
            damaged.gameObject.SetActive(false);

            Transform rubble = K.Group("Rubble", visual);
            BuildRubble(rubble, length, first, last, rng);
            rubble.gameObject.SetActive(false);

            Transform bar = K.Group("HealthBarAnchor", go.transform);
            bar.localPosition = new Vector3(0f, 0.3f, 0f);

            var wall = go.AddComponent<CompoundWall>();
            wall.ConfigureStructure(scope, run.Label, length, Thickness, run.TiedTo, visual.gameObject, bar, dust);
            wall.ConfigureWall(material, intact.gameObject, damaged.gameObject, rubble.gameObject);

            // As it will look when the game starts: an expansion's piece is only there once it is.
            if (run.TiedTo != null && !run.TiedTo.activeInHierarchy) visual.gameObject.SetActive(false);
        }

        static void BuildIntact(Transform p, float length, bool first, bool last, float outside, int seed)
        {
            float panelHeight = Height - FootingHeight - CapHeight;

            K.Box("Footing", p, Vector3.zero, new Vector3(length, FootingHeight, Thickness + 0.14f), Footing);
            K.Box("Panel", p, new Vector3(0f, FootingHeight, 0f), new Vector3(length, panelHeight, Thickness), Panel);
            K.Box("Cap", p, new Vector3(0f, Height - CapHeight, 0f), new Vector3(length + 0.02f, CapHeight, Thickness + 0.1f), Cap);

            // One course line, so the panel reads as blocks at the camera's distance.
            K.Cube("Course", p, new Vector3(0f, 1.1f, 0f), new Vector3(length - 0.04f, 0.05f, Thickness + 0.03f), Band);

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

            GameObject tiedTo = left.TiedTo != null ? left.TiedTo : right.TiedTo;
            bool main = tiedTo == null;

            string label = main ? SideOf(left, compound) + " gate" : "Yard gate";

            var go = new GameObject(label.Replace(' ', '_'));
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));

            Transform visual = K.Group("Visual", go.transform);

            Material lampMaterial = LampMaterial();
            var lamps = new List<Renderer>();

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

            Transform standing = K.Group("Standing", visual);
            K.Box("Track", standing, Vector3.zero, new Vector3(width + 0.4f, 0.03f, 0.18f), K.MetalDark);

            float leafWidth = width * 0.5f + 0.05f;
            float leafHeight = Height - 0.12f;

            Transform leftLeaf = K.Group("Leaf_L", standing);
            leftLeaf.localPosition = new Vector3(-width * 0.25f, 0f, 0f);
            BuildLeaf(leftLeaf, leafWidth, leafHeight);

            Transform rightLeaf = K.Group("Leaf_R", standing);
            rightLeaf.localPosition = new Vector3(width * 0.25f, 0f, 0f);
            BuildLeaf(rightLeaf, leafWidth, leafHeight);

            Transform broken = K.Group("Broken", visual);
            BuildBrokenGate(broken, width, leafWidth);
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
