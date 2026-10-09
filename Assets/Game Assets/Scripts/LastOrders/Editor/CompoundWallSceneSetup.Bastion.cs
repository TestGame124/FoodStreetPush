using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Effects;
using LastOrders.Perimeter;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Bastions: a stretch of the compound wall thickened to carry a defense on top.
    ///
    /// The same concrete as the wall round it but broader and a little taller, with a parapet along
    /// its outer edge, steel straps bolted up its face and a level top for whatever is mounted there -
    /// modelled by <see cref="CompoundWallModels"/>, or in the previous look boxes over the block
    /// texture with darker pillars at its corners. It is a
    /// <see cref="CompoundWall"/> of its own, laid over the wall it thickens: zombies batter it,
    /// it cracks below half, falls to rubble and is rebuilt by a worker like any section - and the
    /// ordinary wall inside it is still there once it is down.
    /// </summary>
    public static partial class CompoundWallSceneSetup
    {
        /// <summary>The top of a bastion: what a defense on it stands on.</summary>
        public const float BastionHeight = 2.3f;

        const float ParapetHeight = 0.26f, ParapetDepth = 0.24f;
        const float BastionPillar = 0.5f;

        /// <summary>
        /// A bastion <paramref name="length"/> along the wall and <paramref name="thickness"/> through it,
        /// standing on <paramref name="centre"/> (the middle of its footprint, on the ground), turned by
        /// <paramref name="yaw"/> so its local +Z faces out of the compound. Replaces one of the same name
        /// under <paramref name="parent"/>.
        /// </summary>
        public static CompoundWall BuildBastion(Transform parent, string name, Vector3 centre, float yaw, float length, float thickness,
                                                float health, GameLifetimeScope scope, PooledEffect dust, int seed)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));

            Transform visual = K.Group("Visual", go.transform);
            BastionLooks(go.transform, visual, length, thickness, seed, out GameObject intact, out GameObject damaged, out GameObject rubble);

            // Over the top of whatever stands on it.
            Transform bar = K.Group("HealthBarAnchor", go.transform);
            bar.localPosition = new Vector3(0f, 1.3f, 0f);

            var wall = go.AddComponent<CompoundWall>();
            wall.ConfigureStructure(scope, "Bastion", length, thickness, null, visual.gameObject, bar, dust);
            wall.ConfigureWall(null, intact, damaged, rubble);

            var so = new SerializedObject(wall);
            so.FindProperty("fallbackHealth").floatValue = health;
            so.ApplyModifiedPropertiesWithoutUndo();

            return wall;
        }

        /// <summary>
        /// A standing bastion's looks built again in the current look, in place: the wall piece, its
        /// health and everything pointing at it stay as they are.
        /// </summary>
        public static void RedressBastion(CompoundWall wall, int seed)
        {
            Transform visual = wall.transform.Find("Visual");
            if (visual == null) return;

            var so = new SerializedObject(wall);
            float length = so.FindProperty("length").floatValue;
            float thickness = so.FindProperty("thickness").floatValue;

            for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);

            BastionLooks(wall.transform, visual, length, thickness, seed, out GameObject intact, out GameObject damaged, out GameObject rubble);
            wall.ConfigureWall(null, intact, damaged, rubble);
            EditorUtility.SetDirty(wall);
        }

        /// <summary>Its three looks under <paramref name="visual"/>: as built, broken below half, fallen.</summary>
        static void BastionLooks(Transform bastion, Transform visual, float length, float thickness, int seed,
                                 out GameObject intact, out GameObject damaged, out GameObject rubble)
        {
            if (!PreviousLook)
            {
                Material[] materials = { BuildingKit.PaintedMaterial() };
                int variant = Mathf.Abs(seed) % 2;

                intact = CompoundWallModels.Part(visual, "Intact",
                                                 CompoundWallModels.BastionMesh(length, thickness, CompoundWallModels.State.Intact, variant), materials);
                damaged = CompoundWallModels.Part(visual, "Damaged",
                                                  CompoundWallModels.BastionMesh(length, thickness, CompoundWallModels.State.Damaged, variant), materials);
                rubble = CompoundWallModels.Part(visual, "Rubble",
                                                 CompoundWallModels.BastionMesh(length, thickness, CompoundWallModels.State.Rubble, variant), materials);
                damaged.SetActive(false);
                rubble.SetActive(false);
                return;
            }

            blockWall = SurfaceKit.CompoundWall(TextureGenerator.WallPanel.Concrete);
            blockWallDark = SurfaceKit.CompoundWallDark();

            // Where it sits along the whole wall, so the blocks carry on from the sections either side.
            Vector3 right = bastion.right, centre = bastion.position;
            float along = Mathf.Abs(right.x) > 0.5f ? centre.x * Mathf.Sign(right.x) : centre.z * Mathf.Sign(right.z);
            var rng = new System.Random(seed * 7919 + 31);

            Transform whole = K.Group("Intact", visual);
            BastionIntact(whole, length, thickness);
            MeshBaker.Bake(whole, WallRemap, along, TextureGenerator.WallTileLength, BastionHeight);

            Transform broken = K.Group("Damaged", visual);
            BastionDamaged(broken, length, thickness, rng);
            MeshBaker.Bake(broken, WallRemap, along, TextureGenerator.WallTileLength, BastionHeight);
            broken.gameObject.SetActive(false);

            Transform fallen = K.Group("Rubble", visual);
            BastionRubble(fallen, length, thickness, rng);
            MeshBaker.Bake(fallen, WallRemap, along, TextureGenerator.WallTileLength, BastionHeight);
            fallen.gameObject.SetActive(false);

            intact = whole.gameObject;
            damaged = broken.gameObject;
            rubble = fallen.gameObject;
        }

        static void BastionIntact(Transform p, float length, float thickness)
        {
            float body = BastionHeight - FootingHeight - CapHeight;
            float outer = thickness * 0.5f;

            K.Box("Footing", p, Vector3.zero, new Vector3(length + 0.14f, FootingHeight, thickness + 0.14f), Footing);
            K.Box("Body", p, new Vector3(0f, FootingHeight, 0f), new Vector3(length, body, thickness), Panel);
            K.Box("Cap", p, new Vector3(0f, BastionHeight - CapHeight, 0f), new Vector3(length + 0.04f, CapHeight, thickness + 0.06f), Cap);

            // A parapet along the outer edge and back along both ends, to stand behind.
            K.Box("Parapet", p, new Vector3(0f, BastionHeight, outer - ParapetDepth * 0.5f), new Vector3(length, ParapetHeight, ParapetDepth), Panel);
            K.Box("ParapetCap", p, new Vector3(0f, BastionHeight + ParapetHeight, outer - ParapetDepth * 0.5f),
                  new Vector3(length + 0.04f, 0.06f, ParapetDepth + 0.05f), Cap);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (length * 0.5f - ParapetDepth * 0.5f);
                K.Box($"Return_{side}", p, new Vector3(x, BastionHeight, outer * 0.5f), new Vector3(ParapetDepth, ParapetHeight * 0.8f, outer), Panel);
            }

            // Pillars at its corners, a shade darker, a cap on each.
            for (int k = 0; k < 4; k++)
            {
                float x = ((k & 1) == 0 ? -1f : 1f) * length * 0.5f, z = ((k & 2) == 0 ? -1f : 1f) * outer;
                K.Box($"Pillar_{k}", p, new Vector3(x, 0f, z), new Vector3(BastionPillar, BastionHeight + 0.24f, BastionPillar), Pillar);
                K.Box($"PillarCap_{k}", p, new Vector3(x, BastionHeight + 0.24f, z), new Vector3(BastionPillar + 0.1f, 0.08f, BastionPillar + 0.1f), Cap);
            }

            // Steel straps bolted up its outer face: it was built to carry weight.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * length * 0.22f;
                K.Box($"Strap_{side}", p, new Vector3(x, FootingHeight + 0.1f, outer + 0.02f), new Vector3(0.14f, body - 0.2f, 0.04f), K.MetalDark);
                for (int b = 0; b < 3; b++)
                    K.Box($"Bolt_{side}_{b}", p, new Vector3(x, FootingHeight + 0.35f + b * 0.55f, outer + 0.045f), new Vector3(0.06f, 0.06f, 0.03f), K.Metal);
            }
        }

        /// <summary>Below half: the top broken back to jagged stumps, the parapet mostly gone, a crack, chunks at its foot.</summary>
        static void BastionDamaged(Transform p, float length, float thickness, System.Random rng)
        {
            float body = BastionHeight - FootingHeight - CapHeight;
            float outer = thickness * 0.5f;

            K.Box("Footing", p, Vector3.zero, new Vector3(length + 0.14f, FootingHeight, thickness + 0.14f), Footing);
            K.Box("Core", p, new Vector3(0f, FootingHeight, 0f), new Vector3(length, body * 0.62f, thickness), Panel);

            float third = length / 3f;
            float[] heights = { 0.38f, 0.2f, 0.32f };
            for (int k = 0; k < 3; k++)
            {
                float x = -length * 0.5f + third * (k + 0.5f);
                float h = body * heights[(k + rng.Next(3)) % 3];
                K.Box($"Stump_{k}", p, new Vector3(x, FootingHeight + body * 0.62f, 0f), new Vector3(third + 0.01f, h, thickness - 0.1f), Panel);

                GameObject jag = K.Cube($"Jag_{k}", p, new Vector3(x + third * 0.15f, FootingHeight + body * 0.62f + h, 0f),
                                        new Vector3(third * 0.5f, 0.2f, thickness * 0.8f), Panel);
                jag.transform.localRotation = Quaternion.Euler(0f, 0f, (k % 2 == 0 ? 1f : -1f) * 12f);
            }

            K.Box("ParapetStub", p, new Vector3(-length * 0.25f, BastionHeight - 0.1f, outer - ParapetDepth * 0.5f),
                  new Vector3(length * 0.4f, ParapetHeight, ParapetDepth), Panel);

            for (int k = 0; k < 4; k++)
            {
                float x = ((k & 1) == 0 ? -1f : 1f) * length * 0.5f, z = ((k & 2) == 0 ? -1f : 1f) * outer;
                K.Box($"Pillar_{k}", p, new Vector3(x, 0f, z), new Vector3(BastionPillar, BastionHeight * (0.7f + 0.2f * k / 3f), BastionPillar), Pillar);
            }

            GameObject crack = K.Cube("Crack", p, new Vector3(-length * 0.15f, 0.8f, outer + 0.01f), new Vector3(0.06f, 0.9f, 0.02f), Crack);
            crack.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);

            for (int k = 0; k < 4; k++)
            {
                float x = (float)(rng.NextDouble() - 0.5) * length * 0.8f;
                float z = outer + 0.25f + (float)rng.NextDouble() * 0.45f;
                float s = 0.25f + (float)rng.NextDouble() * 0.2f;
                K.Box($"Chunk_{k}", p, new Vector3(x, 0f, z), new Vector3(s * 1.3f, s, s), Panel, yaw: rng.Next(0, 90));
            }
        }

        /// <summary>Fallen: the footing, pillar stumps, a heap of broken blocks and bent rebar.</summary>
        static void BastionRubble(Transform p, float length, float thickness, System.Random rng)
        {
            float outer = thickness * 0.5f;
            K.Box("Footing", p, Vector3.zero, new Vector3(length + 0.14f, 0.18f, thickness + 0.14f), Footing);

            for (int k = 0; k < 4; k++)
            {
                float x = ((k & 1) == 0 ? -1f : 1f) * length * 0.5f, z = ((k & 2) == 0 ? -1f : 1f) * outer;
                K.Box($"Stump_{k}", p, new Vector3(x, 0f, z), new Vector3(BastionPillar, 0.5f + 0.25f * (k % 2), BastionPillar), Pillar);
            }

            Color[] colours = { Panel, Footing, Cap };
            for (int k = 0; k < 9; k++)
            {
                float x = (float)(rng.NextDouble() - 0.5) * (length - 0.4f);
                float z = (float)(rng.NextDouble() - 0.5) * thickness;
                var size = new Vector3(0.45f + (float)rng.NextDouble() * 0.45f, 0.22f + (float)rng.NextDouble() * 0.3f,
                                       0.35f + (float)rng.NextDouble() * 0.35f);
                GameObject block = K.Box($"Block_{k}", p, new Vector3(x, 0.1f, z), size, colours[k % colours.Length]);
                block.transform.localRotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 26f, rng.Next(0, 180), 0f);
            }

            for (int k = 0; k < 3; k++)
                K.Log($"Rebar_{k}", p, new Vector3((k - 1) * length * 0.3f, 0.45f, 0f), 0.04f, 0.9f, K.MetalDark, yaw: rng.Next(0, 180), tilt: 35f);
        }

        // --- An empty site ----------------------------------------------------------------------------

        /// <summary>
        /// Where a bastion will go, staked out: pegs at the corners of its footprint either side of the
        /// wall, twine round them, and the makings stacked inside - a pallet of blocks and a few bags.
        /// In the site's own frame: on the wall's centreline, +Z out of the compound, the footprint from
        /// <paramref name="inner"/> to <paramref name="outer"/> metres out.
        /// </summary>
        public static B.Model BastionMarker(float length, float inner, float outer)
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(p.y / 0.4f)) };
            Color peg = B.WalnutDark, tip = K.Hex("A88B45"), twine = B.Twine;

            var corners = new[]
            {
                new Vector3(-length * 0.5f, 0f, -inner), new Vector3(length * 0.5f, 0f, -inner),
                new Vector3(length * 0.5f, 0f, outer), new Vector3(-length * 0.5f, 0f, outer),
            };

            var knots = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                Quaternion lean = Quaternion.Euler(4f * B.Jitter(i + 1), 0f, 4f * B.Jitter(i + 2));
                m.Box(peg, corners[i] + lean * (Vector3.up * 0.3f), new Vector3(0.05f, 0.6f, 0.05f), 0.012f, lean);
                m.Box(tip, corners[i] + lean * (Vector3.up * 0.57f), new Vector3(0.06f, 0.07f, 0.06f), 0.012f, lean);
                knots[i] = corners[i] + lean * (Vector3.up * 0.45f);
            }

            // Twine round the outside and inside; the wall stands between the two long runs.
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = knots[i], b = knots[(i + 1) % 4];
                if (i % 2 == 1) continue;
                Vector3 sag = (a + b) * 0.5f + Vector3.down * 0.06f;
                m.Tube(twine, a, sag, 0.008f, 4);
                m.Tube(twine, sag, b, 0.008f, 4);
            }

            // The makings, inside: a pallet of blocks, two bags of cement.
            var pallet = new Vector3(length * 0.18f, 0f, -inner - 0.55f);
            for (int k = 0; k < 4; k++)
                m.Box(B.Plank(k + 3), pallet + new Vector3(0f, 0.05f, -0.3f + k * 0.2f), new Vector3(0.9f, 0.05f, 0.14f), 0.01f);
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 3; k++)
                    m.Box(K.Hex("94928C") * (1f + 0.04f * B.Jitter(row * 3 + k)), pallet + new Vector3(-0.28f + k * 0.28f, 0.17f + row * 0.19f, 0f),
                          new Vector3(0.26f, 0.18f, 0.5f), 0.02f, Quaternion.Euler(0f, 3f * B.Jitter(k + row), 0f));
            B.Sack(m, pallet + new Vector3(-0.75f, 0f, 0.05f), 0.15f, 0.32f, 0.03f, 6);
            B.Sack(m, pallet + new Vector3(-0.72f, 0f, -0.32f), 0.14f, 0.3f, -0.02f, 9);
            return m;
        }
    }
}
