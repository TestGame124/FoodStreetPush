using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Surfaces painted as shapes with height: the turf's clumps, the floor's tiles, the path's
    /// stones. Each texture comes with a normal map made from that height, so the level's light
    /// rounds the clumps and catches the tiles' bevels - and moves over them as the sun does - the
    /// soft, solid look of the smooth low-poly models standing on them.
    /// </summary>
    public static partial class TextureGenerator
    {
        /// <summary>Where the normal map painted with <paramref name="albedoPath"/> lives.</summary>
        public static string NormalPath(string albedoPath) =>
            albedoPath.Substring(0, albedoPath.Length - ".png".Length) + "_Normal.png";

        /// <summary>The normal map painted with <paramref name="albedo"/>, or null if it has none.</summary>
        public static Texture2D NormalFor(Texture albedo)
        {
            string path = albedo != null ? AssetDatabase.GetAssetPath(albedo) : null;
            return string.IsNullOrEmpty(path) || !path.EndsWith(".png") ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath(path));
        }

        /// <summary>
        /// Gives a URP Lit material the normal map that goes with its base map, or takes the normal
        /// map away if the base map has none.
        /// </summary>
        public static void BindNormal(Material material, Texture albedo, float scale = 1f)
        {
            Texture2D normal = NormalFor(albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", scale);

            if (normal != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
        }

        /// <summary>The albedo at <paramref name="path"/> if it and its normal map were both painted by this version.</summary>
        static Texture2D CurrentRelief(string path, string version)
        {
            Texture2D albedo = Current(path, version);
            return albedo != null && Current(NormalPath(path), version) != null ? albedo : null;
        }

        /// <summary>Writes a relief out as its albedo and its normal map, and returns the albedo.</summary>
        static Texture2D SaveRelief(string path, Relief relief, float normalStrength, TextureWrapMode wrapU, TextureWrapMode wrapV,
                                    string version)
        {
            Save(NormalPath(path), relief.NormalMap(normalStrength), relief.W, relief.H, TextureImporterType.NormalMap, false, wrapU,
                 wrapV, version);
            return Save(path, relief.Albedo, relief.W, relief.H, TextureImporterType.Default, true, wrapU, wrapV, version);
        }

        /// <summary>
        /// A surface being painted as shapes with height, addressed in metres. U always wraps; V
        /// wraps when asked.
        ///
        /// Shapes are drawn the way solid things stack: where two overlap, whichever stands taller
        /// there shows, so a clump laid on a mound sits on it and a stone pushed into the earth
        /// shows only its top. Heights are in metres too, so the normal map's slopes are the
        /// shapes' real slopes.
        /// </summary>
        sealed class Relief
        {
            public readonly int W, H;
            public readonly float MetresU, MetresV;
            public readonly Color[] Albedo;
            public readonly float[] Height;
            readonly bool wrapV;

            public float PpmU => W / MetresU;
            public float PpmV => H / MetresV;

            public Relief(int w, int h, float metresU, float metresV, bool wrapV = true)
            {
                W = w;
                H = h;
                MetresU = metresU;
                MetresV = metresV;
                this.wrapV = wrapV;
                Albedo = new Color[w * h];
                Height = new float[w * h];
            }

            int Index(int x, int y) => (wrapV ? Wrap(y, H) : Mathf.Clamp(y, 0, H - 1)) * W + Wrap(x, W);

            bool Inside(int y) => wrapV || (y >= 0 && y < H);

            public void Fill(Color c, float height = 0f)
            {
                for (int i = 0; i < Albedo.Length; i++)
                {
                    Albedo[i] = new Color(c.r, c.g, c.b, 1f);
                    Height[i] = height;
                }
            }

            public float HeightAt(float u, float v) => Height[Index(Mathf.FloorToInt(u * PpmU), Mathf.FloorToInt(v * PpmV))];

            /// <summary>
            /// A rounded hump: an oval <paramref name="rx"/> by <paramref name="ry"/> turned by
            /// <paramref name="angle"/>, rising <paramref name="height"/> above <paramref name="standOn"/>
            /// at its middle and easing down to nothing at its rim. Drawn only where it stands taller
            /// than what is there; with <paramref name="soft"/> (metres) its colour fades in over that
            /// much rise, so a low hump shades into the ground round it instead of showing a rim.
            /// </summary>
            public void Mound(float u, float v, float rx, float ry, float height, Color colour, float standOn = 0f, float angle = 0f,
                              float soft = 0f)
            {
                float cx = u * PpmU, cy = v * PpmV;
                float px = rx * PpmU, py = ry * PpmV;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                int reach = Mathf.CeilToInt(Mathf.Max(px, py)) + 1;
                float rim = Mathf.Min(px, py);

                for (int y = Mathf.FloorToInt(cy) - reach; y <= Mathf.CeilToInt(cy) + reach; y++)
                {
                    if (!Inside(y)) continue;

                    for (int x = Mathf.FloorToInt(cx) - reach; x <= Mathf.CeilToInt(cx) + reach; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        float nx = (dx * cos + dy * sin) / px, ny = (dy * cos - dx * sin) / py;
                        float d2 = nx * nx + ny * ny;
                        if (d2 >= 1f) continue;

                        float h = standOn + height * (1f - d2) * (1f - 0.3f * d2);
                        Put(Index(x, y), h, colour, Mathf.Clamp01((1f - Mathf.Sqrt(d2)) * rim), soft);
                    }
                }
            }

            /// <summary>
            /// A gentle swell added to whatever is there - the surface rises under it rather than
            /// meeting it at a rim - its colour fading in toward the middle, up to
            /// <paramref name="opacity"/>.
            /// </summary>
            public void Swell(float u, float v, float rx, float ry, float height, Color colour, float opacity, float angle = 0f)
            {
                float cx = u * PpmU, cy = v * PpmV;
                float px = rx * PpmU, py = ry * PpmV;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                int reach = Mathf.CeilToInt(Mathf.Max(px, py)) + 1;

                for (int y = Mathf.FloorToInt(cy) - reach; y <= Mathf.CeilToInt(cy) + reach; y++)
                {
                    if (!Inside(y)) continue;

                    for (int x = Mathf.FloorToInt(cx) - reach; x <= Mathf.CeilToInt(cx) + reach; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        float nx = (dx * cos + dy * sin) / px, ny = (dy * cos - dx * sin) / py;
                        float d2 = nx * nx + ny * ny;
                        if (d2 >= 1f) continue;

                        float k = (1f - d2) * (1f - d2);
                        int i = Index(x, y);
                        Height[i] += height * k;
                        Albedo[i] = Color.Lerp(Albedo[i], colour, k * opacity);
                    }
                }
            }

            public Color ColourAt(float u, float v) => Albedo[Index(Mathf.FloorToInt(u * PpmU), Mathf.FloorToInt(v * PpmV))];

            /// <summary>
            /// A pointed blade from <paramref name="root"/> along <paramref name="dir"/>, gently
            /// curved, folded along its middle into two flat faces - one turned to the light, one
            /// away - as a low-poly blade is. Darker at its foot, lighter toward the tip.
            /// </summary>
            public void Blade(Vector2 root, Vector2 dir, float length, float width, float height, Color foot, Color tip,
                              float standOn = 0f)
            {
                dir.Normalize();
                var side = new Vector2(dir.y, -dir.x);
                float bend = length * 0.1f * (dir.x >= 0f ? 1f : -1f);

                float reach = length + width;
                int x0 = Mathf.FloorToInt((root.x - reach) * PpmU), x1 = Mathf.CeilToInt((root.x + reach) * PpmU);
                int y0 = Mathf.FloorToInt((root.y - reach) * PpmV), y1 = Mathf.CeilToInt((root.y + reach) * PpmV);
                float ppm = (PpmU + PpmV) * 0.5f;

                for (int y = y0; y <= y1; y++)
                {
                    if (!Inside(y)) continue;

                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2((x + 0.5f) / PpmU, (y + 0.5f) / PpmV) - root;
                        float t = Vector2.Dot(p, dir) / length;
                        if (t < 0f || t > 1f) continue;

                        float across = Vector2.Dot(p, side) - bend * t * t;
                        float half = width * 0.5f * Mathf.Pow(1f - t, 0.8f);
                        float inside = half - Mathf.Abs(across);
                        if (inside <= -1f / ppm) continue;

                        float fold = half > 1e-5f ? Mathf.Clamp01(inside / half) : 0f;
                        float h = standOn + height * (1f - 0.55f * t) * fold;
                        Color c = Color.Lerp(foot, tip, Mathf.Pow(t, 1.1f));
                        Put(Index(x, y), h, c, Mathf.Clamp01(inside * ppm + 0.5f) * Mathf.Clamp01(t * length * ppm), 0f);
                    }
                }
            }

            /// <summary>Pushes the surface down along a line - a crack, a seam - and darkens it.</summary>
            public void Groove(Vector2 a, Vector2 b, float width, float depth, Color colour, float opacity)
            {
                float ppm = (PpmU + PpmV) * 0.5f;
                float w = width * 0.5f * ppm;
                Vector2 pa = new Vector2(a.x * PpmU, a.y * PpmV), pb = new Vector2(b.x * PpmU, b.y * PpmV);
                Vector2 ab = pb - pa;
                float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);

                for (int y = Mathf.FloorToInt(Mathf.Min(pa.y, pb.y) - w - 2f); y <= Mathf.CeilToInt(Mathf.Max(pa.y, pb.y) + w + 2f); y++)
                {
                    if (!Inside(y)) continue;

                    for (int x = Mathf.FloorToInt(Mathf.Min(pa.x, pb.x) - w - 2f); x <= Mathf.CeilToInt(Mathf.Max(pa.x, pb.x) + w + 2f); x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - pa, ab) / len2);
                        float d = Vector2.Distance(p, pa + ab * t);
                        float k = Mathf.Clamp01(1f - d / (w + 1f));
                        if (k <= 0f) continue;

                        int i = Index(x, y);
                        Height[i] -= depth * k * k;
                        Albedo[i] = Color.Lerp(Albedo[i], colour, Mathf.Clamp01(w + 0.5f - d) * opacity);
                    }
                }
            }

            /// <summary>Scoops a shallow bowl out of the surface - a chipped corner, a hollow - its floor coloured.</summary>
            public void Dent(float u, float v, float radius, float keep, Color colour, float opacity)
            {
                float cx = u * PpmU, cy = v * PpmV, px = radius * PpmU, py = radius * PpmV;
                int reach = Mathf.CeilToInt(Mathf.Max(px, py)) + 1;

                for (int y = Mathf.FloorToInt(cy) - reach; y <= Mathf.CeilToInt(cy) + reach; y++)
                {
                    if (!Inside(y)) continue;

                    for (int x = Mathf.FloorToInt(cx) - reach; x <= Mathf.CeilToInt(cx) + reach; x++)
                    {
                        float nx = (x + 0.5f - cx) / px, ny = (y + 0.5f - cy) / py;
                        float d = Mathf.Sqrt(nx * nx + ny * ny);
                        if (d >= 1f) continue;

                        int i = Index(x, y);
                        float bowl = 1f - d * d;
                        Height[i] *= Mathf.Lerp(1f, keep, bowl);
                        Albedo[i] = Color.Lerp(Albedo[i], colour, Mathf.Clamp01((1f - d) * Mathf.Min(px, py)) * opacity);
                    }
                }
            }

            /// <summary>Colour only, in a soft-edged oval: a stain, a scuff, a worn patch. The height is left alone.</summary>
            public void Stain(float u, float v, float rx, float ry, float angle, Color colour, float opacity, float softness = 0.5f)
            {
                float cx = u * PpmU, cy = v * PpmV, px = rx * PpmU, py = ry * PpmV;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                int reach = Mathf.CeilToInt(Mathf.Max(px, py)) + 1;

                for (int y = Mathf.FloorToInt(cy) - reach; y <= Mathf.CeilToInt(cy) + reach; y++)
                {
                    if (!Inside(y)) continue;

                    for (int x = Mathf.FloorToInt(cx) - reach; x <= Mathf.CeilToInt(cx) + reach; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        float nx = (dx * cos + dy * sin) / px, ny = (dy * cos - dx * sin) / py;
                        float d = Mathf.Sqrt(nx * nx + ny * ny);
                        if (d >= 1f) continue;

                        float a = Band(1f - d, softness * 0.5f, softness * 0.5f) * opacity;
                        int i = Index(x, y);
                        Albedo[i] = Color.Lerp(Albedo[i], colour, a);
                    }
                }
            }

            /// <summary>
            /// A flat-topped slab - a wall panel, a board - over <paramref name="u0"/>..<paramref name="u1"/>
            /// by <paramref name="v0"/>..<paramref name="v1"/>, rising <paramref name="height"/> above
            /// <paramref name="standOn"/> over a rounded edge <paramref name="bevel"/> wide, its corners
            /// rounded by <paramref name="corner"/>. Drawn only where it stands taller than what is there,
            /// so what it covers shows round its rim as a joint.
            /// </summary>
            public void Slab(float u0, float v0, float u1, float v1, Color colour, float bevel, float height, float corner,
                             float standOn = 0f)
            {
                float cu = (u0 + u1) * 0.5f, cv = (v0 + v1) * 0.5f;
                float hu = (u1 - u0) * 0.5f, hv = (v1 - v0) * 0.5f;
                float ppm = (PpmU + PpmV) * 0.5f;

                for (int y = Mathf.FloorToInt(v0 * PpmV) - 1; y <= Mathf.CeilToInt(v1 * PpmV); y++)
                {
                    if (!Inside(y)) continue;
                    float v = (y + 0.5f) / PpmV;

                    for (int x = Mathf.FloorToInt(u0 * PpmU) - 1; x <= Mathf.CeilToInt(u1 * PpmU); x++)
                    {
                        float u = (x + 0.5f) / PpmU;
                        var q = new Vector2(Mathf.Abs(u - cu) - (hu - corner), Mathf.Abs(v - cv) - (hv - corner));
                        float inside = corner - (Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f));
                        if (inside <= -1f / ppm) continue;

                        float t = Mathf.Clamp01(inside / bevel);
                        float profile = Mathf.Sqrt(1f - (1f - t) * (1f - t));
                        Put(Index(x, y), standOn + height * profile, colour, Mathf.Clamp01(inside * ppm + 0.5f), 0f);
                    }
                }
            }

            /// <summary>A <see cref="Slab"/> running the whole width of the surface, rounded along its top and bottom edges only: a footing, a cap, a batten.</summary>
            public void Course(float v0, float v1, Color colour, float bevel, float height, float standOn = 0f)
            {
                for (int y = Mathf.FloorToInt(v0 * PpmV) - 1; y <= Mathf.CeilToInt(v1 * PpmV); y++)
                {
                    if (!Inside(y)) continue;

                    float v = (y + 0.5f) / PpmV;
                    float inside = Mathf.Min(v - v0, v1 - v);
                    if (inside <= -1f / PpmV) continue;

                    float t = Mathf.Clamp01(inside / bevel);
                    float h = standOn + height * Mathf.Sqrt(1f - (1f - t) * (1f - t));
                    float coverage = Mathf.Clamp01(inside * PpmV + 0.5f);
                    for (int x = 0; x < W; x++) Put(Index(x, y), h, colour, coverage, 0f);
                }
            }

            /// <summary>
            /// Colour only, across the whole width between <paramref name="v0"/> and <paramref name="v1"/>:
            /// <paramref name="amount"/> of <paramref name="colour"/> at the top, easing to none at the
            /// bottom - or the other way up, with <paramref name="fadeUp"/>. Grime rising off the ground,
            /// the shade under a ledge.
            /// </summary>
            public void ShadeBand(float v0, float v1, Color colour, float amount, bool fadeUp = false)
            {
                for (int y = Mathf.FloorToInt(v0 * PpmV); y <= Mathf.CeilToInt(v1 * PpmV); y++)
                {
                    if (!Inside(y)) continue;

                    float t = Mathf.Clamp01(((y + 0.5f) / PpmV - v0) / (v1 - v0));
                    if (fadeUp) t = 1f - t;
                    float a = t * t * (3f - 2f * t) * amount;

                    for (int x = 0; x < W; x++)
                    {
                        int i = Index(x, y);
                        Albedo[i] = Color.Lerp(Albedo[i], colour, a);
                    }
                }
            }

            /// <summary>
            /// The shading a painter adds by eye: where the surface sits below what is round it -
            /// the gaps between clumps, the grout, a crack - it goes toward <paramref name="low"/>;
            /// where it stands above, toward <paramref name="high"/>. <paramref name="radius"/> is how
            /// far "round it" reaches and <paramref name="depth"/> the drop that counts as all the way.
            /// </summary>
            public void Occlude(float radius, float depth, Color low, float lowAmount, Color high, float highAmount)
            {
                float[] around = Blur(Height, Mathf.Max(1, Mathf.RoundToInt(radius * PpmU)), Mathf.Max(1, Mathf.RoundToInt(radius * PpmV)));

                for (int i = 0; i < Albedo.Length; i++)
                {
                    float d = (Height[i] - around[i]) / depth;
                    Color c = Albedo[i];
                    c = d < 0f ? Color.Lerp(c, low, Mathf.Clamp01(-d) * lowAmount) : Color.Lerp(c, high, Mathf.Clamp01(d) * highAmount);
                    Albedo[i] = new Color(c.r, c.g, c.b, 1f);
                }
            }

            /// <summary>Tangent-space normals from the height, slopes multiplied by <paramref name="strength"/>.</summary>
            public Color[] NormalMap(float strength)
            {
                var pixels = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        Vector3 n = NormalAt(x, y, strength);
                        pixels[y * W + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    }
                }

                return pixels;
            }

            Vector3 NormalAt(int x, int y, float strength)
            {
                float du = (Height[Index(x + 1, y)] - Height[Index(x - 1, y)]) * PpmU * 0.5f;
                float dv = (Height[Index(x, y + 1)] - Height[Index(x, y - 1)]) * PpmV * 0.5f;
                return new Vector3(-du * strength, -dv * strength, 1f).normalized;
            }

            void Put(int i, float height, Color colour, float coverage, float soft)
            {
                if (coverage <= 0f || height <= Height[i]) return;

                float rise = height - Height[i];
                Height[i] = Mathf.Lerp(Height[i], height, coverage);
                if (soft > 0f) coverage *= Mathf.Clamp01(rise / soft);

                Color o = Albedo[i];
                Albedo[i] = new Color(o.r + (colour.r - o.r) * coverage, o.g + (colour.g - o.g) * coverage,
                                      o.b + (colour.b - o.b) * coverage, 1f);
            }

            /// <summary>A box blur, wrapping as the surface does.</summary>
            float[] Blur(float[] source, int rx, int ry)
            {
                var across = new float[source.Length];
                var result = new float[source.Length];

                float invX = 1f / (2 * rx + 1);
                for (int y = 0; y < H; y++)
                {
                    float sum = 0f;
                    for (int k = -rx; k <= rx; k++) sum += source[y * W + Wrap(k, W)];

                    for (int x = 0; x < W; x++)
                    {
                        across[y * W + x] = sum * invX;
                        sum += source[y * W + Wrap(x + rx + 1, W)] - source[y * W + Wrap(x - rx, W)];
                    }
                }

                float invY = 1f / (2 * ry + 1);
                for (int x = 0; x < W; x++)
                {
                    float sum = 0f;
                    for (int k = -ry; k <= ry; k++) sum += across[Index(x, k)];

                    for (int y = 0; y < H; y++)
                    {
                        result[y * W + x] = sum * invY;
                        sum += across[Index(x, y + ry + 1)] - across[Index(x, y - ry)];
                    }
                }

                return result;
            }
        }
    }
}
