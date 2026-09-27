using UnityEngine;

namespace FoodStreet.Placement
{
    /// <summary>Clockwise quarter turns with a lower-left grid anchor and a corner-based prefab pivot.</summary>
    public static class GridFootprint
    {
        public static Vector2Int Size(int width, int height, int quarterTurns) =>
            (quarterTurns & 1) == 0 ? new Vector2Int(width, height) : new Vector2Int(height, width);

        public static Vector2Int PivotOffset(int width, int height, int quarterTurns)
        {
            switch (quarterTurns & 3)
            {
                case 1: return new Vector2Int(0, width);
                case 2: return new Vector2Int(width, height);
                case 3: return new Vector2Int(height, 0);
                default: return Vector2Int.zero;
            }
        }
    }
}
