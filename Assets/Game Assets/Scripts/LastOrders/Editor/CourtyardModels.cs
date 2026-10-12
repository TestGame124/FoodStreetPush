using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Expansion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using B = LastOrders.EditorTools.BuildingKit;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The courtyard's two pieces of set dressing in the smooth low-poly style, replacing the primitive
    /// blocks: the old concrete barrier row across the yard (shown until the land is claimed, with
    /// broken sections and rebar), and the kitchen-garden beds (shown once it is).
    ///
    /// Each is one mesh built round the yard's centre and laid out from the yard's side facing the
    /// compound, measured from its wall, so it lines up wherever the yard is. Re-running rewrites the
    /// meshes in place and swaps the scene objects.
    /// </summary>
    public static class CourtyardModels
    {
        const string Folder = ObstacleModels.Folder + "/Courtyard";

        const string AreaPath = "ExpansionAreas/Area_Courtyard";

        static readonly Color Concrete = B.Hex("7E7D79");
        static readonly Color ConcreteDark = B.Hex("65645F");
        static readonly Color Paint = B.Hex("B8B49E");
        static readonly Color Moss = B.Hex("5D7B2F");
        static readonly Color Soil = B.Hex("57453A");
        static readonly Color SoilLight = B.Hex("6A5545");
        static readonly Color[] Leaves = { B.Hex("4A7530"), B.Hex("55823A"), B.Hex("3F6B2A") };

        /// <summary>Barrier blocks along the line the old row stood on, in world x, either side of the yard's gate at x 30.5.</summary>
        static readonly float[] BlockX = { 23.9f, 26.5f, 29.2f, 32.4f, 34.5f, 37.8f };

        /// <summary>Metres in from the yard's side facing the compound: the row's line, and the beds' middles.</summary>
        public const float RowFromFront = 3.8f, BedsFromFront = 8.8f;

        /// <summary>The beds' middles in world x.</summary>
        static readonly float[] BedX = { 26f, 32f };

        /// <summary>The yard's side facing the compound, measured from its wall on every run.</summary>
        static float front = 35.7f;

        static float RowZ => front + RowFromFront;

        [MenuItem("Last Orders/Expansion/Refresh Courtyard Barrier And Garden (Current Scene)", priority = 34)]
        public static void Run()
        {
            Debug.Log(Apply(save: false));
        }

        public static string Apply(bool save)
        {
            GameObject areaGo = GameObject.Find(AreaPath);
            var area = areaGo != null ? areaGo.GetComponent<ExpansionArea>() : null;
            if (area == null) return "[Last Orders] No Area_Courtyard in the scene; nothing was changed.";

            K.Reset();
            Vector3 origin = areaGo.transform.position;
            origin.y = 0f;

            var so = new SerializedObject(area);

            var plan = so.FindProperty("buildPlan").objectReferenceValue as ExpansionBuildPlan;
            // The yard's wall runs on across the defense ring to the compound's; the yard itself starts past the ring.
            if (plan != null && ExpansionNeighbours.TryLand(plan, out Rect yard))
                front = Mathf.Max(yard.yMin, PerimeterStripLayout.NorthLine + PerimeterStripLayout.Ring);
            var locked = new List<GameObject>();
            var unlocked = new List<GameObject>();

            // --- Barrier row ----------------------------------------------------------------------
            Transform lockedRoot = areaGo.transform.Find("LockedUntilClaimed");
            foreach (Transform child in new List<Transform>(Children(lockedRoot)))
            {
                if (child.name.StartsWith("Block") || child.name.StartsWith("Breach") || child.name == "BarrierRow")
                    Undo.DestroyObjectImmediate(child.gameObject);
            }

            Mesh barrier = BuildBarrier(origin).Save($"{Folder}/Courtyard_BarrierRow.asset", "Courtyard_BarrierRow");
            GameObject row = Show("BarrierRow", lockedRoot, origin, barrier);

            // Keep whatever else is locked, add the new row.
            SerializedProperty lockedProp = so.FindProperty("lockedVisuals");
            for (int i = 0; i < lockedProp.arraySize; i++)
            {
                var go = lockedProp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (go != null) locked.Add(go);
            }

            locked.Add(row);
            Assign(lockedProp, locked);

            // --- Garden -----------------------------------------------------------------------------
            Transform claimedRoot = areaGo.transform.Find("ShownWhenClaimed");
            Transform old = claimedRoot.Find("GardenPlaceholder");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            Mesh garden = BuildGarden(origin).Save($"{Folder}/Courtyard_Garden.asset", "Courtyard_Garden");
            Show("Garden", claimedRoot, origin, garden);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(area);

            Scene scene = areaGo.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return $"[Last Orders] Courtyard barrier row ({BlockX.Length} blocks) and garden ({BedX.Length} beds) rebuilt as low-poly models.";
        }

        static IEnumerable<Transform> Children(Transform parent)
        {
            foreach (Transform child in parent) yield return child;
        }

        static void Assign(SerializedProperty array, List<GameObject> items)
        {
            array.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static GameObject Show(string name, Transform parent, Vector3 origin, Mesh mesh)
        {
            Transform t = K.Group(name, parent);
            t.position = origin;
            Undo.RegisterCreatedObjectUndo(t.gameObject, name);
            B.Show(t.gameObject, mesh);

            var renderer = t.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            return t.gameObject;
        }

        static float FootShade(Vector3 p, Vector3 n)
        {
            float t = Mathf.Clamp01(p.y / 0.3f);
            return Mathf.Lerp(0.74f, 1f, t * t * (3f - 2f * t));
        }

        // --- The barrier row ------------------------------------------------------------------------

        static B.Model BuildBarrier(Vector3 origin)
        {
            var m = new B.Model { Shade = FootShade };

            for (int i = 0; i < BlockX.Length; i++)
            {
                Vector3 foot = new Vector3(BlockX[i], 0f, RowZ + 0.12f * B.Jitter(i * 5 + 1)) - origin;
                float yaw = 6f * B.Jitter(i * 3 + 2);

                // Two blocks are knocked over or cracked through; the rest stand.
                if (i == 0 || i == 4) Broken(m, foot, yaw, i);
                else Standing(m, foot, yaw, i, i == 2 || i == 3);
            }

            return m;
        }

        /// <summary>A jersey barrier: wide foot, sloped shoulders, narrow top, a painted band.</summary>
        static void Standing(B.Model m, Vector3 foot, float yaw, int key, bool stripe)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Color body = Concrete * (1f + 0.05f * B.Jitter(key));

            m.Box(body, foot + turn * new Vector3(0f, 0.14f, 0f), new Vector3(2.2f, 0.28f, 0.62f), 0.05f, turn, 2);
            m.Box(body, foot + turn * new Vector3(0f, 0.47f, 0f), new Vector3(2.16f, 0.4f, 0.4f), 0.06f, turn, 2);
            m.Box(body, foot + turn * new Vector3(0f, 0.74f, 0f), new Vector3(2.12f, 0.22f, 0.28f), 0.06f, turn, 2);

            if (stripe) m.Box(Paint, foot + turn * new Vector3(0f, 0.5f, 0f), new Vector3(2.19f, 0.1f, 0.405f), 0.02f, turn);

            // Moss gathered along the foot on the shaded side.
            m.Box(Moss * (1f + 0.08f * B.Jitter(key + 9)), foot + turn * new Vector3(0.3f * B.Jitter(key + 4), 0.03f, 0.28f),
                  new Vector3(0.9f, 0.07f, 0.12f), 0.03f, turn);
        }

        /// <summary>A block cracked across, one half slumped, with rebar showing and chunks on the ground.</summary>
        static void Broken(B.Model m, Vector3 foot, float yaw, int key)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);

            // The standing half, a little short.
            m.Box(Concrete, foot + turn * new Vector3(-0.55f, 0.14f, 0f), new Vector3(1.1f, 0.28f, 0.62f), 0.05f, turn, 2);
            m.Box(Concrete, foot + turn * new Vector3(-0.55f, 0.47f, 0f), new Vector3(1.08f, 0.4f, 0.4f), 0.06f, turn, 2);
            m.Box(Concrete, foot + turn * new Vector3(-0.55f, 0.7f, 0f), new Vector3(1.04f, 0.18f, 0.28f), 0.06f, turn, 2);

            // The fallen half, tipped over on its side.
            Quaternion fallen = turn * Quaternion.Euler(0f, 14f, 68f);
            m.Box(ConcreteDark, foot + turn * new Vector3(0.7f, 0.32f, 0.1f), new Vector3(1.0f, 0.4f, 0.55f), 0.06f, fallen, 2);

            // Rebar out of the break, bent.
            for (int r = 0; r < 3; r++)
            {
                float z = -0.18f + r * 0.18f;
                Vector3 from = foot + turn * new Vector3(0f, 0.3f + 0.1f * r, z);
                Vector3 to = foot + turn * new Vector3(0.45f + 0.1f * r, 0.5f + 0.18f * (r % 2), z + 0.05f);
                m.Tube(B.Rust, from, to, 0.016f, 5);
            }

            // Chunks.
            Vector3[] chunks = { new Vector3(0.2f, 0.07f, 0.5f), new Vector3(-0.3f, 0.06f, -0.5f), new Vector3(1.2f, 0.08f, -0.2f) };
            for (int c = 0; c < chunks.Length; c++)
            {
                Quaternion tilt = Quaternion.Euler(20f * B.Jitter(key + c), 40f * c, 15f * B.Jitter(key + c + 3));
                m.Box(Concrete * (0.9f + 0.1f * c), foot + turn * chunks[c], new Vector3(0.3f, 0.14f, 0.24f), 0.04f, tilt);
            }
        }

        // --- The garden -----------------------------------------------------------------------------

        static B.Model BuildGarden(Vector3 origin)
        {
            var m = new B.Model { Shade = FootShade };

            for (int b = 0; b < BedX.Length; b++)
            {
                Vector3 centre = new Vector3(BedX[b], 0f, front + BedsFromFront) - origin;
                Bed(m, centre, new Vector2(3.6f, 1.6f), b);
            }

            return m;
        }

        /// <summary>A raised bed: boards round a mound of soil, three rows of plants, a post at each corner.</summary>
        static void Bed(B.Model m, Vector3 centre, Vector2 size, int key)
        {
            float w = size.x, d = size.y, h = 0.32f;

            // Soil crowned in the middle.
            var area = new Rect(centre.x - w * 0.5f + 0.06f, centre.z - d * 0.5f + 0.06f, w - 0.12f, d - 0.12f);
            m.Heightfield((p, n) => Color.Lerp(Soil, SoilLight, Mathf.Clamp01(n.y * 0.5f + 0.15f * B.Jitter((int)(p.x * 9f + p.z * 13f)))),
                          area, 14, (x, z) => h - 0.08f + 0.1f * Mathf.Sin(Mathf.Clamp01((x - area.xMin) / area.width) * Mathf.PI) *
                                              Mathf.Sin(Mathf.Clamp01((z - area.yMin) / area.height) * Mathf.PI));

            // Boards: two courses on every side.
            for (int c = 0; c < 2; c++)
            {
                float y = 0.08f + c * 0.15f;
                Color wood = B.Plank(key * 7 + c);
                m.Box(wood, centre + new Vector3(0f, y, -d * 0.5f + 0.03f), new Vector3(w, 0.14f, 0.06f), 0.015f);
                m.Box(B.Plank(key * 7 + c + 2), centre + new Vector3(0f, y, d * 0.5f - 0.03f), new Vector3(w, 0.14f, 0.06f), 0.015f);
                m.Box(B.Plank(key * 7 + c + 4), centre + new Vector3(-w * 0.5f + 0.03f, y, 0f), new Vector3(0.06f, 0.14f, d), 0.015f);
                m.Box(B.Plank(key * 7 + c + 6), centre + new Vector3(w * 0.5f - 0.03f, y, 0f), new Vector3(0.06f, 0.14f, d), 0.015f);
            }

            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                    m.Box(B.WalnutDark, centre + new Vector3(sx * (w * 0.5f - 0.04f), 0.19f, sz * (d * 0.5f - 0.04f)),
                          new Vector3(0.09f, 0.38f, 0.09f), 0.02f);

            // Rows of young leafy plants.
            int cols = 6;
            for (int row = 0; row < 3; row++)
            {
                for (int i = 0; i < cols; i++)
                {
                    int k = key * 100 + row * 10 + i;
                    float x = Mathf.Lerp(-w * 0.5f + 0.4f, w * 0.5f - 0.4f, i / (float)(cols - 1)) + 0.05f * B.Jitter(k);
                    float z = (row - 1) * (d * 0.28f) + 0.05f * B.Jitter(k + 1);
                    Plant(m, centre + new Vector3(x, h + 0.04f, z), k);
                }
            }
        }

        /// <summary>A cabbage-like tuft: a squat dome with leaves fanned round it.</summary>
        static void Plant(B.Model m, Vector3 foot, int key)
        {
            Color leaf = Leaves[B.Hash(key) % Leaves.Length];
            float scale = 0.85f + 0.2f * B.Jitter(key + 5);

            var profile = new List<Vector2>
            {
                new Vector2(0.0f, 0.0f), new Vector2(0.13f, 0.01f), new Vector2(0.19f, 0.07f),
                new Vector2(0.17f, 0.15f), new Vector2(0.09f, 0.21f), new Vector2(0.0f, 0.23f),
            };
            for (int i = 0; i < profile.Count; i++) profile[i] *= scale;
            m.Lathe((p, n) => Color.Lerp(leaf, B.LeafLight, Mathf.Clamp01(p.y / 0.2f) * 0.5f), foot, profile, 10);

            for (int l = 0; l < 4; l++)
            {
                float yaw = l * 90f + 30f * B.Jitter(key + l);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -24f);
                Vector3 at = foot + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.17f * scale, 0.07f * scale, 0f);
                m.Box(leaf * 0.92f, at, new Vector3(0.2f, 0.025f, 0.17f) * scale, 0.01f, q);
            }
        }
    }
}
