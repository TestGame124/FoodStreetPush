using System.Collections.Generic;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using D = LastOrders.EditorTools.DiscoveryKit;
using K = FoodStreet.Visitors.EditorTools.VehicleKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Where the food truck stands by the road: a concrete hardstanding with a kerbed edge and wheel
    /// chocks, an oil stain where it has stood. Found, it is weeds and cones and a fallen bin; back at
    /// work, a chalkboard on the kerb, a table under a parasol with stools, a bin and a lamp. The truck
    /// itself is its own vehicle. Origin on the ground in the middle; the truck parks facing +X.
    /// </summary>
    public static class FoodTruckPadModel
    {
        public const float Length = 7.2f, Width = 4.2f;
        const float Top = 0.07f;

        static float HX => Length * 0.5f;
        static float HZ => Width * 0.5f;

        public static B.Model Pad()
        {
            var m = new B.Model { Shade = D.GroundShade(0.3f) };

            // The slab, a shade lighter toward its middle, its corners a little chipped.
            m.Box(D.Concrete, new Vector3(0f, Top * 0.5f, 0f), new Vector3(Length, Top, Width), 0.03f, 2);
            for (int k = 0; k < 4; k++)
            {
                float x = -HX + (k + 0.5f) * Length / 4f;
                m.Box(D.ConcreteDark, new Vector3(x - Length / 8f, Top + 0.001f, 0f), new Vector3(0.03f, 0.004f, Width - 0.1f), 0.001f);
            }

            // A kerb along the back and the far end.
            for (float x = -HX + 0.4f; x < HX - 0.3f; x += 0.82f)
                m.Box(D.Concrete * (Mathf.Repeat(x, 1.64f) < 0.82f ? 1f : 0.93f), new Vector3(x + 0.4f, 0.1f, HZ + 0.1f), new Vector3(0.78f, 0.2f, 0.2f), 0.04f, 2);
            for (float z = -HZ + 0.3f; z < HZ; z += 0.82f)
                m.Box(D.Concrete * 0.96f, new Vector3(HX + 0.1f, 0.1f, z + 0.4f), new Vector3(0.2f, 0.2f, 0.78f), 0.04f, 2);

            // Wheel chocks for the back wheels, and the oil stain under the engine.
            foreach (float z in new[] { -0.86f, 0.86f })
            {
                Vector3 chock = new Vector3(-1.95f, Top, z);
                m.Box(D.Mustard, chock + Vector3.up * 0.06f, new Vector3(0.22f, 0.12f, 0.3f), 0.03f, Quaternion.Euler(0f, 0f, -25f), 2);
            }

            m.Lathe((p, n) => B.Hex("3A3734"), new Vector3(1.6f, Top, 0f), new List<Vector2>
            {
                new Vector2(0f, 0.002f), new Vector2(0.55f, 0.002f), new Vector2(0.6f, 0f),
            }, 18);
            return m;
        }

        public static B.Model Derelict()
        {
            var m = new B.Model { Shade = D.GroundShade(0.3f) };

            for (int k = 0; k < 10; k++)
            {
                float a = k * 0.62f;
                D.WeedClump(m, new Vector3(Mathf.Cos(a) * (HX + 0.3f), 0f, Mathf.Sin(a) * (HZ + 0.25f)), 0.26f, 10, 140 + k);
            }

            // Weeds pushing up through the cracks.
            D.WeedClump(m, new Vector3(-0.6f, Top, 1.2f), 0.15f, 6, 160);
            D.WeedClump(m, new Vector3(2.4f, Top, -1.4f), 0.15f, 6, 161);

            D.Cone(m, new Vector3(2.9f, Top, -1.6f), 0f, false);
            D.Cone(m, new Vector3(3.4f, Top, -0.6f), 120f, true);
            D.Tyre(m, new Vector3(-3.0f, Top + 0.11f, 1.5f), Vector3.up, 0.38f, 0.22f);

            // A bin knocked over, its rubbish spilled.
            Matrix4x4 was = m.Transform;
            m.Transform = was * Matrix4x4.TRS(new Vector3(-2.6f, 0.26f, -1.65f), Quaternion.Euler(0f, 30f, 90f), Vector3.one);
            m.Lathe((p, n) => D.TealDark * 0.85f, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.23f, 0.7f), new Vector2(0.25f, 0.72f), new Vector2(0.2f, 0.72f), new Vector2(0.18f, 0.1f),
            }, 14);
            m.Transform = was;
            D.RubbishSack(m, new Vector3(-1.9f, Top, -1.8f), 0.7f, 11);

            // Pallets stacked against the kerb.
            for (int k = 0; k < 3; k++)
                Pallet(m, new Vector3(-0.8f, Top + k * 0.15f, HZ - 0.55f), 4f * B.Jitter(k));
            return m;
        }

        public static B.Model Restored()
        {
            var m = new B.Model { Shade = D.GroundShade(0.3f) };

            // A chalkboard A-frame on the kerb, toward the road.
            AFrame(m, new Vector3(2.6f, Top, -HZ + 0.4f), -20f);

            // A table under a parasol, two stools, by the far end.
            Vector3 table = new Vector3(HX + 1.3f, 0f, -0.6f);
            m.Lathe((p, n) => D.Steel, table, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.3f, 0f), new Vector2(0.3f, 0.03f), new Vector2(0.05f, 0.06f), new Vector2(0.04f, 0.72f),
                new Vector2(0f, 0.72f),
            }, 14);
            m.Lathe((p, n) => n.y > 0.5f ? D.CreamLight : D.Steel, table + Vector3.up * 0.72f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.45f, 0f), new Vector2(0.47f, 0.02f), new Vector2(0.45f, 0.04f), new Vector2(0f, 0.04f),
            }, 20);
            m.Tube(D.SteelDark, table + Vector3.up * 0.76f, table + Vector3.up * 2.15f, 0.02f, 8);

            // The parasol: a striped canopy in panels, red and cream, a finial on top.
            Vector3 hub = table + Vector3.up * 2.15f;
            const int Panels = 8;
            for (int i = 0; i < Panels; i++)
            {
                float a0 = i * Mathf.PI * 2f / Panels, a1 = (i + 1) * Mathf.PI * 2f / Panels;
                Color c = i % 2 == 0 ? D.Red : D.CreamLight;
                // The underside faces down (u round, v out); the top faces up (u out, v round).
                m.Surface((p, n) => c * 0.7f, 4, 6, (u, v) =>
                {
                    float a = Mathf.Lerp(a0, a1, u);
                    float r = v * 1.15f;
                    float drop = v * v * 0.42f + Mathf.Sin(u * Mathf.PI) * 0.03f * v + 0.012f;
                    return hub + new Vector3(Mathf.Cos(a) * r, -drop, Mathf.Sin(a) * r);
                });
                m.Surface((p, n) => c, 6, 4, (v, u) =>
                {
                    float a = Mathf.Lerp(a0, a1, u);
                    float r = v * 1.15f;
                    float drop = v * v * 0.42f + Mathf.Sin(u * Mathf.PI) * 0.03f * v;
                    return hub + new Vector3(Mathf.Cos(a) * r, -drop, Mathf.Sin(a) * r);
                });
            }

            S.Ball(m, (p, n) => D.Mustard, hub + Vector3.up * 0.05f, Vector3.one * 0.06f, Quaternion.identity, 8, 6);

            foreach (Vector3 off in new[] { new Vector3(0.75f, 0f, 0.2f), new Vector3(-0.2f, 0f, 0.75f) })
                Stool(m, table + off);

            // A bin, and a lamp on a post by the kerb, lit.
            m.Lathe((p, n) => D.TealDark, new Vector3(-HX + 0.4f, Top, -HZ + 0.45f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.23f, 0.7f), new Vector2(0.25f, 0.72f), new Vector2(0.25f, 0.76f),
                new Vector2(0.17f, 0.78f), new Vector2(0f, 0.86f),
            }, 16);

            var post = new Vector3(-HX - 0.4f, 0f, HZ - 0.2f);
            m.Box(D.Concrete, post + Vector3.up * 0.06f, new Vector3(0.26f, 0.12f, 0.26f), 0.03f, 2);
            m.Tube(D.SteelDark, post, post + Vector3.up * 2.4f, 0.04f, 10);
            m.Lantern(post + Vector3.up * 2.2f + Vector3.right * 0.04f, Vector3.right);
            return m;
        }

        static void Pallet(B.Model m, Vector3 foot, float yaw)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            for (int k = 0; k < 5; k++)
                m.Box(B.Plank(k + 30) * 1.05f, foot + turn * new Vector3(-0.48f + k * 0.24f, 0.13f, 0f), new Vector3(0.14f, 0.025f, 1.0f), 0.006f, turn);
            foreach (float z in new[] { -0.42f, 0f, 0.42f })
                m.Box(B.Walnut, foot + turn * new Vector3(0f, 0.06f, z), new Vector3(1.1f, 0.1f, 0.1f), 0.012f, turn);
        }

        static void AFrame(B.Model m, Vector3 foot, float yaw)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                Quaternion lean = turn * Quaternion.Euler(s * 14f, 0f, 0f);
                Vector3 c = foot + turn * new Vector3(0f, 0.45f, s * 0.11f);
                m.Box(B.Walnut, c, new Vector3(0.62f, 0.9f, 0.035f), 0.012f, lean, 2);
                m.Box(B.Hex("2E3532"), c + lean * new Vector3(0f, 0.02f, s * 0.02f), new Vector3(0.52f, 0.72f, 0.012f), 0.004f, lean);
                for (int k = 0; k < 3; k++)
                    m.Box(D.CreamLight, c + lean * new Vector3(0f, 0.2f - k * 0.16f, s * 0.028f), new Vector3(0.34f - k * 0.06f, 0.035f, 0.006f), 0.003f, lean);
            }

            m.Tube(D.SteelDark, foot + turn * new Vector3(-0.28f, 0.88f, 0f), foot + turn * new Vector3(0.28f, 0.88f, 0f), 0.012f, 6);
        }

        static void Stool(B.Model m, Vector3 foot)
        {
            m.Lathe((p, n) => n.y > 0.5f ? D.Red : D.Steel, foot, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.18f, 0f), new Vector2(0.18f, 0.02f), new Vector2(0.03f, 0.04f), new Vector2(0.03f, 0.48f),
                new Vector2(0.17f, 0.5f), new Vector2(0.18f, 0.55f), new Vector2(0f, 0.57f),
            }, 14);
        }
    }
}
