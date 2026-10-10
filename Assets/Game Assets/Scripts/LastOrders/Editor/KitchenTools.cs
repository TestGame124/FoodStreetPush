using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;
using FX = LastOrders.EditorTools.PlantingEffects;
using G = LastOrders.EditorTools.GriddleStationModel;
using K = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The tools the kitchen corners work with while their station cooks - a chopping knife and the
    /// pieces it cuts, a whisk, a bread knife, a rolling pin and dough - modelled in the corners' style
    /// with their pivots where <see cref="KitchenToolMotion"/> turns them, and the helpers that set
    /// one into a corner as its own moving part.
    /// </summary>
    internal static class KitchenTools
    {
        internal static readonly Color Dough = B.Hex("A29276");
        internal static readonly Color DoughTop = B.Hex("AFA083");
        internal static readonly Color PinWood = B.Hex("B59B76");
        internal static readonly Color Flour = B.Hex("A19D93");

        // --- Models -----------------------------------------------------------------------------------

        /// <summary>
        /// A cook's knife, its origin the tip of its blade on the board: the blade standing on its edge
        /// back along -Z to a walnut handle, so lifting the handle (turning about X) rocks it on its tip.
        /// </summary>
        internal static B.Model ChopKnife()
        {
            var m = new B.Model();
            const float blade = 0.2f, deep = 0.045f;
            m.Box(G.Steel, new Vector3(0f, deep * 0.5f, -blade * 0.62f), new Vector3(0.004f, deep * 0.92f, blade * 0.76f), 0.0018f);
            m.Box(G.Steel, new Vector3(0f, deep * 0.32f, -blade * 0.16f), new Vector3(0.0035f, deep * 0.6f, blade * 0.3f), 0.0015f, Quaternion.Euler(-8f, 0f, 0f));
            m.Box(G.SteelLight, new Vector3(0f, 0.003f, -blade * 0.5f), new Vector3(0.0045f, 0.005f, blade * 0.96f), 0.0015f);
            m.Box(B.Walnut, new Vector3(0f, deep * 0.62f, -blade - 0.06f), new Vector3(0.022f, 0.026f, 0.12f), 0.009f);
            foreach (float z in new[] { -blade - 0.03f, -blade - 0.09f })
                m.Box(G.SteelLight, new Vector3(0f, deep * 0.62f, z), new Vector3(0.024f, 0.006f, 0.006f), 0.002f);
            return m;
        }

        /// <summary>A cut piece, its origin under its middle: a little rounded block of <paramref name="colour"/>, paler on its cut top.</summary>
        internal static B.Model Piece(Color colour, Color cut, Vector3 size)
        {
            var m = new B.Model();
            var shape = new SmoothMesh(1);
            shape.RoundedBox(0, Vector3.up * (size.y * 0.5f), size, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.3f, Quaternion.identity, 2);
            m.Add(shape, (p, n) => n.y > 0.7f ? cut : colour);
            return m;
        }

        /// <summary>
        /// A balloon whisk, its origin in the middle of the bowl it beats, at the egg: wire loops down
        /// into the egg, the neck leaning out over the rim toward +X and a walnut handle beyond.
        /// </summary>
        internal static B.Model Whisk()
        {
            var m = new B.Model();
            var neck = new Vector3(0.035f, 0.075f, 0f);
            var end = new Vector3(0.1f, 0.19f, 0f);
            m.Tube(G.Steel, neck, neck + (end - neck).normalized * 0.03f, 0.007f, 8);
            m.Tube((p, n) => B.Walnut, neck + (end - neck).normalized * 0.025f, end, 0.011f, 0.012f, 10);

            // Loops from the neck down through the egg and back, each turned round from the last.
            Vector3 down = (Vector3.zero - neck).normalized;
            Vector3 side = Vector3.Cross(down, Vector3.forward).normalized;
            for (int k = 0; k < 4; k++)
            {
                Vector3 across = Quaternion.AngleAxis(k * 45f, down) * side;
                const int steps = 10;
                Vector3 Loop(float t) => neck + across * (0.026f * Mathf.Sin(t * Mathf.PI * 2f)) + down * (0.045f * (1f - Mathf.Cos(t * Mathf.PI * 2f)));
                for (int s = 0; s < steps; s++) m.Tube(G.SteelLight, Loop(s / (float)steps), Loop((s + 1) / (float)steps), 0.0026f, 4);
            }

            return m;
        }

        /// <summary>A bread knife, its origin in the middle of its blade, the blade upright along X, the walnut handle toward +X, teeth along its edge.</summary>
        internal static B.Model BreadKnife()
        {
            var m = new B.Model();
            const float length = 0.22f, deep = 0.034f;
            m.Box(G.Steel, Vector3.zero, new Vector3(length, deep, 0.004f), 0.0015f);
            m.Box(G.SteelLight, new Vector3(0f, deep * 0.5f - 0.002f, 0f), new Vector3(length, 0.004f, 0.0045f), 0.0015f);
            for (int k = 0; k < 12; k++)
                m.Box(G.SteelDark, new Vector3(-length * 0.5f + 0.01f + k * 0.018f, -deep * 0.5f + 0.002f, 0f), new Vector3(0.006f, 0.004f, 0.0048f), 0.001f,
                      Quaternion.Euler(0f, 0f, 45f));
            m.Box(B.Walnut, new Vector3(length * 0.5f + 0.055f, 0.004f, 0f), new Vector3(0.11f, 0.024f, 0.02f), 0.008f);
            return m;
        }

        /// <summary>A rolling pin, its origin in its middle, lying along X: the barrel and a handle each end.</summary>
        internal static B.Model RollingPin(float radius = 0.031f)
        {
            var m = new B.Model();
            m.Tube(PinWood, new Vector3(-0.14f, 0f, 0f), new Vector3(0.14f, 0f, 0f), radius, 14);

            // A stripe of flour round the barrel, so it shows turning.
            m.Tube(Flour, new Vector3(-0.04f, 0f, 0f), new Vector3(-0.02f, 0f, 0f), radius + 0.0012f, 14);
            m.Tube(Flour, new Vector3(0.05f, 0f, 0f), new Vector3(0.065f, 0f, 0f), radius + 0.0012f, 14);
            for (int s = -1; s <= 1; s += 2)
            {
                m.Tube(PinWood * 0.92f, new Vector3(s * 0.14f, 0f, 0f), new Vector3(s * 0.205f, 0f, 0f), 0.013f, 8);
                K.Ball(m, (p, n) => PinWood * 0.92f, new Vector3(s * 0.21f, 0f, 0f), Vector3.one * 0.017f, Quaternion.identity, 8, 6);
            }

            return m;
        }

        /// <summary>A sheet of dough rolled out flat, its origin under its middle: a rounded slab, floured paler on top.</summary>
        internal static B.Model DoughSheet(float rx, float rz, float thick)
        {
            // A pillow: flat over most of it, falling away to nothing at its edge.
            var m = new B.Model();
            m.Surface((p, n) => Color.Lerp(Dough, DoughTop, K.Smooth(p.y / thick)), 32, 6, (u, v) =>
            {
                float a = -u * Mathf.PI * 2f;
                return new Vector3(Mathf.Cos(a) * rx * v, thick * (1f - Mathf.Pow(v, 6f)), Mathf.Sin(a) * rz * v);
            }, true);
            return m;
        }

        /// <summary>A ball of dough slumped on its foot, its origin there, paler on top where the flour caught it.</summary>
        internal static B.Model DoughBall(Vector3 radii)
        {
            var m = new B.Model();
            K.Ball(m, (p, n) => Color.Lerp(Dough * 0.92f, DoughTop, K.Smooth(p.y / (radii.y * 2f))), Vector3.up * radii.y, radii, Quaternion.identity, 18, 9);
            return m;
        }

        // --- Setting them in --------------------------------------------------------------------------

        /// <summary>A tool as its own part of the corner, turned by <paramref name="turn"/>.</summary>
        internal static Transform Tool(Transform corner, string name, Mesh mesh, Vector3 at, Quaternion turn, Material material)
        {
            Transform part = G.Part(corner, name, mesh, at, material, ShadowCastingMode.On);
            part.localRotation = turn;
            return part;
        }

        /// <summary>Pieces for a knife to cut, switched off until they fly.</summary>
        internal static Transform[] Pieces(Transform corner, string prefix, Mesh mesh, int count, Vector3 at, Material material)
        {
            var pieces = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                pieces[i] = G.Part(corner, $"{prefix}_{i}", mesh, at, material, ShadowCastingMode.Off);
                pieces[i].gameObject.SetActive(false);
            }

            return pieces;
        }

        /// <summary>Little soft puffs - flour off a pin, a breath of steam - given out a couple at a time by the tool, never on their own.</summary>
        internal static ParticleSystem Puff(Transform corner, string name, Material material, Vector3 at, Color tint, Vector2 size)
        {
            ParticleSystem ps = FX.NewSystem(corner, name, material, ParticleSystemRenderMode.Billboard);
            ps.transform.localPosition = at;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = FX.Turn();
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(tint.r, tint.g, tint.b, 0.55f), new Color(tint.r, tint.g, tint.b, 0.8f));
            main.maxParticles = 16;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;

            FX.Rise(ps, 0.12f, 0.22f);
            FX.Grow(ps, FX.Curve((0f, 0.5f), (1f, 1.5f)));
            K.FadeInOut(ps, 0.1f, 0.4f);
            return ps;
        }
    }
}
