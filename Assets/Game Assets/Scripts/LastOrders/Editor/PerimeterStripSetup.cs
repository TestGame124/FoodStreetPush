using System.Collections.Generic;
using System.Text;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Defense;
using LastOrders.Effects;
using LastOrders.Expansion;
using LastOrders.Night;
using LastOrders.Perimeter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using B = LastOrders.EditorTools.BuildingKit;
using K = LastOrders.EditorTools.PrimitiveKit;
using L = LastOrders.EditorTools.PerimeterStripLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the safe zone's outer defenses into the open scene: the sites on the walls where any
    /// defense can be mounted - a bastion and a corn shooter on those that start with one - the paved
    /// strip outside all four walls with its build slots, the barricades, spike
    /// plates and wire it starts with, and the pads out in the defense ring that take a gun on the
    /// ground.
    ///
    /// Everything is laid from <see cref="PerimeterStripLayout"/>, with the gates read from the
    /// compound wall so the strip opens in front of each one. Plot obstacles sitting on the strip
    /// are moved back onto their land, and the enemy spawner is told to spread each group along the
    /// side it comes from, so the whole ring is in play rather than four points on it.
    ///
    /// Safe to re-run. The strip's ground and slots are rebuilt from the layout every time (a slot
    /// keeps its id, so what was bought for it is kept), and so are the wall sites and their
    /// bastions; whatever was bought for a site is kept the same way.
    /// </summary>
    public static class PerimeterStripSetup
    {
        const string RootName = "PerimeterStrip";
        const string GroundFolder = GroundDefenseModels.Folder + "/Ground";

        /// <summary>Metres either side of a lane's point the spawner spreads a group, when it has no spread of its own.</summary>
        const float LaneSpread = 9f;

        [MenuItem("Last Orders/Defense/Set Up Perimeter Strip & Wall Sites (Current Scene)", priority = 47)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would run the defenses.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                $"- Marks out {L.Mounts.Length} sites on the safe zone's walls where any defense can be mounted on a bastion\n" +
                "- Lays the paved strip outside all four walls, with its build slots\n" +
                $"- Builds {L.Starters.Length} barricades, spike plates and wire coils in it to start\n" +
                $"- Lays {L.RingPads.Length} empty pads out in the defense ring, each taking any gun a wall site does\n" +
                "- Moves plot obstacles that sit on the strip back onto their land\n" +
                "- Spreads each enemy group along the side it comes from\n\n" +
                "In front of every gate the strip is left open; on the road side the drive crosses it to the main gate.",
                "Set up", "Cancel");

            if (!proceed) return;

            Debug.Log(Apply(scope, save: true));
        }

        /// <summary>The setup against the open scene's scope, with no dialogs - for automation.</summary>
        public static string ApplyToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null ? Apply(scope, save) : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;
            K.Reset();

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance").objectReferenceValue as GameBalanceConfig;
            WorldUiPrefabGenerator.Ensure(balance);

            DefenseDefinition[] wallOptions = WallOptions();

            GroundDefenseDefinition[] kinds = DefenseContentGenerator.GenerateGroundDefenses();
            if (GroundDefenseModels.LoadMarker() == null || System.Array.Exists(kinds, k => k.prefab == null)) GroundDefenseModels.Apply(kinds);
            Mesh marker = GroundDefenseModels.LoadMarker();
            PooledEffect dust = EffectPrefabGenerator.EnsureBuildDust();

            Transform walls = FindRoot(scene, "CompoundWalls");
            if (walls == null) return "[Last Orders] No CompoundWalls in the scene; run 'Set Up Walls & Gates' first. Nothing was changed.";

            var report = new StringBuilder("[Last Orders] Perimeter strip and wall sites set up.\n");

            Transform root = FindOrCreateRoot(scene, RootName);
            Transform ground = Group(root, "Ground");
            Transform slotRoot = Group(root, "Slots");

            var wanted = new HashSet<string>();
            int built = 0;

            foreach (L.Side side in L.Strips)
            {
                List<Vector2> gates = Gates(walls, side);
                List<L.Slot> slots = L.Slots(side, gates);

                Mesh mesh = PerimeterStripModel.Build(side, gates, slots).Save($"{GroundFolder}/Strip_{side.Name}.asset", $"Strip_{side.Name}");
                Transform strip = Group(ground, $"Strip_{side.Name}");
                B.Show(strip.gameObject, mesh, new[] { B.PaintedMaterial() });
                strip.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                var starting = new Dictionary<string, GroundDefenseDefinition>();
                foreach (L.Starter starter in L.Starters)
                {
                    if (starter.Side.Key != side.Key) continue;
                    L.Slot nearest = Nearest(slots, starter.Near);
                    starting[nearest.Id] = kinds[(int)starter.Kind];
                }

                foreach (L.Slot slot in slots)
                {
                    starting.TryGetValue(slot.Id, out GroundDefenseDefinition first);
                    PlaceSlot(scope, slotRoot, slot, kinds, first, marker, dust);
                    wanted.Add(slot.Id);
                    if (first != null) built++;
                }

                report.Append($"  {side.Name}: {slots.Count} slots, {gates.Count} gate(s) left open, {mesh.vertexCount} vertices of paving\n");
            }

            int removed = RemoveStale(slotRoot, wanted);
            report.Append($"  {wanted.Count} slots in all, {built} built from the start{(removed > 0 ? $", {removed} old slot(s) removed" : "")}\n");

            report.Append(RemoveCropCannons(scene));
            report.Append(PlaceWallSites(scope, root, walls, wallOptions, dust));
            report.Append(PlaceRingPads(scope, root, wallOptions));
            report.Append(ClearStripOfDebris(scene));
            report.Append($"  {SpreadLanes()}\n");

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return report.ToString();
        }

        // --- The gates ------------------------------------------------------------------------------------

        /// <summary>The gates in the wall along <paramref name="side"/>, as spans along it, in order.</summary>
        static List<Vector2> Gates(Transform walls, L.Side side)
        {
            var spans = new List<Vector2>();

            foreach (CompoundGate gate in walls.GetComponentsInChildren<CompoundGate>(true))
            {
                Vector3 at = gate.transform.position;
                if (Mathf.Abs(side.OutOf(at)) > 0.3f) continue;

                Vector3 runs = gate.transform.right;
                if (Mathf.Abs(Vector3.Dot(runs, side.Along)) < 0.9f) continue;

                float middle = side.AlongOf(at), half = gate.Length * 0.5f;
                if (middle + half < side.From || middle - half > side.To) continue;

                spans.Add(new Vector2(middle - half, middle + half));
            }

            spans.Sort((a, b) => a.x.CompareTo(b.x));
            return spans;
        }

        static L.Slot Nearest(List<L.Slot> slots, float along)
        {
            L.Slot best = slots[0];
            foreach (L.Slot slot in slots)
                if (Mathf.Abs(slot.Along - along) < Mathf.Abs(best.Along - along)) best = slot;

            return best;
        }

        // --- Slots ---------------------------------------------------------------------------------------

        static void PlaceSlot(GameLifetimeScope scope, Transform root, L.Slot slot, GroundDefenseDefinition[] kinds,
                              GroundDefenseDefinition first, Mesh marker, PooledEffect dust)
        {
            StripSlot existing = null;
            foreach (StripSlot s in root.GetComponentsInChildren<StripSlot>(true))
                if (s.SlotId == slot.Id) existing = s;

            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject($"Slot_{slot.Id}");
                go.transform.SetParent(root, false);
                Undo.RegisterCreatedObjectUndo(go, "Place strip slot");
            }

            go.transform.SetPositionAndRotation(slot.Position, Quaternion.Euler(0f, slot.Side.Yaw, 0f));

            var tap = go.GetComponent<BoxCollider>();
            if (tap == null) tap = go.AddComponent<BoxCollider>();

            var view = go.GetComponent<StripSlot>();
            if (view == null) view = go.AddComponent<StripSlot>();

            // The pegs an empty slot is marked out with.
            Transform pegs = go.transform.Find("Marker");
            if (pegs == null)
            {
                pegs = new GameObject("Marker").transform;
                pegs.SetParent(go.transform, false);
            }

            pegs.localPosition = Vector3.zero;
            pegs.localRotation = Quaternion.identity;
            B.Show(pegs.gameObject, marker, new[] { B.PaintedMaterial() });
            pegs.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            pegs.gameObject.SetActive(first == null);

            // What it starts with, standing in the scene so the level shows it before it runs.
            Transform model = go.transform.Find("Model");
            bool keep = model != null && first != null && PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject) == first.prefab;
            if (model != null && !keep)
            {
                Object.DestroyImmediate(model.gameObject);
                model = null;
            }

            if (model == null && first != null && first.prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(first.prefab, go.transform);
                instance.name = "Model";
                model = instance.transform;
            }

            if (model != null)
            {
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
            }

            view.Configure(scope, slot.Id, kinds, first, pegs.gameObject, tap, new Vector2(L.SlotLength, L.SlotDepth), dust);
            view.ConfigureModel(model != null ? model.gameObject : null, model != null ? first : null);

            tap.size = new Vector3(L.SlotLength, 0.4f, L.SlotDepth);
            tap.center = new Vector3(0f, 0.2f, 0f);
            EditorUtility.SetDirty(view);
        }

        /// <summary>Slots from an older layout that this one no longer has.</summary>
        static int RemoveStale(Transform root, HashSet<string> wanted)
        {
            var stale = new List<GameObject>();
            foreach (StripSlot slot in root.GetComponentsInChildren<StripSlot>(true))
                if (!wanted.Contains(slot.SlotId)) stale.Add(slot.gameObject);

            foreach (GameObject go in stale) Undo.DestroyObjectImmediate(go);
            return stale.Count;
        }

        // --- The wall sites --------------------------------------------------------------------------------

        const string SiteFolder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/WallSite";

        /// <summary>A little in from the bastion's middle, clear of its parapet: where the defense stands.</summary>
        const float MountInset = 0.12f;

        /// <summary>
        /// What a wall site offers, each with the prefab placed for it: the corn shooter first - the
        /// wall's own gun - then the scrap turret and the Toastapult. The guard tower is not offered:
        /// it is a tower of its own, raised on its own legs.
        /// </summary>
        static DefenseDefinition[] WallOptions()
        {
            DefenseDefinition corn = DefenseContentGenerator.GenerateCornShooter();
            GameObject cornPrefab = CornShooterModel.Ensure(corn, DefenseContentGenerator.LoadCornShooterMotion());

            DefenseDefinition turret = DefenseContentGenerator.GenerateScrapTurret();
            GameObject turretPrefab = ScrapTurretModel.Ensure(turret, DefenseContentGenerator.LoadScrapTurretMotion());

            DefenseDefinition toaster = DefenseContentGenerator.GenerateToastapult();
            GameObject toasterPrefab = ToastapultModel.Ensure(toaster, DefenseContentGenerator.LoadToastapultMotion());

            SetPrefab(corn, cornPrefab);
            SetPrefab(turret, turretPrefab);
            SetPrefab(toaster, toasterPrefab);
            return new[] { corn, turret, toaster };
        }

        static void SetPrefab(DefenseDefinition definition, GameObject prefab)
        {
            if (definition.prefab == prefab) return;

            definition.prefab = prefab;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        static string PlaceWallSites(GameLifetimeScope scope, Transform root, Transform walls, DefenseDefinition[] options, PooledEffect dust)
        {
            Transform sites = Group(root, "WallSites");
            var report = new StringBuilder();
            DefenseDefinition corn = options[0];
            int built = 0;

            var markers = new Dictionary<char, Mesh>();
            for (int i = 0; i < L.Mounts.Length; i++)
            {
                L.WallMount mount = L.Mounts[i];
                L.Bastion shape = mount.Bastion;
                float yaw = mount.Side.Yaw;

                Transform site = Group(sites, $"Site_{mount.Id}");
                site.SetPositionAndRotation(mount.Position, Quaternion.Euler(0f, yaw, 0f));

                // The thickened wall, rebuilt every run.
                Vector3 middle = site.TransformPoint(new Vector3(0f, 0f, shape.Middle));
                CompoundWall bastion = CompoundWallSceneSetup.BuildBastion(site, "Bastion", middle, yaw, shape.Length, shape.Thickness,
                                                                           L.BastionHealth, scope, dust, i + 1);

                Transform top = Group(site, "Mount");
                top.localPosition = new Vector3(0f, CompoundWallSceneSetup.BastionHeight, shape.Middle - MountInset);
                top.localRotation = Quaternion.identity;

                // Pegs and twine where the bastion will go, while nothing is built.
                char key = mount.Side.HasStrip ? 's' : 'r';
                if (!markers.TryGetValue(key, out Mesh markerMesh))
                {
                    string name = mount.Side.HasStrip ? "WallSite_Marker_Strip" : "WallSite_Marker_Road";
                    markerMesh = CompoundWallSceneSetup.BastionMarker(shape.Length, shape.Inner, shape.Outer).Save($"{SiteFolder}/{name}.asset", name);
                    markers[key] = markerMesh;
                }

                Transform marker = Group(site, "Marker");
                marker.localPosition = Vector3.zero;
                marker.localRotation = Quaternion.identity;
                B.Show(marker.gameObject, markerMesh, new[] { B.PaintedMaterial() });
                marker.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                var tap = site.GetComponent<BoxCollider>();
                if (tap == null) tap = site.gameObject.AddComponent<BoxCollider>();
                tap.center = new Vector3(0f, 1.25f, shape.Middle);
                tap.size = new Vector3(shape.Length, 2.5f, shape.Thickness);

                var view = site.GetComponent<WallSite>();
                if (view == null) view = site.gameObject.AddComponent<WallSite>();

                DefenseDefinition first = mount.StartsBuilt ? corn : null;
                view.Configure(scope, mount.Id, options, first, bastion.gameObject, bastion, top, marker.gameObject, tap,
                               new Vector3(0f, 3.1f, 0f));
                view.ConfigurePost(StandingPost(scope, top, mount.Id, first), first);
                EditorUtility.SetDirty(view);

                bastion.gameObject.SetActive(first != null);
                marker.gameObject.SetActive(first == null);
                if (first != null) built++;

                if (!OnAWall(walls, mount.Position))
                    report.Append($"  WARNING: wall site {mount.Id} at {mount.Position} is not on a wall section.\n");
            }

            report.Insert(0, $"  Wall sites: {L.Mounts.Length}, {built} with a bastion and a {corn.displayName} from the start, " +
                             $"{L.Mounts.Length - built} empty - each takes any of: {string.Join(", ", System.Array.ConvertAll(options, o => o.displayName))}.\n");
            return report.ToString();
        }

        /// <summary>
        /// The defense a site starts with, standing on its mount in the scene so the level shows it before
        /// it runs - wired as built under the site's id. Anything else on the mount is cleared away.
        /// </summary>
        static DefensePost StandingPost(GameLifetimeScope scope, Transform top, string id, DefenseDefinition first)
        {
            DefensePost keep = null;
            var doomed = new List<GameObject>();

            foreach (Transform child in top)
            {
                bool same = first != null && keep == null && PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == first.prefab;
                if (same) keep = child.GetComponent<DefensePost>();
                else doomed.Add(child.gameObject);
            }

            foreach (GameObject go in doomed) Object.DestroyImmediate(go);
            if (first == null || first.prefab == null) return null;

            if (keep == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(first.prefab, top);
                instance.name = $"{first.displayName}_{id}";
                keep = instance.GetComponent<DefensePost>();
            }

            keep.transform.localPosition = Vector3.zero;
            keep.transform.localRotation = Quaternion.identity;

            var so = new SerializedObject(keep);
            so.FindProperty("scope").objectReferenceValue = scope;
            so.FindProperty("definition").objectReferenceValue = first;
            so.FindProperty("postId").stringValue = id;
            so.FindProperty("startsBuilt").boolValue = true;
            so.ApplyModifiedProperties();

            ShowInEditor(so.FindProperty("builtVisuals").objectReferenceValue as GameObject, true);
            ShowInEditor(so.FindProperty("siteVisuals").objectReferenceValue as GameObject, false);
            return keep;
        }

        // --- The ring pads ---------------------------------------------------------------------------------

        const string PadFolder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/RingPad";

        static readonly Color PadConcrete = B.Hex("94928C");
        static readonly Color PadEdge = B.Hex("85837D");
        static readonly Color PadGravel = B.Hex("857D72");
        static readonly Color PadBolt = B.Hex("8D9398");
        static readonly Color PegTip = K.Hex("A88B45");

        /// <summary>
        /// The pads out in the defense ring: a low concrete slab on a bed of gravel, pegged out with
        /// twine while it is empty, that takes any of the guns a wall site does - a wall site with no
        /// bastion, standing on the ground. They start empty; what was bought for one is kept under
        /// its id.
        /// </summary>
        static string PlaceRingPads(GameLifetimeScope scope, Transform root, DefenseDefinition[] options)
        {
            Transform pads = Group(root, "RingPads");

            Mesh slab = RingPadSlab().Save($"{PadFolder}/RingPad_Slab.asset", "RingPad_Slab");
            Mesh pegs = RingPadMarker().Save($"{PadFolder}/RingPad_Marker.asset", "RingPad_Marker");

            var wanted = new HashSet<string>();
            foreach (L.RingPad pad in L.RingPads)
            {
                Transform site = Group(pads, $"Pad_{pad.Id}");
                site.SetPositionAndRotation(pad.Position, Quaternion.Euler(0f, pad.Side.Yaw, 0f));
                wanted.Add(site.name);

                Transform ground = Group(site, "Slab");
                ground.localPosition = Vector3.zero;
                ground.localRotation = Quaternion.identity;
                B.Show(ground.gameObject, slab, new[] { B.PaintedMaterial() });
                ground.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                Transform marker = Group(site, "Marker");
                marker.localPosition = Vector3.zero;
                marker.localRotation = Quaternion.identity;
                B.Show(marker.gameObject, pegs, new[] { B.PaintedMaterial() });
                marker.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                marker.gameObject.SetActive(true);

                Transform top = Group(site, "Mount");
                top.localPosition = new Vector3(0f, L.RingPadTop, 0f);
                top.localRotation = Quaternion.identity;

                var tap = site.GetComponent<BoxCollider>();
                if (tap == null) tap = site.gameObject.AddComponent<BoxCollider>();
                tap.center = new Vector3(0f, 0.6f, 0f);
                tap.size = new Vector3(L.RingPadSize, 1.2f, L.RingPadSize);

                var view = site.GetComponent<WallSite>();
                if (view == null) view = site.gameObject.AddComponent<WallSite>();

                view.Configure(scope, pad.Id, options, null, null, null, top, marker.gameObject, tap, new Vector3(0f, 2.2f, 0f));
                view.ConfigurePost(StandingPost(scope, top, pad.Id, null), null);
                EditorUtility.SetDirty(view);
            }

            // Pads from an older layout that this one no longer has.
            var stale = new List<GameObject>();
            foreach (Transform child in pads)
                if (!wanted.Contains(child.name)) stale.Add(child.gameObject);
            foreach (GameObject go in stale) Undo.DestroyObjectImmediate(go);

            return $"  Ring pads: {L.RingPads.Length}, {L.RingPadOut:0.#} m out from the walls, empty - each takes any of: " +
                   $"{string.Join(", ", System.Array.ConvertAll(options, o => o.displayName))}" +
                   $"{(stale.Count > 0 ? $"; {stale.Count} old pad(s) removed" : "")}.\n";
        }

        /// <summary>A pad's ground, in its own frame (+Z out from the wall): a bevelled slab on a gravel bed, anchor bolts at its corners.</summary>
        static B.Model RingPadSlab()
        {
            var m = new B.Model { Shade = (p, n) => n.y > 0.7f ? 1f : 0.86f };
            float size = L.RingPadSize, top = L.RingPadTop;

            m.Box(PadGravel, new Vector3(0f, 0.01f, 0f), new Vector3(size + 0.5f, 0.02f, size + 0.5f), 0.01f);
            m.Box(PadConcrete, new Vector3(0f, top * 0.5f, 0f), new Vector3(size, top, size), 0.025f);

            // A darker kerb of poured edge round the top, as the strip's patches have.
            float h = size * 0.5f - 0.07f;
            m.Box(PadEdge, new Vector3(0f, top + 0.004f, h), new Vector3(size - 0.06f, 0.008f, 0.1f), 0.003f);
            m.Box(PadEdge, new Vector3(0f, top + 0.004f, -h), new Vector3(size - 0.06f, 0.008f, 0.1f), 0.003f);
            m.Box(PadEdge, new Vector3(h, top + 0.004f, 0f), new Vector3(0.1f, 0.008f, size - 0.26f), 0.003f);
            m.Box(PadEdge, new Vector3(-h, top + 0.004f, 0f), new Vector3(0.1f, 0.008f, size - 0.26f), 0.003f);

            float b = size * 0.5f - 0.3f;
            foreach (float x in new[] { -b, b })
                foreach (float z in new[] { -b, b })
                    m.Tube(PadBolt, new Vector3(x, top, z), new Vector3(x, top + 0.02f, z), 0.025f, 6);

            return m;
        }

        /// <summary>An empty pad, pegged out: a peg at each corner just off the slab, twine all round.</summary>
        static B.Model RingPadMarker()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(p.y / 0.4f)) };
            float r = L.RingPadSize * 0.5f + 0.12f;

            var corners = new[] { new Vector3(-r, 0f, -r), new Vector3(r, 0f, -r), new Vector3(r, 0f, r), new Vector3(-r, 0f, r) };
            var knots = new Vector3[4];

            for (int i = 0; i < 4; i++)
            {
                Quaternion lean = Quaternion.Euler(4f * B.Jitter(i + 11), 0f, 4f * B.Jitter(i + 12));
                m.Box(B.WalnutDark, corners[i] + lean * (Vector3.up * 0.3f), new Vector3(0.05f, 0.6f, 0.05f), 0.012f, lean);
                m.Box(PegTip, corners[i] + lean * (Vector3.up * 0.57f), new Vector3(0.06f, 0.07f, 0.06f), 0.012f, lean);
                knots[i] = corners[i] + lean * (Vector3.up * 0.45f);
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3 a = knots[i], b = knots[(i + 1) % 4];
                Vector3 sag = (a + b) * 0.5f + Vector3.down * 0.06f;
                m.Tube(B.Twine, a, sag, 0.008f, 4);
                m.Tube(B.Twine, sag, b, 0.008f, 4);
            }

            return m;
        }

        /// <summary>The Crop Cannons this setup used to mount, which the corn shooters replace.</summary>
        static string RemoveCropCannons(Scene scene)
        {
            var doomed = new List<GameObject>();
            foreach (GameObject top in scene.GetRootGameObjects())
                foreach (DefensePost post in top.GetComponentsInChildren<DefensePost>(true))
                    if (post.name.StartsWith("CropCannon_")) doomed.Add(post.gameObject);

            foreach (GameObject go in doomed) Undo.DestroyObjectImmediate(go);
            return doomed.Count > 0 ? $"  Removed {doomed.Count} Crop Cannon(s).\n" : string.Empty;
        }

        /// <summary>True when a standing wall section (not a gate) runs under <paramref name="at"/>.</summary>
        static bool OnAWall(Transform walls, Vector3 at)
        {
            foreach (CompoundWall wall in walls.GetComponentsInChildren<CompoundWall>(true))
            {
                if (!wall.gameObject.activeInHierarchy) continue;
                if ((wall.ClosestPoint(at) - new Vector3(at.x, wall.transform.position.y, at.z)).sqrMagnitude < 0.09f) return true;
            }

            return false;
        }

        // --- Plot obstacles -----------------------------------------------------------------------------

        /// <summary>
        /// Moves any plot obstacle that reaches onto the strip straight back out, away from the wall,
        /// until it clears the strip's outer edge. It stays on its own land and keeps its id, so
        /// clearing it is the same job it was.
        /// </summary>
        static string ClearStripOfDebris(Scene scene)
        {
            int moved = 0;
            var names = new List<string>();

            foreach (GameObject top in scene.GetRootGameObjects())
            {
                foreach (Debris debris in top.GetComponentsInChildren<Debris>(true))
                {
                    if (!Bounds(debris.gameObject, out Bounds b)) continue;

                    foreach (L.Side side in L.Strips)
                    {
                        float push = Push(side, b);
                        if (push <= 0f) continue;

                        Undo.RecordObject(debris.transform, "Clear the strip");
                        debris.transform.position += side.Out * push;
                        moved++;
                        names.Add($"{debris.name} {push:0.0} m");
                        Bounds(debris.gameObject, out b);
                    }
                }
            }

            return moved == 0
                ? "  No plot obstacles were on the strip.\n"
                : $"  Moved {moved} plot obstacle(s) off the strip: {string.Join(", ", names)}\n";
        }

        /// <summary>Metres <paramref name="b"/> has to move out from <paramref name="side"/>'s wall to clear its strip. Zero when it already does.</summary>
        static float Push(L.Side side, Bounds b)
        {
            // The box's extent along the wall and out from it.
            float a0 = float.MaxValue, a1 = float.MinValue, o0 = float.MaxValue, o1 = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, 0f, (i & 2) == 0 ? b.min.z : b.max.z);
                float a = side.AlongOf(corner), o = side.OutOf(corner);
                a0 = Mathf.Min(a0, a);
                a1 = Mathf.Max(a1, a);
                o0 = Mathf.Min(o0, o);
                o1 = Mathf.Max(o1, o);
            }

            if (a1 < side.From || a0 > side.To) return 0f;

            // Only what lies beyond the wall: anything inside is the compound's own.
            if (o1 <= L.WallHalf) return 0f;

            // A hair of slack, so one moved out on an earlier run is not moved again by rounding.
            float edge = L.Depth + L.DebrisClear;
            return o0 < edge - 0.02f ? edge - o0 : 0f;
        }

        static bool Bounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (any) bounds.Encapsulate(r.bounds);
                else bounds = r.bounds;
                any = true;
            }

            return any;
        }

        // --- The lanes ------------------------------------------------------------------------------------

        /// <summary>Gives the spawner a spread along each side, unless it already has one of its own.</summary>
        static string SpreadLanes()
        {
            EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner == null) return "No EnemySpawner in the scene; lanes left alone.";

            var so = new SerializedObject(spawner);
            SerializedProperty spread = so.FindProperty("laneSpread");
            if (spread.floatValue > 0f) return $"Enemy groups already spread {spread.floatValue:0.#} m either side of their lane; left as it is.";

            spread.floatValue = LaneSpread;
            so.ApplyModifiedProperties();
            return $"Enemy groups now spread {LaneSpread:0.#} m either side of their lane, so they reach the walls along their whole length.";
        }

        // --- Scene plumbing --------------------------------------------------------------------------------

        static void ShowInEditor(GameObject part, bool active)
        {
            if (part == null || part.activeSelf == active) return;

            Undo.RecordObject(part, "Place wall gun");
            part.SetActive(active);
        }

        static Transform Group(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;

            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(t.gameObject, name);
            return t;
        }

        static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject existing in scene.GetRootGameObjects())
                if (existing.name == name) return existing.transform;

            return null;
        }

        static Transform FindOrCreateRoot(Scene scene, string name)
        {
            Transform found = FindRoot(scene, name);
            if (found != null) return found;

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go.transform;
        }
    }
}
