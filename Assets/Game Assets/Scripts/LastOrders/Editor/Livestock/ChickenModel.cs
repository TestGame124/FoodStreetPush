using System;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Livestock;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;
using V = FoodStreet.Visitors.EditorTools.VehicleKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The coop's hens, modelled in code like the compound's props: plump and a little bigger than
    /// life so they read from the game camera, a round body with a full breast and a fluffy rump,
    /// a short tail fanned up behind, a red comb and wattles, small dark eyes - a hen, not a toy:
    /// no googly eyes, the plumage in the muted palette with the colour in the feathers' own shades.
    /// Three plumages - cream, russet, speckled grey - so a run of them reads as a flock.
    ///
    /// Seven meshes for the parts that move apart (<see cref="ChickenBody"/> moves them): the body,
    /// the head and neck about the base of the neck, each wing about its shoulder, the tail about the
    /// rump, a leg (used twice) about the hip. It faces +Z, its right is +X, the ground is y 0.
    /// </summary>
    internal static class ChickenModel
    {
        internal enum Breed { Cream, Russet, Speckled }

        internal static readonly Breed[] Breeds = { Breed.Cream, Breed.Russet, Breed.Speckled };

        internal static string PrefabPath(Breed breed) => $"{LivestockKit.PrefabFolder}/Animal_Hen_{breed}.prefab";

        const string Folder = LivestockKit.AnimalMeshFolder;

        // --- Measurements (metres) -------------------------------------------------------------

        static readonly Vector3 NeckBase = new Vector3(0f, 0.335f, 0.1f);
        static readonly Vector3 Shoulder = new Vector3(0.118f, 0.318f, 0.035f);
        static readonly Vector3 TailRoot = new Vector3(0f, 0.335f, -0.165f);
        const float HipX = 0.056f, HipY = 0.172f;

        // --- The palette -------------------------------------------------------------------------

        static readonly Color Comb = B.Hex("94403A");
        static readonly Color CombDark = B.Hex("7A3430");
        static readonly Color Beak = B.Hex("B39C68");
        static readonly Color Eye = B.Hex("231E1B");
        static readonly Color Shank = B.Hex("A99358");

        struct Plumage
        {
            public Color Body, Light, Dark, Hackle, Wing, Flight, Tail;
            public float Speckle;
        }

        static Plumage Of(Breed breed)
        {
            switch (breed)
            {
                case Breed.Russet:
                    return new Plumage
                    {
                        Body = B.Hex("846550"), Light = B.Hex("957459"), Dark = B.Hex("5F4637"), Hackle = B.Hex("957157"),
                        Wing = B.Hex("775A47"), Flight = B.Hex("4A3a30"), Tail = B.Hex("3A322D"),
                    };
                case Breed.Speckled:
                    return new Plumage
                    {
                        Body = B.Hex("6F6D68"), Light = B.Hex("A19D93"), Dark = B.Hex("4D4B48"), Hackle = B.Hex("8D8A82"),
                        Wing = B.Hex("65635E"), Flight = B.Hex("44423F"), Tail = B.Hex("3B3A38"), Speckle = 1f,
                    };
                default:
                    return new Plumage
                    {
                        Body = B.Hex("AFA897"), Light = B.Hex("B9B2A1"), Dark = B.Hex("8F8879"), Hackle = B.Hex("9C978B"),
                        Wing = B.Hex("A59E8D"), Flight = B.Hex("6D6A63"), Tail = B.Hex("4A4845"), Speckle = 0.25f,
                    };
            }
        }

        // --- Building ----------------------------------------------------------------------------

        /// <summary>The meshes and a prefab per plumage. Returns a line for the log.</summary>
        internal static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);
            int vertices = 0;
            Mesh leg = LivestockKit.Save(Leg(), Folder, "Hen_Leg");

            foreach (Breed breed in Breeds)
            {
                Mesh body = LivestockKit.Save(Body(breed), Folder, $"Hen_{breed}_Body");
                Mesh head = LivestockKit.Save(Head(breed), Folder, $"Hen_{breed}_Head");
                Mesh wingL = LivestockKit.Save(Wing(breed, -1f), Folder, $"Hen_{breed}_Wing_L");
                Mesh wingR = LivestockKit.Save(Wing(breed, 1f), Folder, $"Hen_{breed}_Wing_R");
                Mesh tail = LivestockKit.Save(Tail(breed), Folder, $"Hen_{breed}_Tail");

                LivestockKit.Prefab(PrefabPath(breed), $"Hen_{breed}", (root, created) => Rig(root, body, head, wingL, wingR, tail, leg));
                vertices = body.vertexCount + head.vertexCount + wingL.vertexCount * 2 + tail.vertexCount + leg.vertexCount * 2;
            }

            return $"Hens: {Breeds.Length} plumages, {vertices} vertices each.";
        }

        static void Rig(GameObject root, Mesh body, Mesh head, Mesh wingL, Mesh wingR, Mesh tail, Mesh leg)
        {
            Transform model = LivestockKit.Part(root.transform, "Model", Vector3.zero, body);
            Transform headPart = LivestockKit.Part(model, "Head", NeckBase, head);
            Transform left = LivestockKit.Part(model, "Wing_L", new Vector3(-Shoulder.x, Shoulder.y, Shoulder.z), wingL);
            Transform right = LivestockKit.Part(model, "Wing_R", Shoulder, wingR);
            Transform tailPart = LivestockKit.Part(model, "Tail", TailRoot, tail);
            Transform legLeft = LivestockKit.Part(model, "Leg_L", new Vector3(-HipX, HipY, 0f), leg, false);
            Transform legRight = LivestockKit.Part(model, "Leg_R", new Vector3(HipX, HipY, 0f), leg, false);

            var hen = root.GetComponent<ChickenBody>();
            if (hen == null) hen = root.AddComponent<ChickenBody>();
            hen.Configure(model, headPart, left, right, tailPart, legLeft, legRight, null, null);
        }

        // --- The parts ---------------------------------------------------------------------------

        static Func<Vector3, Vector3, Color> Feathers(Plumage plumage, Color main, int key)
        {
            Vector3 seed = V.Seed(key);
            return (p, n) =>
            {
                // Lighter on the back and breast where the light falls, dusky underneath.
                Color c = Color.Lerp(plumage.Dark, main, LivestockKit.Smooth((n.y + 0.75f) / 1.1f));
                c = Color.Lerp(c, plumage.Light, LivestockKit.Smooth((n.y - 0.35f) / 0.6f) * 0.5f);
                c *= 0.95f + 0.08f * V.Noise(p * 22f + seed);

                // Speckles: soft, so they read as a mottle rather than as dots.
                if (plumage.Speckle > 0f)
                {
                    float spot = V.Band(V.Noise(p * 75f + seed * 2f), 0.6f, 0.7f) * plumage.Speckle;
                    c = Color.Lerp(c, plumage.Light, spot * 0.5f);
                }

                return c;
            };
        }

        static B.Model Body(Breed breed)
        {
            Plumage plumage = Of(breed);
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, LivestockKit.Smooth((p.y - 0.12f) / 0.18f)) };
            Func<Vector3, Vector3, Color> paint = Feathers(plumage, plumage.Body, 11 + (int)breed);

            // One smooth body: a full breast low in front, deepest at the middle, the back rising
            // behind into a fluffy rump that tips up toward the tail.
            var body = new Loft
            {
                Back = -0.235f, Front = 0.19f, CapBack = 0.1f, CapFront = 0.11f, Round = 2.15f, CapRound = 2.1f,
                Section = z =>
                {
                    float t = Mathf.InverseLerp(-0.135f, 0.08f, z);
                    float bottom = Mathf.Lerp(0.2f, 0.148f, LivestockKit.Smooth(t * 1.3f));
                    float top = Mathf.Lerp(0.37f, 0.375f, t);
                    float half = Mathf.Lerp(0.108f, 0.142f, LivestockKit.Smooth(t * 1.6f));
                    return new Vector3(bottom, top, half);
                },
                Tuck = y => 1f - 0.12f * Mathf.Max(0f, y) * Mathf.Max(0f, y),
                Warp = p =>
                {
                    // The rump tipped up behind; the breast pushed out and down in front.
                    float back = Mathf.Max(0f, -p.z - 0.06f);
                    float front = Mathf.Max(0f, p.z - 0.02f);
                    return p + new Vector3(0f, back * back * 1.9f - front * front * 0.6f * Mathf.Max(0f, 0.3f - p.y) * 4f, 0f);
                },
            };
            body.Build(m, paint, 34, 30);

            // Fluffy thighs, the legs coming out of them.
            for (int side = -1; side <= 1; side += 2)
                S.Ball(m, paint, new Vector3(side * 0.058f, 0.19f, -0.005f), new Vector3(0.058f, 0.07f, 0.078f), Quaternion.identity, 14, 10);

            return m;
        }

        /// <summary>Neck, head, comb, wattles, beak and eyes, about the base of the neck.</summary>
        static B.Model Head(Breed breed)
        {
            Plumage plumage = Of(breed);
            var m = new B.Model { Transform = Matrix4x4.Translate(-NeckBase) };
            Func<Vector3, Vector3, Color> hackle = Feathers(plumage, plumage.Hackle, 31 + (int)breed);

            // The neck: a ruff of hackle feathers, thick at the shoulders.
            S.Ball(m, hackle, new Vector3(0f, 0.375f, 0.118f), new Vector3(0.066f, 0.088f, 0.068f), Quaternion.Euler(18f, 0f, 0f), 16, 12);
            S.Ball(m, hackle, new Vector3(0f, 0.442f, 0.148f), new Vector3(0.052f, 0.054f, 0.062f), Quaternion.Euler(-8f, 0f, 0f), 16, 12);

            // A beak, a little hooked down.
            m.Tube((p, n) => n.y < -0.3f ? Beak * 0.82f : Beak, new Vector3(0f, 0.438f, 0.19f), new Vector3(0f, 0.421f, 0.243f), 0.019f, 0.004f, 10);

            // The comb: a row of rounded lobes along the crown, the tallest in the middle.
            Func<Vector3, Vector3, Color> comb = (p, n) => Color.Lerp(CombDark, Comb, LivestockKit.Smooth((p.y - 0.47f) / 0.05f));
            S.Ball(m, comb, new Vector3(0f, 0.486f, 0.145f), new Vector3(0.011f, 0.022f, 0.048f), Quaternion.identity, 10, 8);
            float[] lobeZ = { 0.178f, 0.152f, 0.126f, 0.104f };
            float[] lobeH = { 0.022f, 0.03f, 0.027f, 0.019f };
            for (int i = 0; i < lobeZ.Length; i++)
                S.Ball(m, comb, new Vector3(0f, 0.497f + lobeH[i] * 0.4f, lobeZ[i]), new Vector3(0.0105f, lobeH[i], 0.0145f),
                       Quaternion.Euler(-8f + i * 9f, 0f, 0f), 10, 8);

            // Wattles under the beak.
            for (int side = -1; side <= 1; side += 2)
                S.Ball(m, comb, new Vector3(side * 0.011f, 0.398f, 0.18f), new Vector3(0.012f, 0.024f, 0.011f), Quaternion.Euler(-10f, 0f, 0f), 10, 8);

            // Small dark eyes, set into the side of the head.
            for (int side = -1; side <= 1; side += 2)
                S.Ball(m, (p, n) => Eye, new Vector3(side * 0.046f, 0.455f, 0.162f), new Vector3(0.0085f, 0.011f, 0.011f), Quaternion.identity, 8, 6);

            return m;
        }

        /// <summary>A wing folded against the body: coverts on top, the flight feathers darker at the back. About the shoulder.</summary>
        static B.Model Wing(Breed breed, float side)
        {
            Plumage plumage = Of(breed);
            var shoulder = new Vector3(side * Shoulder.x, Shoulder.y, Shoulder.z);
            var m = new B.Model { Transform = Matrix4x4.Translate(-shoulder) };
            Func<Vector3, Vector3, Color> feathers = Feathers(plumage, plumage.Wing, 51 + (int)breed);

            S.Ball(m, (p, n) =>
            {
                Color c = feathers(p, n);
                // The primaries showing darker toward the tip, in bands a feather wide.
                float back = LivestockKit.Smooth((-p.z - 0.07f) / 0.08f);
                float band = Mathf.Repeat(p.y * 38f, 1f) < 0.5f ? 0.92f : 1f;
                return Color.Lerp(c, plumage.Flight * band, back * 0.85f);
            }, new Vector3(side * 0.13f, 0.272f, -0.06f), new Vector3(0.03f, 0.082f, 0.14f), Quaternion.Euler(-12f, side * -5f, side * 4f), 16, 12);

            return m;
        }

        /// <summary>The tail: five feathers fanned up and back from the rump. About where they join the back.</summary>
        static B.Model Tail(Breed breed)
        {
            Plumage plumage = Of(breed);
            var m = new B.Model { Transform = Matrix4x4.Translate(-TailRoot) };

            for (int k = -2; k <= 2; k++)
            {
                Quaternion fan = Quaternion.Euler(-32f - Mathf.Abs(k) * 7f, k * 13f, k * 9f);
                Vector3 centre = TailRoot + fan * new Vector3(0f, 0.07f, 0f);
                Color tail = plumage.Tail * (1f + 0.05f * B.Jitter(k + 7 * (int)breed));
                S.Ball(m, (p, n) => Color.Lerp(tail * 0.85f, tail * 1.12f, LivestockKit.Smooth((p.y - TailRoot.y) / 0.12f)), centre,
                       new Vector3(0.024f, 0.092f, 0.04f), fan, 12, 10);
            }

            return m;
        }

        /// <summary>A scaly yellow shank and three toes forward, one back. About the hip.</summary>
        static B.Model Leg()
        {
            var m = new B.Model();
            var ankle = new Vector3(0f, -0.158f, 0.012f);
            m.Tube((p, n) => Shank, new Vector3(0f, -0.05f, 0.004f), ankle, 0.016f, 0.012f, 8);

            float[] spread = { -0.034f, 0f, 0.034f };
            foreach (float x in spread)
                m.Tube((p, n) => Shank * 0.95f, ankle, new Vector3(x, -0.167f, 0.075f), 0.0085f, 0.0065f, 6);
            m.Tube((p, n) => Shank * 0.95f, ankle, new Vector3(0f, -0.166f, -0.038f), 0.008f, 0.006f, 6);
            return m;
        }

        [MenuItem("Last Orders/Farming/Build Hens (Model & Prefabs)", priority = 97)]
        static void Run() => Debug.Log("[Last Orders] " + Apply());
    }
}
