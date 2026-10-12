using System.Collections.Generic;
using System.Text;
using FoodStreet.Visitors;
using FoodStreet.Visitors.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using B = LastOrders.EditorTools.BuildingKit;
using E = LastOrders.EditorTools.EnvKit;
using T = LastOrders.EditorTools.TextureGenerator;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The diner's car park, made properly: the drive in from the gate, the lot and the corner
    /// between them paved as one surface with its markings painted in, edged all round with precast
    /// kerbs on rounded corners - painted red and cream either side of the gate, dropped flush where
    /// cars swing in and where people walk off it - a wheel stop at the back of every bay, three lamp
    /// posts in the diner's livery that throw pools of light at night (the one by the gate carries
    /// the parking sign), a hedge down the far side with a gap toward the lot gate, shrubs along the
    /// wall, and stepping stones up to the diner door along the line visitors already walk.
    ///
    /// Everything is measured from the scene - the old apron, the drive, the gate, the bays and the
    /// route nodes - so it sits where the cars park and drive. Nothing the game reads is touched: the
    /// bays, the lane nodes and the route stay as they are, and none of it collides.
    ///
    /// Written as new assets in a folder per scene; the old apron, the drive and its elbow are only
    /// switched off, and 'Restore Previous Car Park' switches them back on. The drive's stretch
    /// outside the gate is laid again under the car park, and 'Build World Edges' re-lays it when it
    /// moves the road. Re-running 'Set Up Visitor System' rebuilds the VisitorSystem and loses the
    /// car park with it; run this again after it.
    /// </summary>
    public static class CarParkModel
    {
        const string RootName = "CarPark";
        const string Folder = EnvKit.TextureFolder + "/CarPark";

        /// <summary>The paved surface's top: over the old drive slab (0.045) it is laid on.</summary>
        const float SurfaceTop = 0.05f;

        const float KerbWidth = 0.2f;
        const float KerbHeight = 0.185f;
        const float DroppedHeight = 0.072f;

        /// <summary>Where a lamp's head is, in the lamp's own space: its arm reaches along +Z.</summary>
        static readonly Vector3 HeadLocal = new Vector3(0f, 4.18f, 1.2f);

        /// <summary>Toward the camera, along the ground: what a sign faces to be read.</summary>
        static readonly Vector3 TowardCamera = new Vector3(0.6f, 0f, -0.8f);

        // --- The palette ------------------------------------------------------------------------------
        //
        // Concrete as the compound wall's, the diner's livery (teal, cream, red) on the lamps and the
        // kerbs at the gate, a muted blue parking sign, and a hedge darker than the lawn so it reads
        // against it. Painted a little darker than it shows, as everything lit by LastOrders/Stylized is.

        static readonly Color[] KerbGrey = { B.Hex("9E9C95"), B.Hex("97948D"), B.Hex("A3A099"), B.Hex("928F88") };
        static readonly Color KerbDropped = B.Hex("A6A39B");
        static readonly Color Red = B.Hex("8E3B33");
        static readonly Color Cream = B.Hex("AFA898");
        static readonly Color Teal = B.Hex("3D6762");
        static readonly Color TealDark = B.Hex("33554F");
        static readonly Color Footing = B.Hex("8F8D87");
        static readonly Color Iron = B.Iron;
        static readonly Color StopConcrete = B.Hex("A19E96");
        static readonly Color StopPaint = B.Hex("B39A55");
        static readonly Color SignBlue = B.Hex("46627C");
        static readonly Color SignCream = B.Hex("B9B19F");
        static readonly Color Glyph = B.Hex("CFC8B6");
        static readonly Color Lens = B.Hex("D6D0BD");
        static readonly Color[] Flagstone = { B.Hex("8E897E"), B.Hex("958F84"), B.Hex("878276"), B.Hex("9A9488"), B.Hex("817C71") };
        static readonly Color Moss = B.Hex("6E7A4E");
        static readonly Color HedgeLow = B.Hex("3E5530");
        static readonly Color HedgeHigh = B.Hex("6B8A45");

        enum Edge
        {
            /// <summary>No kerb: the gateway, where the paving runs on under the gate.</summary>
            Open,
            Kerb,

            /// <summary>Red and cream, either side of the way in.</summary>
            Painted,

            /// <summary>Flush with the tarmac: where cars swing over it and where people walk off.</summary>
            Dropped,
        }

        /// <summary>The car park as the scene has it, and the outline that follows from it.</summary>
        sealed class Plan
        {
            public Rect Apron, Drive, Elbow;
            public float GateZ, LaneZ, LaneHalf;
            public Vector2 BaySize;
            public readonly List<ParkingSlot> Bays = new List<ParkingSlot>();
            public Vector3 Door, GateInside, LaneEntry, LaneExit;

            /// <summary>The stretch of the far (east) edge left open, from the lane toward the lot gate.</summary>
            public Vector2 EastGap;

            public Vector2 PathFoot;
            public List<Vector2> Outline;
            public List<Edge> Edges;
            public Rect Area;
        }

        // --- Menus --------------------------------------------------------------------------------------

        [MenuItem("Last Orders/Environment/Build Car Park (Current Scene)", priority = 88)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This builds the car park in the open scene:\n\n" +
                "- the drive, the lot and the corner between them paved as one, bays, arrows and wear painted in\n" +
                "- kerbs on rounded corners, wheel stops, three lamp posts with night light pools and a parking sign\n" +
                "- a hedge, shrubs, and stepping stones up to the diner door\n\n" +
                "The bays, lanes and routes are not touched. The old apron and drive slabs are switched off, not deleted - " +
                "'Restore Previous Car Park' puts it back. The scene is left unsaved.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply(save: false));
        }

        [MenuItem("Last Orders/Environment/Restore Previous Car Park", priority = 89)]
        public static void RunRestore()
        {
            if (EditorUtility.DisplayDialog("Last Orders", "Switch the new car park off and put the old flat apron back? " +
                                            "The scene is left unsaved.", "Restore", "Cancel"))
                Debug.Log(Restore(save: false));
        }

        /// <summary>Builds the car park in the open scene, replacing one built before; no dialogs.</summary>
        public static string Apply(bool save)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "[Last Orders] Leave play mode first; nothing was changed.";

            Scene scene = SceneManager.GetActiveScene();
            GameObject visitors = FindRoot(scene, "VisitorSystem");
            if (!TryMeasure(scene, visitors, out Plan plan, out string problem))
                return $"[Last Orders] {problem} Nothing was changed.";

            var report = new StringBuilder("[Last Orders] Car park built.\n");
            string folder = $"{Folder}/{scene.name}";
            ContentGenerator.EnsureFolder(folder);

            Transform parking = visitors.transform.Find("Parking");
            Transform previous = parking.Find(RootName);
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(parking, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build car park");

            // The surface, its markings painted into its own texture.
            Texture2D painted = T.PaintCarPark($"{folder}/CarPark_Surface.png", Layout(plan));
            Mesh surface = SaveMesh(BuildSurface(plan), $"{folder}/CarPark_Surface.asset");
            Place(root, "Surface", surface, new[] { SurfaceMaterial(scene.name, painted) }, castShadows: false);
            LayDriveToGate(root, plan.Drive, plan.GateZ);
            report.Append($"  Surface: {plan.Area.width:0.#} x {plan.Area.height:0.#} m painted, {surface.vertexCount} verts; " +
                          "the drive outside the gate laid on its own.\n");

            B.Model hardscape = BuildHardscape(plan, out int stones, out int flags);
            Mesh kerbs = hardscape.Save($"{folder}/CarPark_Kerbs.asset", "CarPark_Kerbs");
            Place(root, "Kerbs", kerbs, B.MaterialsFor(kerbs), castShadows: true);
            report.Append($"  Kerbs: {stones} stones, {plan.Bays.Count} wheel stops, {flags} stepping stones to the door.\n");

            Mesh planting = BuildPlanting(plan).Save($"{folder}/CarPark_Planting.asset", "CarPark_Planting");
            Place(root, "Planting", planting, B.MaterialsFor(planting), castShadows: true);

            Mesh lamp = BuildLamp(null).Save($"{folder}/CarPark_Lamp.asset", "CarPark_Lamp");
            Mesh signed = BuildLamp(TowardCamera).Save($"{folder}/CarPark_LampSign.asset", "CarPark_LampSign");

            float southRow = RowZ(plan, north: false), northRow = RowZ(plan, north: true);
            var lamps = new (string name, Vector3 foot, float yaw, Mesh mesh)[]
            {
                ("Lamp_Gate", new Vector3(plan.Apron.xMin + 0.8f, 0f, plan.GateZ + 1.0f), 0f, signed),
                ("Lamp_SouthRow", new Vector3(plan.Apron.xMax + 0.62f, 0f, southRow), -90f, lamp),
                ("Lamp_NorthRow", new Vector3(plan.Apron.xMax + 0.62f, 0f, northRow), -90f, lamp),
            };

            Material pool = E.SoftSpot("LampPool", new Color(1f, 0.82f, 0.48f, 0.55f), additive: true, nightOnly: true, fogTint: false);
            Material halo = E.SoftSpot("LampHalo", new Color(1f, 0.86f, 0.6f, 0.85f), additive: true, nightOnly: true, fogTint: false);

            foreach (var (name, foot, yaw, mesh) in lamps)
            {
                GameObject go = Place(root, name, mesh, B.MaterialsFor(mesh), castShadows: true);
                go.transform.SetPositionAndRotation(foot, Quaternion.Euler(0f, yaw, 0f));

                Vector3 head = go.transform.TransformPoint(HeadLocal);
                GameObject spot = E.GroundSpot("Pool", go.transform, go.transform.InverseTransformPoint(new Vector3(head.x, 0.075f, head.z)), 8.5f, pool);
                NightLightsSetup.CarParkLamp(spot);
                E.GroundSpot("Halo", go.transform, HeadLocal + Vector3.down * 0.13f, 0.95f, halo);
            }

            report.Append("  Lamps: by the gate (with the parking sign) and at the far end of each row, light pools at night.\n");

            foreach (Transform old in OldSurfaces(visitors.transform))
            {
                if (old == null || !old.gameObject.activeSelf) continue;
                Undo.RecordObject(old.gameObject, "Build car park");
                old.gameObject.SetActive(false);
                report.Append($"  '{old.name}' switched off (kept).\n");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            return report.ToString();
        }

        /// <summary>The new car park off and the old flat apron back on, in the open scene.</summary>
        public static string Restore(bool save)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject visitors = FindRoot(scene, "VisitorSystem");
            Transform parking = visitors != null ? visitors.transform.Find("Parking") : null;
            if (parking == null) return "[Last Orders] No VisitorSystem/Parking in the open scene; nothing was changed.";

            var report = new StringBuilder("[Last Orders] Previous car park restored.\n");

            Transform ours = parking.Find(RootName);
            if (ours != null && ours.gameObject.activeSelf)
            {
                Undo.RecordObject(ours.gameObject, "Restore car park");
                ours.gameObject.SetActive(false);
                report.Append($"  '{RootName}' switched off.\n");
            }

            foreach (Transform old in OldSurfaces(visitors.transform))
            {
                if (old == null || old.gameObject.activeSelf) continue;
                Undo.RecordObject(old.gameObject, "Restore car park");
                old.gameObject.SetActive(true);
                report.Append($"  '{old.name}' back on.\n");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            return report.ToString();
        }

        // --- Measuring ----------------------------------------------------------------------------------

        static bool TryMeasure(Scene scene, GameObject visitors, out Plan plan, out string problem)
        {
            plan = null;
            problem = null;

            if (visitors == null)
            {
                problem = $"No VisitorSystem in '{scene.name}' - run 'Set Up Visitor System' first.";
                return false;
            }

            Transform parking = visitors.transform.Find("Parking");
            var roads = visitors.GetComponent<VisitorRoads>();
            var lot = parking != null ? parking.GetComponent<ParkingLot>() : null;
            if (parking == null || roads == null || lot == null || !roads.IsComplete)
            {
                problem = "The VisitorSystem has no parking lot or an unwired route.";
                return false;
            }

            plan = new Plan();
            plan.Apron = WorldRect(parking.Find("Apron"), out Rect apron) ? apron : ZoneLayout.Apron;
            plan.Drive = WorldRect(visitors.transform.Find("Drive/Drive"), out Rect drive) ? drive : ZoneLayout.Drive;
            plan.Elbow = WorldRect(visitors.transform.Find("Drive/DriveToApron"), out Rect elbow) ? elbow : ZoneLayout.DriveToApron;

            GameObject walls = FindRoot(scene, "CompoundWalls");
            Transform gate = walls != null ? walls.transform.Find("South_gate") : null;
            plan.GateZ = gate != null ? gate.position.z : PerimeterStripLayout.SouthLine;

            plan.Bays.AddRange(parking.GetComponentsInChildren<ParkingSlot>(true));
            plan.LaneZ = plan.Apron.center.y;
            plan.LaneHalf = ZoneLayout.LaneWidth * 0.5f;
            plan.BaySize = new Vector2(plan.Apron.width / ZoneLayout.BaysPerRow, (plan.Apron.height - ZoneLayout.LaneWidth) * 0.5f);

            plan.Door = roads.DoorOutside;
            plan.GateInside = roads.GateInside;
            plan.LaneEntry = lot.LaneEntry;
            plan.LaneExit = lot.LaneExit;
            plan.EastGap = new Vector2(plan.LaneZ - plan.LaneHalf, plan.LaneZ + plan.LaneHalf + 1.4f);

            float north = plan.Elbow.yMax;
            plan.PathFoot = new Vector2(Mathf.Min(plan.LaneEntry.x - 1f, plan.Apron.xMin - 2.5f), north + 0.32f);

            // The shape this tool knows how to pave: a drive up from the gate, the lot east of it, the
            // drive's elbow meeting the lot's lane. Anything else is said rather than built wrong.
            bool fits = plan.Bays.Count > 0 &&
                        plan.Drive.xMax < plan.Apron.xMin - 1.5f &&
                        plan.GateZ < plan.Apron.yMin - 1.1f &&
                        north > plan.Apron.yMin + 3f && north < plan.Apron.yMax - 3f &&
                        plan.Elbow.xMax >= plan.Apron.xMin - 0.1f;
            if (!fits)
            {
                problem = $"The car park's layout is not the drive-elbow-lot shape this tool paves (apron {plan.Apron}, drive " +
                          $"{plan.Drive}, elbow {plan.Elbow}, gate z {plan.GateZ:0.##}).";
                return false;
            }

            plan.Outline = Outline(plan, out plan.Edges);
            plan.Area = BoundsOf(plan.Outline);
            return true;
        }

        /// <summary>
        /// The paved edge, counter-clockwise from the gate's west post, every corner rounded - the
        /// lot's and the drive's outside corners cut back, the grass corners' tips filled in - and
        /// what kind of edge each stretch is.
        /// </summary>
        static List<Vector2> Outline(Plan p, out List<Edge> edges)
        {
            float xW = p.Drive.xMin, xD = p.Drive.xMax, zG = p.GateZ, zN = p.Elbow.yMax;
            float aX0 = p.Apron.xMin, aX1 = p.Apron.xMax, aZ0 = p.Apron.yMin, aZ1 = p.Apron.yMax;

            var corners = new (Vector2 at, float radius)[]
            {
                (new Vector2(xW, zG), 0f),
                (new Vector2(xD, zG), 0f),
                (new Vector2(xD, aZ0), 1f),
                (new Vector2(aX1, aZ0), 1.5f),
                (new Vector2(aX1, aZ1), 1.5f),
                (new Vector2(aX0, aZ1), 1f),
                (new Vector2(aX0, zN), 1.5f),
                (new Vector2(xW, zN), 1.5f),
            };

            var points = new List<Vector2>();
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 c = corners[i].at;
                float r = corners[i].radius;
                if (r <= 0f)
                {
                    points.Add(c);
                    continue;
                }

                Vector2 toPrev = (corners[(i + corners.Length - 1) % corners.Length].at - c).normalized;
                Vector2 toNext = (corners[(i + 1) % corners.Length].at - c).normalized;
                Vector2 centre = c + (toPrev + toNext) * r;
                Vector2 from = c + toPrev * r, to = c + toNext * r;

                float a0 = Mathf.Atan2(from.y - centre.y, from.x - centre.x);
                float sweep = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, Mathf.Atan2(to.y - centre.y, to.x - centre.x) * Mathf.Rad2Deg) *
                              Mathf.Deg2Rad;
                int steps = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(sweep) * r / 0.28f));
                for (int s = 0; s <= steps; s++)
                {
                    float a = a0 + sweep * s / steps;
                    points.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }

            Edge KindAt(Vector2 m)
            {
                if (Mathf.Abs(m.y - zG) < 0.05f) return Edge.Open;
                if (m.y < zG + 3.2f && m.x < xD + 1.05f) return Edge.Painted;
                if (m.x > aX1 - 0.3f && m.y > p.EastGap.x && m.y < p.EastGap.y) return Edge.Dropped;
                if (m.x > aX0 - 1.6f && m.x < aX0 + 0.3f && m.y > zN - 0.1f && m.y < zN + 2.9f) return Edge.Dropped;
                if (Mathf.Abs(m.y - zN) < 0.1f && m.x > p.PathFoot.x - 1.1f && m.x < aX0) return Edge.Dropped;
                return Edge.Kerb;
            }

            // Each stretch classified in short pieces, so a kind can change partway along a straight
            // edge, then the pieces merged back into straight runs of one kind for the kerbstones.
            var pieces = new List<(Vector2 from, Edge kind)>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.25f));
                for (int k = 0; k < n; k++)
                {
                    Vector2 from = Vector2.Lerp(a, b, k / (float)n), to = Vector2.Lerp(a, b, (k + 1) / (float)n);
                    pieces.Add((from, KindAt((from + to) * 0.5f)));
                }
            }

            var merged = new List<Vector2>();
            edges = new List<Edge>();
            for (int i = 0; i < pieces.Count; i++)
            {
                if (merged.Count > 0)
                {
                    Vector2 before = merged[merged.Count - 1];
                    Vector2 here = pieces[i].from, next = pieces[(i + 1) % pieces.Count].from;
                    bool straight = Vector2.Dot((here - before).normalized, (next - here).normalized) > 0.9999f;
                    if (straight && pieces[i].kind == edges[edges.Count - 1]) continue;
                }

                merged.Add(pieces[i].from);
                edges.Add(pieces[i].kind);
            }

            return merged;
        }

        static T.CarParkLayout Layout(Plan p)
        {
            var layout = new T.CarParkLayout
            {
                Area = p.Area,
                KerbWidth = KerbWidth,
                BaySize = p.BaySize,
                Lane = Rect.MinMaxRect(p.Apron.xMin, p.LaneZ - p.LaneHalf, p.Apron.xMax, p.LaneZ + p.LaneHalf),
                GateInside = XZ(p.GateInside),
                LaneEntry = XZ(p.LaneEntry),
                LaneExit = XZ(p.LaneExit),
                DriveCentreX = p.Drive.center.x,
                GateZ = p.GateZ,
            };

            layout.Outline.AddRange(p.Outline);
            foreach (Edge e in p.Edges) layout.Kerbed.Add(e != Edge.Open);

            foreach (ParkingSlot bay in p.Bays)
            {
                layout.BayCentres.Add(XZ(bay.transform.position));
                layout.BayForwards.Add(XZ(bay.transform.forward).normalized);
            }

            return layout;
        }

        // --- The surface ---------------------------------------------------------------------------------

        /// <summary>The outline filled in at <see cref="SurfaceTop"/>, UV'd over the area its texture is painted for.</summary>
        static Mesh BuildSurface(Plan p)
        {
            var mesh = new EnvMesh(1);
            List<Vector2> polygon = Simplified(p.Outline);
            List<int> triangles = Triangulate(polygon);

            Vector2 UV(Vector2 q) => new Vector2((q.x - p.Area.xMin) / p.Area.width, (q.y - p.Area.yMin) / p.Area.height);
            Vector3 At(Vector2 q) => new Vector3(q.x, SurfaceTop, q.y);

            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                Vector2 a = polygon[triangles[i]], b = polygon[triangles[i + 1]], c = polygon[triangles[i + 2]];
                mesh.Triangle(0, At(a), At(b), At(c), UV(a), UV(b), UV(c), Vector3.up);
            }

            return mesh.Build("CarPark_Surface", out _);
        }

        static Material SurfaceMaterial(string sceneName, Texture2D texture) =>
            E.CachedMaterial("CarPark_Surface_" + sceneName.Replace(' ', '_'), () => new Material(Shader.Find("Universal Render Pipeline/Lit")),
                             m =>
                             {
                                 m.SetTexture("_BaseMap", texture);
                                 m.SetTextureScale("_BaseMap", Vector2.one);
                                 m.SetColor("_BaseColor", Color.white);
                                 T.BindNormal(m, texture);
                                 m.SetFloat("_Smoothness", 0.1f);
                                 m.SetFloat("_Metallic", 0f);
                                 m.SetFloat("_EnvironmentReflections", 0f);
                                 m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                                 m.enableInstancing = true;
                             });

        // --- Kerbs, wheel stops and the stepping stones ----------------------------------------------------

        static B.Model BuildHardscape(Plan p, out int stones, out int flags)
        {
            var m = new B.Model { Shade = (q, n) => Mathf.Lerp(0.8f, 1f, Smooth(q.y / 0.06f)) };

            stones = Kerbs(m, p);

            foreach (ParkingSlot bay in p.Bays) WheelStop(m, p, bay);

            flags = SteppingStones(m, p);
            return m;
        }

        /// <summary>
        /// Kerbstones along every edge but the gateway: a metre long on the straights, a short piece
        /// per step round a corner, each its own shade of concrete; red and cream by turns at the
        /// gate; low and flush where dropped, stepping down at either end of a dropped run.
        /// </summary>
        static int Kerbs(B.Model m, Plan p)
        {
            List<Vector2> outline = p.Outline;
            int count = outline.Count, placed = 0;

            for (int i = 0; i < count; i++)
            {
                Edge kind = p.Edges[i];
                if (kind == Edge.Open) continue;

                Vector2 a = outline[i], b = outline[(i + 1) % count];
                float length = Vector2.Distance(a, b);
                if (length < 0.02f) continue;

                bool raisedBefore = p.Edges[(i + count - 1) % count] is Edge.Kerb or Edge.Painted;
                bool raisedAfter = p.Edges[(i + 1) % count] is Edge.Kerb or Edge.Painted;

                int pieces = Mathf.Max(1, Mathf.RoundToInt(length / (kind == Edge.Painted ? 0.6f : 1f)));
                for (int s = 0; s < pieces; s++)
                {
                    Vector2 from = Vector2.Lerp(a, b, s / (float)pieces), to = Vector2.Lerp(a, b, (s + 1) / (float)pieces);
                    Vector2 dir = (to - from).normalized, inward = new Vector2(-dir.y, dir.x);
                    Vector2 middle = (from + to) * 0.5f + inward * (KerbWidth * 0.5f);

                    float height = KerbHeight;
                    Color colour = KerbGrey[B.Hash(placed * 7 + 3) % KerbGrey.Length] * (1f + 0.03f * B.Jitter(placed));
                    if (kind == Edge.Painted)
                    {
                        colour = placed % 2 == 0 ? Red : Cream;
                    }
                    else if (kind == Edge.Dropped)
                    {
                        bool step = (s == 0 && raisedBefore) || (s == pieces - 1 && raisedAfter);
                        height = step ? (KerbHeight + DroppedHeight) * 0.5f : DroppedHeight;
                        colour = KerbDropped * (1f + 0.03f * B.Jitter(placed));
                    }

                    var rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
                    var size = new Vector3(KerbWidth, height + 0.005f, Vector2.Distance(from, to) - 0.014f);
                    var centre = new Vector3(middle.x, height * 0.5f - 0.005f, middle.y);

                    Color top = colour * 1.04f, side = colour * 0.96f;
                    var shape = new SmoothMesh(1);
                    shape.RoundedBox(0, centre, size, kind == Edge.Dropped ? 0.02f : 0.035f, rotation, 2);
                    m.Add(shape, (q, n) => n.y > 0.6f ? top : side);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>
        /// A concrete wheel stop across the back of a bay, its ends painted, two bolt heads in its top:
        /// a wheel's width behind where the rear wheels of the longest car stand.
        /// </summary>
        static void WheelStop(B.Model m, Plan p, ParkingSlot bay)
        {
            Vector3 f = bay.transform.forward;
            f.y = 0f;
            f.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, f);
            var rotation = Quaternion.LookRotation(f, Vector3.up);

            Vector3 centre = bay.transform.position - f * (p.BaySize.y * 0.5f - KerbWidth - 0.45f);
            centre.y = SurfaceTop + 0.06f;

            const float length = 1.55f;
            m.Box(StopConcrete, centre, new Vector3(length, 0.12f, 0.17f), 0.04f, rotation, 2);

            foreach (float side in new[] { -1f, 1f })
            {
                m.Box(StopPaint, centre + right * (side * (length * 0.5f - 0.13f)), new Vector3(0.27f, 0.124f, 0.174f), 0.042f, rotation, 2);

                Vector3 bolt = centre + right * (side * 0.4f) + Vector3.up * 0.058f;
                m.Tube(Iron, bolt, bolt + Vector3.up * 0.014f, 0.024f, 8);
            }
        }

        /// <summary>
        /// Flagstones in the lawn from the edge of the tarmac to the diner door, along the line
        /// visitors walk from the lane: pairs and single wide slabs by turns, each laid a little
        /// askew. Thin enough to go under the diner's floor if the front is pushed out over them.
        /// </summary>
        static int SteppingStones(B.Model m, Plan p)
        {
            Vector2 start = p.PathFoot, end = new Vector2(p.Door.x, p.Door.z - 0.1f);
            Vector2 control = new Vector2(start.x - 0.7f, Mathf.Lerp(start.y, end.y, 0.6f));

            var curve = new List<Vector2>();
            for (int i = 0; i <= 120; i++)
            {
                float t = i / 120f;
                curve.Add((1f - t) * (1f - t) * start + 2f * t * (1f - t) * control + t * t * end);
            }

            var lengths = new List<float> { 0f };
            for (int i = 1; i < curve.Count; i++) lengths.Add(lengths[i - 1] + Vector2.Distance(curve[i - 1], curve[i]));
            float total = lengths[lengths.Count - 1];

            int key = 0, laid = 0, row = 0;
            for (float s = 0.32f; s < total - 0.24f; s += 0.6f, row++)
            {
                float along = s + 0.06f * B.Jitter(row * 13 + 2);
                int i = lengths.FindIndex(l => l >= along);
                if (i <= 0) i = 1;
                Vector2 at = curve[i];
                Vector2 tangent = (curve[i] - curve[i - 1]).normalized;
                Vector2 side = new Vector2(tangent.y, -tangent.x);
                float yaw = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg;
                float shift = (row % 2 == 0 ? 0.09f : -0.09f) + 0.04f * B.Jitter(row * 7 + 1);

                if (B.Hash(row * 5 + 1) % 4 != 0)
                {
                    laid += Flag(m, at + side * (shift - 0.34f), yaw, new Vector2(0.64f + 0.08f * B.Jitter(key + 2), 0.56f + 0.06f * B.Jitter(key + 3)),
                                 key++);
                    laid += Flag(m, at + side * (shift + 0.35f), yaw, new Vector2(0.62f + 0.08f * B.Jitter(key + 2), 0.57f + 0.06f * B.Jitter(key + 3)),
                                 key++);
                }
                else
                {
                    laid += Flag(m, at + side * shift, yaw, new Vector2(1.05f + 0.08f * B.Jitter(key + 2), 0.6f + 0.05f * B.Jitter(key + 3)), key++);
                }
            }

            return laid;
        }

        /// <summary>
        /// One flagstone: an irregular five- to seven-sided slab about <paramref name="size"/> across
        /// and along, turned by <paramref name="yaw"/>, its top edge rounded over, its sides darker
        /// and a little moss on some of them.
        /// </summary>
        static int Flag(B.Model m, Vector2 at, float yaw, Vector2 size, int key)
        {
            int corners = 5 + B.Hash(key * 19 + 4) % 3;
            float spin = B.Jitter(key * 23 + 9) * Mathf.PI;
            var outline = new Vector2[corners];
            Quaternion turn = Quaternion.Euler(0f, yaw + 9f * B.Jitter(key * 29 + 1), 0f);
            for (int k = 0; k < corners; k++)
            {
                float a = spin + (k + 0.3f * B.Jitter(key * 31 + k)) * Mathf.PI * 2f / corners;
                float reach = 0.86f + 0.14f * B.Jitter(key * 37 + k);
                Vector3 corner = turn * new Vector3(Mathf.Cos(a) * size.x * 0.5f * reach, 0f, Mathf.Sin(a) * size.y * 0.5f * reach);
                outline[k] = new Vector2(corner.x, corner.z);
            }

            // Laid round the way the angles increase: the outline has to run counter-clockwise for its faces to face out.
            if (Cross(outline[0], outline[1], outline[2]) < 0f) System.Array.Reverse(outline);

            Vector2 Edge(float u)
            {
                float f = Mathf.Repeat(u, 1f) * corners;
                int k = Mathf.FloorToInt(f) % corners;
                return Vector2.Lerp(outline[k], outline[(k + 1) % corners], f - Mathf.Floor(f));
            }

            const float top = 0.038f, bottom = -0.012f, bevel = 0.022f;
            float mean = (size.x + size.y) * 0.25f;
            Color stone = Flagstone[B.Hash(key * 11 + 5) % Flagstone.Length] * (1f + 0.04f * B.Jitter(key * 3));
            bool mossy = B.Hash(key * 41 + 2) % 3 == 0;

            m.Surface((q, n) =>
            {
                if (n.y < 0.7f) return stone * 0.8f;
                float edge = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(q.x, q.z), at) / mean);
                return mossy ? Color.Lerp(Moss, stone, Smooth(edge * 2.4f)) : stone;
            }, corners * 6, 8, (u, v) =>
            {
                Vector2 rim = Edge(u);
                float scale, y;
                if (v < 0.3f)
                {
                    scale = 1f;
                    y = Mathf.Lerp(bottom, top - bevel, v / 0.3f);
                }
                else if (v < 0.6f)
                {
                    float a = (v - 0.3f) / 0.3f * Mathf.PI * 0.5f;
                    scale = 1f - bevel / mean * (1f - Mathf.Cos(a));
                    y = top - bevel + bevel * Mathf.Sin(a);
                }
                else
                {
                    scale = (1f - bevel / mean) * (1f - (v - 0.6f) / 0.4f);
                    y = top + 0.003f * (1f - scale);
                }

                Vector2 p = at + rim * scale;
                return new Vector3(p.x, y, p.y);
            }, wrapU: true);

            return 1;
        }

        // --- Planting ---------------------------------------------------------------------------------

        /// <summary>
        /// A clipped hedge down the far side of the lot, behind the lamps, broken where the lane meets
        /// it so the way to the lot gate stays open; shrubs between the lot and the south wall; one by
        /// the diner's corner. Lumps of foliage overlapping, darker at the foot.
        /// </summary>
        static B.Model BuildPlanting(Plan p)
        {
            var m = new B.Model { Shade = (q, n) => Mathf.Lerp(0.74f, 1f, Smooth(q.y / 0.35f)) };
            int key = 0;

            float hedgeX = p.Apron.xMax + 1.35f;
            Hedge(m, hedgeX, p.Apron.yMin - 0.3f, p.EastGap.x - 0.3f, 0.4f, 0.68f, key++);
            Hedge(m, hedgeX, p.EastGap.y + 0.3f, p.Apron.yMax + 0.55f, 0.4f, 0.68f, key++);

            float verge = (p.GateZ + 0.32f + p.Apron.yMin) * 0.5f;
            foreach (float x in new[] { 2.1f, 6.8f, 8.9f })
            {
                var foot = new Vector3(p.Apron.xMin + x, 0f, verge);
                Bush(m, foot, 0.5f, 0.75f, key++);
                Bush(m, foot + new Vector3(0.42f * (B.Hash(key) % 2 == 0 ? 1f : -1f), 0f, 0.1f), 0.34f, 0.52f, key++);
                Flowers(m, foot, 0.5f, 0.75f, key++);
            }

            var corner = new Vector3(p.Apron.xMin - 0.6f, 0f, p.Apron.yMax - 0.9f);
            Bush(m, corner, 0.45f, 0.7f, key++);
            Bush(m, corner + new Vector3(-0.38f, 0f, -0.22f), 0.3f, 0.46f, key++);
            Flowers(m, corner, 0.45f, 0.7f, key);

            return m;
        }

        /// <summary>
        /// A clipped hedge along Z from <paramref name="z0"/> to <paramref name="z1"/>: one body, boxy
        /// in section with rounded shoulders, its ends rounded off, softly lumpy all over where the
        /// clipping has grown out, its foot in the ground. Darker low down, lit on top, mottled.
        /// </summary>
        static void Hedge(B.Model m, float x, float z0, float z1, float halfWidth, float height, int key)
        {
            float length = z1 - z0;
            if (length < 0.5f) return;

            const float cap = 0.4f, foot = -0.06f;
            float seed = key * 3.17f;
            float Noise(float a, float b) => Mathf.PerlinNoise(a + seed, b + seed * 0.5f);

            Vector3 At(float u, float v)
            {
                // Round the section the opposite way to u, so the faces look out (see SmoothMesh.Surface).
                float theta = -u * Mathf.PI * 2f;
                float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
                float z = z0 + v * length;

                float t = Mathf.Min(z - z0, z1 - z) / cap;
                float end = t >= 1f ? 1f : Mathf.Sqrt(Mathf.Max(0f, 1f - (1f - t) * (1f - t)));

                float lump = 1f + 0.16f * (Noise(z * 1.7f + c * 0.8f, s * 0.8f) - 0.5f);
                float sx = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.62f), sy = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.62f);
                float topY = foot + (height - foot) * Mathf.Lerp(0.6f, 1f, end) * lump;

                return new Vector3(x + sx * halfWidth * end * lump, foot + (sy + 1f) * 0.5f * (topY - foot), z);
            }

            Color Paint(Vector3 q, Vector3 n)
            {
                float mottle = 0.9f + 0.2f * Noise(q.z * 2.6f + q.x, q.y * 2.6f);
                return Color.Lerp(HedgeLow, HedgeHigh, Smooth(q.y / (height + 0.15f))) * mottle;
            }

            m.Surface(Paint, 28, Mathf.CeilToInt(length / 0.1f), At, wrapU: true);

            // Clumps of leaves grown out of the clipped top, by turns either side of the middle,
            // smaller toward the ends: what makes it a hedge rather than a green bolster.
            int clumps = Mathf.Max(2, Mathf.RoundToInt(length / 0.27f));
            for (int i = 0; i < clumps; i++)
            {
                int k = key * 131 + i;
                float z = Mathf.Lerp(z0 + 0.22f, z1 - 0.22f, (i + 0.5f) / clumps) + 0.05f * B.Jitter(k);
                float t = Mathf.Clamp01(Mathf.Min(z - z0, z1 - z) / cap);
                float end = Mathf.Sqrt(1f - (1f - t) * (1f - t));
                float radius = (0.22f + 0.06f * B.Jitter(k + 1)) * Mathf.Lerp(0.6f, 1f, end);
                float side = (i % 2 == 0 ? 1f : -1f) * (0.13f + 0.07f * B.Jitter(k + 2)) * end;
                var centre = new Vector3(x + side, Mathf.Lerp(0.6f, 1f, end) * height * 0.86f + 0.04f * B.Jitter(k + 3), z);
                Clump(m, centre, radius, height, k);
            }
        }

        /// <summary>A round clump of leaves on a hedge, painted to the hedge's own height so it shades into it.</summary>
        static void Clump(B.Model m, Vector3 centre, float radius, float hedgeHeight, int key)
        {
            float phase = B.Jitter(key) * Mathf.PI, phase2 = B.Jitter(key * 7 + 3) * Mathf.PI;

            Vector3 At(float u, float v)
            {
                float az = u * Mathf.PI * 2f;
                float el = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                float ce = Mathf.Cos(el);
                float bump = 1f + 0.08f * Mathf.Sin(3f * az + phase) * ce + 0.05f * Mathf.Sin(5f * az + 2f * el + phase2);
                return centre + new Vector3(Mathf.Cos(az) * ce, Mathf.Sin(el) * 0.82f, Mathf.Sin(az) * ce) * (radius * bump);
            }

            float tint = 1f + 0.07f * B.Jitter(key * 13 + 1);
            Color Paint(Vector3 q, Vector3 n) =>
                Color.Lerp(HedgeLow, HedgeHigh, Smooth(q.y / (hedgeHeight + 0.15f))) * tint * (n.y > 0.4f ? 1.05f : 0.95f);

            m.Surface(Paint, 12, 8, At, wrapU: true);
        }

        /// <summary>A lump of foliage: a squashed, lumpy sphere standing on <paramref name="foot"/>, its underside in the ground.</summary>
        static void Bush(B.Model m, Vector3 foot, float radius, float height, int key)
        {
            float phase = B.Jitter(key) * Mathf.PI, phase2 = B.Jitter(key * 7 + 3) * Mathf.PI;
            float tint = 1f + 0.06f * B.Jitter(key * 13 + 1);
            float cy = height * 0.42f, ry = height * 0.58f;

            Vector3 At(float u, float v)
            {
                float az = u * Mathf.PI * 2f;
                float el = Mathf.Lerp(-1f, Mathf.PI * 0.5f, v);
                float ce = Mathf.Cos(el);
                float bump = 1f + 0.075f * Mathf.Sin(3f * az + phase) * ce + 0.05f * Mathf.Sin(5f * az + 2f * el + phase2);
                return foot + new Vector3(Mathf.Cos(az) * ce * radius * bump, cy + Mathf.Sin(el) * ry * bump, Mathf.Sin(az) * ce * radius * bump);
            }

            Color Paint(Vector3 q, Vector3 n) => Color.Lerp(HedgeLow, HedgeHigh, Smooth((q.y - foot.y) / height)) * tint;

            m.Surface(Paint, 16, 9, At, wrapU: true);
        }

        /// <summary>A few small flowers dotted over the top of a bush.</summary>
        static void Flowers(B.Model m, Vector3 foot, float radius, float height, int key)
        {
            for (int i = 0; i < 6; i++)
            {
                float az = (B.Hash(key * 17 + i) % 628) / 100f;
                float el = 0.35f + (B.Hash(key * 29 + i) % 80) / 100f;
                var at = foot + new Vector3(Mathf.Cos(az) * Mathf.Cos(el) * radius * 1.04f, height * 0.42f + Mathf.Sin(el) * height * 0.61f,
                                            Mathf.Sin(az) * Mathf.Cos(el) * radius * 1.04f);
                Color petal = B.Flowers[B.Hash(key * 3 + i) % 2];
                m.Lathe((q, n) => petal, at, new List<Vector2>
                {
                    new Vector2(0f, -0.02f), new Vector2(0.035f, 0f), new Vector2(0.03f, 0.02f), new Vector2(0f, 0.028f),
                }, 7);
            }
        }

        // --- The lamp posts ----------------------------------------------------------------------------------

        /// <summary>
        /// A lamp post in the diner's livery: a concrete footing and an iron flange, a teal pole with a
        /// cream collar, a red band and a cream finial, a swan-neck arm reaching out along +Z to a teal
        /// head with a pale lens under it. With <paramref name="signFacing"/>, the parking sign is
        /// clamped to the pole facing that way.
        /// </summary>
        static B.Model BuildLamp(Vector3? signFacing)
        {
            var m = new B.Model { Shade = (q, n) => Mathf.Lerp(0.8f, 1f, Smooth(q.y / 0.3f)) };

            m.Lathe((q, n) => Footing, Vector3.zero, new List<Vector2>
            {
                new Vector2(0f, -0.03f), new Vector2(0.29f, -0.03f), new Vector2(0.29f, -0.03f), new Vector2(0.29f, 0.2f),
                new Vector2(0.23f, 0.27f), new Vector2(0.23f, 0.27f), new Vector2(0f, 0.27f),
            }, 16);

            m.Lathe((q, n) => Iron, new Vector3(0f, 0.27f, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.16f, 0f), new Vector2(0.16f, 0f), new Vector2(0.16f, 0.035f), new Vector2(0.12f, 0.05f),
                new Vector2(0.12f, 0.05f), new Vector2(0f, 0.05f),
            }, 14);

            for (int k = 0; k < 4; k++)
            {
                float a = (k + 0.5f) * Mathf.PI * 0.5f;
                Vector3 bolt = new Vector3(Mathf.Cos(a) * 0.135f, 0.3f, Mathf.Sin(a) * 0.135f);
                m.Tube(Iron, bolt, bolt + Vector3.up * 0.03f, 0.014f, 6);
            }

            m.Tube((q, n) => Teal, new Vector3(0f, 0.3f, 0f), new Vector3(0f, 3.95f, 0f), 0.095f, 0.072f, 16);

            m.Lathe((q, n) => Cream, new Vector3(0f, 0.3f, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.12f, 0f), new Vector2(0.12f, 0f), new Vector2(0.12f, 0.12f), new Vector2(0.1f, 0.17f),
                new Vector2(0.1f, 0.17f), new Vector2(0f, 0.17f),
            }, 14);

            m.Tube((q, n) => Red, new Vector3(0f, 2.56f, 0f), new Vector3(0f, 2.69f, 0f), 0.088f, 0.087f, 16);

            m.Lathe((q, n) => Cream, new Vector3(0f, 3.92f, 0f), new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.088f, 0f), new Vector2(0.088f, 0f), new Vector2(0.094f, 0.06f), new Vector2(0.062f, 0.12f),
                new Vector2(0f, 0.15f),
            }, 12);

            // The swan neck, swept along a curve up from the pole and out to the head.
            Vector3 p0 = new Vector3(0f, 3.82f, 0f), p1 = new Vector3(0f, 4.38f, 0.1f), p2 = new Vector3(0f, 4.22f, 0.98f);
            m.Surface((q, n) => Teal, 10, 14, (u, v) =>
            {
                Vector3 centre = (1f - v) * (1f - v) * p0 + 2f * v * (1f - v) * p1 + v * v * p2;
                Vector3 along = (2f * (1f - v) * (p1 - p0) + 2f * v * (p2 - p1)).normalized;
                Vector3 normal = Vector3.Cross(Vector3.right, along).normalized;
                float a = u * Mathf.PI * 2f;
                return centre + (Mathf.Cos(a) * Vector3.right + Mathf.Sin(a) * normal) * Mathf.Lerp(0.056f, 0.045f, v);
            }, wrapU: true);

            Quaternion tilt = Quaternion.Euler(6f, 0f, 0f);
            m.Box(TealDark, HeadLocal, new Vector3(0.38f, 0.16f, 0.66f), 0.075f, tilt, 2);
            m.Box(Cream, HeadLocal + tilt * new Vector3(0f, 0.08f, -0.04f), new Vector3(0.18f, 0.026f, 0.42f), 0.012f, tilt, 1);
            m.Box(Lens, HeadLocal + tilt * new Vector3(0f, -0.083f, 0.03f), new Vector3(0.28f, 0.036f, 0.48f), 0.015f, tilt, 1);

            if (signFacing.HasValue) Sign(m, signFacing.Value);
            return m;
        }

        /// <summary>
        /// The parking sign: a blue plate in a cream border with a cream "P", clamped to the pole by
        /// two iron brackets, facing <paramref name="facing"/>.
        /// </summary>
        static void Sign(B.Model m, Vector3 facing)
        {
            Vector3 f = new Vector3(facing.x, 0f, facing.z).normalized;
            var face = Quaternion.LookRotation(f, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, -f).normalized;
            const float height = 2.35f;
            Vector3 at = new Vector3(0f, height, 0f) + f * 0.12f;

            foreach (float y in new[] { -0.21f, 0.21f })
            {
                m.Box(Iron, new Vector3(0f, height + y, 0f) + f * 0.065f, new Vector3(0.06f, 0.045f, 0.11f), 0.01f, face);
                m.Tube(Iron, new Vector3(0f, height + y - 0.02f, 0f), new Vector3(0f, height + y + 0.02f, 0f), 0.09f, 12);
            }

            m.Box(SignCream, at, new Vector3(0.76f, 0.76f, 0.035f), 0.035f, face, 2);
            m.Box(SignBlue, at + f * 0.013f, new Vector3(0.66f, 0.66f, 0.02f), 0.024f, face, 2);

            // The "P", in the plate's front face as the reader sees it: x to their right, y up.
            Vector3 front = at + f * 0.026f;
            Vector3 Glyphed(float x, float y) => front + right * x + Vector3.up * y;

            m.Box(Glyph, Glyphed(-0.066f, 0f), new Vector3(0.088f, 0.45f, 0.018f), 0.009f, face);

            var bowl = new Vector2(-0.022f, 0.094f);
            const float mid = 0.0875f;
            const int pieces = 9;
            for (int k = 0; k < pieces; k++)
            {
                float a = (-90f + 180f * (k + 0.5f) / pieces) * Mathf.Deg2Rad;
                Vector3 tangent = right * -Mathf.Sin(a) + Vector3.up * Mathf.Cos(a);
                var piece = Quaternion.LookRotation(f, tangent);
                m.Box(Glyph, Glyphed(bowl.x + Mathf.Cos(a) * mid, bowl.y + Mathf.Sin(a) * mid),
                      new Vector3(0.088f, mid * Mathf.PI / pieces + 0.016f, 0.018f), 0.007f, piece);
            }
        }

        // --- The old surfaces and the drive outside the gate ----------------------------------------------

        /// <summary>
        /// What the car park is laid over and switches off: the old apron, the drive's elbow, and the
        /// drive itself - a square slab from the road to the lane, whose corner would poke out past
        /// the rounded kerb. Its stretch outside the gate is laid again on its own.
        /// </summary>
        static IEnumerable<Transform> OldSurfaces(Transform visitors)
        {
            yield return visitors.Find("Parking/Apron");
            yield return visitors.Find("Drive/DriveToApron");
            yield return visitors.Find("Drive/Drive");
        }

        const string DriveToGateName = "DriveToGate";

        /// <summary>The drive from the road up to just under the gate, where the car park's own surface takes over.</summary>
        static void LayDriveToGate(Transform carPark, Rect drive, float gateZ)
        {
            Rect outside = Rect.MinMaxRect(drive.xMin, drive.yMin, drive.xMax, gateZ + 0.3f);
            GameObject slab = SurfaceKit.Slab(carPark, DriveToGateName, outside, 0.045f, 0.04f, SurfaceKit.Asphalt());
            slab.transform.SetSiblingIndex(1);
        }

        /// <summary>
        /// Lays the car park's drive outside the gate again from the layout, when there is one: the
        /// world edge's tool calls this after it moves the road, as it re-lays the old drive.
        /// </summary>
        public static void RelayDriveToGate(Transform visitors)
        {
            Transform carPark = visitors != null ? visitors.Find("Parking/" + RootName) : null;
            Transform old = carPark != null ? carPark.Find(DriveToGateName) : null;
            if (old == null) return;

            Undo.DestroyObjectImmediate(old.gameObject);
            LayDriveToGate(carPark, ZoneLayout.Drive, PerimeterStripLayout.SouthLine);
            Undo.RegisterCreatedObjectUndo(carPark.Find(DriveToGateName).gameObject, "Lay drive");
        }

        // --- Plumbing ------------------------------------------------------------------------------------

        static GameObject Place(Transform parent, string name, Mesh mesh, Material[] materials, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>Saves a built mesh at <paramref name="path"/> - into the mesh already there, so the scene keeps pointing at it.</summary>
        static Mesh SaveMesh(Mesh built, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                AssetDatabase.SaveAssetIfDirty(built);
                return built;
            }

            existing.Clear();
            existing.name = built.name;
            existing.indexFormat = built.indexFormat;
            existing.SetVertices(built.vertices);
            existing.SetNormals(built.normals);
            existing.SetUVs(0, built.uv);
            existing.SetTangents(built.tangents);
            existing.subMeshCount = built.subMeshCount;
            for (int s = 0; s < built.subMeshCount; s++) existing.SetTriangles(built.GetTriangles(s), s);
            existing.RecalculateBounds();

            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        /// <summary>
        /// Triangles over a simple counter-clockwise polygon, by clipping ears: the paved outline is
        /// not convex - the drive, the lot and the grass corners between them - so a fan will not do.
        /// </summary>
        static List<int> Triangulate(List<Vector2> polygon)
        {
            var remaining = new List<int>();
            for (int i = 0; i < polygon.Count; i++) remaining.Add(i);
            var triangles = new List<int>();

            for (int guard = 0; remaining.Count > 3 && guard < polygon.Count * polygon.Count; guard++)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count && !clipped; i++)
                {
                    int ia = remaining[(i + remaining.Count - 1) % remaining.Count], ib = remaining[i], ic = remaining[(i + 1) % remaining.Count];
                    Vector2 a = polygon[ia], b = polygon[ib], c = polygon[ic];
                    if (Cross(a, b, c) <= 1e-7f) continue;

                    bool blocked = false;
                    foreach (int j in remaining)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (Cross(a, b, polygon[j]) >= 0f && Cross(b, c, polygon[j]) >= 0f && Cross(c, a, polygon[j]) >= 0f)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (blocked) continue;

                    triangles.Add(ia);
                    triangles.Add(ib);
                    triangles.Add(ic);
                    remaining.RemoveAt(i);
                    clipped = true;
                }

                if (!clipped) break;
            }

            if (remaining.Count == 3) triangles.AddRange(remaining);
            else if (remaining.Count > 3) Debug.LogWarning($"[Last Orders] The car park's outline left {remaining.Count} points untriangulated.");

            return triangles;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        /// <summary>The outline without repeated points or points on a straight line between their neighbours, counter-clockwise.</summary>
        static List<Vector2> Simplified(List<Vector2> outline)
        {
            var points = new List<Vector2>();
            foreach (Vector2 q in outline)
                if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], q) > 1e-3f) points.Add(q);
            if (points.Count > 1 && Vector2.Distance(points[0], points[points.Count - 1]) <= 1e-3f) points.RemoveAt(points.Count - 1);

            for (int i = 0; i < points.Count && points.Count > 3;)
            {
                Vector2 a = points[(i + points.Count - 1) % points.Count], b = points[i], c = points[(i + 1) % points.Count];
                if (Mathf.Abs(Cross(a, b, c)) < 1e-5f) points.RemoveAt(i);
                else i++;
            }

            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }

            if (area < 0f) points.Reverse();
            return points;
        }

        static float RowZ(Plan p, bool north)
        {
            float sum = 0f;
            int count = 0;
            foreach (ParkingSlot bay in p.Bays)
            {
                if (bay.transform.forward.z < 0f != north) continue;
                sum += bay.transform.position.z;
                count++;
            }

            return count > 0 ? sum / count : north ? p.Apron.yMax - p.BaySize.y * 0.5f : p.Apron.yMin + p.BaySize.y * 0.5f;
        }

        /// <summary>The world rectangle a mesh covers, from its mesh and transform: works on switched-off objects too.</summary>
        static bool WorldRect(Transform t, out Rect rect)
        {
            rect = default;
            var filter = t != null ? t.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) return false;

            Bounds b = filter.sharedMesh.bounds;
            Matrix4x4 m = t.localToWorldMatrix;
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? -1f : 1f, (i / 2) % 2 == 0 ? -1f : 1f, i / 4 == 0 ? -1f : 1f);
                Vector3 w = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, corner));
                x0 = Mathf.Min(x0, w.x);
                z0 = Mathf.Min(z0, w.z);
                x1 = Mathf.Max(x1, w.x);
                z1 = Mathf.Max(z1, w.z);
            }

            rect = Rect.MinMaxRect(x0, z0, x1, z1);
            return true;
        }

        static Rect BoundsOf(List<Vector2> points)
        {
            Vector2 min = points[0], max = points[0];
            foreach (Vector2 q in points)
            {
                min = Vector2.Min(min, q);
                max = Vector2.Max(max, q);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
