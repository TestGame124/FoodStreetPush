using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The diner's tables and chairs, modelled in code in the game's smooth low-poly style:
    /// chunky and rounded, light wood under a soft light.
    ///
    /// A farmhouse diner set, to go with the teal and off-white checkerboard and the teal skirting:
    /// a square table of four light oak boards on a dark frame and an H-stretcher, ketchup, mustard
    /// and a napkin holder in the middle; wooden chairs with a plump diner-red cushion and a
    /// slatted back. Shaded by LastOrders/Stylized - light wrapping softly round the form into a
    /// cool shade, no cel band - with the vertices painted darker toward the floor, the shade a
    /// painter lays where a leg meets the ground.
    ///
    /// Built to the old models' measurements - the table top at 0.45 m, the seat at 0.35 m - so
    /// seated customers, their plates and the waiters' routes all land where they did. The models
    /// go into the table prefab by swapping the meshes on the pieces already there: each chair's
    /// own transform is where a customer sits, and replacing the object would cut that link.
    ///
    /// The models and materials are in the art style's own folder (see <see cref="EnvKit.Style"/>);
    /// the round fifties set from before stays in the folder above.
    /// </summary>
    public static class DinerFurniture
    {
        const string Folder = "Assets/Game Assets/Content/LastOrders/Furniture/" + EnvKit.Style;
        const string TableMeshPath = Folder + "/DinerTable.asset";
        const string ChairMeshPath = Folder + "/DinerChair.asset";

        const string TablePrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Table/pfTable.prefab";
        const string GhostPrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Table/TableVisuals.prefab";

        /// <summary>Where the table stands in its 3 x 3 m cell, as the old model did.</summary>
        static readonly Vector3 TableCentre = new Vector3(1.5f, 0f, 1.5f);

        /// <summary>
        /// How far below its own origin each piece reaches. The furniture is placed 6 cm up, on the
        /// build grid, and the restaurant's floor is 3.5 cm up: without this the feet float.
        /// </summary>
        const float Sink = 0.025f;

        /// <summary>Half the table top's width: 1.1 m square, so a seat tucks a little way under each side.</summary>
        const float TableHalf = 0.55f;

        // Submeshes, by material.
        const int Cream = 0, Cherry = 1, Chrome = 2, Mustard = 3, Honey = 4, HoneyDark = 5, Walnut = 6, Cushion = 7;
        const int Materials = 8;

        [MenuItem("Last Orders/Restaurant/Build Diner Furniture (Tables & Chairs)", priority = 76)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This rebuilds the diner's table and chair models and puts them into:\n\n" +
                "- pfTable (every table in every scene, and every table the player builds)\n" +
                "- TableVisuals (the ghost shown while placing a table)\n\n" +
                "Sit points, plate points and colliders are kept as they are.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the models and puts them into the prefabs, with no dialogs - for automation.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);

            // Painted darker than they show, as the level's other surfaces are: the sky light alone
            // lifts a table top facing straight up by more than half again.
            Material[] palette =
            {
                Paint("Diner_Cream", new Color(0.78f, 0.77f, 0.72f)),
                Paint("Diner_Cherry", new Color(0.68f, 0.18f, 0.15f)),
                Paint("Diner_Chrome", new Color(0.55f, 0.58f, 0.62f)),
                Paint("Diner_Mustard", new Color(0.78f, 0.58f, 0.1f)),
                Paint("Diner_Honey", new Color(0.62f, 0.5f, 0.39f)),
                Paint("Diner_HoneyDark", new Color(0.56f, 0.45f, 0.35f)),
                Paint("Diner_Walnut", new Color(0.36f, 0.27f, 0.21f)),
                Paint("Diner_Cushion", new Color(0.64f, 0.22f, 0.2f)),
            };

            (Mesh table, Material[] tableMaterials) = Save(BuildTable(), TableMeshPath, "DinerTable", palette);
            (Mesh chair, Material[] chairMaterials) = Save(BuildChair(), ChairMeshPath, "DinerChair", palette);

            var report = new StringBuilder("[Last Orders] Diner furniture built.\n");
            report.Append($"  Square table: {table.vertexCount} vertices; wooden chair: {chair.vertexCount} vertices; soft-shaded.\n");
            report.Append($"  {DressTablePrefab(table, tableMaterials, chair, chairMaterials, out List<Pose> chairs)}\n");
            report.Append($"  {DressGhost(table, tableMaterials, chair, chairMaterials, chairs)}\n");
            return report.ToString();
        }

        // --- The models ------------------------------------------------------------------------------

        /// <summary>
        /// The table, standing on its own origin: a square top 1.1 m across, 45 cm up, of four
        /// boards in two shades of honey with a hairline between; a walnut apron under it, four
        /// square legs and an H of stretchers low between them. In the middle, where no plate goes,
        /// the diner's condiments: ketchup, mustard and a chrome napkin holder.
        /// </summary>
        static SmoothMesh BuildTable()
        {
            var mesh = new SmoothMesh(Materials);

            const int boards = 4;
            float board = TableHalf * 2f / boards;
            for (int k = 0; k < boards; k++)
            {
                float z = -TableHalf + board * (k + 0.5f);
                mesh.RoundedBox(k % 2 == 0 ? Honey : HoneyDark, new Vector3(0f, 0.42f, z),
                                new Vector3(TableHalf * 2f, 0.06f, board - 0.01f), 0.016f, Quaternion.identity, 2);
            }

            mesh.RoundedBox(Walnut, new Vector3(0f, 0.355f, 0f), new Vector3(0.94f, 0.07f, 0.94f), 0.016f, Quaternion.identity, 2);

            const float leg = 0.45f;
            float legTop = 0.36f;
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    mesh.RoundedBox(Walnut, new Vector3(x * leg, (legTop - Sink) * 0.5f, z * leg),
                                    new Vector3(0.08f, legTop + Sink, 0.08f), 0.016f, Quaternion.identity, 2);
                }

                // The H: a rail down each side, joined across the middle.
                mesh.RoundedBox(Walnut, new Vector3(x * leg, 0.1f, 0f), new Vector3(0.04f, 0.05f, leg * 2f - 0.06f), 0.012f,
                                Quaternion.identity, 2);
            }

            mesh.RoundedBox(Walnut, new Vector3(0f, 0.1f, 0f), new Vector3(leg * 2f - 0.06f, 0.05f, 0.04f), 0.012f, Quaternion.identity, 2);

            // The condiments, a hand's width apart across the middle.
            Bottle(mesh, new Vector3(-0.11f, 0.45f, 0f), Cherry);
            Bottle(mesh, new Vector3(0.11f, 0.45f, 0f), Mustard);
            mesh.RoundedBox(Chrome, new Vector3(0f, 0.51f, 0f), new Vector3(0.11f, 0.12f, 0.07f), 0.018f, Quaternion.identity, 2);
            mesh.RoundedBox(Cream, new Vector3(0f, 0.575f, 0f), new Vector3(0.085f, 0.022f, 0.05f), 0.008f, Quaternion.identity, 1);

            return mesh;
        }

        /// <summary>A squeeze bottle standing at <paramref name="foot"/>: a rounded body, a shoulder, a cream nozzle.</summary>
        static void Bottle(SmoothMesh mesh, Vector3 foot, int body)
        {
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.026f, 0f), new Vector2(0.031f, 0.012f), new Vector2(0.031f, 0.085f),
                new Vector2(0.024f, 0.105f), new Vector2(0.013f, 0.112f),
                new Vector2(0.013f, 0.112f),
                new Vector2(0.011f, 0.13f), new Vector2(0.004f, 0.15f), new Vector2(0f, 0.152f),
            };
            var subs = new List<int> { body, body, body, body, body, body, Cream, Cream, Cream };
            mesh.Lathe(foot, profile, subs, 12);
        }

        /// <summary>
        /// A chair facing +Z, standing on its own origin: a honey seat board 30 cm up with a plump
        /// terracotta cushion on it to 35 cm, four walnut legs braced at the sides, and two posts
        /// leaning a little back holding a broad top rail and a slat.
        /// </summary>
        static SmoothMesh BuildChair()
        {
            var mesh = new SmoothMesh(Materials);
            var lean = Quaternion.Euler(-8f, 0f, 0f);

            mesh.RoundedBox(Honey, new Vector3(0f, 0.28f, 0.015f), new Vector3(0.46f, 0.045f, 0.44f), 0.016f, Quaternion.identity, 2);
            mesh.RoundedBox(Cushion, new Vector3(0f, 0.324f, 0.025f), new Vector3(0.41f, 0.052f, 0.39f), 0.024f, Quaternion.identity);

            const float legTop = 0.26f;
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    mesh.RoundedBox(Walnut, new Vector3(x * 0.19f, (legTop - Sink) * 0.5f, 0.015f + z * 0.175f),
                                    new Vector3(0.05f, legTop + Sink, 0.05f), 0.012f, Quaternion.identity, 2);
                }

                mesh.RoundedBox(Walnut, new Vector3(x * 0.19f, 0.09f, 0.015f), new Vector3(0.035f, 0.035f, 0.33f), 0.01f,
                                Quaternion.identity, 2);
                mesh.RoundedBox(Walnut, new Vector3(x * 0.19f, 0.53f, -0.18f), new Vector3(0.05f, 0.5f, 0.05f), 0.012f, lean, 2);
            }

            mesh.RoundedBox(Honey, new Vector3(0f, 0.73f, -0.21f), new Vector3(0.44f, 0.11f, 0.045f), 0.02f, lean, 2);
            mesh.RoundedBox(HoneyDark, new Vector3(0f, 0.55f, -0.185f), new Vector3(0.36f, 0.06f, 0.03f), 0.012f, lean, 2);

            return mesh;
        }

        // --- Assets --------------------------------------------------------------------------------------

        /// <summary>One colour of the set on LastOrders/Stylized, made if missing and set every time.</summary>
        static Material Paint(string name, Color colour)
        {
            string path = $"{Folder}/{name}.mat";
            Shader shader = Shader.Find("LastOrders/Stylized");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", colour);
            material.SetColor("_ShadeColor", new Color(0.55f, 0.6f, 0.74f));
            material.SetFloat("_Wrap", 0.55f);
            material.SetFloat("_LightStrength", 0.88f);
            material.SetFloat("_AmbientStrength", 0.62f);
            material.SetFloat("_ShadowStrength", 0.8f);
            material.SetFloat("_RimStrength", 0.1f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        /// <summary>
        /// Saves a model as a mesh asset, its vertices painted grey toward the floor - the shade
        /// under a table, where a leg meets the ground - which the shader multiplies the colour by.
        /// Returns it with its materials in submesh order.
        /// </summary>
        static (Mesh, Material[]) Save(SmoothMesh model, string path, string name, Material[] palette)
        {
            Mesh mesh = model.BuildAsset(path, name, out List<int> used);

            Vector3[] vertices = mesh.vertices;
            var colours = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float k = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01((vertices[i].y + Sink) / 0.32f));
                colours[i] = new Color(k, k, k, 1f);
            }

            mesh.colors = colours;
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);

            var materials = new Material[used.Count];
            for (int i = 0; i < used.Count; i++) materials[i] = palette[used[i]];
            return (mesh, materials);
        }

        // --- The prefabs ---------------------------------------------------------------------------------

        /// <summary>
        /// Swaps the table's and the chairs' meshes in pfTable. The table piece is put at the cell's
        /// middle at full size (the old model stood scaled down); the chairs keep their transforms,
        /// which the table's sit points refer to. Returns where the chairs stand, for the ghost.
        /// </summary>
        static string DressTablePrefab(Mesh table, Material[] tableMaterials, Mesh chair, Material[] chairMaterials,
                                       out List<Pose> chairs)
        {
            chairs = new List<Pose>();
            GameObject root = PrefabUtility.LoadPrefabContents(TablePrefabPath);

            try
            {
                Transform tableTransform = root.transform.Find("table_round_A");
                if (tableTransform == null) return $"WARNING: no 'table_round_A' in {TablePrefabPath}; nothing was changed.";

                Dress(tableTransform, table, tableMaterials);
                tableTransform.localPosition = TableCentre;
                tableTransform.localRotation = Quaternion.identity;
                tableTransform.localScale = Vector3.one;

                int dressed = 0;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.name.StartsWith("chair_A")) continue;

                    Dress(filter.transform, chair, chairMaterials);
                    chairs.Add(new Pose(root.transform.InverseTransformPoint(filter.transform.position),
                                        Quaternion.Inverse(root.transform.rotation) * filter.transform.rotation));
                    dressed++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, TablePrefabPath);
                return $"pfTable: new table and {dressed} chairs; sit and plate points untouched.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The ghost shown while placing a table: the same table and chairs, where pfTable has
        /// them, in place of the old café set it showed (four chairs round a different table).
        /// </summary>
        static string DressGhost(Mesh table, Material[] tableMaterials, Mesh chair, Material[] chairMaterials, List<Pose> chairs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GhostPrefabPath);

            try
            {
                Transform old = root.transform.Find("Table");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                Transform set = root.transform.Find("DinerSet");
                if (set != null) Object.DestroyImmediate(set.gameObject);

                set = new GameObject("DinerSet").transform;
                set.SetParent(root.transform, false);

                Piece(set, "Table", new Pose(TableCentre, Quaternion.identity), table, tableMaterials);
                for (int i = 0; i < chairs.Count; i++) Piece(set, $"Chair_{i}", chairs[i], chair, chairMaterials);

                PrefabUtility.SaveAsPrefabAsset(root, GhostPrefabPath);
                return $"TableVisuals: the placing ghost shows the new table and {chairs.Count} chairs.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void Dress(Transform piece, Mesh mesh, Material[] materials)
        {
            var filter = piece.GetComponent<MeshFilter>();
            var renderer = piece.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null) return;

            filter.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
        }

        static void Piece(Transform parent, string name, Pose pose, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pose.position, pose.rotation);
            Dress(go.transform, mesh, materials);
        }
    }
}
