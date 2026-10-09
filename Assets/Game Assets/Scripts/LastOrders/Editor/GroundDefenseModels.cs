using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Defense;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// What the strip outside the walls can hold, modelled in code in the game's smooth low-poly style,
    /// and a prefab for each: a barricade of tin sheets nailed to posts and braced from behind, a
    /// sprung plate of rebar spikes, and a coil of razor wire staked out low - plus the pegs and twine an
    /// empty slot is marked out with.
    ///
    /// Each prefab carries a <see cref="GroundDefenseView"/> and the parts it switches and moves: the
    /// barricade standing, battered and wrecked; the spike bed that punches up; the coil that trembles,
    /// and the wire torn flat. Painted in their vertices on LastOrders/Stylized, from the same timber,
    /// tin and sacking as the compound's other props.
    ///
    /// The models stand on the slot's reinforced patch, which is their ground (y = 0), with +Z facing
    /// out from the wall. Re-running rebuilds the meshes in place, re-dresses each prefab by name and
    /// sets it on its definition.
    /// </summary>
    public static class GroundDefenseModels
    {
        internal const string Folder = "Assets/Game Assets/Content/LastOrders/Defense/" + EnvKit.Style + "/Strip";

        public const string BarricadePrefab = DefensePrefabGenerator.Folder + "/Strip_Barricade.prefab";
        public const string SpikePlatePrefab = DefensePrefabGenerator.Folder + "/Strip_SpikePlate.prefab";
        public const string RazorWirePrefab = DefensePrefabGenerator.Folder + "/Strip_RazorWire.prefab";

        /// <summary>The mesh an empty slot is marked out with: pegs at its corners, twine between them, ticks on the ground.</summary>
        public const string MarkerMeshPath = Folder + "/Strip_SlotMarker.asset";

        // --- The palette ---------------------------------------------------------------------------------

        static readonly Color[] Burlap = { B.Hex("A9946F"), B.Hex("9F8A66"), B.Hex("B19C78"), B.Hex("958260") };
        static readonly Color BurlapSeam = B.Hex("7E6D50");
        static readonly Color IronDark = B.Hex("2F3236");
        static readonly Color Steel = B.Hex("6E7479");
        static readonly Color SteelLight = B.Hex("8D9398");
        static readonly Color PlateSteel = B.Hex("5F6468");
        static readonly Color Rebar = B.Hex("6A5446");
        static readonly Color Galvanised = B.Hex("9AA0A4");
        static readonly Color Rust = B.Hex("7D5E4A");
        static readonly Color Hole = B.Hex("1A1918");
        static readonly Color Ochre = B.Hex("8F7A4A");
        static readonly Color Charcoal = B.Hex("34312C");
        static readonly Color PaintMark = B.Hex("BDB6A6");
        static readonly Color FreshCut = B.Hex("B8A07C");
        static readonly Color SheetRed = B.Hex("844A42");
        static readonly Color BareTin = B.Hex("8F969B");

        [MenuItem("Last Orders/Defense/Rebuild Strip Defenses (Models & Prefabs)", priority = 46)]
        static void Run() => Debug.Log(Apply(DefenseContentGenerator.GenerateGroundDefenses()));

        /// <summary>Builds every mesh and prefab, and sets each prefab on the definition of its kind. No dialogs.</summary>
        public static string Apply(GroundDefenseDefinition[] definitions)
        {
            ContentGenerator.EnsureFolder(Folder);
            ContentGenerator.EnsureFolder(DefensePrefabGenerator.Folder);

            Mesh standing = Save(Barricade(Wear.Standing), "Barricade_Standing");
            Mesh battered = Save(Barricade(Wear.Battered), "Barricade_Battered");
            Mesh wrecked = Save(Barricade(Wear.Wrecked), "Barricade_Wrecked");
            Mesh frame = Save(SpikeFrame(), "SpikePlate_Frame");
            Mesh spikes = Save(SpikeBed(), "SpikePlate_Spikes");
            Mesh pickets = Save(Pickets(torn: false), "RazorWire_Pickets");
            Mesh coil = Save(Coil(), "RazorWire_Coil");
            Mesh torn = Save(TornWire(), "RazorWire_Torn");
            Mesh marker = Save(Marker(), "Strip_SlotMarker");

            GameObject barricade = Prefab(BarricadePrefab, "Strip_Barricade", root =>
            {
                Transform body = Child(root.transform, "Body");
                GameObject a = Show(Child(body, "Standing"), standing);
                GameObject b = Show(Child(body, "Battered"), battered);
                GameObject c = Show(Child(body, "Wrecked"), wrecked);
                b.SetActive(false);
                c.SetActive(false);
                View(root).Configure(a, b, c, body, null, null);
            });

            GameObject plate = Prefab(SpikePlatePrefab, "Strip_SpikePlate", root =>
            {
                Transform body = Child(root.transform, "Body");
                GameObject a = Show(Child(body, "Frame"), frame);

                // Down in the plate at rest: the tips just under its holes.
                Transform bed = Child(body, "Spikes");
                bed.localPosition = Vector3.down * SpikeRest;
                Show(bed, spikes);
                View(root).Configure(a, null, null, body, bed, null);
            });

            GameObject wire = Prefab(RazorWirePrefab, "Strip_RazorWire", root =>
            {
                Transform body = Child(root.transform, "Body");
                Transform up = Child(body, "Standing");
                Show(up, pickets);
                Transform turns = Child(up, "Coil");
                turns.localPosition = new Vector3(0f, CoilY, 0f);
                Show(turns, coil);
                GameObject down = Show(Child(body, "Torn"), torn);
                down.SetActive(false);
                View(root).Configure(up.gameObject, null, down, body, null, new[] { turns });
            });

            int set = 0;
            foreach (GroundDefenseDefinition definition in definitions)
            {
                if (definition == null) continue;

                GameObject prefab = definition.kind == GroundDefenseKind.Barricade ? barricade
                                  : definition.kind == GroundDefenseKind.Trap ? plate : wire;
                if (definition.prefab == prefab) continue;

                definition.prefab = prefab;
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
                set++;
            }

            return $"[Last Orders] Strip defenses built: barricade {standing.vertexCount} vertices, spike plate {frame.vertexCount + spikes.vertexCount}, " +
                   $"razor wire {pickets.vertexCount + coil.vertexCount}, slot marker {marker.vertexCount}; {set} definition(s) given their prefab.";
        }

        public static Mesh LoadMarker() => AssetDatabase.LoadAssetAtPath<Mesh>(MarkerMeshPath);

        // --- Prefabs ---------------------------------------------------------------------------------------

        static GameObject Prefab(string path, string name, Action<GameObject> dress)
        {
            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(path) == null;
            GameObject root = created ? new GameObject(name) : PrefabUtility.LoadPrefabContents(path);

            try
            {
                dress(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (created) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static GroundDefenseView View(GameObject root)
        {
            var view = root.GetComponent<GroundDefenseView>();
            return view != null ? view : root.AddComponent<GroundDefenseView>();
        }

        static Transform Child(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            t.gameObject.SetActive(true);
            return t;
        }

        static GameObject Show(Transform t, Mesh mesh)
        {
            B.Show(t.gameObject, mesh, new[] { B.PaintedMaterial() });
            return t.gameObject;
        }

        static Mesh Save(B.Model model, string name) => model.Save($"{Folder}/{name}.asset", name);

        // --- The barricade -------------------------------------------------------------------------------

        enum Wear { Standing, Battered, Wrecked }

        const float PostHeight = 1.12f;
        static readonly float[] PostX = { -1.1f, 0f, 1.1f };

        /// <summary>
        /// Tin sheets nailed across three sharpened posts, two rails behind them, braces down to the
        /// ground on the wall side, a row of sandbags along the foot and stakes jutting out at the ends.
        /// Battered: the red sheet half torn away and hanging, the middle post leaning, the top rail
        /// split. Wrecked: posts snapped to stumps, the sheets and rails down on the ground.
        /// </summary>
        static B.Model Barricade(Wear wear)
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.76f, 1f, S.Smooth(p.y / 0.7f)) };

            if (wear == Wear.Wrecked)
            {
                WreckedBarricade(m);
                return m;
            }

            bool battered = wear == Wear.Battered;

            for (int i = 0; i < PostX.Length; i++)
            {
                float lean = battered && i == 1 ? 11f : 1.5f * B.Jitter(i + 4);
                Post(m, new Vector3(PostX[i], 0f, -0.03f), PostHeight + 0.05f * B.Jitter(i + 7), Quaternion.Euler(0f, 0f, lean), i);
            }

            // Rails behind the sheets: the top one split and sagging once it has been hit hard.
            Rail(m, 0.32f, 0, 0f);
            if (battered)
            {
                m.Box(B.Plank(31), new Vector3(-0.62f, 0.86f, -0.11f), new Vector3(1.2f, 0.11f, 0.035f), 0.012f, Quaternion.Euler(0f, 0f, 3f));
                m.Box(B.Plank(32), new Vector3(0.55f, 0.72f, -0.11f), new Vector3(1.15f, 0.11f, 0.035f), 0.012f, Quaternion.Euler(0f, 0f, -16f));
            }
            else Rail(m, 0.86f, 1, 0f);

            // The sheets, nailed over the front. The red one is an old panel off something painted.
            Sheet(m, new Vector3(-1.2f, 1.06f, 0.045f), 1.24f, 0.96f, (u, v) => TinPaint(u, v, 1), Quaternion.identity);
            if (battered)
            {
                // Torn off at the bottom and swung out from its top nails.
                Sheet(m, new Vector3(-0.02f, 1.0f, 0.06f), 1.22f, 0.62f, (u, v) => RedPaint(u, v, 2), Quaternion.Euler(-28f, 0f, 4f),
                      pivot: new Vector3(-0.02f, 1.0f, 0.06f));
            }
            else Sheet(m, new Vector3(-0.02f, 1.0f, 0.06f), 1.22f, 0.9f, (u, v) => RedPaint(u, v, 2), Quaternion.identity);

            Nails(m, battered);

            // Braced from behind, down to the ground on the wall side.
            foreach (float x in new[] { -1.1f, 0f, 1.1f })
                Beam(m, B.Plank(40 + (int)(x * 10f)), new Vector3(x, PostHeight - 0.2f, -0.1f), new Vector3(x + 0.06f, 0.03f, -0.5f), 0.065f);

            // Sandbags along the foot.
            for (int i = 0; i < 5; i++)
            {
                if (battered && i == 3) continue;
                Bag(m, new Vector3(-0.96f + i * 0.48f, 0.075f, 0.2f), 3f * B.Jitter(i + 12), i);
            }

            if (battered) Bag(m, new Vector3(0.5f, 0.06f, 0.48f), 38f, 3);

            // Stakes jutting out past each end, sharpened.
            for (int side = -1; side <= 1; side += 2)
                Stake(m, new Vector3(side * 1.24f, 0.25f, -0.05f), new Vector3(side * 1.36f, 0.62f, 0.6f), 0.034f, side + 50);

            return m;
        }

        static void WreckedBarricade(B.Model m)
        {
            // The posts snapped off low, the splinters left standing.
            for (int i = 0; i < PostX.Length; i++)
            {
                float h = 0.26f + 0.08f * B.Jitter(i + 60);
                var foot = new Vector3(PostX[i], 0f, -0.03f);
                m.Box(B.Plank(i), foot + Vector3.up * (h * 0.5f), new Vector3(0.11f, h, 0.11f), 0.012f);
                m.Tube((p, n) => FreshCut, foot + Vector3.up * h, foot + new Vector3(0.02f, h + 0.12f, 0.01f), 0.04f, 0.004f, 5);
            }

            // What was standing, down on the ground.
            Beam(m, B.Plank(1), new Vector3(-1.0f, 0.06f, 0.35f), new Vector3(-0.1f, 0.06f, 0.62f), 0.1f);
            Beam(m, B.Plank(2), new Vector3(0.3f, 0.06f, 0.5f), new Vector3(1.15f, 0.07f, 0.28f), 0.1f);
            m.Box(B.Plank(31), new Vector3(-0.3f, 0.04f, -0.3f), new Vector3(1.9f, 0.035f, 0.11f), 0.012f, Quaternion.Euler(0f, 9f, 0f));

            FlatSheet(m, new Vector3(-1.2f, 0.03f, -0.1f), 1.2f, 0.85f, (u, v) => TinPaint(u, v, 1), 8f, 4f);
            FlatSheet(m, new Vector3(0.05f, 0.05f, -0.35f), 1.1f, 0.8f, (u, v) => RedPaint(u, v, 2), -12f, -7f);

            for (int i = 0; i < 4; i++)
                Bag(m, new Vector3(-0.9f + i * 0.58f + 0.08f * B.Jitter(i + 70), 0.07f, 0.2f + 0.12f * B.Jitter(i + 71)), 30f * B.Jitter(i + 72), i + 20);
        }

        static void Post(B.Model m, Vector3 foot, float height, Quaternion lean, int key)
        {
            Color wood = B.Plank(key + 17) * 0.94f;
            Vector3 up = lean * Vector3.up;
            m.Box(wood, foot + up * (height * 0.5f), new Vector3(0.11f, height, 0.11f), 0.014f, lean);
            m.Tube((p, n) => Color.Lerp(wood, FreshCut, S.Smooth((p.y - foot.y - height) / 0.14f)), foot + up * height,
                   foot + up * (height + 0.17f), 0.06f, 0.008f, 8);
        }

        static void Rail(B.Model m, float y, int key, float tilt) =>
            m.Box(B.Plank(30 + key), new Vector3(0f, y, -0.11f), new Vector3(2.42f, 0.11f, 0.035f), 0.012f, Quaternion.Euler(0f, 0f, tilt));

        /// <summary>A corrugated sheet facing +Z, its top-left corner at <paramref name="corner"/>, turned about <paramref name="pivot"/>.</summary>
        static void Sheet(B.Model m, Vector3 corner, float width, float length, Func<float, float, Color> paint, Quaternion turn,
                          Vector3? pivot = null)
        {
            Matrix4x4 was = m.Transform;
            Vector3 about = pivot ?? corner;
            m.Transform = was * Matrix4x4.TRS(about, turn, Vector3.one) * Matrix4x4.Translate(-about);
            m.Corrugated(corner, Vector3.right, Vector3.down, width, length, 0.076f, 0.022f, 0.01f, paint, rowStep: 0.08f);
            m.Transform = was;
        }

        /// <summary>A corrugated sheet lying on the ground from <paramref name="corner"/>, turned by <paramref name="yaw"/>, one edge propped up by <paramref name="tilt"/>.</summary>
        static void FlatSheet(B.Model m, Vector3 corner, float width, float length, Func<float, float, Color> paint, float yaw, float tilt)
        {
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(corner, Quaternion.Euler(0f, yaw, tilt), Vector3.one);
            m.Corrugated(Vector3.zero, Vector3.right, Vector3.forward, width, length, 0.076f, 0.022f, 0.01f, paint, rowStep: 0.08f);
            m.Transform = was;
        }

        static Color TinPaint(float u, float v, int key)
        {
            float rust = S.Smooth((v - 0.55f) / 0.4f) * (0.5f + 0.5f * Mathf.Sin(u * 13f + key));
            Color tin = B.Tin[key % B.Tin.Length] * 0.86f * (1f + 0.04f * Mathf.Sin(u * 7f + v * 3f));
            return Color.Lerp(tin, Rust, rust * 0.55f);
        }

        static Color RedPaint(float u, float v, int key)
        {
            float chip = Mathf.Sin(u * 19f + key * 1.7f) * Mathf.Sin(v * 15f - key * 2.3f) + 0.35f * Mathf.Sin((u + v) * 37f + key);
            if (chip > 1.05f) return BareTin;
            return SheetRed * (1f + 0.05f * Mathf.Sin(u * 6f + v * 9f));
        }

        static void Nails(B.Model m, bool battered)
        {
            foreach (float x in PostX)
                foreach (float y in new[] { 0.32f, 0.86f })
                {
                    if (battered && x > 0f && y > 0.5f) continue;
                    S.Ball(m, (p, n) => IronDark, new Vector3(x, y, 0.09f), new Vector3(0.016f, 0.016f, 0.01f), Quaternion.identity, 6, 4);
                }
        }

        static void Bag(B.Model m, Vector3 at, float yaw, int key)
        {
            const float Length = 0.46f, Height = 0.15f, Depth = 0.28f;
            Quaternion lie = Quaternion.Euler(3f * B.Jitter(key), yaw, 4f * B.Jitter(key + 9));
            m.Box(Burlap[B.Hash(key) % Burlap.Length], at, new Vector3(Length, Height, Depth), 0.07f, lie, 3);
            m.Box(BurlapSeam, at + lie * new Vector3(Length * 0.43f, 0f, 0f), new Vector3(0.05f, Height * 0.86f, Depth * 0.86f), 0.02f, lie, 2);
        }

        static void Stake(B.Model m, Vector3 from, Vector3 to, float radius, int key)
        {
            Vector3 tip = Vector3.Lerp(from, to, 0.8f);
            Color wood = B.Plank(key) * 0.9f;
            m.Tube(wood, from, tip, radius, 8);
            m.Tube((p, n) => FreshCut, tip, to, radius, 0.005f, 8);
        }

        // --- The spike plate --------------------------------------------------------------------------------

        const float PlateHalfX = 0.75f, PlateHalfZ = 0.6f, PlateTop = 0.085f;
        const float SpikeHeight = 0.3f;

        /// <summary>Metres the spike bed sits down in the plate at rest, so only their tips are under the holes.</summary>
        const float SpikeRest = SpikeHeight - PlateTop + 0.01f;

        static readonly float[] SpikeXs = { -0.52f, -0.26f, 0f, 0.26f, 0.52f };
        static readonly float[] SpikeZs = { -0.36f, -0.12f, 0.12f, 0.36f };

        /// <summary>
        /// A steel plate in a raised rim painted with worn hazard stripes, a grid of holes the spikes
        /// come up through, anchor bolts at its corners, and the spring box down one side.
        /// </summary>
        static B.Model SpikeFrame()
        {
            var m = new B.Model();

            m.Box(IronDark, new Vector3(0f, 0.025f, 0f), new Vector3(PlateHalfX * 2f, 0.05f, PlateHalfZ * 2f), 0.012f);
            m.Box(PlateSteel, new Vector3(0f, PlateTop - 0.015f, 0f), new Vector3(PlateHalfX * 2f - 0.16f, 0.03f, PlateHalfZ * 2f - 0.16f), 0.008f);

            // Tread: low ribs across the plate between the rows of holes.
            for (int k = 0; k < SpikeZs.Length - 1; k++)
            {
                float z = (SpikeZs[k] + SpikeZs[k + 1]) * 0.5f;
                m.Box(Steel, new Vector3(0f, PlateTop + 0.003f, z), new Vector3(PlateHalfX * 2f - 0.24f, 0.008f, 0.03f), 0.003f);
            }

            foreach (float x in SpikeXs)
                foreach (float z in SpikeZs)
                    S.Disc(m, (p, n) => Hole, new Vector3(x, PlateTop + 0.001f, z), Vector3.up, 0.03f, 10);

            // The rim, in short blocks of ochre and charcoal: stripes paint can only change at a vertex.
            Stripes(m, new Vector3(-PlateHalfX, 0f, -PlateHalfZ), Vector3.right, PlateHalfX * 2f, 0);
            Stripes(m, new Vector3(PlateHalfX, 0f, PlateHalfZ), Vector3.left, PlateHalfX * 2f, 1);
            Stripes(m, new Vector3(PlateHalfX, 0f, -PlateHalfZ), Vector3.forward, PlateHalfZ * 2f, 0);
            Stripes(m, new Vector3(-PlateHalfX, 0f, PlateHalfZ), Vector3.back, PlateHalfZ * 2f, 1);

            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -PlateHalfX + 0.04f : PlateHalfX - 0.04f, z = (k & 2) == 0 ? -PlateHalfZ + 0.04f : PlateHalfZ - 0.04f;
                m.Tube(SteelLight, new Vector3(x, 0.1f, z), new Vector3(x, 0.125f, z), 0.024f, 6);
            }

            // The spring box down the right side, a lever out of it.
            var box = new Vector3(PlateHalfX + 0.07f, 0.07f, 0f);
            m.Box(IronDark, box, new Vector3(0.12f, 0.14f, 0.52f), 0.015f, 2);
            m.Box(Ochre * 0.9f, box + new Vector3(0.061f, 0.01f, 0f), new Vector3(0.004f, 0.06f, 0.3f), 0.002f);
            m.Tube(SheetRed, box + new Vector3(0.03f, 0.07f, 0.18f), box + new Vector3(0.1f, 0.17f, 0.24f), 0.014f, 6);
            return m;
        }

        static void Stripes(B.Model m, Vector3 start, Vector3 along, float length, int key)
        {
            const float Block = 0.15f;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / Block));
            float step = length / count;
            Vector3 inward = Vector3.Cross(Vector3.up, along).normalized;
            Quaternion turn = Quaternion.LookRotation(along);

            for (int i = 0; i < count; i++)
            {
                Vector3 centre = start + along * (step * (i + 0.5f)) + inward * 0.04f + Vector3.up * 0.05f;
                Color c = (i + key) % 2 == 0 ? Ochre : Charcoal;
                c *= 1f + 0.05f * B.Jitter(i * 7 + key);
                m.Box(c, centre, new Vector3(0.08f, 0.1f, step + 0.002f), 0.01f, turn);
            }
        }

        /// <summary>The bed of sharpened rebar on its own plate: a grid of spikes, rusty and bright by turns.</summary>
        static B.Model SpikeBed()
        {
            var m = new B.Model();
            m.Box(IronDark, new Vector3(0f, 0.015f, 0f), new Vector3(1.24f, 0.03f, 0.9f), 0.008f);

            int key = 0;
            foreach (float x in SpikeXs)
                foreach (float z in SpikeZs)
                {
                    Color shaft = key % 3 == 0 ? SteelLight : Rebar;
                    var foot = new Vector3(x, 0.03f, z);
                    m.Tube((p, n) => Color.Lerp(shaft, SteelLight * 1.08f, S.Smooth((p.y - 0.2f) / 0.1f)), foot,
                           foot + Vector3.up * (SpikeHeight - 0.03f + 0.02f * B.Jitter(key)), 0.024f, 0.004f, 6);
                    key++;
                }

            return m;
        }

        // --- The razor wire ---------------------------------------------------------------------------------

        const float CoilY = 0.38f, CoilRadiusY = 0.32f, CoilRadiusZ = 0.38f, CoilHalf = 1.2f;
        const int CoilTurns = 15;

        static readonly float[] PicketX = { -1.05f, 0f, 1.05f };

        /// <summary>Three crossed stakes lashed in the middle, the coil laid through them.</summary>
        static B.Model Pickets(bool torn)
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.78f, 1f, S.Smooth(p.y / 0.6f)) };

            for (int i = 0; i < PicketX.Length; i++)
            {
                float x = PicketX[i];
                Quaternion lean = Quaternion.Euler(0f, 4f * B.Jitter(i + 3), torn && i == 2 ? 62f : 2.5f * B.Jitter(i + 5));
                Vector3 Lean(Vector3 p) => new Vector3(x, 0f, 0f) + lean * (p - new Vector3(x, 0f, 0f));

                Beam(m, B.Plank(i * 3 + 1), Lean(new Vector3(x, 0f, -0.42f)), Lean(new Vector3(x, 0.84f, 0.3f)), 0.05f);
                Beam(m, B.Plank(i * 3 + 2), Lean(new Vector3(x + 0.04f, 0f, 0.42f)), Lean(new Vector3(x + 0.04f, 0.84f, -0.3f)), 0.05f);
                m.Tube(B.Twine, Lean(new Vector3(x - 0.04f, 0.45f, 0f)), Lean(new Vector3(x + 0.08f, 0.45f, 0f)), 0.03f, 8);
            }

            return m;
        }

        /// <summary>
        /// The coil: a helix of wire along X round its own middle - the prefab lifts it to
        /// <see cref="CoilY"/> - flattened a little, with barbs along it, so it trembles about its axis.
        /// </summary>
        static B.Model Coil()
        {
            var m = new B.Model();
            Helix(m, -CoilHalf, CoilHalf, CoilRadiusY, CoilRadiusZ, 0f, 0);
            return m;
        }

        static void Helix(B.Model m, float x0, float x1, float ry, float rz, float sag, int key)
        {
            const float Wire = 0.016f;
            float turns = CoilTurns * (x1 - x0) / (CoilHalf * 2f);
            int steps = Mathf.Max(24, Mathf.RoundToInt(turns * 24f));

            Vector3 Centre(float v)
            {
                float a = v * turns * Mathf.PI * 2f;
                float x = Mathf.Lerp(x0, x1, v);
                float droop = sag * Mathf.Sin(v * Mathf.PI);
                return new Vector3(x, Mathf.Sin(a) * ry - droop, Mathf.Cos(a) * rz);
            }

            Color Paint(Vector3 p, Vector3 n)
            {
                float spot = Mathf.Sin(p.x * 23f + key) * Mathf.Sin(p.z * 17f - p.y * 11f);
                return spot > 0.7f ? Rust : Galvanised * (1f + 0.05f * Mathf.Sin(p.x * 9f));
            }

            m.Surface(Paint, 5, steps, (u, v) =>
            {
                const float e = 1e-3f;
                Vector3 c = Centre(v);
                Vector3 t = (Centre(Mathf.Min(1f, v + e)) - Centre(Mathf.Max(0f, v - e))).normalized;
                Vector3 side = Vector3.Cross(t, Vector3.right).normalized;
                if (side.sqrMagnitude < 1e-6f) side = Vector3.up;
                Vector3 up = Vector3.Cross(side, t);
                float a = u * Mathf.PI * 2f;
                return c + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * Wire;
            }, wrapU: true);

            // Barbs: short spikes across the wire, six to a turn.
            int barbs = Mathf.RoundToInt(turns * 6f);
            for (int b = 0; b < barbs; b++)
            {
                float v = (b + 0.5f) / barbs;
                Vector3 c = Centre(v);
                Vector3 t = (Centre(Mathf.Min(1f, v + 0.002f)) - Centre(Mathf.Max(0f, v - 0.002f))).normalized;
                Vector3 across = Vector3.Cross(t, c - new Vector3(c.x, 0f, 0f)).normalized;
                if (across.sqrMagnitude < 1e-6f) across = Vector3.right;
                m.Tube((p, n) => SteelLight, c - across * 0.04f, c + across * 0.04f, 0.012f, 0.003f, 4);
            }
        }

        /// <summary>The wire torn and crushed flat, cut through near the middle, one of the pickets knocked over.</summary>
        static B.Model TornWire()
        {
            B.Model m = Pickets(torn: true);

            m.Transform = Matrix4x4.Translate(new Vector3(0f, 0.13f, 0.05f));
            Helix(m, -CoilHalf, -0.18f, 0.11f, 0.46f, 0.04f, 1);
            Helix(m, 0.22f, CoilHalf, 0.13f, 0.44f, -0.03f, 2);
            m.Transform = Matrix4x4.identity;
            return m;
        }

        // --- The empty slot ---------------------------------------------------------------------------------

        /// <summary>
        /// A slot waiting to be built on: pegs at its four corners with a line of twine round them, and
        /// painted corner ticks on the patch, as a site is staked out.
        /// </summary>
        static B.Model Marker()
        {
            var m = new B.Model();
            const float X = 1.15f, Z = 0.7f;

            var corners = new[] { new Vector3(-X, 0f, -Z), new Vector3(X, 0f, -Z), new Vector3(X, 0f, Z), new Vector3(-X, 0f, Z) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = corners[i];
                Quaternion tilt = Quaternion.Euler(4f * B.Jitter(i + 1), 0f, 4f * B.Jitter(i + 2));
                m.Box(B.WalnutDark, c + Vector3.up * 0.13f, new Vector3(0.045f, 0.26f, 0.045f), 0.01f, tilt);
                m.Box(Ochre, c + tilt * Vector3.up * 0.23f, new Vector3(0.05f, 0.05f, 0.05f), 0.01f, tilt);
            }

            for (int i = 0; i < 4; i++)
                m.Tube(B.Twine, corners[i] + Vector3.up * 0.18f, corners[(i + 1) % 4] + Vector3.up * 0.18f, 0.007f, 5);

            // The ticks: an L at each corner, a hand's breadth in from the pegs.
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = corners[i] * 0.93f;
                float sx = -Mathf.Sign(c.x), sz = -Mathf.Sign(c.z);
                m.Box(PaintMark, c + new Vector3(sx * 0.14f, 0.003f, 0f), new Vector3(0.3f, 0.006f, 0.05f), 0.002f);
                m.Box(PaintMark, c + new Vector3(0f, 0.003f, sz * 0.12f), new Vector3(0.05f, 0.006f, 0.26f), 0.002f);
            }

            return m;
        }

        // --- Helpers -----------------------------------------------------------------------------------------

        static void Beam(B.Model m, Color colour, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 along = b - a;
            Quaternion turn = Quaternion.LookRotation(along.normalized, Mathf.Abs(along.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
            m.Box(colour, (a + b) * 0.5f, new Vector3(thickness, thickness, along.magnitude), 0.01f, turn);
        }
    }
}
