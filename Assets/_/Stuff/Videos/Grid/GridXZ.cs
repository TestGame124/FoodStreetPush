using System;
using UnityEngine;

/// <summary>
/// Compatibility API for existing navigation and authored scenes.
/// Dense, row-major storage; no debug objects, cell polling or per-frame allocations.
/// </summary>
public class GridXZ<TGridObject>
{
    public event EventHandler<OnGridObjectChangedEventArgs> OnGridObjectChanged;
    public class OnGridObjectChangedEventArgs : EventArgs
    {
        public readonly int x, z;
        public OnGridObjectChangedEventArgs(int x, int z) { this.x = x; this.z = z; }
    }
    readonly FoodStreet.Placement.GridLayout layout;
    readonly TGridObject[] cells;
    // One immutable event payload per cell, reused for every notification.
    readonly OnGridObjectChangedEventArgs[] changes;
    public GridXZ(int width, int height, float cellSize, Vector3 originPosition,
        Func<GridXZ<TGridObject>, int, int, TGridObject> createGridObject)
    {
        layout = new FoodStreet.Placement.GridLayout(width, height, cellSize, originPosition);
        cells = new TGridObject[checked(width * height)];
        changes = new OnGridObjectChangedEventArgs[cells.Length];
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                int index = z * width + x;
                cells[index] = createGridObject(this, x, z);
                changes[index] = new OnGridObjectChangedEventArgs(x, z);
            }
    }
    public int GetWidth() => layout.Width;
    public int GetHeight() => layout.Height;
    public float GetCellSize() => layout.CellSize;
    public Vector3 GetOriginPosition() => layout.Origin;
    public Vector3 GetWorldPosition(int x, int z) => layout.CellToWorld(new Vector2Int(x, z));
    public void GetXZ(Vector3 worldPosition, out int x, out int z)
    {
        var cell = layout.WorldToCell(worldPosition); x = cell.x; z = cell.y;
    }
    public void SetGridObject(int x, int z, TGridObject value)
    {
        if (!layout.Contains(new Vector2Int(x, z))) return;
        cells[z * layout.Width + x] = value; TriggerGridObjectChanged(x, z);
    }
    public void TriggerGridObjectChanged(int x, int z)
    {
        if (layout.Contains(new Vector2Int(x, z))) OnGridObjectChanged?.Invoke(this, changes[z * layout.Width + x]);
    }
    public void SetGridObject(Vector3 position, TGridObject value)
    {
        GetXZ(position, out int x, out int z); SetGridObject(x, z, value);
    }
    public TGridObject GetGridObject(int x, int z) =>
        layout.Contains(new Vector2Int(x, z)) ? cells[z * layout.Width + x] : default;
    public TGridObject GetGridObject(Vector3 position)
    {
        GetXZ(position, out int x, out int z); return GetGridObject(x, z);
    }
    public Vector2Int ValidateGridPosition(Vector2Int position) => new Vector2Int(
        Mathf.Clamp(position.x, 0, layout.Width - 1), Mathf.Clamp(position.y, 0, layout.Height - 1));
}
