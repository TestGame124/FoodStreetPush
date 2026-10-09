using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The crops in the plant pots as sprites: each stage of each crop in <see cref="CropModels"/>,
    /// rendered from the game camera's angle with the toon gradient shading painted in, saved as a
    /// transparent PNG and imported as a sprite pivoted on the crop's base. A pot shows them on a
    /// camera-facing <see cref="CropSprite"/>, so they read as the 3D models they were drawn from.
    ///
    /// Each stage is rendered in a few variants, and each crop gets a <see cref="CropLook"/>: the
    /// variants and the spots its plants stand on in a pot, which a <see cref="CropPatch"/> on the
    /// pot lays out. The seed items get the look, and the first variant of each stage in
    /// <see cref="ItemDataSO.growStages"/> as before.
    ///
    /// Rendered without a camera: a command buffer draws the model with the bake shader into a
    /// render texture four times the size, so no scene light, post effect or render pipeline setting
    /// can change the result, and the result is filtered down so edges are smooth.
    /// </summary>
    public static class CropSprites
    {
        const string Folder = "Assets/Game Assets/Content/LastOrders/Farming/Crops";
        const string MaterialFolder = "Assets/Game Assets/Content/LastOrders/Farming";
        const string PotPrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Farming/PlantPot/pfPlantPot.prefab";

        const string CropName = "Crop";
        const string ShadowName = "CropShadow";

        /// <summary>Sprite pixels to a metre: about twice what the focus camera shows on a tall phone screen.</summary>
        public const int PixelsPerMetre = 400;
        const int Supersample = 4;
        const float Margin = 0.02f;

        /// <summary>CameraPanZoomRig's followOffset in the level: the camera sits here from what it frames.</summary>
        static readonly Vector3 DefaultCameraOffset = new Vector3(15f, 30f, -20f);

        /// <summary>The level's sun by day.</summary>
        static readonly Quaternion DefaultSun = Quaternion.Euler(40f, 45f, 0f);

        static readonly (CropModels.Crop crop, ItemType item)[] Crops =
        {
            (CropModels.Crop.Wheat, ItemType.Flour),
            (CropModels.Crop.Corn, ItemType.Corn),
            (CropModels.Crop.Tomato, ItemType.Tomato),
            (CropModels.Crop.Cabbage, ItemType.Cabbage),
        };

        static readonly string[] StageNames = { "Sprout", "Young", "Grown", "Ripe" };

        [MenuItem("Last Orders/Farming/Build Crop Sprites (Plant Pots)", priority = 91)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This renders the crops' grow stages into sprites and puts them into:\n\n" +
                $"- {Folder} ({CropModels.Variants} versions of four stages for each crop, and a look for each)\n" +
                "- the seed items (Flour, Corn, Tomato, Cabbage): their look and grow stages\n" +
                "- pfPlantPot: a patch of camera-facing crop sprites, each with its shadow\n\n" +
                "The scene is not changed.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Renders every stage and puts the sprites into the items and the pot, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);
            var report = new StringBuilder("[Last Orders] Crop sprites built.\n");

            Material[] palette = BakeMaterials();
            try
            {
                foreach (var (crop, itemType) in Crops)
                {
                    var stages = new Sprite[CropModels.StageCount][];
                    var sizes = new List<string>();

                    for (int stage = 0; stage < CropModels.StageCount; stage++)
                    {
                        stages[stage] = new Sprite[CropModels.Variants];
                        for (int variant = 0; variant < CropModels.Variants; variant++)
                        {
                            Texture2D texture = Render(crop, stage, variant, palette, out Vector2 pivot);
                            stages[stage][variant] = SaveSprite(texture, SpritePath(crop, stage, variant), pivot);
                            if (variant == 0) sizes.Add($"{texture.width}x{texture.height}");
                            Object.DestroyImmediate(texture);
                        }
                    }

                    CropLook look = SaveLook(crop, stages);
                    report.Append($"  {crop}: {CropModels.Spots(crop).Length} plants a pot, {string.Join(", ", sizes)} px; ");
                    report.Append($"{AssignToItem(itemType, look, stages)}\n");
                }
            }
            finally
            {
                foreach (Material material in palette) Object.DestroyImmediate(material);
            }

            report.Append($"  {DressPot()}\n");
            return report.ToString();
        }

        // --- Rendering -------------------------------------------------------------------------------

        /// <summary>The direction the game camera looks in.</summary>
        public static Quaternion View()
        {
            Vector3 offset = DefaultCameraOffset;

            // The level's own framing, when it is open: the sprites must match the angle it is seen from.
            var rig = Object.FindFirstObjectByType<CameraPanZoomRig>(FindObjectsInactive.Include);
            if (rig != null)
            {
                SerializedProperty property = new SerializedObject(rig).FindProperty("followOffset");
                if (property != null && property.vector3Value.sqrMagnitude > 1f) offset = property.vector3Value;
            }

            return Quaternion.LookRotation(-offset.normalized, Vector3.up);
        }

        /// <summary>The bake shader in every paint of <see cref="CropModels.Paint"/>; destroy them when done.</summary>
        public static Material[] BakeMaterials()
        {
            Shader shader = Shader.Find("Hidden/LastOrders/CropBake");
            var materials = new Material[(int)CropModels.Paint.Count];

            for (int i = 0; i < materials.Length; i++)
            {
                CropModels.Swatch swatch = CropModels.Of((CropModels.Paint)i);
                var material = new Material(shader) { name = ((CropModels.Paint)i).ToString(), hideFlags = HideFlags.HideAndDontSave };
                material.SetColor("_ColorA", swatch.Base);
                material.SetColor("_ColorB", swatch.Tip);
                material.SetFloat("_GradientPower", swatch.Power > 0f ? swatch.Power : 1f);
                material.SetFloat("_Vein", swatch.Vein);
                material.SetFloat("_Gloss", swatch.Gloss);
                material.SetFloat("_EdgeShade", swatch.Edge);
                material.SetFloat("_Threshold", -0.05f - swatch.Wrap);
                material.SetVector("_Kernels", swatch.Kernels);
                material.SetFloat("_GroundShade", 0.72f);
                material.SetFloat("_GroundHeight", 0.05f);
                materials[i] = material;
            }

            return materials;
        }

        /// <summary>The first variant's file keeps the name the single sprite had, so references to it survive.</summary>
        static string SpritePath(CropModels.Crop crop, int stage, int variant) =>
            $"{Folder}/Crop_{crop}_{stage}_{StageNames[stage]}{(variant == 0 ? "" : $"_{variant + 1}")}.png";

        /// <summary>
        /// <paramref name="crop"/> at <paramref name="stage"/> as seen by the game camera, cropped to
        /// the plant with a small margin; <paramref name="pivot"/> is where its base lands, 0-1 across it.
        /// </summary>
        public static Texture2D Render(CropModels.Crop crop, int stage, Material[] palette, out Vector2 pivot) =>
            Render(crop, stage, 0, palette, out pivot);

        /// <summary>As <see cref="Render(CropModels.Crop, int, Material[], out Vector2)"/>, for the plant grown from seed <paramref name="variant"/>.</summary>
        public static Texture2D Render(CropModels.Crop crop, int stage, int variant, Material[] palette, out Vector2 pivot)
        {
            Mesh mesh = CropModels.Build(crop, stage, variant).Build($"{crop}_{stage}_{variant}", out List<int> used);

            try
            {
                // Scaled round its base, so the base stays on the soil; a uniform scale leaves the normals as they are.
                float scale = CropModels.DisplayScale(crop);
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] *= scale;
                mesh.vertices = vertices;
                mesh.RecalculateBounds();

                return Render(mesh, used, palette, View(), out pivot);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>
        /// <paramref name="mesh"/>, its submeshes painted <paramref name="used"/>, seen along <paramref name="view"/>
        /// and lit from <paramref name="toLight"/> (a world direction toward the light; the level's sun when not given).
        /// </summary>
        public static Texture2D Render(Mesh mesh, List<int> used, Material[] palette, Quaternion view, out Vector2 pivot, Vector3? toLight = null)
        {
            Vector3 right = view * Vector3.right, up = view * Vector3.up, forward = view * Vector3.forward;

            // What the camera sees of the model (nothing below the soil is drawn), and its base.
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;
            foreach (Vector3 v in mesh.vertices)
            {
                if (!float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z))
                    throw new System.InvalidOperationException($"{mesh.name}: the model has a vertex at {v}; a shape in CropModels divided by zero.");
                if (v.y < 0f) continue;
                float x = Vector3.Dot(v, right), y = Vector3.Dot(v, up);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }

            // Whole pixels, in blocks of four so the texture compresses cleanly.
            int width = Mathf.CeilToInt((maxX - minX + Margin * 2f) * PixelsPerMetre / 4f) * 4;
            int height = Mathf.CeilToInt((maxY - minY + Margin * 2f) * PixelsPerMetre / 4f) * 4;
            float widthM = width / (float)PixelsPerMetre, heightM = height / (float)PixelsPerMetre;
            float left = (minX + maxX) * 0.5f - widthM * 0.5f, bottom = (minY + maxY) * 0.5f - heightM * 0.5f;

            Vector3 eye = right * (left + widthM * 0.5f) + up * (bottom + heightM * 0.5f) - forward * 5f;
            Matrix4x4 worldToView = Matrix4x4.TRS(eye, view, new Vector3(1f, 1f, -1f)).inverse;
            Matrix4x4 projection = Matrix4x4.Ortho(-widthM * 0.5f, widthM * 0.5f, -heightM * 0.5f, heightM * 0.5f, 0.05f, 10f);
            Matrix4x4 clip = GL.GetGPUProjectionMatrix(projection, true) * worldToView;

            int bigWidth = width * Supersample, bigHeight = height * Supersample;
            var target = RenderTexture.GetTemporary(new RenderTextureDescriptor(bigWidth, bigHeight, RenderTextureFormat.ARGB32, 24)
            {
                sRGB = false,
            });

            var cmd = new CommandBuffer { name = "Bake crop sprite" };
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, Color.clear);
            cmd.SetGlobalMatrix("_CropBakeMatrix", clip);
            cmd.SetGlobalVector("_CropBakeLight", toLight ?? -(SunRotation() * Vector3.forward));
            cmd.SetGlobalVector("_CropBakeView", -forward);
            for (int s = 0; s < used.Count; s++) cmd.DrawMesh(mesh, Matrix4x4.identity, palette[used[s]], s, 0);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var big = new Texture2D(bigWidth, bigHeight, TextureFormat.RGBA32, false);
            big.ReadPixels(new Rect(0, 0, bigWidth, bigHeight), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            Texture2D result = Downsample(big, width, height);
            Object.DestroyImmediate(big);

            pivot = new Vector2(-left / widthM, -bottom / heightM);
            return result;
        }

        static Quaternion SunRotation()
        {
            Light sun = RenderSettings.sun;
            return sun != null && sun.type == LightType.Directional ? sun.transform.rotation : DefaultSun;
        }

        /// <summary>
        /// Each block of samples averaged into one pixel, weighted by coverage: the render cleared to
        /// transparent black, so a plain average would darken every edge toward black.
        /// </summary>
        static Texture2D Downsample(Texture2D big, int width, int height)
        {
            Color32[] source = big.GetPixels32();
            var pixels = new Color32[width * height];
            int bigWidth = width * Supersample;
            const float samples = Supersample * Supersample;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        int row = (y * Supersample + sy) * bigWidth + x * Supersample;
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            Color32 c = source[row + sx];
                            float w = c.a / 255f;
                            r += c.r * w; g += c.g * w; b += c.b * w; a += w;
                        }
                    }

                    pixels[y * width + x] = a > 0f
                        ? new Color32((byte)Mathf.RoundToInt(r / a), (byte)Mathf.RoundToInt(g / a), (byte)Mathf.RoundToInt(b / a),
                                      (byte)Mathf.RoundToInt(a / samples * 255f))
                        : new Color32(0, 0, 0, 0);
                }
            }

            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.SetPixels32(pixels);
            result.Apply(false);
            return result;
        }

        static Sprite SaveSprite(Texture2D texture, string path, Vector2 pivot)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerMetre;
            // Also bleeds the edge colours out into the clear pixels, so filtering draws no dark fringe.
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 1024;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // --- The items and the pot -------------------------------------------------------------------

        /// <summary>The crop's look: every stage's variants and where its plants stand, saved beside the sprites.</summary>
        static CropLook SaveLook(CropModels.Crop crop, Sprite[][] stages)
        {
            string path = $"{Folder}/CropLook_{crop}.asset";
            var look = AssetDatabase.LoadAssetAtPath<CropLook>(path);
            if (look == null)
            {
                look = ScriptableObject.CreateInstance<CropLook>();
                AssetDatabase.CreateAsset(look, path);
            }

            look.Configure(stages, CropModels.Spots(crop), CropModels.SizeJitter(crop), 0.02f);
            EditorUtility.SetDirty(look);
            AssetDatabase.SaveAssetIfDirty(look);
            return look;
        }

        static string AssignToItem(ItemType type, CropLook look, Sprite[][] stages)
        {
            var first = new Sprite[stages.Length];
            for (int i = 0; i < stages.Length; i++) first[i] = stages[i][0];

            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDataSO"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.Type != type || !item.canBePlanted) continue;

                Undo.RecordObject(item, "Crop sprites");
                item.growStages = first;
                item.cropLook = look;
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(item);
                assigned++;
            }

            return assigned > 0 ? $"given to the {type} seed" : $"WARNING: no plantable {type} item found";
        }

        /// <summary>
        /// pfPlantPot: whatever grew under its crop preview before (the art pack's cabbage, whose mesh
        /// the pot swapped for each stage) out, and the crop sprite and its shadow in, on the soil's crown.
        /// </summary>
        static string DressPot()
        {
            Material spriteMaterial = SpriteMaterial();
            Material shadowMaterial = ShadowMaterial();
            GameObject root = PrefabUtility.LoadPrefabContents(PotPrefabPath);

            try
            {
                var pot = root.GetComponent<PlantPot>();
                if (pot == null || pot.plantPreview == null) return "WARNING: pfPlantPot has no PlantPot with a crop preview.";

                Transform plants = pot.plantPreview.transform;
                var removed = new List<string>();
                for (int i = plants.childCount - 1; i >= 0; i--)
                {
                    GameObject child = plants.GetChild(i).gameObject;
                    if (child.name == CropName || child.name == ShadowName) continue;
                    removed.Add(child.name);
                    Object.DestroyImmediate(child);
                }

                var anchor = new Vector3(0.5f, SoilBed.Crown, 0.5f);

                Transform crop = plants.Find(CropName);
                if (crop == null)
                {
                    crop = new GameObject(CropName, typeof(SpriteRenderer), typeof(CropSprite)).transform;
                    crop.SetParent(plants, false);
                }

                crop.SetLocalPositionAndRotation(anchor, Quaternion.identity);
                crop.localScale = Vector3.one;

                var renderer = crop.GetComponent<SpriteRenderer>();
                renderer.sprite = null;
                renderer.sharedMaterial = spriteMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Transform shadow = plants.Find(ShadowName);
                if (shadow == null)
                {
                    shadow = new GameObject(ShadowName, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    shadow.SetParent(plants, false);
                }

                shadow.SetLocalPositionAndRotation(anchor + Vector3.up * 0.006f, Quaternion.Euler(90f, 0f, 0f));
                shadow.localScale = new Vector3(0.3f, 0.3f, 1f);
                shadow.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var shadowRenderer = shadow.GetComponent<MeshRenderer>();
                shadowRenderer.sharedMaterial = shadowMaterial;
                shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                shadowRenderer.receiveShadows = false;
                shadow.gameObject.SetActive(false);

                var cropSprite = crop.GetComponent<CropSprite>();
                var cropObject = new SerializedObject(cropSprite);
                cropObject.FindProperty("sprite").objectReferenceValue = renderer;
                cropObject.FindProperty("shadow").objectReferenceValue = shadow;
                cropObject.ApplyModifiedPropertiesWithoutUndo();

                // The rest of a pot's plants are copies of this one, made by the patch as a crop needs them.
                var patch = plants.GetComponent<CropPatch>();
                if (patch == null) patch = plants.gameObject.AddComponent<CropPatch>();
                patch.Configure(cropSprite, anchor);

                var potObject = new SerializedObject(pot);
                potObject.FindProperty("crop").objectReferenceValue = cropSprite;
                potObject.FindProperty("patch").objectReferenceValue = patch;
                potObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PotPrefabPath);
                return $"pfPlantPot: a crop patch of camera-facing sprites, each with a shadow on the soil" +
                       $"{(removed.Count > 0 ? $"; removed {string.Join(", ", removed)}" : "")}.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Material SpriteMaterial() =>
            SavedMaterial("Crop_Sprite", "LastOrders/CropSprite", m => m.SetFloat("_DepthPull", 0.35f));

        /// <summary>A soft dark blob, blended rather than added: SoftBlob is a light pool by default.</summary>
        static Material ShadowMaterial() =>
            SavedMaterial("Crop_Shadow", "LastOrders/SoftBlob", m =>
            {
                // Several plants share a pot and their shadows overlap, so each is lighter than one alone would be.
                m.SetColor("_Color", new Color(0.12f, 0.08f, 0.04f, 0.3f));
                m.SetFloat("_Softness", 0.85f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                // Under the crops, which share the transparent queue with it.
                m.renderQueue = (int)RenderQueue.Transparent - 10;
            });

        static Material SavedMaterial(string name, string shaderName, System.Action<Material> setUp)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find(shaderName);

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            setUp(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
    }
}
