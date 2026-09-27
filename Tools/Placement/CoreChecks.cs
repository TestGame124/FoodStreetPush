using System;
using FoodStreet.Placement;
using UnityEngine;
class Program
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static int Main()
    {
        foreach (float size in new[] { 1f, .5f, .1f, .3f })
        {
            var grid = new FoodStreet.Placement.GridLayout(50, 40, size, new Vector3(-4.3f, 2f, 10.2f));
            for (int x = 0; x < grid.Width; x++)
                for (int z = 0; z < grid.Height; z++)
                {
                    var cell = new Vector2Int(x, z);
                    var world = grid.CellToWorld(cell);
                    Check(grid.WorldToCell(world) == cell, "Cell roundtrip failed: size=" + size + " x=" + x + " z=" + z);
                    Check(grid.WorldToCell(world + new Vector3(size * .49f, 0, size * .49f)) == cell, "Interior snap");
                }
            Check(grid.WorldToCell(grid.Origin - new Vector3(.01f, 0, .01f)) == new Vector2Int(-1, -1), "Negative floor");
            Check(!grid.Contains(new Vector2Int(-1, 0)) && !grid.Contains(new Vector2Int(50, 0)), "Bounds");
        }
        for (int w = 1; w <= 8; w++)
            for (int h = 1; h <= 8; h++)
                for (int turn = 0; turn < 4; turn++)
                {
                    var size = GridFootprint.Size(w, h, turn);
                    Check(size.x * size.y == w * h, "Rotation area");
                    Check(size == ((turn & 1) == 0 ? new Vector2Int(w, h) : new Vector2Int(h, w)), "Rotated extent");
                    var pivot = GridFootprint.PivotOffset(w, h, turn);
                    Check(pivot.x >= 0 && pivot.x <= size.x && pivot.y >= 0 && pivot.y <= size.y, "Pivot bounds");
                }
        var dense = new GridXZ<int>(7, 3, .5f, new Vector3(2, 4, -5), (g, x, z) => z * 7 + x);
        int events = 0;
        dense.OnGridObjectChanged += (s, e) => { Check(e.x == 3 && e.z == 2, "Change coordinates"); events++; };
        dense.SetGridObject(3, 2, 99);
        Check(dense.GetGridObject(3, 2) == 99 && dense.GetGridObject(2, 2) == 16, "Row-major storage");
        Check(dense.GetGridObject(-1, 0) == 0, "Safe out-of-bounds query");
        Check(events == 1, "Changed cells only");
        bool threw = false;
        try { new FoodStreet.Placement.GridLayout(0, 1, 1, Vector3.zero); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "Invalid grid rejected");
        Console.WriteLine(checks + " core geometry/storage assertions passed.");
        return 0;
    }
}
