using System;
using UnityEngine;

namespace FoodStreet.Placement
{
    /// <summary>XZ geometry. Origins are minimum footprint corners, never prefab pivots.</summary>
    public readonly struct GridLayout
    {
        public readonly int Width, Height;
        public readonly float CellSize;
        public readonly Vector3 Origin;

        public GridLayout(int width, int height, float cellSize, Vector3 origin)
        {
            if (width <= 0 || height <= 0 || cellSize <= 0 || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Grid dimensions and cell size must be positive and finite.");
            Width = width; Height = height; CellSize = cellSize; Origin = origin;
        }

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        // Absorb float roundoff at authored cell boundaries (notably 0.1/0.3 cell sizes).
        const float BoundaryTolerance = 0.00001f;
        public Vector2Int WorldToCell(Vector3 world) => new Vector2Int(
            Mathf.FloorToInt((world.x - Origin.x) / CellSize + BoundaryTolerance),
            Mathf.FloorToInt((world.z - Origin.z) / CellSize + BoundaryTolerance));
        public Vector3 CellToWorld(Vector2Int cell) => Origin + new Vector3(cell.x, 0, cell.y) * CellSize;
        public Vector3 Pivot(PlacedObjectTypeSO type, Vector2Int cell, PlacedObjectTypeSO.Dir direction) =>
            CellToWorld(cell + type.GetRotationOffset(direction));
        public Vector2Int PivotToCell(PlacedObjectTypeSO type, Vector3 world, PlacedObjectTypeSO.Dir direction) =>
            WorldToCell(world) - type.GetRotationOffset(direction);
    }
}
