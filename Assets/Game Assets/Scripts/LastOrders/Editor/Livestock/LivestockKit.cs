using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using B = LastOrders.EditorTools.BuildingKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Shared ground for the farmyard's builders - the hens, the cow, the chicken coop and the cow
    /// shelter: where their meshes and prefabs go, and the small helpers every one of them uses to
    /// hang a mesh on a named child. Everything is painted in its vertices on the buildings'
    /// LastOrders/Stylized material, as the rest of the compound's props are.
    /// </summary>
    internal static class LivestockKit
    {
        internal const string ContentFolder = "Assets/Game Assets/Content/LastOrders/Farming";
        internal const string MeshFolder = ContentFolder + "/" + EnvKit.Style;
        internal const string AnimalMeshFolder = MeshFolder + "/Animals";
        internal const string PrefabFolder = "Assets/Game Assets/Content/LastOrders/Prefabs/Livestock";
        internal const string EffectsFolder = MeshFolder + "/Fx";

        internal static Mesh Save(B.Model model, string folder, string name) => model.Save($"{folder}/{name}.asset", name);

        /// <summary>The child called <paramref name="name"/>, made if missing, put at <paramref name="at"/> with no turn and no scale.</summary>
        internal static Transform Child(Transform parent, string name, Vector3 at)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }

            t.localPosition = at;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        /// <summary>A child showing <paramref name="mesh"/> on the painted material.</summary>
        internal static Transform Part(Transform parent, string name, Vector3 at, Mesh mesh, bool shadows = true)
        {
            Transform t = Child(parent, name, at);
            B.Show(t.gameObject, mesh);
            var renderer = t.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return t;
        }

        /// <summary>A prefab made if missing or opened for rebuilding in place; <paramref name="dress"/> fills it.</summary>
        internal static GameObject Prefab(string path, string rootName, System.Action<GameObject, bool> dress)
        {
            ContentGenerator.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(path) == null;
            GameObject root = created ? new GameObject(rootName) : PrefabUtility.LoadPrefabContents(path);

            try
            {
                dress(root, created);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (created) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
