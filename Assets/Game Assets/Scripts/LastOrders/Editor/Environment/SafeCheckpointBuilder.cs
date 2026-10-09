using FoodStreet.Visitors.EditorTools;
using LastOrders.Core;
using TMPro;
using UnityEngine;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Where the customers come from: the survivors' checkpoint on the road west.
    ///
    /// The fiction needs somewhere for travellers to be travelling from, and the fog hides what is
    /// behind it, so this is the one part of the settlement anybody sees: a scrap wall across the
    /// road with its gate open by day, a watchtower, floodlights, a fire going, and a sign. It is
    /// the only lit thing out in the dark, which is the point - at night the suburbs go black and
    /// the checkpoint does not.
    ///
    /// Built in its own space: the road runs along local X, local +X points east toward the diner.
    /// </summary>
    public static class SafeCheckpointBuilder
    {
        public static Transform Build(Transform parent, ref DeterministicRandom rng)
        {
            Transform root = K.Group("SafeCheckpoint", parent);
            root.localPosition = L.Checkpoint;

            float roadHalf = ZoneLayout.RoadWidth * 0.5f;

            // --- The wall across the road -----------------------------------------------------------------

            Transform wall = K.Group("Wall", root);
            ScrapWall(wall, "North", roadHalf + 1f, 13f, ref rng);
            ScrapWall(wall, "South", -roadHalf - 1f, -15f, ref rng);

            // --- The gate ---------------------------------------------------------------------------------

            Transform gate = K.Group("Gate", root);
            foreach (float z in new[] { roadHalf + 0.6f, -roadHalf - 0.6f })
                K.Box($"GatePost_{(z > 0 ? "N" : "S")}", gate, new Vector3(0f, 0f, z), new Vector3(0.45f, 3.4f, 0.45f), K.WoodDark);

            // Leaves swung back toward the settlement: open by day.
            foreach (float side in new[] { 1f, -1f })
            {
                Transform leaf = K.Group($"Leaf_{(side > 0 ? "N" : "S")}", gate);
                leaf.localPosition = new Vector3(0f, 0f, side * (roadHalf + 0.4f));
                leaf.localRotation = Quaternion.Euler(0f, side * 68f, 0f);

                for (int i = 0; i < 3; i++)
                {
                    K.Box($"Sheet_{i}", leaf, new Vector3(-0.1f, 0f, -side * (0.45f + i * 0.85f)), new Vector3(0.08f, rng.Range(2.1f, 2.5f), 0.85f),
                          E.Pick(E.Scrap, ref rng));
                }
            }

            // The boom, raised.
            Transform boom = K.Group("Boom", gate);
            boom.localPosition = new Vector3(0.8f, 1.05f, roadHalf + 0.9f);
            boom.localRotation = Quaternion.Euler(72f, 0f, 0f);
            K.Box("Pivot", gate, new Vector3(0.8f, 0f, roadHalf + 0.9f), new Vector3(0.35f, 1.05f, 0.35f), K.MetalDark);
            for (int i = 0; i < 6; i++)
            {
                K.Cube($"Stripe_{i}", boom, new Vector3(0f, 0f, -0.35f - i * 0.7f), new Vector3(0.14f, 0.14f, 0.7f),
                       i % 2 == 0 ? K.Lock : K.Dark);
            }

            // --- The tower ------------------------------------------------------------------------------------

            Watchtower(root, new Vector3(-3.2f, 0f, roadHalf + 4f), ref rng);

            // --- Lights -------------------------------------------------------------------------------------

            Transform lights = K.Group("Lights", root);
            Material pool = E.SoftSpot("LampPool", new Color(1f, 0.82f, 0.48f, 0.55f), additive: true, nightOnly: true, fogTint: false);
            Material firePool = E.SoftSpot("FirePool", new Color(1f, 0.55f, 0.2f, 0.6f), additive: true, nightOnly: true, fogTint: false);

            Floodlight(lights, new Vector3(1.4f, 0f, roadHalf + 1.8f), 160f, pool);
            Floodlight(lights, new Vector3(1.4f, 0f, -roadHalf - 1.8f), 20f, pool);
            E.GroundSpot("GatePool", lights, new Vector3(2.5f, 0.06f, 0f), 11f, pool);

            // --- Behind the wall -------------------------------------------------------------------------------

            Transform yard = K.Group("Yard", root);

            FireBarrel(yard, new Vector3(-4.5f, 0f, -roadHalf - 3f), firePool);
            FireBarrel(yard, new Vector3(-6.5f, 0f, roadHalf + 7.5f), firePool);

            ObstacleKit.Crates(yard, "Supplies", new Vector2(-6f, -roadHalf - 6.5f), ref rng);
            ObstacleKit.Crates(yard, "Supplies", new Vector2(-8.5f, roadHalf + 3.5f), ref rng);

            K.Cylinder("WaterTank", yard, new Vector3(-9f, 0f, -roadHalf - 9f), 2.2f, 2.4f, K.Hex("5E7A86"));
            K.Cylinder("TankBand", yard, new Vector3(-9f, 1.1f, -roadHalf - 9f), 2.26f, 0.12f, K.MetalDark);

            ObstacleKit.Car(yard, "SettlementTruck", new Vector2(-4.5f, roadHalf + 10.5f), 90f + rng.Range(-8f, 8f), ref rng, allowPack: false);

            // Where the gate crew sleep: two shacks, doors toward the road, windows lit.
            AbandonedHouseBuilder.Shed(yard, "Shack_North", new Vector2(-10.5f, roadHalf + 8.5f), -90f, new Vector2(4f, 3.2f), 2.4f,
                                       E.Weathered[0], true, ref rng);
            AbandonedHouseBuilder.Shed(yard, "Shack_South", new Vector2(-9.5f, -roadHalf - 13.5f), -90f, new Vector2(3.6f, 3f), 2.3f,
                                       E.Weathered[2], true, ref rng);
            ObstacleKit.Sandbags(yard, "Sandbags", new Vector2(2.2f, roadHalf + 6f), 90f, ref rng);
            ObstacleKit.Sandbags(yard, "Sandbags", new Vector2(2.2f, -roadHalf - 7f), 90f, ref rng);

            Flag(root, new Vector3(0.3f, 0f, roadHalf + 2.6f), K.Hex("E07B39"));
            Flag(root, new Vector3(0.3f, 0f, -roadHalf - 2.6f), K.Hex("3F6FA8"));

            // --- The sign ------------------------------------------------------------------------------------

            Transform sign = K.Group("Sign", root);
            sign.localPosition = new Vector3(1.2f, 0f, roadHalf + 4.5f);
            sign.localRotation = Quaternion.Euler(0f, -40f, 0f);

            K.Cylinder("PostL", sign, new Vector3(-1.4f, 0f, 0f), 0.16f, 2.8f, K.WoodDark);
            K.Cylinder("PostR", sign, new Vector3(1.4f, 0f, 0f), 0.16f, 2.8f, K.WoodDark);
            K.Cube("Board", sign, new Vector3(0f, 2.1f, 0f), new Vector3(3.4f, 1.2f, 0.12f), K.Hex("E8E1CF"));
            SignText.Add(sign, "SAFE ZONE", new Vector3(0f, 2.28f, -0.08f), 0.5f, K.Hex("2F7D55"));
            SignText.Add(sign, "survivors welcome", new Vector3(0f, 1.78f, -0.08f), 0.22f, K.Dark);

            return root;
        }

        /// <summary>A wall of whatever came to hand: sheets of tin, doors, planks, on posts, of uneven height.</summary>
        static void ScrapWall(Transform parent, string name, float from, float to, ref DeterministicRandom rng)
        {
            Transform run = K.Group(name, parent);
            float dir = Mathf.Sign(to - from);
            float z = from;
            int i = 0;

            while ((to - z) * dir > 0.4f)
            {
                float width = Mathf.Min(rng.Range(1.1f, 1.9f), Mathf.Abs(to - z));
                float height = rng.Range(2.0f, 2.8f);

                E.Block($"Sheet_{i}", run, new Vector3(rng.Range(-0.08f, 0.08f), height * 0.5f, z + dir * width * 0.5f),
                        new Vector3(0.1f, height, width), new Vector3(rng.Range(-3f, 3f), rng.Range(-4f, 4f), 0f), E.Pick(E.Scrap, ref rng));

                if (i % 2 == 0)
                    K.Cylinder($"Post_{i}", run, new Vector3(0.18f, 0f, z), 0.16f, height + 0.3f, K.WoodDark);

                z += dir * width;
                i++;
            }

            // A line of barbed wire along the top, sagging between the posts.
            float mid = (from + to) * 0.5f;
            K.Log("Wire", run, new Vector3(0.18f, 2.95f, mid), 0.03f, Mathf.Abs(to - from), K.MetalDark, 0f);
        }

        static void Watchtower(Transform parent, Vector3 at, ref DeterministicRandom rng)
        {
            Transform tower = K.Group("Watchtower", parent);
            tower.localPosition = at;
            tower.localRotation = Quaternion.Euler(0f, rng.Range(-6f, 6f), 0f);

            const float legs = 3.6f;
            const float roofAt = legs + 2f;
            foreach (float dx in new[] { -1f, 1f })
                foreach (float dz in new[] { -1f, 1f })
                    K.Cylinder("Leg", tower, new Vector3(dx, 0f, dz), 0.2f, roofAt, K.WoodDark);

            // A tin roof on the corner posts: a lookout stands up there all night.
            var roof = new EnvMesh(2);
            roof.HipRoof(new EnvMesh.RoofParts { Covering = 0, Gable = 1, Trim = 1 }, new Vector3(0f, roofAt, 0f), Quaternion.identity,
                         2.4f, 2.4f, 0.8f, 0.4f);
            AbandonedHouseBuilder.Attach(tower, "Roof", roof, new[]
            {
                E.Surface("Metal", TextureGenerator.EnsureMetal(), Color.white, 0.8f), K.MaterialFor(K.WoodDark),
            });

            K.Cube("Deck", tower, new Vector3(0f, legs, 0f), new Vector3(2.6f, 0.18f, 2.6f), K.Wood);

            // Rails on three sides, the ladder side left clear.
            K.Box("Rail_N", tower, new Vector3(0f, legs + 0.09f, 1.25f), new Vector3(2.6f, 0.9f, 0.1f), K.WoodDark);
            K.Box("Rail_S", tower, new Vector3(0f, legs + 0.09f, -1.25f), new Vector3(2.6f, 0.9f, 0.1f), K.WoodDark);
            K.Box("Rail_W", tower, new Vector3(-1.25f, legs + 0.09f, 0f), new Vector3(0.1f, 0.9f, 2.6f), K.WoodDark);
            E.Block("Ladder", tower, new Vector3(1.55f, legs * 0.5f, 0f), new Vector3(0.1f, legs + 0.6f, 0.5f), new Vector3(0f, 0f, -16f), K.Wood);

            GameObject lamp = K.Cube("Searchlight", tower, new Vector3(0.6f, legs + 0.9f, 0.6f), new Vector3(0.45f, 0.35f, 0.55f), K.MetalDark, 35f);
            GameObject lens = K.Cube("Lens", lamp.transform, new Vector3(0f, 0f, 0.52f), new Vector3(0.8f, 0.8f, 0.05f), K.Lock);
            lens.GetComponent<Renderer>().sharedMaterial = E.Glow(E.LampGlow);

            ObstacleKit.Sandbags(tower, "Sandbags", new Vector2(0f, 0f), 0f, ref rng);
        }

        static void Floodlight(Transform parent, Vector3 at, float facing, Material pool)
        {
            Transform light = K.Group("Floodlight", parent);
            light.localPosition = at;
            light.localRotation = Quaternion.Euler(0f, facing, 0f);

            K.Cylinder("Pole", light, Vector3.zero, 0.14f, 4.6f, K.MetalDark);
            GameObject head = K.Cube("Head", light, new Vector3(0f, 4.55f, 0.2f), new Vector3(0.7f, 0.35f, 0.3f), K.Lock);
            head.GetComponent<Renderer>().sharedMaterial = E.Glow(E.LampGlow);

            E.GroundSpot("Pool", light, new Vector3(0f, 0.07f, 3f), 8f, pool);
        }

        static void FireBarrel(Transform parent, Vector3 at, Material pool)
        {
            Transform fire = K.Group("FireBarrel", parent);
            fire.localPosition = at;

            if (E.Pack(E.Drums[2], fire, Vector3.zero, 0f, 1f) == null)
                K.Cylinder("Drum", fire, Vector3.zero, 0.62f, 0.95f, E.Rust[0]);

            GameObject flame = K.Sphere("Flame", fire, new Vector3(0f, 1.05f, 0f), new Vector3(0.5f, 0.35f, 0.5f), K.Lock);
            flame.GetComponent<Renderer>().sharedMaterial = E.Glow(E.FireGlow);

            E.GroundSpot("Glow", fire, new Vector3(0f, 0.07f, 0f), 5f, pool);
        }

        static void Flag(Transform parent, Vector3 at, Color colour)
        {
            Transform flag = K.Group("Flag", parent);
            flag.localPosition = at;

            K.Cylinder("Pole", flag, Vector3.zero, 0.08f, 5f, K.Metal);
            K.Cube("Cloth", flag, new Vector3(0f, 4.55f, 0.55f), new Vector3(0.03f, 0.7f, 1.1f), colour);
        }
    }

    /// <summary>Words on a board, sized to fit the width they are given.</summary>
    public static class SignText
    {
        public static TextMeshPro Add(Transform parent, string text, Vector3 localPosition, float height, Color colour)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = colour;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 40f;
            tmp.rectTransform.sizeDelta = new Vector2(3f, height * (text.Contains("\n") ? 2.2f : 1.1f));

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return tmp;
        }
    }
}
