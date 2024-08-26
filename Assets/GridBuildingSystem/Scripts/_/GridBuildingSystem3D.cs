using System;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;
using System.Threading;
using BayatGames.SaveGameFree;
using BayatGames.SaveGameFree.Types;

public class GridBuildingSystem3D : MonoBehaviour {

    public static GridBuildingSystem3D Instance { get; private set; }

    public event EventHandler OnSelectedChanged;
    public event EventHandler OnObjectPlaced;

    public delegate void OnObjectPlaceDelegate();

    public event OnObjectPlaceDelegate OnObjectPlacedEvent;

    [SerializeField] private BuildingGhost ghostObject;
    public BuildingGhost GhostObject => ghostObject;

    private static GridXZ<GridObject> grid;
    //[SerializeField] private List<PlacedObjectTypeSO> placedObjectTypeSOList = null;

   [SerializeField] ObjectDatabaseHandler objectDataHandler;

   [SerializeField] public GameObject gridVisualization;

    public PlacedObject_Done selectedObject;

    private PlacedObjectTypeSO placedObjectTypeSO;
    private PlacedObjectTypeSO.Dir dir;
    private ShopItem currentShopItem;
    public ShopItem CurrentShopItem => currentShopItem;

    public bool CanBuild { get; private set; }
    public void Initialize() {
        Instance = this;

        objectDataHandler.RegisterDatabases();

        int gridWidth = 50;
        int gridHeight = 50;
        float cellSize = 1f;
        grid = new GridXZ<GridObject>(gridWidth, gridHeight, cellSize, new Vector3(0, 0, 0), (GridXZ<GridObject> g, int x, int y) => new GridObject(g, x, y));

        placedObjectTypeSO = null;// placedObjectTypeSOList[0];
    }

    public class GridObject {

        private GridXZ<GridObject> grid;
        private int x;
        private int y;
        public PlacedObject_Done placedObject;

        public GridObject(GridXZ<GridObject> grid, int x, int y) {
            this.grid = grid;
            this.x = x;
            this.y = y;
            placedObject = null;
        }

        //public override string ToString() {
        //    return x + ", " + y + "\n" + placedObject;
        //}

        public void SetPlacedObject(PlacedObject_Done placedObject) {
            this.placedObject = placedObject;
            grid.TriggerGridObjectChanged(x, y);
        }

        public void ClearPlacedObject() {
            placedObject = null;
            grid.TriggerGridObjectChanged(x, y);
        }

        public PlacedObject_Done GetPlacedObject() {
            return placedObject;
        }

        public bool CanBuild() {
            return placedObject == null || placedObject.shopItemType == ObjectType.Floor1;
        }

    }

    private void Update() {

        if (UIGame.IsPointerOverUI())
            return;
        
#if UNITY_EDITOR
        // For Rotation
        if (Input.GetKeyDown(KeyCode.R)) {
            dir = PlacedObjectTypeSO.GetNextDir(dir);
        }
        if (Input.GetKeyDown(KeyCode.Alpha0)) { DeselectObjectType(); }
        if (Input.GetMouseButtonDown(1))
        {
            Vector3 mousePosition = Mouse3D.GetMouseWorldPosition();
            RemoveObject(mousePosition);
        }
#endif
        if (placedObjectTypeSO != null)
        {
            if (placedObjectTypeSO.categoryType == ObjectCategory.Floor)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    StartDrag();
                }

                if (Input.GetMouseButton(0) && isDragging)
                {
                    ContinueDrag();
                }

            }
        }
        
    }

    public void RemoveObject(Vector3 worldPos)
    {
        Debug.Log($"Removed Obj Pos : {worldPos}");
        if (grid.GetGridObject(worldPos) != null)
        {

            // Valid Grid Position
            
                PlacedObject_Done placedObject = grid.GetGridObject(worldPos).GetPlacedObject();
            if (selectedObject != null)
                placedObject = selectedObject;

            if (placedObject != null)
            {
                // Demolish
                placedObject.DestroySelf();

                //if (dontClearPosFromGrid)
                //    return;
                List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
                foreach (Vector2Int gridPosition in gridPositionList)
                {
                    grid.GetGridObject(gridPosition.x, gridPosition.y).ClearPlacedObject();
                }
            }
            selectedObject = null;

        }
    }

    public bool PlaceObject(PlacedObject_Done selectedplacedObject = null)
    {

        if (placedObjectTypeSO.type == ObjectType.Floor1)
            return false;
        Vector3 mousePosition;

        if (!TutorialController.TutorialCompleted() && placedObjectTypeSO.type == ObjectType.SoupStove)
        {
            mousePosition = LevelTutorialBehaviour.instance.soupStationPos.transform.position;
        }
        else
        {
            if (UIGame.IsPointerOverUI())
            {
                mousePosition = recentMousePosition;
            }
            else
            {
                mousePosition = Mouse3D.GetMouseWorldPosition();
                recentMousePosition = mousePosition;
            }
        }

        grid.GetXZ(mousePosition, out int x, out int z);

        Vector2Int placedObjectOrigin = new Vector2Int(x, z);
        placedObjectOrigin = grid.ValidateGridPosition(placedObjectOrigin);

        // Test Can Build
        List<Vector2Int> gridPositionList = placedObjectTypeSO.GetGridPositionList(placedObjectOrigin, dir);
        bool canBuild = true;
        
        foreach (Vector2Int gridPosition in gridPositionList)
        {

            
            if (!grid.GetGridObject(gridPosition.x, gridPosition.y).CanBuild())
            {
                canBuild = false;
                break;
            }
        }

        if (canBuild)
        {
            Debug.Log("Can Build");
            Vector2Int rotationOffset = placedObjectTypeSO.GetRotationOffset(dir);
            Vector3 placedObjectWorldPosition = grid.GetWorldPosition(placedObjectOrigin.x, placedObjectOrigin.y) + new Vector3(rotationOffset.x, 0, rotationOffset.y) * grid.GetCellSize();
            PlacedObject_Done placedObject = selectedplacedObject;
            if (selectedplacedObject == null)
            {
                placedObject = PlacedObject_Done.Create(placedObjectWorldPosition, placedObjectOrigin, dir, placedObjectTypeSO);
                SavePlacedObject(placedObject);
                
            }
            else
            {
                Vector3 oldPos = placedObject.placedPosition;
                Debug.Log($"Old Pos : {oldPos}");
                placedObject.PlaceSelectedObject(placedObjectWorldPosition, placedObjectOrigin, dir);

                PlacedObjectData data = new PlacedObjectData
                {
                    Id = placedObject.ID,
                    objectType = placedObject.GetPlacedObjectTypeSO().type,
                    //position = new SerializableVector3(placedObject.transform.position),
                    position = new BayatGames.SaveGameFree.Types.Vector3Save(placedObject.transform.position),
                    direction = placedObject.GetDir()
                };

                SaveController.UpdatePlacedObject(/*SavePlacedObject(placedObject)*/data,oldPos);
                Debug.Log($"Updated Pos : {placedObjectWorldPosition}");
            }
            ghostObject.selectedState = false;

            foreach (Vector2Int gridPosition in gridPositionList)
                {
                    grid.GetGridObject(gridPosition.x, gridPosition.y).SetPlacedObject(placedObject);
                }


                OnObjectPlaced?.Invoke(this, EventArgs.Empty);
            //Debug.Log("")

            //DeselectObjectType();
            selectedObject = null;
        }
        else
        {
            // Cannot build here
            UtilsClass.CreateWorldTextPopup("Cannot Build Here!", mousePosition);
        }
        return canBuild;
    }

    #region Flooring 
    private Vector2Int dragStartPosition;
    private Vector2Int dragEndPosition;

    private bool isDragging = false;
    public bool IsDragging => isDragging;
    

    private static Dictionary<Vector2Int, PlacedObject_Done> placedTiles = new();

    public static void AddPlacedTiles(Vector2Int pos, PlacedObject_Done tile)
    {
        if(!placedTiles.ContainsKey(pos))
            placedTiles.Add(pos, tile);

    }public static void RemovePlacedTiles(Vector2Int pos)
    {
        if(placedTiles.ContainsKey(pos))
            placedTiles.Remove(pos);

    }
    private void StartDrag()
    {
        ClearPreviewTiles();

        Vector3 mousePosition = Mouse3D.GetMouseWorldPosition();
        grid.GetXZ(mousePosition, out int x, out int z);
        dragStartPosition = new Vector2Int(x, z);
        isDragging = true;

    }

    private void ContinueDrag()
    {
        Vector3 mousePosition = Mouse3D.GetMouseWorldPosition();
        grid.GetXZ(mousePosition, out int x, out int z);
        dragEndPosition = new Vector2Int(x, z);
        UpdatePreviewTiles(dragStartPosition, dragEndPosition);
        
    }

    public void EndDrag()
    {
        ClearPreviewTiles();
        bool removeFiles = placedObjectTypeSO.type == ObjectType.RemoveFloor;
        PlaceFloorArea(dragStartPosition, dragEndPosition, removeFiles);
        selectedObject = null;
        isDragging = false;

    }

    private List<Transform> previewTiles = new List<Transform>();
    private List<PlacedObjectTypeSO>  previewObjectData = new();
    private Queue<Transform> tilePool = new Queue<Transform>();

    //private void UpdatePreviewTiles(Vector2Int start, Vector2Int end)
    //{
    //    ClearPreviewTiles();

    //    int xMin = Mathf.Min(start.x, end.x);
    //    int xMax = Mathf.Max(start.x, end.x);
    //    int yMin = Mathf.Min(start.y, end.y);
    //    int yMax = Mathf.Max(start.y, end.y);

    //    for (int x = xMin; x <= xMax; x++)
    //    {
    //        for (int y = yMin; y <= yMax; y++)
    //        {
    //            Vector2Int gridPosition = new Vector2Int(x, y);
    //            //if (!placedTiles.ContainsKey(gridPosition))
    //            //{
    //                CreatePreviewTile(gridPosition);
    //            //}
    //        }
    //    }
    //}
    private void UpdatePreviewTiles(Vector2Int start, Vector2Int end)
    {
        ClearPreviewTiles();

        int xMin = Mathf.Min(start.x, end.x);
        int xMax = Mathf.Max(start.x, end.x);
        int yMin = Mathf.Min(start.y, end.y);
        int yMax = Mathf.Max(start.y, end.y);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                Vector2Int gridPosition = new Vector2Int(x, y);
                CreatePreviewTile(gridPosition);
            }
        }
    }

    private void CreatePreviewTile(Vector2Int gridPosition)
    {
        Vector3 worldPosition = grid.GetWorldPosition(gridPosition.x, gridPosition.y);
        
        Transform previewTile = placedObjectTypeSO.GetVisual();
        previewTile.position = worldPosition;
        previewTile.rotation = Quaternion.identity;

        previewTiles.Add(previewTile);
    }

   
    public void ClearPreviewTiles()
    {
        
        foreach (Transform previewTile in previewTiles)
        {
            
            placedObjectTypeSO.ReleaseVisual(previewTile);
        }
        previewTiles.Clear();
    }
    private void PlaceFloorArea(Vector2Int start, Vector2Int end, bool removeFloors = false)
    {
        int xMin = Mathf.Min(start.x, end.x);
        int xMax = Mathf.Max(start.x, end.x);
        int zMin = Mathf.Min(start.y, end.y);
        int zMax = Mathf.Max(start.y, end.y);

        List<PlacedObjectData> placedObjectsBatch = new List<PlacedObjectData>();
        List<PlacedObjectData> removedObjectsBatch = new List<PlacedObjectData>();
        for (int x = xMin; x <= xMax; x++)
        {
            for (int z = zMin; z <= zMax; z++)
            {
        //Debug.Log($"start Pos : {start}, End Pos : {end}");
                PlaceFloorAtPosition(x, z,placedObjectsBatch,removedObjectsBatch, removeFloors);

            }
        }

        SaveController.BatchSavePlacedObjects(placedObjectsBatch, removedObjectsBatch);

        if (removeFloors)
        {

            ClearPreviewTiles();
        }
    }

    private void PlaceFloorAtPosition(int x, int z, List<PlacedObjectData> placedObjectsBatch,List<PlacedObjectData> removedObjectsBatch, bool removeFloors = false)
    {
        //Vector2Int floorPosition = new Vector2Int(x, z);
        Vector2Int floorPosition = GetGridPosition(new Vector3(x, 0,z));
        //Vector2Int floorPosition = grid.GetXZ(new Vector3(), out int gX, out int gZ);
        ;
        //GridObject gridObject = grid.GetGridObject(floorPosition.x, floorPosition.y);

        if (placedTiles.ContainsKey(floorPosition))
        {
            removedObjectsBatch.Add(CreatePlacedObjectData(placedTiles[floorPosition], true));
            placedTiles[floorPosition].DestroySelf();
            placedTiles.Remove(floorPosition);
        }
        if (removeFloors)
        {
            return;
        }
        
        // Place new floor tile
        Vector3 placedFloorWorldPosition = grid.GetWorldPosition(floorPosition.x, floorPosition.y);
        PlacedObject_Done placedFloor = PlacedObject_Done.Create(placedFloorWorldPosition, floorPosition, PlacedObjectTypeSO.Dir.Down, placedObjectTypeSO);
        if (!placedTiles.ContainsKey(floorPosition))
        {
            placedTiles.Add(floorPosition, placedFloor);
        }


        placedObjectsBatch.Add(placedFloor.GetPlacedObjectData());


    }

    #endregion

    public void DeselectObjectType(bool drag = false) {
        ghostObject.selectedState = false;
        ghostObject.activeConfirmPanel = null;
        if (!drag)
        {
        isDragging = false;
        }
        placedObjectTypeSO = null; RefreshSelectedObjectType();

    }

    private void RefreshSelectedObjectType() {
        OnSelectedChanged?.Invoke(this, EventArgs.Empty);
    }


    public Vector2Int GetGridPosition(Vector3 worldPosition) {
        grid.GetXZ(worldPosition, out int x, out int z);
        return new Vector2Int(x, z);
    }

    Vector3 recentMousePosition;

    public Vector3 GetMouseWorldSnappedPosition(Transform snapPos = null) {
        Vector3 mousePosition = Vector3.zero;
        if (snapPos != null)
        {
            mousePosition = snapPos.position;
        }
        else
        {
            if (UIGame.IsPointerOverUI())
            {
                mousePosition = recentMousePosition;
            }
            else
            {
                mousePosition = Mouse3D.GetMouseWorldPosition();
                recentMousePosition = mousePosition;
            }
        }
        grid.GetXZ(mousePosition, out int x, out int z);
        if (placedObjectTypeSO != null) {
            Vector2Int rotationOffset = placedObjectTypeSO.GetRotationOffset(dir);
            Vector3 placedObjectWorldPosition = grid.GetWorldPosition(x, z) + new Vector3(rotationOffset.x, 0, rotationOffset.y) * grid.GetCellSize();

            bool canRotate = placedObjectTypeSO.categoryType != ObjectCategory.Floor;
           
                UIGame.GetUI().placeObjectPanel.Initialize(this, canRotate, placedObjectTypeSO.Sellable);
                UIGame.GetUI().confirmationPopup.Initialize(this,canRotate);


            return placedObjectWorldPosition;
        } else {
            return mousePosition;
        }
    }

    public Quaternion GetPlacedObjectRotation() {
        if (placedObjectTypeSO != null) {
            return Quaternion.Euler(0, placedObjectTypeSO.GetRotationAngle(dir), 0);
        } else {
            return Quaternion.identity;
        }
    }

    public PlacedObjectTypeSO GetPlacedObjectTypeSO() {
        return placedObjectTypeSO;
    }
    public void SetPlacedObjectTypeSO(PlacedObjectTypeSO POtypeSo) {
        placedObjectTypeSO = POtypeSo;
    }

    public void RotateObject()
    {
        dir = PlacedObjectTypeSO.GetNextDir(dir);
    }

    public void RotateObjectToAngle(PlacedObjectTypeSO.Dir dir)
    {
        this.dir = dir; 
    }
    
    public void SetPlacingObject(ObjectCategory type, ObjectType itemType)
    {

        //Debug.Log($"Placing Object Index : {index}");
        if (GetPlacedObjectTypeSO() != null)
        {
            
            if (GetPlacedObjectTypeSO().categoryType == ObjectCategory.Floor)
            {
                ClearPreviewTiles();
            }
            //DeselectObjectType(true);

            //gameObject.SetActive(false);
        }
        placedObjectTypeSO = ObjectDatabaseHandler.GetPlacedObject(itemType);
        currentShopItem = ShopItemDataContainer.GetShopItem(itemType, type);


        RefreshSelectedObjectType();
    }

    
    public static GridXZ<GridObject> GetGrid()
    {
        return grid;
    }

    #region Saving/Loading
    public bool isLoadingData;
public void LoadPlacedObjects()
    {
        if (GameHandler.FirstTime)
        {
            return;
        }
        if (!TutorialController.TutorialCompleted())
            return;
        isLoadingData = true;
        List<PlacedObjectData> placedObjectDataList = SaveController.LoadPlacedObjects();

        foreach (PlacedObjectData data in placedObjectDataList)
        {
            PlacedObjectTypeSO objectType = ObjectDatabaseHandler.GetPlacedObject(data.objectType);
            //SerializableVector3 worldPosition = data.position;
            BayatGames.SaveGameFree.Types.Vector3Save worldPosition = data.position;
            //Vector3 vectorPos = worldPosition.ToVector3();
            Vector3 vectorPos = new Vector3Save(worldPosition);
            Vector2Int gridPosition = GetGridPosition(vectorPos);

            List<Vector2Int> gridPositionList = objectType.GetGridPositionList(gridPosition, data.direction);
            bool objectExists = false;

            foreach (Vector2Int gridPos in gridPositionList)
            {
                GridObject gridObject = grid.GetGridObject(gridPos.x, gridPos.y);
                if (gridObject.GetPlacedObject() != null)
                {
                    objectExists = true;
                    
                    break;
                }
            }

            if (!objectExists)
            {

                
                PlacedObject_Done placedObject = LevelController.Instance.zone.GetAlreadyPlacedObject(data.Id);

                if(placedObject == null) 
                    placedObject = PlacedObject_Done.Create(vectorPos, gridPosition, data.direction, objectType);
                else
                {

                    foreach (Vector2Int placedGridPos in placedObject.GetGridPositionList())
                    {
                        grid.GetGridObject(placedGridPos.x, placedGridPos.y).ClearPlacedObject();
                    }

                        placedObject.PlaceSelectedObject(vectorPos, gridPosition, data.direction);
                    LevelController.Instance.zone.RemoveAlreadyPlacedObject(placedObject);
                }
                
                if (placedObject.GetPlacedObjectTypeSO().categoryType != ObjectCategory.Floor)
                {
                    foreach (Vector2Int gridPos in gridPositionList)
                    {
                        
                        grid.GetGridObject(gridPos.x, gridPos.y).SetPlacedObject(placedObject);
                    }
                }

            }
            else
            {
                PlacedObject_Done placedObject = LevelController.Instance.zone.GetAlreadyPlacedObject(data.Id);
                if (placedObject != null)
                    LevelController.Instance.zone.RemoveAlreadyPlacedObject(placedObject);
                ;
            }
        }
        isLoadingData = false;
    }
   
    public PlacedObjectData CreatePlacedObjectData(PlacedObject_Done placedObject, bool removeObject = false)
    {
        Vector3 objectPos = placedObject.transform.position;
        if (removeObject)
        {
            objectPos = placedObject.placedPosition;
        }

        PlacedObjectData data = new PlacedObjectData
        {
            Id = placedObject.ID,
            objectType = placedObject.GetPlacedObjectTypeSO().type,
            position = new BayatGames.SaveGameFree.Types.Vector3Save(objectPos),
            direction = placedObject.GetDir(),
            isRemoved = placedObject.isRemoved
        };

        return data;
    }

    public PlacedObjectData SavePlacedObject(PlacedObject_Done placedObject, bool removeObject = false)
    {
        Vector3 objectPos = placedObject.transform.position;
        if (removeObject)
        {
            Debug.Log("Remove Obj");
            objectPos = placedObject.placedPosition;
        }
        
        
        PlacedObjectData data = new PlacedObjectData
        {
            Id = placedObject.ID,
            objectType = placedObject.GetPlacedObjectTypeSO().type,
            //position = new SerializableVector3(objectPos),
            position = new BayatGames.SaveGameFree.Types.Vector3Save(objectPos),
            direction = placedObject.GetDir(),
            isRemoved = placedObject.isRemoved

        };
       
        // Run the save operation in a separate thread
        //Thread saveThread = null;

        if (!removeObject)
        {
            SaveController.SavePlacedObject(data);
            //saveThread = new Thread(() => SaveController.SavePlacedObject(data));
        }
        else
        {
            SaveController.RemoveObject(data, objectPos);
            //saveThread = new Thread(() => SaveController.RemoveObject(data, objectPos));

        }

        //saveThread.Start();

        return data;
    }
    
    
  



    #endregion
}
