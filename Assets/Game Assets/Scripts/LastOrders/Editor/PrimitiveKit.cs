using System.Collections.Generic;
using LastOrders.Core;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Cubes, spheres and cylinders, in one palette.
    ///
    /// Three art packs at three different authoring scales look worse together than honest
    /// primitives do, which is what the first two dressed passes demonstrated. 20 asks for
    /// "rounded, chunky 3D shapes with warm diner colours and slightly cooler surroundings",
    /// and a kit of shapes hits that directly: every object is built from the same vocabulary,
    /// so nothing can be the wrong scale relative to anything else.
    ///
    /// Sizes are in metres and positions are centres, matching the schedule's own units, so a
    /// wall 0.25 m thick is written as 0.25 and no conversion sits between the plan and the
    /// geometry.
    ///
    /// Every shape is placed in LOCAL space. That is what lets a composite - a car, a person -
    /// be written as offsets under one positioned, rotated parent; placing in world space
    /// instead would assemble each of them back at the origin, unrotated.
    /// </summary>
    public static class PrimitiveKit
    {
        const string MaterialFolder = "Assets/Game Assets/Content/LastOrders/Materials/Stylized";

        // --- Palette -------------------------------------------------------------------
        //
        // 20: the restaurant stays the dominant building, so it holds the only saturated warm
        // colours. Everything outdoors is cooler and lower contrast, and grass, crops and paths
        // differ in value as well as hue so the plan reads without relying on colour alone.

        public static readonly Color DinerWall = Hex("F2E7D3");
        public static readonly Color DinerTrim = Hex("3FA9A2");
        public static readonly Color DinerAccent = Hex("D2574C");
        public static readonly Color DinerFloor = Hex("E4DAC6");
        public static readonly Color DinerTile = Hex("BFD8D4");
        public static readonly Color Glass = Hex("9FD0DD");

        public static readonly Color Wood = Hex("A9763F");
        public static readonly Color WoodDark = Hex("7C5230");
        public static readonly Color WoodLight = Hex("C9A063");
        public static readonly Color Cloth = Hex("D8CBA6");

        public static readonly Color Soil = Hex("6E4C30");
        public static readonly Color SoilDark = Hex("543822");
        public static readonly Color Grass = Hex("79A85C");
        public static readonly Color Leaf = Hex("5E9A55");
        public static readonly Color LeafDeep = Hex("46793F");

        public static readonly Color Metal = Hex("9AA3A9");
        public static readonly Color MetalDark = Hex("5C656C");
        public static readonly Color Stone = Hex("8E8E8C");
        public static readonly Color Rust = Hex("8B5B3E");

        public static readonly Color Asphalt = Hex("4A4A4F");
        public static readonly Color RoadLine = Hex("E6E2D6");

        public static readonly Color Skin = Hex("E7B189");
        public static readonly Color ChefWhite = Hex("F6F3ED");
        public static readonly Color Denim = Hex("46587A");
        public static readonly Color Dark = Hex("34302B");

        public static readonly Color Water = Hex("4E86B0");
        public static readonly Color Lock = Hex("E8B93C");

        public static readonly Color[] FlowerHues =
        {
            Hex("E36A6A"), Hex("E8C55A"), Hex("D98FC4"), Hex("F2F0E6"),
        };

        public static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color c);
            return c;
        }

        // --- Materials -----------------------------------------------------------------

        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static void Reset() => Cache.Clear();

        /// <summary>
        /// One shared material per colour, saved as an asset so the scene references assets
        /// rather than carrying hundreds of embedded materials, and so batching has a chance.
        /// </summary>
        public static Material MaterialFor(Color colour)
        {
            string key = ColorUtility.ToHtmlStringRGB(colour);
            if (Cache.TryGetValue(key, out Material cached) && cached != null) return cached;

            ContentGenerator.EnsureFolder(MaterialFolder);

            string path = $"{MaterialFolder}/Stylized_{key}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                                ?? Shader.Find("Standard");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = colour;

            // Flat and matte: 20 wants readable silhouettes, and a specular highlight on a
            // hundred primitives reads as noise at this camera distance.
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.08f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

            EditorUtility.SetDirty(material);
            Cache[key] = material;
            return material;
        }

        // --- Shapes ---------------------------------------------------------------------

        /// <summary>A box. Position is its centre; size is its full extent in metres.</summary>
        public static GameObject Cube(string name, Transform parent, Vector3 centre, Vector3 size,
                                      Color colour, float yaw = 0f, bool collider = false)
        {
            return Shape(PrimitiveType.Cube, name, parent, centre, size, colour, yaw, collider);
        }

        /// <summary>A box sitting ON the ground: position is the centre of its footprint.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 groundCentre, Vector3 size,
                                     Color colour, float yaw = 0f, bool collider = false)
        {
            Vector3 centre = groundCentre + Vector3.up * (size.y * 0.5f);
            return Shape(PrimitiveType.Cube, name, parent, centre, size, colour, yaw, collider);
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 centre, Vector3 size,
                                        Color colour, float yaw = 0f)
        {
            return Shape(PrimitiveType.Sphere, name, parent, centre, size, colour, yaw, false);
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 centre, float diameter,
                                        Color colour)
        {
            return Sphere(name, parent, centre, Vector3.one * diameter, colour);
        }

        /// <summary>
        /// A cylinder standing on the ground. Unity's is two units tall, which is the kind of
        /// detail that belongs in one place rather than at every call site.
        /// </summary>
        public static GameObject Cylinder(string name, Transform parent, Vector3 groundCentre,
                                          float diameter, float height, Color colour,
                                          Vector3 euler = default, bool collider = false)
        {
            Vector3 centre = groundCentre + Vector3.up * (height * 0.5f);

            var go = Make(PrimitiveType.Cylinder, name, parent, colour, collider);
            go.transform.localPosition = centre;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.transform.localRotation = Quaternion.Euler(euler);

            return go;
        }

        /// <summary>A cylinder lying along one axis - a log, a rail, a pipe.</summary>
        public static GameObject Log(string name, Transform parent, Vector3 centre, float diameter,
                                     float length, Color colour, float yaw, float tilt = 0f)
        {
            var go = Make(PrimitiveType.Cylinder, name, parent, colour, false);
            go.transform.localPosition = centre;
            go.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);

            // Laid on its side, then turned to face along the run.
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, tilt);

            return go;
        }

        public static GameObject Capsule(string name, Transform parent, Vector3 groundCentre,
                                         float diameter, float height, Color colour, float yaw = 0f)
        {
            var go = Make(PrimitiveType.Capsule, name, parent, colour, false);
            go.transform.localPosition = groundCentre + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            return go;
        }

        static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 centre,
                                Vector3 size, Color colour, float yaw, bool collider)
        {
            var go = Make(type, name, parent, colour, collider);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            return go;
        }

        static GameObject Make(PrimitiveType type, string name, Transform parent, Color colour,
                               bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            // Colliders off by default. Most of this is scenery, and a few hundred unused
            // colliders is cost with no reader - the grid, not physics, is what the systems
            // query about occupancy.
            if (!collider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }

            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(colour);

            return go;
        }

        // --- Composites -------------------------------------------------------------------

        /// <summary>
        /// A run of upright posts with rails between them - the fence of the reference image.
        ///
        /// Posts land on the metre grid and the rails are continuous, so the run reads as one
        /// barrier rather than as repeated instances of a prop.
        /// </summary>
        public static Transform Fence(string name, Transform parent, bool alongX, float line,
                                      float from, float to, float height = 1.35f,
                                      float thickness = 0.14f)
        {
            Transform root = Group(name, parent);

            int posts = Mathf.Max(2, Mathf.RoundToInt((to - from) / 1.6f) + 1);
            float step = (to - from) / (posts - 1);

            for (int i = 0; i < posts; i++)
            {
                float at = from + i * step;
                Vector3 p = alongX ? new Vector3(at, 0f, line) : new Vector3(line, 0f, at);

                Box($"Post_{i:00}", root, p, new Vector3(thickness * 1.6f, height, thickness * 1.6f), WoodDark);
            }

            // Two rails, at the heights a picket fence actually carries them.
            foreach (float h in new[] { height * 0.35f, height * 0.75f })
            {
                Vector3 centre = alongX
                    ? new Vector3((from + to) * 0.5f, h, line)
                    : new Vector3(line, h, (from + to) * 0.5f);

                Vector3 size = alongX
                    ? new Vector3(to - from, thickness, thickness * 0.8f)
                    : new Vector3(thickness * 0.8f, thickness, to - from);

                Cube($"Rail_{h:0.00}", root, centre, size, Wood);
            }

            return root;
        }

        /// <summary>A tree: one trunk, two or three overlapping canopy spheres.</summary>
        public static Transform Tree(string name, Transform parent, Vector3 at, float scale,
                                     DeterministicRandom rng)
        {
            Transform root = Group(name, parent);

            float trunk = 2.2f * scale;
            Cylinder("Trunk", root, at, 0.45f * scale, trunk, WoodDark);

            int blobs = rng.Range(2, 4);
            for (int i = 0; i < blobs; i++)
            {
                float r = (1.9f - i * 0.35f) * scale;

                Vector3 offset = new Vector3(rng.Range(-0.5f, 0.5f), trunk + i * 0.75f * scale,
                                             rng.Range(-0.5f, 0.5f)) * scale;

                Sphere($"Canopy_{i}", root, at + offset,
                       new Vector3(r * 2f, r * 1.6f, r * 2f), i == 0 ? Leaf : LeafDeep);
            }

            return root;
        }

        /// <summary>A person: capsule body, sphere head, and whatever hat says who they are.</summary>
        public static Transform Person(string name, Transform parent, Vector3 at, Color body,
                                       bool chefHat, float yaw)
        {
            Transform root = Group(name, parent);
            root.position = at;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);

            Capsule("Body", root, Vector3.zero, 0.5f, 1.25f, body);
            Sphere("Head", root, new Vector3(0f, 1.42f, 0f), 0.42f, Skin);

            if (chefHat)
            {
                Cylinder("Hat", root, new Vector3(0f, 1.55f, 0f), 0.34f, 0.3f, ChefWhite);
                Sphere("HatPuff", root, new Vector3(0f, 1.9f, 0f), 0.4f, ChefWhite);
            }
            else
            {
                // The helper is carrying the starting supplies, per 13's opening shot.
                Box("Box", root, new Vector3(0.32f, 0.72f, 0.1f), new Vector3(0.42f, 0.36f, 0.34f), Wood);
            }

            return root;
        }

        public static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
