using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds people out of the project's own character art.
    ///
    /// Figures made of primitives were built for a scene made of cubes, where a capsule with a
    /// box on its back is the most detailed thing on screen. Dropped into a dressed level they
    /// read as placeholders, because that is what they are.
    ///
    /// This does the same job against the characters the project already owns: one model per
    /// variant, the existing animator on it, and a hat where the caller asks for one. Five
    /// distinct people and a handful of hats is more visual difference than the primitive kit
    /// could express, and it costs no art time because every piece of it is already here.
    ///
    /// Shared by the worker setup and by the visitor content generator, so a Chef built out of
    /// primitives never ends up standing next to visitors built out of character art.
    /// </summary>
    public static class VisitorCharacterGenerator
    {
        const string Folder = "Assets/Game Assets/Content/LastOrders/Prefabs/Visitors";
        const string CharacterFolder = "Assets/PolygonFarm/Prefabs/Characters";
        const string VisitorPrefab = "Assets/Game Assets/Prefabs/Characters/Visitor.prefab";
        const string AnimatorPath = "Assets/Game Assets/Prefabs/Characters/VisitorAnimator.controller";

        /// <summary>
        /// The models, in the order variants draw from them.
        ///
        /// Ordered so the three opening traveller variants are three visibly different people
        /// rather than the same silhouette three times - which is the entire reason the design
        /// asks for three variants before it asks for a second archetype.
        /// </summary>
        static readonly string[] Models =
        {
            "SM_Chr_FarmBoy_01",
            "SM_Chr_FarmGirl_01",
            "SM_Chr_Farmer_Male_01",
            "SM_Chr_Farmer_Female_01",
            "SM_Chr_Farmer_Male_Old_01",
        };

        /// <summary>True when the project has the character art this needs.</summary>
        public static bool IsAvailable =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterFolder}/{Models[0]}.prefab") != null;

        /// <summary>How many distinct people this can build. Callers wrap their own index
        /// into it rather than assuming a count that changes when art is added.</summary>
        public static int ModelCount => Models.Length;

        /// <summary>The layer the project's own characters sit on, so a tap on one registers
        /// against the same mask everything else does.</summary>
        public static int CharacterLayer => VisitorLayer();

        /// <summary>
        /// Instantiates one of the project's characters into a scene, animated.
        /// </summary>
        public static GameObject CreateCharacter(int modelIndex, string name, Vector3 at, Transform parent)
        {
            GameObject model = Model(modelIndex);
            if (model == null) return null;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = at;

            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;

            ApplyAnimator(body);
            AddShadow(root.transform);

            root.layer = VisitorLayer();
            return root;
        }

        static GameObject Model(int variantIndex)
        {
            if (Models.Length == 0) return null;

            int index = ((variantIndex % Models.Length) + Models.Length) % Models.Length;
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterFolder}/{Models[index]}.prefab");
        }

        /// <summary>
        /// Puts the existing visitor animator on the character.
        ///
        /// The controller is the one the old visitors used, so its Move, Sit and Eat parameters
        /// are exactly what the agent drives. The avatar is left alone: it belongs to this
        /// model's own rig, and overwriting it is how a character ends up in a T-pose.
        /// </summary>
        static void ApplyAnimator(GameObject body)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);
            if (controller == null)
            {
                Debug.LogWarning($"[Last Orders] No visitor animator at {AnimatorPath}. " +
                                 "Customers will walk without animating.");
                return;
            }

            var animator = body.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"[Last Orders] '{body.name}' has no Animator.", body);
                return;
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        /// <summary>
        /// Parents a hat to the head bone.
        ///
        /// Found by bone name rather than by index. These characters share a rig, but a rig is
        /// exactly the kind of thing that gets re-exported, and a hat parented to whatever bone
        /// happened to be at index 14 ends up on an elbow.
        /// </summary>
        public static void AddHat(GameObject body, string hat)
        {
            if (string.IsNullOrEmpty(hat)) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterFolder}/{hat}.prefab");
            if (prefab == null) return;

            Transform head = FindDeep(body.transform, "Head");
            if (head == null) return;

            var worn = (GameObject)PrefabUtility.InstantiatePrefab(prefab, head);
            worn.transform.localPosition = Vector3.zero;
            worn.transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Copies the blob shadow off the project's existing character prefab.
        ///
        /// Worth carrying over rather than relying on real-time shadows: it is what makes a
        /// character read as standing ON the ground rather than hovering a few centimetres
        /// above it, and it costs one sprite.
        /// </summary>
        public static void AddShadow(Transform root)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorPrefab);
            if (source == null) return;

            Transform shadow = source.transform.Find("Shadow");
            if (shadow == null) return;

            var copy = Object.Instantiate(shadow.gameObject, root);
            copy.name = "Shadow";
            copy.transform.localPosition = shadow.localPosition;
            copy.transform.localRotation = shadow.localRotation;
            copy.transform.localScale = shadow.localScale;
        }

        static int VisitorLayer()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorPrefab);
            return source != null ? source.layer : 0;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
