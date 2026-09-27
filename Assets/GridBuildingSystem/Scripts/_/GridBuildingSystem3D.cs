using System;
using UnityEngine;
using FoodStreet.Placement;
using FoodStreet.Interaction;

/// <summary>Scene/shop compatibility facade. PlacementService owns rules and mutations.</summary>
public class GridBuildingSystem3D : MonoBehaviour
{
    public static GridBuildingSystem3D Instance { get; private set; }
    public event EventHandler OnSelectedChanged;
    public event EventHandler OnObjectPlaced;
    public delegate void OnObjectPlaceDelegate();
    public event OnObjectPlaceDelegate OnObjectPlacedEvent;
    [SerializeField] BuildingGhost ghostObject;
    [SerializeField] ObjectDatabaseHandler objectDataHandler;
    [SerializeField] GridSettings settings;
    [SerializeField] public GameObject gridVisualization;
    public BuildingGhost GhostObject => ghostObject;
    public GridSettings Settings => settings;
    public PlacementService Placement { get; private set; }
    public PlacedObject_Done selectedObject;
    public ShopItem CurrentShopItem { get; private set; }
    public bool CanBuild { get; private set; }
    public bool IsDragging => brush != null && brush.IsDragging;
    static GridXZ<GridObject> grid;
    FloorBrush brush;
    GridOverlay overlay;
    PlacedObjectTypeSO placedObjectTypeSO;
    PlacedObjectTypeSO.Dir dir;
    Vector3 recentMousePosition;

    public void Initialize() => Initialize(settings != null ? settings.width : 50, settings != null ? settings.height : 50,
        settings != null ? settings.cellSize : 1, settings != null ? settings.origin : Vector3.zero);

    public void Initialize(int gridWidth, int gridHeight, float cellSize, Vector3 origin)
    {
        var layout = new FoodStreet.Placement.GridLayout(gridWidth, gridHeight, cellSize, origin);
        DeselectObjectType();
        Instance = this;
        if (objectDataHandler != null) objectDataHandler.RegisterDatabases();
        grid = new GridXZ<GridObject>(layout.Width, layout.Height, layout.CellSize, layout.Origin, (g, x, z) => new GridObject(g, x, z));
        Placement = new PlacementService(grid);
        brush = new FloorBrush(Placement);
        overlay?.Dispose();
        if (Application.isPlaying && gridVisualization == null) overlay = new GridOverlay(layout);
    }

    /// <summary>Independent floor and furniture layers; navigation sees furniture first.</summary>
    public class GridObject
    {
        readonly GridXZ<GridObject> owner;
        readonly int x, z;
        public PlacedObject_Done Floor { get; private set; }
        public PlacedObject_Done Solid { get; private set; }
        public PlacedObject_Done placedObject => GetPlacedObject();
        public GridObject(GridXZ<GridObject> owner, int x, int z) { this.owner = owner; this.x = x; this.z = z; }
        public void SetPlacedObject(PlacedObject_Done value)
        {
            if (value == null) return;
            if (value.IsFloor()) { if (Floor == value) return; Floor = value; }
            else { if (Solid == value) return; Solid = value; }
            owner.TriggerGridObjectChanged(x, z);
        }
        public void Remove(PlacedObject_Done value)
        {
            bool changed = false;
            if (Floor != null && Floor == value) { Floor = null; changed = true; }
            if (Solid != null && Solid == value) { Solid = null; changed = true; }
            if (changed) owner.TriggerGridObjectChanged(x, z);
        }
        public void ClearPlacedObject() => Remove(GetPlacedObject());
        public PlacedObject_Done GetPlacedObject() => Solid != null ? Solid : Floor;
        public bool CanBuild() => Solid == null;
    }

    void Update()
    {
        if (Placement == null) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { DeselectObjectType(); return; }
        if (PointerInput.OverUI) return;
        if (Input.GetKeyDown(KeyCode.R)) RotateObject();
        if (placedObjectTypeSO == null) return;
        if (placedObjectTypeSO.categoryType == ObjectCategory.Floor)
        {
            if (Input.GetMouseButtonDown(0)) brush.Begin(placedObjectTypeSO, GetGridPosition(ReadPointer()));
            if (Input.GetMouseButton(0) && IsDragging) brush.Update(GetGridPosition(ReadPointer()));
        }
    }

    Vector3 ReadPointer()
    {
        // Keep the released placement anchored while the player reaches the confirmation controls.
        if (ghostObject != null && ghostObject.activeConfirmPanel != null &&
            ghostObject.activeConfirmPanel.gameObject.activeSelf && !Input.GetMouseButton(0)) return recentMousePosition;
        if (!PointerInput.OverUI)
        {
            var camera = Camera.main;
            if (camera != null && Placement != null)
            {
                var ray = camera.ScreenPointToRay(Input.mousePosition);
                var plane = new Plane(Vector3.up, Placement.Layout.Origin);
                if (plane.Raycast(ray, out float distance)) recentMousePosition = ray.GetPoint(distance);
            }
        }
        return recentMousePosition;
    }

    public bool PlaceObject(PlacedObject_Done selectedplacedObject = null)
    {
        if (Placement == null || placedObjectTypeSO == null || placedObjectTypeSO.categoryType == ObjectCategory.Floor) return false;
        var position = ReadPointer();
        var origin = Placement.Layout.WorldToCell(position);
        var target = selectedplacedObject != null ? selectedplacedObject : selectedObject;
        bool success;
        if (target != null) success = Placement.TryMove(target, origin, dir);
        else success = Placement.TryPlace(placedObjectTypeSO, origin, dir, out target);
        CanBuild = success;
        if (!success) return false;
        selectedObject = null;
        if (ghostObject != null) ghostObject.selectedState = false;
        OnObjectPlaced?.Invoke(this, EventArgs.Empty);
        OnObjectPlacedEvent?.Invoke();
        return true;
    }

    public void RemoveObject(Vector3 worldPos)
    {
        var target = selectedObject != null ? selectedObject : grid?.GetGridObject(worldPos)?.GetPlacedObject();
        if (target == null || Placement == null || !Placement.CanRemove(target)) return;
        target.DestroySelf();
        selectedObject = null;
    }
    public static void AddPlacedTiles(Vector2Int pos, PlacedObject_Done tile) => Instance?.Placement?.Register(tile);
    public static void RemovePlacedTiles(Vector2Int pos)
    {
        var floor = grid?.GetGridObject(pos.x, pos.y)?.Floor;
        if (floor != null) Instance?.Placement?.Unregister(floor);
    }
    public void EndDrag() => brush?.Commit();
    public void ClearPreviewTiles() => brush?.Clear();

    public void DeselectObjectType(bool drag = false)
    {
        if (selectedObject != null && Placement != null)
        {
            selectedObject.RestorePlacementPose(Placement.Layout.Pivot(selectedObject.PlacedObjectTypeSO, selectedObject.Origin, selectedObject.Dir));
            var ui = UIGame.GetUI();
            if (ui != null && ui.uiItemsWindow != null) ui.uiItemsWindow.ActiveState(true);
        }
        selectedObject = null;
        if (!drag) brush?.Clear();
        placedObjectTypeSO = null;
        CanBuild = false;
        overlay?.SetVisible(false);
        if (ghostObject != null)
        {
            ghostObject.selectedState = false;
            if (ghostObject.activeConfirmPanel != null) ghostObject.activeConfirmPanel.gameObject.SetActive(false);
            ghostObject.activeConfirmPanel = null;
        }
        OnSelectedChanged?.Invoke(this, EventArgs.Empty);
    }
    public Vector2Int GetGridPosition(Vector3 worldPosition) => Placement.Layout.WorldToCell(worldPosition);
    public Vector3 GetMouseWorldSnappedPosition(Transform snapPos = null)
    {
        var world = snapPos != null ? snapPos.position : ReadPointer();
        if (snapPos != null) recentMousePosition = world;
        if (placedObjectTypeSO == null || Placement == null) return world;
        var cell = Placement.Layout.WorldToCell(world);
        var ignored = placedObjectTypeSO.categoryType == ObjectCategory.Floor ? Placement.GetAt(cell, true) : selectedObject;
        CanBuild = placedObjectTypeSO.type == ObjectType.RemoveFloor
            ? Placement.CanRemove(ignored)
            : Placement.Validate(placedObjectTypeSO, cell, dir, ignored) == PlacementFailure.None;
        return Placement.Layout.Pivot(placedObjectTypeSO, cell, dir);
    }
    public Quaternion GetPlacedObjectRotation() => Quaternion.Euler(0, placedObjectTypeSO != null ? placedObjectTypeSO.GetRotationAngle(dir) : 0, 0);
    public PlacedObjectTypeSO GetPlacedObjectTypeSO() => placedObjectTypeSO;
    public void SetPlacedObjectTypeSO(PlacedObjectTypeSO type) { placedObjectTypeSO = type; overlay?.SetVisible(type != null); InitializePanels(); }
    public void RotateObject()
    {
        if (placedObjectTypeSO == null || placedObjectTypeSO.categoryType == ObjectCategory.Floor ||
            (placedObjectTypeSO.placementProfile != null && !placedObjectTypeSO.placementProfile.allowRotation)) return;
        dir = PlacedObjectTypeSO.GetNextDir(dir);
    }
    public void RotateObjectToAngle(PlacedObjectTypeSO.Dir direction) => dir = direction;
    public void SetPlacingObject(ObjectCategory category, ObjectType itemType)
    {
        DeselectObjectType();
        dir = PlacedObjectTypeSO.Dir.Down;
        placedObjectTypeSO = ObjectDatabaseHandler.GetPlacedObject(itemType);
        CurrentShopItem = ShopItemDataContainer.GetShopItem(itemType, category);
        overlay?.SetVisible(placedObjectTypeSO != null);
        InitializePanels();
        OnSelectedChanged?.Invoke(this, EventArgs.Empty);
    }
    void InitializePanels()
    {
        var ui = UIGame.GetUI();
        if (ui == null || placedObjectTypeSO == null) return;
        bool rotate = placedObjectTypeSO.categoryType != ObjectCategory.Floor &&
            (placedObjectTypeSO.placementProfile == null || placedObjectTypeSO.placementProfile.allowRotation);
        if (ui.placeObjectPanel != null) ui.placeObjectPanel.Initialize(this, rotate, placedObjectTypeSO.Sellable);
        if (ui.confirmationPopup != null) ui.confirmationPopup.Initialize(this, rotate);
    }
    public static GridXZ<GridObject> GetGrid() => grid;

    void OnDisable() { if (Instance == this) DeselectObjectType(); }
    void OnDestroy()
    {
        brush?.Clear();
        overlay?.Dispose();
        if (Instance == this) { Instance = null; grid = null; }
    }
}
