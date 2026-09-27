using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Makes the boxes and quads the blockout is built from.
    ///
    /// Primitives rather than ProBuilder or generated prefabs. A Unity cube is a unit cube, so
    /// localScale is literally the footprint - no mesh authoring, no rounding, and a scene diff
    /// that reads straight against BlockoutSchedule. ProBuilder would pull an assembly
    /// dependency into a magic-folder editor script to buy editability a regenerated blockout
    /// does not want, and per-object prefabs would mean two dozen assets whose only content is
    /// a scale value that already lives in the schedule.
    /// </summary>
    public static class BlockoutGeometry
    {
        const string MaterialFolder = "Assets/Game Assets/Content/LastOrders/Materials";

        public enum Palette { Structure, Furniture, Prop, Locked, Ground, Route }

        static readonly Dictionary<Palette, Material> Cache = new Dictionary<Palette, Material>();

        /// <summary>Clears the per-run material cache. Call once at the start of a build.</summary>
        public static void ResetCache() => Cache.Clear();

        // --- Boxes ------------------------------------------------------------------

        /// <summary>
        /// A world-aligned box occupying exactly the given footprint, sitting on Y = 0.
        ///
        /// The facing yaw goes on a child, never here: this transform carries the scale, and
        /// rotating a scaled transform would transpose the width and depth that 16 has already
        /// resolved into world axes.
        /// </summary>
        public static GameObject Footprint(in BlockoutSchedule.Footprint f, Transform parent,
                                           Palette palette, bool collider = true)
        {
            Vector3 centre = BlockoutSchedule.CentreOf(f, f.Height * 0.5f);
            GameObject go = Box($"{f.Id}_{f.Label.Replace(" ", string.Empty)}", parent, centre,
                                new Vector3(f.SizeX, Mathf.Max(f.Height, 0.01f), f.SizeZ),
                                palette, collider);

            if (f.Face != BlockoutSchedule.Facing.Any)
            {
                var facing = new GameObject("Facing");
                facing.transform.SetParent(go.transform, false);

                // Undo the parent's non-uniform scale so the child is a clean unit frame for
                // art and interaction points to attach to.
                facing.transform.localScale = new Vector3(
                    1f / Mathf.Max(f.SizeX, 1e-4f),
                    1f / Mathf.Max(f.Height, 1e-4f),
                    1f / Mathf.Max(f.SizeZ, 1e-4f));

                facing.transform.localRotation = Quaternion.Euler(0f, BlockoutSchedule.YawOf(f.Face), 0f);
            }

            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 centre, Vector3 size,
                                     Palette palette, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;

            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.localScale = size;

            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(palette);
            return go;
        }

        // --- Rect-based walls ---------------------------------------------------------

        /// <summary>
        /// A box occupying exactly the given world-XZ rectangle, sitting on Y = 0.
        ///
        /// Walls are specified as rectangles rather than as a centreline plus an offset
        /// because the two wall families in this plan sit differently against their lines: 17
        /// says the diner footprint INCLUDES its 0.25 m walls, so those run inward from the
        /// footprint edge, while the perimeter fence straddles the centrelines 16 gives. One
        /// offset convention cannot express both, and a rectangle removes the question.
        /// </summary>
        public static GameObject BoxForRect(string name, Transform parent, Rect area, float height,
                                            Palette palette, bool collider = true)
        {
            return Box(name, parent,
                       new Vector3(area.center.x, height * 0.5f, area.center.y),
                       new Vector3(area.width, Mathf.Max(height, 0.01f), area.height),
                       palette, collider);
        }

        /// <summary>A wall rectangle straddling its centreline. Used for the perimeter fence.</summary>
        public static Rect StraddlingWall(bool alongX, float line, float from, float to, float thickness)
        {
            return alongX
                ? new Rect(from, line - thickness * 0.5f, to - from, thickness)
                : new Rect(line - thickness * 0.5f, from, thickness, to - from);
        }

        /// <summary>
        /// A wall rectangle lying inside its line. Used for the diner shell, whose 12 x 10 m
        /// footprint contains its own walls.
        /// </summary>
        public static Rect InsetWall(bool alongX, float line, float from, float to,
                                     float thickness, bool inwardIsPositive)
        {
            float lo = inwardIsPositive ? line : line - thickness;

            return alongX
                ? new Rect(from, lo, to - from, thickness)
                : new Rect(lo, from, thickness, to - from);
        }

        // --- Ground quads -------------------------------------------------------------

        /// <summary>
        /// A flat, collider-less quad for a route strip or a table envelope.
        ///
        /// Quad rather than Plane: Unity's Plane is 10x10 units and 100 triangles, the Quad is
        /// 1x1 and two. Lifted slightly off the ground so it does not z-fight the world plane.
        /// </summary>
        public static GameObject GroundQuad(string name, Transform parent, Rect area,
                                            Palette palette, float y = 0.02f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;

            Object.DestroyImmediate(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(area.center.x, y, area.center.y);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(area.width, area.height, 1f);

            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(palette);
            return go;
        }

        // --- Materials -----------------------------------------------------------------

        /// <summary>
        /// Create-or-load a shared URP material, following the fallback chain PrefabGenerator
        /// established. The project is URP, so a primitive's default built-in material renders
        /// magenta - creating one explicitly is the difference between a visible blockout and a
        /// field of error-shader boxes.
        /// </summary>
        public static Material MaterialFor(Palette palette)
        {
            if (Cache.TryGetValue(palette, out Material cached) && cached != null) return cached;

            bool unlit = palette == Palette.Route;

            Shader shader = unlit
                ? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")
                : Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                  ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[Blockout] No usable shader found for the blockout materials.");
                return null;
            }

            ContentGenerator.EnsureFolder(MaterialFolder);

            string path = $"{MaterialFolder}/Blockout_{palette}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.color = ColourFor(palette);

            if (unlit) MakeTransparent(material);

            EditorUtility.SetDirty(material);
            Cache[palette] = material;
            return material;
        }

        /// <summary>
        /// 20 asks for warm diner colours against slightly cooler surroundings, and for values
        /// to differ as well as hues so the plan stays readable without relying on colour
        /// alone. These are greybox stand-ins chosen on that basis.
        /// </summary>
        static Color ColourFor(Palette palette) => palette switch
        {
            Palette.Structure => new Color(0.72f, 0.69f, 0.63f),
            Palette.Furniture => new Color(0.80f, 0.62f, 0.44f),
            Palette.Prop      => new Color(0.55f, 0.66f, 0.48f),
            Palette.Locked    => new Color(0.45f, 0.46f, 0.49f),
            Palette.Ground    => new Color(0.42f, 0.50f, 0.36f),
            Palette.Route     => new Color(0.85f, 0.74f, 0.53f, 0.35f),
            _                 => Color.white,
        };

        /// <summary>
        /// Switches a URP material to the transparent surface type. Route strips are diagram
        /// overlays; opaque ones read as geometry and get mistaken for buildable ground.
        /// </summary>
        static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);              // 0 opaque, 1 transparent
            material.SetFloat("_Blend", 0f);                // alpha
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
    }
}
