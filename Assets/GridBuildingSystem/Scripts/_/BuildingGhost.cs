
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingGhost : MonoBehaviour{

    private Transform visual;
    private Transform centerObject;

    public bool selectedState;
    private PlacedObjectTypeSO placedObjectTypeSO;

    public static Action<Vector3> OnObjectPicked;

    private UIGame uiGame => UIGame.GetUI();

    public ConfirmationUiPanel activeConfirmPanel;

    bool shopItemDroppedTutorial;

    public static bool isDragging;

    [SerializeField]private Vector3 popupOffset = new Vector3(0, 100, 0);
    private void Start() {
        RefreshVisual();

        GridBuildingSystem3D.Instance.OnSelectedChanged += Instance_OnSelectedChanged;
    }

    private void Instance_OnSelectedChanged(object sender, System.EventArgs e) {
        RefreshVisual();
    }
    
    private void LateUpdate() {


        if (Input.GetMouseButtonDown(0) && !UIGame.IsPointerOverUI())
        {
            if (activeConfirmPanel != null)
                activeConfirmPanel.gameObject.SetActive(false);
            //ChangePosAndRotation();

        }
        else if (Input.GetMouseButton(0) && !UIGame.IsPointerOverUI())
        {
            ChangePosAndRotation();
        }
        if (Input.GetMouseButtonUp(0))
        {


            OnObjectPicked?.Invoke(transform.position);

            if (!TutorialController.TutorialCompleted() && GridBuildingSystem3D.Instance.GetPlacedObjectTypeSO() != null)
            {
                if (!shopItemDroppedTutorial) {
                    shopItemDroppedTutorial = true;
                SnapToPosition(LevelTutorialBehaviour.instance.soupStationPos.transform);
                TutorialController.ObjectPlacingStageEvent?.Invoke();
                }
            }
            else
            {

                ChangePosAndRotation();
            }
            if (centerObject != null && activeConfirmPanel != null)
            {

                Vector3 objectPos = centerObject.position;
                activeConfirmPanel.transform.position = UIGame.GetScreenPostion(transform.position) + popupOffset;
                activeConfirmPanel.gameObject.SetActive(true);
            }

           

        }



    }

    private void ChangePosAndRotation()
    {
        Vector3 targetPosition = GridBuildingSystem3D.Instance.GetMouseWorldSnappedPosition();
        targetPosition.y = .4f;

        transform.SetPositionAndRotation(targetPosition, GridBuildingSystem3D.Instance.GetPlacedObjectRotation());
    }

    private void SnapToPosition(Transform snapToPos)
    {
        Vector3 targetPos = GridBuildingSystem3D.Instance.GetMouseWorldSnappedPosition(snapToPos);
        transform.SetPositionAndRotation(targetPos, GridBuildingSystem3D.Instance.GetPlacedObjectRotation());

    }

    private void RefreshVisual() {
        if (visual != null) { 
            Destroy(visual.gameObject);
            visual = null;
        }

        PlacedObjectTypeSO placedObjectTypeSO = GridBuildingSystem3D.Instance.GetPlacedObjectTypeSO();

        if (placedObjectTypeSO != null) {
            visual = Instantiate(placedObjectTypeSO.visual, Vector3.zero, Quaternion.identity);
            visual.parent = transform;
            visual.localPosition = Vector3.zero;
            visual.localEulerAngles = Vector3.zero;
            selectedState = true;
            centerObject = visual.GetComponentInChildren<BoxCollider>().transform;
            activeConfirmPanel = uiGame.confirmationPopup;

            SetLayerRecursive(visual.gameObject, 11);
        }
    }

    private void SetLayerRecursive(GameObject targetGameObject, int layer) {
        targetGameObject.layer = layer;
        foreach (Transform child in targetGameObject.transform) {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    public Vector3 selectedObjOldPos;
    public void SelectObject(PlacedObject_Done targetGameObject)
    {
        if (selectedState)
            return;

        selectedState = true;

                selectedObjOldPos = targetGameObject.transform.position;
        activeConfirmPanel = uiGame.placeObjectPanel;
      
        UIGame.GetUI().uiItemsWindow.ActiveState(false);

        GridBuildingSystem3D.Instance.selectedObject= targetGameObject;

        GridBuildingSystem3D.Instance.SetPlacedObjectTypeSO(targetGameObject.PlacedObjectTypeSO);
        //GridBuildingSystem3D.Instance.RotateObjectToAngle(targetGameObject.Dir);
        //GridBuildingSystem3D.Instance.SavePlacedObject(targetGameObject);
       

        targetGameObject.transform.SetParent(transform, false);
        targetGameObject.transform.localPosition = Vector3.zero;
        targetGameObject.transform.localEulerAngles = Vector3.zero;
        centerObject = targetGameObject.GetComponentInChildren<BoxCollider>().transform;
        //GridBuildingSystem3D.Instance.UpdatePlacedObject(targetGameObject);
        
    }

   
   
}

