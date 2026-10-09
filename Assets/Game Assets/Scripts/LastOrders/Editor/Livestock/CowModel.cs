using System;
using System.Collections.Generic;
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
    /// The cow shelter's cow: a small, chunky dairy cow, black and white - the patches laid on her
    /// as crisp panels of her own surface, the way the cars' doors are, rather than painted in
    /// ragged - a dark head with a white blaze down it, a pale pink muzzle, short horns, ears out
    /// sideways, a leather collar with a bell, an udder, a long tail with a dark tuft. Muted like
    /// everything else: cream rather than white, charcoal rather than black.
    ///
    /// Thirteen pieces for the parts that move apart (<see cref="CowBody"/> moves them): the body and
    /// udder; the head and neck about where the neck meets the shoulders, carrying the jaw, both
    /// ears, the eyelids and the bell; the tail in two lengths; a front and a back leg, each used
    /// on both sides. She faces +Z, her right is +X, the ground is y 0.
    /// </summary>
    internal static class CowModel
    {
        internal const string PrefabPath = LivestockKit.PrefabFolder + "/Animal_Cow.prefab";

        const string Folder = LivestockKit.AnimalMeshFolder;

        // --- Measurements (metres) -------------------------------------------------------------

        static readonly Vector3 NeckBase = new Vector3(0f, 0.74f, 0.46f);
        static readonly Vector3 Skull = new Vector3(0f, 0.93f, 0.86f);

        /// <summary>The head's own frame: tipped nose-down, its z running from the poll to the nose.</summary>
        static readonly Quaternion HeadTurn = Quaternion.Euler(42f, 0f, 0f);

        static readonly Vector3 TailRoot = new Vector3(0f, 0.85f, -0.71f);
        static readonly Vector3 TailBend = new Vector3(0f, -0.3f, -0.07f);
        static readonly Vector3 Shoulder = new Vector3(0.175f, 0.6f, 0.36f);
        static readonly Vector3 Hip = new Vector3(0.175f, 0.62f, -0.46f);

        static Vector3 OnHead(float x, float y, float z) => Skull + HeadTurn * new Vector3(x, y, z);

        static Vector3 JawHinge => OnHead(0f, -0.095f, 0.03f);
        static Vector3 EarRoot(float side) => OnHead(side * 0.13f, 0.005f, -0.15f);
        static Vector3 LidPivot => OnHead(0f, 0.095f, -0.04f);
        static Vector3 Eye(float side) => OnHead(side * 0.13f, 0.065f, -0.04f);
        static readonly Vector3 NeckCentre = new Vector3(0f, 0.82f, 0.6f);
        static readonly Quaternion NeckTurn = Quaternion.Euler(40f, 0f, 0f);
        static Vector3 CollarCentre => NeckCentre + NeckTurn * Vector3.up * 0.1f;

        /// <summary>A point round the collar: 0 on her right, a quarter turn under her throat.</summary>
        static Vector3 CollarAt(float a) => CollarCentre + Vector3.right * (Mathf.Cos(a) * 0.19f) + NeckTurn * Vector3.forward * (Mathf.Sin(a) * 0.246f);

        static Vector3 BellStrap => CollarAt(Mathf.PI * 0.5f) + NeckTurn * Vector3.forward * 0.012f;

        // --- The palette -------------------------------------------------------------------------

        static readonly Color Hide = B.Hex("ACA597");
        static readonly Color HideShade = B.Hex("948D80");
        static readonly Color Patch = B.Hex("3B3734");
        static readonly Color Muzzle = B.Hex("A28B83");
        static readonly Color MuzzleDark = B.Hex("7E6A64");
        static readonly Color Udder = B.Hex("A68C86");
        static readonly Color Hoof = B.Hex("3A3431");
        static readonly Color Horn = B.Hex("BCB39F");
        static readonly Color HornTip = B.Hex("6E665B");
        static readonly Color EyeDark = B.Hex("221D1A");
        static readonly Color Leather = B.Hex("4D3B2F");
        static readonly Color Brass = B.Hex("9C8D66");

        // --- Building ----------------------------------------------------------------------------

        /// <summary>The meshes and the prefab. Returns a line for the log.</summary>
        internal static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);

            Mesh body = LivestockKit.Save(Body(), Folder, "Cow_Body");
            Mesh head = LivestockKit.Save(Head(), Folder, "Cow_Head");
            Mesh jaw = LivestockKit.Save(Jaw(), Folder, "Cow_Jaw");
            Mesh earL = LivestockKit.Save(Ear(-1f), Folder, "Cow_Ear_L");
            Mesh earR = LivestockKit.Save(Ear(1f), Folder, "Cow_Ear_R");
            Mesh lids = LivestockKit.Save(Lids(), Folder, "Cow_Lids");
            Mesh bell = LivestockKit.Save(Bell(), Folder, "Cow_Bell");
            Mesh tail = LivestockKit.Save(Tail(), Folder, "Cow_Tail");
            Mesh tip = LivestockKit.Save(TailTip(), Folder, "Cow_TailTip");
            Mesh front = LivestockKit.Save(Leg(true), Folder, "Cow_Leg_Front");
            Mesh back = LivestockKit.Save(Leg(false), Folder, "Cow_Leg_Back");
            Mesh udder = LivestockKit.Save(UdderPart(), Folder, "Cow_Udder");

            LivestockKit.Prefab(PrefabPath, "Cow", (root, created) => Rig(root, body, head, jaw, earL, earR, lids, bell, tail, tip, front, back, udder));

            int vertices = body.vertexCount + head.vertexCount + jaw.vertexCount + earL.vertexCount * 2 + lids.vertexCount + bell.vertexCount +
                           tail.vertexCount + tip.vertexCount + front.vertexCount * 2 + back.vertexCount * 2;
            return $"Cow: {vertices} vertices.";
        }

        static void Rig(GameObject root, Mesh body, Mesh head, Mesh jaw, Mesh earL, Mesh earR, Mesh lids, Mesh bell, Mesh tail, Mesh tip,
                        Mesh front, Mesh back, Mesh udder)
        {
            Transform model = LivestockKit.Part(root.transform, "Model", Vector3.zero, body);
            Transform headPart = LivestockKit.Part(model, "Head", NeckBase, head);
            Transform jawPart = LivestockKit.Part(headPart, "Jaw", JawHinge - NeckBase, jaw, false);
            Transform left = LivestockKit.Part(headPart, "Ear_L", EarRoot(-1f) - NeckBase, earL, false);
            Transform right = LivestockKit.Part(headPart, "Ear_R", EarRoot(1f) - NeckBase, earR, false);
            Transform lidPart = LivestockKit.Part(headPart, "Lids", LidPivot - NeckBase, lids, false);
            lidPart.localScale = new Vector3(1f, 0.02f, 1f);
            Transform bellPart = LivestockKit.Part(headPart, "Bell", BellStrap - NeckBase, bell, false);

            Transform tailPart = LivestockKit.Part(model, "Tail", TailRoot, tail, false);
            Transform tipPart = LivestockKit.Part(tailPart, "TailTip", TailBend, tip, false);

            Transform fl = LivestockKit.Part(model, "Leg_FL", new Vector3(-Shoulder.x, Shoulder.y, Shoulder.z), front);
            Transform fr = LivestockKit.Part(model, "Leg_FR", Shoulder, front);
            Transform bl = LivestockKit.Part(model, "Leg_BL", new Vector3(-Hip.x, Hip.y, Hip.z), back);
            Transform br = LivestockKit.Part(model, "Leg_BR", Hip, back);
            Transform bag = LivestockKit.Part(model, "Udder", UdderRoot, udder, false);

            var cow = root.GetComponent<CowBody>();
            if (cow == null) cow = root.AddComponent<CowBody>();
            cow.Configure(model, headPart, jawPart, left, right, lidPart, bellPart, tailPart, tipPart, fl, fr, bl, br, bag);
        }

        // --- The body ----------------------------------------------------------------------------

        static Loft Torso() => new Loft
        {
            Back = -0.76f, Front = 0.64f, CapBack = 0.2f, CapFront = 0.26f, Round = 2.7f, CapRound = 2.4f,
            Section = z =>
            {
                float t = Mathf.InverseLerp(-0.54f, 0.38f, z);
                float barrel = Mathf.Sin(t * Mathf.PI);
                // The belly hangs in the middle, the back dips a little behind the shoulders.
                float bottom = 0.44f - 0.08f * barrel;
                float top = 0.925f - 0.03f * Mathf.Sin(Mathf.Clamp01(t * 1.3f) * Mathf.PI) + 0.012f * (1f - t);
                float half = 0.27f + 0.07f * Mathf.Pow(Mathf.Max(0f, barrel), 0.7f);
                return new Vector3(bottom, top, half);
            },
            Tuck = y => 1f - 0.18f * Mathf.Max(0f, y) * Mathf.Max(0f, y) - 0.08f * Mathf.Max(0f, -y) * Mathf.Max(0f, -y),
        };

        static B.Model Body()
        {
            var m = new B.Model();
            Loft torso = Torso();
            Vector3 seed = V.Seed(3);

            torso.Build(m, (p, n) =>
            {
                Color c = Color.Lerp(HideShade, Hide, LivestockKit.Smooth((n.y + 0.8f) / 1.2f));
                return c * (0.97f + 0.05f * V.Noise(p * 6f + seed));
            }, 40, 44);

            // The patches: crisp panels lifted off her own surface, their outlines wobbled into blots.
            Func<Vector3, Vector3, Color> dark = (p, n) => Patch * (0.95f + 0.08f * V.Noise(p * 7f + seed)) * (n.y < -0.3f ? 0.85f : 1f);
            Blot(torso, m, dark, torso.SideArea(-1f, -0.4f, 0.02f, 0.56f, 0.91f), 1);
            Blot(torso, m, dark, torso.SideArea(-1f, 0.2f, 0.42f, 0.49f, 0.78f), 2);
            Blot(torso, m, dark, torso.SideArea(1f, -0.08f, 0.3f, 0.52f, 0.9f), 3);
            Blot(torso, m, dark, torso.SideArea(1f, -0.56f, -0.3f, 0.62f, 0.87f), 4);
            Blot(torso, m, dark, torso.TopArea(-0.16f, 0.12f, -0.47f, -0.2f), 5);
            Blot(torso, m, dark, torso.TopArea(-0.1f, 0.18f, 0.05f, 0.3f), 6);

            return m;
        }

        /// <summary>Where the udder hangs from her belly: it is scaled about here as it fills.</summary>
        static readonly Vector3 UdderRoot = new Vector3(0f, 0.45f, -0.29f);

        /// <summary>The udder and four teats under it, about where it hangs from the belly - its own part, so it can fill.</summary>
        static B.Model UdderPart()
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(-UdderRoot) };
            var udder = new Vector3(0f, 0.375f, -0.29f);
            S.Ball(m, (p, n) => Color.Lerp(Udder * 0.86f, Udder, LivestockKit.Smooth((p.y - 0.3f) / 0.1f)), udder, new Vector3(0.13f, 0.085f, 0.125f),
                   Quaternion.identity, 16, 10);
            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -0.05f : 0.05f, z = (k & 2) == 0 ? -0.055f : 0.055f;
                var top = new Vector3(x, 0.315f, udder.z + z);
                m.Tube((p, n) => Udder * 0.9f, top, top + new Vector3(x * 0.15f, -0.055f, 0f), 0.017f, 0.013f, 8);
            }

            return m;
        }

        /// <summary>A patch of hide laid on the body whose outline is wobbled into a blot rather than a rounded rectangle.</summary>
        static void Blot(Loft torso, B.Model m, Func<Vector3, Vector3, Color> paint, Func<float, float, Vector3> area, int key)
        {
            float k = key * 1.7f;
            Func<float, float, Vector3> wobbled = (a, b) =>
            {
                float r = 0.82f + 0.13f * Mathf.Sin(Mathf.Atan2(b, a) * 3f + k) + 0.06f * Mathf.Sin(Mathf.Atan2(b, a) * 5f - k * 2f);
                return area(Mathf.Clamp(a * r, -1f, 1f), Mathf.Clamp(b * r, -1f, 1f));
            };
            torso.Patch(m, paint, wobbled, 0.004f, 18, 14, 2.2f);
        }

        // --- The head ----------------------------------------------------------------------------

        /// <summary>Neck, head, muzzle, horns, eyes, blaze and collar, about where the neck meets the shoulders.</summary>
        static B.Model Head()
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(-NeckBase) };
            Vector3 seed = V.Seed(9);

            // The neck: thick, with the dewlap hanging under it, half dark and half cream.
            S.Ball(m, (p, n) =>
            {
                Color c = p.x < -0.02f && p.y > 0.8f ? Patch : Color.Lerp(HideShade, Hide, LivestockKit.Smooth((n.y + 0.8f) / 1.2f));
                return c * (0.97f + 0.05f * V.Noise(p * 6f + seed));
            }, NeckCentre, new Vector3(0.19f, 0.24f, 0.25f), NeckTurn, 22, 16);
            S.Ball(m, (p, n) => HideShade, new Vector3(0f, 0.66f, 0.66f), new Vector3(0.07f, 0.12f, 0.13f), Quaternion.Euler(-20f, 0f, 0f), 12, 10);

            // The skull: dark, broad at the poll, narrowing down to the muzzle.
            var skull = new SmoothMesh(1);
            skull.RoundedBox(0, OnHead(0f, 0f, -0.03f), new Vector3(0.27f, 0.21f, 0.38f), 0.095f, HeadTurn, 3);
            m.Add(skull, (p, n) => Patch * (n.y < -0.3f ? 0.85f : 1f) * (0.97f + 0.05f * V.Noise(p * 9f + seed)));

            // A white blaze down the face, and a white forelock between the horns.
            S.Ball(m, (p, n) => Hide, OnHead(0f, 0.098f, -0.01f), new Vector3(0.042f, 0.014f, 0.16f), HeadTurn, 14, 10);
            S.Ball(m, (p, n) => Hide, OnHead(0f, 0.075f, -0.2f), new Vector3(0.07f, 0.045f, 0.05f), HeadTurn, 12, 8);

            // The muzzle: broad, soft, pale pink, with dark nostrils.
            var muzzle = new SmoothMesh(1);
            muzzle.RoundedBox(0, OnHead(0f, -0.005f, 0.2f), new Vector3(0.26f, 0.2f, 0.14f), 0.075f, HeadTurn, 3);
            m.Add(muzzle, (p, n) => Muzzle * (n.y < -0.4f ? 0.86f : 1f));
            for (int side = -1; side <= 1; side += 2)
                S.Ball(m, (p, n) => MuzzleDark, OnHead(side * 0.055f, 0.035f, 0.27f), new Vector3(0.024f, 0.016f, 0.012f), HeadTurn, 10, 6);

            // Eyes on the sides of the head, dark and calm.
            for (int side = -1; side <= 1; side += 2)
                S.Ball(m, (p, n) => EyeDark, Eye(side), new Vector3(0.022f, 0.026f, 0.028f), HeadTurn, 10, 8);

            // Short horns, curving out and up, darker at the tips.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 root = OnHead(side * 0.09f, 0.05f, -0.2f);
                var path = new List<Vector3> { root, root + new Vector3(side * 0.07f, 0.03f, 0.0f), root + new Vector3(side * 0.12f, 0.085f, 0.02f) };
                V.Sweep(m, (p, n) => Color.Lerp(Horn, HornTip, LivestockKit.Smooth(((p - root).magnitude - 0.08f) / 0.07f)), path, 0.024f, 10, 5);
            }

            // A leather collar round the neck.
            var collar = new List<Vector3>();
            for (int i = 0; i < 12; i++) collar.Add(CollarAt(i / 12f * Mathf.PI * 2f));

            V.Sweep(m, (p, n) => Leather * (n.y < -0.3f ? 0.8f : 1f), collar, 0.024f, 8, 3, true, new Vector2(1f, 0.55f));
            return m;
        }

        /// <summary>The lower jaw, about its hinge: it chews and drops open to moo.</summary>
        static B.Model Jaw()
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(-JawHinge) };
            var jaw = new SmoothMesh(1);
            jaw.RoundedBox(0, OnHead(0f, -0.1f, 0.17f), new Vector3(0.18f, 0.06f, 0.15f), 0.028f, HeadTurn, 2);
            m.Add(jaw, (p, n) => Muzzle * (n.y < -0.4f ? 0.82f : 0.95f));
            return m;
        }

        /// <summary>An ear sticking out sideways, pink inside. About its root on the side of the head.</summary>
        static B.Model Ear(float side)
        {
            Vector3 root = EarRoot(side);
            var m = new B.Model { Transform = Matrix4x4.Translate(-root) };
            Quaternion turn = Quaternion.Euler(0f, 0f, side * -18f) * Quaternion.Euler(10f, side * 8f, 0f);
            S.Ball(m, (p, n) => n.z > 0.35f ? Muzzle : Patch, root + turn * new Vector3(side * 0.095f, 0f, 0f), new Vector3(0.1f, 0.03f, 0.058f),
                   turn, 14, 10);
            return m;
        }

        /// <summary>Both eyelids, about a pivot above the eyes: full size they close the eyes; scaled to nothing they are open.</summary>
        static B.Model Lids()
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(-LidPivot) };
            for (int side = -1; side <= 1; side += 2)
                S.Ball(m, (p, n) => Patch * 1.08f, Eye(side), new Vector3(0.027f, 0.031f, 0.033f), HeadTurn, 10, 8);
            return m;
        }

        /// <summary>A brass bell on a strap, about where it hangs from the collar.</summary>
        static B.Model Bell()
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(-BellStrap) };
            Vector3 foot = BellStrap + new Vector3(0f, -0.11f, 0f);
            m.Box(Leather, BellStrap + new Vector3(0f, -0.02f, 0f), new Vector3(0.03f, 0.05f, 0.012f), 0.005f);
            m.Lathe((p, n) => Brass * (0.9f + 0.15f * Mathf.Max(0f, n.y)) * (n.y < -0.5f ? 0.55f : 1f), foot, new List<Vector2>
            {
                new Vector2(0f, 0.005f), new Vector2(0.045f, 0f), new Vector2(0.05f, 0.004f), new Vector2(0.04f, 0.04f),
                new Vector2(0.03f, 0.07f), new Vector2(0.018f, 0.082f), new Vector2(0f, 0.085f),
            }, 14);
            S.Ball(m, (p, n) => Brass * 0.5f, foot + Vector3.down * 0.004f, Vector3.one * 0.014f, Quaternion.identity, 8, 6);
            return m;
        }

        // --- The tail and legs -------------------------------------------------------------------

        static B.Model Tail()
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(-TailRoot) };
            m.Tube((p, n) => Hide * 0.95f, TailRoot + new Vector3(0f, 0.02f, 0.02f), TailRoot + TailBend, 0.034f, 0.024f, 10);
            return m;
        }

        static B.Model TailTip()
        {
            var m = new B.Model();
            m.Tube((p, n) => Hide * 0.92f, Vector3.zero, new Vector3(0f, -0.26f, 0.01f), 0.024f, 0.018f, 10);
            S.Ball(m, (p, n) => Patch * (0.95f + 0.1f * Mathf.Max(0f, n.y)), new Vector3(0f, -0.32f, 0.012f), new Vector3(0.042f, 0.085f, 0.042f),
                   Quaternion.identity, 12, 10);
            return m;
        }

        /// <summary>A leg from shoulder or hip to hoof: cream, a dark cloven hoof. The back legs bend back at the hock.</summary>
        static B.Model Leg(bool front)
        {
            var m = new B.Model();
            float ground = -(front ? Shoulder.y : Hip.y);
            var knee = new Vector3(0f, ground * 0.48f, front ? 0.01f : -0.035f);
            var fetlock = new Vector3(0f, ground + 0.11f, front ? 0.015f : 0.01f);

            Func<Vector3, Vector3, Color> leg = (p, n) => Color.Lerp(HideShade, Hide, LivestockKit.Smooth((n.y + 0.6f) / 1.2f)) *
                                                          Mathf.Lerp(0.9f, 1f, LivestockKit.Smooth((p.y - ground) / 0.3f));
            m.Tube(leg, new Vector3(0f, 0.06f, 0f), knee, front ? 0.11f : 0.12f, 0.074f, 14);
            S.Ball(m, leg, knee, new Vector3(0.074f, 0.05f, 0.074f), Quaternion.identity, 12, 8);
            m.Tube(leg, knee, fetlock, 0.07f, 0.058f, 12);
            S.Ball(m, leg, fetlock, new Vector3(0.058f, 0.04f, 0.058f), Quaternion.identity, 10, 8);

            // The hoof: two toes, dark, a hairline split between them.
            for (int side = -1; side <= 1; side += 2)
                m.Box(Hoof, new Vector3(side * 0.03f, ground + 0.04f, 0.022f), new Vector3(0.056f, 0.08f, 0.12f), 0.022f, Quaternion.Euler(-4f, 0f, 0f), 2);

            return m;
        }

        [MenuItem("Last Orders/Farming/Build Cow (Model & Prefab)", priority = 98)]
        static void Run() => Debug.Log("[Last Orders] " + Apply());
    }
}
