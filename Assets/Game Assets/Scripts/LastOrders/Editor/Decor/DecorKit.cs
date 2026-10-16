using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Model = LastOrders.EditorTools.BuildingKit.Model;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The diner's decorations - plants, a jukebox, a menu board, a gumball machine and the rest -
    /// modelled in code to stand beside the imported dining table and chairs: the same chunky rounded
    /// shapes, the same honey wood, teal metal, cream and cushion red, and the same toon lighting.
    ///
    /// The dining set's look is a hand-painted texture: every part a gradient, darker and richer where
    /// it turns away or runs down to the floor, a worn light edge where a top face rolls over. Here that
    /// paint goes into the vertices (<see cref="Painted"/>) and the material is the dining set's own
    /// LastOrders/Toon with its vertex paint switched on, so the two light alike by day and by night.
    ///
    /// The palette was measured off the dining set's texture (the median of each colour, with the
    /// darker edge and the lighter highlight it is painted with). The project renders in gamma space,
    /// so these are the albedo values as they are.
    /// </summary>
    public static class DecorKit
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Decor";
        public const string MeshFolder = Folder + "/Meshes";
        const string MaterialPath = Folder + "/Decor_Toon.mat";

        /// <summary>The imported dining set's material: its lighting is copied, so the decorations match it.</summary>
        const string DiningSetMaterialPath = "Assets/Game Assets/Models/lambert3.mat";

        /// <summary>The top of the diner's floor slab, where a decoration's foot stands.</summary>
        public const float Floor = 0.035f;

        // --- The palette (measured off the dining set) ---------------------------------------------

        public static readonly Color Honey = Hex("F0B151");
        public static readonly Color HoneyEdge = Hex("DC9035");
        public static readonly Color HoneyDark = Hex("BB6E32");
        public static readonly Color Groove = Hex("8A4A24");

        public static readonly Color Teal = Hex("4F6F66");
        public static readonly Color TealLight = Hex("638E85");
        public static readonly Color TealDark = Hex("3A4F48");

        public static readonly Color Cream = Hex("EDD8B2");
        public static readonly Color CreamLight = Hex("F4E7CA");
        public static readonly Color CreamShade = Hex("D5BC97");

        public static readonly Color Red = Hex("BE4A36");
        public static readonly Color RedLight = Hex("D95943");
        public static readonly Color RedDark = Hex("8F2E22");

        // --- Beside it: what the decorations hold, in the same saturation ---------------------------

        public static readonly Color Chrome = Hex("C3C9C8");
        public static readonly Color ChromeDark = Hex("8D9799");
        public static readonly Color Soil = Hex("5E3F2A");
        public static readonly Color Leaf = Hex("4E9638");
        public static readonly Color LeafLight = Hex("72B547");
        public static readonly Color Stem = Hex("6A8A36");
        public static readonly Color Slate = Hex("33443E");
        public static readonly Color Chalk = Hex("EFE6D2");
        public static readonly Color Butter = Hex("F2C552");
        public static readonly Color Coral = Hex("E2785C");
        public static readonly Color Ink = Hex("27302D");

        public static Color Hex(string rgb) => PrimitiveKit.Hex(rgb);

        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // --- The paint ------------------------------------------------------------------------------

        /// <summary>
        /// <paramref name="colour"/> lightened or darkened by <paramref name="k"/> the way the dining
        /// set is painted: a shade is richer as well as darker, a highlight a little paler.
        /// </summary>
        public static Color Tone(Color colour, float k)
        {
            Color.RGBToHSV(colour, out float h, out float s, out float v);
            float saturation = k < 1f ? Mathf.Lerp(1f, 1.2f, (1f - k) / 0.35f) : Mathf.Lerp(1f, 0.86f, (k - 1f) / 0.15f);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * saturation), Mathf.Min(v * k, 0.98f));
        }

        /// <summary>
        /// How a painter shades a part standing from <paramref name="bottom"/> to <paramref name="top"/>:
        /// darker low down, a lift on whatever faces up, darker underneath, and a worn light edge
        /// where a top face rolls over the side.
        /// </summary>
        public static float Light(float y, Vector3 n, float bottom, float top)
        {
            float t = top > bottom ? Mathf.Clamp01((y - bottom) / (top - bottom)) : 1f;
            float k = Mathf.Lerp(0.84f, 1.03f, Smooth(t));
            k *= n.y >= 0f ? 1f + 0.05f * n.y : 1f + 0.3f * n.y;
            k *= 1f + 0.08f * Mathf.Clamp01(1f - Mathf.Abs(n.y - 0.6f) / 0.3f);
            return k;
        }

        /// <summary>A part painted <paramref name="colour"/>, shaded over its height <paramref name="bottom"/> to <paramref name="top"/>.</summary>
        public static Func<Vector3, Vector3, Color> Painted(Color colour, float bottom, float top) =>
            (p, n) => Tone(colour, Light(p.y, n, bottom, top));

        /// <summary>
        /// A new decoration, darkened where it meets the floor as the dining set's legs are: the
        /// painted contact shadow, under whatever cast shadow the sun adds.
        /// </summary>
        public static Model New()
        {
            return new Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, Smooth((p.y - Floor) / 0.16f)) };
        }

        // --- Shapes ---------------------------------------------------------------------------------

        /// <summary>A rounded box painted <paramref name="colour"/>, shaded over its own height.</summary>
        public static void Box(Model m, Color colour, Vector3 centre, Vector3 size, float radius, Quaternion rotation, int segments = 2)
        {
            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, centre, size, radius, rotation, segments);
            float reach = Mathf.Abs((rotation * new Vector3(size.x, 0f, 0f)).y) + Mathf.Abs((rotation * new Vector3(0f, size.y, 0f)).y) +
                          Mathf.Abs((rotation * new Vector3(0f, 0f, size.z)).y);
            m.Add(shape, Painted(colour, centre.y - reach * 0.5f, centre.y + reach * 0.5f));
        }

        public static void Box(Model m, Color colour, Vector3 centre, Vector3 size, float radius, int segments = 2) =>
            Box(m, colour, centre, size, radius, Quaternion.identity, segments);

        /// <summary>A round rod from <paramref name="from"/> to <paramref name="to"/>, painted as one part.</summary>
        public static void Rod(Model m, Color colour, Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int segments = 10)
        {
            m.Tube(Painted(colour, Mathf.Min(from.y, to.y), Mathf.Max(from.y, to.y)), from, to, radiusFrom, radiusTo, segments);
        }

        /// <summary>A turned shape round the Y axis through <paramref name="foot"/>, coloured by height with <paramref name="colourAt"/>.</summary>
        public static void Turned(Model m, Func<float, Color> colourAt, Vector3 foot, IList<Vector2> profile, int segments = 20,
                                  int sub = Model.Painted)
        {
            float bottom = float.MaxValue, top = float.MinValue;
            foreach (Vector2 p in profile)
            {
                bottom = Mathf.Min(bottom, p.y);
                top = Mathf.Max(top, p.y);
            }

            m.Lathe((p, n) => Tone(colourAt(p.y - foot.y), Light(p.y - foot.y, n, bottom, top)), foot, profile, segments, sub);
        }

        public static void Turned(Model m, Color colour, Vector3 foot, IList<Vector2> profile, int segments = 20) =>
            Turned(m, y => colour, foot, profile, segments);

        /// <summary>A ball of radius <paramref name="r"/>: a knob, a gumball, a flower's eye.</summary>
        public static void Ball(Model m, Color colour, Vector3 centre, float r, int segments = 10, int rings = 6, int sub = Model.Painted)
        {
            var profile = new List<Vector2>();
            for (int i = 0; i <= rings; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * i / rings;
                profile.Add(new Vector2(Mathf.Max(Mathf.Cos(a) * r, 0f), Mathf.Sin(a) * r));
            }

            profile[0] = new Vector2(0f, -r);
            profile[rings] = new Vector2(0f, r);
            m.Lathe((p, n) => Tone(colour, Light(p.y - centre.y, n, -r, r)), centre, profile, segments, sub);
        }

        /// <summary>
        /// A bar of rounded square section bent round an arc in the XY plane about <paramref name="centre"/>:
        /// <paramref name="radius"/> to the middle of the bar, <paramref name="thick"/> across it in the
        /// plane, <paramref name="depth"/> through it along Z, from <paramref name="fromDeg"/> to
        /// <paramref name="toDeg"/> (0 = +X, 90 = up; from below to). A jukebox's arch, the tube of light inside it.
        /// </summary>
        public static void Arch(Model m, Func<Vector3, Vector3, Color> paint, Vector3 centre, float radius, float thick, float depth,
                                float fromDeg, float toDeg, int steps, int sub = Model.Painted)
        {
            m.Surface(paint, 24, steps, (u, v) =>
            {
                float a = Mathf.Lerp(fromDeg, toDeg, v) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);

                // Round the section: a squircle, flat sides with soft corners.
                float b = u * Mathf.PI * 2f;
                float cx = Mathf.Cos(b), cy = Mathf.Sin(b);
                float sx = Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), 0.4f), sy = Mathf.Sign(cy) * Mathf.Pow(Mathf.Abs(cy), 0.4f);
                return centre + radial * (radius + sx * thick * 0.5f) + Vector3.forward * (sy * depth * 0.5f);
            }, true, sub);
        }

        /// <summary>
        /// A broad leaf, both faces, from <paramref name="root"/> out along <paramref name="heading"/>
        /// (flattened to level): it rises at <paramref name="rise"/> (height gained per length) and
        /// droops by <paramref name="droop"/> toward its tip, <paramref name="length"/> long and
        /// <paramref name="width"/> across its widest, its halves folded up from the midrib by
        /// <paramref name="fold"/>. The top is <paramref name="colour"/> with a paler midrib, darker at
        /// the stalk; the underside paler and duller.
        /// </summary>
        public static void LeafBlade(Model m, Vector3 root, Vector3 heading, float rise, float droop, float length, float width, float fold,
                                     Color colour, int steps = 8)
        {
            Vector3 fwd = new Vector3(heading.x, 0f, heading.z).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;

            Vector3 Mid(float s) => root + fwd * (length * s) + Vector3.up * (length * (rise * s - droop * s * s));
            // sin(pi) comes out a hair below zero, and a fractional power of that is NaN.
            float Half(float s) => width * 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(s), 0.72f))), 0.8f);

            Vector3 At(float u, float s)
            {
                float a = u * 2f - 1f;
                float w = Half(s);
                return Mid(s) + side * (a * w) + Vector3.up * (fold * Mathf.Abs(a) * w);
            }

            Color Paint(Vector3 p, Vector3 n, bool top)
            {
                Vector3 d = p - root;
                float s = Mathf.Clamp01(Vector3.Dot(d, fwd) / length);
                float across = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(d, side)) / Mathf.Max(Half(s), 1e-3f));
                float k = Mathf.Lerp(1.16f, 0.9f, Smooth(across)) * Mathf.Lerp(0.82f, 1.04f, Smooth(s * 1.6f));
                return top ? Tone(colour, k) : Tone(Color.Lerp(colour, CreamShade, 0.18f), k * 0.92f);
            }

            m.Surface((p, n) => Paint(p, n, true), 4, steps, At);
            m.Surface((p, n) => Paint(p, n, false), 4, steps, (u, s) => At(1f - u, s) - Vector3.up * 0.004f);
        }

        /// <summary>
        /// A slab of the outline <paramref name="outline"/> - points (z, y), round it either way -
        /// from <paramref name="x0"/> to <paramref name="x1"/> along X: flat faces with sharp edges, the
        /// side of a cabinet. The outline must be visible whole from its own centre.
        /// </summary>
        public static void Prism(Model m, Color colour, IList<Vector2> outline, float x0, float x1)
        {
            int count = outline.Count;
            float bottom = float.MaxValue, top = float.MinValue, area = 0f;
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % count];
                area += a.x * b.y - b.x * a.y;
                centre += a;
                bottom = Mathf.Min(bottom, a.y);
                top = Mathf.Max(top, a.y);
            }

            centre /= count;
            Func<Vector3, Vector3, Color> paint = Painted(colour, bottom, top);

            // Walked so that, seen from +X, the outline runs anticlockwise: then each edge's outward
            // normal is on its right in (z, y), and the faces below are wound outward.
            var points = new List<Vector2>(outline);
            if (area < 0f) points.Reverse();

            Vector2 Along(float u)
            {
                float f = Mathf.Repeat(u, 1f) * count;
                int i = Mathf.Min((int)f, count - 1);
                return Vector2.Lerp(points[i], points[(i + 1) % count], f - i);
            }

            Vector3 P(float x, Vector2 zy) => new Vector3(x, zy.y, zy.x);

            // The rim, one flat face per edge.
            for (int i = 0; i < count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % count];
                m.Surface(paint, 1, 1, (u, v) => P(Mathf.Lerp(x1, x0, u), Vector2.Lerp(a, b, v)));
            }

            // The two faces, each a fan in from the outline; corners land on grid lines, so it closes.
            int steps = count * 2;
            m.Surface(paint, steps, 1, (u, v) => P(x1, Vector2.Lerp(Along(u), centre, v)), true);
            m.Surface(paint, steps, 1, (u, v) => P(x0, Vector2.Lerp(Along(1f - u), centre, v)), true);
        }

        // --- The material ---------------------------------------------------------------------------

        /// <summary>
        /// LastOrders/Toon with the dining set's lighting, no palette texture and the vertex paint on:
        /// made if missing, set every time.
        /// </summary>
        public static Material Material()
        {
            ContentGenerator.EnsureFolder(Folder);
            Shader shader = Shader.Find("LastOrders/Toon");
            if (shader == null) throw new InvalidOperationException("Shader LastOrders/Toon is missing.");

            var diningSet = AssetDatabase.LoadAssetAtPath<Material>(DiningSetMaterialPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Decor_Toon" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = shader;
            if (diningSet != null && diningSet.shader == shader)
            {
                foreach (string colour in new[] { "_ShadeColor", "_RimColor" }) material.SetColor(colour, diningSet.GetColor(colour));
                foreach (string value in new[] { "_Threshold", "_Softness", "_LightStrength", "_AmbientStrength", "_RimStrength" })
                    material.SetFloat(value, diningSet.GetFloat(value));
            }

            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_VertexPaint", 1f);
            material.SetFloat("_LeafStrength", 0f);
            material.SetFloat("_CanopyLow", 1f);
            material.SetFloat("_CanopyHigh", 1f);
            material.SetFloat("_WindBend", 0f);
            material.SetFloat("_WindFlutter", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        /// <summary>The materials for a decoration's submeshes: painted, then glowing if it has a glow.</summary>
        public static Material[] MaterialsFor(Mesh mesh) =>
            mesh.subMeshCount > 1 ? new[] { Material(), BuildingKit.GlowMaterial() } : new[] { Material() };
    }
}
