using System.Collections.Generic;
using System.Text;
using LastOrders.Blockout;
using UnityEditor;
using UnityEngine;
using Footprint = LastOrders.EditorTools.BlockoutSchedule.Footprint;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Checks the open scene against BlockoutSchedule.
    ///
    /// Reads only the schedule, never a copied number, so this cannot agree with the builder
    /// about a coordinate that is wrong in both. What it is really guarding is the small set of
    /// conventions that are easy to get subtly wrong and hard to see: the corner-to-centre
    /// conversion, the rule that a facing goes on a child rather than on the scaled parent, and
    /// the fact that an editor-authored object has to survive a scene reload with its grid
    /// identity intact.
    /// </summary>
    public static class BlockoutValidator
    {
        const float Tolerance = 1e-3f;

        class Report
        {
            public readonly StringBuilder Text = new StringBuilder();
            public int Failures;
            public int Warnings;
            public int Checks;

            public void Pass(string what)
            {
                Checks++;
                Text.Append("  PASS  ").Append(what).Append('\n');
            }

            public void Fail(string what)
            {
                Checks++; Failures++;
                Text.Append("  FAIL  ").Append(what).Append('\n');
            }

            public void Warn(string what)
            {
                Warnings++;
                Text.Append("  WARN  ").Append(what).Append('\n');
            }

            public void Section(string name) => Text.Append('\n').Append(name).Append('\n');
        }

        [MenuItem("Last Orders/Validate Blockout Scene", priority = 4)]
        public static void Validate()
        {
            var report = new Report();

            IReadOnlyList<PlacedObject_Done> placed = CollectPlaced();

            CheckFenceCount(report);
            CheckFootprints(report);
            CheckOverlaps(report);
            CheckGridIdentity(report, placed);

            if (InitialiseGrid(report, placed))
            {
                CheckRouteClearance(report);
                CheckClassification(report);
            }

            string header =
                $"[Blockout] Validation: {report.Checks - report.Failures}/{report.Checks} checks passed" +
                (report.Failures > 0 ? $", {report.Failures} FAILED" : string.Empty) +
                (report.Warnings > 0 ? $", {report.Warnings} warnings" : string.Empty);

            string body = header + "\n" + report.Text;

            if (report.Failures > 0) Debug.LogError(body);
            else if (report.Warnings > 0) Debug.LogWarning(body);
            else Debug.Log(body);
        }

        // --- 1. Fence ----------------------------------------------------------------

        static void CheckFenceCount(Report r)
        {
            r.Section("Fence (16)");

            int scheduled = 0;
            foreach (BlockoutSchedule.FenceRun run in BlockoutSchedule.Fence) scheduled += run.SectionCount;

            if (scheduled == BlockoutSchedule.ExpectedFenceSections)
                r.Pass($"schedule sums to {scheduled} sections");
            else
                r.Fail($"schedule sums to {scheduled} sections, document says " +
                       $"{BlockoutSchedule.ExpectedFenceSections}");

            var sections = new List<Renderer>();
            foreach (GameObject root in RootObjects())
            {
                Transform fence = root.transform.Find("Compound/Fence");
                if (fence == null) continue;

                foreach (Transform runRoot in fence)
                {
                    if (runRoot.name == "CornerPosts") continue;

                    foreach (Transform section in runRoot)
                    {
                        var renderer = section.GetComponent<Renderer>();
                        if (renderer != null) sections.Add(renderer);
                    }
                }
            }

            if (sections.Count == BlockoutSchedule.ExpectedFenceSections)
                r.Pass($"scene contains {sections.Count} fence sections");
            else
                r.Fail($"scene contains {sections.Count} fence sections, expected " +
                       $"{BlockoutSchedule.ExpectedFenceSections}");

            CheckStructuralOverlaps(r);
        }

        /// <summary>
        /// 16: "Join corner posts without duplicate collision."
        ///
        /// Extended to the diner shell as well, because the same mistake is available there:
        /// a north wall run that spans the full footprint overlaps the east and west walls at
        /// both corners. Two colliders in one volume is invisible in the scene view and stays
        /// invisible until something queries it.
        /// </summary>
        static void CheckStructuralOverlaps(Report r)
        {
            var boxes = new List<Renderer>();

            foreach (GameObject root in RootObjects())
            {
                Collect(root.transform.Find("Compound/Fence"), boxes);
                Collect(root.transform.Find("Diner/Walls"), boxes);
            }

            int overlaps = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    if (OverlapArea(Flatten(boxes[i].bounds), Flatten(boxes[j].bounds)) <= 1e-4f) continue;

                    overlaps++;
                    if (overlaps <= 3)
                        r.Fail($"structures share volume: {boxes[i].name} and {boxes[j].name}");
                }
            }

            if (overlaps == 0)
                r.Pass($"no two of {boxes.Count} fence and diner-wall boxes share volume");
            else if (overlaps > 3)
                r.Fail($"...and {overlaps - 3} further structural overlaps");
        }

        static void Collect(Transform root, List<Renderer> into)
        {
            if (root == null) return;
            into.AddRange(root.GetComponentsInChildren<Renderer>(true));
        }

        // --- 2. Footprint fidelity -----------------------------------------------------

        static void CheckFootprints(Report r)
        {
            r.Section("Footprints (16, 17)");

            foreach (Footprint f in BlockoutSchedule.Outdoor) CheckOne(r, f);
            foreach (Footprint f in BlockoutSchedule.Interior) CheckOne(r, f);
            foreach (Footprint f in BlockoutSchedule.TableCores) CheckOne(r, f);
        }

        static void CheckOne(Report r, in Footprint f)
        {
            // R1 is a shell rather than a solid, so it has no single renderer to measure; its
            // walls are checked by the overlap and clearance passes instead.
            if (f.Id == "R1") return;

            Renderer renderer = FindById(f.Id);
            if (renderer == null)
            {
                r.Fail($"{f.Id} ({f.Label}) not found in the scene");
                return;
            }

            Rect actual = Flatten(renderer.bounds);
            Rect expected = BlockoutSchedule.RectOf(f);

            bool ok = Mathf.Abs(actual.xMin - expected.xMin) < Tolerance
                      && Mathf.Abs(actual.yMin - expected.yMin) < Tolerance
                      && Mathf.Abs(actual.width - expected.width) < Tolerance
                      && Mathf.Abs(actual.height - expected.height) < Tolerance;

            if (ok)
            {
                r.Pass($"{f.Id} at ({expected.xMin}, {expected.yMin}) size {expected.width} x {expected.height}");
                return;
            }

            r.Fail($"{f.Id} is at ({actual.xMin:0.###}, {actual.yMin:0.###}) size " +
                   $"{actual.width:0.###} x {actual.height:0.###}, schedule says " +
                   $"({expected.xMin}, {expected.yMin}) size {expected.width} x {expected.height}");
        }

        // --- 3. Mutual overlap -----------------------------------------------------------

        static void CheckOverlaps(Report r)
        {
            r.Section("Overlaps");

            var all = new List<Footprint>();
            all.AddRange(BlockoutSchedule.Outdoor);
            all.AddRange(BlockoutSchedule.Interior);
            all.AddRange(BlockoutSchedule.TableCores);

            int found = 0;
            for (int i = 0; i < all.Count; i++)
            {
                for (int j = i + 1; j < all.Count; j++)
                {
                    // The diner contains its own interior, and the table cores sit inside their
                    // envelopes. Neither is a conflict.
                    if (Contains(all[i], all[j]) || Contains(all[j], all[i])) continue;

                    float area = OverlapArea(BlockoutSchedule.RectOf(all[i]), BlockoutSchedule.RectOf(all[j]));
                    if (area <= 1e-4f) continue;

                    found++;
                    r.Fail($"{all[i].Id} and {all[j].Id} overlap by {area:0.###} m2");
                }
            }

            if (found == 0) r.Pass($"no overlaps among {all.Count} scheduled footprints");
        }

        static bool Contains(in Footprint outer, in Footprint inner)
        {
            Rect o = BlockoutSchedule.RectOf(outer);
            Rect i = BlockoutSchedule.RectOf(inner);

            return i.xMin >= o.xMin - Tolerance && i.xMax <= o.xMax + Tolerance
                   && i.yMin >= o.yMin - Tolerance && i.yMax <= o.yMax + Tolerance;
        }

        // --- 4. Grid identity ------------------------------------------------------------

        /// <summary>
        /// The reload check. PlacedObject_Done holds its type and origin in private fields; if
        /// those are not serialized they come back null after a scene load, the grid restores
        /// nothing, and navigation believes every tile is open ground - while the scene still
        /// looks completely correct. Everything else here would pass.
        /// </summary>
        static void CheckGridIdentity(Report r, IReadOnlyList<PlacedObject_Done> placed)
        {
            r.Section("Grid identity (survives reload)");

            if (placed.Count == 0)
            {
                r.Fail("no PlacedObject_Done components in the scene - nothing is on the grid");
                return;
            }

            int missing = 0;
            foreach (PlacedObject_Done p in placed)
                if (p.PlacedObjectTypeSO == null) missing++;

            if (missing == 0)
                r.Pass($"all {placed.Count} placed objects kept their type through load");
            else
                r.Fail($"{missing} of {placed.Count} placed objects have a null type. " +
                       "PlacedObject_Done.placedObjectTypeSO and origin must be [SerializeField] " +
                       "or authored scenes lose their grid registration on reload.");
        }

        // --- Grid + navigation -------------------------------------------------------------

        static bool InitialiseGrid(Report r, IReadOnlyList<PlacedObject_Done> placed)
        {
            r.Section("Grid");

            var system = Object.FindFirstObjectByType<GridBuildingSystem3D>();
            if (system == null)
            {
                r.Fail("no GridBuildingSystem3D in the scene");
                return false;
            }

            system.Initialize(BlockoutSchedule.GridWidth, BlockoutSchedule.GridHeight,
                              BlockoutSchedule.CellSize, Vector3.zero);

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();
            if (grid == null)
            {
                r.Fail("grid did not initialise");
                return false;
            }

            int registered = 0;
            foreach (PlacedObject_Done p in placed)
            {
                if (p.PlacedObjectTypeSO == null) continue;

                foreach (Vector2Int tile in p.GetGridPositionList())
                    grid.GetGridObject(tile.x, tile.y)?.SetPlacedObject(p);

                registered++;
            }

            r.Pass($"{BlockoutSchedule.GridWidth} x {BlockoutSchedule.GridHeight} grid at " +
                   $"{BlockoutSchedule.CellSize} m, {registered} objects registered");

            return true;
        }

        // --- 5. Route clearance ---------------------------------------------------------------

        static void CheckRouteClearance(Report r)
        {
            r.Section("Reserved routes (18)");

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();

            foreach (BlockoutSchedule.Strip s in BlockoutSchedule.Routes)
            {
                var blockers = new HashSet<string>();

                int x0 = BlockoutSchedule.ColumnOf(s.MinX);
                int x1 = BlockoutSchedule.ColumnOf(s.MaxX - Tolerance);
                int z0 = BlockoutSchedule.RowOf(s.MinZ);
                int z1 = BlockoutSchedule.RowOf(s.MaxZ - Tolerance);

                for (int x = x0; x <= x1; x++)
                {
                    for (int z = z0; z <= z1; z++)
                    {
                        // A cell counts as part of the route only if its CENTRE lies inside the
                        // reservation. Some route edges fall mid-cell - the kitchen opening ends
                        // at X 25.75, against the inner face of a 0.25 m wall on a 0.5 m grid -
                        // and counting a cell the route barely clips would report the diner's
                        // own east wall as obstructing the route it defines.
                        if (!CentreInside(s, x, z)) continue;

                        PlacedObject_Done occupant = grid.GetGridObject(x, z)?.GetPlacedObject();
                        if (occupant == null) continue;

                        PlacedObjectTypeSO type = occupant.PlacedObjectTypeSO;

                        // A door in a route is the route working, not an obstruction: the gate
                        // sits in the gate-to-porch reservation by design, and so does the
                        // diner's own doorway in the door throat.
                        if (type != null && type.type == ObjectType.Door) continue;

                        blockers.Add(occupant.name);
                    }
                }

                if (blockers.Count == 0)
                {
                    r.Pass($"{s.Id} clear across X {s.MinX}-{s.MaxX}, Z {s.MinZ}-{s.MaxZ}");
                    continue;
                }

                r.Fail($"{s.Id} is obstructed by: {string.Join(", ", blockers)}");
            }

            CheckKitchenCorridor(r);
        }

        /// <summary>
        /// The measurement behind the one real layout problem in the plan.
        ///
        /// T1 and T2 end at Z 21 and the counter begins at Z 22. That 1 m band is the only
        /// connection between the dining aisle and the kitchen opening, because the counter
        /// blocks X 15-23 and the inner east wall blocks X 25.75. If the 3 x 3 table envelopes
        /// are treated as collision it stays 1 m, against 18's own "main two-way routes remain
        /// at least 2 m" - on the most-travelled path in the building. Treating the envelope as
        /// reservation and only the 1 x 1 table as solid widens it to 2 m.
        /// </summary>
        static void CheckKitchenCorridor(Report r)
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();

            const float aisleMinX = 19f;
            const float aisleMaxX = 22f;

            float clear = 0f;
            float best = 0f;

            for (float z = 17.5f; z < 22f; z += BlockoutSchedule.CellSize)
            {
                bool rowClear = true;

                for (float x = aisleMinX; x < aisleMaxX; x += BlockoutSchedule.CellSize)
                {
                    PlacedObject_Done occupant = grid
                        .GetGridObject(BlockoutSchedule.ColumnOf(x), BlockoutSchedule.RowOf(z))
                        ?.GetPlacedObject();

                    if (occupant == null) continue;
                    if (occupant.PlacedObjectTypeSO?.type == ObjectType.Door) continue;

                    rowClear = false;
                    break;
                }

                clear = rowClear ? clear + BlockoutSchedule.CellSize : 0f;
                best = Mathf.Max(best, clear);
            }

            if (best >= 2f - Tolerance)
                r.Pass($"dining-to-kitchen corridor is {best:0.##} m (18 requires 2 m)");
            else
                r.Warn($"dining-to-kitchen corridor is only {best:0.##} m, against 18's 2 m " +
                       "minimum for main two-way routes. Check that the table envelopes are " +
                       "reservations rather than collision.");
        }

        // --- 6. Classification -----------------------------------------------------------------

        static void CheckClassification(Report r)
        {
            r.Section("Navigation classification");

            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();

            // Fence midpoints must read as wall, or there is no perimeter to breach.
            foreach (BlockoutSchedule.FenceRun run in BlockoutSchedule.Fence)
            {
                float mid = (run.From + run.To) * 0.5f;

                // Sampled through the schedule's own outward rule rather than by nudging off
                // the centreline, so this checks the rule rather than re-deriving it.
                int tile = BlockoutSchedule.FenceTile(run);

                int col = run.AlongX ? BlockoutSchedule.ColumnOf(mid) : tile;
                int row = run.AlongX ? tile : BlockoutSchedule.RowOf(mid);

                PlacedObject_Done occupant = grid.GetGridObject(col, row)?.GetPlacedObject();

                ObjectType? type = occupant?.PlacedObjectTypeSO?.type;

                if (type == ObjectType.Wall) r.Pass($"fence run {run.Id} reads as wall at its midpoint");
                else r.Fail($"fence run {run.Id} reads as {(type.HasValue ? type.ToString() : "open ground")} " +
                            "at its midpoint");
            }

            CheckDoorAt(r, "gate", BlockoutSchedule.ColumnOf(20f),
                        BlockoutSchedule.WallTileOutward(BlockoutSchedule.CompoundMinZ, true));

            CheckDoorAt(r, "diner doorway", BlockoutSchedule.ColumnOf(20f),
                        BlockoutSchedule.RowOf(17.1f));

            // Without an Inventory-typed tile the Pantry, Breach and WeakestPoint flow fields
            // have empty seed lists and every enemy stands still.
            bool pantry = false;
            foreach (PlacedObject_Done p in CollectPlaced())
                if (p.PlacedObjectTypeSO != null && p.PlacedObjectTypeSO.type == ObjectType.Inventory) pantry = true;

            if (pantry) r.Pass("an Inventory-typed tile exists to seed the attack flow fields");
            else r.Fail("no Inventory-typed tile: Pantry, Breach and WeakestPoint will have no seeds");

            CheckMarkersWalkable(r, grid);
        }

        static void CheckDoorAt(Report r, string label, int column, int row)
        {
            GridXZ<GridBuildingSystem3D.GridObject> grid = GridBuildingSystem3D.GetGrid();

            PlacedObject_Done occupant = grid.GetGridObject(column, row)?.GetPlacedObject();
            ObjectType? type = occupant?.PlacedObjectTypeSO?.type;

            if (type == ObjectType.Door) r.Pass($"{label} reads as a door at cell ({column}, {row})");
            else r.Fail($"{label} at cell ({column}, {row}) reads as " +
                        $"{(type.HasValue ? type.ToString() : "open ground")}, expected Door");
        }

        static void CheckMarkersWalkable(Report r, GridXZ<GridBuildingSystem3D.GridObject> grid)
        {
            var blocked = new List<string>();

            foreach (BlockoutMarker marker in Object.FindObjectsByType<BlockoutMarker>(FindObjectsSortMode.None))
            {
                if (marker.id == "GuardPostRange") continue;

                Vector3 p = marker.transform.position;
                PlacedObject_Done occupant = grid
                    .GetGridObject(BlockoutSchedule.ColumnOf(p.x), BlockoutSchedule.RowOf(p.z))
                    ?.GetPlacedObject();

                if (occupant == null) continue;
                if (occupant.PlacedObjectTypeSO?.type == ObjectType.Door) continue;

                blocked.Add($"{marker.id} (inside {occupant.name})");
            }

            if (blocked.Count == 0) r.Pass("every marker node stands on walkable ground");
            else r.Fail($"markers inside geometry: {string.Join(", ", blocked)}");
        }

        // --- Helpers ---------------------------------------------------------------------------

        static IReadOnlyList<PlacedObject_Done> CollectPlaced()
        {
            return Object.FindObjectsByType<PlacedObject_Done>(FindObjectsSortMode.None);
        }

        static IEnumerable<GameObject> RootObjects()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        }

        /// <summary>
        /// Finds a scheduled object by its id prefix, and measures the RESULT rather than the
        /// authored transform. A regression that transposed width and depth twice would cancel
        /// out in the transform values and still be wrong on screen; world-space renderer
        /// bounds catch it.
        /// </summary>
        static Renderer FindById(string id)
        {
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                string name = renderer.gameObject.name;
                if (name == id || name.StartsWith(id + "_")) return renderer;
            }

            return null;
        }

        static Rect Flatten(Bounds b) =>
            new Rect(b.min.x, b.min.z, b.size.x, b.size.z);

        /// <summary>True when the centre of grid cell (x, z) lies within the reservation.</summary>
        static bool CentreInside(in BlockoutSchedule.Strip s, int x, int z)
        {
            float cx = (x + 0.5f) * BlockoutSchedule.CellSize;
            float cz = (z + 0.5f) * BlockoutSchedule.CellSize;

            return cx >= s.MinX - Tolerance && cx < s.MaxX - Tolerance
                   && cz >= s.MinZ - Tolerance && cz < s.MaxZ - Tolerance;
        }

        static float OverlapArea(Rect a, Rect b)
        {
            float x = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float z = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);

            return x <= 0f || z <= 0f ? 0f : x * z;
        }
    }
}
