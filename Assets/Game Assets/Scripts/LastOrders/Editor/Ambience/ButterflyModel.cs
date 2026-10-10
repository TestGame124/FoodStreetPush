using System;
using LastOrders.Ambience;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;
using V = FoodStreet.Visitors.EditorTools.VehicleKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Butterflies for the grass, a good deal bigger than life so a flutter reads from the game camera:
    /// a slim dark body with feelers, and two wings - forewing and hindwing in one piece a side -
    /// hinged along the body so <see cref="Butterfly"/> can clap them up over the back.
    ///
    /// Four of the wild ones a field like this has, in their own muted colours: a cabbage white, a
    /// sulphur-yellow brimstone, a small tortoiseshell, a common blue. Faces +Z.
    /// </summary>
    internal static class ButterflyModel
    {
        internal enum Kind { CabbageWhite, Brimstone, Tortoiseshell, CommonBlue }

        internal static string PrefabPath(Kind kind) => $"{AmbienceKit.PrefabFolder}/Butterfly_{kind}.prefab";

        const string Folder = AmbienceKit.InsectFolder;

        struct Pattern
        {
            public Color Ground, Edge, Spot, Under;
            public float EdgeWidth, Spots;
        }

        static Pattern Of(Kind kind)
        {
            switch (kind)
            {
                case Kind.Brimstone:
                    return new Pattern { Ground = B.Hex("D3C477"), Edge = B.Hex("C2B266"), Spot = B.Hex("B07A45"), Under = B.Hex("C9C08A"), EdgeWidth = 0.1f, Spots = 0.3f };
                case Kind.Tortoiseshell:
                    return new Pattern { Ground = B.Hex("B0703F"), Edge = B.Hex("3F3530"), Spot = B.Hex("D6CBA8"), Under = B.Hex("5A4A3E"), EdgeWidth = 0.22f, Spots = 1f };
                case Kind.CommonBlue:
                    return new Pattern { Ground = B.Hex("8696B5"), Edge = B.Hex("4F5463"), Spot = B.Hex("C9C6BC"), Under = B.Hex("A8A49A"), EdgeWidth = 0.14f, Spots = 0.2f };
                default:
                    return new Pattern { Ground = B.Hex("DAD5C6"), Edge = B.Hex("5E5B57"), Spot = B.Hex("4A4845"), Under = B.Hex("D0CCAE"), EdgeWidth = 0.16f, Spots = 0.6f };
            }
        }

        internal static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);
            ContentGenerator.EnsureFolder(AmbienceKit.PrefabFolder);
            Mesh body = LivestockKit.Save(Body(), Folder, "Butterfly_Body");
            int count = 0;

            foreach (Kind kind in (Kind[])Enum.GetValues(typeof(Kind)))
            {
                Mesh left = LivestockKit.Save(Wing(kind, -1f), Folder, $"Butterfly_{kind}_Wing_L");
                Mesh right = LivestockKit.Save(Wing(kind, 1f), Folder, $"Butterfly_{kind}_Wing_R");

                LivestockKit.Prefab(PrefabPath(kind), $"Butterfly_{kind}", (root, created) =>
                {
                    Transform model = LivestockKit.Part(root.transform, "Body", Vector3.zero, body, false);
                    Transform l = LivestockKit.Part(model, "Wing_L", Vector3.zero, left, false);
                    Transform r = LivestockKit.Part(model, "Wing_R", Vector3.zero, right, false);

                    // Half again bigger than the meshes: a flutter has to read from the game camera.
                    root.transform.localScale = Vector3.one * 1.5f;

                    var fly = root.GetComponent<Butterfly>();
                    if (fly == null) fly = root.AddComponent<Butterfly>();
                    fly.Configure(l, r, kind == Kind.CommonBlue ? 11f : 9f, 1.3f);
                });
                count++;
            }

            return $"Butterflies: {count} kinds.";
        }

        static B.Model Body()
        {
            var m = new B.Model();
            Color dark = B.Hex("2F2B28");
            S.Ball(m, (p, n) => dark * (0.9f + 0.2f * Mathf.Max(0f, n.y)), new Vector3(0f, 0f, -0.02f), new Vector3(0.008f, 0.008f, 0.032f), Quaternion.identity, 8, 8);
            S.Ball(m, (p, n) => dark, new Vector3(0f, 0.001f, 0.017f), new Vector3(0.009f, 0.009f, 0.011f), Quaternion.identity, 8, 6);
            S.Ball(m, (p, n) => dark, new Vector3(0f, 0.002f, 0.031f), new Vector3(0.0065f, 0.0065f, 0.0065f), Quaternion.identity, 8, 6);
            for (int side = -1; side <= 1; side += 2)
            {
                m.Tube((p, n) => dark, new Vector3(side * 0.003f, 0.005f, 0.035f), new Vector3(side * 0.012f, 0.018f, 0.06f), 0.0012f, 0.0012f, 4);
                S.Ball(m, (p, n) => dark, new Vector3(side * 0.012f, 0.018f, 0.06f), new Vector3(0.0022f, 0.0022f, 0.0022f), Quaternion.identity, 6, 4);
            }

            return m;
        }

        /// <summary>Forewing and hindwing together, lying flat out to <paramref name="side"/>, hinged on the body's axis.</summary>
        static B.Model Wing(Kind kind, float side)
        {
            Pattern pattern = Of(kind);
            var m = new B.Model();
            Vector3 seed = V.Seed(81 + (int)kind);

            Func<Vector3, Vector3, Color> paint = (p, n) =>
            {
                if (n.y < 0f) return pattern.Under * (0.95f + 0.08f * V.Noise(p * 120f + seed));

                // Darker toward the rim of each wing, a row of spots along it, a dusting of scales.
                float reach = new Vector2(p.x * 1.1f, p.z).magnitude / 0.075f;
                float edge = LivestockKit.Smooth((reach - (1f - pattern.EdgeWidth)) / 0.12f);
                Color c = Color.Lerp(pattern.Ground, pattern.Edge, edge);
                float spot = V.Band(V.Noise(p * 260f + seed), 0.68f, 0.74f) * pattern.Spots * LivestockKit.Smooth((reach - 0.35f) / 0.2f);
                c = Color.Lerp(c, pattern.Spot, spot);
                return c * (0.95f + 0.08f * V.Noise(p * 150f + seed));
            };

            // Forewing swept forward and out, hindwing rounder behind.
            S.Ball(m, paint, new Vector3(side * 0.04f, 0f, 0.012f), new Vector3(0.042f, 0.0016f, 0.028f), Quaternion.Euler(0f, side * -28f, 0f), 14, 6);
            S.Ball(m, paint, new Vector3(side * 0.032f, -0.0005f, -0.02f), new Vector3(0.032f, 0.0016f, 0.026f), Quaternion.Euler(0f, side * 22f, 0f), 14, 6);
            return m;
        }

        [MenuItem("Last Orders/Ambience/Build Butterflies (Models & Prefabs)", priority = 121)]
        static void Run() => Debug.Log("[Last Orders] " + Apply());
    }
}
