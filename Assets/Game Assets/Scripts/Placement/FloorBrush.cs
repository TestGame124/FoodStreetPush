using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Placement
{
    /// <summary>Rectangle brush with pooled previews. Unchanged rectangles do no work.</summary>
    public sealed class FloorBrush
    {
        readonly PlacementService service;
        readonly List<Transform> previews = new List<Transform>();
        PlacedObjectTypeSO type;
        Vector2Int start;
        RectInt bounds;
        bool hasBounds;
        public bool IsDragging { get; private set; }
        public FloorBrush(PlacementService service) { this.service = service; }
        public void Begin(PlacedObjectTypeSO definition, Vector2Int cell)
        {
            Clear();
            if (!service.Layout.Contains(cell) || definition == null) return;
            type = definition; start = cell; IsDragging = true; Update(cell);
        }
        public void Update(Vector2Int cell)
        {
            if (!IsDragging) return;
            var end = new Vector2Int(Mathf.Clamp(cell.x, 0, service.Layout.Width - 1), Mathf.Clamp(cell.y, 0, service.Layout.Height - 1));
            var next = new RectInt(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), Mathf.Abs(start.x - end.x) + 1, Mathf.Abs(start.y - end.y) + 1);
            if (hasBounds && bounds == next) return;
            bounds = next; hasBounds = true;
            int count = bounds.width * bounds.height;
            while (previews.Count > count)
            {
                int last = previews.Count - 1;
                type.ReleaseVisual(previews[last]); previews.RemoveAt(last);
            }
            if (type.visual == null) return;
            while (previews.Count < count)
            {
                var visual = type.GetVisual();
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
                previews.Add(visual);
            }
            int index = 0;
            foreach (var position in bounds.allPositionsWithin)
                previews[index++].SetPositionAndRotation(service.Layout.CellToWorld(position), Quaternion.identity);
        }
        public void Commit()
        {
            if (!IsDragging || type == null) return;
            if (type.WidthInCells != 1 || type.HeightInCells != 1) { Clear(); return; }
            foreach (var cell in bounds.allPositionsWithin)
            {
                var old = service.GetAt(cell, true);
                bool erase = type.type == ObjectType.RemoveFloor;
                if (!erase && old != null && old.PlacedObjectTypeSO == type) continue;
                var solid = service.GetAt(cell);
                if (erase && solid != null && solid.PlacedObjectTypeSO.placementProfile != null && solid.PlacedObjectTypeSO.placementProfile.requiresFloor) continue;
                if (!erase && (type.prefab == null || type.prefab.GetComponent<PlacedObject_Done>() == null)) continue;
                if (!erase && service.Validate(type, cell, PlacedObjectTypeSO.Dir.Down, old) != PlacementFailure.None) continue;
                if (old != null) old.DestroySelf();
                if (!erase) service.TryPlace(type, cell, PlacedObjectTypeSO.Dir.Down, out _);
            }
            Clear();
        }
        public void Clear()
        {
            if (type != null) foreach (var visual in previews) if (visual != null) type.ReleaseVisual(visual);
            previews.Clear(); type = null; hasBounds = false; IsDragging = false;
        }
    }
}
