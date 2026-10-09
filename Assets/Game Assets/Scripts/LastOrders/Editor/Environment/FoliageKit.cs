using UnityEditor;
using UnityEngine;
using E = LastOrders.EditorTools.EnvKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the art pack's trees, bushes and grass on the toon foliage shader
    /// (Shaders/Toon): banded light, a cool shade, a painted leaf pattern over the green and
    /// a gradient up the canopy. Their meshes and the pack's palette texture stay as they are; only
    /// the material changes, and only on vegetation - a fence or a tyre from the same pack keeps the
    /// pack's own material.
    /// </summary>
    public static class FoliageKit
    {
        const string PackMaterial = "PolygonFarm_01_A";
        const string DuskMaterial = PackMaterial + "_Dusk";
        const string PackMaterialPath = "Assets/PolygonFarm/Materials/" + PackMaterial + ".mat";

        static readonly Color DuskTint = new Color(0.8f, 0.86f, 0.8f);

        /// <summary>Pack models that are plants, by the start of their name.</summary>
        static readonly string[] Vegetation =
        {
            "SM_Generic_Tree", "SM_Env_Tree", "SM_Prop_Plant_Bush", "SM_Generic_Grass", "SM_Env_Bush", "SM_Generic_Bush",
            "SM_Env_Grass", "SM_Generic_Plant",
        };

        /// <summary>
        /// Swaps every plant under <paramref name="root"/> onto the toon material: the pack's own
        /// material for the lit one, the forest's dimmed copy for the dusk one. Returns how many
        /// renderers changed.
        /// </summary>
        public static int Apply(Transform root)
        {
            Material toon = null, toonDusk = null;
            int changed = 0;

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!IsVegetation(renderer.transform)) continue;

                Material[] shared = renderer.sharedMaterials;
                bool any = false;

                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null) continue;

                    if (shared[i].name == PackMaterial)
                    {
                        if (toon == null) toon = Toon(shared[i], "Toon", Color.white);
                        shared[i] = toon;
                        any = true;
                    }
                    else if (shared[i].name == DuskMaterial)
                    {
                        if (toonDusk == null) toonDusk = Toon(shared[i], "ToonDusk", DuskTint);
                        shared[i] = toonDusk;
                        any = true;
                    }
                }

                if (!any) continue;

                renderer.sharedMaterials = shared;
                changed++;
            }

            return changed;
        }

        static bool IsVegetation(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                foreach (string prefix in Vegetation)
                    if (p.name.StartsWith(prefix)) return true;

                // Up to the pack instance, not past it into the builder's groups.
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(p.gameObject)) break;
            }

            return false;
        }

        /// <summary>
        /// Sets both foliage materials up again in the current style, without touching the scene:
        /// a scene still on the previous style's copies is moved over by <see cref="PaintedLookSetup"/>.
        /// </summary>
        public static string Restyle()
        {
            var pack = AssetDatabase.LoadAssetAtPath<Material>(PackMaterialPath);
            if (pack == null) return $"Foliage: WARNING: no {PackMaterialPath}, so the tree materials were left as they were.";

            Toon(pack, "Toon", Color.white);
            Toon(pack, "ToonDusk", DuskTint);
            return "Foliage: tree, bush and grass materials up to date.";
        }

        /// <summary>
        /// The toon copy of a pack material: its palette, a tint, the generated leaf pattern, lit
        /// the low-poly way - a deep, cool green low in the canopy and in shade, a fresh lighter
        /// green where the sun reaches the crown, with a soft edge between rather than a hard cel band.
        /// </summary>
        static Material Toon(Material source, string suffix, Color tint)
        {
            Texture palette = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
            Texture2D leaves = TextureGenerator.EnsureLeaves();

            return E.CachedMaterial($"{PackMaterial}_{suffix}", () => new Material(Shader.Find("LastOrders/Toon")), m =>
            {
                m.shader = Shader.Find("LastOrders/Toon");
                m.SetTexture("_BaseMap", palette);
                // A little less red than the pack's palette, never less blue: cutting blue had turned the crowns lime.
                m.SetColor("_BaseColor", tint * new Color(0.94f, 1f, 0.96f));
                m.SetTexture("_LeafTex", leaves);

                // A leaf a hand or two long: at the game's distance anything smaller is noise.
                m.SetFloat("_LeafScale", 0.28f);
                m.SetFloat("_LeafStrength", 0.85f);
                m.SetColor("_ShadeColor", new Color(0.36f, 0.48f, 0.56f));
                m.SetFloat("_Softness", 0.16f);
                m.SetFloat("_CanopyLow", 0.62f);
                m.SetFloat("_CanopyHigh", 1.24f);
                m.SetColor("_RimColor", new Color(0.92f, 1f, 0.9f));
                m.SetFloat("_RimStrength", 0.24f);
                m.enableInstancing = true;
            });
        }
    }
}
