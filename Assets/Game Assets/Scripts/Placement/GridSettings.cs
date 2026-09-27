using UnityEngine;

namespace FoodStreet.Placement
{
    [CreateAssetMenu(menuName = "Food Street/Placement/Grid Settings")]
    public sealed class GridSettings : ScriptableObject
    {
        [Min(1)] public int width = 50;
        [Min(1)] public int height = 50;
        [Min(0.01f)] public float cellSize = 1;
        public Vector3 origin;
        public Color validColor = new Color(0.2f, 1, 0.4f, 0.65f);
        public Color invalidColor = new Color(1, 0.15f, 0.15f, 0.65f);
    }
}
