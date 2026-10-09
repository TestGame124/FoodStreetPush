using System.Collections.Generic;
using LastOrders.Livestock;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using F = LastOrders.EditorTools.FarmBuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;
using V = FoodStreet.Visitors.EditorTools.VehicleKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The chicken coop, in the hand of the shelter hut and the storehouse: a little hen house of
    /// weathered boards up on four legs at the back of a wire run, a blue tin roof, a pop-hole with
    /// its teal sliding door and a cleated ramp down to the yard, and a nest box hung on its side
    /// with the lid propped open - the eggs laid in it show in the straw, one by one, until somebody
    /// collects them. In the run: a feeder, a water fount, straw strewn over the dirt, a gate in the
    /// wire. Four hens of three plumages wander it, peck, scratch and dust about, and roost under the
    /// house at night (<see cref="AnimalHerd"/>, <see cref="ChickenBody"/>, <see cref="LivestockPen"/>).
    ///
    /// Bought from the shop and set down on the grid, so the prefab's root is the footprint's
    /// minimum corner and the coop stands in the middle of its 4 x 3 cells, its front - the run's gate,
    /// the pop-hole - toward -Z, the camera's side.
    /// </summary>
    internal static class ChickenCoopModel
    {
        internal const string PrefabPath = LivestockKit.PrefabFolder + "/Livestock_ChickenCoop.prefab";
        internal const int Width = 4, Depth = 3;

        const string Folder = LivestockKit.MeshFolder + "/ChickenCoop";

        // --- Measurements (metres, about the footprint's middle; front is -Z) ---------------------

        const float HouseX0 = -1.86f, HouseX1 = -0.3f, HouseZ0 = 0.16f, HouseZ1 = 1.34f;
        const float FloorY = 0.62f, WallTop = 1.28f, Slope = 0.6f, Overhang = 0.2f, GableOverhang = 0.14f;
        const float HoleX = -0.62f, HoleWidth = 0.3f, HoleHeight = 0.34f;
        const float NestX0 = -0.3f, NestX1 = 0.18f, NestZ0 = 0.34f, NestZ1 = 1.16f, NestY0 = 0.66f, NestY1 = 0.96f;
        const float FenceX = 1.93f, FenceZ = 1.43f, FenceTop = 0.74f;
        const float GateX0 = 0.6f, GateX1 = 1.3f;

        static float HouseCX => (HouseX0 + HouseX1) * 0.5f;
        static float HouseCZ => (HouseZ0 + HouseZ1) * 0.5f;
        static float HalfDepth => (HouseZ1 - HouseZ0) * 0.5f;

        /// <summary>The roof's underside at <paramref name="z"/>: the eave over the front and back walls, climbing to the ridge.</summary>
        static float RoofUnder(float z) => WallTop + Slope * (HalfDepth - Mathf.Abs(z - HouseCZ));

        static readonly Color Lid = B.Hex("8C4C42");
        static readonly Color Shell = B.Hex("9C7A5C");
        static readonly Color ShellCream = B.Hex("B5AA94");

        /// <summary>Where the eggs sit in the nest box: two to each of its three compartments.</summary>
        static Vector3 EggAt(int i)
        {
            int compartment = i / 2;
            float z = NestZ0 + (compartment + 0.5f) * (NestZ1 - NestZ0) / 3f + (i % 2 == 0 ? -0.05f : 0.06f);
            float x = i % 2 == 0 ? -0.1f : 0.03f;
            return new Vector3(x, NestY0 + 0.085f, z);
        }

        const int Eggs = 6;

        // --- Building ----------------------------------------------------------------------------

        /// <summary>The coop's meshes and prefab, the hens nested in it. Returns a line for the log.</summary>
        internal static string Apply(ItemDataSO egg, ItemDataSO corn)
        {
            ContentGenerator.EnsureFolder(Folder);
            Mesh coop = LivestockKit.Save(Coop(), Folder, "ChickenCoop");
            Mesh brown = LivestockKit.Save(Egg(Shell), Folder, "Egg_Brown");
            Mesh cream = LivestockKit.Save(Egg(ShellCream), Folder, "Egg_Cream");
            Mesh cob = LivestockKit.Save(CornCob(), Folder, "CornCob");
            var (card, bubble, drop) = LivestockUi.Apply();

            LivestockKit.Prefab(PrefabPath, "Livestock_ChickenCoop",
                                (root, created) => Dress(root, coop, brown, cream, cob, egg, corn, card, bubble, drop));
            return $"Chicken coop: {coop.vertexCount} vertices.";
        }

        /// <summary>Where the corn the hens are given lies, in front of the feeder: one cob per item in the trough.</summary>
        static readonly (Vector3 at, float yaw)[] CornAt =
        {
            (new Vector3(1.12f, 0f, 0.52f), 20f), (new Vector3(1.42f, 0f, 0.47f), -35f), (new Vector3(1.66f, 0f, 0.6f), 75f),
        };

        static void Dress(GameObject root, Mesh coop, Mesh brown, Mesh cream, Mesh cob, ItemDataSO egg, ItemDataSO corn,
                          ProduceProgressView card, ProduceBubbleView bubble, FeedDrop drop)
        {
            var middle = new Vector3(Width * 0.5f, 0f, Depth * 0.5f);
            Transform model = LivestockKit.Part(root.transform, "Model", middle, coop);

            // The eggs, hidden until laid.
            Transform nest = LivestockKit.Child(model, "Eggs", Vector3.zero);
            var eggs = new GameObject[Eggs];
            for (int i = 0; i < Eggs; i++)
            {
                Transform e = LivestockKit.Part(nest, $"Egg_{i}", EggAt(i), i % 3 == 1 ? cream : brown, false);
                e.localRotation = Quaternion.Euler(70f + 10f * B.Jitter(i), 40f * i, 0f);
                eggs[i] = e.gameObject;
                e.gameObject.SetActive(false);
            }

            // The hens: fresh copies every build, so the herd's list is always the one in the prefab.
            Transform flock = LivestockKit.Child(model, "Hens", Vector3.zero);
            for (int i = flock.childCount - 1; i >= 0; i--) Object.DestroyImmediate(flock.GetChild(i).gameObject);

            ChickenModel.Breed[] breeds = { ChickenModel.Breed.Russet, ChickenModel.Breed.Cream, ChickenModel.Breed.Speckled, ChickenModel.Breed.Russet };
            Vector3[] spots = { new Vector3(0.35f, 0f, -0.65f), new Vector3(1.25f, 0f, -0.15f), new Vector3(-0.35f, 0f, -1f), new Vector3(1.35f, 0f, 0.4f) };
            float[] yaws = { 200f, 150f, 120f, 250f };
            var hens = new Transform[breeds.Length];
            for (int i = 0; i < breeds.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChickenModel.PrefabPath(breeds[i]));
                var hen = (GameObject)PrefabUtility.InstantiatePrefab(prefab, flock);
                hen.name = $"Hen_{i}";
                hen.transform.localPosition = spots[i];
                hen.transform.localRotation = Quaternion.Euler(0f, yaws[i], 0f);
                hens[i] = hen.transform;
            }

            // The effects: feathers and dust the hens share, the burst out of the nest box.
            Transform fx = LivestockKit.Child(model, "Fx", Vector3.zero);
            ParticleSystem feathers = LivestockEffects.Feathers(fx);
            ParticleSystem dust = LivestockEffects.Scuff(fx);
            Transform burstAt = LivestockKit.Child(fx, "CollectBurst", new Vector3(NestX1 + 0.05f, NestY0 + 0.1f, (NestZ0 + NestZ1) * 0.5f));
            ParticleSystem[] burst = LivestockEffects.EggBurst(burstAt);

            // Straw worked out of the nest while a hen broods in it; a pop of light as she lays.
            Transform nestFx = LivestockKit.Child(fx, "Nest", new Vector3(NestX1 - 0.05f, NestY0 + 0.12f, (NestZ0 + NestZ1) * 0.5f));
            ParticleSystem rustle = LivestockEffects.Rustle(nestFx);
            ParticleSystem[] laid = LivestockEffects.Pop(nestFx);

            // The corn they are given, by the feeder; kernels thrown up as it is put down.
            Transform feedPile = LivestockKit.Child(model, "Corn", Vector3.zero);
            var cobs = new GameObject[CornAt.Length];
            for (int i = 0; i < CornAt.Length; i++)
            {
                Transform c = LivestockKit.Part(feedPile, $"Cob_{i}", CornAt[i].at + Vector3.up * 0.075f, cob, false);
                c.localRotation = Quaternion.Euler(0f, CornAt[i].yaw, 0f);
                cobs[i] = c.gameObject;
                c.gameObject.SetActive(false);
            }

            // Corn fed is thrown in here: kernels scattered, and the corn seen taken in.
            Transform feederFx = LivestockKit.Child(fx, "Feeder", new Vector3(1.4f, 0.1f, 0.55f));
            ParticleSystem[] scatter = LivestockEffects.CornScatter(feederFx);
            ParticleSystem[] gulp = LivestockEffects.Gulp(feederFx, new Color(0.91f, 0.77f, 0.36f));
            Transform dropInto = LivestockKit.Child(feederFx, "DropInto", new Vector3(0f, 0.25f, 0f));

            foreach (Transform hen in hens)
            {
                var body = hen.GetComponent<ChickenBody>();
                Transform m = hen.Find("Model");
                body.Configure(m, m.Find("Head"), m.Find("Wing_L"), m.Find("Wing_R"), m.Find("Tail"), m.Find("Leg_L"), m.Find("Leg_R"), feathers, dust);
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            }

            GameObject shimmer = Shimmer(model, new Vector3((NestX0 + NestX1) * 0.5f + 0.1f, NestY0 + 0.04f, (NestZ0 + NestZ1) * 0.5f), 0.7f);
            Transform collect = LivestockKit.Child(model, "CollectPoint", new Vector3(NestX1 + 0.3f, 0.55f, (NestZ0 + NestZ1) * 0.5f - 0.1f));

            var herd = model.GetComponent<AnimalHerd>();
            if (herd == null) herd = model.gameObject.AddComponent<AnimalHerd>();

            var pen = root.GetComponent<LivestockPen>();
            if (pen == null) pen = root.AddComponent<LivestockPen>();
            pen.Configure("Chicken Coop", egg, "egg", "eggs", "the hens", "The hens are roosting for the night.", 30f, Eggs, collect, eggs, shimmer,
                          burst);
            pen.ConfigureFeed(corn, "corn", CornAt.Length, 2, cobs, Join(scatter, gulp), dropInto, drop);
            pen.ConfigureUi(LivestockKit.Child(model, "UiAnchor", new Vector3(-0.35f, 2.35f, 0.55f)), card, bubble, laid);

            herd.Configure(pen, hens,
                new Rect(-FenceX + 0.06f, -FenceZ + 0.06f, (FenceX - 0.06f) * 2f, (FenceZ - 0.06f) * 2f),
                new[]
                {
                    new Vector3(HouseX0 + 0.06f, HouseZ0 + 0.06f, 0.08f), new Vector3(HouseX1 - 0.06f, HouseZ0 + 0.06f, 0.08f),
                    new Vector3(HouseX0 + 0.06f, HouseZ1 - 0.06f, 0.08f), new Vector3(HouseX1 - 0.06f, HouseZ1 - 0.06f, 0.08f),
                    new Vector3(HoleX, HouseZ0 - 0.42f, 0.24f), new Vector3(FeederAt.x, FeederAt.z, 0.2f), new Vector3(FountAt.x, FountAt.z, 0.17f),
                },
                new[]
                {
                    new AnimalHerd.Bed(new Vector3(-1.5f, 0f, 0.45f), 160f), new AnimalHerd.Bed(new Vector3(-1.15f, 0f, 1.0f), 210f),
                    new AnimalHerd.Bed(new Vector3(-0.85f, 0f, 0.5f), 120f), new AnimalHerd.Bed(new Vector3(-0.55f, 0f, 1.0f), 250f),
                },
                new Vector2(0.35f, 0.8f), new Vector2(0.3f, 1.8f), 1.6f, 480f, 0f, 0.16f, 0.85f);

            // The work: a hen up in the middle nest box, facing out over its lip, until she lays.
            float nestZ = (NestZ0 + NestZ1) * 0.5f;
            herd.ConfigureWork(new AnimalHerd.WorkSpot(new Vector3(NestX1 + 0.32f, 0f, nestZ), new Vector3((NestX0 + NestX1) * 0.5f - 0.02f, NestY0 + 0.03f, nestZ), 90f),
                               true, new Vector2(8f, 14f), new Vector2(2f, 5f), rustle, 1.8f,
                               new[]
                               {
                                   new Vector3(1.0f, 0f, 0.3f), new Vector3(1.38f, 0f, 0.22f), new Vector3(1.68f, 0f, 0.36f), new Vector3(0.9f, 0f, 0.7f),
                                   new Vector3(1.66f, 0f, 0.85f),
                               },
                               new Vector2(7f, 12f));

            // The tap box: a trigger, so it takes taps but the eggs thrown out land on the ground, not on it.
            var tap = root.GetComponent<BoxCollider>();
            if (tap == null) tap = root.AddComponent<BoxCollider>();
            tap.isTrigger = true;
            tap.center = middle + new Vector3(0f, 0.7f, 0f);
            tap.size = new Vector3(Width - 0.1f, 1.4f, Depth - 0.1f);

            if (root.GetComponent<PlacedObject_Done>() == null) root.AddComponent<PlacedObject_Done>();
        }

        /// <summary>The harvest shimmer the ripe crops use, nested over the nest, off until there is something in it.</summary>
        internal static GameObject Shimmer(Transform parent, Vector3 at, float scale)
        {
            Transform old = parent.Find("Shimmer");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlantingEffects.Folder + "/RipeShimmer.prefab");
            if (prefab == null) return null;

            var shimmer = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            shimmer.name = "Shimmer";
            shimmer.transform.localPosition = at;
            shimmer.transform.localScale = Vector3.one * scale;
            shimmer.SetActive(false);
            return shimmer;
        }

        internal static ParticleSystem[] Join(ParticleSystem[] a, ParticleSystem[] b)
        {
            var all = new ParticleSystem[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }

        // --- The coop ----------------------------------------------------------------------------

        static readonly Vector3 FeederAt = new Vector3(1.35f, 0f, 0.92f);
        static readonly Vector3 FountAt = new Vector3(0.62f, 0f, 1.12f);

        static B.Model Coop()
        {
            var m = new B.Model
            {
                Shade = (p, n) =>
                {
                    // Darker toward the ground and in under the house.
                    float k = Mathf.Lerp(0.8f, 1f, LivestockKit.Smooth(p.y / 0.4f));
                    bool under = p.x > HouseX0 - 0.05f && p.x < HouseX1 + 0.05f && p.z > HouseZ0 - 0.05f && p.z < HouseZ1 + 0.05f && p.y < FloorY;
                    return under ? k * 0.8f : k;
                },
            };

            F.Yard(m, new Rect(-Width * 0.5f + 0.02f, -Depth * 0.5f + 0.02f, Width - 0.04f, Depth - 0.04f), 21,
                   (x, z) => 0.55f + 0.45f * Mathf.Sin(x * 1.7f + z * 2.3f),
                   (x, z) => x > HouseX0 - 0.1f && x < HouseX1 + 0.1f && z > HouseZ0 - 0.1f && z < HouseZ1 + 0.1f ? 1f : 0f);

            House(m);
            Roof(m);
            PopHole(m);
            NestBox(m);
            Run(m);

            F.Feeder(m, FeederAt, 1.15f);
            F.Fount(m, FountAt, 1.1f);

            // Straw kicked about the run.
            Vector2[] tufts = { new Vector2(0.9f, -0.9f), new Vector2(-0.1f, -0.4f), new Vector2(1.6f, -1.1f), new Vector2(0.2f, 0.5f), new Vector2(-1.4f, -0.8f) };
            for (int i = 0; i < tufts.Length; i++) F.StrawTuft(m, new Vector3(tufts[i].x, 0.03f, tufts[i].y), 0.14f, i + 3);

            return m;
        }

        /// <summary>Four legs, a floor, upright boards all round up to the eaves and into the gables, the plates along the top.</summary>
        static void House(B.Model m)
        {
            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? HouseX0 + 0.05f : HouseX1 - 0.05f, z = (k & 2) == 0 ? HouseZ0 + 0.05f : HouseZ1 - 0.05f;
                m.Box(B.Walnut, new Vector3(x, WallTop * 0.5f, z), new Vector3(0.09f, WallTop, 0.09f), 0.018f, 2);
                // A flat stone under each leg.
                m.Box(B.Stone * (1f + 0.05f * B.Jitter(k)), new Vector3(x, 0.03f, z), new Vector3(0.18f, 0.06f, 0.17f), 0.025f,
                      Quaternion.Euler(0f, 9f * B.Jitter(k + 4), 0f), 2);
            }

            // The floor, and a skirting board under it.
            m.Box(B.WalnutDark, new Vector3(HouseCX, FloorY - 0.03f, HouseCZ), new Vector3(HouseX1 - HouseX0 + 0.04f, 0.06f, HouseZ1 - HouseZ0 + 0.04f),
                  0.012f, 2);

            const float inset = 0.06f;
            var hole = new[] { new Vector3(HoleX - HoleWidth * 0.5f, HoleX + HoleWidth * 0.5f, FloorY + HoleHeight) };
            m.BoardWall(new Vector3(0f, 0f, HouseZ0), Vector3.right, Vector3.back, HouseX0 + inset, HouseX1 - inset, FloorY, _ => WallTop, 0.17f, 41, hole);
            m.BoardWall(new Vector3(0f, 0f, HouseZ1), Vector3.right, Vector3.forward, HouseX0 + inset, HouseX1 - inset, FloorY, _ => WallTop, 0.17f, 42);

            // The gable ends, up under the roof: each board's square top set by its far corner, so none pokes through the tin.
            m.BoardWall(new Vector3(HouseX0, 0f, 0f), Vector3.forward, Vector3.left, HouseZ0 + inset, HouseZ1 - inset, FloorY,
                        z => RoofUnder(z + Mathf.Sign(z - HouseCZ) * 0.09f) - 0.01f, 0.17f, 43);
            m.BoardWall(new Vector3(HouseX1, 0f, 0f), Vector3.forward, Vector3.right, HouseZ0 + inset, HouseZ1 - inset, FloorY,
                        z => RoofUnder(z + Mathf.Sign(z - HouseCZ) * 0.09f) - 0.01f, 0.17f, 44);

            // The plates along the eaves, and cream trim boards on the corners.
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side < 0 ? HouseZ0 - 0.02f : HouseZ1 + 0.02f;
                // Kept under the tin: a plate standing proud of the eave shows through the troughs of the sheets.
                m.Box(B.Walnut, new Vector3(HouseCX, WallTop - 0.045f, z), new Vector3(HouseX1 - HouseX0 + 0.1f, 0.07f, 0.07f), 0.015f, 2);
            }

            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? HouseX0 + 0.01f : HouseX1 - 0.01f;
                m.Box(F.Cream, new Vector3(x, (FloorY + WallTop) * 0.5f, HouseZ0 - 0.035f), new Vector3(0.08f, WallTop - FloorY, 0.03f), 0.01f, 1);
            }

            // A little barred window in the front wall, to the left of the pop-hole.
            var window = new Vector3(-1.38f, FloorY + 0.38f, HouseZ0 - 0.03f);
            m.Box(F.Cream, window, new Vector3(0.32f, 0.26f, 0.03f), 0.012f, 2);
            m.Box(B.Glass, window + Vector3.back * 0.008f, new Vector3(0.24f, 0.18f, 0.02f), 0.006f, 1);
            for (int i = -1; i <= 1; i++)
                m.Box(F.Cream * 0.95f, window + new Vector3(i * 0.06f, 0f, -0.02f), new Vector3(0.018f, 0.2f, 0.018f), 0.005f, 1);
        }

        /// <summary>Two slopes of blue tin meeting at a ridge along the house, a capping on the ridge, barge boards at the gables.</summary>
        static void Roof(B.Model m)
        {
            const float thickness = 0.03f;
            float cos = 1f / Mathf.Sqrt(1f + Slope * Slope), sin = Slope * cos;
            float run = HalfDepth + Overhang;
            float length = run / cos;
            float lift = thickness / cos;
            float x0 = HouseX0 - GableOverhang, x1 = HouseX1 + GableOverhang;
            float ridge = RoofUnder(HouseCZ) + lift;

            for (int side = -1; side <= 1; side += 2)
            {
                var top0 = new Vector3(x0, ridge, HouseCZ + side * 0.02f);
                var top1 = new Vector3(x1, ridge, HouseCZ + side * 0.02f);
                F.TinSlope(m, top0, top1, new Vector3(0f, -sin, side * cos), length, B.RoofTin, 3, side > 0 ? 1 : 2, 0.4f);

                // Barge boards under the sheets' edges at both gables.
                float angle = Mathf.Atan(Slope) * Mathf.Rad2Deg * side;
                var mid = new Vector3(0f, RoofUnder(HouseCZ + side * run * 0.5f) - 0.05f, HouseCZ + side * run * 0.5f);
                foreach (float x in new[] { x0 + 0.03f, x1 - 0.03f })
                    m.Box(B.Walnut, mid + Vector3.right * x, new Vector3(0.045f, 0.11f, length + 0.04f), 0.012f, Quaternion.Euler(angle, 0f, 0f), 2);
            }

            m.Tube(B.Hex("485E70"), new Vector3(x0 - 0.02f, ridge + 0.02f, HouseCZ), new Vector3(x1 + 0.02f, ridge + 0.02f, HouseCZ), 0.055f, 14);
        }

        /// <summary>The pop-hole: a cream frame, the teal door slid up in its runners, the ramp down to the yard with its cleats.</summary>
        static void PopHole(B.Model m)
        {
            float z = HouseZ0 - 0.03f;
            float top = FloorY + HoleHeight;
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(F.Cream, new Vector3(HoleX + side * (HoleWidth * 0.5f + 0.03f), FloorY + 0.32f, z), new Vector3(0.05f, 0.64f, 0.035f), 0.01f, 1);
            }

            m.Box(F.Cream, new Vector3(HoleX, top + 0.02f, z - 0.005f), new Vector3(HoleWidth + 0.12f, 0.05f, 0.035f), 0.01f, 1);
            m.Box(B.WalnutDark * 0.7f, new Vector3(HoleX, FloorY + HoleHeight * 0.5f, HouseZ0 + 0.02f), new Vector3(HoleWidth, HoleHeight, 0.02f), 0.005f, 1);

            // The door, slid up above the hole, with a pull cord's ring.
            var door = new Vector3(HoleX, top + 0.2f, z - 0.025f);
            m.Box(F.DoorPaint, door, new Vector3(HoleWidth + 0.02f, 0.3f, 0.025f), 0.008f, 2);
            for (int i = 0; i < 2; i++)
                m.Box(F.DoorPaint * 0.85f, door + new Vector3(0f, -0.07f + i * 0.14f, -0.015f), new Vector3(HoleWidth - 0.02f, 0.03f, 0.012f), 0.004f, 1);
            V.Sweep(m, B.Iron, new[] { door + new Vector3(-0.03f, -0.12f, -0.03f), door + new Vector3(0f, -0.16f, -0.035f), door + new Vector3(0.03f, -0.12f, -0.03f) },
                    0.006f, 6, 3);

            // The ramp: a plank down to the yard, cleats across it every hand's width.
            var from = new Vector3(HoleX, FloorY - 0.005f, HouseZ0 - 0.03f);
            var to = new Vector3(HoleX, 0.035f, HouseZ0 - 0.82f);
            Vector3 d = to - from;
            Quaternion turn = Quaternion.LookRotation(d.normalized, Vector3.up);
            m.Box(B.Plank(77), (from + to) * 0.5f, new Vector3(0.3f, 0.025f, d.magnitude), 0.008f, turn, 1);
            int cleats = 6;
            for (int i = 1; i < cleats; i++)
            {
                Vector3 at = Vector3.Lerp(from, to, i / (float)cleats) + turn * Vector3.up * 0.02f;
                m.Box(B.Walnut, at, new Vector3(0.28f, 0.02f, 0.025f), 0.005f, turn, 1);
            }
        }

        /// <summary>
        /// The nest box on the house's side: three compartments of straw, a lip along the front, the
        /// lid hinged at the wall and propped open so the eggs show.
        /// </summary>
        static void NestBox(B.Model m)
        {
            float midZ = (NestZ0 + NestZ1) * 0.5f, spanZ = NestZ1 - NestZ0;
            Color wood = B.Plank(91);

            // Floor, ends, dividers and a low lip along the front.
            m.Box(wood * 0.9f, new Vector3((NestX0 + NestX1) * 0.5f, NestY0, midZ), new Vector3(NestX1 - NestX0, 0.03f, spanZ + 0.03f), 0.006f, 1);
            for (int k = 0; k <= 3; k++)
            {
                float z = NestZ0 + spanZ * k / 3f;
                float h = k == 0 || k == 3 ? NestY1 - NestY0 : 0.16f;
                m.Box(k == 0 || k == 3 ? wood : wood * 0.85f, new Vector3((NestX0 + NestX1) * 0.5f, NestY0 + h * 0.5f, z),
                      new Vector3(NestX1 - NestX0, h, 0.025f), 0.006f, 1);
            }

            m.Box(wood * 0.95f, new Vector3(NestX1 - 0.012f, NestY0 + 0.04f, midZ), new Vector3(0.025f, 0.08f, spanZ + 0.03f), 0.006f, 1);

            // Straw in each compartment, heaped a little.
            for (int k = 0; k < 3; k++)
            {
                var c = new Vector3((NestX0 + NestX1) * 0.5f - 0.02f, NestY0 + 0.03f, NestZ0 + spanZ * (k + 0.5f) / 3f);
                S.Ball(m, (p, n) => Color.Lerp(F.Straw, F.StrawLight, V.Noise(p * 30f)) * (n.y < 0f ? 0.75f : 1f), c,
                       new Vector3(0.19f, 0.04f, 0.115f), Quaternion.identity, 12, 8);
            }

            // Brackets under it, back to the wall.
            foreach (float z in new[] { NestZ0 + 0.08f, NestZ1 - 0.08f })
            {
                var a = new Vector3(NestX0 + 0.02f, NestY0 - 0.28f, z);
                var b = new Vector3(NestX1 - 0.06f, NestY0 - 0.02f, z);
                m.Box(B.Walnut, (a + b) * 0.5f, new Vector3(0.04f, (b - a).magnitude, 0.04f), 0.01f, Quaternion.FromToRotation(Vector3.up, b - a), 1);
            }

            // The lid, painted barn red, hinged at the wall and propped open against it.
            var hinge = new Vector3(NestX0 + 0.01f, NestY1 + 0.02f, midZ);
            Quaternion open = Quaternion.Euler(0f, 0f, 78f);
            Vector3 lidCentre = hinge + open * new Vector3(0.27f, 0f, 0f);
            m.Box(Lid, lidCentre, new Vector3(0.54f, 0.03f, spanZ + 0.08f), 0.01f, open, 2);
            for (int i = 0; i < 2; i++)
                m.Box(Lid * 0.85f, lidCentre + open * new Vector3(-0.15f + i * 0.3f, 0.025f, 0f), new Vector3(0.05f, 0.02f, spanZ + 0.06f), 0.006f, open, 1);
            m.Box(B.Iron, hinge + new Vector3(0.01f, 0f, -spanZ * 0.3f), new Vector3(0.05f, 0.025f, 0.06f), 0.006f, 1);
            m.Box(B.Iron, hinge + new Vector3(0.01f, 0f, spanZ * 0.3f), new Vector3(0.05f, 0.025f, 0.06f), 0.006f, 1);
        }

        /// <summary>The run: posts and rails round the whole footprint, wire stretched between them, a gate in the front.</summary>
        static void Run(B.Model m)
        {
            const float post = 0.075f;
            var corners = new[]
            {
                new Vector3(-FenceX, 0f, -FenceZ), new Vector3(FenceX, 0f, -FenceZ), new Vector3(FenceX, 0f, FenceZ), new Vector3(-FenceX, 0f, FenceZ),
            };

            // Posts along each side about a metre apart; the front leaves room for the gate.
            var front = new List<float> { -FenceX, -0.65f, GateX0, GateX1, FenceX };
            int key = 0;
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = corners[side], b = corners[(side + 1) % 4];
                List<float> along = new List<float>();
                if (side == 0)
                {
                    foreach (float x in front) along.Add((x + FenceX) / (2f * FenceX));
                }
                else
                {
                    int spans = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 1f));
                    for (int i = 0; i <= spans; i++) along.Add(i / (float)spans);
                }

                for (int i = 0; i < along.Count; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, along[i]);
                    F.Post(m, p, FenceTop + 0.06f, post, ++key);
                    if (i == 0) continue;

                    Vector3 q = Vector3.Lerp(a, b, along[i - 1]);
                    bool gate = side == 0 && Mathf.Abs(q.x - GateX0) < 0.01f;
                    if (gate)
                    {
                        Gate(m, q, p);
                        continue;
                    }

                    F.Rail(m, q + Vector3.up * FenceTop, p + Vector3.up * FenceTop, 0.06f, 0.035f, ++key);
                    F.Rail(m, q + Vector3.up * 0.06f, p + Vector3.up * 0.06f, 0.08f, 0.03f, ++key);
                    F.WireMesh(m, q, p, 0.1f, FenceTop - 0.03f, 0.13f);
                }
            }
        }

        /// <summary>A gate of the run: a frame with a brace, wire across it, two strap hinges and a latch.</summary>
        static void Gate(B.Model m, Vector3 hingeFoot, Vector3 latchFoot)
        {
            Vector3 a = hingeFoot + Vector3.right * 0.05f, b = latchFoot - Vector3.right * 0.05f;
            Color frame = B.Plank(55) * 1.05f;
            float top = FenceTop - 0.02f, bottom = 0.08f;

            F.Rail(m, a + Vector3.up * top, b + Vector3.up * top, 0.06f, 0.035f, 201);
            F.Rail(m, a + Vector3.up * bottom, b + Vector3.up * bottom, 0.06f, 0.035f, 202);
            m.Box(frame, (a + b) * 0.5f + Vector3.up * ((top + bottom) * 0.5f) + Vector3.left * ((b - a).magnitude * 0.5f - 0.02f),
                  new Vector3(0.05f, top - bottom, 0.035f), 0.008f, 1);
            m.Box(frame, (a + b) * 0.5f + Vector3.up * ((top + bottom) * 0.5f) + Vector3.right * ((b - a).magnitude * 0.5f - 0.02f),
                  new Vector3(0.05f, top - bottom, 0.035f), 0.008f, 1);

            // The brace, from the bottom hinge side to the top latch side.
            Vector3 p0 = a + Vector3.up * (bottom + 0.03f), p1 = b + Vector3.up * (top - 0.03f);
            m.Box(frame * 0.95f, (p0 + p1) * 0.5f + Vector3.back * 0.012f, new Vector3(0.045f, (p1 - p0).magnitude, 0.025f), 0.007f,
                  Quaternion.FromToRotation(Vector3.up, p1 - p0), 1);
            F.WireMesh(m, a, b, bottom + 0.03f, top - 0.03f, 0.13f);

            foreach (float y in new[] { bottom + 0.06f, top - 0.06f })
                m.Box(B.Iron, a + new Vector3(0.08f, y, -0.025f), new Vector3(0.18f, 0.025f, 0.01f), 0.004f, 1);
            m.Box(B.Iron, b + new Vector3(0.02f, top - 0.12f, -0.03f), new Vector3(0.1f, 0.03f, 0.015f), 0.004f, 1);
        }

        /// <summary>An egg, about its middle: a little pointed at the top.</summary>
        static B.Model Egg(Color shell)
        {
            var m = new B.Model();
            const float r = 0.043f, h = 0.115f;
            var profile = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                float radius = r * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.85f) * (1.1f - 0.22f * t);
                profile.Add(new Vector2(radius, t * h - h * 0.5f));
            }

            m.Lathe((p, n) => shell * Mathf.Lerp(0.88f, 1.05f, LivestockKit.Smooth(n.y * 0.5f + 0.5f)), Vector3.zero, profile, 12);
            return m;
        }

        [MenuItem("Last Orders/Farming/Build Chicken Coop (Model & Prefab)", priority = 99)]
        static void Run() => Debug.Log("[Last Orders] " + Apply(AssetDatabase.LoadAssetAtPath<ItemDataSO>(LivestockContent.EggPath),
                                                                AssetDatabase.LoadAssetAtPath<ItemDataSO>(LivestockContent.CornPath)));

        /// <summary>A cob of corn lying on its side, husk peeled back: what the hens are fed. About its middle.</summary>
        static B.Model CornCob()
        {
            var m = new B.Model();
            CornShooterModel.Cob(m, new Vector3(0f, 0f, -0.12f), Quaternion.Euler(90f, 0f, 0f), 0.24f, 0.052f, 5);
            return m;
        }
    }
}
