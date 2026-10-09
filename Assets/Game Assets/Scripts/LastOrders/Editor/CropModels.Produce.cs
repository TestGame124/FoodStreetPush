using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// What each crop gives at harvest, modelled for its item icon (<see cref="CropIcons"/>): a ripe
    /// ear of corn with its husk peeled back, a pair of glossy tomatoes, a cabbage head cupped in its
    /// outer leaves and a tied sack of flour with a sheaf of wheat against it.
    ///
    /// Built at the plants' own scale with their parts and paints, so the icons match the crops in the
    /// pots; the icon renderer scales each to fill its frame. Each stands on the origin, +Y up, its
    /// best side toward -Z, where the icon camera looks from.
    /// </summary>
    public static partial class CropModels
    {
        /// <summary>Kernels round the icon's cob and along it; the bake shader's grooves use the same grid.</summary>
        const float ProduceKernelColumns = 14f, ProduceKernelRows = 17f;

        /// <summary>The harvested produce of <paramref name="crop"/>.</summary>
        public static SmoothMesh Produce(Crop crop)
        {
            var mesh = new SmoothMesh((int)Paint.Count);
            var dice = new Dice(65537 * ((int)crop + 3));

            switch (crop)
            {
                case Crop.Wheat: FlourSack(mesh, dice); break;
                case Crop.Corn: CornCob(mesh, dice); break;
                case Crop.Tomato: Tomatoes(mesh, dice); break;
                case Crop.Cabbage: CabbageHead(mesh, dice); break;
            }

            return mesh;
        }

        // --- Corn ------------------------------------------------------------------------------------

        /// <summary>
        /// One fat ear leaning to the right, every kernel modelled as a plump bump in staggered rows, the
        /// husk peeled back from its foot and hanging open in four leaves, and the cut stalk below.
        /// </summary>
        static void CornCob(SmoothMesh m, Dice d)
        {
            const float Length = 0.2f, Radius = 0.042f;
            Vector3 axis = Direction(90f, 58f);
            // The seam of the wrap at the back, out of sight.
            Vector3 across = Vector3.forward;
            Vector3 over = Vector3.Cross(axis, across).normalized;
            across = Vector3.Cross(over, axis).normalized;
            Vector3 Around(float angle) => across * Mathf.Cos(angle) + over * Mathf.Sin(angle);

            m.Surface((int)Paint.CobBig, 84, 102, (u, v) =>
            {
                float r = Radius * Mathf.Pow(Arch(Mathf.PI * Mathf.Lerp(0.02f, 1f, v)), 0.28f) * (1f - 0.28f * Mathf.Pow(v, 1.5f));
                float ends = Mathf.SmoothStep(0f, 1f, v / 0.06f) * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.08f);
                r *= 1f + 0.09f * Kernel(u, v) * ends;
                return axis * (v * Length) + Around(u * Mathf.PI * 2f) * r;
            }, wrapU: true);

            // The husk: up the cob a little, then out and down past its foot, like a peeled banana.
            const int husks = 4;
            for (int k = 0; k < husks; k++)
            {
                float centre = (k + 0.5f) * Mathf.PI * 2f / husks + d.Range(-0.3f, 0.3f);
                float reach = d.Range(0.92f, 1.12f);
                m.Surface((int)Paint.Husk, 8, 18, (u, v) =>
                {
                    float x = u * 2f - 1f;
                    float along = Length * reach * (0.3f * Mathf.Sin(v * Mathf.PI) - 0.36f * v * v);
                    float r = Radius * (1.06f + 1.9f * reach * Mathf.Pow(Mathf.SmoothStep(0f, 1f, (v - 0.12f) / 0.88f), 1.3f));
                    float width = Radius * 1.4f * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(v, 2.2f)), 0.6f);
                    float a = centre + x * width / r;
                    // Cupped: the edges curl in toward the cob.
                    r -= Radius * 0.35f * x * x * Mathf.Sin(v * Mathf.PI);
                    return axis * along + Around(a) * r;
                });
            }

            // The end of the stalk it was snapped from.
            m.Tube((int)Paint.CornStalk, -axis * 0.04f, axis * 0.012f, Radius * 0.5f, Radius * 0.62f, 12);
        }

        /// <summary>
        /// How far a kernel stands out at (<paramref name="u"/> round, <paramref name="v"/> along): 1 on
        /// a kernel's crown, 0 in the grooves, every other row shifted half a kernel as the shader does.
        /// </summary>
        static float Kernel(float u, float v)
        {
            float gy = v * ProduceKernelRows;
            float gx = u * ProduceKernelColumns + 0.5f * (Mathf.Floor(gy) % 2f);
            float cx = Mathf.Abs(gx - Mathf.Floor(gx) - 0.5f) * 2f;
            float cy = Mathf.Abs(gy - Mathf.Floor(gy) - 0.5f) * 2f;

            // A rounded square: kernels pack tight, so they are plump rather than round.
            float e = Mathf.Pow(cx * cx * cx + cy * cy * cy, 1f / 3f);
            return 1f - Mathf.SmoothStep(0f, 1f, (e - 0.5f) / 0.5f);
        }

        // --- Tomato ----------------------------------------------------------------------------------

        /// <summary>A big tomato in front and a smaller one behind it to the left, each with its green calyx.</summary>
        static void Tomatoes(SmoothMesh m, Dice d)
        {
            IconTomato(m, d, new Vector3(-0.05f, 0.036f, 0.05f), 0.038f, 20f);
            IconTomato(m, d, new Vector3(0.012f, 0.042f, -0.006f), 0.05f, 50f);
        }

        static void IconTomato(SmoothMesh m, Dice d, Vector3 centre, float r, float turn)
        {
            const float Squash = 0.84f;
            m.Surface((int)Paint.TomatoRed, 28, 20, (u, v) =>
            {
                float th = v * Mathf.PI, ph = u * Mathf.PI * 2f;
                float lobe = 1f + 0.05f * Mathf.Cos(ph * 5f + turn * Mathf.Deg2Rad) * Mathf.Pow(Arch(th), 2f);
                float dimple = 0.16f * Mathf.Pow(Mathf.Max(0f, v - 0.8f) / 0.2f, 2f);
                var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * lobe * 1.04f, -Mathf.Cos(th) * Squash - dimple,
                                    Mathf.Sin(th) * Mathf.Sin(ph) * lobe * 1.04f);
                return centre + p * r;
            }, wrapU: true);

            // The calyx: a star of pointed sepals lying over the shoulders, and the stalk standing up from it.
            Vector3 top = centre + Vector3.up * r * 0.72f;
            for (int k = 0; k < 5; k++)
                Leaf(m, Paint.TomatoStem, Spine.Arc(top, turn + k * 72f + d.Range(-10f, 10f), r * d.Range(0.68f, 0.8f), 28f, -38f),
                     r * 0.2f, Narrow, 0.2f);

            Stem(m, Paint.TomatoStem, Spine.Arc(top + Vector3.down * r * 0.04f, turn + 200f, r * 0.42f, 84f, 40f), r * 0.09f, r * 0.07f, 8, 6);
        }

        // --- Cabbage ---------------------------------------------------------------------------------

        /// <summary>A round firm head, its pale leaves wrapped tight, cupped in darker outer leaves whose tops flare out and ripple.</summary>
        static void CabbageHead(SmoothMesh m, Dice d)
        {
            var centre = new Vector3(0f, 0.085f, 0f);
            var radii = new Vector3(0.09f, 0.082f, 0.09f);
            Vector3 On(float th, float ph, float s) =>
                centre + Vector3.Scale(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), -Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)), radii) * s;

            Blob(m, Paint.CabbageCore, centre, radii, Quaternion.identity, 24, 16);

            // The heart's leaves wrapped over it, each lying a little proud of the one before so they
            // never cut through each other, their edges lifting just enough to catch the light.
            const int wraps = 5;
            for (int k = 0; k < wraps; k++)
            {
                float middle = (k * 137.5f + 40f) * Mathf.Deg2Rad;
                float half = d.Range(0.95f, 1.15f);
                float end = d.Range(0.86f, 0.95f) * Mathf.PI;
                float lift = 1.02f + 0.03f * k;

                m.Surface((int)Paint.CabbageHead, 12, 16, (u, v) =>
                {
                    float x = u * 2f - 1f;
                    float th = Mathf.Lerp(0.25f * Mathf.PI, end, v);
                    float taper = Mathf.Pow(Arch(Mathf.PI * Mathf.Lerp(0.15f, 1f, v)), 0.5f);
                    return On(th, middle + x * half * taper, lift + 0.018f * x * x * v);
                });
            }

            // Broad outer leaves cupping it from below, overlapping, their tops flaring out in a rippled edge.
            const int leaves = 5;
            for (int k = 0; k < leaves; k++)
            {
                float middle = (k * 360f / leaves + 20f + d.Range(-8f, 8f)) * Mathf.Deg2Rad;
                float half = d.Range(0.8f, 0.92f);
                // Low at the front, so the head shows; taller behind, framing it.
                float behind = 0.5f + 0.5f * Mathf.Sin(middle);
                float top = (Mathf.Lerp(0.47f, 0.68f, behind) + d.Range(-0.02f, 0.02f)) * Mathf.PI;
                float flare = Mathf.Lerp(0.16f, 0.26f, behind) + d.Range(-0.03f, 0.03f);
                float lift = 1.1f + 0.016f * k;
                float ripple = d.Range(0f, 6f);

                m.Surface((int)(k % 2 == 0 ? Paint.CabbageOuter : Paint.CabbageInner), 16, 18, (u, v) =>
                {
                    float x = u * 2f - 1f;
                    float th = Mathf.Lerp(0.04f * Mathf.PI, top, v);
                    float taper = Mathf.Lerp(0.45f, 1f, Mathf.SmoothStep(0f, 1f, v / 0.5f)) *
                                  (0.6f + 0.4f * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v * v)));
                    float s = lift + flare * Mathf.Pow(Mathf.SmoothStep(0f, 1f, (v - 0.45f) / 0.55f), 1.6f)
                              + 0.06f * x * x * v
                              + 0.05f * Mathf.Sin(x * Mathf.PI * 3f + ripple) * v * v * v
                              // The midrib, standing proud.
                              + 0.014f * Mathf.Exp(-x * x / 0.006f) * (1f - 0.5f * v);
                    return On(th, middle + x * half * taper, s);
                });
            }
        }

        // --- Flour -----------------------------------------------------------------------------------

        /// <summary>
        /// A plump open sack of flour: woven sacking creased toward the top, its mouth rolled down into a
        /// thick lumpy cuff round a heap of flour, and three stalks of ripe wheat leaning out at its side.
        /// </summary>
        static void FlourSack(SmoothMesh m, Dice d)
        {
            const float R = 0.13f, H = 0.27f;

            // (radius, height) up the sack, as fractions of R and H: flat bottom, bulging belly, a waist, the mouth.
            Vector2[] profile =
            {
                new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(0.94f, 0.06f), new Vector2(1.04f, 0.25f),
                new Vector2(1f, 0.47f), new Vector2(0.9f, 0.66f), new Vector2(0.84f, 0.82f), new Vector2(0.86f, 0.93f),
                new Vector2(0.84f, 1f),
            };

            m.Surface((int)Paint.Burlap, 56, 40, (u, v) =>
            {
                Vector2 p = Through(profile, v);
                float a = u * Mathf.PI * 2f;
                // Creases running down from the cuff, fading out over the belly.
                float creases = 1f + 0.045f * Mathf.SmoothStep(0f, 1f, (v - 0.45f) / 0.55f) * Mathf.Sin(a * 7f + 0.8f * Mathf.Sin(a * 3f));
                float lumps = 1f + 0.035f * Mathf.Sin(a * 3f + 1.3f) * Arch(v * Mathf.PI) + 0.02f * Mathf.Sin(a * 5f + v * 9f);
                float r = p.x * R * creases * lumps;
                return new Vector3(Mathf.Cos(a) * r, p.y * H, Mathf.Sin(a) * r * 0.94f);
            }, wrapU: true);

            // The rolled-down cuff: a fat, lumpy roll of the same sacking, a shade darker.
            m.Surface((int)Paint.BurlapDark, 12, 56, (u, v) =>
            {
                float a = v * Mathf.PI * 2f, b = u * Mathf.PI * 2f;
                float major = R * 0.9f * (1f + 0.035f * Mathf.Sin(a * 5f + 1f));
                float minor = R * 0.2f * (1f + 0.22f * Mathf.Sin(a * 7f + 0.5f) + 0.1f * Mathf.Sin(a * 3f));
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a) * 0.94f);
                return new Vector3(0f, H * 0.97f, 0f) + outward * (major + minor * Mathf.Cos(b)) + Vector3.up * (minor * 0.8f * Mathf.Sin(b));
            }, wrapU: true);

            // The flour, heaped up out of the mouth.
            m.Surface((int)Paint.Flour, 40, 12, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float r = R * 0.86f * (1f - v) * (1f + 0.05f * Mathf.Sin(a * 4f + 2f) * (1f - v));
                float y = H * 0.98f + R * 0.5f * (1f - Mathf.Pow(1f - v, 2.2f));
                return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r * 0.94f);
            }, wrapU: true);

            // Wheat leaning out at the sack's front right, the ears clear of its outline.
            var foot = new Vector3(0.02f, 0f, -0.14f);
            for (int i = 0; i < 3; i++)
            {
                float yaw = 68f + i * 14f + d.Range(-3f, 3f);
                float rise = 66f - i * 7f;
                Vector3 at = foot + new Vector3(i * 0.008f, 0f, i * 0.006f);
                Spine stem = Spine.Arc(at, yaw, d.Range(0.24f, 0.27f) - i * 0.015f, rise, rise - 6f);
                Stem(m, Paint.WheatGold, stem, 0.0055f, 0.0045f, 6, 10);
                Spine ear = Spine.Arc(stem.End, yaw, 0.1f, rise - 6f, rise - 30f);
                Stem(m, Paint.WheatGold, ear, 0.0045f, 0.0025f, 6, 6);
                Ear(m, Paint.GrainGold, Paint.WheatGold, ear, 1.7f, false);
            }
        }

        /// <summary>The point <paramref name="v"/> of the way along a smooth curve through <paramref name="points"/>.</summary>
        static Vector2 Through(Vector2[] points, float v)
        {
            int last = points.Length - 1;
            float f = Mathf.Clamp01(v) * last;
            int i = Mathf.Min((int)f, last - 1);
            float k = f - i, k2 = k * k, k3 = k2 * k;

            Vector2 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, last)];
            return 0.5f * (2f * p1 + (p2 - p0) * k + (2f * p0 - 5f * p1 + 4f * p2 - p3) * k2 + (3f * p1 - p0 - 3f * p2 + p3) * k3);
        }
    }
}
