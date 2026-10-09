using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Housing;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the shelter: its definition and its prefab - the hut, and the empty site it
    /// stands on before it is bought.
    ///
    /// The hut is a little timber cabin in the game's low-poly style (see <see cref="BuildingKit"/>):
    /// weathered boards lapped like clapboard between dark corner posts on a stone-footed sill, a
    /// blue tin roof with a stovepipe, a teal-painted plank door under a little
    /// canopy, a lantern beside it, flower boxes under the windows, a woodpile and a water barrel.
    /// Somewhere a survivor would want to come home to, put together from whatever was to hand.
    /// The empty site is a dirt pad pegged out with twine, the timber and the roof sheets stacked
    /// on it waiting.
    ///
    /// Local +Z is the front. The door is in the middle of it with a window either side, one per
    /// bed, so from the camera the two windows say how many people the hut holds. Each window has
    /// the two parts <see cref="ShelterHut"/> draws its beds with: a curtain, drawn once somebody
    /// has the bed, and a warm light, on at night while they are home.
    ///
    /// Rebuilding dresses the prefab in place: the hut, the site and the root stay the same
    /// objects, so the huts in a scene keep where they stand, their ids and whether they are
    /// built. The definition's numbers are filled in only when it is created, so a tuned price
    /// survives a re-run.
    /// </summary>
    public static class ShelterContentGenerator
    {
        public const string DefinitionFolder = "Assets/Game Assets/Content/LastOrders/Housing";
        public const string DefinitionPath = DefinitionFolder + "/Shelter_Hut.asset";

        public const string PrefabFolder = "Assets/Game Assets/Content/LastOrders/Prefabs/Housing";
        public const string PrefabPath = PrefabFolder + "/Shelter_Hut.prefab";

        const string HutMeshPath = B.Folder + "/ShelterHut.asset";
        const string SiteMeshPath = B.Folder + "/ShelterSite.asset";
        const string CurtainMeshPath = B.Folder + "/ShelterHut_Curtain.asset";
        const string WindowGlowMeshPath = B.Folder + "/ShelterHut_WindowGlow.asset";

        // --- Measurements (metres, local space, the floor's middle at the origin) -----------------

        const float Width = 3.2f;
        const float Depth = 2.8f;

        /// <summary>Top of the sill the walls stand on.</summary>
        const float Sill = 0.15f;

        /// <summary>Top of the walls; the top plate sits on it, and the roof on that.</summary>
        const float WallHeight = 1.85f;

        /// <summary>The roof's underside over the side walls, and how much it climbs per metre in toward the ridge.
        /// High enough that the top plate, which reaches out past the wall, stays under it.</summary>
        const float Eave = 2f;

        const float Slope = 0.58f;

        /// <summary>How far the roof reaches past the side walls, and past the gables front and back.</summary>
        const float Overhang = 0.3f;

        const float GableOverhang = 0.28f;

        const float DoorWidth = 0.9f;
        const float DoorHeight = 1.55f;

        /// <summary>Where each window sits across the front: centred on the wall either side of the door.</summary>
        const float WindowX = (Width * 0.5f + DoorWidth * 0.5f) * 0.5f;

        const float WindowY = 1.1f;

        static readonly Vector2 WindowSize = new Vector2(0.5f, 0.42f);

        /// <summary>The front face of the boards, where a window is set. The lapped boards' lower edges
        /// kick out a little past it, so what lies on the window stands proud of it by these.</summary>
        const float FrontFace = Depth * 0.5f + 0.045f;

        const float GlassZ = 0.012f, GlowZ = 0.016f, CurtainZ = 0.025f, RodZ = 0.036f;

        static readonly Color FlowerBox = B.Hex("9E5A4B");
        static readonly Color DoorPaint = B.Hex("47766E");
        static readonly Color Soil = B.Hex("4E3A2A");
        static readonly Color Fabric = B.Hex("B9473A");
        static readonly Color FabricLight = B.Hex("CF6A4E");
        static readonly Color Ribbon = B.Hex("E3D5B0");
        static readonly Color Bark = B.Hex("584536");
        static readonly Color LogEnd = B.Hex("C9AE86");
        static readonly Color Water = B.Hex("3B4F57");
        static readonly Color TinDark = B.Hex("666A6D");
        static readonly Color RidgeCap = B.Hex("485E70");
        static readonly Color Dirt = B.Hex("8C7A62");
        static readonly Color DirtDark = B.Hex("75654F");
        static readonly Color Toolbox = B.Hex("A8432F");

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

        /// <summary>The prefab, built if it does not exist yet - or dressed again in place when asked.</summary>
        public static GameObject EnsurePrefab(ShelterDefinition definition, bool rebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && !rebuild) return existing;

            ContentGenerator.EnsureFolder(PrefabFolder);

            // In place when it exists: the scene's huts override the root, the hut and the site -
            // where each stands, its id, which is built - and those overrides hold only while they
            // are the same objects.
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Shelter_Hut");

            try
            {
                Dress(root, definition, created: existing == null);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[Last Orders] {(existing != null ? "Rebuilt" : "Created")} the shelter prefab at {PrefabPath}.");
                return prefab;
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        static void Dress(GameObject root, ShelterDefinition definition, bool created)
        {
            Transform hut = EmptyGroup(root.transform, "Hut");
            Transform site = EmptyGroup(root.transform, "Site");

            Mesh hutMesh = BuildHut().Save(HutMeshPath, "ShelterHut");
            Mesh siteMesh = BuildSite().Save(SiteMeshPath, "ShelterSite");
            Mesh curtain = BuildCurtain().Save(CurtainMeshPath, "ShelterHut_Curtain");
            Mesh glow = BuildWindowGlow().Save(WindowGlowMeshPath, "ShelterHut_WindowGlow");

            Part("Model", hut, Vector3.zero, hutMesh, null, shadows: true);
            Part("Model", site, Vector3.zero, siteMesh, null, shadows: true);

            ShelterHut.Bed[] beds =
            {
                Window(hut, "Window_0", -WindowX, curtain, glow),
                Window(hut, "Window_1", WindowX, curtain, glow),
            };

            var tap = root.GetComponent<BoxCollider>();
            if (tap == null) tap = root.AddComponent<BoxCollider>();

            var shelter = root.GetComponent<ShelterHut>();
            if (shelter == null) shelter = root.AddComponent<ShelterHut>();

            if (created) shelter.Configure(null, definition, "shelter", false);
            shelter.ConfigureParts(hut.gameObject, site.gameObject, beds, tap);
        }

        /// <summary>The group named <paramref name="name"/> under <paramref name="parent"/>, emptied - or made.</summary>
        static Transform EmptyGroup(Transform parent, string name)
        {
            Transform group = parent.Find(name);
            if (group == null)
            {
                group = new GameObject(name).transform;
                group.SetParent(parent, false);
            }

            for (int i = group.childCount - 1; i >= 0; i--) Object.DestroyImmediate(group.GetChild(i).gameObject);
            return group;
        }

        static GameObject Part(string name, Transform parent, Vector3 at, Mesh mesh, Material[] materials, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            B.Show(go, mesh, materials);
            go.GetComponent<MeshRenderer>().shadowCastingMode =
                shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>One window's bed parts, at the window's glass: its light (under) and its curtain (over), both hidden.</summary>
        static ShelterHut.Bed Window(Transform hut, string name, float x, Mesh curtain, Mesh glow)
        {
            Transform window = new GameObject(name).transform;
            window.SetParent(hut, false);
            window.localPosition = new Vector3(x, WindowY, FrontFace);

            GameObject lit = Part("Lit", window, Vector3.zero, glow, new[] { B.GlowMaterial() }, shadows: false);
            lit.SetActive(false);

            GameObject drawn = Part("Curtain", window, Vector3.zero, curtain, null, shadows: false);
            drawn.SetActive(false);

            return new ShelterHut.Bed { occupied = drawn, lit = lit };
        }

        // --- The hut --------------------------------------------------------------------------------

        /// <summary>The roof's underside at <paramref name="x"/> across: <see cref="Eave"/> over the side walls, climbing to the ridge.</summary>
        static float RoofUnder(float x) => Eave + Slope * (Width * 0.5f - Mathf.Abs(x));

        static B.Model BuildHut()
        {
            var m = new B.Model { Shade = B.TimberShade(new Vector2(Width * 0.5f, Depth * 0.5f), Eave) };

            Footings(m);
            Walls(m);
            Roof(m);
            Door(m);
            for (int side = -1; side <= 1; side += 2) WindowDressing(m, side * WindowX, side);
            m.Lantern(new Vector3(0.615f, Sill + 1.36f, FrontFace), Vector3.forward);
            Woodpile(m);
            B.Barrel(m, new Vector3(1.25f, 0f, Depth * 0.5f + 0.37f), 0.2f, 0.55f, B.DoorWood, Water);

            return m;
        }

        /// <summary>A walnut sill on four stone footings, the corner posts standing on it.</summary>
        static void Footings(B.Model m)
        {
            m.Box(B.WalnutDark, new Vector3(0f, 0.07f, 0f), new Vector3(Width + 0.18f, 0.16f, Depth + 0.18f), 0.03f, 2);

            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -1f : 1f, z = (k & 2) == 0 ? -1f : 1f;
                m.Box(B.Stone * (1f + 0.05f * B.Jitter(k)), new Vector3(x * (Width * 0.5f + 0.03f), 0.05f, z * (Depth * 0.5f + 0.03f)),
                      new Vector3(0.3f, 0.14f, 0.3f), 0.045f, Quaternion.Euler(0f, 8f * B.Jitter(k + 9), 0f), 2);
                m.Box(B.Walnut, new Vector3(x * Width * 0.5f, (Sill + WallHeight) * 0.5f, z * Depth * 0.5f),
                      new Vector3(0.15f, WallHeight - Sill + 0.02f, 0.15f), 0.025f, 2);
            }
        }

        /// <summary>Lapped boards on all four sides, the doorway left open, the top plate over them, and the gables above.</summary>
        static void Walls(B.Model m)
        {
            float halfW = Width * 0.5f, halfD = Depth * 0.5f;
            const float inset = 0.07f;
            const int rows = 8;

            var doorway = new[] { new Vector3(-DoorWidth * 0.5f - 0.05f, DoorWidth * 0.5f + 0.05f, Sill + DoorHeight + 0.02f) };
            m.LapWall(new Vector3(0f, 0f, halfD), Vector3.right, Vector3.forward, -halfW + inset, halfW - inset, Sill, WallHeight, rows, 1, doorway);
            m.LapWall(new Vector3(0f, 0f, -halfD), Vector3.right, Vector3.back, -halfW + inset, halfW - inset, Sill, WallHeight, rows, 2);
            m.LapWall(new Vector3(-halfW, 0f, 0f), Vector3.forward, Vector3.left, -halfD + inset, halfD - inset, Sill, WallHeight, rows, 3);
            m.LapWall(new Vector3(halfW, 0f, 0f), Vector3.forward, Vector3.right, -halfD + inset, halfD - inset, Sill, WallHeight, rows, 4);

            // The top plate, all round.
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(B.Walnut, new Vector3(0f, WallHeight + 0.05f, side * (halfD + 0.035f)), new Vector3(Width + 0.16f, 0.1f, 0.1f), 0.02f, 2);
                m.Box(B.Walnut, new Vector3(side * (halfW + 0.035f), WallHeight + 0.05f, 0f), new Vector3(0.1f, 0.1f, Depth + 0.16f), 0.02f, 2);
            }

            // Gables of upright boards, front and back, reaching up under the roof.
            for (int side = -1; side <= 1; side += 2)
            {
                // Each board's square top is set by its lower corner, so no corner comes up through the tin.
                m.BoardWall(new Vector3(0f, 0f, side * halfD), Vector3.right, side > 0 ? Vector3.forward : Vector3.back, -halfW - 0.02f,
                            halfW + 0.02f, WallHeight + 0.1f, x => RoofUnder(Mathf.Abs(x) + 0.1f) + 0.015f, 0.2f, side > 0 ? 5 : 6);
            }

            // A diamond vent high in the front gable.
            var vent = Quaternion.Euler(0f, 0f, 45f);
            float ventY = (WallHeight + 0.1f + RoofUnder(0f)) * 0.5f + 0.06f;
            m.Box(B.WalnutDark, new Vector3(0f, ventY, halfD + 0.07f), new Vector3(0.24f, 0.24f, 0.04f), 0.02f, vent, 2);
            m.Box(B.Glass, new Vector3(0f, ventY, halfD + 0.093f), new Vector3(0.15f, 0.15f, 0.012f), 0.005f, vent, 1);
        }

        /// <summary>
        /// Two slopes of corrugated blue tin meeting at a ridge that runs front to back, three
        /// sheets a side, rust run down them from the fixings; a capping along the ridge, walnut
        /// barge boards along the gables, and a stovepipe through the back of one slope.
        ///
        /// Each slope is one corrugated surface with its sheets painted on, a seam between them: sheets
        /// modelled overlapping each other meet out of step, and the crests of the one below poke
        /// through the one on top.
        /// </summary>
        static void Roof(B.Model m)
        {
            const float thickness = 0.03f, pitch = 0.15f, depth = 0.035f;

            float cos = 1f / Mathf.Sqrt(1f + Slope * Slope), sin = Slope * cos;
            float run = Width * 0.5f + Overhang;
            float length = run / cos;
            float span = Depth + GableOverhang * 2f;
            float lift = thickness / cos;

            for (int side = -1; side <= 1; side += 2)
            {
                var down = new Vector3(side * cos, -sin, 0f);
                // The sheets' ridges run down the slope; across is chosen so the top faces up.
                Vector3 across = side > 0 ? Vector3.back : Vector3.forward;
                float startZ = side > 0 ? span * 0.5f : -span * 0.5f;
                const int sheets = 3;
                float sheet = span / sheets;
                int first = side > 0 ? 0 : 1;

                var corner = new Vector3(side * 0.02f, RoofUnder(0.02f) + lift, startZ);
                m.Corrugated(corner, across, down, span, length, pitch, depth, thickness, (u, v) =>
                {
                    int k = Mathf.Min(sheets - 1, Mathf.FloorToInt(u / sheet));
                    Color c = Weathered(B.RoofTin[(k + first) % B.RoofTin.Length], u, v / length, k + side * 10, 0.45f);
                    // The lap where one sheet lies on the next, a line of shadow.
                    float seam = Mathf.Abs(u - Mathf.Round(u / sheet) * sheet);
                    return u > 0.1f && u < span - 0.1f && seam < 0.035f ? c * 0.78f : c;
                });


                // The barge boards, front and back, under the sheets' edges.
                float angle = Mathf.Atan(Slope) * Mathf.Rad2Deg * -side;
                Vector3 mid = new Vector3(side * run * 0.5f, RoofUnder(run * 0.5f) - 0.06f, 0f);
                foreach (float z in new[] { span * 0.5f - 0.04f, -span * 0.5f + 0.04f })
                {
                    m.Box(B.Walnut, mid + Vector3.forward * z, new Vector3(length + 0.04f, 0.13f, 0.05f), 0.015f,
                          Quaternion.Euler(0f, 0f, angle), 2);
                }
            }

            float ridgeTop = RoofUnder(0f) + lift + depth;
            m.Tube(RidgeCap, new Vector3(0f, ridgeTop - 0.02f, -span * 0.5f - 0.02f), new Vector3(0f, ridgeTop - 0.02f, span * 0.5f + 0.02f),
                   0.065f, 14);

            // The stovepipe, its collar where it comes through the tin, and its rain cap.
            var pipe = new Vector3(0.85f, 0f, -0.6f);
            float roofThere = RoofUnder(pipe.x) + lift;
            m.Tube(TinDark, pipe + Vector3.up * (roofThere - 0.15f), pipe + Vector3.up * 3.3f, 0.075f, 12);
            m.Tube(TinDark * 0.85f, pipe + Vector3.up * (roofThere - 0.02f), pipe + Vector3.up * (roofThere + 0.1f), 0.11f, 12);
            m.Lathe((p, n) => TinDark * 0.9f, pipe + Vector3.up * 3.33f, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.15f, 0f), new Vector2(0.15f, 0f), new Vector2(0.02f, 0.11f), new Vector2(0f, 0.115f),
            }, 14);
            m.Tube(TinDark, pipe + Vector3.up * 3.28f, pipe + Vector3.up * 3.34f, 0.012f, 6);
        }

        /// <summary>
        /// Tin weathered as a painter would show it: a shade lighter toward the ridge, and rust run
        /// down in streaks from the fixings, heavier toward the eave. <paramref name="v"/> is 0 at the
        /// top to 1 at the eave.
        /// </summary>
        static Color Weathered(Color tin, float u, float v, int key, float rust)
        {
            float phase = B.Jitter(key) * 10f;
            float streak = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 7.3f + phase)), 10f) +
                           0.6f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 13.1f + phase * 2f)), 14f);
            float amount = Mathf.Clamp01(streak) * Mathf.Clamp01((v - 0.3f) / 0.7f) * rust;
            Color c = tin * Mathf.Lerp(1.05f, 0.95f, v) * (1f + 0.03f * Mathf.Sin(u * 2.1f + phase));
            return Color.Lerp(c, B.Rust, amount * 0.55f);
        }

        /// <summary>A plank door with ledges and a brace in its frame, a canopy of tin over it on two brackets, and a stone step.</summary>
        static void Door(B.Model m)
        {
            float z = Depth * 0.5f;
            float top = Sill + DoorHeight;

            for (int side = -1; side <= 1; side += 2)
                m.Box(B.Walnut, new Vector3(side * (DoorWidth * 0.5f + 0.035f), Sill + DoorHeight * 0.5f, z + 0.04f),
                      new Vector3(0.07f, DoorHeight + 0.02f, 0.07f), 0.015f, 2);
            m.Box(B.Walnut, new Vector3(0f, top + 0.04f, z + 0.045f), new Vector3(DoorWidth + 0.2f, 0.08f, 0.08f), 0.018f, 2);

            const int planks = 4;
            float plank = DoorWidth / planks;
            for (int i = 0; i < planks; i++)
            {
                float x = -DoorWidth * 0.5f + plank * (i + 0.5f);
                m.Box(DoorPaint * (1f + 0.05f * B.Jitter(40 + i)), new Vector3(x, Sill + DoorHeight * 0.5f, z + 0.005f),
                      new Vector3(plank - 0.012f, DoorHeight - 0.02f, 0.045f), 0.01f);
            }

            Color ledge = DoorPaint * 0.86f;
            float low = Sill + 0.3f, high = top - 0.3f;
            m.Box(ledge, new Vector3(0f, low, z + 0.035f), new Vector3(DoorWidth - 0.06f, 0.09f, 0.03f), 0.01f);
            m.Box(ledge, new Vector3(0f, high, z + 0.035f), new Vector3(DoorWidth - 0.06f, 0.09f, 0.03f), 0.01f);

            float rise = high - low, across = DoorWidth - 0.2f;
            m.Box(ledge, new Vector3(0f, (low + high) * 0.5f, z + 0.035f), new Vector3(Mathf.Sqrt(rise * rise + across * across), 0.08f, 0.028f),
                  0.01f, Quaternion.Euler(0f, 0f, Mathf.Atan2(rise, across) * Mathf.Rad2Deg));

            foreach (float y in new[] { low, high })
                m.Box(B.Iron, new Vector3(-DoorWidth * 0.5f + 0.17f, y, z + 0.054f), new Vector3(0.28f, 0.035f, 0.012f), 0.006f);
            m.Box(B.Iron, new Vector3(DoorWidth * 0.5f - 0.13f, Sill + 0.85f, z + 0.06f), new Vector3(0.05f, 0.05f, 0.03f), 0.012f, 2);

            // The canopy: a short sheet sloping out over the door, on a bracket either side.
            const float drop = 0.2f, reach = 0.55f;
            float canopyY = top + 0.36f;
            var down = new Vector3(0f, -Mathf.Sin(drop), Mathf.Cos(drop));
            Vector3 up = Vector3.Cross(down, Vector3.right);
            const float sheetThickness = 0.025f;
            var eave = new Vector3(-0.72f, canopyY, z + 0.06f);
            m.Corrugated(eave, Vector3.right, down, 1.44f, reach, 0.12f, 0.025f, sheetThickness,
                         (u, v) => Weathered(B.RoofTin[2], u, v / reach, 91, 0.35f));

            // Under it, either side: an arm along its underside out from the wall, and a strut up to it.
            Quaternion along = Quaternion.LookRotation(down, up);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 root = eave + Vector3.right * (0.72f + side * 0.6f) - up * (sheetThickness + 0.026f);
                m.Box(B.Walnut, root + down * 0.22f, new Vector3(0.05f, 0.05f, 0.44f), 0.012f, along);

                var foot = new Vector3(side * 0.6f, top + 0.08f, z + 0.06f);
                Vector3 head = root + down * 0.38f - up * 0.02f;
                Vector3 d = head - foot;
                m.Box(B.Walnut, (foot + head) * 0.5f, new Vector3(0.05f, d.magnitude, 0.05f), 0.012f,
                      Quaternion.FromToRotation(Vector3.up, d.normalized));
            }

            m.Box(B.Stone, new Vector3(0f, 0.05f, z + 0.32f), new Vector3(1.05f, 0.12f, 0.42f), 0.035f, 2);
            m.Box(B.Stone * 1.06f, new Vector3(0.08f, 0.015f, z + 0.86f), new Vector3(0.5f, 0.05f, 0.38f), 0.02f,
                  Quaternion.Euler(0f, 12f, 0f), 2);
        }

        /// <summary>A window's frame, glass, sill and flower box. The bed parts - light and curtain - are separate objects.</summary>
        static void WindowDressing(B.Model m, float x, int side)
        {
            var c = new Vector3(x, WindowY, FrontFace);
            float hx = WindowSize.x * 0.5f, hy = WindowSize.y * 0.5f;

            // The glass, a sky-coloured gleam slanting across it.
            m.Surface((p, n) =>
            {
                float gleam = Mathf.Exp(-Mathf.Pow((p.x - c.x) * 0.9f + (p.y - c.y) - 0.04f, 2f) / 0.004f);
                return Color.Lerp(B.Glass, B.GlassSky, 0.25f + 0.55f * gleam + 0.2f * (p.y - c.y + hy) / (hy * 2f));
            }, 8, 8, (u, v) => c + new Vector3(hx - u * hx * 2f, -hy + v * hy * 2f, GlassZ));

            const float bar = 0.07f;
            m.Box(B.Walnut, c + new Vector3(0f, hy + bar * 0.5f, 0.025f), new Vector3(WindowSize.x + bar * 2f, bar, 0.06f), 0.016f, 2);
            m.Box(B.Walnut, c + new Vector3(0f, -hy - bar * 0.5f, 0.025f), new Vector3(WindowSize.x + bar * 2f, bar, 0.06f), 0.016f, 2);
            m.Box(B.Walnut, c + new Vector3(-hx - bar * 0.5f, 0f, 0.025f), new Vector3(bar, WindowSize.y, 0.06f), 0.016f, 2);
            m.Box(B.Walnut, c + new Vector3(hx + bar * 0.5f, 0f, 0.025f), new Vector3(bar, WindowSize.y, 0.06f), 0.016f, 2);
            m.Box(B.HoneyLight, c + new Vector3(0f, -hy - bar - 0.02f, 0.06f), new Vector3(WindowSize.x + 0.2f, 0.045f, 0.13f), 0.014f, 2);

            // The flower box hung under the sill: terracotta, a heap of leaves, flowers of one colour.
            float boxY = c.y - hy - bar - 0.14f;
            var box = new Vector3(x, boxY, FrontFace + 0.1f);
            m.Box(FlowerBox, box, new Vector3(0.6f, 0.15f, 0.16f), 0.025f, 2);
            m.Box(Soil, box + Vector3.up * 0.07f, new Vector3(0.54f, 0.03f, 0.12f), 0.012f);

            Color flower = B.Flowers[(side + 1) / 2 % B.Flowers.Length];
            for (int k = 0; k < 5; k++)
            {
                float fx = -0.22f + k * 0.11f;
                float r = 0.055f + 0.012f * B.Jitter(300 + k + side * 10);
                Blob(m, (p, n) => Color.Lerp(B.Leaf, B.LeafLight, Mathf.Clamp01(n.y * 0.6f + 0.3f)),
                     box + new Vector3(fx, 0.1f + 0.01f * B.Jitter(k), 0.01f * B.Jitter(k + 3)), new Vector3(r, r * 0.8f, r * 0.85f));
            }

            for (int k = 0; k < 4; k++)
            {
                float fx = -0.17f + k * 0.115f + 0.02f * B.Jitter(k + side * 5);
                Blob(m, (p, n) => flower, box + new Vector3(fx, 0.16f + 0.015f * B.Jitter(k + 20), 0.03f), Vector3.one * 0.026f);
            }
        }

        static void Blob(B.Model m, System.Func<Vector3, Vector3, Color> paint, Vector3 centre, Vector3 radii)
        {
            m.Surface(paint, 12, 8, (u, v) =>
            {
                float th = v * Mathf.PI, ph = u * Mathf.PI * 2f;
                return centre + Vector3.Scale(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), -Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)), radii);
            }, true);
        }

        /// <summary>Split logs stacked three, two, one against the front wall, left of the door.</summary>
        static void Woodpile(B.Model m)
        {
            float z = Depth * 0.5f + 0.17f;
            const float r = 0.064f;
            int key = 0;

            void Log(float y, float dz)
            {
                key++;
                float x0 = -1.42f + 0.03f * B.Jitter(key), x1 = -0.9f + 0.03f * B.Jitter(key + 50);
                float radius = r * (1f + 0.1f * B.Jitter(key + 90));
                m.Tube((p, n) => Mathf.Abs(n.x) > 0.8f ? LogEnd : Bark * (1f + 0.06f * B.Jitter(key)),
                       new Vector3(x0, y, z + dz), new Vector3(x1, y, z + dz), radius, radius, 9);
            }

            for (int i = 0; i < 3; i++) Log(r, i * 0.13f);
            for (int i = 0; i < 2; i++) Log(r * 2.75f, 0.065f + i * 0.13f);
            Log(r * 4.5f, 0.13f);
        }

        // --- The bed parts --------------------------------------------------------------------------

        /// <summary>
        /// A curtain gathered across the left of the window and tied back half-way down, in folds,
        /// with a rod across the top. Built round the window's middle, on its glass.
        /// </summary>
        static B.Model BuildCurtain()
        {
            var m = new B.Model();
            float hx = WindowSize.x * 0.5f, hy = WindowSize.y * 0.5f;
            const float folds = 4f;

            m.Surface((p, n) =>
            {
                float tie = Mathf.Exp(-Mathf.Pow(p.y + 0.02f, 2f) / 0.0006f);
                if (tie > 0.5f) return Ribbon;
                return Color.Lerp(Fabric, FabricLight, Mathf.Clamp01(n.z * 0.5f + 0.5f * Mathf.Abs(n.x)) * 0.5f);
            }, 28, 14, (u, v) =>
            {
                float y = hy - 0.01f - v * (hy * 2f + 0.02f);
                // Pulled toward the side at the tie, fuller below it.
                float tie = Mathf.Exp(-Mathf.Pow(y + 0.02f, 2f) / 0.004f);
                float width = WindowSize.x * 0.58f * (1f - 0.45f * tie) * (v > 0.5f ? 1.05f : 1f);
                float x = -hx + 0.005f + u * width;
                float z = CurtainZ + 0.004f * Mathf.Sin(u * folds * Mathf.PI * 2f) * (1f - 0.6f * tie);
                return new Vector3(x, y, z);
            });

            m.Tube(B.Iron, new Vector3(-hx - 0.02f, hy - 0.012f, RodZ), new Vector3(hx + 0.02f, hy - 0.012f, RodZ), 0.008f, 6);
            return m;
        }

        /// <summary>The warm light in a window, just in front of its glass, under its curtain.</summary>
        static B.Model BuildWindowGlow()
        {
            var m = new B.Model();
            m.Box(Color.white, new Vector3(0f, 0f, GlowZ), new Vector3(WindowSize.x, WindowSize.y, 0.004f), 0.0015f);
            return m;
        }

        // --- The empty site -------------------------------------------------------------------------

        /// <summary>
        /// A pad of trodden dirt pegged out at the corners with twine, footing stones where the
        /// corners will stand, a stack of timber, two roof sheets and a toolbox: somewhere a hut is
        /// meant to go.
        /// </summary>
        static B.Model BuildSite()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(p.y / 0.3f)) };
            const float hx = 1.75f, hz = 1.55f;

            m.Heightfield((p, n) =>
            {
                float mottle = 0.5f + 0.25f * Mathf.Sin(p.x * 4.1f + p.z * 2.3f) * Mathf.Sin(p.z * 3.7f - p.x * 1.3f) +
                               0.25f * Mathf.Sin(p.x * 9.3f - p.z * 7.1f);
                return Color.Lerp(DirtDark, Dirt, mottle) * Mathf.Lerp(0.92f, 1.04f, n.y);
            }, new Rect(-hx, -hz, hx * 2f, hz * 2f), 32, (x, z) =>
            {
                float edge = Mathf.Min(hx - Mathf.Abs(x), hz - Mathf.Abs(z));
                float rise = Mathf.Clamp01(edge / 0.35f);
                rise = rise * rise * (3f - 2f * rise);
                return -0.006f + 0.034f * rise + 0.004f * Mathf.Sin(x * 6.3f + 1f) * Mathf.Sin(z * 5.1f) * rise;
            });

            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -1f : 1f, z = (k & 2) == 0 ? -1f : 1f;
                m.Box(B.Stone * (1f + 0.05f * B.Jitter(k)), new Vector3(x * (Width * 0.5f + 0.03f), 0.06f, z * (Depth * 0.5f + 0.03f)),
                      new Vector3(0.3f, 0.14f, 0.3f), 0.045f, Quaternion.Euler(0f, 8f * B.Jitter(k + 9), 0f), 2);
            }

            // Pegged out, the twine strung between the stakes.
            var stakes = new[]
            {
                new Vector3(-1.6f, 0f, -1.42f), new Vector3(1.6f, 0f, -1.42f), new Vector3(1.6f, 0f, 1.42f), new Vector3(-1.6f, 0f, 1.42f),
            };
            for (int k = 0; k < 4; k++)
            {
                m.Box(B.HoneyLight, stakes[k] + Vector3.up * 0.28f, new Vector3(0.06f, 0.58f, 0.06f), 0.012f);
                Vector3 a = stakes[k] + Vector3.up * 0.46f, b = stakes[(k + 1) % 4] + Vector3.up * 0.46f;
                m.Tube(B.Twine, a, b, 0.007f, 5);
            }

            // Timber, stacked crosswise on two bearers.
            m.Transform = Matrix4x4.TRS(new Vector3(-0.35f, 0.03f, -0.3f), Quaternion.Euler(0f, 8f, 0f), Vector3.one);
            for (int k = -1; k <= 1; k += 2) m.Box(B.WalnutDark, new Vector3(k * 0.7f, 0.04f, 0f), new Vector3(0.1f, 0.08f, 0.9f), 0.015f);
            for (int i = 0; i < 4; i++)
                m.Box(B.Plank(500 + i), new Vector3(0.03f * B.Jitter(i), 0.11f, -0.33f + i * 0.22f), new Vector3(2f, 0.06f, 0.2f), 0.012f);
            for (int i = 0; i < 3; i++)
                m.Box(B.Plank(520 + i), new Vector3(0.05f * B.Jitter(i + 7), 0.17f, -0.22f + i * 0.22f), new Vector3(1.9f, 0.06f, 0.2f), 0.012f,
                      Quaternion.Euler(0f, 3f * B.Jitter(i + 30), 0f));

            // Two roof sheets on blocks.
            m.Transform = Matrix4x4.TRS(new Vector3(0.85f, 0.03f, 0.75f), Quaternion.Euler(0f, -24f, 0f), Vector3.one);
            for (int k = -1; k <= 1; k += 2) m.Box(B.WalnutDark, new Vector3(0f, 0.04f, k * 0.3f), new Vector3(1f, 0.08f, 0.1f), 0.015f);
            for (int s = 0; s < 2; s++)
            {
                Color colour = B.RoofTin[s];
                m.Corrugated(new Vector3(-0.55f + s * 0.04f, 0.11f + s * 0.06f, -0.42f), Vector3.right, Vector3.forward, 1.1f, 0.84f, 0.15f,
                             0.035f, 0.02f, (u, v) => Weathered(colour, u, v / 0.84f, 200 + s, 0.5f));
            }

            // A toolbox with its handle up.
            m.Transform = Matrix4x4.TRS(new Vector3(1.05f, 0f, -0.85f), Quaternion.Euler(0f, 20f, 0f), Vector3.one);
            m.Box(Toolbox, new Vector3(0f, 0.09f, 0f), new Vector3(0.42f, 0.18f, 0.2f), 0.025f, 2);
            m.Box(Toolbox * 0.8f, new Vector3(0f, 0.185f, 0f), new Vector3(0.44f, 0.02f, 0.21f), 0.008f);
            m.Tube(B.Iron, new Vector3(-0.1f, 0.25f, 0f), new Vector3(0.1f, 0.25f, 0f), 0.012f, 6);
            m.Tube(B.Iron, new Vector3(-0.1f, 0.19f, 0f), new Vector3(-0.1f, 0.25f, 0f), 0.01f, 6);
            m.Tube(B.Iron, new Vector3(0.1f, 0.19f, 0f), new Vector3(0.1f, 0.25f, 0f), 0.01f, 6);
            m.Transform = Matrix4x4.identity;

            return m;
        }
    }
}
