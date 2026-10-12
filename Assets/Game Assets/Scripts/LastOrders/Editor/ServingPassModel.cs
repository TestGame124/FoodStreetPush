using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LastOrders.Kitchen;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using G = LastOrders.EditorTools.GriddleStationModel;
using K = LastOrders.EditorTools.SoupKitchenModel;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The serving pass (pfServingPass): a length of counter between the diner's kitchen and its dining
    /// room, where finished dishes are set out for the waiters. One module, reused - the diner gets two
    /// with an opening between them - in the smooth low-poly style and the diner's own paint: a cream
    /// body framed in the walls' brown, a red pinstripe under a maple top, and a steel hot shelf along
    /// the top for the plates. A brass bell at one end, a ticket spike at the other; nothing else, so
    /// it stays low and light between the stations and the tables.
    ///
    /// The whole counter is one mesh on the buildings' material, so both modules and the walls draw in
    /// one batch. The dishes that stand on it (<see cref="PassDishSet"/>) are the recipe icons' plated
    /// dishes, baked to one vertex-painted mesh each on the same material; dishes with no model come
    /// out under a cloche.
    ///
    /// Also makes the waiters' animator: the workers' breathing idle and walk, and the staff's
    /// arms-forward carry on the arms only, weighted up by <see cref="Restaurant.Waiter"/> while it
    /// holds a plate.
    ///
    /// The prefab's origin is the middle of the counter's footprint on the ground; its front faces -Z,
    /// into the dining room. Rebuilt in place, so instances in the level keep their links.
    /// </summary>
    public static class ServingPassModel
    {
        internal const string PrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/ServingPass/pfServingPass.prefab";
        internal const string Folder = "Assets/Game Assets/Content/LastOrders/Kitchen/" + EnvKit.Style + "/ServingPass";
        internal const string DishSetPath = Folder + "/PassDishes.asset";
        internal const string WaiterControllerPath = "Assets/Game Assets/Content/LastOrders/Characters/WaiterAnimator.controller";

        const string WorkerControllerPath = "Assets/Game Assets/Content/LastOrders/Characters/WorkerAnimator.controller";
        const string StaffControllerPath = "Assets/Game Assets/Prefabs/Characters/StaffAnimator.controller";

        // --- Measurements (metres; front toward -Z; origin the middle of the footprint on the ground) ---

        /// <summary>The diner floor's top, which the counter stands on.</summary>
        const float FloorTop = 0.035f;

        internal const float TopHalf = 1.5f, TopFront = -0.31f, TopBack = 0.3f;
        const float TopBottom = 0.84f, TopTop = 0.9f;

        const float BodyHalf = 1.46f, BodyFront = -0.27f, BodyBack = 0.27f;
        const float KickHeight = 0.1f, KickInset = 0.035f;

        const float ShelfHalf = 1.28f, ShelfFront = -0.2f, ShelfBack = 0.17f, ShelfThick = 0.007f;

        /// <summary>The panels' frame on the front: stiles at these x, rails at these heights.</summary>
        static readonly float[] StileX = { -1.425f, -0.49f, 0.49f, 1.425f };

        const float StileWidth = 0.05f, FrameProud = 0.016f, RailLow = 0.135f, RailHigh = 0.785f, RailHeight = 0.04f;

        /// <summary>Where plates stand along the shelf, and where a waiter stands to take one.</summary>
        static readonly float[] SlotX = { -1.0f, -0.34f, 0.34f, 1.0f };

        const float SlotZ = -0.015f;
        static readonly float[] StandX = { -0.72f, 0.72f };
        const float StandZ = -0.78f;

        /// <summary>The widest a dish is across, plate and all.</summary>
        const float DishSpan = 0.3f;

        // --- The palette: the diner shell's own ---------------------------------------------------------

        static readonly Color Cream = B.Hex("BFA68D");
        static readonly Color CreamCap = B.Hex("C7AF95");
        static readonly Color Skirting = B.Hex("74503C");
        static readonly Color DoorRed = B.Hex("7A443B");
        static readonly Color Brass = B.Hex("A08443");
        static readonly Color BrassDark = B.Hex("7A6332");
        static readonly Color Ticket = B.Hex("B4AC9A");
        static readonly Color TicketInk = B.Hex("8A3A32");

        /// <summary>
        /// The dishes' paints come from the icon renderer, which lights them flat and soft: under the
        /// level's sun the same colour comes out about a third brighter, and the glaze clips. Painted down.
        /// </summary>
        const float DishPaint = 0.74f;

        [MenuItem("Last Orders/Kitchen/Build Serving Pass (pfServingPass, Dishes, Waiter Animator)", priority = 106)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the serving pass - a 3 m counter module with a steel hot shelf for plates - into pfServingPass, " +
                "bakes a plated mesh for every dish, and makes the waiters' animator.\n\n" +
                "Run 'Set Up Serving Pass & Waiters (Current Scene)' to put it in the diner.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds everything, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);
            var report = new StringBuilder();

            Material painted = B.PaintedMaterial();
            Mesh counter = Counter().Save($"{Folder}/ServingPass.asset", "ServingPass");
            report.Append($"[Last Orders] Serving pass: counter {counter.vertexCount} vertices");

            PassDishSet dishes = BuildDishes(painted, report);
            string prefab = BuildPrefab(counter, painted, dishes);
            string animator = BuildWaiterAnimator();

            report.Append($"; {prefab}; {animator}.");
            return report.ToString();
        }

        // --- The counter ------------------------------------------------------------------------------

        static B.Model Counter()
        {
            // A shade darker toward the floor, as the walls are.
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.82f, 1f, K.Smooth(p.y / 0.4f)) };

            // The kick, set back under the body, the walls' brown.
            m.Box(Skirting, new Vector3(0f, KickHeight * 0.5f, (BodyFront + KickInset + BodyBack - 0.01f) * 0.5f),
                  new Vector3(BodyHalf * 2f - 0.02f, KickHeight, BodyBack - 0.01f - (BodyFront + KickInset)), 0.008f);

            // The body.
            m.Box(Cream, new Vector3(0f, (KickHeight + TopBottom) * 0.5f, (BodyFront + BodyBack) * 0.5f),
                  new Vector3(BodyHalf * 2f, TopBottom - KickHeight, BodyBack - BodyFront), 0.014f, 2);

            Front(m);

            // The red pinstripe, tucked under the top's front edge.
            m.Box(DoorRed, new Vector3(0f, TopBottom - 0.013f, BodyFront - 0.006f), new Vector3(BodyHalf * 2f - 0.01f, 0.018f, 0.014f), 0.004f);

            Top(m);
            Bell(m, new Vector3(1.39f, TopTop, -0.13f));
            TicketSpike(m, new Vector3(-1.39f, TopTop, -0.12f));
            return m;
        }

        /// <summary>The front: three panels framed in brown stiles and rails, each panel a shade lighter and standing proud.</summary>
        static void Front(B.Model m)
        {
            float face = BodyFront - FrameProud * 0.5f;
            float frameHeight = RailHigh + RailHeight * 0.5f - (RailLow - RailHeight * 0.5f);
            float frameMiddle = (RailHigh + RailHeight * 0.5f + RailLow - RailHeight * 0.5f) * 0.5f;

            foreach (float x in StileX)
                m.Box(Skirting, new Vector3(x, frameMiddle, face), new Vector3(StileWidth, frameHeight, FrameProud), 0.006f);

            foreach (float y in new[] { RailLow, RailHigh })
                m.Box(Skirting, new Vector3(0f, y, face), new Vector3(StileX[3] - StileX[0], RailHeight, FrameProud), 0.006f);

            float panelBottom = RailLow + RailHeight * 0.5f + 0.03f, panelTop = RailHigh - RailHeight * 0.5f - 0.03f;
            for (int i = 0; i < StileX.Length - 1; i++)
            {
                float left = StileX[i] + StileWidth * 0.5f + 0.03f, right = StileX[i + 1] - StileWidth * 0.5f - 0.03f;
                m.Box(CreamCap * 0.97f, new Vector3((left + right) * 0.5f, (panelBottom + panelTop) * 0.5f, BodyFront - 0.004f),
                      new Vector3(right - left, panelTop - panelBottom, 0.01f), 0.004f);
            }
        }

        /// <summary>
        /// The top: four maple boards running its length, each its own shade, with a steel hot shelf
        /// laid along it for the plates - grey under cream plates, so the dishes read from the floor.
        /// </summary>
        static void Top(B.Model m)
        {
            const int boards = 4;
            float depth = (TopBack - TopFront) / boards;
            for (int i = 0; i < boards; i++)
            {
                float z = TopFront + depth * (i + 0.5f);
                m.Box(G.Maple[(i + 1) % G.Maple.Length], new Vector3(0f, (TopBottom + TopTop) * 0.5f, z),
                      new Vector3(TopHalf * 2f, TopTop - TopBottom, depth - 0.004f), 0.012f);
            }

            m.Box(G.SteelLight, new Vector3(0f, TopTop + ShelfThick * 0.5f, (ShelfFront + ShelfBack) * 0.5f),
                  new Vector3(ShelfHalf * 2f, ShelfThick, ShelfBack - ShelfFront), 0.003f);

            // A raised lip along the shelf's kitchen side, where the cooks slide the plates on.
            m.Box(G.Steel, new Vector3(0f, TopTop + 0.012f, ShelfBack + 0.008f), new Vector3(ShelfHalf * 2f, 0.024f, 0.016f), 0.005f);
        }

        /// <summary>The service bell: a brass dome on a dark foot, its plunger on top.</summary>
        static void Bell(B.Model m, Vector3 foot)
        {
            m.Lathe((p, n) => B.WalnutDark, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.048f, 0f), new Vector2(0.05f, 0.008f), new Vector2(0.046f, 0.014f), new Vector2(0f, 0.014f),
            }, 16);
            m.Lathe((p, n) => Color.Lerp(BrassDark, Brass, K.Smooth((p.y - foot.y - 0.014f) / 0.035f)), foot + Vector3.up * 0.014f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.042f, 0f), new Vector2(0.04f, 0.012f), new Vector2(0.032f, 0.026f), new Vector2(0.018f, 0.034f),
                new Vector2(0f, 0.037f),
            }, 16);
            m.Tube(BrassDark, foot + Vector3.up * 0.05f, foot + Vector3.up * 0.064f, 0.005f, 8);
            m.Lathe((p, n) => Brass, foot + Vector3.up * 0.064f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.01f, 0f), new Vector2(0.01f, 0.004f), new Vector2(0f, 0.006f),
            }, 10);
        }

        /// <summary>A ticket spike: a dark foot, a steel pin and three order tickets speared on it at angles.</summary>
        static void TicketSpike(B.Model m, Vector3 foot)
        {
            m.Lathe((p, n) => B.WalnutDark, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.034f, 0f), new Vector2(0.032f, 0.014f), new Vector2(0f, 0.016f),
            }, 14);
            m.Tube((p, n) => G.Chrome, foot + Vector3.up * 0.016f, foot + Vector3.up * 0.12f, 0.003f, 0.001f, 6);

            for (int i = 0; i < 3; i++)
            {
                Quaternion q = Quaternion.Euler(4f * B.Jitter(i + 40), 30f + i * 47f, 3f * B.Jitter(i + 50));
                Vector3 at = foot + Vector3.up * (0.026f + i * 0.012f);
                m.Box(Ticket * (1f - 0.04f * i), at, new Vector3(0.068f, 0.003f, 0.09f), 0.002f, q);
                m.Box(TicketInk, at + q * new Vector3(0f, 0.002f, 0.03f), new Vector3(0.05f, 0.001f, 0.008f), 0.0005f, q);
            }
        }

        // --- The dishes -------------------------------------------------------------------------------

        static PassDishSet BuildDishes(Material material, StringBuilder report)
        {
            var entries = new List<PassDishSet.Entry>();
            var sizes = new List<string>();

            foreach (CropModels.Dish dish in Enum.GetValues(typeof(CropModels.Dish)))
            {
                if (!Enum.TryParse(dish.ToString(), out ItemType item))
                {
                    sizes.Add($"{dish} (no item)");
                    continue;
                }

                Mesh mesh = BakeDish(dish, out Vector3 was);
                entries.Add(new PassDishSet.Entry { item = item, mesh = mesh });
                sizes.Add($"{dish} {was.x:0.00}x{was.z:0.00}");
            }

            Mesh covered = Cloche().Save($"{Folder}/PassDish_Covered.asset", "PassDish_Covered");

            var set = AssetDatabase.LoadAssetAtPath<PassDishSet>(DishSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<PassDishSet>();
                AssetDatabase.CreateAsset(set, DishSetPath);
            }

            set.Configure(entries.ToArray(), covered, material);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);

            report.Append($"; {entries.Count} dishes ({string.Join(", ", sizes)} m before fitting to {DishSpan} m) + covered");
            return set;
        }

        /// <summary>
        /// The recipe icon's dish as one mesh, each part painted in its vertices with the gradient its
        /// paint has in the icon shader (base to tip up the part), scaled to <see cref="DishSpan"/>
        /// across, standing on the origin.
        /// </summary>
        static Mesh BakeDish(CropModels.Dish dish, out Vector3 measured)
        {
            Mesh built = CropModels.Plated(dish).Build($"{dish}_Plated", out List<int> used);
            try
            {
                Vector3[] v = built.vertices, n = built.normals;
                Vector2[] uv = built.uv;
                var colours = new Color32[v.Length];
                var triangles = new List<int>();

                for (int s = 0; s < built.subMeshCount; s++)
                {
                    CropModels.Swatch swatch = CropModels.Of((CropModels.Paint)used[s]);
                    float power = swatch.Power > 0f ? swatch.Power : 1f;

                    int[] sub = built.GetTriangles(s);
                    triangles.AddRange(sub);
                    foreach (int i in sub)
                    {
                        float up = uv.Length == v.Length ? Mathf.Pow(Mathf.Clamp01(uv[i].y), power) : 0.5f;
                        Color c = Color.Lerp(swatch.Base, swatch.Tip, up) * DishPaint;
                        c.a = 1f;
                        colours[i] = c;
                    }
                }

                Bounds b = built.bounds;
                measured = b.size;
                float scale = DishSpan / Mathf.Max(b.size.x, b.size.z, 1e-3f);
                var offset = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                for (int i = 0; i < v.Length; i++) v[i] = (v[i] + offset) * scale;

                var mesh = new Mesh { name = $"PassDish_{dish}" };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetColors(colours);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return SaveMesh(mesh, $"{Folder}/PassDish_{dish}.asset");
            }
            finally
            {
                Object.DestroyImmediate(built);
            }
        }

        /// <summary>A plate under a steel cloche: any dish the set has no model for.</summary>
        static B.Model Cloche()
        {
            var m = new B.Model();
            G.DinerPlate(m, Vector3.zero, DishSpan * 0.5f);
            m.Lathe((p, n) => Color.Lerp(G.Steel, G.Chrome, K.Smooth(p.y / 0.11f)), Vector3.up * 0.012f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.118f, 0f), new Vector2(0.116f, 0.03f), new Vector2(0.1f, 0.068f), new Vector2(0.068f, 0.094f),
                new Vector2(0.028f, 0.104f), new Vector2(0f, 0.106f),
            }, 24);
            m.Lathe((p, n) => G.Chrome, Vector3.up * 0.116f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.012f, 0f), new Vector2(0.016f, 0.012f), new Vector2(0.012f, 0.022f), new Vector2(0f, 0.024f),
            }, 12);
            return m;
        }

        /// <summary>Saved through the mesh API when it exists, as <see cref="B.Model.Save"/> does, so links to it hold.</summary>
        static Mesh SaveMesh(Mesh built, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                AssetDatabase.SaveAssetIfDirty(built);
                return built;
            }

            existing.Clear();
            existing.name = built.name;
            existing.SetVertices(built.vertices);
            existing.SetNormals(built.normals);
            existing.SetColors(built.colors32);
            existing.subMeshCount = 1;
            existing.SetTriangles(built.triangles, 0);
            existing.RecalculateBounds();

            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        // --- The prefab -------------------------------------------------------------------------------

        static string BuildPrefab(Mesh counter, Material painted, PassDishSet dishes)
        {
            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            if (created)
            {
                ContentGenerator.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
                var made = new GameObject(Path.GetFileNameWithoutExtension(PrefabPath));
                try
                {
                    made.AddComponent<ServingPass>();
                    PrefabUtility.SaveAsPrefabAsset(made, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(made);
                }
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (string old in new[] { "Counter", "Slots", "ServicePoints" })
                {
                    Transform previous = root.transform.Find(old);
                    if (previous != null) Object.DestroyImmediate(previous.gameObject);
                }

                var model = new GameObject("Counter");
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = new Vector3(0f, FloorTop, 0f);
                B.Show(model, counter, new[] { painted });
                var renderer = model.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                // It never moves: batched with whatever else stands still on the same material.
                GameObjectUtility.SetStaticEditorFlags(model, StaticEditorFlags.BatchingStatic);

                Transform slotsRoot = Group(root.transform, "Slots");
                var slots = new Transform[SlotX.Length];
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = Point(slotsRoot, $"Slot_{i}", new Vector3(SlotX[i], FloorTop + TopTop + ShelfThick, SlotZ), Quaternion.identity);

                Transform standsRoot = Group(root.transform, "ServicePoints");
                var stands = new Transform[StandX.Length];
                for (int i = 0; i < stands.Length; i++)
                    stands[i] = Point(standsRoot, $"Stand_{i}", new Vector3(StandX[i], 0f, StandZ), Quaternion.identity);

                // Not ??: a missing component comes back as Unity's fake null.
                var pass = root.GetComponent<ServingPass>();
                if (pass == null) pass = root.AddComponent<ServingPass>();
                pass.Configure(slots, stands, Rect.MinMaxRect(-TopHalf, TopFront, TopHalf, TopBack), dishes);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return $"{(created ? "made" : "rebuilt")} {PrefabPath}";
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Transform Point(Transform parent, string name, Vector3 at, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(at, rotation);
            return go.transform;
        }

        // --- The waiters' animator --------------------------------------------------------------------

        /// <summary>
        /// Base layer: the workers' breathing idle blended to the walk on Move. Carry layer: the staff's
        /// box-carrying arms, on the arms only, weighted by the waiter. Made the first time; left alone
        /// after, so a hand-tuned controller is never overwritten.
        /// </summary>
        static string BuildWaiterAnimator()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(WaiterControllerPath) != null)
                return $"kept {WaiterControllerPath}";

            var worker = AssetDatabase.LoadAssetAtPath<AnimatorController>(WorkerControllerPath);
            var staff = AssetDatabase.LoadAssetAtPath<AnimatorController>(StaffControllerPath);
            if (worker == null || staff == null) return "no waiter animator (worker or staff controller missing)";

            Motion idle = BlendChild(worker, 0), walk = BlendChild(worker, 1);
            if (idle == null || walk == null)
            {
                idle = BlendChild(staff, 0);
                walk = BlendChild(staff, 1);
            }

            AnimatorControllerLayer staffHands = staff.layers.Length > 1 ? staff.layers[1] : null;
            Motion carry = staffHands != null && staffHands.stateMachine.states.Length > 0 ? staffHands.stateMachine.states[0].state.motion : null;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(WaiterControllerPath);
            controller.AddParameter("Move", AnimatorControllerParameterType.Float);

            AnimatorState movement = controller.CreateBlendTreeInController("Movement", out BlendTree tree, 0);
            tree.blendParameter = "Move";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1f);
            controller.layers[0].stateMachine.defaultState = movement;

            controller.AddLayer("Carry");
            AnimatorControllerLayer[] layers = controller.layers;
            layers[1].avatarMask = staffHands != null ? staffHands.avatarMask : null;
            layers[1].defaultWeight = 0f;
            AnimatorState holding = layers[1].stateMachine.AddState("Carry");
            holding.motion = carry;
            layers[1].stateMachine.defaultState = holding;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return $"made {WaiterControllerPath} (idle {Name(idle)}, walk {Name(walk)}, carry {Name(carry)})";
        }

        /// <summary>The <paramref name="index"/>th motion of the first blend tree in the controller's base layer.</summary>
        static Motion BlendChild(AnimatorController controller, int index)
        {
            foreach (ChildAnimatorState state in controller.layers[0].stateMachine.states)
                if (state.state.motion is BlendTree tree && tree.children.Length > index)
                    return tree.children[index].motion;
            return null;
        }

        static string Name(Object o) => o != null ? o.name : "none";
    }
}
