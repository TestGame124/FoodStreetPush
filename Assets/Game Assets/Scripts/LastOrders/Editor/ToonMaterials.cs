using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Flat colours on the level's toon shader (Shaders/Toon), for the props modelled in code - the
    /// diner's furniture, the farm plots. The shader's foliage features are off, and it takes less
    /// of the sky than the trees do: flat bright colours under the sun and a full white sky wash out.
    /// </summary>
    public static class ToonMaterials
    {
        /// <summary>
        /// The material at <paramref name="folder"/>/<paramref name="name"/>.mat, made if missing and
        /// set to <paramref name="colour"/> every time, so re-running a builder picks up a new colour.
        /// Colours are painted darker than they show: the level's light lifts them by about half again.
        /// </summary>
        public static Material Flat(string folder, string name, Color colour, float rim)
        {
            ContentGenerator.EnsureFolder(folder);

            string path = $"{folder}/{name}.mat";
            Shader shader = Shader.Find("LastOrders/Toon");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_LeafStrength", 0f);
            material.SetFloat("_CanopyLow", 1f);
            material.SetFloat("_CanopyHigh", 1f);
            material.SetFloat("_Threshold", 0.08f);
            material.SetFloat("_Softness", 0.045f);
            material.SetFloat("_LightStrength", 0.88f);
            material.SetFloat("_AmbientStrength", 0.6f);
            material.SetColor("_ShadeColor", new Color(0.5f, 0.55f, 0.68f));
            material.SetFloat("_RimStrength", rim);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
    }
}
