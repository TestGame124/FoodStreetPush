using System.Collections.Generic;
using LastOrders.Core;
using UnityEditor;
using UnityEngine;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The world edge's vocabulary: its palettes, the shapes <see cref="PrimitiveKit"/> does not
    /// have (a tilted block, a flat patch of ground), glowing materials, and the pack models.
    ///
    /// Colours come from fixed palettes and never from a random mix, because every colour is a
    /// material asset: a random tint per rock would be a thousand materials and no batching.
    /// Variety comes from which palette entry, which shape and which way up.
    /// </summary>
    public static class EnvKit
    {
        const string EnvironmentFolder = "Assets/Game Assets/Content/LastOrders/Environment";

        /// <summary>
        /// The art style the generated textures and materials are made in: the game's clean,
        /// smooth low-poly look. Each style keeps its own folder of both, so the looks before it -
        /// the warm painted one ("Painted"), and the muted relief textures and materials at the
        /// folders' top level - stay in the project to go back to. <see cref="PaintedLookSetup"/>
        /// moves a scene between them.
        /// </summary>
        public const string Style = "LowPoly";

        /// <summary>The materials and textures from before <see cref="Style"/>, left as they were.</summary>
        public const string PreviousMaterialFolder = EnvironmentFolder + "/Materials";

        public const string PreviousTextureFolder = GeneratedFolder;

        public const string MaterialFolder = PreviousMaterialFolder + "/" + Style;
        public const string ShaderFolder = EnvironmentFolder + "/Shaders";

        /// <summary>Generated meshes, the fog's noise and the stains' atlas: the same whatever the style.</summary>
        public const string GeneratedFolder = EnvironmentFolder + "/Generated";

        /// <summary>The generated textures of the current <see cref="Style"/>.</summary>
        public const string TextureFolder = GeneratedFolder + "/" + Style;

        const string PackFolder = "Assets/PolygonFarm/Prefabs";

        // --- Palettes ---------------------------------------------------------------------------------
        //
        // The low-poly look: fresh greens, neutral earth and stone, and houses in a spread of
        // colours - weathered timber, off-white, blue, sand, sage, brick - so no one hue takes over.
        // Stains are see-through, so a patch's colour mixes with the grass under it.

        public static readonly Color[] Rust = { K.Hex("8B5B3E"), K.Hex("7A4A32"), K.Hex("9C6A45") };
        public static readonly Color[] Burnt = { K.Hex("3B3632"), K.Hex("4A4440"), K.Hex("2F2B28") };
        public static readonly Color[] CarPaint =
        {
            K.Hex("6F8FA6"), K.Hex("A65B4E"), K.Hex("C9C0A8"), K.Hex("7D8C6A"), K.Hex("5E6166"), K.Hex("D9D4C7"),
        };
        public static readonly Color[] Concrete = { K.Hex("A8A49C"), K.Hex("928E86"), K.Hex("BDB8AD") };
        public static readonly Color[] Weathered = { K.Hex("8C7B66"), K.Hex("7C6B55"), K.Hex("A08B6E") };
        // Plain brown earth, neither orange nor grey.
        public static readonly Color[] Dirt = { K.Hex("7A6650"), K.Hex("6E5C48"), K.Hex("857058") };
        // Darker, wetter earth, where the path is trodden barest.
        public static readonly Color[] Mud = { K.Hex("5A4A3B"), K.Hex("4F4134") };
        // Thicker grass: a fresher, lighter green than the lawn round it.
        public static readonly Color[] SunGrass = { K.Hex("66833F"), K.Hex("6E8C44"), K.Hex("60793C") };
        // Mossy, a step darker than the grass: under the toon grass a near-black floor read as holes.
        public static readonly Color[] ForestFloor = { K.Hex("3D6634"), K.Hex("375E2F"), K.Hex("426C38"), K.Hex("33572C") };
        // Weathered timber, off-white, blue, sand, sage, brick.
        public static readonly Color[] HouseWall =
        {
            K.Hex("8D7A68"), K.Hex("D8D6CC"), K.Hex("7F9CB0"), K.Hex("B9A98E"), K.Hex("93AB8E"), K.Hex("A8655A"),
        };
        public static readonly Color[] Sandbag = { K.Hex("9C8A63"), K.Hex("8A7A56"), K.Hex("A39170") };
        public static readonly Color[] Scrap = { K.Hex("8B5B3E"), K.Hex("9AA3A9"), K.Hex("5C656C"), K.Hex("7C6B55"), K.Hex("6F8FA6") };

        public static readonly Color Asphalt = K.Asphalt;
        public static readonly Color AsphaltDark = K.Hex("3A3A3E");
        public static readonly Color AsphaltPatch = K.Hex("55555A");
        public static readonly Color Scorch = K.Hex("2E2B29");
        public static readonly Color Foundation = K.Hex("9A968C");
        public static readonly Color Bark = K.Hex("5E4630");
        public static readonly Color LampGlow = K.Hex("FFD98A");
        public static readonly Color FireGlow = K.Hex("FF9A3C");

        public static Color Pick(Color[] palette, ref DeterministicRandom rng) => palette[rng.Range(0, palette.Length)];

        // --- Shapes -------------------------------------------------------------------------------------

        /// <summary>A box turned any way at all - a toppled wardrobe, a slab of wall leaning on its neighbour.</summary>
        public static GameObject Block(string name, Transform parent, Vector3 centre, Vector3 size, Vector3 euler, Color colour)
        {
            GameObject go = K.Cube(name, parent, centre, size, colour);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        /// <summary>
        /// A flat oval of ground: a patch of mud, dead grass, a scorch mark. <paramref name="layer"/>
        /// lifts it a few millimetres so two patches that overlap never fight over the same depth.
        /// </summary>
        public static GameObject Disc(string name, Transform parent, Vector2 at, float sizeX, float sizeZ, float yaw,
                                      Color colour, int layer = 0)
        {
            float y = 0.006f + layer * 0.004f;
            GameObject go = K.Cylinder(name, parent, new Vector3(at.x, y, at.y), 1f, 0.008f, colour);
            go.transform.localScale = new Vector3(sizeX, 0.004f, sizeZ);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <summary>
        /// A soft, ragged stain on the ground - mud, dead grass, scorch - made of one to three
        /// overlapping splats turned and stretched differently, so no two are the same shape.
        /// </summary>
        public static Transform Blob(string name, Transform parent, Vector2 at, float size, Color colour, int layer,
                                     ref DeterministicRandom rng, float alpha = 0.6f)
        {
            Transform root = K.Group(name, parent);
            int parts = rng.Range(1, 4);

            for (int i = 0; i < parts; i++)
            {
                Vector2 offset = i == 0 ? Vector2.zero : new Vector2(rng.Range(-0.35f, 0.35f), rng.Range(-0.35f, 0.35f)) * size;
                float sx = size * rng.Range(0.75f, 1.2f);
                float sz = size * rng.Range(0.55f, 1f);
                GroundSplats.Splat($"Part_{i}", root, at + offset, sx, sz, rng.Range(0f, 360f), colour,
                                   alpha * (i == 0 ? 1f : 0.8f), layer, ref rng);
            }

            return root;
        }

        /// <summary>A thin slab lying on the ground, top at <paramref name="top"/>.</summary>
        public static GameObject Slab(string name, Transform parent, Vector2 centre, Vector2 size, float yaw, float top,
                                      float thickness, Color colour)
        {
            return K.Cube(name, parent, new Vector3(centre.x, top - thickness * 0.5f, centre.y),
                          new Vector3(size.x, thickness, size.y), colour, yaw);
        }

        /// <summary>A world rect as a slab.</summary>
        public static GameObject Slab(string name, Transform parent, Rect rect, float top, float thickness, Color colour)
        {
            return Slab(name, parent, rect.center, rect.size, 0f, top, thickness, colour);
        }

        // --- Glowing and see-through materials -------------------------------------------------------------

        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static void Reset()
        {
            Cache.Clear();
            GroundSplats.Reset();
        }

        /// <summary>An unlit colour: a lamp that is on, whatever the sun is doing.</summary>
        public static Material Glow(Color colour)
        {
            string key = "Glow_" + ColorUtility.ToHtmlStringRGB(colour);
            return CachedMaterial(key, () =>
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                var m = new Material(shader);
                m.SetColor("_BaseColor", colour);
                return m;
            }, m => m.SetColor("_BaseColor", colour));
        }

        /// <summary>A soft round spot from the SoftBlob shader: additive for light, blended for mist.</summary>
        public static Material SoftSpot(string name, Color colour, bool additive, bool nightOnly, bool fogTint)
        {
            return CachedMaterial("Soft_" + name, () => new Material(Shader.Find("LastOrders/SoftBlob")), m =>
            {
                m.SetColor("_Color", colour);
                m.SetFloat("_Softness", additive ? 0.9f : 0.75f);
                m.SetFloat("_NightOnly", nightOnly ? 1f : 0f);
                m.SetFloat("_UseFogTint", fogTint ? 1f : 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", additive
                    ? (float)UnityEngine.Rendering.BlendMode.One
                    : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.renderQueue = 3100;
            });
        }

        /// <summary>
        /// A lit, textured surface - siding, shingles, sheet metal - tinted by <paramref name="colour"/>
        /// and tiled so one copy of the texture covers <paramref name="metresPerTile"/>, for meshes UV'd
        /// in metres (<see cref="EnvMesh"/>). One material per texture and tint, shared by every house.
        /// </summary>
        public static Material Surface(string kind, Texture2D texture, Color colour, float metresPerTile)
        {
            string key = $"{kind}_{ColorUtility.ToHtmlStringRGB(colour)}";
            return CachedMaterial(key, () => new Material(Shader.Find("Universal Render Pipeline/Lit")), m =>
            {
                m.SetTexture("_BaseMap", texture);
                m.SetTextureScale("_BaseMap", Vector2.one / metresPerTile);
                m.SetColor("_BaseColor", colour);
                m.SetFloat("_Smoothness", 0.08f);
                m.SetFloat("_Metallic", 0f);
                m.enableInstancing = true;
            });
        }

        /// <summary>A darker copy of a pack material, for trees that stand in the gloom at the back.</summary>
        public static Material Dimmed(Material source, string suffix, Color multiply)
        {
            if (source == null) return null;

            return CachedMaterial($"{source.name}_{suffix}", () => new Material(source), m =>
            {
                m.CopyPropertiesFromMaterial(source);
                Color baseColour = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColour * multiply);
                m.enableInstancing = true;
            });
        }

        public static Material CachedMaterial(string key, System.Func<Material> create, System.Action<Material> configure)
        {
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            ContentGenerator.EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{key}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = create();
                AssetDatabase.CreateAsset(material, path);
            }

            configure?.Invoke(material);
            EditorUtility.SetDirty(material);

            Cache[key] = material;
            return material;
        }

        /// <summary>A unit quad lying flat, facing up, for a soft spot on the ground.</summary>
        public static GameObject GroundSpot(string name, Transform parent, Vector3 centre, float size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Over the ground stains, under the fog: a lamp in the mist is a glow, not a hole in it.
            renderer.sortingOrder = EdgeFogBuilder.FogSortingOrder - 10;

            return go;
        }

        // --- The pack's models -------------------------------------------------------------------------------

        static readonly Dictionary<string, GameObject> Packs = new Dictionary<string, GameObject>();

        /// <summary>Pack prefabs that were asked for and are not in the project, for the report.</summary>
        public static readonly HashSet<string> Missing = new HashSet<string>();

        public static void ResetPacks()
        {
            Packs.Clear();
            Missing.Clear();
        }

        /// <summary>A POLYGON Farm model, by its path under Prefabs (e.g. "Generic/SM_Generic_Tree_01").</summary>
        public static GameObject LoadPack(string relative)
        {
            if (Packs.TryGetValue(relative, out GameObject cached)) return cached;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackFolder}/{relative}.prefab");
            if (prefab == null) Missing.Add(relative);

            Packs[relative] = prefab;
            return prefab;
        }

        /// <summary>
        /// Places a pack model, linked to its prefab so the scene only stores where it stands.
        /// Null when the pack is missing: callers build a shape instead.
        /// </summary>
        public static GameObject Pack(string relative, Transform parent, Vector3 localPosition, float yaw, float scale,
                                      Material replace = null, Vector3 tilt = default)
        {
            GameObject prefab = LoadPack(relative);
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt);
            instance.transform.localScale = Vector3.one * scale;

            if (replace != null)
            {
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] shared = r.sharedMaterials;
                    for (int i = 0; i < shared.Length; i++)
                    {
                        if (shared[i] != null && replace.name.StartsWith(shared[i].name)) shared[i] = replace;
                    }
                    r.sharedMaterials = shared;
                }
            }

            return instance;
        }

        /// <summary>One of several pack models, picked at random.</summary>
        public static GameObject PackOf(string[] choices, Transform parent, Vector3 localPosition, float yaw, float scale,
                                        ref DeterministicRandom rng, Material replace = null)
        {
            return Pack(choices[rng.Range(0, choices.Length)], parent, localPosition, yaw, scale, replace);
        }

        public static readonly string[] Trees =
        {
            "Generic/SM_Generic_Tree_01", "Generic/SM_Generic_Tree_02", "Generic/SM_Generic_Tree_03", "Generic/SM_Generic_Tree_04",
        };

        public static readonly string[] TreePatches = { "Generic/SM_Generic_Tree_Patch_01", "Generic/SM_Generic_Tree_Patch_02" };

        public const string DeadTree = "Generic/SM_Generic_TreeDead_01";
        public const string BigTree = "Environments/SM_Env_Tree_Large_01";

        public static readonly string[] Stumps =
        {
            "Generic/SM_Generic_TreeStump_01", "Environments/SM_Env_Tree_Stump_01", "Environments/SM_Env_Tree_Stump_02",
            "Environments/SM_Env_Tree_Stump_03",
        };

        public static readonly string[] Rocks =
        {
            "Generic/SM_Generic_Small_Rocks_01", "Generic/SM_Generic_Small_Rocks_02", "Generic/SM_Generic_Small_Rocks_03",
            "Generic/SM_Generic_Small_Rocks_04", "Generic/SM_Generic_Small_Rocks_05",
        };

        public static readonly string[] Grass =
        {
            "Generic/SM_Generic_Grass_Patch_01", "Generic/SM_Generic_Grass_Patch_02", "Generic/SM_Generic_Grass_Patch_03",
        };

        public static readonly string[] Bushes =
        {
            "Plants/SM_Prop_Plant_Bush_01_L", "Plants/SM_Prop_Plant_Bush_02_L", "Plants/SM_Prop_Plant_Bush_03_L",
            "Plants/SM_Prop_Plant_Bush_01_M", "Plants/SM_Prop_Plant_Bush_02_M", "Plants/SM_Prop_Plant_Bush_03_M",
        };

        public static readonly string[] Mailboxes =
        {
            "Props/SM_Prop_LetterBox_01", "Props/SM_Prop_LetterBox_02", "Props/SM_Prop_LetterBox_03",
            "Props/SM_Prop_LetterBox_04", "Props/SM_Prop_LetterBox_05", "Props/SM_Prop_LetterBox_06",
        };

        public static readonly string[] Wrecks = { "Props/SM_Prop_Ute_Wreck_01", "Props/SM_Prop_Truck_Rusted_01" };

        public static readonly string[] Drums = { "Props/SM_Prop_Barrel_01", "Props/SM_Prop_Barrel_02", "Props/SM_Prop_Rusted_Drum_01" };

        public static readonly string[] RubbishPiles =
        {
            "Props/SM_Prop_Rubbish_Pile_01", "Props/SM_Prop_Rubbish_Pile_02", "Props/SM_Prop_Rubbish_Pile_03",
            "Props/SM_Prop_Rubbish_Pile_04", "Props/SM_Prop_Rubbish_Pile_05",
        };

        public static readonly string[] Rubbish =
        {
            "Props/SM_Prop_Rubbish_01", "Props/SM_Prop_Rubbish_02", "Props/SM_Prop_Rubbish_03", "Props/SM_Prop_Rubbish_04",
            "Props/SM_Prop_Rubbish_05", "Props/SM_Prop_Rubbish_06",
        };

        public static readonly string[] WoodFences = { "Props/SM_Prop_Fence_Wood_01", "Props/SM_Prop_Fence_Wood_02", "Props/SM_Prop_Fence_Wood_03" };

        public static readonly string[] PaintedFences = { "Props/SM_Prop_Fence_Painted_01", "Props/SM_Prop_Fence_Painted_02" };

        public const string Tyre = "Props/SM_Prop_Tyre_01";
        public const string PowerPole = "Props/SM_Prop_Power_Pole_01";
        public const string WoodStack = "Props/SM_Prop_Wood_Stack_01";
        public const string PalletCrate = "Props/SM_Prop_PalletCrate_01";
        public const string Crate = "Props/SM_Prop_Crate_01";
        public const string Bin = "Props/SM_Prop_PlasticBin_01";
        public const string Gastank = "Props/SM_Prop_Gastank_01";
    }
}
