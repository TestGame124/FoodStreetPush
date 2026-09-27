using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Housing;
using UnityEditor;
using UnityEngine;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the shelter: its definition and its prefab - the hut, and the empty site it
    /// stands on before it is bought.
    ///
    /// The hut is grey-box like the guard tower - planks, a rusted roof and a stovepipe from the
    /// primitive kit - and expected to be replaced by a modelled one. The replacement only has to
    /// keep the parts <see cref="ShelterHut"/> is wired to: a curtain and a light for each bed.
    ///
    /// Local +Z is the front. The door is in the middle of it with a window either side, one per
    /// bed, so from the camera the two windows say how many people the hut holds.
    ///
    /// The definition's numbers are filled in only when it is created, so a tuned price survives
    /// a re-run.
    /// </summary>
    public static class ShelterContentGenerator
    {
        public const string DefinitionFolder = "Assets/Game Assets/Content/LastOrders/Housing";
        public const string DefinitionPath = DefinitionFolder + "/Shelter_Hut.asset";

        public const string PrefabFolder = "Assets/Game Assets/Content/LastOrders/Prefabs/Housing";
        public const string PrefabPath = PrefabFolder + "/Shelter_Hut.prefab";

        const string LitMaterialPath = "Assets/Game Assets/Content/LastOrders/Materials/Shelter_WindowLit.mat";

        const float Width = 3.2f;
        const float Depth = 2.8f;
        const float WallHeight = 1.9f;
        const float Ridge = 2.75f;
        const float DoorWidth = 0.9f;
        const float DoorHeight = 1.55f;

        /// <summary>Where each window sits across the front: centred on the wall either side of the door.</summary>
        const float WindowX = (Width * 0.5f + DoorWidth * 0.5f) * 0.5f;

        const float WindowY = 1.15f;

        static readonly Vector2 WindowSize = new Vector2(0.5f, 0.42f);

        static readonly Color Glass = K.Hex("3A4550");
        static readonly Color Curtain = K.Hex("B5533F");
        static readonly Color LitWindow = new Color(1f, 0.84f, 0.48f);

        [MenuItem("Last Orders/Shelters/Rebuild Shelter Prefab", priority = 72)]
        static void Rebuild() => EnsurePrefab(EnsureDefinition(null), rebuild: true);

        /// <summary>
        /// The definition, created with placeholder prices if it does not exist. The design names
        /// no figures, so these are pitched against what a visitor pays - 22 to 60 Coins - and
        /// what clearing the yard's debris returns in Wood.
        /// </summary>
        public static ShelterDefinition EnsureDefinition(GameBalanceConfig balance)
        {
            ContentGenerator.EnsureFolder(DefinitionFolder);

            var definition = AssetDatabase.LoadAssetAtPath<ShelterDefinition>(DefinitionPath);
            if (definition != null) return definition;

            definition = ScriptableObject.CreateInstance<ShelterDefinition>();
            definition.id = "shelter";
            definition.displayName = "Shelter";
            definition.description = "A little hut with two beds. Everyone you hire needs a bed, so each " +
                                     "shelter you build lets you take on two more survivors.";
            definition.beds = 2;
            definition.buildPrice = new ShelterPrice(50, new ResourceAmount(ResourceType.Wood, 12));

            var hires = new List<ShelterHire>();

            SurvivorRoleDefinition guard = FindRole(balance, SurvivorRole.Guard);
            if (guard != null)
            {
                hires.Add(new ShelterHire
                {
                    role = guard,
                    pitch = "stands watch in a guard tower, and takes over when a guard is hurt",
                    price = new ShelterPrice(40),
                });
            }

            SurvivorRoleDefinition medic = FindRole(balance, SurvivorRole.Medic);
            if (medic != null)
            {
                hires.Add(new ShelterHire
                {
                    role = medic,
                    pitch = "looks after the hospital tent, so patients recover faster",
                    price = new ShelterPrice(50),
                });
            }

            if (hires.Count == 0)
            {
                Debug.LogWarning("[Last Orders] No Guard or Medic role was found, so the shelter lists nobody " +
                                 "to hire. Run 'Last Orders / Generate Starting Content', then add hires to " +
                                 $"{DefinitionPath}.");
            }

            definition.hires = hires.ToArray();

            AssetDatabase.CreateAsset(definition, DefinitionPath);
            AssetDatabase.SaveAssets();
            return definition;
        }

        /// <summary>From the balance's roles first, then from the generated role assets.</summary>
        static SurvivorRoleDefinition FindRole(GameBalanceConfig balance, SurvivorRole role)
        {
            if (balance != null && balance.survivorRoles != null)
            {
                foreach (SurvivorRoleDefinition candidate in balance.survivorRoles)
                    if (candidate != null && candidate.role == role) return candidate;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:SurvivorRoleDefinition"))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<SurvivorRoleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.role == role) return candidate;
            }

            return null;
        }

        /// <summary>The prefab, built if it does not exist yet - or rebuilt over it when asked.</summary>
        public static GameObject EnsurePrefab(ShelterDefinition definition, bool rebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && !rebuild) return existing;

            ContentGenerator.EnsureFolder(PrefabFolder);
            K.Reset();

            GameObject root = BuildShelter(definition);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[Last Orders] {(existing != null ? "Rebuilt" : "Created")} the shelter prefab at {PrefabPath}.");
            return prefab;
        }

        static GameObject BuildShelter(ShelterDefinition definition)
        {
            var root = new GameObject("Shelter_Hut");

            Transform hut = K.Group("Hut", root.transform);
            BuildShell(hut);
            BuildRoof(hut);
            BuildFront(hut);

            ShelterHut.Bed[] beds =
            {
                BuildWindow(hut, "Window_0", -WindowX),
                BuildWindow(hut, "Window_1", WindowX),
            };

            Transform site = BuildSite(root.transform);

            var tap = root.AddComponent<BoxCollider>();

            var shelter = root.AddComponent<ShelterHut>();
            shelter.Configure(null, definition, "shelter", false);
            shelter.ConfigureParts(hut.gameObject, site.gameObject, beds, tap);

            return root;
        }

        // --- The hut --------------------------------------------------------------------------

        /// <summary>The floor, the back and side walls, and the patching that makes it a
        /// shelter built from whatever was to hand rather than a garden shed.</summary>
        static void BuildShell(Transform parent)
        {
            K.Box("Floor", parent, Vector3.zero, new Vector3(Width + 0.2f, 0.12f, Depth + 0.2f), K.WoodDark);

            K.Box("Wall_Back", parent, new Vector3(0f, 0f, -Depth * 0.5f), new Vector3(Width, WallHeight, 0.12f), K.Wood);
            K.Box("Wall_L", parent, new Vector3(-Width * 0.5f, 0f, 0f), new Vector3(0.12f, WallHeight, Depth), K.Wood);
            K.Box("Wall_R", parent, new Vector3(Width * 0.5f, 0f, 0f), new Vector3(0.12f, WallHeight, Depth), K.Wood);

            // Battens across the side walls, so they read as planks at the camera's distance.
            for (int i = 0; i < 3; i++)
            {
                float y = 0.45f + i * 0.55f;
                K.Cube($"Batten_L_{i}", parent, new Vector3(-Width * 0.5f - 0.07f, y, 0f), new Vector3(0.03f, 0.06f, Depth), K.WoodDark);
                K.Cube($"Batten_R_{i}", parent, new Vector3(Width * 0.5f + 0.07f, y, 0f), new Vector3(0.03f, 0.06f, Depth), K.WoodDark);
            }

            // A sheet of corrugated iron nailed over a hole in one side. Dark, so it does not
            // read as a third window.
            K.Cube("Patch", parent, new Vector3(Width * 0.5f + 0.09f, 0.95f, -0.45f), new Vector3(0.03f, 0.8f, 0.9f), K.MetalDark);

            // The gables: stacked strips narrowing to the ridge, front and back. Each is as wide
            // as the roof is at its top edge, so no corner pokes out through the roof; the sliver
            // left under the ridge is behind the ridge cap.
            const int strips = 6;
            float rise = Ridge - WallHeight;
            float eave = Width * 0.5f + 0.25f;

            foreach (float z in new[] { Depth * 0.5f, -Depth * 0.5f })
            {
                for (int i = 0; i < strips; i++)
                {
                    float top = WallHeight + rise * (i + 1) / strips;
                    float width = Mathf.Min(Width, 2f * eave * (Ridge - top) / rise - 0.1f);
                    if (width < 0.2f) continue;

                    K.Cube(z > 0f ? $"Gable_Front_{i}" : $"Gable_Back_{i}", parent,
                           new Vector3(0f, top - rise / strips * 0.5f, z),
                           new Vector3(width, rise / strips + 0.01f, 0.12f), K.WoodLight);
                }
            }
        }

        /// <summary>Two rusted panels meeting at a ridge that runs front to back, a stovepipe
        /// through one of them.</summary>
        static void BuildRoof(Transform parent)
        {
            float half = Width * 0.5f + 0.25f;
            float rise = Ridge - WallHeight;
            float slope = Mathf.Sqrt(half * half + rise * rise) + 0.1f;
            float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject panel = K.Cube(side < 0 ? "Roof_L" : "Roof_R", parent,
                                          new Vector3(side * half * 0.5f, (Ridge + WallHeight) * 0.5f + 0.04f, 0f),
                                          new Vector3(slope, 0.07f, Depth + 0.5f), K.Rust);

                // Tilted down toward its own eave.
                panel.transform.localRotation = Quaternion.Euler(0f, 0f, -side * angle);
            }

            K.Log("RidgeCap", parent, new Vector3(0f, Ridge + 0.06f, 0f), 0.12f, Depth + 0.5f, K.MetalDark, yaw: 0f);

            K.Cylinder("Stovepipe", parent, new Vector3(0.8f, 2.1f, -0.55f), 0.16f, 1f, K.MetalDark);
            K.Cylinder("StovepipeCap", parent, new Vector3(0.8f, 3.1f, -0.55f), 0.26f, 0.06f, K.MetalDark);
        }

        /// <summary>The front wall either side of the doorway, the lintel over it, the door, and
        /// a crate on the step.</summary>
        static void BuildFront(Transform parent)
        {
            float z = Depth * 0.5f;
            float sideWidth = (Width - DoorWidth) * 0.5f;
            float sideX = (Width + DoorWidth) * 0.25f;

            K.Box("Front_L", parent, new Vector3(-sideX, 0f, z), new Vector3(sideWidth, WallHeight, 0.12f), K.Wood);
            K.Box("Front_R", parent, new Vector3(sideX, 0f, z), new Vector3(sideWidth, WallHeight, 0.12f), K.Wood);
            K.Cube("Lintel", parent, new Vector3(0f, (DoorHeight + WallHeight) * 0.5f, z),
                   new Vector3(DoorWidth, WallHeight - DoorHeight, 0.12f), K.Wood);

            K.Box("Door", parent, new Vector3(0f, 0.06f, z + 0.02f), new Vector3(DoorWidth - 0.1f, DoorHeight - 0.08f, 0.06f), K.WoodDark);
            K.Cube("DoorHandle", parent, new Vector3(0.26f, 0.85f, z + 0.07f), new Vector3(0.06f, 0.06f, 0.05f), K.Metal);

            K.Box("Step", parent, new Vector3(0f, 0f, z + 0.3f), new Vector3(1.1f, 0.1f, 0.45f), K.Stone);
            K.Box("Crate", parent, new Vector3(1.25f, 0f, z + 0.45f), new Vector3(0.45f, 0.4f, 0.45f), K.WoodLight, yaw: 12f);
        }

        /// <summary>
        /// One window, standing for one bed: dark glass, a curtain that is drawn once somebody
        /// has the bed, and a warm light that comes on at night while they are home.
        /// </summary>
        static ShelterHut.Bed BuildWindow(Transform parent, string name, float x)
        {
            Transform window = K.Group(name, parent);
            window.localPosition = new Vector3(x, WindowY, Depth * 0.5f);

            K.Cube("Frame", window, new Vector3(0f, 0f, 0.07f), new Vector3(WindowSize.x + 0.12f, WindowSize.y + 0.12f, 0.03f), K.WoodDark);
            K.Cube("Glass", window, new Vector3(0f, 0f, 0.09f), new Vector3(WindowSize.x, WindowSize.y, 0.01f), Glass);

            GameObject lit = K.Cube("Lit", window, new Vector3(0f, 0f, 0.1f), new Vector3(WindowSize.x, WindowSize.y, 0.01f), LitWindow);
            lit.GetComponent<Renderer>().sharedMaterial = LitMaterial();
            lit.SetActive(false);

            // Drawn across one side of the window, so a lit window still shows its light.
            GameObject curtain = K.Cube("Curtain", window, new Vector3(-WindowSize.x * 0.22f, 0.02f, 0.11f),
                                        new Vector3(WindowSize.x * 0.56f, WindowSize.y + 0.04f, 0.01f), Curtain);
            curtain.SetActive(false);

            K.Cube("Sill", window, new Vector3(0f, -WindowSize.y * 0.5f - 0.07f, 0.11f), new Vector3(WindowSize.x + 0.2f, 0.05f, 0.1f), K.WoodLight);

            return new ShelterHut.Bed { occupied = curtain, lit = lit };
        }

        // --- The empty site -------------------------------------------------------------------

        static Transform BuildSite(Transform parent)
        {
            Transform site = K.Group("Site", parent);

            K.Cube("Pad", site, new Vector3(0f, 0.03f, 0f), new Vector3(Width + 0.2f, 0.06f, Depth + 0.2f), K.Stone);

            // Marked out, with the planks and the roof sheet waiting - somewhere a hut is meant to go.
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -Width * 0.5f : Width * 0.5f;
                float z = (i & 2) == 0 ? -Depth * 0.5f : Depth * 0.5f;
                K.Box($"Stake_{i}", site, new Vector3(x, 0f, z), new Vector3(0.1f, 0.55f, 0.1f), K.Lock);
            }

            for (int i = 0; i < 3; i++)
                K.Box($"Plank_{i}", site, new Vector3(-0.4f, 0.06f + i * 0.1f, -0.3f), new Vector3(2f, 0.1f, 0.3f), K.WoodLight, yaw: 6f - i * 5f);

            GameObject sheet = K.Cube("RoofSheet", site, new Vector3(0.9f, 0.3f, 0.5f), new Vector3(1.2f, 0.05f, 0.9f), K.Rust, yaw: -20f);
            sheet.transform.localRotation *= Quaternion.Euler(0f, 0f, 24f);

            return site;
        }

        /// <summary>Unlit, so a lit window reads at night however dark the scene is.</summary>
        static Material LitMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (material != null) return material;

            ContentGenerator.EnsureFolder(System.IO.Path.GetDirectoryName(LitMaterialPath).Replace('\\', '/'));

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");

            material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", LitWindow);
            material.color = LitWindow;

            AssetDatabase.CreateAsset(material, LitMaterialPath);
            return material;
        }
    }
}
