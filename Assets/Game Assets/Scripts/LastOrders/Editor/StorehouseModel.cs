using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The storehouse - pfInventory, where the harvest and the cooked dishes are kept and where the
    /// pickups fly to - modelled in the game's low-poly style (see <see cref="BuildingKit"/>): a long
    /// timber shed of upright weathered planks between dark posts on stone footings, under a roof of
    /// grey corrugated tin streaked with rust; red barn doors with a lantern either side, a
    /// window in the gable end, crates, sacks and a barrel stacked out front.
    ///
    /// It replaces the four grey boxes the shed was (their materials stay where they were). It goes
    /// into the prefab as one renderer in the Storage group, where the boxes stood and covering the
    /// same ground; the prefab's components, its collider and the locked marker are left alone.
    /// </summary>
    public static class StorehouseModel
    {
        const string PrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Decoration/Inventory/pfInventory.prefab";
        const string MeshPath = B.Folder + "/Storehouse.asset";

        /// <summary>The boxes the shed was made of, under Storage.</summary>
        static readonly string[] OldParts = { "Body", "Door", "RoofA", "RoofB" };

        const string ModelName = "Model";

        /// <summary>Where the old body stood in the prefab: the model is built round its own middle and put here.</summary>
        static readonly Vector3 Centre = new Vector3(1.24f, 0f, 0.5f);

        // --- Measurements (metres, round the shed's middle; the front faces -Z) ------------------

        const float Width = 4.7f;
        const float Depth = 2.7f;
        const float Sill = 0.13f;
        const float WallHeight = 1.75f;

        /// <summary>The roof's underside over the long walls - high enough to clear the top plate - and its pitch.</summary>
        const float Eave = 1.9f;

        const float Slope = 0.6f;
        const float Overhang = 0.32f;
        const float GableOverhang = 0.28f;

        const float DoorWidth = 1.4f;
        const float DoorHeight = 1.5f;

        /// <summary>The boards' outer face, front and back.</summary>
        const float Face = Depth * 0.5f + 0.045f;

        static readonly Color DoorWood = B.Hex("9A4239");
        static readonly Color RidgeCap = B.Hex("8D9295");
        static readonly Color Water = B.Hex("3B4F57");

        /// <summary>Rust on galvanised tin: warmer and lighter than on the shelter's painted sheets, a stain rather than a scab.</summary>
        static readonly Color TinRust = B.Hex("937058");

        [MenuItem("Last Orders/Buildings/Build Storehouse Model (pfInventory)", priority = 74)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the storehouse model and puts it into pfInventory, in place of the grey boxes " +
                "the shed was made of.\n\nThe prefab's components, collider and locked marker are kept; " +
                "the old boxes' materials stay where they are.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the model and puts it into the prefab, with no dialogs.</summary>
        public static string Apply()
        {
            Mesh mesh = Build().Save(MeshPath, "Storehouse");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Transform storage = root.transform.Find("Storage");
                if (storage == null) return $"WARNING: no 'Storage' group in {PrefabPath}; nothing was changed.";

                var removed = new List<string>();
                for (int i = storage.childCount - 1; i >= 0; i--)
                {
                    Transform child = storage.GetChild(i);
                    if (child.name != ModelName && System.Array.IndexOf(OldParts, child.name) < 0) continue;
                    if (child.name != ModelName) removed.Add(child.name);
                    Object.DestroyImmediate(child.gameObject);
                }

                var model = new GameObject(ModelName);
                model.transform.SetParent(storage, false);
                model.transform.localPosition = Centre;
                B.Show(model, mesh);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return $"[Last Orders] Storehouse built: {mesh.vertexCount} vertices, one renderer in pfInventory/Storage" +
                       $"{(removed.Count > 0 ? $"; took out {string.Join(", ", removed)}" : "")}.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // --- The model ----------------------------------------------------------------------------

        static float RoofUnder(float z) => Eave + Slope * (Depth * 0.5f - Mathf.Abs(z));

        static B.Model Build()
        {
            var m = new B.Model { Shade = B.TimberShade(new Vector2(Width * 0.5f, Depth * 0.5f), Eave) };

            Footings(m);
            Walls(m);
            Roof(m);
            Doors(m);
            GableWindow(m);
            for (int side = -1; side <= 1; side += 2) m.Lantern(new Vector3(side * 1.02f, Sill + 1.4f, -Face), Vector3.back);
            Stores(m);

            return m;
        }

        /// <summary>A dark sill on stone footings - one at each corner, one under each long wall's middle - and the posts.</summary>
        static void Footings(B.Model m)
        {
            float hx = Width * 0.5f, hz = Depth * 0.5f;
            m.Box(B.WalnutDark, new Vector3(0f, 0.06f, 0f), new Vector3(Width + 0.16f, 0.14f, Depth + 0.16f), 0.03f, 2);

            int k = 0;
            foreach (float x in new[] { -hx, 0f, hx })
            {
                foreach (float z in new[] { -hz, hz })
                {
                    k++;
                    if (x == 0f && z < 0f) continue;
                    m.Box(B.Stone * (1f + 0.05f * B.Jitter(k)), new Vector3(x * 1.01f, 0.045f, z * 1.02f), new Vector3(0.32f, 0.13f, 0.3f), 0.045f,
                          Quaternion.Euler(0f, 7f * B.Jitter(k + 20), 0f), 2);
                }
            }

            // The corner posts, one each side of the doors, and one in the middle of the back.
            var posts = new List<Vector3>
            {
                new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz),
                new Vector3(0f, 0f, hz),
            };
            foreach (Vector3 p in posts)
                m.Box(B.Walnut, p + Vector3.up * (Sill + WallHeight) * 0.5f, new Vector3(0.15f, WallHeight - Sill + 0.02f, 0.15f), 0.025f, 2);
        }

        static void Walls(B.Model m)
        {
            float hx = Width * 0.5f, hz = Depth * 0.5f;
            const float inset = 0.075f, board = 0.19f;
            float Flat(float _) => WallHeight;

            var doorway = new[] { new Vector3(-DoorWidth * 0.5f - 0.06f, DoorWidth * 0.5f + 0.06f, Sill + DoorHeight + 0.03f) };
            m.BoardWall(new Vector3(0f, 0f, -hz), Vector3.right, Vector3.back, -hx + inset, hx - inset, Sill, Flat, board, 11, doorway);
            m.BoardWall(new Vector3(0f, 0f, hz), Vector3.right, Vector3.forward, -hx + inset, hx - inset, Sill, Flat, board, 12);
            m.BoardWall(new Vector3(-hx, 0f, 0f), Vector3.forward, Vector3.left, -hz + inset, hz - inset, Sill, Flat, board, 13);
            m.BoardWall(new Vector3(hx, 0f, 0f), Vector3.forward, Vector3.right, -hz + inset, hz - inset, Sill, Flat, board, 14);

            // A rail along the boards at knee height and the top plate over them, all round.
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(B.Walnut, new Vector3(0f, WallHeight + 0.05f, side * (hz + 0.035f)), new Vector3(Width + 0.16f, 0.1f, 0.1f), 0.02f, 2);
                m.Box(B.Walnut, new Vector3(side * (hx + 0.035f), WallHeight + 0.05f, 0f), new Vector3(0.1f, 0.1f, Depth + 0.16f), 0.02f, 2);
                m.Box(B.Walnut, new Vector3(side * (hx + 0.05f), 0.5f, 0f), new Vector3(0.05f, 0.08f, Depth - 0.1f), 0.015f);
            }

            foreach (float x0 in new[] { -hx + 0.08f, DoorWidth * 0.5f + 0.13f })
            {
                float x1 = x0 < 0f ? -DoorWidth * 0.5f - 0.13f : hx - 0.08f;
                m.Box(B.Walnut, new Vector3((x0 + x1) * 0.5f, 0.5f, -hz - 0.05f), new Vector3(x1 - x0, 0.08f, 0.05f), 0.015f);
            }

            // The gable ends, upright boards up under the roof, each topped by its lower corner.
            for (int side = -1; side <= 1; side += 2)
            {
                m.BoardWall(new Vector3(side * hx, 0f, 0f), Vector3.forward, side > 0 ? Vector3.right : Vector3.left, -hz - 0.02f, hz + 0.02f,
                            WallHeight + 0.1f, z => RoofUnder(Mathf.Abs(z) + 0.1f) + 0.015f, 0.2f, side > 0 ? 15 : 16);
            }
        }

        /// <summary>
        /// Two slopes of grey tin, four sheets painted on each with a seam between, rust run down
        /// from the fixings toward the eaves; a capping along the ridge and walnut barge boards at
        /// the gable ends.
        /// </summary>
        static void Roof(B.Model m)
        {
            const float thickness = 0.03f, pitch = 0.15f, depth = 0.035f;
            float cos = 1f / Mathf.Sqrt(1f + Slope * Slope), sin = Slope * cos;
            float run = Depth * 0.5f + Overhang;
            float length = run / cos;
            float span = Width + GableOverhang * 2f;
            float lift = thickness / cos;
            const int sheets = 4;
            float sheet = span / sheets;

            for (int side = -1; side <= 1; side += 2)
            {
                var down = new Vector3(0f, -sin, side * cos);
                Vector3 across = side > 0 ? Vector3.right : Vector3.left;
                var corner = new Vector3(side > 0 ? -span * 0.5f : span * 0.5f, RoofUnder(0.02f) + lift, side * 0.02f);
                int first = side > 0 ? 1 : 0;

                m.Corrugated(corner, across, down, span, length, pitch, depth, thickness, (u, v) =>
                {
                    int k = Mathf.Min(sheets - 1, Mathf.FloorToInt(u / sheet));
                    Color c = Weathered(B.Tin[(k + first) % B.Tin.Length], u, v / length, k + side * 10);
                    float seam = Mathf.Abs(u - Mathf.Round(u / sheet) * sheet);
                    return u > 0.1f && u < span - 0.1f && seam < 0.035f ? c * 0.8f : c;
                });

                float angle = Mathf.Atan(Slope) * Mathf.Rad2Deg * side;
                var mid = new Vector3(0f, RoofUnder(run * 0.5f) - 0.06f, side * run * 0.5f);
                foreach (float x in new[] { span * 0.5f - 0.04f, -span * 0.5f + 0.04f })
                {
                    m.Box(B.Walnut, mid + Vector3.right * x, new Vector3(0.05f, 0.13f, length + 0.04f), 0.015f,
                          Quaternion.Euler(angle, 0f, 0f), 2);
                }
            }

            float ridgeTop = RoofUnder(0f) + lift + depth;
            m.Tube(RidgeCap, new Vector3(-span * 0.5f - 0.02f, ridgeTop - 0.02f, 0f), new Vector3(span * 0.5f + 0.02f, ridgeTop - 0.02f, 0f), 0.065f, 14);
        }

        /// <summary>Galvanised tin, a shade lighter toward the ridge, rust run down in streaks toward the eave.</summary>
        static Color Weathered(Color tin, float u, float v, int key)
        {
            float phase = B.Jitter(key) * 10f;
            float streak = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 6.1f + phase)), 12f) +
                           0.7f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 11.7f + phase * 1.7f)), 16f);
            float amount = Mathf.Clamp01(streak) * Mathf.Clamp01((v - 0.45f) / 0.55f);
            Color c = tin * Mathf.Lerp(1.04f, 0.94f, v) * (1f + 0.025f * Mathf.Sin(u * 1.7f + phase));
            return Color.Lerp(c, TinRust, amount * 0.4f);
        }

        /// <summary>Double barn doors in a walnut frame: planked leaves, each braced with a Z, iron straps and pulls.</summary>
        static void Doors(B.Model m)
        {
            float z = -Depth * 0.5f;
            float top = Sill + DoorHeight;

            for (int side = -1; side <= 1; side += 2)
                m.Box(B.Walnut, new Vector3(side * (DoorWidth * 0.5f + 0.045f), Sill + DoorHeight * 0.5f, z - 0.045f),
                      new Vector3(0.09f, DoorHeight + 0.04f, 0.08f), 0.018f, 2);
            m.Box(B.Walnut, new Vector3(0f, top + 0.05f, z - 0.05f), new Vector3(DoorWidth + 0.28f, 0.1f, 0.09f), 0.02f, 2);

            float leaf = DoorWidth * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                float cx = side * leaf * 0.5f;
                const int planks = 3;
                for (int i = 0; i < planks; i++)
                {
                    float x = cx - leaf * 0.5f + leaf / planks * (i + 0.5f);
                    m.Box(DoorWood * (1f + 0.05f * B.Jitter(60 + i + side * 7)), new Vector3(x, Sill + DoorHeight * 0.5f, z - 0.005f),
                          new Vector3(leaf / planks - 0.012f, DoorHeight - 0.02f, 0.045f), 0.01f);
                }

                Color ledge = DoorWood * 0.85f;
                float low = Sill + 0.25f, high = top - 0.25f;
                m.Box(ledge, new Vector3(cx, low, z - 0.035f), new Vector3(leaf - 0.06f, 0.09f, 0.03f), 0.01f);
                m.Box(ledge, new Vector3(cx, high, z - 0.035f), new Vector3(leaf - 0.06f, 0.09f, 0.03f), 0.01f);

                float rise = high - low, across = leaf - 0.16f;
                // The brace climbs from the hinge side toward the middle, so the two leaves mirror each other.
                float angle = Mathf.Atan2(rise, across) * Mathf.Rad2Deg * -side;
                m.Box(ledge, new Vector3(cx, (low + high) * 0.5f, z - 0.035f), new Vector3(Mathf.Sqrt(rise * rise + across * across), 0.08f, 0.028f),
                      0.01f, Quaternion.Euler(0f, 0f, angle));

                foreach (float y in new[] { low, high })
                    m.Box(B.Iron, new Vector3(side * (leaf - 0.16f), y, z - 0.054f), new Vector3(0.3f, 0.035f, 0.012f), 0.006f);
                m.Box(B.Iron, new Vector3(side * 0.09f, Sill + 0.8f, z - 0.062f), new Vector3(0.035f, 0.22f, 0.03f), 0.01f, 2);
            }

            m.Box(B.Stone, new Vector3(0f, 0.04f, z - 0.3f), new Vector3(DoorWidth + 0.2f, 0.1f, 0.4f), 0.035f, 2);
        }

        /// <summary>A window in the gable end the camera sees: frame, a cross of glazing bars, a sill.</summary>
        static void GableWindow(B.Model m)
        {
            float x = Width * 0.5f + 0.045f;
            var c = new Vector3(x, 1.12f, 0f);
            const float hz = 0.3f, hy = 0.24f, bar = 0.07f;

            m.Surface((p, n) =>
            {
                float gleam = Mathf.Exp(-Mathf.Pow((p.z - c.z) * 0.9f - (p.y - c.y) + 0.03f, 2f) / 0.005f);
                return Color.Lerp(B.Glass, B.GlassSky, 0.25f + 0.55f * gleam + 0.2f * (p.y - c.y + hy) / (hy * 2f));
            }, 8, 8, (u, v) => c + new Vector3(0.012f, -hy + v * hy * 2f, -hz + u * hz * 2f));

            m.Box(B.Walnut, c + new Vector3(0.025f, hy + bar * 0.5f, 0f), new Vector3(0.06f, bar, hz * 2f + bar * 2f), 0.016f, 2);
            m.Box(B.Walnut, c + new Vector3(0.025f, -hy - bar * 0.5f, 0f), new Vector3(0.06f, bar, hz * 2f + bar * 2f), 0.016f, 2);
            m.Box(B.Walnut, c + new Vector3(0.025f, 0f, -hz - bar * 0.5f), new Vector3(0.06f, hy * 2f, bar), 0.016f, 2);
            m.Box(B.Walnut, c + new Vector3(0.025f, 0f, hz + bar * 0.5f), new Vector3(0.06f, hy * 2f, bar), 0.016f, 2);
            m.Box(B.Walnut, c + new Vector3(0.022f, 0f, 0f), new Vector3(0.03f, hy * 2f, 0.03f), 0.008f);
            m.Box(B.Walnut, c + new Vector3(0.022f, 0f, 0f), new Vector3(0.03f, 0.03f, hz * 2f), 0.008f);
            m.Box(B.HoneyLight, c + new Vector3(0.06f, -hy - bar - 0.02f, 0f), new Vector3(0.13f, 0.045f, hz * 2f + 0.2f), 0.014f, 2);
        }

        /// <summary>What the store holds, spilling out front: stacked crates and a sack to the left of the doors, a barrel and sacks to the right.</summary>
        static void Stores(B.Model m)
        {
            float z = -Depth * 0.5f - 0.36f;

            B.Crate(m, new Vector3(-1.62f, 0f, z), new Vector3(0.5f, 0.42f, 0.46f), 6f, 1);
            B.Crate(m, new Vector3(-1.6f, 0.45f, z + 0.02f), new Vector3(0.46f, 0.38f, 0.42f), -9f, 2);
            B.Crate(m, new Vector3(-2.1f, 0f, z - 0.05f), new Vector3(0.44f, 0.36f, 0.42f), 18f, 3);
            B.Sack(m, new Vector3(-2.08f, 0.39f, z - 0.05f), 0.15f, 0.32f, 0.03f, 4);

            B.Barrel(m, new Vector3(1.75f, 0f, z + 0.04f), 0.24f, 0.62f, B.DoorWood, Water);
            B.Sack(m, new Vector3(1.32f, 0f, z - 0.04f), 0.17f, 0.4f, -0.05f, 5);
            B.Sack(m, new Vector3(2.15f, 0f, z - 0.02f), 0.16f, 0.36f, 0.06f, 6);
        }
    }
}
