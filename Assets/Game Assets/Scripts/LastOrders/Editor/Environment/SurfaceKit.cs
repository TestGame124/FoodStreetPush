using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using E = LastOrders.EditorTools.EnvKit;
using T = LastOrders.EditorTools.TextureGenerator;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Textured surfaces for everything the level drives, walks and parks on, and the diner's walls.
    ///
    /// Each surface is one mesh with its markings in its texture. The road used to be a slab, a
    /// kerb each side and seventy-odd dash cubes; the car park a slab and three cubes per bay. Now
    /// the road is one renderer and the car park another, and a line cannot drift off its surface
    /// or flicker against it, because it is painted on.
    ///
    /// Meshes are UV'd for their textures: the road and the streets along their length and across
    /// their width, so the markings run down the middle; slabs in metres, so every slab's grain is
    /// the same size; the car park over its own rectangle, because its texture is painted for it.
    /// </summary>
    public static class SurfaceKit
    {
        const string DinerWallMeshPath = EnvKit.GeneratedFolder + "/DinerWall_Box.asset";

        // --- Materials ------------------------------------------------------------------------------------

        public static Material Road() => Textured("Surface_Road", T.EnsureRoad(), Vector2.one, 0.12f);

        public static Material Street() => Textured("Surface_Street", T.EnsureStreet(), Vector2.one, 0.1f);

        public static Material Asphalt() => Textured("Surface_Asphalt", T.EnsureAsphalt(), Vector2.one / T.AsphaltTile, 0.1f);

        public static Material Kerb() => Textured("Surface_Kerb", T.EnsureKerb(), Vector2.one / T.KerbTile, 0.06f);

        public static Material Pavement() => Textured("Surface_Pavement", T.EnsurePavement(), Vector2.one / T.PavementTile, 0.06f);

        public static Material DirtPath() => Textured("Surface_DirtPath", T.EnsureDirtPath(), Vector2.one / T.DirtPathTile, 0f);

        /// <summary>The compound wall with <paramref name="panel"/> between its footing and cap.</summary>
        public static Material CompoundWall(T.WallPanel panel = T.WallPanel.Concrete) =>
            Textured(panel switch
            {
                T.WallPanel.Planks => "Wall_Planks",
                T.WallPanel.Tin => "Wall_Tin",
                _ => "Wall_ConcreteBlock",
            }, T.EnsureCompoundWall(panel), Vector2.one, panel == T.WallPanel.Tin ? 0.2f : 0.05f);

        /// <summary>The concrete wall a shade darker and greyer: the pillars, so they read apart from the panels.</summary>
        public static Material CompoundWallDark() =>
            Textured("Wall_ConcreteBlock_Dark", T.EnsureCompoundWall(), Vector2.one, 0.05f, new Color(0.74f, 0.75f, 0.77f));

        public static Material DinerWall() => Textured("Wall_Diner", T.EnsureDinerWall(), Vector2.one, 0.1f);

        /// <summary>
        /// Every surface material again, after their textures have been repainted: the road, the
        /// streets, the drive, kerbs, pavements, the dirt path, the car park and the walls. Their
        /// textures keep their paths, so a scene already on these materials is not touched. Call
        /// <see cref="EnvKit.Reset"/> first, or materials already set up this session are handed
        /// back as they were.
        /// </summary>
        public static string RestyleSurfaces()
        {
            Road();
            Street();
            Asphalt();
            Kerb();
            Pavement();
            DirtPath();
            foreach (T.WallPanel panel in System.Enum.GetValues(typeof(T.WallPanel))) CompoundWall(panel);
            CompoundWallDark();
            DinerWall();

            Texture2D parking = T.RepaintParking();
            if (parking == null)
                return "Surfaces: road, streets, drive, kerbs, pavements, path and walls up to date; the car park was not " +
                       "repainted - its bay rectangles were not kept with its texture.";

            Textured("Surface_Parking", parking, Vector2.one, 0.1f);
            return "Surfaces: road, streets, drive, kerbs, pavements, path and walls up to date; car park repainted.";
        }

        /// <summary>URP Lit with a generated texture and its normal map if it has one, matte, no reflections.</summary>
        static Material Textured(string key, Texture2D texture, Vector2 tiling, float smoothness, Color? tint = null)
        {
            return E.CachedMaterial(key, () => new Material(Shader.Find("Universal Render Pipeline/Lit")), m =>
            {
                m.SetTexture("_BaseMap", texture);
                m.SetTextureScale("_BaseMap", tiling);
                m.SetColor("_BaseColor", tint ?? Color.white);
                T.BindNormal(m, texture);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                m.enableInstancing = true;
            });
        }

        // --- The visitors' road -----------------------------------------------------------------------------

        /// <summary>
        /// The road from <paramref name="west"/> to <paramref name="east"/> as one renderer: the
        /// carriageway, its markings in its texture, standing <paramref name="top"/> proud of the
        /// ground, and a kerb each side between <paramref name="kerbWest"/> and <paramref name="kerbEast"/>.
        /// </summary>
        public static GameObject Road(Transform parent, float west, float east, float centreZ, float width, float top,
                                      float kerbWest, float kerbEast, float kerbWidth, float kerbHeight)
        {
            var mesh = new EnvMesh(2);
            float south = centreZ - width * 0.5f, north = centreZ + width * 0.5f;

            Vector2 UV(float x, float across) => new Vector2(x / T.RoadTile, across);

            mesh.Quad(0, new Vector3(west, top, south), new Vector3(west, top, north), new Vector3(east, top, north),
                      new Vector3(east, top, south), UV(west, 0f), UV(west, 1f), UV(east, 1f), UV(east, 0f), Vector3.up);

            // The slab's edges, in the plain strip of asphalt outside the edge lines.
            foreach (float z in new[] { south, north })
            {
                float v = z < centreZ ? 0.01f : 0.99f;
                mesh.Quad(0, new Vector3(west, 0f, z), new Vector3(west, top, z), new Vector3(east, top, z), new Vector3(east, 0f, z),
                          UV(west, v), UV(west, v), UV(east, v), UV(east, v), new Vector3(0f, 0f, z < centreZ ? -1f : 1f));
            }

            if (kerbEast > kerbWest)
            {
                foreach (float side in new[] { 1f, -1f })
                {
                    var centre = new Vector3((kerbWest + kerbEast) * 0.5f, kerbHeight * 0.5f,
                                             centreZ + side * (width * 0.5f + kerbWidth * 0.5f));
                    mesh.Box(1, centre, new Vector3(kerbEast - kerbWest, kerbHeight, kerbWidth), Quaternion.identity);
                }
            }

            return Place(parent, "Road", mesh, new[] { Road(), Kerb() }, castShadows: false);
        }

        // --- The suburbs' streets ----------------------------------------------------------------------------

        /// <summary>A side street as one slab, UV'd along its length and across its width, its faded dash in its texture.</summary>
        public static GameObject Street(Transform parent, Rect surface, bool alongX, float top, float thickness)
        {
            var mesh = new EnvMesh(1);
            float bottom = top - thickness;

            // Corners in order round the slab, each with (along, across) for its UV.
            Vector3 P(float x, float z, float y) => new Vector3(x, y, z);
            Vector2 UV(float x, float z) => alongX
                ? new Vector2(x / T.StreetTile, (z - surface.yMin) / surface.height)
                : new Vector2(z / T.StreetTile, (surface.xMax - x) / surface.width);

            float x0 = surface.xMin, x1 = surface.xMax, z0 = surface.yMin, z1 = surface.yMax;
            mesh.Quad(0, P(x0, z0, top), P(x0, z1, top), P(x1, z1, top), P(x1, z0, top),
                      UV(x0, z0), UV(x0, z1), UV(x1, z1), UV(x1, z0), Vector3.up);

            // The four edges, in the plain asphalt at the texture's margin.
            void Edge(Vector3 a, Vector3 b, Vector3 outward)
            {
                Vector2 ua = UV(a.x, a.z), ub = UV(b.x, b.z);
                mesh.Quad(0, new Vector3(a.x, bottom, a.z), new Vector3(a.x, top, a.z), new Vector3(b.x, top, b.z),
                          new Vector3(b.x, bottom, b.z), ua, ua, ub, ub, outward);
            }

            Edge(P(x0, z0, 0f), P(x1, z0, 0f), Vector3.back);
            Edge(P(x0, z1, 0f), P(x1, z1, 0f), Vector3.forward);
            Edge(P(x0, z0, 0f), P(x0, z1, 0f), Vector3.left);
            Edge(P(x1, z0, 0f), P(x1, z1, 0f), Vector3.right);

            return Place(parent, "Surface", mesh, new[] { Street() }, castShadows: false);
        }

        /// <summary>Collects a street's pavement slabs into one mesh, UV'd in metres so the slab joints in the texture line up.</summary>
        public sealed class PavementRun
        {
            readonly EnvMesh mesh = new EnvMesh(1);

            public void Add(Vector2 centre, Vector2 size, float top, float thickness) =>
                mesh.Box(0, new Vector3(centre.x, top - thickness * 0.5f, centre.y), new Vector3(size.x, thickness, size.y),
                         Quaternion.identity);

            public GameObject Build(Transform parent) =>
                mesh.IsEmpty ? null : Place(parent, "Pavement", mesh, new[] { Pavement() }, castShadows: false);
        }

        // --- Slabs and the car park ---------------------------------------------------------------------------

        /// <summary>A flat slab over <paramref name="area"/>, its top at <paramref name="top"/>, UV'd in metres.</summary>
        public static GameObject Slab(Transform parent, string name, Rect area, float top, float thickness, Material material)
        {
            var mesh = new EnvMesh(1);
            mesh.Box(0, new Vector3(area.center.x, top - thickness * 0.5f, area.center.y), new Vector3(area.width, thickness, area.height),
                     Quaternion.identity);
            return Place(parent, name, mesh, new[] { material }, castShadows: false);
        }

        /// <summary>
        /// The car park: one slab over <paramref name="apron"/> with every bay line and stop line
        /// in <paramref name="markings"/> (world rectangles) painted into its own texture.
        /// </summary>
        public static GameObject Parking(Transform parent, Rect apron, IReadOnlyList<Rect> markings, float top, float thickness)
        {
            Texture2D painted = T.PaintParking(apron, markings);
            Material material = Textured("Surface_Parking", painted, Vector2.one, 0.1f);

            var mesh = new EnvMesh(1);
            Vector2 UV(float x, float z) => new Vector2((x - apron.xMin) / apron.width, (z - apron.yMin) / apron.height);

            float x0 = apron.xMin, x1 = apron.xMax, z0 = apron.yMin, z1 = apron.yMax, bottom = top - thickness;
            mesh.Quad(0, new Vector3(x0, top, z0), new Vector3(x0, top, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0),
                      UV(x0, z0), UV(x0, z1), UV(x1, z1), UV(x1, z0), Vector3.up);

            (Vector3 a, Vector3 b, Vector3 n)[] edges =
            {
                (new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), Vector3.back),
                (new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), Vector3.forward),
                (new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), Vector3.left),
                (new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), Vector3.right),
            };

            foreach (var (a, b, n) in edges)
            {
                Vector2 ua = UV(a.x, a.z), ub = UV(b.x, b.z);
                mesh.Quad(0, new Vector3(a.x, bottom, a.z), new Vector3(a.x, top, a.z), new Vector3(b.x, top, b.z),
                          new Vector3(b.x, bottom, b.z), ua, ua, ub, ub, n);
            }

            return Place(parent, "Apron", mesh, new[] { material }, castShadows: false);
        }

        /// <summary>
        /// Brings a visitor system built before this kit up to date, in place: the drive, the path
        /// and the car park become textured slabs where the flat-coloured cubes were, and the bays'
        /// line and stop cubes are painted into the car park's texture and removed. The bays
        /// themselves - the parking slots cars aim for - are not touched. Does nothing to a system
        /// already brought up to date. Returns what it did, for the report.
        /// </summary>
        public static string RestyleYard(Transform visitorSystem)
        {
            if (visitorSystem == null) return "No VisitorSystem; the drive and car park were left alone.";

            int slabs = 0;
            Transform drive = visitorSystem.Find("Drive");
            if (drive != null)
            {
                foreach ((string name, bool asphalt) in new[] { ("Drive", true), ("DriveToApron", true), ("PathToDoor", false) })
                {
                    Transform old = drive.Find(name);
                    if (!IsLegacyCube(old)) continue;

                    Bounds b = old.GetComponent<Renderer>().bounds;
                    Undo.DestroyObjectImmediate(old.gameObject);

                    GameObject slab = Slab(drive, name, Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z), b.max.y, b.size.y,
                                           asphalt ? Asphalt() : DirtPath());
                    Undo.RegisterCreatedObjectUndo(slab, "Restyle drive");
                    slabs++;
                }
            }

            int lines = 0;
            Transform parking = visitorSystem.Find("Parking");
            Transform apronCube = parking != null ? parking.Find("Apron") : null;
            if (IsLegacyCube(apronCube))
            {
                var markings = new List<Rect>();
                var doomed = new List<GameObject>();

                foreach (Renderer r in parking.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name != "Line" && r.name != "Stop") continue;
                    Bounds b = r.bounds;
                    markings.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
                    doomed.Add(r.gameObject);
                }

                Bounds apron = apronCube.GetComponent<Renderer>().bounds;
                doomed.Add(apronCube.gameObject);
                foreach (GameObject go in doomed) Undo.DestroyObjectImmediate(go);

                GameObject built = Parking(parking, Rect.MinMaxRect(apron.min.x, apron.min.z, apron.max.x, apron.max.z), markings,
                                           apron.max.y, apron.size.y);
                built.transform.SetSiblingIndex(0);
                Undo.RegisterCreatedObjectUndo(built, "Restyle car park");
                lines = markings.Count;
            }

            return slabs == 0 && lines == 0
                ? "Drive and car park already textured."
                : $"{slabs} drive and path slabs textured; {lines} bay line meshes painted into the car park's texture and removed.";
        }

        /// <summary>A primitive cube from before this kit: a Unity cube mesh with one flat-coloured material.</summary>
        static bool IsLegacyCube(Transform t)
        {
            var filter = t != null ? t.GetComponent<MeshFilter>() : null;
            return filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "Cube";
        }

        // --- The diner's walls -----------------------------------------------------------------------------

        /// <summary>
        /// A unit cube like Unity's, bounds and all, UV'd for the diner wall's bands: up every side
        /// V is the height (0 at the bottom, 1 at the top), and the top face sits in the cap's band
        /// alone. The walls are stretched along their length as the building grows; the texture only
        /// changes with height, so it does not stretch with them.
        /// </summary>
        public static Mesh DinerWallMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(DinerWallMeshPath);
            if (existing != null) return existing;

            var mesh = new EnvMesh(1);
            float h = 0.5f;

            // Sides: U along the face (anything - the bands are level), V the height.
            (Vector3 n, Vector3 u)[] sides =
            {
                (Vector3.forward, Vector3.right), (Vector3.back, Vector3.left), (Vector3.right, Vector3.back), (Vector3.left, Vector3.forward),
            };

            foreach (var (n, u) in sides)
            {
                Vector3 c = n * h;
                mesh.Quad(0, c - u * h - Vector3.up * h, c - u * h + Vector3.up * h, c + u * h + Vector3.up * h, c + u * h - Vector3.up * h,
                          new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), n);
            }

            var cap = new Vector2(0.5f, 0.97f);
            mesh.Quad(0, new Vector3(-h, h, -h), new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, h, -h),
                      cap, cap, cap, cap, Vector3.up);

            var base_ = new Vector2(0.5f, 0.2f);
            mesh.Quad(0, new Vector3(-h, -h, -h), new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, -h, -h),
                      base_, base_, base_, base_, Vector3.down);

            Mesh built = mesh.Build("DinerWall_Box", out _);
            ContentGenerator.EnsureFolder(EnvKit.GeneratedFolder);
            AssetDatabase.CreateAsset(built, DinerWallMeshPath);
            return built;
        }

        // --- Plumbing ------------------------------------------------------------------------------------------

        static GameObject Place(Transform parent, string name, EnvMesh mesh, Material[] materials, bool castShadows)
        {
            Mesh built = mesh.Build(name, out List<int> used);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = built;

            var renderer = go.AddComponent<MeshRenderer>();
            var shared = new Material[used.Count];
            for (int i = 0; i < used.Count; i++) shared[i] = materials[used[i]];
            renderer.sharedMaterials = shared;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }
    }
}
