using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The pickup thrown out when an item is made - two from every crop picked, one for each dish
    /// cooked - built in code as a prefab for <see cref="ItemReceiver"/>: the item's icon on
    /// LastOrders/PickupSprite, a slowly turning glow of soft gold rays behind it, a shadow blob on
    /// the ground, and a twinkle played where it is collected.
    ///
    /// The prefab the level used before (Game Assets/Prefabs/GetItemButton) is left as it was; the open
    /// scene's ItemController is pointed at the new one.
    /// </summary>
    public static class HarvestPickups
    {
        const string PrefabPath = PlantingEffects.Folder + "/ItemPickup.prefab";
        const string BurstPath = PlantingEffects.Folder + "/PickupCollect.prefab";
        const string GlowPath = PlantingEffects.Folder + "/Pickup_Glow.png";
        const string ShadowMaterialPath = "Assets/Game Assets/Content/LastOrders/Farming/Crop_Shadow.mat";
        const string SampleIconPath = "Assets/Game Assets/Content/LastOrders/Farming/UI/Icon_Corn.png";

        const int GlowSize = 256;

        [MenuItem("Last Orders/Farming/Build Item Pickups", priority = 96)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the pickup thrown out when a crop is picked or a dish cooked, and the twinkle as it is collected:\n\n" +
                $"- {PrefabPath}\n- {BurstPath}\n" +
                "- the open scene: its ItemController uses the new pickup\n\n" +
                "Run Build Planting Effects first (the twinkle uses its sparkle). The scene is left unsaved.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds it all and points the open scene at it, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(PlantingEffects.Folder);

            Sprite glowSprite = SaveGlow();
            Material icon = PickupMaterial("Pickup_Icon", false, 0.15f, 1.2f);
            Material glow = PickupMaterial("Pickup_Glow", true, 0f, 1.25f);
            var shadow = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);

            GameObject burst = BuildBurst();
            GameObject pickup = BuildPickup(icon, glow, glowSprite, shadow, burst);

            var report = new StringBuilder("[Last Orders] Item pickups built.\n");
            report.Append($"  {PrefabPath}: icon, turning glow, shadow{(shadow == null ? " (WARNING: Crop_Shadow.mat missing, no shadow)" : "")}.\n");
            report.Append($"  {BurstPath}: sparkles, glow pop, motes.\n");
            report.Append($"  {SetUpScene(pickup)}\n");
            return report.ToString();
        }

        // --- The pickup ------------------------------------------------------------------------------

        static GameObject BuildPickup(Material iconMaterial, Material glowMaterial, Sprite glowSprite, Material shadowMaterial, GameObject burst)
        {
            var root = new GameObject("ItemPickup");
            try
            {
                var receiver = root.AddComponent<ItemReceiver>();

                var body = new GameObject("Body").transform;
                body.SetParent(root.transform, false);

                SpriteRenderer glow = Sprite(body, "Glow", glowSprite, glowMaterial, 0);
                glow.color = new Color(1f, 0.85f, 0.45f, 0.85f);
                SpriteRenderer icon = Sprite(body, "Icon", AssetDatabase.LoadAssetAtPath<Sprite>(SampleIconPath), iconMaterial, 1);

                Transform shadow = null;
                if (shadowMaterial != null)
                {
                    shadow = new GameObject("Shadow", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    shadow.SetParent(root.transform, false);
                    shadow.SetLocalPositionAndRotation(new Vector3(0f, 0.012f, 0f), Quaternion.Euler(90f, 0f, 0f));
                    shadow.localScale = new Vector3(0.4f, 0.34f, 1f);
                    shadow.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                    var renderer = shadow.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = shadowMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                var serialized = new SerializedObject(receiver);
                serialized.FindProperty("itemImage").objectReferenceValue = icon;
                serialized.FindProperty("body").objectReferenceValue = body;
                serialized.FindProperty("glow").objectReferenceValue = glow;
                serialized.FindProperty("shadow").objectReferenceValue = shadow;
                serialized.FindProperty("collectBurst").objectReferenceValue = burst;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Material material, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        static Material PickupMaterial(string name, bool additive, float lit, float pull)
        {
            Material material = PlantingEffects.SavedMaterial(name, "LastOrders/PickupSprite");
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Flash", 0f);
            material.SetFloat("_Lit", lit);
            material.SetFloat("_Additive", additive ? 1f : 0f);
            material.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_DepthPull", pull);
            PlantingEffects.Save(material);
            return material;
        }

        /// <summary>A soft gold glow with a ring of gentle rays round it, white so the renderer's colour tints it.</summary>
        static Sprite SaveGlow()
        {
            const int Samples = 3;
            var pixels = new Color[GlowSize * GlowSize];
            for (int j = 0; j < GlowSize; j++)
                for (int i = 0; i < GlowSize; i++)
                {
                    float sum = 0f;
                    for (int sj = 0; sj < Samples; sj++)
                        for (int si = 0; si < Samples; si++)
                        {
                            float x = (i + (si + 0.5f) / Samples) / GlowSize * 2f - 1f;
                            float y = (j + (sj + 0.5f) / Samples) / GlowSize * 2f - 1f;
                            float r = Mathf.Sqrt(x * x + y * y);
                            float angle = Mathf.Atan2(y, x);

                            float core = Mathf.Exp(-r * r * 9f) * 0.75f;
                            float rays = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(angle * 10f), 5f) * Mathf.Exp(-r * 2.4f) *
                                         PlantingEffects.Smooth(Mathf.InverseLerp(0.12f, 0.35f, r)) * 0.5f;
                            float edge = 1f - PlantingEffects.Smooth(Mathf.InverseLerp(0.8f, 1f, r));
                            sum += Mathf.Clamp01(core + rays) * edge;
                        }

                    pixels[j * GlowSize + i] = new Color(1f, 1f, 1f, sum / (Samples * Samples));
                }

            var texture = new Texture2D(GlowSize, GlowSize, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply(false);
            File.WriteAllBytes(GlowPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(GlowPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GlowPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = GlowSize;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
        }

        // --- The twinkle as it is collected ----------------------------------------------------------

        static GameObject BuildBurst()
        {
            Material sparkle = Existing("Burst_Sparkle");
            Material glow = Existing("Ripe_Glow");

            var root = new GameObject("PickupCollect");
            try
            {
                ParticleSystem pop = Instant(root.transform, "Pop", glow);
                var main = pop.main;
                main.startLifetime = 0.3f;
                main.startSpeed = 0f;
                main.startSize = 0.9f;
                main.startColor = PlantingEffects.Fade(PlantingEffects.Hex("FFD47A"), 0.6f);
                main.maxParticles = 1;
                PlantingEffects.Burst(pop, 0f, 1);
                var shape = pop.shape;
                shape.enabled = false;
                PlantingEffects.Grow(pop, PlantingEffects.Curve((0f, 0.4f), (0.35f, 1f), (1f, 1.1f)));
                PlantingEffects.FadeOut(pop, 0.2f);

                ParticleSystem sparkles = Instant(root.transform, "Sparkles", sparkle);
                main = sparkles.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-20f * Mathf.Deg2Rad, 20f * Mathf.Deg2Rad);
                main.startColor = new ParticleSystem.MinMaxGradient(PlantingEffects.Hex("FFE58A"), PlantingEffects.Hex("FFF8DA"));
                main.maxParticles = 10;
                PlantingEffects.Burst(sparkles, 0f, 7);
                shape = sparkles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.12f;
                PlantingEffects.Drag(sparkles, 0.2f, 0.25f);
                PlantingEffects.Grow(sparkles, PlantingEffects.Curve((0f, 0.2f), (0.15f, 1f), (0.6f, 0.7f), (1f, 0f)));
                PlantingEffects.Spin(sparkles, 90f, 200f);
                sparkles.GetComponent<ParticleSystemRenderer>().sortingFudge = -20f;

                ParticleSystem motes = Instant(root.transform, "Motes", glow);
                main = motes.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
                main.startColor = new ParticleSystem.MinMaxGradient(PlantingEffects.Hex("FFE9A0"), PlantingEffects.Hex("FFF4C8"));
                main.gravityModifier = -0.15f;
                main.maxParticles = 10;
                PlantingEffects.Burst(motes, 0.02f, 6);
                shape = motes.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f;
                PlantingEffects.FadeOut(motes, 0.5f);

                return PrefabUtility.SaveAsPrefabAsset(root, BurstPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A particle system that goes off as soon as it is made, in world space so it stays where the pickup ended.</summary>
        static ParticleSystem Instant(Transform root, string name, Material material)
        {
            ParticleSystem ps = PlantingEffects.NewSystem(root, name, material, ParticleSystemRenderMode.Billboard);
            var main = ps.main;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            return ps;
        }

        static Material Existing(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{PlantingEffects.Folder}/{name}.mat");
            if (material == null)
                throw new System.InvalidOperationException($"{name} is missing: run Last Orders / Farming / Build Planting Effects and Build Harvest Effects first.");
            return material;
        }

        // --- The scene -------------------------------------------------------------------------------

        static string SetUpScene(GameObject pickup)
        {
            var controller = Object.FindFirstObjectByType<ItemController>(FindObjectsInactive.Include);
            if (controller == null) return "Scene: no ItemController here; nothing pointed at the new pickup.";

            string before = controller.itemReceiver != null ? controller.itemReceiver.name : "nothing";
            Undo.RecordObject(controller, "Item pickups");
            controller.itemReceiver = pickup.GetComponent<ItemReceiver>();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return $"Scene: {controller.name}'s ItemController now throws ItemPickup (was {before}); the scene is not saved.";
        }
    }
}
