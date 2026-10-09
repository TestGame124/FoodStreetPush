using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The crop items' icons - flour, corn, tomato, cabbage - in the style of the sickle: the produce
    /// each crop gives (<see cref="CropModels.Produce"/>) rendered like the crops in the pots, from a
    /// little above and to the right, lit from the top left, then set in a square with a warm dark
    /// outline and a soft shadow. The seed tray, the store, orders, the customers' bubbles and the
    /// pickups thrown out at harvest all show them.
    ///
    /// The icons they replace (stock clip art in Game Assets/UI/Items) are left where they are;
    /// Restore Previous Crop Icons points the items back at them.
    /// </summary>
    public static class CropIcons
    {
        const string Folder = "Assets/Game Assets/Content/LastOrders/Farming/UI";
        const string PreviousFolder = "Assets/Game Assets/UI/Items";

        public const int Size = 512;

        /// <summary>How much of the square the outlined produce may fill.</summary>
        const float Fill = 0.93f;
        const int Outline = 11;
        static readonly Color OutlineColour = new Color(0.23f, 0.15f, 0.09f);

        /// <summary>Looking a little down on the produce, from its front right.</summary>
        static readonly Quaternion View = Quaternion.Euler(24f, -22f, 0f);

        /// <summary>The key light, in the view's own axes: from the top left, a little in front.</summary>
        static readonly Vector3 LightInView = new Vector3(-0.55f, 0.72f, -0.42f);

        static readonly (CropModels.Crop crop, ItemType item, string name)[] Icons =
        {
            (CropModels.Crop.Wheat, ItemType.Flour, "Flour"),
            (CropModels.Crop.Corn, ItemType.Corn, "Corn"),
            (CropModels.Crop.Tomato, ItemType.Tomato, "Tomato"),
            (CropModels.Crop.Cabbage, ItemType.Cabbage, "Cabbage"),
        };

        [MenuItem("Last Orders/Farming/Build Crop Icons", priority = 94)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This renders the crops' produce into item icons and gives them to the items:\n\n" +
                $"- {Folder}: Icon_Flour, Icon_Corn, Icon_Tomato, Icon_Cabbage\n" +
                "- the Flour, Corn, Tomato and Cabbage items: their icon\n\n" +
                $"The previous icons in {PreviousFolder} are not touched.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        [MenuItem("Last Orders/Farming/Restore Previous Crop Icons", priority = 95)]
        public static void RestorePrevious()
        {
            var report = new StringBuilder("[Last Orders] Previous crop icons restored.\n");
            foreach (var (_, item, name) in Icons)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PreviousFolder}/{name}.png");
                report.Append($"  {name}: {(sprite != null ? AssignIcon(item, sprite) : "WARNING: previous icon missing")}\n");
            }

            Debug.Log(report.ToString());
        }

        /// <summary>Renders every icon and gives each to its item, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(Folder);
            var report = new StringBuilder("[Last Orders] Crop icons built.\n");

            foreach (var (crop, item, name) in Icons)
            {
                Sprite sprite = SaveIcon(Folder, $"Icon_{name}", Render(crop));
                report.Append($"  Icon_{name}: {AssignIcon(item, sprite)}\n");
            }

            return report.ToString();
        }

        /// <summary>The finished icon of <paramref name="crop"/>, <see cref="Size"/> square; destroy it when done.</summary>
        public static Texture2D Render(CropModels.Crop crop) => Render(CropModels.Produce(crop), $"{crop}_Produce", View);

        /// <summary>
        /// The finished icon of any model built with the crops' paints, seen along <paramref name="view"/>
        /// and lit from the top left of it, <see cref="Size"/> square; destroy it when done.
        /// </summary>
        public static Texture2D Render(SmoothMesh model, string name, Quaternion view)
        {
            Mesh mesh = model.Build(name, out List<int> used);
            Material[] palette = Palette();

            try
            {
                Frame(mesh, view);
                Texture2D render = CropSprites.Render(mesh, used, palette, view, out Vector2 _, view * LightInView.normalized);
                try
                {
                    return Compose(render);
                }
                finally
                {
                    Object.DestroyImmediate(render);
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                foreach (Material material in palette) Object.DestroyImmediate(material);
            }
        }

        /// <summary>
        /// The crops' paints, without the darkening toward the soil (the produce is not standing in
        /// any) and with a little more rim light, so the shapes stand off the outline.
        /// </summary>
        static Material[] Palette()
        {
            Material[] palette = CropSprites.BakeMaterials();
            foreach (Material material in palette)
            {
                material.SetFloat("_GroundShade", 1f);
                material.SetFloat("_GroundHeight", 0.001f);
                material.SetFloat("_Rim", 0.3f);
            }

            return palette;
        }

        /// <summary>
        /// The produce scaled so the render comes out as big as the icon will show it, with nothing
        /// below the bake's soil line at zero, which it does not draw.
        /// </summary>
        static void Frame(Mesh mesh, Quaternion view)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3 right = view * Vector3.right, up = view * Vector3.up;

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, bottom = float.MaxValue;
            foreach (Vector3 v in vertices)
            {
                float x = Vector3.Dot(v, right), y = Vector3.Dot(v, up);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                bottom = Mathf.Min(bottom, v.y);
            }

            float box = (Size * Fill - Outline * 2f) / CropSprites.PixelsPerMetre;
            float scale = box / Mathf.Max(maxX - minX, maxY - minY);
            Vector3 lift = Vector3.up * (0.01f - bottom * scale);

            for (int i = 0; i < vertices.Length; i++) vertices[i] = vertices[i] * scale + lift;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        // --- The icon --------------------------------------------------------------------------------

        /// <summary><paramref name="render"/> centred in the square with a dark outline round it and a soft shadow under it.</summary>
        internal static Texture2D Compose(Texture2D render)
        {
            // Trimmed to what was drawn: the renderer frames from the origin, below and beside the produce.
            Color32[] source = render.GetPixels32();
            int minX = render.width, minY = render.height, maxX = -1, maxY = -1;
            for (int y = 0; y < render.height; y++)
                for (int x = 0; x < render.width; x++)
                {
                    if (source[y * render.width + x].a < 3) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }

            float drawnW = maxX - minX + 1, drawnH = maxY - minY + 1;
            float box = Size * Fill - Outline * 2f;
            float scale = Mathf.Min(1f, Mathf.Min(box / drawnW, box / drawnH));
            int w = Mathf.RoundToInt(drawnW * scale), h = Mathf.RoundToInt(drawnH * scale);
            // Nudged up and left, leaving room for the shadow down and to the right.
            int ox = (Size - w) / 2 - 3, oy = (Size - h) / 2 + 5;

            var art = new Color[Size * Size];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    art[(y + oy) * Size + x + ox] = render.GetPixelBilinear((minX + (x + 0.5f) / scale) / render.width,
                                                                            (minY + (y + 0.5f) / scale) / render.height);

            float[] ring = Dilate(art, Outline);
            float[] shadow = Blur(Shift(ring, 5, -8), 5);

            var pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = new Color(0.1f, 0.06f, 0.03f, shadow[i] * 0.34f);
                c = Over(new Color(OutlineColour.r, OutlineColour.g, OutlineColour.b, ring[i]), c);
                pixels[i] = Over(art[i], c);
            }

            var icon = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            icon.SetPixels(pixels);
            icon.Apply(false);
            return icon;
        }

        /// <summary>The art's alpha spread <paramref name="radius"/> pixels out in every direction, its edge anti-aliased.</summary>
        static float[] Dilate(Color[] art, int radius)
        {
            var offsets = new List<(int dx, int dy, float weight)>();
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    float weight = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    if (weight > 0f) offsets.Add((dx, dy, weight));
                }

            var alpha = new float[art.Length];
            for (int i = 0; i < art.Length; i++) alpha[i] = art[i].a;

            var result = new float[art.Length];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float best = 0f;
                    foreach (var (dx, dy, weight) in offsets)
                    {
                        int sx = x + dx, sy = y + dy;
                        if (sx < 0 || sy < 0 || sx >= Size || sy >= Size) continue;
                        float a = alpha[sy * Size + sx] * weight;
                        if (a > best && (best = a) >= 1f) break;
                    }

                    result[y * Size + x] = best;
                }

            return result;
        }

        static float[] Shift(float[] values, int dx, int dy)
        {
            var result = new float[values.Length];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int sx = x - dx, sy = y - dy;
                    if (sx >= 0 && sy >= 0 && sx < Size && sy < Size) result[y * Size + x] = values[sy * Size + sx];
                }

            return result;
        }

        /// <summary>A box blur <paramref name="radius"/> pixels each way, across then down.</summary>
        static float[] Blur(float[] values, int radius)
        {
            var across = new float[values.Length];
            var result = new float[values.Length];
            float count = radius * 2 + 1;

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += values[y * Size + Mathf.Clamp(x + k, 0, Size - 1)];
                    across[y * Size + x] = sum / count;
                }

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += across[Mathf.Clamp(y + k, 0, Size - 1) * Size + x];
                    result[y * Size + x] = sum / count;
                }

            return result;
        }

        static Color Over(Color top, Color under)
        {
            float a = top.a + under.a * (1f - top.a);
            if (a <= 0f) return Color.clear;
            Color c = (top * top.a + under * under.a * (1f - top.a)) / a;
            c.a = a;
            return c;
        }

        // --- Saving and the items --------------------------------------------------------------------

        /// <summary>Writes an icon into <paramref name="folder"/> as a sprite, and destroys the texture.</summary>
        internal static Sprite SaveIcon(string folder, string name, Texture2D texture)
        {
            ContentGenerator.EnsureFolder(folder);
            string path = $"{folder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Size;
            importer.alphaIsTransparency = true;
            // Shown small in the UI and on pickups out in the level: mipmaps keep it smooth there.
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = Size;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Gives <paramref name="sprite"/> to every item of <paramref name="type"/>.</summary>
        internal static string AssignIcon(ItemType type, Sprite sprite)
        {
            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDataSO"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.Type != type) continue;

                Undo.RecordObject(item, "Crop icons");
                item.itemImage = sprite;
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(item);
                assigned++;
            }

            return assigned > 0 ? $"given to the {type} item" : $"WARNING: no {type} item found";
        }
    }
}
