using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The bread stove (pfBreadStove), modelled in the game's smooth low-poly style as a baker's
    /// wood-fired oven, so it says "bread" at a glance: a smooth pale plaster dome on a plinth of
    /// grey stone blocks, a red brick arch round its mouth with flames glowing inside, a brick
    /// chimney, firewood stacked in a niche under the hearth with its cut ends out, loaves cooling on
    /// a board on the hearth's ledge, a sack of flour and the long bread peel leaning on one side.
    ///
    /// Its stone and plaster are painted a mid grey: they show pale under the level's light, and
    /// painted any lighter they went flat white.
    ///
    /// Built with <see cref="BuildingKit"/> - colour painted into the vertices on LastOrders/Stylized,
    /// the fire in the unlit glow - and put into the prefab in place of the prototype stove model,
    /// which is switched off rather than deleted. It stands where that stood and fits its collider;
    /// the station's components, centre point and floor shadows are left alone.
    ///
    /// Its front faces -Z, out into the dining room. Built standing on y = 0; the station's own origin
    /// is a little above the floor, so the model is set down to it (<see cref="Floor"/>).
    /// </summary>
    public static class BreadOvenModel
    {
        const string PrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/BreadStove/pfBreadStove.prefab";
        const string MeshPath = "Assets/Game Assets/Content/LastOrders/Kitchen/" + EnvKit.Style + "/BreadOven.asset";

        const string OldModel = "Stove";
        const string ModelName = "BreadOven";

        /// <summary>Where the floor is in the station's space: the prototype stood its base here too.</summary>
        const float Floor = -0.135f;

        // --- Measurements (metres; front toward -Z) ------------------------------------------------

        const float PlinthWidth = 1.6f, PlinthDepth = 1.3f, PlinthHeight = 0.68f, PlinthZ = 0.06f;
        const float HearthThickness = 0.08f;
        const float DomeRadius = 0.6f, DomeHeight = 0.6f, DomeZ = 0.1f;

        /// <summary>The mouth: an arch springing from straight jambs, a ring of bricks round it.</summary>
        const float Spring = 0.14f, MouthRadius = 0.22f, ArchRadius = 0.36f;

        const float Front = PlinthZ - PlinthDepth * 0.5f;
        const float HearthTop = PlinthHeight + HearthThickness;

        /// <summary>The front of the vault the mouth is set in.</summary>
        const float MouthZ = DomeZ - DomeRadius - 0.14f;

        static readonly Color Plaster = B.Hex("A6A298");
        static readonly Color PlasterFoot = B.Hex("8E8A80");
        static readonly Color Soot = B.Hex("2A2624");
        static readonly Color Mortar = B.Hex("5E5C58");
        static readonly Color Hearth = B.Hex("8F8D87");
        static readonly Color[] Stones = { B.Hex("85837D"), B.Hex("8E8C86"), B.Hex("7C7A75"), B.Hex("93918B") };
        static readonly Color[] Bricks = { B.Hex("9E4B41"), B.Hex("8E4339"), B.Hex("A85449"), B.Hex("964840") };
        static readonly Color Keystone = B.Hex("B35C50");
        static readonly Color Cap = B.Hex("5E5F61");
        static readonly Color Wood = B.Hex("7A6250");
        static readonly Color WoodLight = B.Hex("A08A70");
        static readonly Color Bark = B.Hex("584536");
        static readonly Color LogEnd = B.Hex("C9AE86");
        static readonly Color Crust = B.Hex("B07A44");
        static readonly Color CrustTop = B.Hex("C99254");
        static readonly Color Score = B.Hex("E3C48E");

        [MenuItem("Last Orders/Kitchen/Build Bread Oven Model (pfBreadStove)", priority = 98)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the bread oven model and puts it into pfBreadStove in place of the prototype stove " +
                "model, which is switched off, not deleted.\n\nThe station's components, collider and centre point " +
                "are kept.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the model and puts it into the prefab, with no dialogs.</summary>
        public static string Apply()
        {
            Mesh mesh = Build().Save(MeshPath, "BreadOven");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Transform old = root.transform.Find(OldModel);
                if (old != null) old.gameObject.SetActive(false);

                Transform model = root.transform.Find(ModelName);
                if (model != null) Object.DestroyImmediate(model.gameObject);

                var go = new GameObject(ModelName);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, Floor, 0f);
                B.Show(go, mesh);

                // The bakery corner has taken its place: built switched off, so the two never stand in each other.
                bool replaced = root.transform.Find(BakeryCornerModel.ModelName) != null;
                go.SetActive(!replaced);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return $"[Last Orders] Bread oven built: {mesh.vertexCount} vertices, one renderer in pfBreadStove" +
                       $"{(old != null ? $"; the prototype '{OldModel}' model is switched off" : "")}" +
                       $"{(replaced ? $"; left switched off, as the '{BakeryCornerModel.ModelName}' stands in its place" : "")}.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // --- The model ----------------------------------------------------------------------------

        static B.Model Build()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, Smooth(p.y / 0.35f)) };

            Plinth(m);
            Dome(m);
            Mouth(m);
            Chimney(m);
            Loaves(m);
            Tools(m);

            return m;
        }

        /// <summary>Grey stone blocks in staggered courses round a mortar core, the firewood niche in the front, the hearth slab on top.</summary>
        static void Plinth(B.Model m)
        {
            float hw = PlinthWidth * 0.5f, hd = PlinthDepth * 0.5f, back = PlinthZ + hd;
            m.Box(Mortar, new Vector3(0f, PlinthHeight * 0.5f, PlinthZ), new Vector3(PlinthWidth - 0.04f, PlinthHeight, PlinthDepth - 0.04f), 0.02f);

            const int courses = 3;
            float course = PlinthHeight / courses;
            const float niche = 0.36f, lintel = niche + 0.08f;
            float nicheTop = course * 2f;
            int key = 0;

            Color Stone() => Stones[B.Hash(++key) % Stones.Length] * (1f + 0.04f * B.Jitter(key + 500));

            // The front: two courses cut round the niche, the third round the long stone lintel over it.
            for (int c = 0; c < courses; c++)
            {
                float y0 = c * course, y1 = y0 + course;
                float cut = c < 2 ? niche : lintel;
                foreach (Vector2 span in Blocks(-hw, hw, 0.38f, c))
                {
                    if (span.y > -cut && span.x < cut)
                    {
                        if (span.x < -cut) Block(m, Stone(), new Vector2(span.x, -cut), y0, y1, Front, Vector3.back);
                        if (span.y > cut) Block(m, Stone(), new Vector2(cut, span.y), y0, y1, Front, Vector3.back);
                        continue;
                    }

                    Block(m, Stone(), span, y0, y1, Front, Vector3.back);
                }
            }

            Block(m, Hearth, new Vector2(-lintel, lintel), nicheTop, PlinthHeight, Front, Vector3.back);

            // The sides, front to back.
            for (int side = -1; side <= 1; side += 2)
                for (int c = 0; c < courses; c++)
                    foreach (Vector2 span in Blocks(Front, back, 0.42f, c + 1))
                        Block(m, Stone(), span, c * course, (c + 1) * course, side * hw, side > 0 ? Vector3.right : Vector3.left);

            // The niche: soot-dark at the back, firewood stacked in it with the cut ends out.
            m.Box(Soot, new Vector3(0f, nicheTop * 0.5f, Front + 0.04f), new Vector3(niche * 2f, nicheTop, 0.06f), 0.01f);
            int log = 0;
            void Log(float x, float y)
            {
                log++;
                float r = 0.058f * (1f + 0.08f * B.Jitter(log + 40));
                float out0 = Front - 0.035f + 0.02f * B.Jitter(log + 60);
                m.Tube((p, n) => Mathf.Abs(n.z) > 0.8f ? LogEnd : Bark * (1f + 0.06f * B.Jitter(log)),
                       new Vector3(x, y, out0), new Vector3(x, y, Front + 0.07f), r, r, 9);
            }

            for (int i = 0; i < 5; i++) Log(-0.24f + i * 0.12f, 0.062f);
            for (int i = 0; i < 4; i++) Log(-0.18f + i * 0.12f, 0.17f);
            for (int i = 0; i < 3; i++) Log(-0.12f + i * 0.12f, 0.278f);

            // The hearth slab, reaching out in front as a ledge for the loaves.
            m.Box(Hearth, new Vector3(0f, PlinthHeight + HearthThickness * 0.5f, (Front - 0.24f + back + 0.02f) * 0.5f),
                  new Vector3(PlinthWidth + 0.06f, HearthThickness, back + 0.02f - (Front - 0.24f)), 0.025f, 2);
        }

        /// <summary>Spans of blocks <paramref name="length"/> long from <paramref name="from"/> to <paramref name="to"/>, every other course shifted half a block.</summary>
        static IEnumerable<Vector2> Blocks(float from, float to, float length, int course)
        {
            float x = from - (course % 2 == 1 ? length * 0.5f : 0f);
            while (x < to - 0.01f)
            {
                float a = Mathf.Max(from, x), b = Mathf.Min(to, x + length);
                if (b - a > 0.06f) yield return new Vector2(a, b);
                x += length;
            }
        }

        /// <summary>A stone block on a face: <paramref name="span"/> along it (x, or z for a side), <paramref name="y0"/> to <paramref name="y1"/> up, standing a little proud of the core.</summary>
        static void Block(B.Model m, Color colour, Vector2 span, float y0, float y1, float face, Vector3 outward)
        {
            bool front = Mathf.Abs(outward.z) > 0.5f;
            float mid = (span.x + span.y) * 0.5f, len = span.y - span.x - 0.022f;
            if (len < 0.04f) return;
            var centre = front ? new Vector3(mid, (y0 + y1) * 0.5f, face) : new Vector3(face, (y0 + y1) * 0.5f, mid);
            Vector3 size = front ? new Vector3(len, y1 - y0 - 0.022f, 0.07f) : new Vector3(0.07f, y1 - y0 - 0.022f, len);
            m.Box(colour, centre, size, 0.018f, 2);
        }

        /// <summary>
        /// The dome: smooth plaster on a darker foot band, a shade lower down. (A soot smudge over the
        /// mouth, painted into the dome's sparse vertices, came out as two dark streaks - a frowning face.)
        /// </summary>
        static void Dome(B.Model m)
        {
            Color Plastered(Vector3 p, Vector3 n)
            {
                float mottle = 1f + 0.025f * Mathf.Sin(p.x * 13f + p.y * 7f) * Mathf.Sin(p.z * 11f - p.y * 5f);
                return Plaster * mottle * Mathf.Lerp(0.9f, 1f, Smooth((p.y - HearthTop) / DomeHeight));
            }

            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(DomeRadius + 0.04f, 0f), new Vector2(DomeRadius + 0.04f, 0f),
                new Vector2(DomeRadius + 0.04f, 0.06f), new Vector2(DomeRadius + 0.04f, 0.06f), new Vector2(DomeRadius, 0.06f),
                new Vector2(DomeRadius, 0.06f),
            };
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 12f * Mathf.PI * 0.5f;
                profile.Add(new Vector2(DomeRadius * Mathf.Cos(t), 0.06f + DomeHeight * Mathf.Sin(t)));
            }

            m.Lathe((p, n) => p.y < HearthTop + 0.065f ? PlasterFoot : Plastered(p, n), new Vector3(0f, HearthTop, DomeZ), profile, 32);

            // The vault the mouth is set in, standing out from the dome's front.
            m.Surface(Plastered, 40, 4, (u, v) =>
            {
                Vector2 o = Outline(u, ArchRadius + 0.005f);
                return new Vector3(o.x, HearthTop + o.y, Mathf.Lerp(MouthZ + 0.03f, DomeZ - DomeRadius * 0.35f, v));
            });
        }

        /// <summary>
        /// The arch's outline, from the foot of its left jamb (<paramref name="t"/> 0) up, over the
        /// top and down to the foot of the right one (1), for an arch of <paramref name="radius"/>.
        /// </summary>
        static Vector2 Outline(float t, float radius)
        {
            float length = Spring * 2f + Mathf.PI * radius;
            float d = Mathf.Clamp01(t) * length;
            if (d < Spring) return new Vector2(-radius, d);
            if (d < Spring + Mathf.PI * radius)
            {
                float a = Mathf.PI - (d - Spring) / radius;
                return new Vector2(radius * Mathf.Cos(a), Spring + radius * Mathf.Sin(a));
            }

            return new Vector2(radius, Spring - (d - Spring - Mathf.PI * radius));
        }

        /// <summary>The brick arch with its keystone, the dark oven behind it and the fire burning inside: embers along its floor, flames licking up.</summary>
        static void Mouth(B.Model m)
        {
            const int voussoirs = 9;
            float ring = (MouthRadius + ArchRadius) * 0.5f, depth = ArchRadius - MouthRadius;
            float z = MouthZ - 0.005f;

            for (int i = 0; i < voussoirs; i++)
            {
                float a = Mathf.PI - (i + 0.5f) / voussoirs * Mathf.PI;
                bool key = i == voussoirs / 2;
                float radial = key ? depth + 0.045f : depth;
                float r = key ? ring + 0.02f : ring;
                var centre = new Vector3(r * Mathf.Cos(a), HearthTop + Spring + r * Mathf.Sin(a), z);
                float width = Mathf.PI * ring / voussoirs - 0.014f;
                m.Box(key ? Keystone : Bricks[B.Hash(i + 70) % Bricks.Length] * (1f + 0.04f * B.Jitter(i)), centre,
                      new Vector3(key ? width + 0.02f : width, radial, 0.08f), 0.014f, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f), 2);
            }

            // The jambs: two bricks a side, down to the hearth.
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 2; k++)
                    m.Box(Bricks[B.Hash(k + side * 5 + 90) % Bricks.Length], new Vector3(side * ring, HearthTop + Spring * (k + 0.5f) * 0.5f, z),
                          new Vector3(depth, Spring * 0.5f - 0.012f, 0.08f), 0.012f, 2);

            // Inside: the dark of the oven, the embers along its floor and three tongues of flame.
            // Kept inside the vault: taller or wider, its top corners came out through the plaster as two dark streaks.
            m.Box(Soot, new Vector3(0f, HearthTop + 0.19f, MouthZ + 0.17f), new Vector3(MouthRadius * 2f + 0.04f, 0.38f, 0.2f), 0.01f);

            var embers = new SmoothMesh(1);
            embers.RoundedBox(0, new Vector3(0f, HearthTop + 0.03f, MouthZ + 0.13f), new Vector3(0.38f, 0.05f, 0.14f), 0.02f, Quaternion.identity, 2);
            m.Add(embers, Color.white, B.Model.Glow);

            // Flames: teardrops, round at the foot and drawn to a point, the middle one tallest.
            float[] xs = { -0.13f, -0.065f, 0f, 0.07f, 0.135f };
            float[] heights = { 0.09f, 0.14f, 0.19f, 0.13f, 0.08f };
            for (int k = 0; k < xs.Length; k++)
            {
                var flame = new SmoothMesh(1);
                float h = heights[k], r = h * 0.3f, lean = (xs[k] * 0.4f);
                var foot = new Vector3(xs[k], HearthTop + 0.04f, MouthZ + 0.075f - k % 2 * 0.012f);
                flame.Surface(0, 10, 8, (u, v) =>
                {
                    float a = u * Mathf.PI * 2f;
                    // Floored at zero: sin(pi) comes out a hair below it at the tip, and a fractional power of that is NaN.
                    float width = r * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 1f, v))), 0.8f) * (1f - 0.55f * v);
                    return foot + new Vector3(Mathf.Cos(a) * width + lean * v * v, v * h, Mathf.Sin(a) * width * 0.45f);
                }, wrapU: true);
                m.Add(flame, Color.white, B.Model.Glow);
            }
        }

        /// <summary>A square brick chimney rising from the dome's front, a course at a time, under a slab cap.</summary>
        static void Chimney(B.Model m)
        {
            const float z = DomeZ - 0.22f, side = 0.28f, courseHeight = 0.1f;
            float baseY = HearthTop + 0.06f + DomeHeight * 0.62f;

            for (int c = 0; c < 6; c++)
            {
                Color brick = Bricks[B.Hash(c + 120) % Bricks.Length] * (1f + 0.04f * B.Jitter(c + 7));
                m.Box(brick, new Vector3(0f, baseY + courseHeight * (c + 0.5f), z), new Vector3(side, courseHeight - 0.012f, side), 0.014f, 2);
            }

            float top = baseY + courseHeight * 6f;
            m.Box(Cap, new Vector3(0f, top + 0.025f, z), new Vector3(side + 0.08f, 0.05f, side + 0.08f), 0.015f, 2);
            m.Box(Soot, new Vector3(0f, top + 0.052f, z), new Vector3(side - 0.1f, 0.012f, side - 0.1f), 0.004f);
        }

        /// <summary>Two scored loaves cooling on a board on the hearth's ledge, right of the mouth, and a round one to its left.</summary>
        static void Loaves(B.Model m)
        {
            float ledge = Front - 0.12f;
            m.Box(WoodLight, new Vector3(0.6f, HearthTop + 0.015f, ledge), new Vector3(0.42f, 0.03f, 0.2f), 0.01f, 2);

            Loaf(m, new Vector3(0.5f, HearthTop + 0.03f, ledge), new Vector3(0.1f, 0.06f, 0.06f), 8f, 1);
            Loaf(m, new Vector3(0.7f, HearthTop + 0.03f, ledge + 0.01f), new Vector3(0.09f, 0.055f, 0.06f), -10f, 2);
            Loaf(m, new Vector3(-0.56f, HearthTop, ledge), new Vector3(0.085f, 0.07f, 0.085f), 0f, 3);
        }

        /// <summary>A loaf sitting on <paramref name="foot"/>: a rounded crust browner low down, golden on top, with pale slashes across it.</summary>
        static void Loaf(B.Model m, Vector3 foot, Vector3 half, float yaw, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            bool round = Mathf.Abs(half.x - half.z) < 0.005f;

            float Slash(Vector3 local)
            {
                // Long loaves are slashed across; a round one with a cross.
                float cut = round
                    ? Mathf.Max(Mathf.Exp(-local.x * local.x / 0.00018f), Mathf.Exp(-local.z * local.z / 0.00018f))
                    : Mathf.Max(Mathf.Exp(-Mathf.Pow(local.x - local.z * 0.5f - 0.04f, 2f) / 0.00015f),
                                Mathf.Exp(-Mathf.Pow(local.x - local.z * 0.5f + 0.04f, 2f) / 0.00015f));
                return cut * Smooth((local.y - half.y * 0.55f) / (half.y * 0.3f));
            }

            m.Surface((p, n) =>
            {
                Vector3 local = Quaternion.Inverse(turn) * (p - foot - Vector3.up * half.y);
                local.y += half.y;
                float up = Smooth(local.y / (half.y * 2f));
                Color c = Color.Lerp(Crust * 0.88f, CrustTop, up);
                return Color.Lerp(c, Score, Slash(local - Vector3.up * half.y) * 0.85f);
            }, 24, 14, (u, v) =>
            {
                float th = v * Mathf.PI, ph = u * Mathf.PI * 2f;
                float s = Mathf.Sin(th);
                // Flattened underneath, domed on top.
                float y = -Mathf.Cos(th);
                y = y < 0f ? y * 0.55f : y;
                var p = new Vector3(s * Mathf.Cos(ph) * half.x, (y + 0.55f) / 1.55f * half.y * 2f, s * Mathf.Sin(ph) * half.z);
                float cut = Slash(new Vector3(p.x, p.y - half.y, p.z));
                p.y -= 0.008f * cut;
                return foot + turn * p;
            }, true);
        }

        /// <summary>A sack of flour at the plinth's foot and the long peel leaning on the right.</summary>
        static void Tools(B.Model m)
        {
            B.Sack(m, new Vector3(-0.6f, 0f, Front - 0.17f), 0.15f, 0.38f, 0.04f, 7);

            // The peel: a long handle, its broad flat blade up top.
            var foot = new Vector3(0.9f, 0f, Front + 0.12f);
            var top = new Vector3(0.84f, 1.2f, Front + 0.5f);
            Vector3 along = (top - foot).normalized;
            m.Tube(Wood, foot, top, 0.018f, 6);
            Quaternion lean = Quaternion.FromToRotation(Vector3.up, along);
            m.Box(WoodLight, top + along * 0.14f, new Vector3(0.022f, 0.3f, 0.24f), 0.01f, lean, 2);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
