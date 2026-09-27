using LastOrders.Data;
using LastOrders.Defense;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the guard tower prefab: the tower, the empty site it stands on before it is
    /// bought, and the guard who stands watch in it.
    ///
    /// One prefab for every site, so a change to the tower is made once. It is grey-box -
    /// primitives from the same kit as the rest of the compound, and the project's own
    /// character art for the guard - and is expected to be replaced by a modelled tower. The
    /// replacement only has to keep the parts <see cref="DefensePost"/> is wired to.
    ///
    /// Local +Z is the side the guard faces at rest, so a site rotated to face the road has
    /// its guard watching the road.
    /// </summary>
    public static class DefensePrefabGenerator
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Prefabs/Defense";
        public const string GuardTowerPath = Folder + "/Defense_GuardTower.prefab";

        const string TracerMaterialPath = "Assets/Game Assets/Content/LastOrders/Materials/Defense_Tracer.mat";

        /// <summary>The guard's model among the project's characters. Not the Chef's or the Helper's.</summary>
        const int GuardModel = 3;

        const float DeckHeight = 2.75f;

        [MenuItem("Last Orders/Defense/Rebuild Guard Tower Prefab", priority = 41)]
        static void Rebuild()
        {
            DefenseDefinition tower = DefenseContentGenerator.Generate();
            EnsureGuardTower(tower, rebuild: true);
        }

        /// <summary>The prefab, built if it does not exist yet - or rebuilt over it when asked.</summary>
        public static GameObject EnsureGuardTower(DefenseDefinition definition, bool rebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GuardTowerPath);
            if (existing != null && !rebuild) return existing;

            ContentGenerator.EnsureFolder(Folder);
            K.Reset();

            GameObject root = BuildGuardTower(definition);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, GuardTowerPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[Last Orders] {(existing != null ? "Rebuilt" : "Created")} the guard tower prefab at {GuardTowerPath}.");
            return prefab;
        }

        static GameObject BuildGuardTower(DefenseDefinition definition)
        {
            var root = new GameObject("Defense_GuardTower");

            Transform built = K.Group("Tower", root.transform);
            BuildFrame(built);
            BuildPlatform(built);
            BuildLadder(built);

            // The guard stands on the aim pivot, so turning to track an enemy turns the guard.
            Transform pivot = K.Group("AimPivot", built);
            pivot.localPosition = new Vector3(0f, DeckHeight + 0.08f, 0f);

            GameObject guard = BuildGuard(pivot);

            // Held at chest height. The muzzle is just past the barrel, which is where the
            // tracer starts from.
            K.Cube("Pistol", guard.transform, new Vector3(0.22f, 1.22f, 0.42f), new Vector3(0.07f, 0.11f, 0.26f), K.Dark);

            Transform muzzle = K.Group("Muzzle", guard.transform);
            muzzle.localPosition = new Vector3(0.22f, 1.24f, 0.58f);

            GameObject flash = K.Sphere("MuzzleFlash", muzzle, Vector3.zero, 0.2f, K.Lock);
            flash.SetActive(false);

            Transform site = BuildSite(root.transform);

            LineRenderer tracer = BuildTracer(root.transform);

            var tap = root.AddComponent<BoxCollider>();

            var post = root.AddComponent<DefensePost>();
            post.Configure(null, definition, "defense", false);
            post.ConfigureParts(built.gameObject, site.gameObject, pivot, muzzle, guard, tracer, flash, tap);

            return root;
        }

        // --- The tower ----------------------------------------------------------------------------

        static void BuildFrame(Transform parent)
        {
            // Four legs and a ring of braces halfway up. A tower on stilts, not a hut.
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -1f : 1f;
                float z = (i & 2) == 0 ? -1f : 1f;
                K.Box($"Leg_{i}", parent, new Vector3(x, 0f, z), new Vector3(0.24f, DeckHeight, 0.24f), K.WoodDark);
            }

            K.Cube("Brace_Front", parent, new Vector3(0f, 1.2f, 1f), new Vector3(2f, 0.12f, 0.12f), K.Wood);
            K.Cube("Brace_Back", parent, new Vector3(0f, 1.2f, -1f), new Vector3(2f, 0.12f, 0.12f), K.Wood);
            K.Cube("Brace_Left", parent, new Vector3(-1f, 1.2f, 0f), new Vector3(0.12f, 0.12f, 2f), K.Wood);
            K.Cube("Brace_Right", parent, new Vector3(1f, 1.2f, 0f), new Vector3(0.12f, 0.12f, 2f), K.Wood);
        }

        static void BuildPlatform(Transform parent)
        {
            K.Cube("Deck", parent, new Vector3(0f, DeckHeight, 0f), new Vector3(2.6f, 0.16f, 2.6f), K.Wood);

            // Waist-high walls, so the guard reads as standing IN the tower rather than on a table.
            const float wall = 0.9f;
            float y = DeckHeight + 0.08f + wall * 0.5f;

            K.Cube("Parapet_Front", parent, new Vector3(0f, y, 1.24f), new Vector3(2.6f, wall, 0.12f), K.WoodLight);
            K.Cube("Parapet_Back", parent, new Vector3(0f, y, -1.24f), new Vector3(2.6f, wall, 0.12f), K.WoodLight);
            K.Cube("Parapet_Left", parent, new Vector3(-1.24f, y, 0f), new Vector3(0.12f, wall, 2.6f), K.WoodLight);
            K.Cube("Parapet_Right", parent, new Vector3(1.24f, y, 0f), new Vector3(0.12f, wall, 2.6f), K.WoodLight);

            // Sandbags along the side that faces the road.
            for (int i = 0; i < 3; i++)
            {
                K.Sphere($"Sandbag_{i}", parent, new Vector3(-0.8f + i * 0.8f, y + wall * 0.5f + 0.1f, 1.26f),
                         new Vector3(0.78f, 0.3f, 0.42f), K.Cloth);
            }

            // A roof on four poles: a lookout, and something to read at a distance as "tower".
            float top = DeckHeight + 2.35f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -1.2f : 1.2f;
                float z = (i & 2) == 0 ? -1.2f : 1.2f;
                K.Cube($"RoofPost_{i}", parent, new Vector3(x, (DeckHeight + top) * 0.5f, z),
                       new Vector3(0.1f, top - DeckHeight, 0.1f), K.WoodDark);
            }

            K.Cube("Roof", parent, new Vector3(0f, top, 0f), new Vector3(3f, 0.14f, 3f), K.Rust);
        }

        /// <summary>Up the back, the side away from the road.</summary>
        static void BuildLadder(Transform parent)
        {
            Transform ladder = K.Group("Ladder", parent);

            K.Cube("RailL", ladder, new Vector3(-0.28f, DeckHeight * 0.5f, -1.42f), new Vector3(0.07f, DeckHeight, 0.07f), K.WoodDark);
            K.Cube("RailR", ladder, new Vector3(0.28f, DeckHeight * 0.5f, -1.42f), new Vector3(0.07f, DeckHeight, 0.07f), K.WoodDark);

            for (int i = 0; i < 7; i++)
                K.Cube($"Rung_{i}", ladder, new Vector3(0f, 0.35f + i * 0.35f, -1.42f), new Vector3(0.56f, 0.05f, 0.06f), K.Wood);
        }

        static GameObject BuildGuard(Transform pivot)
        {
            GameObject guard = VisitorCharacterGenerator.CreateCharacter(GuardModel, "Guard", pivot.position, pivot);

            if (guard == null)
            {
                // No character art: a primitive figure is still somebody standing watch.
                guard = K.Person("Guard", pivot, pivot.position, K.Denim, chefHat: false, yaw: 0f).gameObject;
            }

            guard.transform.localPosition = Vector3.zero;
            guard.transform.localRotation = Quaternion.identity;
            return guard;
        }

        // --- The empty site ---------------------------------------------------------------------

        static Transform BuildSite(Transform parent)
        {
            Transform site = K.Group("Site", parent);

            K.Cube("Pad", site, new Vector3(0f, 0.03f, 0f), new Vector3(2.6f, 0.06f, 2.6f), K.Stone);

            // Marked out, with the timber waiting - somewhere a tower is meant to go.
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -1.2f : 1.2f;
                float z = (i & 2) == 0 ? -1.2f : 1.2f;
                K.Box($"Stake_{i}", site, new Vector3(x, 0f, z), new Vector3(0.1f, 0.55f, 0.1f), K.Lock);
            }

            for (int i = 0; i < 3; i++)
                K.Log($"Timber_{i}", site, new Vector3(0.3f, 0.14f + i * 0.2f, -0.2f), 0.2f, 1.9f, K.Wood, 8f + i * 4f);

            return site;
        }

        // --- The shot ---------------------------------------------------------------------------

        static LineRenderer BuildTracer(Transform parent)
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(parent, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.06f;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = TracerMaterial();
            line.startColor = new Color(1f, 0.93f, 0.6f);
            line.endColor = new Color(1f, 0.75f, 0.35f, 0.6f);
            line.enabled = false;

            return line;
        }

        /// <summary>Unlit, so the shot reads at night as well as by day.</summary>
        static Material TracerMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(TracerMaterialPath);
            if (material != null) return material;

            ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(TracerMaterialPath).Replace('\\', '/'));

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Unlit/Color");

            material = new Material(shader);

            Color colour = new Color(1f, 0.88f, 0.5f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            material.color = colour;

            AssetDatabase.CreateAsset(material, TracerMaterialPath);
            return material;
        }
    }
}
