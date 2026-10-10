using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The griddle's and the grill's dishes, and the ingredients they need that nothing supplies yet,
    /// modelled for their icons (<see cref="GriddleGrillContent"/>) in the style and paints of the
    /// other dishes: two fried eggs on a plate, hash browns fanned out by a dab of ketchup, French
    /// toast with butter and syrup; two grilled cobs, three chicken and onion skewers, a whole grilled
    /// fish with tomato and lemon. Potatoes, onions and fish get icons of their own the same way.
    ///
    /// Each stands on the origin, +Y up, its best side toward -Z, where the icon camera looks from.
    /// </summary>
    public static partial class CropModels
    {
        // --- Fried eggs ------------------------------------------------------------------------------

        /// <summary>Two eggs fried sunny side up, black pepper over them and a few snipped chives.</summary>
        static void FriedEggs(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);
            var eggs = new[]
            {
                (foot: new Vector3(-0.05f, well, 0.035f), r: 0.072f, key: 20f),
                (foot: new Vector3(0.055f, well + 0.0015f, -0.04f), r: 0.07f, key: 140f),
            };

            foreach (var egg in eggs) FriedEgg(m, egg.foot, egg.r, egg.key);

            for (int i = 0; i < 36; i++)
            {
                var egg = eggs[i % 2];
                float t = d.Range(0.25f, 0.85f), a = d.Range(0f, Mathf.PI * 2f);
                Vector3 at = EggPoint(egg.foot, egg.r, egg.key, a, t) + Vector3.up * 0.0008f;
                if (i < 28) Blob(m, Paint.GrillMark, at, new Vector3(0.0024f, 0.001f, 0.0024f), Quaternion.Euler(0f, d.Range(0f, 90f), 0f), 6, 4);
                else
                {
                    Vector3 along = Horizontal(d.Range(0f, 360f)) * 0.007f;
                    m.Tube((int)Paint.Basil, at - along + Vector3.up * 0.002f, at + along + Vector3.up * 0.002f, 0.0026f, 0.0026f, 6);
                }
            }
        }

        /// <summary>Where the egg's white stands, <paramref name="t"/> of the way from the yolk out to its ragged edge toward <paramref name="a"/>.</summary>
        static Vector3 EggPoint(Vector3 foot, float r, float key, float a, float t)
        {
            float k = key * Mathf.Deg2Rad;
            float edge = r * (1f + 0.1f * Mathf.Sin(a * 3f + k) + 0.06f * Mathf.Sin(a * 5f + k * 2f) + 0.03f * Mathf.Sin(a * 9f + k));
            var yolk = new Vector3(Mathf.Cos(k) * r * 0.18f, 0f, Mathf.Sin(k) * r * 0.18f);
            Vector3 rim = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * edge;
            return foot + Vector3.Lerp(yolk, rim, t) + Vector3.up * (0.003f + 0.009f * (1f - t * t));
        }

        /// <summary>
        /// A fried egg on <paramref name="foot"/>: its white spread in a ragged round, crisped brown at
        /// the lacy edge just under it, domed a little toward the yolk, which sits off its middle.
        /// </summary>
        static void FriedEgg(SmoothMesh m, Vector3 foot, float r, float key)
        {
            m.Surface((int)Paint.EggWhiteCrisp, 60, 3, (u, v) =>
            {
                float a = -u * Mathf.PI * 2f;
                Vector3 p = EggPoint(foot, r * 1.07f, key, a, v);
                return new Vector3(p.x, foot.y + 0.0015f, p.z);
            }, wrapU: true);

            m.Surface((int)Paint.EggWhite, 60, 10, (u, v) => EggPoint(foot, r, key, -u * Mathf.PI * 2f, v), wrapU: true);

            float k = key * Mathf.Deg2Rad, s = r / 0.07f;
            Vector3 yolk = foot + new Vector3(Mathf.Cos(k) * r * 0.18f, 0.0105f, Mathf.Sin(k) * r * 0.18f);
            Blob(m, Paint.Yolk, yolk, new Vector3(0.028f, 0.016f, 0.028f) * s, Quaternion.identity, 20, 12);
        }

        // --- Hash browns -----------------------------------------------------------------------------

        /// <summary>Three hash brown patties set apart round a plate, a dab of ketchup between them and parsley by it.</summary>
        static void HashBrowns(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);
            var spots = new[] { new Vector3(-0.065f, 0f, 0.045f), new Vector3(0.068f, 0f, 0.04f), new Vector3(0f, 0f, -0.06f) };
            for (int i = 0; i < spots.Length; i++)
                Patty(m, d, spots[i] + Vector3.up * (well + 0.011f), Quaternion.Euler(0f, -20f + i * 35f + d.Range(-6f, 6f), 0f));

            Blob(m, Paint.Soup, new Vector3(0.005f, well + 0.003f, 0.105f), new Vector3(0.034f, 0.012f, 0.026f), Quaternion.Euler(0f, 10f, 0f), 18, 8);
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(0.045f, well + 0.004f, 0.11f);
                Leaf(m, Paint.Basil, Spine.Arc(at, 40f + k * 50f + d.Range(-8f, 8f), 0.035f, 10f, -6f), 0.013f, Ovate, 0.3f);
            }
        }

        /// <summary>An oval patty of grated potato: golden, its sides fried a deeper brown, loose shreds crisped over its top.</summary>
        static void Patty(SmoothMesh m, Dice d, Vector3 centre, Quaternion turn)
        {
            var size = new Vector3(0.088f, 0.022f, 0.062f);
            m.RoundedBox((int)Paint.HashBrown, centre, size, 0.01f, turn, 3);
            m.RoundedBox((int)Paint.HashBrownCrisp, centre - turn * Vector3.up * 0.002f, new Vector3(size.x + 0.004f, size.y * 0.55f, size.z + 0.004f), 0.006f,
                         turn, 3);

            for (int s = 0; s < 36; s++)
            {
                float x = d.Range(-0.036f, 0.036f), z = d.Range(-0.023f, 0.023f);
                Vector3 at = centre + turn * new Vector3(x, size.y * 0.5f, z);
                Vector3 along = turn * (Quaternion.Euler(0f, d.Range(0f, 180f), 0f) * Vector3.right) * d.Range(0.01f, 0.017f);
                m.Tube(s % 3 == 0 ? (int)Paint.HashBrownCrisp : (int)Paint.HashBrown, at - along, at + along, 0.0028f, 0.0024f, 5);
            }
        }

        // --- French toast ----------------------------------------------------------------------------

        /// <summary>Two slices of French toast, the second laid across the first, a pat of butter on top, syrup pooled over it and run down onto the plate, a dusting of sugar.</summary>
        static void FrenchToast(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);
            const float Thick = 0.024f;

            ToastSlice(m, d, new Vector3(-0.035f, well + Thick * 0.5f, 0.03f), Quaternion.Euler(0f, 14f, 0f), 0.066f, Thick);

            Quaternion turn = Quaternion.Euler(0f, -30f, 0f) * Quaternion.Euler(-3f, 0f, 3f);
            var centre = new Vector3(0.035f, well + Thick * 1.5f + 0.006f, -0.035f);
            ToastSlice(m, d, centre, turn, 0.064f, Thick);

            Vector3 top = centre + turn * (Vector3.up * (Thick * 0.5f + 0.002f));
            Pool(m, Paint.Syrup, top + turn * new Vector3(0.006f, 0.0006f, -0.004f), turn, 0.042f, 1.3f, 0.0015f);
            m.RoundedBox((int)Paint.Butter, top + turn * new Vector3(-0.004f, 0.007f, 0.004f), new Vector3(0.032f, 0.012f, 0.026f), 0.004f,
                         turn * Quaternion.Euler(0f, 25f, 0f), 2);

            // Syrup run off the front of the top slice, pooled on the plate in front of it. (Drips modelled
            // running down its edge read as the legs of a stool.)
            foreach (float x in new[] { -0.012f, 0.03f })
            {
                Vector3 foot = centre + turn * new Vector3(x, 0f, -0.078f);
                foot.y = well + 0.0015f;
                Pool(m, Paint.Syrup, foot, turn, 0.024f, x * 40f, 0.0025f);
            }

            for (int i = 0; i < 40; i++)
            {
                float a = d.Range(0f, Mathf.PI * 2f), r = 0.055f * Mathf.Sqrt(d.Value);
                Vector3 at = top + turn * new Vector3(Mathf.Cos(a) * r, 0.0018f, Mathf.Sin(a) * r);
                Blob(m, Paint.Flour, at, new Vector3(0.0026f, 0.0012f, 0.0026f), Quaternion.identity, 6, 4);
            }
        }

        /// <summary>A slice of sandwich bread, squared off and domed on one side as it came off the loaf: its outline at <paramref name="a"/>, half across.</summary>
        static Vector2 SliceOutline(float a, float half)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            float x = Mathf.Sign(c) * Mathf.Sqrt(Mathf.Abs(c));
            float z = Mathf.Sign(s) * Mathf.Sqrt(Mathf.Abs(s));
            if (z > 0f) z *= 1f + 0.2f * (1f - x * x);
            return new Vector2(x, z) * half;
        }

        /// <summary>A slice of French toast lying flat at <paramref name="centre"/>: both faces fried golden with browner spots, the crust round its edge.</summary>
        static void ToastSlice(SmoothMesh m, Dice d, Vector3 centre, Quaternion turn, float half, float thick)
        {
            float h = thick * 0.5f;
            Vector3 At(float a, float r, float y)
            {
                Vector2 o = SliceOutline(a, half) * r;
                return centre + turn * new Vector3(o.x, y, o.y);
            }

            m.Surface((int)Paint.FrenchToast, 56, 6, (u, v) => At(-u * Mathf.PI * 2f, v, h + 0.003f * (1f - v * v)), wrapU: true);
            m.Surface((int)Paint.FrenchToast, 56, 3, (u, v) => At(u * Mathf.PI * 2f, v, -h), wrapU: true);
            m.Surface((int)Paint.BreadCrust, 56, 4, (u, v) => At(u * Mathf.PI * 2f, 1f + 0.05f * Mathf.Sin(Mathf.PI * v), Mathf.Lerp(-h, h, v)), wrapU: true);

            for (int i = 0; i < 9; i++)
            {
                float a = d.Range(0f, Mathf.PI * 2f), r = d.Range(0.1f, 0.75f);
                Vector3 at = At(a, r, h + 0.003f * (1f - r * r) + 0.0004f);
                Blob(m, Paint.HashBrownCrisp, at, new Vector3(d.Range(0.006f, 0.011f), 0.0008f, d.Range(0.005f, 0.009f)),
                     turn * Quaternion.Euler(0f, d.Range(0f, 180f), 0f), 10, 4);
            }
        }

        /// <summary>A puddle of <paramref name="paint"/> lying on <paramref name="centre"/>, ragged round its edge, a little domed.</summary>
        static void Pool(SmoothMesh m, Paint paint, Vector3 centre, Quaternion turn, float r, float key, float dome)
        {
            m.Surface((int)paint, 40, 4, (u, v) =>
            {
                float a = -u * Mathf.PI * 2f;
                float rr = r * v * (1f + 0.16f * Mathf.Sin(a * 3f + key) + 0.08f * Mathf.Sin(a * 5f + key * 1.7f));
                return centre + turn * new Vector3(Mathf.Cos(a) * rr, dome * (1f - v * v), Mathf.Sin(a) * rr);
            }, wrapU: true);
        }

        // --- Grilled corn ----------------------------------------------------------------------------

        /// <summary>Two husked cobs off the grill on a plate, charred in bars across them, on their own peeled husks, butter melting on the front one.</summary>
        static void GrilledCorn(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);

            for (int k = 0; k < 4; k++)
            {
                Vector3 at = new Vector3(-0.1f + k * 0.035f, well + 0.002f, 0.07f);
                Leaf(m, Paint.Husk, Spine.Arc(at, 70f + k * 16f + d.Range(-6f, 6f), d.Range(0.15f, 0.18f), 4f, -2f), 0.022f, Narrow, 0.35f);
            }

            GrilledCob(m, d, new Vector3(-0.01f, well + 0.004f, 0.04f), 97f);
            Vector3 front = GrilledCob(m, d, new Vector3(0.01f, well, -0.045f), 80f);

            Blob(m, Paint.Butter, front + Vector3.up * 0.004f, new Vector3(0.022f, 0.008f, 0.017f), Quaternion.Euler(0f, 30f, 0f), 14, 8);
            Pool(m, Paint.Butter, front + Vector3.up * 0.0005f + Vector3.back * 0.006f, Quaternion.identity, 0.026f, 2f, 0.002f);
        }

        /// <summary>
        /// A husked cob lying on <paramref name="foot"/> along <paramref name="yaw"/>, kernels modelled one
        /// by one as on the corn's own icon, the grill's bars charred across its top and down its front;
        /// the stub of its stalk at one end. Returns the top of its middle.
        /// </summary>
        static Vector3 GrilledCob(SmoothMesh m, Dice d, Vector3 foot, float yaw)
        {
            const float Length = 0.2f, Radius = 0.036f;
            Vector3 axis = Horizontal(yaw);
            Vector3 across = Vector3.up;
            Vector3 over = Vector3.Cross(axis, across).normalized;
            Vector3 butt = foot + Vector3.up * Radius - axis * (Length * 0.5f);
            Vector3 Around(float a) => across * Mathf.Cos(a) + over * Mathf.Sin(a);
            float Girth(float v) => Radius * Mathf.Pow(Arch(Mathf.PI * Mathf.Lerp(0.02f, 1f, v)), 0.28f) * (1f - 0.28f * Mathf.Pow(v, 1.5f));

            m.Surface((int)Paint.CobGrilled, 84, 102, (u, v) =>
            {
                float ends = Mathf.SmoothStep(0f, 1f, v / 0.06f) * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.08f);
                float r = Girth(v) * (1f + 0.09f * Kernel(u, v) * ends);
                return butt + axis * (v * Length) + Around(u * Mathf.PI * 2f) * r;
            }, wrapU: true);

            // The char: bars across the top, reaching down the side toward the camera.
            Vector3 toward = Vector3.Dot(over, Vector3.back) > 0f ? over : -over;
            float lean = Vector3.Dot(toward, over) > 0f ? 1f : -1f;
            foreach (float t in new[] { 0.16f, 0.32f, 0.48f, 0.64f, 0.8f })
            {
                float w = d.Range(0.009f, 0.013f);
                m.Surface((int)Paint.GrillMark, 12, 2, (u, v) =>
                {
                    float a = Mathf.Lerp(-0.5f, 1.3f, u) * lean;
                    float along = t + (v - 0.5f) * w / Length;
                    return butt + axis * (along * Length) + Around(a) * (Girth(along) * 1.075f);
                });
            }

            m.Tube((int)Paint.CornStalk, butt - axis * 0.03f, butt + axis * 0.01f, Radius * 0.5f, Radius * 0.6f, 12);
            return butt + axis * (Length * 0.5f) + Vector3.up * (Girth(0.5f) * 1.06f);
        }

        // --- Chicken skewers -------------------------------------------------------------------------

        /// <summary>Three skewers laid side by side across a plate: chunks of chicken with onion between, charred in bars, parsley scattered by them.</summary>
        static void ChickenSkewers(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.17f);
            Vector3 axis = Horizontal(72f);
            Vector3 across = Vector3.Cross(Vector3.up, axis).normalized;

            for (int s = 0; s < 3; s++)
            {
                Vector3 mid = new Vector3(0f, well + 0.018f, 0f) + across * ((s - 1) * 0.06f) + axis * (0.012f * (s - 1));
                Skewer(m, d, mid, axis);
            }

            for (int k = 0; k < 4; k++)
            {
                Vector3 at = new Vector3(d.Range(-0.1f, 0.1f), well + 0.004f, d.Range(0.07f, 0.1f));
                Leaf(m, Paint.Basil, Spine.Arc(at, d.Range(0f, 360f), 0.03f, 10f, -6f), 0.012f, Ovate, 0.3f);
            }
        }

        static void Skewer(SmoothMesh m, Dice d, Vector3 mid, Vector3 axis)
        {
            m.Tube((int)Paint.Skewer, mid - axis * 0.15f, mid + axis * 0.15f, 0.0042f, 0.0034f, 8);
            m.Tube((int)Paint.Skewer, mid + axis * 0.15f, mid + axis * 0.172f, 0.0034f, 0.0006f, 8);

            float[] along = { -0.095f, -0.052f, -0.01f, 0.032f, 0.075f };
            for (int i = 0; i < along.Length; i++)
            {
                Vector3 c = mid + axis * along[i];
                Quaternion turn = Quaternion.LookRotation(axis) * Quaternion.Euler(0f, 0f, d.Range(-18f, 18f));
                if (i % 2 == 0) ChickenChunk(m, d, c, turn);
                else OnionPiece(m, c, turn);
            }
        }

        /// <summary>A chunk of chicken threaded on the stick: browned all round, the grill's bars across its top.</summary>
        static void ChickenChunk(SmoothMesh m, Dice d, Vector3 c, Quaternion turn)
        {
            var size = new Vector3(d.Range(0.034f, 0.039f), d.Range(0.03f, 0.034f), 0.034f);
            m.RoundedBox((int)Paint.Chicken, c, size, 0.011f, turn, 3);
            foreach (float z in new[] { -0.009f, 0.003f })
                m.RoundedBox((int)Paint.GrillMark, c + turn * new Vector3(0f, size.y * 0.5f - 0.0006f, z), new Vector3(size.x * 0.82f, 0.0026f, 0.0045f), 0.001f,
                             turn * Quaternion.Euler(0f, 18f, 0f), 2);
        }

        /// <summary>A piece of quartered onion on the stick: three layers edge up, the middle one paler, the top edges charred.</summary>
        static void OnionPiece(SmoothMesh m, Vector3 c, Quaternion turn)
        {
            for (int l = -1; l <= 1; l++)
            {
                float inner = 1f - Mathf.Abs(l);
                m.RoundedBox(l == 0 ? (int)Paint.Onion : (int)Paint.OnionGrilled, c + turn * new Vector3(0f, -0.002f + 0.002f * inner, l * 0.0072f),
                             new Vector3(0.04f - 0.005f * (1f - inner), 0.03f, 0.0065f), 0.003f, turn, 2);
            }

            m.RoundedBox((int)Paint.GrillMark, c + turn * new Vector3(0f, 0.0142f, 0f), new Vector3(0.026f, 0.003f, 0.007f), 0.0012f, turn, 2);
        }

        // --- Grilled fish ----------------------------------------------------------------------------

        /// <summary>A whole fish grilled golden, the bars across its flank, two slices of tomato and a wedge of lemon beside it, a sprig of herbs.</summary>
        static void GrilledFish(SmoothMesh m, Dice d)
        {
            float well = Plate(m, 0.19f);
            Fish(m, Paint.FishGrilled, new Vector3(-0.015f, well, 0.025f), Quaternion.Euler(0f, -12f, 0f), 0.29f, 0.058f, 0.024f, true);

            TomatoSlice(m, new Vector3(0.05f, well + 0.002f, -0.1f), 0.032f, 0.011f, Quaternion.Euler(0f, 30f, 0f));
            TomatoSlice(m, new Vector3(0.1f, well + 0.006f, -0.08f), 0.03f, 0.011f, Quaternion.AngleAxis(-12f, Vector3.right) * Quaternion.Euler(0f, -20f, 0f));
            LemonWedge(m, new Vector3(-0.085f, well, -0.085f), 0.034f, 150f);

            for (int k = 0; k < 4; k++)
            {
                Vector3 at = new Vector3(0.12f, well + 0.004f, 0.06f);
                Leaf(m, Paint.Basil, Spine.Arc(at, 200f + k * 30f + d.Range(-8f, 8f), 0.04f, 12f, -6f), 0.012f, Narrow, 0.3f);
            }
        }

        /// <summary>
        /// A fish lying on its side on <paramref name="foot"/>, its snout toward -X of <paramref name="turn"/>
        /// and its tail toward +X: a body deep at the shoulder narrowing to the tail's wrist, its belly to
        /// the front (-Z) and its back behind; a forked tail fin, a fin along its back, a small one under
        /// its belly and one on its flank behind the gill; an eye. Grilled, the bars of the grill run
        /// slanting across its flank.
        /// </summary>
        static void Fish(SmoothMesh m, Paint skin, Vector3 foot, Quaternion turn, float length, float depth, float thick, bool grilled)
        {
            float body = length * 0.8f, tail = length * 0.22f;
            float X(float s) => (s - 0.5f) * body - tail * 0.35f;
            float D(float s) => depth * Mathf.Pow(Arch(Mathf.PI * Mathf.Lerp(0f, 0.88f, Mathf.Pow(s, 0.8f))), 0.6f) * (1f - 0.55f * s * s * s);
            float T(float s) => thick * Mathf.Pow(Arch(Mathf.PI * Mathf.Lerp(0f, 0.9f, Mathf.Pow(s, 0.85f))), 0.5f) * (1f - 0.6f * s * s);
            float lift = thick;

            Vector3 Flank(float s, float th, float grow) =>
                foot + turn * new Vector3(X(s), lift + T(s) * Mathf.Sin(th) * grow, -D(s) * Mathf.Cos(th) * grow);

            m.Surface((int)skin, 48, 16, (u, v) => Flank(u, v * Mathf.PI, 1f));
            m.Surface((int)skin, 48, 6, (u, v) => foot + turn * new Vector3(X(u), lift - T(u) * 0.9f * Mathf.Sin(v * Mathf.PI), D(u) * Mathf.Cos(v * Mathf.PI)));
            Blob(m, skin, foot + turn * new Vector3(X(1f), lift, 0f), new Vector3(0.012f, T(1f) * 0.9f, D(1f)), turn, 10, 6);

            // The tail: forked, lying flat out from the wrist, both faces.
            Vector3 wrist = foot + turn * new Vector3(X(1f) - 0.004f, lift, 0f);
            float spread = depth * 0.95f;
            Vector3 TailAt(float u, float v)
            {
                float across = u * 2f - 1f;
                float reach = tail * (1f - 0.42f * Mathf.Pow(1f - Mathf.Abs(across), 1.5f));
                return wrist + turn * new Vector3(v * reach, 0.002f * Mathf.Sin(Mathf.PI * v), across * Mathf.Lerp(D(1f) * 0.8f, spread, v));
            }

            m.Surface((int)Paint.FishFin, 12, 8, (u, v) => TailAt(u, v));
            m.Surface((int)Paint.FishFin, 12, 8, (u, v) => TailAt(1f - u, v) - Vector3.up * 0.0015f);

            // The fins along its back and under its belly, lying flat out from its edge, and the one on its flank.
            Fin(m, foot, turn, s => X(s), s => D(s), lift, 0.3f, 0.72f, depth * 0.55f, 1f);
            Fin(m, foot, turn, s => X(s), s => D(s), lift, 0.55f, 0.75f, depth * 0.35f, -1f);

            Vector3 gill = Flank(0.2f, Mathf.PI * 0.45f, 1.01f);
            Leaf(m, Paint.FishFin, Spine.Line(gill, gill + turn * new Vector3(0.045f, 0.004f, -0.012f)), 0.012f, Ovate, 0.1f);

            // The gill's line and the eye.
            for (int k = 0; k < 6; k++)
            {
                float th0 = Mathf.Lerp(0.2f, 0.8f, k / 6f) * Mathf.PI, th1 = Mathf.Lerp(0.2f, 0.8f, (k + 1) / 6f) * Mathf.PI;
                m.Tube((int)Paint.FishFin, Flank(0.17f + 0.02f * Mathf.Sin(th0), th0, 1.01f), Flank(0.17f + 0.02f * Mathf.Sin(th1), th1, 1.01f), 0.0018f, 0.0018f, 5);
            }

            Vector3 eye = Flank(0.07f, Mathf.PI * 0.62f, 1f);
            Blob(m, Paint.EggWhite, eye, new Vector3(0.0085f, 0.003f, 0.0085f), turn, 12, 6);
            Blob(m, Paint.GrillMark, eye + Vector3.up * 0.0018f, new Vector3(0.0048f, 0.0024f, 0.0048f), turn, 10, 6);

            if (!grilled) return;

            for (int b = 0; b < 5; b++)
            {
                float s0 = 0.3f + b * 0.13f;
                m.Surface((int)Paint.GrillMark, 3, 10, (u, v) =>
                {
                    float th = Mathf.Lerp(0.22f, 0.82f, v) * Mathf.PI;
                    float s = s0 + 0.09f * (v - 0.5f) + (u - 0.5f) * 0.018f;
                    return Flank(s, th, 1.04f);
                });
            }
        }

        /// <summary>A fin lying flat out from the fish's edge (<paramref name="side"/> 1 its back, -1 its belly), from <paramref name="from"/> to <paramref name="to"/> along it, <paramref name="height"/> at its tallest; both faces.</summary>
        static void Fin(SmoothMesh m, Vector3 foot, Quaternion turn, System.Func<float, float> x, System.Func<float, float> edge, float lift, float from, float to,
                        float height, float side)
        {
            Vector3 At(float u, float v)
            {
                float s = Mathf.Lerp(from, to, u);
                float h = height * Mathf.Pow(Arch(Mathf.PI * u), 0.7f) * (1f - 0.4f * u) * v;
                return foot + turn * new Vector3(x(s) + 0.01f * v, lift, side * (edge(s) * 0.92f + h));
            }

            if (side > 0f)
            {
                m.Surface((int)Paint.FishFin, 12, 4, (u, v) => At(u, v));
                m.Surface((int)Paint.FishFin, 12, 4, (u, v) => At(1f - u, v) - Vector3.up * 0.0015f);
            }
            else
            {
                m.Surface((int)Paint.FishFin, 12, 4, (u, v) => At(1f - u, v));
                m.Surface((int)Paint.FishFin, 12, 4, (u, v) => At(u, v) - Vector3.up * 0.0015f);
            }
        }

        /// <summary>A wedge of lemon lying skin down on <paramref name="foot"/>, its core along <paramref name="yaw"/>: the rind, and the flesh of its two cut faces.</summary>
        static void LemonWedge(SmoothMesh m, Vector3 foot, float r, float yaw)
        {
            const float half = 0.55f;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 25f);
            Vector3 At(float rho, float th, float ph) =>
                foot + q * new Vector3(rho * Mathf.Sin(th) * Mathf.Sin(ph), r - rho * Mathf.Sin(th) * Mathf.Cos(ph), -rho * Mathf.Cos(th) * 1.25f);

            float Th(float v) => Mathf.Lerp(0.03f, 0.97f, v) * Mathf.PI;
            m.Surface((int)Paint.Lemon, 10, 14, (u, v) => At(r, Th(v), Mathf.Lerp(-half, half, u)));
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                m.Surface((int)Paint.LemonFlesh, 5, 14, (u, v) => side < 0 ? At(r * 0.94f * u, Th(v), s * half) : At(r * 0.94f * (1f - u), Th(v), s * half));
            }
        }

        // --- The ingredients -------------------------------------------------------------------------

        /// <summary>Three potatoes, knobbly and dusty, and one cut in half in front showing its flesh.</summary>
        static void Potatoes(SmoothMesh m, Dice d)
        {
            Potato(m, d, new Vector3(-0.065f, 0f, 0.05f), new Vector3(0.068f, 0.048f, 0.05f), 30f);
            Potato(m, d, new Vector3(0.06f, 0f, 0.06f), new Vector3(0.06f, 0.045f, 0.046f), -55f);
            Potato(m, d, new Vector3(0.01f, 0f, -0.01f), new Vector3(0.064f, 0.05f, 0.05f), 100f);
            HalfPotato(m, new Vector3(-0.06f, 0f, -0.075f), new Vector3(0.055f, 0.036f, 0.042f), 15f);
        }

        static void Potato(SmoothMesh m, Dice d, Vector3 foot, Vector3 radii, float yaw)
        {
            float k1 = d.Range(0f, 6.28f), k2 = d.Range(0f, 6.28f);
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 centre = foot + Vector3.up * radii.y * 0.85f;
            Vector3 At(float u, float v)
            {
                float th = v * Mathf.PI, ph = u * Mathf.PI * 2f;
                float lump = 1f + 0.07f * Mathf.Sin(ph * 2f + k1) * Arch(th) + 0.05f * Mathf.Sin(th * 3f + ph + k2);
                float y = -Mathf.Cos(th) * radii.y * (th < Mathf.PI * 0.5f ? 0.85f : 1f);
                return centre + turn * (new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * radii.x, y, Mathf.Sin(th) * Mathf.Sin(ph) * radii.z) * lump);
            }

            m.Surface((int)Paint.PotatoSkin, 26, 16, At, wrapU: true);

            // Eyes: little dark dimples over its top.
            for (int e = 0; e < 5; e++)
            {
                Vector3 at = At(d.Value, d.Range(0.45f, 0.85f));
                Vector3 normal = (at - centre).normalized;
                Blob(m, Paint.HashBrownCrisp, at - normal * 0.0015f, new Vector3(0.005f, 0.0022f, 0.005f), Quaternion.FromToRotation(Vector3.up, normal), 8, 4);
            }
        }

        /// <summary>Half a potato lying cut face up: its skin underneath, the pale flesh on top, a little lighter at the rim.</summary>
        static void HalfPotato(SmoothMesh m, Vector3 foot, Vector3 radii, float yaw)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 centre = foot + Vector3.up * radii.y;
            m.Surface((int)Paint.PotatoSkin, 26, 8, (u, v) =>
            {
                float th = v * Mathf.PI * 0.5f, ph = u * Mathf.PI * 2f;
                return centre + turn * new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * radii.x, -Mathf.Cos(th) * radii.y, Mathf.Sin(th) * Mathf.Sin(ph) * radii.z);
            }, wrapU: true);
            m.Surface((int)Paint.PotatoFlesh, 26, 4, (u, v) =>
            {
                float a = -u * Mathf.PI * 2f;
                return centre + turn * new Vector3(Mathf.Cos(a) * radii.x * v, 0.002f * (1f - v * v), Mathf.Sin(a) * radii.z * v);
            }, wrapU: true);
        }

        /// <summary>A whole onion standing on its roots, its dry neck up, and half of one beside it showing its rings.</summary>
        static void Onions(SmoothMesh m, Dice d)
        {
            var foot = new Vector3(0.03f, 0f, 0.035f);
            const float R = 0.07f, H = 0.13f;
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0.004f), new Vector2(0.02f, 0.002f), new Vector2(0.05f, 0.012f), new Vector2(0.066f, 0.035f), new Vector2(R, 0.058f),
                new Vector2(0.064f, 0.083f), new Vector2(0.045f, 0.103f), new Vector2(0.022f, 0.118f), new Vector2(0.012f, H), new Vector2(0.008f, H + 0.02f),
                new Vector2(0.004f, H + 0.035f), new Vector2(0f, H + 0.037f),
            };
            var subs = new List<int>();
            for (int i = 0; i < profile.Count - 1; i++) subs.Add((int)Paint.OnionSkin);
            m.Lathe(foot, profile, subs, 32);

            for (int k = 0; k < 7; k++)
            {
                Vector3 root = foot + Vector3.up * 0.004f;
                Vector3 tip = root + Horizontal(k * 51f + d.Range(-10f, 10f)) * d.Range(0.018f, 0.028f) + Vector3.down * 0.002f;
                m.Tube((int)Paint.Twine, root, tip, 0.0018f, 0.0012f, 4);
            }

            // The half: skin underneath, the cut face on top in rings from the skin to the green heart.
            var half = new Vector3(-0.07f, 0f, -0.06f);
            const float r = 0.06f, h = 0.05f;
            m.Lathe(half, new List<Vector2>
            {
                new Vector2(0f, 0.003f), new Vector2(0.02f, 0.002f), new Vector2(0.045f, 0.012f), new Vector2(0.057f, 0.03f), new Vector2(r, h),
            }, new[] { (int)Paint.OnionSkin, (int)Paint.OnionSkin, (int)Paint.OnionSkin, (int)Paint.OnionSkin }, 32);

            var face = new List<Vector2> { new Vector2(r, h), new Vector2(r * 0.95f, h) };
            var faceSubs = new List<int> { (int)Paint.OnionSkin };
            float ring = r * 0.95f;
            for (int k = 0; k < 4; k++)
            {
                float next = ring - r * 0.17f;
                face.Add(new Vector2(next + r * 0.02f, h + 0.0005f));
                faceSubs.Add((int)Paint.Onion);
                face.Add(new Vector2(next, h + 0.0005f));
                faceSubs.Add((int)Paint.OnionRing);
                ring = next;
            }

            face.Add(new Vector2(0f, h + 0.001f));
            faceSubs.Add((int)Paint.OnionRing);
            m.Lathe(half, face, faceSubs, 32);
        }

        /// <summary>Two fresh fish, one in front of the other, silver along the belly and blue-grey along the back.</summary>
        static void WholeFish(SmoothMesh m, Dice d)
        {
            Fish(m, Paint.FishSkin, new Vector3(0.02f, 0f, 0.07f), Quaternion.Euler(0f, 8f, 0f), 0.25f, 0.05f, 0.02f, false);
            Fish(m, Paint.FishSkin, new Vector3(-0.01f, 0f, -0.035f), Quaternion.Euler(0f, -10f, 0f), 0.28f, 0.056f, 0.022f, false);
        }
    }
}
