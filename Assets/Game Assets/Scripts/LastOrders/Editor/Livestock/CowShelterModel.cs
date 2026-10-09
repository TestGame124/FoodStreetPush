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
    /// The small cow shelter: a lean-to of weathered boards along the back of a paddock under a
    /// galvanised tin roof, open to the front on three posts, straw bedding inside, a hay rack on
    /// the back wall, a lantern on the middle post. A water trough, a stack of hay bales, a milking
    /// stool and pail, a churn stand by the paddock's corner where the morning's milk stands in
    /// churns - one more each time she gives - and a post-and-rail fence round it all with a farm
    /// gate. One cow lives in it: wanders, grazes, swishes her tail, and at night lies down in the
    /// straw (<see cref="AnimalHerd"/>, <see cref="CowBody"/>, <see cref="LivestockPen"/>).
    ///
    /// Bought from the shop and set down on the grid: the root is the footprint's minimum corner and
    /// the shelter stands in the middle of its 5 x 4 cells, the paddock's open side toward -Z.
    /// </summary>
    internal static class CowShelterModel
    {
        internal const string PrefabPath = LivestockKit.PrefabFolder + "/Livestock_CowShelter.prefab";
        internal const int Width = 5, Depth = 4;

        const string Folder = LivestockKit.MeshFolder + "/CowShelter";

        // --- Measurements (metres, about the footprint's middle; front is -Z) ---------------------

        const float ShedX0 = -2.32f, ShedX1 = 0.98f, ShedZ0 = 0.44f, ShedZ1 = 1.9f;
        const float FrontEave = 1.85f, BackEave = 2.22f, Overhang = 0.3f, BackOverhang = 0.14f, SideOverhang = 0.16f;
        const float FenceX = 2.44f, FenceZ = 1.94f, FenceTop = 0.95f;
        const float GateX0 = -1.6f, GateX1 = -0.45f;

        static readonly Vector3 StandAt = new Vector3(1.9f, 0f, -1.6f);
        const float StandTop = 0.34f;
        static readonly Vector3 TroughAt = new Vector3(-1.95f, 0f, -0.75f);
        static readonly Vector3 StoolAt = new Vector3(0.95f, 0f, -1.55f);
        static readonly Vector3 BalesAt = new Vector3(1.68f, 0f, 1.3f);

        /// <summary>The roof's underside at <paramref name="z"/>: low at the open front, high at the back wall.</summary>
        static float RoofAt(float z) => Mathf.LerpUnclamped(FrontEave, BackEave, (z - ShedZ0) / (ShedZ1 - ShedZ0));

        static Vector3 ChurnAt(int i) => StandAt + new Vector3(-0.3f + i * 0.3f, StandTop + 0.015f, 0f);

        const int Churns = 3;

        // --- Building ----------------------------------------------------------------------------

        /// <summary>The shelter's meshes and prefab, the cow in it. Returns a line for the log.</summary>
        internal static string Apply(ItemDataSO milk, ItemDataSO wheat)
        {
            ContentGenerator.EnsureFolder(Folder);
            Mesh shelter = LivestockKit.Save(Shelter(), Folder, "CowShelter");
            Mesh churn = LivestockKit.Save(Churn(), Folder, "Churn");
            Mesh hay = LivestockKit.Save(HayBundle(), Folder, "HayBundle");
            var (card, bubble, drop) = LivestockUi.Apply();

            LivestockKit.Prefab(PrefabPath, "Livestock_CowShelter", (root, created) => Dress(root, shelter, churn, hay, milk, wheat, card, bubble, drop));
            return $"Cow shelter: {shelter.vertexCount} vertices.";
        }

        // The hay rack on the back wall: where the cow eats, and where her wheat goes in as hay.
        const float RackX0 = -2.0f, RackX1 = -0.95f, RackBottom = 0.75f, RackTop = 1.3f;
        static float RackWall => ShedZ1 - 0.04f;
        const int Feeds = 3;

        static void Dress(GameObject root, Mesh shelter, Mesh churn, Mesh hay, ItemDataSO milk, ItemDataSO wheat, ProduceProgressView card,
                          ProduceBubbleView bubble, FeedDrop drop)
        {
            var middle = new Vector3(Width * 0.5f, 0f, Depth * 0.5f);
            Transform model = LivestockKit.Part(root.transform, "Model", middle, shelter);

            Transform stand = LivestockKit.Child(model, "Churns", Vector3.zero);
            var churns = new GameObject[Churns];
            for (int i = 0; i < Churns; i++)
            {
                Transform c = LivestockKit.Part(stand, $"Churn_{i}", ChurnAt(i), churn);
                c.localRotation = Quaternion.Euler(0f, 25f * i - 20f, 0f);
                churns[i] = c.gameObject;
                c.gameObject.SetActive(false);
            }

            Transform herdRoot = LivestockKit.Child(model, "Cows", Vector3.zero);
            for (int i = herdRoot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(herdRoot.GetChild(i).gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CowModel.PrefabPath);
            var cow = (GameObject)PrefabUtility.InstantiatePrefab(prefab, herdRoot);
            cow.name = "Cow";
            cow.transform.localPosition = new Vector3(0.2f, 0f, -0.6f);
            cow.transform.localRotation = Quaternion.Euler(0f, 215f, 0f);

            Transform fx = LivestockKit.Child(model, "Fx", Vector3.zero);
            Transform burstAt = LivestockKit.Child(fx, "CollectBurst", StandAt + Vector3.up * (StandTop + 0.45f));
            ParticleSystem[] burst = LivestockEffects.MilkBurst(burstAt);
            Transform standFx = LivestockKit.Child(fx, "Stand", StandAt + Vector3.up * (StandTop + 0.3f));
            ParticleSystem[] filled = LivestockEffects.Pop(standFx);

            // Hay in the rack, one bundle per item of wheat; hay falling from it as she eats, tossed in as she is fed.
            Transform rack = LivestockKit.Child(model, "Hay", Vector3.zero);
            var bundles = new GameObject[Feeds];
            for (int i = 0; i < Feeds; i++)
            {
                var at = new Vector3(Mathf.Lerp(RackX0 + 0.2f, RackX1 - 0.2f, i / (Feeds - 1f)), RackTop - 0.08f + 0.03f * B.Jitter(i), RackWall - 0.2f);
                Transform b = LivestockKit.Part(rack, $"Hay_{i}", at, hay, false);
                b.localRotation = Quaternion.Euler(0f, 35f * B.Jitter(i + 5), 0f);
                bundles[i] = b.gameObject;
                b.gameObject.SetActive(false);
            }

            Transform rackFx = LivestockKit.Child(fx, "Rack", new Vector3((RackX0 + RackX1) * 0.5f, RackBottom + 0.1f, RackWall - 0.32f));
            ParticleSystem hayFall = LivestockEffects.HayFall(rackFx);

            // Wheat fed goes in under the front eave by the rack - the rack itself is hidden by the
            // roof from the game's camera - with hay tossed and the wheat seen taken in.
            foreach (string old in new[] { "Hay", "Chaff" })
            {
                Transform stale = rackFx.Find(old);
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
            }

            Transform feedFx = LivestockKit.Child(fx, "FeedIn", new Vector3((RackX0 + RackX1) * 0.5f, 0.85f, ShedZ0 - 0.1f));
            ParticleSystem[] toss = ChickenCoopModel.Join(LivestockEffects.HayToss(feedFx), LivestockEffects.Gulp(feedFx, new Color(0.86f, 0.74f, 0.45f)));
            Transform dropInto = LivestockKit.Child(feedFx, "DropInto", new Vector3(0f, 0.15f, 0f));

            GameObject shimmer = ChickenCoopModel.Shimmer(model, StandAt + Vector3.up * (StandTop + 0.02f), 0.8f);
            Transform collect = LivestockKit.Child(model, "CollectPoint", StandAt + new Vector3(0f, 0.6f, -0.4f));

            var herd = model.GetComponent<AnimalHerd>();
            if (herd == null) herd = model.gameObject.AddComponent<AnimalHerd>();

            var pen = root.GetComponent<LivestockPen>();
            if (pen == null) pen = root.AddComponent<LivestockPen>();
            pen.Configure("Cow Shelter", milk, "churn of milk", "churns of milk", "the cow", "The cow is lying down for the night.", 60f, Churns,
                          collect, churns, shimmer, burst);
            pen.ConfigureFeed(wheat, "wheat", Feeds, 1, bundles, toss, dropInto, drop);
            pen.ConfigureUi(LivestockKit.Child(model, "UiAnchor", new Vector3(0.6f, 2.8f, 0.3f)), card, bubble, filled);

            herd.Configure(pen, new[] { cow.transform },
                new Rect(-FenceX + 0.08f, -FenceZ + 0.08f, (FenceX - 0.08f) * 2f, (FenceZ - 0.08f) * 2f),
                new[]
                {
                    new Vector3(ShedX0 + 0.05f, ShedZ0 + 0.05f, 0.1f), new Vector3(-0.67f, ShedZ0 + 0.05f, 0.1f), new Vector3(ShedX1 - 0.05f, ShedZ0 + 0.05f, 0.1f),
                    new Vector3(TroughAt.x, TroughAt.z - 0.3f, 0.3f), new Vector3(TroughAt.x, TroughAt.z + 0.3f, 0.3f),
                    new Vector3(BalesAt.x, BalesAt.z, 0.7f), new Vector3(StandAt.x, StandAt.z, 0.5f), new Vector3(StoolAt.x, StoolAt.z, 0.3f),
                },
                new[] { new AnimalHerd.Bed(new Vector3(-0.85f, 0f, 1.15f), 95f) },
                new Vector2(0.22f, 0.4f), new Vector2(1.5f, 5f), 2.2f, 80f, 35f, 0.4f, 0.85f, 1.05f, 0.85f);

            // The work: eating at the rack in bouts, her nose up in the hay.
            var atRack = new Vector3((RackX0 + RackX1) * 0.5f, 0f, RackWall - 0.36f - 1.0f);
            herd.ConfigureWork(new AnimalHerd.WorkSpot(atRack, atRack, 0f), false, new Vector2(8f, 14f), new Vector2(3f, 7f), hayFall, 2.5f, null,
                               new Vector2(9f, 15f));

            var tap = root.GetComponent<BoxCollider>();
            if (tap == null) tap = root.AddComponent<BoxCollider>();
            tap.isTrigger = true;
            tap.center = middle + new Vector3(0f, 1f, 0f);
            tap.size = new Vector3(Width - 0.1f, 2f, Depth - 0.1f);

            if (root.GetComponent<PlacedObject_Done>() == null) root.AddComponent<PlacedObject_Done>();
        }

        // --- The shelter -------------------------------------------------------------------------

        static B.Model Shelter()
        {
            var m = new B.Model
            {
                Shade = (p, n) =>
                {
                    float k = Mathf.Lerp(0.78f, 1f, LivestockKit.Smooth(p.y / 0.45f));
                    bool under = p.x > ShedX0 && p.x < ShedX1 && p.z > ShedZ0 - 0.1f && p.z < ShedZ1 && p.y < FrontEave - 0.1f;
                    return under ? k * 0.84f : k;
                },
            };

            F.Yard(m, new Rect(-Width * 0.5f + 0.02f, -Depth * 0.5f + 0.02f, Width - 0.04f, Depth - 0.04f), 33,
                   (x, z) => z > ShedZ0 - 0.2f && x < ShedX1 + 0.2f ? 1.6f : 0.25f + 0.25f * Mathf.Sin(x * 2.1f - z * 1.3f),
                   (x, z) => x > ShedX0 && x < ShedX1 && z > ShedZ0 && z < ShedZ1 ? 0.8f : 0f);

            Frame(m);
            Walls(m);
            Roof(m);
            HayRack(m);
            Paddock(m);
            Trough(m);
            ChurnStand(m);
            Stool(m);

            // Hay: two bales side by side, one across the top, a pitchfork leant on them.
            V.Bale(m, BalesAt + new Vector3(-0.25f, 0f, 0.02f), new Vector3(0.48f, 0.4f, 0.82f), 2f, 61);
            V.Bale(m, BalesAt + new Vector3(0.27f, 0f, -0.03f), new Vector3(0.48f, 0.4f, 0.82f), -3f, 62);
            V.Bale(m, BalesAt + new Vector3(0.02f, 0.4f, 0.05f), new Vector3(0.82f, 0.4f, 0.48f), 6f, 63);
            Pitchfork(m, BalesAt + new Vector3(-0.55f, 0f, -0.25f));

            // A lantern on the middle post, lit at night like the shelters' windows.
            m.Lantern(new Vector3(-0.67f, FrontEave - 0.35f, ShedZ0 - 0.06f), Vector3.back);
            return m;
        }

        /// <summary>The posts: three along the open front, two at the back corners, a beam along the front under the rafters.</summary>
        static void Frame(B.Model m)
        {
            float[] fronts = { ShedX0 + 0.05f, -0.67f, ShedX1 - 0.05f };
            for (int i = 0; i < fronts.Length; i++)
            {
                float z = ShedZ0 + 0.05f;
                m.Box(B.Walnut, new Vector3(fronts[i], RoofAt(z) * 0.5f, z), new Vector3(0.12f, RoofAt(z), 0.12f), 0.02f, 2);
                m.Box(B.Stone, new Vector3(fronts[i], 0.035f, z), new Vector3(0.22f, 0.07f, 0.22f), 0.025f, Quaternion.Euler(0f, 10f * B.Jitter(i), 0f), 2);
            }

            foreach (float x in new[] { ShedX0 + 0.05f, ShedX1 - 0.05f })
            {
                float z = ShedZ1 - 0.05f;
                m.Box(B.Walnut, new Vector3(x, RoofAt(z) * 0.5f, z), new Vector3(0.12f, RoofAt(z), 0.12f), 0.02f, 2);
            }

            // The beam along the front, and knee braces up to it from the posts.
            m.Box(B.Walnut, new Vector3((ShedX0 + ShedX1) * 0.5f, FrontEave - 0.06f, ShedZ0 + 0.05f), new Vector3(ShedX1 - ShedX0 + 0.12f, 0.12f, 0.12f),
                  0.02f, 2);
            for (int i = 0; i < fronts.Length; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    if ((i == 0 && side < 0) || (i == fronts.Length - 1 && side > 0)) continue;
                    var a = new Vector3(fronts[i], FrontEave - 0.42f, ShedZ0 + 0.05f);
                    var b = new Vector3(fronts[i] + side * 0.36f, FrontEave - 0.1f, ShedZ0 + 0.05f);
                    m.Box(B.Walnut * 0.95f, (a + b) * 0.5f, new Vector3(0.07f, (b - a).magnitude, 0.07f), 0.012f, Quaternion.FromToRotation(Vector3.up, b - a), 1);
                }

            // Rafters, front to back, seen from under the open front.
            foreach (float x in new[] { ShedX0 + 0.05f, -0.67f, ShedX1 - 0.05f, -1.5f, 0.15f })
            {
                float za = ShedZ0 - Overhang + 0.05f, zb = ShedZ1 + BackOverhang - 0.03f;
                var a = new Vector3(x, RoofAt(za) + 0.03f, za);
                var b = new Vector3(x, RoofAt(zb) + 0.03f, zb);
                m.Box(B.Walnut, (a + b) * 0.5f, new Vector3(0.07f, 0.1f, (b - a).magnitude), 0.015f, Quaternion.LookRotation((b - a).normalized), 1);
            }
        }

        /// <summary>Boards up the back and the left side to the roof, a half wall on the right, the hay showing over it.</summary>
        static void Walls(B.Model m)
        {
            const float inset = 0.1f;
            m.BoardWall(new Vector3(0f, 0f, ShedZ1), Vector3.right, Vector3.forward, ShedX0 + inset, ShedX1 - inset, 0.04f,
                        _ => RoofAt(ShedZ1) - 0.02f, 0.2f, 71);
            m.BoardWall(new Vector3(ShedX0, 0f, 0f), Vector3.forward, Vector3.left, ShedZ0 + inset, ShedZ1 - inset, 0.04f,
                        z => RoofAt(z - 0.1f) - 0.03f, 0.2f, 72);
            m.BoardWall(new Vector3(ShedX1, 0f, 0f), Vector3.forward, Vector3.right, ShedZ0 + inset, ShedZ1 - inset, 0.04f, _ => 1.05f, 0.2f, 73);
            m.Box(B.Walnut, new Vector3(ShedX1 + 0.02f, 1.08f, (ShedZ0 + ShedZ1) * 0.5f), new Vector3(0.08f, 0.07f, ShedZ1 - ShedZ0), 0.015f, 2);
        }

        /// <summary>One slope of galvanised tin, from over the back wall down past the open front.</summary>
        static void Roof(B.Model m)
        {
            float z0 = ShedZ0 - Overhang, z1 = ShedZ1 + BackOverhang;
            float y0 = RoofAt(z0) + 0.1f, y1 = RoofAt(z1) + 0.1f;
            var top0 = new Vector3(ShedX0 - SideOverhang, y1, z1);
            var top1 = new Vector3(ShedX1 + SideOverhang, y1, z1);
            var down = new Vector3(0f, y0 - y1, z0 - z1);
            F.TinSlope(m, top0, top1, down, down.magnitude, B.Tin, 4, 3, 0.55f);

            // A fascia board along the low front edge.
            float slope = Mathf.Atan2(y1 - y0, z1 - z0) * Mathf.Rad2Deg;
            m.Box(B.Walnut, new Vector3((ShedX0 + ShedX1) * 0.5f, y0 - 0.05f, z0 + 0.02f), new Vector3(ShedX1 - ShedX0 + SideOverhang * 2f, 0.1f, 0.04f),
                  0.012f, Quaternion.Euler(-slope, 0f, 0f), 2);
        }

        /// <summary>A hay rack on the back wall: slats leaning out from it, hay heaped in them.</summary>
        static void HayRack(B.Model m)
        {
            const float x0 = -2.0f, x1 = -0.95f, bottom = 0.75f, top = 1.3f;
            float wall = ShedZ1 - 0.04f;

            m.Box(B.Walnut, new Vector3((x0 + x1) * 0.5f, bottom, wall - 0.2f), new Vector3(x1 - x0, 0.06f, 0.06f), 0.012f, 1);
            m.Box(B.Walnut, new Vector3((x0 + x1) * 0.5f, top, wall - 0.36f), new Vector3(x1 - x0, 0.06f, 0.06f), 0.012f, 1);
            int slats = 7;
            for (int i = 0; i <= slats; i++)
            {
                float x = Mathf.Lerp(x0 + 0.03f, x1 - 0.03f, i / (float)slats);
                var a = new Vector3(x, bottom - 0.02f, wall - 0.04f);
                var b = new Vector3(x, top, wall - 0.36f);
                m.Box(B.Plank(i + 300), (a + b) * 0.5f, new Vector3(0.035f, (b - a).magnitude, 0.035f), 0.008f, Quaternion.FromToRotation(Vector3.up, b - a), 1);
            }

            // The hay in it is the cow's feed, shown one bundle per item of wheat (HayBundle).
        }

        /// <summary>A bundle of hay pushed into the rack, about its middle: lumpy, wisps sticking out.</summary>
        static B.Model HayBundle()
        {
            var m = new B.Model();
            System.Func<Vector3, Vector3, Color> straw = (p, n) => Color.Lerp(F.Straw, F.StrawLight, V.Noise(p * 18f)) * (n.y < 0f ? 0.78f : 1f);
            S.Ball(m, straw, Vector3.zero, new Vector3(0.2f, 0.14f, 0.17f), Quaternion.identity, 14, 10);
            S.Ball(m, straw, new Vector3(0.08f, 0.07f, -0.03f), new Vector3(0.11f, 0.09f, 0.1f), Quaternion.Euler(0f, 25f, 15f), 10, 8);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 15f * B.Jitter(i + 40);
                Vector3 from = Quaternion.Euler(0f, a, 0f) * new Vector3(0.14f, 0.04f * B.Jitter(i), 0f);
                Vector3 to = from + Quaternion.Euler(0f, a, 0f) * new Vector3(0.11f, -0.04f + 0.08f * B.Jitter(i + 9), 0.02f);
                m.Tube(F.StrawLight * 0.95f, from, to, 0.008f, 5);
            }

            return m;
        }

        /// <summary>Post and rail round the paddock, two rails, a five-bar gate on the front.</summary>
        static void Paddock(B.Model m)
        {
            var corners = new[]
            {
                new Vector3(-FenceX, 0f, -FenceZ), new Vector3(FenceX, 0f, -FenceZ), new Vector3(FenceX, 0f, FenceZ), new Vector3(-FenceX, 0f, FenceZ),
            };

            int key = 500;
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = corners[side], b = corners[(side + 1) % 4];
                var along = new List<float>();
                if (side == 0)
                {
                    foreach (float x in new[] { -FenceX, GateX0, GateX1, 1.0f, FenceX }) along.Add((x + FenceX) / (2f * FenceX));
                }
                else
                {
                    int spans = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 1.25f));
                    for (int i = 0; i <= spans; i++) along.Add(i / (float)spans);
                }

                for (int i = 0; i < along.Count; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, along[i]);
                    F.Post(m, p, FenceTop + 0.08f, 0.1f, ++key);
                    if (i == 0) continue;

                    Vector3 q = Vector3.Lerp(a, b, along[i - 1]);
                    if (side == 0 && Mathf.Abs(q.x - GateX0) < 0.01f)
                    {
                        FarmGate(m, q, p);
                        continue;
                    }

                    F.Rail(m, q + Vector3.up * (FenceTop - 0.06f), p + Vector3.up * (FenceTop - 0.06f), 0.11f, 0.045f, ++key);
                    F.Rail(m, q + Vector3.up * 0.48f, p + Vector3.up * 0.48f, 0.11f, 0.045f, ++key);
                }
            }
        }

        /// <summary>A five-bar gate: two stiles, five rails, a brace, hinges and a latch.</summary>
        static void FarmGate(B.Model m, Vector3 hingeFoot, Vector3 latchFoot)
        {
            Vector3 a = hingeFoot + Vector3.right * 0.07f, b = latchFoot - Vector3.right * 0.07f;
            Color wood = B.Plank(700) * 1.06f;
            float bottom = 0.18f, top = FenceTop - 0.04f;
            for (int i = 0; i < 5; i++)
            {
                float y = Mathf.Lerp(bottom, top, i / 4f);
                F.Rail(m, a + Vector3.up * y, b + Vector3.up * y, 0.08f, 0.035f, 710 + i);
            }

            foreach (Vector3 x in new[] { a, b })
                m.Box(wood, x + Vector3.up * ((bottom + top) * 0.5f), new Vector3(0.06f, top - bottom + 0.08f, 0.045f), 0.01f, 1);

            Vector3 p0 = a + Vector3.up * bottom, p1 = b + Vector3.up * top;
            m.Box(wood * 0.95f, (p0 + p1) * 0.5f + Vector3.back * 0.035f, new Vector3(0.06f, (p1 - p0).magnitude, 0.03f), 0.01f,
                  Quaternion.FromToRotation(Vector3.up, p1 - p0), 1);
            foreach (float y in new[] { bottom + 0.05f, top - 0.05f })
                m.Box(B.Iron, a + new Vector3(0.09f, y, -0.03f), new Vector3(0.22f, 0.03f, 0.012f), 0.004f, 1);
            m.Box(B.Iron, b + new Vector3(0.03f, top - 0.15f, -0.035f), new Vector3(0.12f, 0.035f, 0.02f), 0.005f, 1);
        }

        /// <summary>A galvanised water trough on the paddock's left, full.</summary>
        static void Trough(B.Model m)
        {
            Vector3 c = TroughAt;
            var size = new Vector3(0.44f, 0.4f, 1.05f);
            var tin = new SmoothMesh(1);
            tin.RoundedBox(0, c + Vector3.up * (size.y * 0.5f), size, 0.08f, Quaternion.identity, 3);
            m.Add(tin, (p, n) => (n.y > 0.6f ? F.GalvanisedTop : F.Galvanised) * (0.92f + 0.09f * V.Noise(p * 9f)) *
                                 Mathf.Lerp(0.8f, 1f, LivestockKit.Smooth(p.y / 0.3f)));
            m.Box(F.Water, c + Vector3.up * (size.y - 0.015f), new Vector3(size.x - 0.07f, 0.02f, size.z - 0.07f), 0.03f, 2);
            m.Box(F.GalvanisedDark, c + Vector3.up * (size.y + 0.002f), new Vector3(size.x + 0.02f, 0.025f, 0.05f), 0.008f, 1);
        }

        /// <summary>A low stand of boards on short legs, for the churns to stand on by the gate.</summary>
        static void ChurnStand(B.Model m)
        {
            Vector3 c = StandAt;
            const float w = 1.0f, d = 0.5f;
            for (int i = 0; i < 4; i++)
                m.Box(B.Plank(800 + i) * 1.03f, c + new Vector3(0f, StandTop - 0.02f, -d * 0.5f + (i + 0.5f) * d / 4f), new Vector3(w, 0.035f, d / 4f - 0.012f),
                      0.006f, 1);
            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -w * 0.45f : w * 0.45f, z = (k & 2) == 0 ? -d * 0.4f : d * 0.4f;
                m.Box(B.Walnut, c + new Vector3(x, (StandTop - 0.04f) * 0.5f, z), new Vector3(0.06f, StandTop - 0.04f, 0.06f), 0.01f, 1);
            }

            m.Box(B.Walnut, c + new Vector3(0f, StandTop - 0.06f, -d * 0.4f), new Vector3(w * 0.9f, 0.05f, 0.04f), 0.008f, 1);
        }

        /// <summary>A three-legged milking stool and a galvanised pail beside it.</summary>
        static void Stool(B.Model m)
        {
            Vector3 c = StoolAt;
            m.Lathe((p, n) => B.Plank(901) * (n.y < -0.4f ? 0.7f : 1.05f), c + Vector3.up * 0.28f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.14f, 0f), new Vector2(0.145f, 0.02f), new Vector2(0.14f, 0.04f), new Vector2(0f, 0.04f),
            }, 14);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                var foot = c + new Vector3(Mathf.Cos(a) * 0.15f, 0f, Mathf.Sin(a) * 0.15f);
                m.Tube(B.Walnut, foot, c + new Vector3(Mathf.Cos(a) * 0.08f, 0.29f, Mathf.Sin(a) * 0.08f), 0.02f, 6);
            }

            Vector3 pail = c + new Vector3(0.32f, 0f, 0.05f);
            m.Lathe((p, n) => (n.y > 0.6f ? F.GalvanisedTop : F.Galvanised) * (0.92f + 0.08f * V.Noise(p * 12f)) * (n.y < -0.3f ? 0.7f : 1f), pail,
                    new List<Vector2>
                    {
                        new Vector2(0f, 0f), new Vector2(0.11f, 0f), new Vector2(0.14f, 0.26f), new Vector2(0.145f, 0.27f), new Vector2(0.135f, 0.27f),
                        new Vector2(0.11f, 0.03f), new Vector2(0f, 0.03f),
                    }, 16);
            S.Disc(m, (p, n) => B.Hex("D0CBBE"), pail + Vector3.up * 0.2f, Vector3.up, 0.128f, 14);
            V.Sweep(m, F.GalvanisedDark, new[]
            {
                pail + new Vector3(-0.14f, 0.26f, 0f), pail + new Vector3(-0.08f, 0.38f, 0f), pail + new Vector3(0.08f, 0.38f, 0f), pail + new Vector3(0.14f, 0.26f, 0f),
            }, 0.007f, 6, 4);
        }

        static void Pitchfork(B.Model m, Vector3 foot)
        {
            Vector3 top = foot + new Vector3(0.12f, 1.25f, 0.18f);
            m.Tube(B.HoneyLight * 0.9f, foot + Vector3.up * 0.25f, top, 0.02f, 8);
            Vector3 head = foot + Vector3.up * 0.25f;
            m.Box(B.Iron, head, new Vector3(0.2f, 0.03f, 0.03f), 0.008f, 1);
            for (int i = 0; i < 3; i++)
                m.Tube(B.Iron, head + new Vector3(-0.08f + i * 0.08f, 0f, 0f), new Vector3(head.x - 0.08f + i * 0.08f, 0.02f, head.z - 0.04f), 0.008f, 5);
        }

        /// <summary>A milk churn: the vehicles' galvanised churn, a little bigger, stood on its own so each can appear in turn.</summary>
        static B.Model Churn()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.85f, 1f, LivestockKit.Smooth(p.y / 0.2f)) };
            V.Churn(m, Vector3.zero, 1.05f);
            return m;
        }

        [MenuItem("Last Orders/Farming/Build Cow Shelter (Model & Prefab)", priority = 100)]
        static void Run() => Debug.Log("[Last Orders] " + Apply(AssetDatabase.LoadAssetAtPath<ItemDataSO>(LivestockContent.MilkPath),
                                                                AssetDatabase.LoadAssetAtPath<ItemDataSO>(LivestockContent.WheatPath)));
    }
}
