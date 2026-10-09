using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FoodStreet.Placement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The plant pot's raised bed, modelled in code in the game's smooth low-poly style, in pieces
    /// that join: pots placed side by side grow into one bed.
    ///
    /// The bed is two chunky timber boards stacked on each side, rounded on every edge, with a
    /// sturdy post at each corner, round dark tilled soil heaped into three soft rows. Its colour
    /// is painted into the mesh - a worn light edge along the tops of the boards, darker where they
    /// meet the ground and the soil, light crowns and dark furrows in the rows - and lit by the
    /// soft-gradient LastOrders/Stylized shader, so it shades like the hand-painted characters
    /// rather than in toon bands.
    ///
    /// The bed is built as four quarters round the cell's centre. Each quarter comes in five shapes
    /// (a closed corner, a border down one side or the other, an inside corner, or open soil), and
    /// a <see cref="GridAutoTile"/> on the pot picks a shape for each quarter from which of its
    /// eight neighbours are pots too, so the borders between joined pots drop away. Where a straight
    /// border runs on from one pot into the next, a smaller post marks the seam.
    ///
    /// Everything shared across a seam - the boards' cross-section, the soil's rows and the colour
    /// patterns - is a function of where it is in the cell, repeating every metre, so the pieces
    /// meet without a step or a change of colour.
    /// </summary>
    public static class SoilBed
    {
        const string Folder = "Assets/Game Assets/Content/LastOrders/Farming";
        const string MeshPath = Folder + "/SoilBed.asset";
        const string TilesPath = Folder + "/SoilBedTiles.asset";
        const string MaterialName = "Farm_SoilBed";

        const string VisualsPrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Farming/PlantPot/PlantPotVisuals.prefab";
        const string PotPrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Farming/PlantPot/pfPlantPot.prefab";

        const string OldSoilName = "SM_Env_Dirt_Skirt_01";
        const string OldPlanterName = "SM_Prop_Pot_02";
        const string BedName = "SoilBed";

        /// <summary>The growing crop's sprite (CropSprites), under the pot's crop preview.</summary>
        internal const string PlantPath = "Plants/Crop";

        /// <summary>The middle of the pot's 1 x 1 m cell, in its own space.</summary>
        static readonly Vector3 Centre = new Vector3(0.5f, 0f, 0.5f);

        // --- Measurements (metres, from the cell's centre) ---------------------------------------

        const float Half = 0.5f;

        /// <summary>From the cell's edge to a board's outer face: the gap between two pots that do not join.</summary>
        const float Gap = 0.035f;

        const float BoardThickness = 0.075f;

        /// <summary>From the cell's centre to the middle of a board.</summary>
        const float Inset = Half - Gap - BoardThickness * 0.5f;

        /// <summary>How far the boards and posts reach below the pot's origin, to meet the ground.</summary>
        const float Footing = -0.06f;

        /// <summary>Where the lower board ends and the upper one starts.</summary>
        const float BoardSplit = 0.09f;

        const float BoardTop = 0.175f;
        const float BoardRound = 0.016f;

        const float PostWidth = 0.115f;
        const float PostTop = 0.215f;
        const float JointWidth = 0.09f;
        const float JointTop = 0.2f;

        /// <summary>How high the soil's crown stands: where a crop's base goes.</summary>
        internal const float Crown = 0.12f;

        /// <summary>Soil samples from the centre to a border: about 4 cm apart.</summary>
        const int SoilSteps = 10;

        /// <summary>Three furrow ridges a metre, one through the middle where the crop stands.</summary>
        const float RowsPerMetre = 3f;

        // --- Palette (painted a little darker than it shows: the level's light lifts it) ---------

        static readonly Color WoodLower = Hex("74533A");
        static readonly Color WoodUpper = Hex("83603F");
        static readonly Color WoodTop = Hex("A58058");
        static readonly Color WoodWorn = Hex("AE9070");
        static readonly Color PostSide = Hex("553C2A");
        static readonly Color PostCap = Hex("7A5E44");
        static readonly Color SoilCrest = Hex("5E4433");
        static readonly Color SoilFurrow = Hex("3A2A1F");
        static readonly Color StoneSide = Hex("5E5850");
        static readonly Color StoneTop = Hex("7D766B");
        static readonly Color ClodSide = Hex("4A3527");
        static readonly Color ClodTop = Hex("6A4D39");

        static readonly string[] QuarterNames = { "NE", "SE", "SW", "NW" };

        [MenuItem("Last Orders/Farming/Build Soil Beds (Plant Pots)", priority = 90)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This rebuilds the plant pots' raised beds and puts them into:\n\n" +
                "- SoilBedTiles (the pieces pots join from) and SoilBed (a lone bed)\n" +
                "- PlantPotVisuals (every pot, and the ghost shown while placing one)\n" +
                "- pfPlantPot (joins pots placed side by side into one bed)\n" +
                "- the open scene: pots are squared onto their grid cells so neighbours join\n\n" +
                "The scene is left unsaved.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Builds the bed and puts it into the prefabs and the open scene, with no dialogs.</summary>
        public static string Apply()
        {
            Material material = BedMaterial();

            var pieces = new Mesh[AutoTileSet.PieceCount];
            for (int quarter = 0; quarter < TileMask.Quarters; quarter++)
            {
                foreach (QuarterShape shape in Enum.GetValues(typeof(QuarterShape)))
                    pieces[AutoTileSet.PieceIndex(quarter, shape)] = Piece(quarter, shape);
            }

            Mesh joint = JointPost();
            AutoTileSet set = SaveTiles(pieces, joint);
            Mesh lone = SaveLoneBed(set);

            int vertices = 0;
            for (int mask = 0; mask < 256; mask++) vertices = Mathf.Max(vertices, CountVertices(set, mask));

            var report = new StringBuilder("[Last Orders] Soil beds built.\n");
            report.Append($"  Pieces: {pieces.Length} quarters + a joint post; a lone bed is {lone.vertexCount} vertices, ");
            report.Append($"the busiest neighbourhood {vertices}; crown {Crown:0.00} m up.\n");
            report.Append($"  {DressVisuals(lone, material)}\n");
            report.Append($"  {DressPot(set)}\n");
            report.Append($"  {SquareScenePots()}\n");
            return report.ToString();
        }

        static int CountVertices(AutoTileSet set, int mask)
        {
            Mesh mesh = set.Combine(mask, "count");
            int count = mesh.vertexCount;
            Object.DestroyImmediate(mesh);
            return count;
        }

        // --- The pieces ------------------------------------------------------------------------------

        /// <summary>One quarter of a bed, in one of its five shapes, round the cell's centre.</summary>
        internal static Mesh Piece(int quarter, QuarterShape shape)
        {
            Vector2Int sign = TileMask.QuarterSigns[quarter];
            float sx = sign.x, sz = sign.y;
            var bed = new PaintedMesh();

            Soil(bed, sx, sz, shape);
            Stones(bed, quarter, sx, sz, shape);

            // Boards run from the middle of the cell out to the corner post, or on to the cell's edge
            // where they carry on into the next pot. Inside is always toward the soil.
            var north = new Vector3(0f, 0f, sz);
            var east = new Vector3(sx, 0f, 0f);

            switch (shape)
            {
                case QuarterShape.Corner:
                    Boards(bed, new Vector3(0f, 0f, sz * Inset), new Vector3(sx * Inset, 0f, sz * Inset), -north);
                    Boards(bed, new Vector3(sx * Inset, 0f, 0f), new Vector3(sx * Inset, 0f, sz * Inset), -east);
                    Post(bed, new Vector3(sx * Inset, 0f, sz * Inset), PostWidth, PostTop);
                    break;

                case QuarterShape.BorderZ:
                    Boards(bed, new Vector3(0f, 0f, sz * Inset), new Vector3(sx * Half, 0f, sz * Inset), -north);
                    break;

                case QuarterShape.BorderX:
                    Boards(bed, new Vector3(sx * Inset, 0f, 0f), new Vector3(sx * Inset, 0f, sz * Half), -east);
                    break;

                case QuarterShape.InsideCorner:
                    // The two borders that meet round the empty corner cell, joined by a post.
                    Boards(bed, new Vector3(sx * Half, 0f, sz * Inset), new Vector3(sx * Inset, 0f, sz * Inset), -north);
                    Boards(bed, new Vector3(sx * Inset, 0f, sz * Inset), new Vector3(sx * Inset, 0f, sz * Half), -east);
                    Post(bed, new Vector3(sx * Inset, 0f, sz * Inset), PostWidth, PostTop);
                    break;
            }

            return bed.Finish($"SoilBed {QuarterNames[quarter]} {shape}");
        }

        /// <summary>The post that marks a seam where a straight border runs on into the next pot.</summary>
        static Mesh JointPost()
        {
            var post = new PaintedMesh();
            Post(post, Vector3.zero, JointWidth, JointTop);
            return post.Finish("SoilBed Joint");
        }

        // --- Soil ------------------------------------------------------------------------------------

        /// <summary>
        /// How far a point of this quarter is inside the bed, from the middle of the nearest board
        /// (positive inside). Taken on the quarter's own unsigned coordinates.
        /// </summary>
        static Func<float, float, float> BorderDistance(QuarterShape shape)
        {
            switch (shape)
            {
                case QuarterShape.Corner: return (ax, az) => Mathf.Min(Inset - ax, Inset - az);
                case QuarterShape.BorderZ: return (ax, az) => Inset - az;
                case QuarterShape.BorderX: return (ax, az) => Inset - ax;
                case QuarterShape.InsideCorner:
                    return (ax, az) =>
                    {
                        // Round the empty corner cell's square: past it on one side, or round its corner.
                        if (ax >= Inset) return Inset - az;
                        if (az >= Inset) return Inset - ax;
                        float dx = Inset - ax, dz = Inset - az;
                        return Mathf.Sqrt(dx * dx + dz * dz);
                    };
                default: return (ax, az) => 1f;
            }
        }

        /// <summary>0 at a board's inner face, 1 a hand's width in from it.</summary>
        static float AwayFromBoards(float distance) =>
            Smooth(Mathf.InverseLerp(BoardThickness * 0.5f, BoardThickness * 0.5f + 0.1f, distance));

        /// <summary>0 in a furrow, 1 on a row's crown. Rows run east-west, one through the cell's middle.</summary>
        static float Row(float z)
        {
            float r = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * RowsPerMetre * z);
            return Smooth(r);
        }

        /// <summary>A few centimetres of lumpiness, repeating every metre so cells meet.</summary>
        static float Lumps(float x, float z)
        {
            const float Tau = Mathf.PI * 2f;
            return 0.004f * Mathf.Sin(Tau * (3f * x + 2f * z) + 1.3f)
                 + 0.003f * Mathf.Sin(Tau * (5f * x - 4f * z) + 0.4f)
                 + 0.0025f * Mathf.Sin(Tau * (-2f * x + 5f * z) + 2.1f)
                 + 0.0015f * Mathf.Sin(Tau * (7f * x + 6f * z) + 4.2f);
        }

        static float SoilHeight(float x, float z, float distance)
        {
            float away = AwayFromBoards(distance);
            // Rows soften toward the boards, and the soil settles a little lower against them.
            return 0.09f + 0.026f * Row(z) * (0.35f + 0.65f * away) + Lumps(x, z) - 0.012f * (1f - away);
        }

        static void Soil(PaintedMesh bed, float sx, float sz, QuarterShape shape)
        {
            Func<float, float, float> distance = BorderDistance(shape);
            bool openX = shape == QuarterShape.BorderZ || shape == QuarterShape.Open || shape == QuarterShape.InsideCorner;
            bool openZ = shape == QuarterShape.BorderX || shape == QuarterShape.Open || shape == QuarterShape.InsideCorner;

            Color Paint(Vector3 p, Vector3 n)
            {
                float away = AwayFromBoards(distance(Mathf.Abs(p.x), Mathf.Abs(p.z)));
                Color c = Color.Lerp(SoilFurrow, SoilCrest, Row(p.z) * 0.85f + 0.15f * Mathf.Clamp01(n.y));
                float mottle = 1f + 0.07f * Mathf.Sin(2f * Mathf.PI * (5f * p.x + 3f * p.z) + 0.3f)
                                           * Mathf.Sin(2f * Mathf.PI * (-3f * p.x + 4f * p.z) + 1.1f);
                return c * (mottle * Mathf.Lerp(0.68f, 1f, away));
            }

            if (shape == QuarterShape.InsideCorner)
            {
                // Everything but the empty corner cell's square, in two rectangles that share an edge.
                SoilPatch(bed, sx, sz, Samples(false), Samples(true), distance, Paint);
                SoilPatch(bed, sx, sz, new[] { Inset, Half }, Samples(false), distance, Paint);
                return;
            }

            SoilPatch(bed, sx, sz, Samples(openX), Samples(openZ), distance, Paint);
        }

        /// <summary>
        /// Where the soil is sampled along one axis of a quarter: evenly from the centre to the board,
        /// then one more step to the cell's edge when that side joins the next pot. Both cells of a
        /// seam sample it at the same places, so their soil meets vertex for vertex.
        /// </summary>
        static float[] Samples(bool open)
        {
            var samples = new List<float>();
            for (int i = 0; i <= SoilSteps; i++) samples.Add(Inset * i / SoilSteps);
            if (open) samples.Add(Half);
            return samples.ToArray();
        }

        static void SoilPatch(PaintedMesh bed, float sx, float sz, float[] xs, float[] zs,
                              Func<float, float, float> distance, Func<Vector3, Vector3, Color> paint)
        {
            Vector3 Point(float ax, float az)
            {
                float x = sx * ax, z = sz * az;
                return new Vector3(x, SoilHeight(x, z, distance(ax, az)), z);
            }

            var shape = new SmoothMesh(1);

            // Facing up: the surface's front is cross(along v, along u), which flips with the quarter.
            if (sx * sz > 0f)
                shape.Surface(0, xs.Length - 1, zs.Length - 1, (u, v) => Point(Along(xs, u), Along(zs, v)));
            else
                shape.Surface(0, zs.Length - 1, xs.Length - 1, (u, v) => Point(Along(xs, v), Along(zs, u)));

            bed.Add(shape, paint);
        }

        /// <summary>The value at <paramref name="t"/> (0-1) along samples spaced as given, one step per interval.</summary>
        static float Along(float[] samples, float t)
        {
            float f = Mathf.Clamp01(t) * (samples.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), samples.Length - 2);
            return Mathf.Lerp(samples[i], samples[i + 1], f - i);
        }

        /// <summary>
        /// A clod or a pebble or two, the same places in every shape of a quarter, left out where a
        /// board now runs: enough to break up the soil, few enough not to read as litter.
        /// </summary>
        static void Stones(PaintedMesh bed, int quarter, float sx, float sz, QuarterShape shape)
        {
            Func<float, float, float> distance = BorderDistance(shape);
            var rng = new System.Random(4242 + quarter * 97);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            for (int i = 0; i < 2; i++)
            {
                float ax = R(0.07f, 0.4f), az = R(0.07f, 0.4f);
                float size = R(0.03f, 0.045f);
                bool pebble = (quarter + i) % 3 == 0;
                bool skip = rng.NextDouble() < 0.35;
                var turn = Quaternion.Euler(R(-12f, 12f), R(0f, 180f), R(-12f, 12f));

                // Clear of the crop in the middle and of any board.
                if (skip || ax * ax + az * az < 0.16f * 0.16f) continue;
                if (distance(ax, az) < BoardThickness * 0.5f + size + 0.03f) continue;

                float x = sx * ax, z = sz * az;
                var at = new Vector3(x, SoilHeight(x, z, distance(ax, az)) + size * 0.08f, z);
                var lump = new SmoothMesh(1);
                lump.RoundedBox(0, at, new Vector3(size, size * 0.55f, size * 0.8f), size * 0.25f, turn, 1);

                Color side = pebble ? StoneSide : ClodSide, top = pebble ? StoneTop : ClodTop;
                bed.Add(lump, (p, n) => Color.Lerp(side, top, Mathf.Clamp01(n.y)));
            }
        }

        // --- Boards and posts ----------------------------------------------------------------------

        /// <summary>
        /// The two stacked boards of a border, from <paramref name="from"/> to <paramref name="to"/>
        /// (heights ignored), with <paramref name="inside"/> pointing at the soil. Their ends are open:
        /// each either carries on into the next piece or stops inside a post.
        /// </summary>
        static void Boards(PaintedMesh bed, Vector3 from, Vector3 to, Vector3 inside)
        {
            // Wound so the surface faces out: cross(up, run) has to point away from the soil.
            if (Vector3.Dot(Vector3.Cross(Vector3.up, to - from), inside) > 0f)
                (from, to) = (to, from);

            Vector3 run = to - from;
            int steps = Mathf.Max(1, Mathf.CeilToInt(run.magnitude / 0.125f));
            bool alongX = Mathf.Abs(run.x) > Mathf.Abs(run.z);

            foreach (List<Vector2> profile in new[] { Profile(Footing, BoardSplit, false), Profile(BoardSplit, BoardTop, true) })
            {
                var shape = new SmoothMesh(1);
                List<Vector2> section = profile;
                shape.Surface(0, steps, section.Count - 1, (u, v) =>
                {
                    Vector2 p = AlongProfile(section, v);
                    return Vector3.Lerp(from, to, u) + inside * p.x + Vector3.up * p.y;
                });

                bed.Add(shape, (p, n) => PaintBoard(p, n, alongX ? p.x : p.z, Vector3.Dot(n, inside)));
            }
        }

        /// <summary>
        /// A board's cross-section, (across, up) with across running from the outside (-) to the soil
        /// (+): up the outer face, over a rounded top, down the inner face. The upper board is rounded
        /// underneath too, which leaves a groove between the two.
        /// </summary>
        static List<Vector2> Profile(float bottom, float top, bool roundUnderneath)
        {
            float h = BoardThickness * 0.5f, r = BoardRound;
            var points = new List<Vector2>();

            if (roundUnderneath)
            {
                points.Add(new Vector2(0f, bottom));
                Arc(points, new Vector2(-h + r, bottom + r), r, 270f, 180f);
            }
            else
            {
                points.Add(new Vector2(-h, bottom));
            }

            Arc(points, new Vector2(-h + r, top - r), r, 180f, 90f);
            Arc(points, new Vector2(h - r, top - r), r, 90f, 0f);

            if (roundUnderneath)
            {
                Arc(points, new Vector2(h - r, bottom + r), r, 0f, -90f);
                points.Add(new Vector2(0f, bottom));
            }
            else
            {
                points.Add(new Vector2(h, bottom));
            }

            return points;
        }

        static void Arc(List<Vector2> points, Vector2 centre, float radius, float fromDegrees, float toDegrees)
        {
            const int Segments = 2;
            for (int i = 0; i <= Segments; i++)
            {
                float a = Mathf.Lerp(fromDegrees, toDegrees, i / (float)Segments) * Mathf.Deg2Rad;
                points.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        static Vector2 AlongProfile(List<Vector2> profile, float t)
        {
            float f = Mathf.Clamp01(t) * (profile.Count - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), profile.Count - 2);
            return Vector2.Lerp(profile[i], profile[i + 1], f - i);
        }

        /// <summary>
        /// Weathered timber: lighter on top, a worn pale edge along the top corners, darker down to
        /// the ground and on the face against the soil, with a slow grain along its length.
        /// </summary>
        static Color PaintBoard(Vector3 p, Vector3 n, float along, float facingSoil)
        {
            bool upper = p.y > BoardSplit;
            Color c = upper ? WoodUpper : WoodLower;

            c = Color.Lerp(c, WoodTop, Mathf.Clamp01(n.y) * (upper ? 1f : 0.55f));

            // The rounded top corners catch the wear.
            float worn = Mathf.Clamp01(1f - Mathf.Abs(n.y - 0.6f) / 0.35f) * Mathf.InverseLerp(BoardTop - 0.035f, BoardTop, p.y);
            c = Color.Lerp(c, WoodWorn, worn * 0.7f);

            // Whole cycles a metre, so the pattern runs on unbroken into the next pot's boards.
            float grain = 1f + 0.05f * Mathf.Sin(2f * Mathf.PI * along + p.y * 30f)
                             + 0.035f * Mathf.Sin(2f * Mathf.PI * (2f * along) + 1.7f);
            float ground = Mathf.Lerp(0.5f, 1f, Smooth(Mathf.InverseLerp(-0.01f, 0.08f, p.y)));
            float soilSide = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(facingSoil));
            return c * (grain * ground * soilSide);
        }

        /// <summary>A square post with rounded edges, standing on <paramref name="at"/> (height ignored).</summary>
        static void Post(PaintedMesh bed, Vector3 at, float width, float top)
        {
            var shape = new SmoothMesh(1);
            float height = top - Footing;
            shape.RoundedBox(0, new Vector3(at.x, Footing + height * 0.5f, at.z), new Vector3(width, height, width),
                             0.022f, Quaternion.identity, 2);

            bed.Add(shape, (p, n) =>
            {
                Color c = Color.Lerp(PostSide, PostCap, Mathf.Clamp01(n.y));
                float worn = Mathf.Clamp01(1f - Mathf.Abs(n.y - 0.6f) / 0.35f) * Mathf.InverseLerp(top - 0.04f, top, p.y);
                c = Color.Lerp(c, WoodWorn, worn * 0.5f);
                return c * Mathf.Lerp(0.5f, 1f, Smooth(Mathf.InverseLerp(-0.01f, 0.09f, p.y)));
            });
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color c);
            return c;
        }

        // --- Assets ----------------------------------------------------------------------------------

        static Material BedMaterial()
        {
            ContentGenerator.EnsureFolder(Folder);

            string path = $"{Folder}/{MaterialName}.mat";
            Shader shader = Shader.Find("LastOrders/Stylized");
            if (shader == null) throw new InvalidOperationException("Shader LastOrders/Stylized is missing.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = MaterialName };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_ShadeColor", new Color(0.55f, 0.6f, 0.74f));
            material.SetFloat("_Wrap", 0.55f);
            material.SetFloat("_LightStrength", 0.88f);
            material.SetFloat("_AmbientStrength", 0.62f);
            material.SetFloat("_ShadowStrength", 0.8f);
            material.SetFloat("_RimStrength", 0.08f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        /// <summary>
        /// The tile set, its pieces saved inside it. A piece already there is rewritten in place, so
        /// re-running the builder keeps every reference to the set.
        /// </summary>
        static AutoTileSet SaveTiles(Mesh[] pieces, Mesh joint)
        {
            var set = AssetDatabase.LoadAssetAtPath<AutoTileSet>(TilesPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<AutoTileSet>();
                set.name = "SoilBedTiles";
                AssetDatabase.CreateAsset(set, TilesPath);
            }

            Dictionary<string, Mesh> stored = AssetDatabase.LoadAllAssetsAtPath(TilesPath).OfType<Mesh>()
                                                           .GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());

            Mesh Keep(Mesh built)
            {
                if (stored.TryGetValue(built.name, out Mesh existing))
                {
                    Overwrite(existing, built);
                    Object.DestroyImmediate(built);
                    return existing;
                }

                AssetDatabase.AddObjectToAsset(built, set);
                return built;
            }

            var kept = new Mesh[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) kept[i] = Keep(pieces[i]);

            set.Configure(kept, Keep(joint), Half, Inset);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            return set;
        }

        /// <summary>A bed on its own, with a border all round: what a pot shows before it joins, and the placing ghost.</summary>
        static Mesh SaveLoneBed(AutoTileSet set)
        {
            Mesh lone = set.Combine(0, "SoilBed");
            lone.name = "SoilBed";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(lone, MeshPath);
                AssetDatabase.SaveAssetIfDirty(lone);
                return lone;
            }

            Overwrite(existing, lone);
            Object.DestroyImmediate(lone);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        /// <summary>
        /// Rewrites a saved mesh through the mesh API, not CopySerialized: that copies the data but
        /// leaves the GPU drawing the old vertices with the new triangles until the editor restarts.
        /// </summary>
        static void Overwrite(Mesh existing, Mesh built)
        {
            existing.Clear();
            existing.indexFormat = built.indexFormat;
            existing.SetVertices(built.vertices);
            existing.SetNormals(built.normals);
            existing.SetColors(built.colors32);
            existing.subMeshCount = built.subMeshCount;
            for (int s = 0; s < built.subMeshCount; s++) existing.SetTriangles(built.GetTriangles(s), s);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
        }

        // --- The prefabs and the scene -------------------------------------------------------------

        /// <summary>
        /// PlantPotVisuals, which both every pot and the placing ghost show: the lone bed in, centred
        /// on the cell (the joined pieces are modelled round the centre), and the old dirt out.
        /// </summary>
        static string DressVisuals(Mesh mesh, Material material)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(VisualsPrefabPath);

            try
            {
                Transform old = root.transform.Find(OldSoilName);
                bool hadSkirt = old != null;
                if (hadSkirt) Object.DestroyImmediate(old.gameObject);

                Transform planter = root.transform.Find(OldPlanterName);
                if (planter != null) planter.gameObject.SetActive(false);

                Transform bed = root.transform.Find(BedName);
                if (bed == null)
                {
                    bed = new GameObject(BedName, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    bed.SetParent(root.transform, false);
                }

                bed.SetLocalPositionAndRotation(Centre, Quaternion.identity);
                bed.localScale = Vector3.one;
                bed.GetComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = bed.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { material };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                PrefabUtility.SaveAsPrefabAsset(root, VisualsPrefabPath);
                return $"PlantPotVisuals: raised bed in{(hadSkirt ? ", dirt skirt out" : "")}, on {MaterialName} (LastOrders/Stylized).";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// pfPlantPot: joins up with its neighbours (a GridAutoTile driving the bed), crops anchored on
        /// the soil's crown, and a tap box over the whole cell so a seed dragged across a bed lands in
        /// every pot it passes.
        /// </summary>
        static string DressPot(AutoTileSet set)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PotPrefabPath);

            try
            {
                Transform visuals = root.transform.Find("PlantPotVisuals");
                Transform bed = visuals != null ? visuals.Find(BedName) : null;
                if (bed == null) return "pfPlantPot: WARNING no PlantPotVisuals/SoilBed found; not joined up.";

                var tile = root.GetComponent<GridAutoTile>();
                if (tile == null) tile = root.AddComponent<GridAutoTile>();
                tile.Configure(set, bed.GetComponent<MeshFilter>(), Centre);

                Transform plant = visuals.Find(PlantPath);
                if (plant != null) plant.localPosition = new Vector3(Centre.x, Crown, Centre.z);

                var box = root.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = new Vector3(0.5f, 0.3f, 0.5f);
                    box.size = new Vector3(1f, 0.6f, 1f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PotPrefabPath);
                return $"pfPlantPot: joins neighbouring pots (GridAutoTile on SoilBedTiles); " +
                       $"{(plant != null ? $"crops at {Crown:0.00} m" : "WARNING no crop anchor")}; tap box the whole cell.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The open scene's pots, squared onto the grid cell under each one's middle, facing the way
        /// their placement says and standing at the grid's height, as a placed pot would. Pots only
        /// join cell to cell, so one left a fraction of a cell off would leave a gap or an overlap.
        /// </summary>
        static string SquareScenePots()
        {
            Scene scene = SceneManager.GetActiveScene();
            GridBuildingSystem3D grid = Object.FindFirstObjectByType<GridBuildingSystem3D>(FindObjectsInactive.Include);
            Vector3 origin = grid != null && grid.Settings != null ? grid.Settings.origin : Vector3.zero;
            float size = grid != null && grid.Settings != null ? grid.Settings.cellSize : 1f;

            var taken = new HashSet<Vector2Int>();
            int moved = 0, clashes = 0;

            foreach (PlantPot pot in Object.FindObjectsByType<PlantPot>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                if (pot.gameObject.scene != scene) continue;

                Vector3 middle = pot.transform.TransformPoint(Centre);
                var cell = new Vector2Int(Mathf.FloorToInt((middle.x - origin.x) / size), Mathf.FloorToInt((middle.z - origin.z) / size));
                if (!taken.Add(cell))
                {
                    clashes++;
                    continue;
                }

                var placed = pot.GetComponent<PlacedObject_Done>();
                PlacedObjectTypeSO.Dir dir = placed != null ? placed.Dir : PlacedObjectTypeSO.Dir.Down;
                Vector2Int pivotCell = cell + GridFootprint.PivotOffset(1, 1, (int)dir);
                Vector3 pivot = origin + new Vector3(pivotCell.x * size, 0f, pivotCell.y * size);
                Quaternion facing = Quaternion.Euler(0f, (int)dir * 90f, 0f);

                if ((pot.transform.position - pivot).sqrMagnitude < 1e-6f && Quaternion.Angle(pot.transform.rotation, facing) < 0.01f)
                    continue;

                Undo.RecordObject(pot.transform, "Square plant pot");
                pot.transform.SetPositionAndRotation(pivot, facing);

                if (placed != null)
                {
                    Undo.RecordObject(placed, "Square plant pot");
                    placed.placedPosition = pivot;
                }

                moved++;
            }

            if (moved > 0) EditorSceneManager.MarkSceneDirty(scene);

            string report = moved > 0
                ? $"Scene: {moved} pots squared onto their grid cells (the scene is not saved)."
                : "Scene: the pots already sit on their grid cells.";
            return clashes > 0 ? report + $" WARNING: {clashes} pots share a cell with another and were left where they are." : report;
        }

        // --- Painted meshes ------------------------------------------------------------------------

        /// <summary>
        /// Parts modelled with <see cref="SmoothMesh"/>, each with its colour painted onto its
        /// vertices from where each one is and which way it faces, merged into one mesh with one
        /// submesh and no UVs: the whole bed is one material.
        /// </summary>
        sealed class PaintedMesh
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Color32> colours = new List<Color32>();
            readonly List<int> triangles = new List<int>();

            public void Add(SmoothMesh shape, Func<Vector3, Vector3, Color> paint)
            {
                Mesh part = shape.Build("part", out List<int> used);

                try
                {
                    if (used.Count == 0) return;

                    int start = vertices.Count;
                    Vector3[] v = part.vertices, n = part.normals;
                    for (int i = 0; i < v.Length; i++)
                    {
                        vertices.Add(v[i]);
                        normals.Add(n[i]);
                        Color c = paint(v[i], n[i]);
                        c.a = 1f;
                        colours.Add(c);
                    }

                    foreach (int index in part.GetTriangles(0)) triangles.Add(start + index);
                }
                finally
                {
                    Object.DestroyImmediate(part);
                }
            }

            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colours);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
