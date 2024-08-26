using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;
using Cinemachine;

public class CameraTarget : MonoBehaviour {

   
    private Vector3 dragStartPos;
    private Vector3 dragCurrentPos;
    private Vector3 newPosition;
    private Vector3 newZoom;

    public float zoomSpeed = 10f;
    public float minZoom = 10f;
    public float maxZoom = 100f;
    public float DefaultZoom = 20f;

    [SerializeField] BoxCollider collider;

    [SerializeField] float movementTime;

    CinemachineVirtualCamera mainCamera;

    public static bool stopDrag;

    public LayerMask cameraLayer;
    private void Start()
    {
        mainCamera = CameraController.GetCamera(CameraController.CameraType.Main);
        newPosition = transform.position;
        mainCamera.m_Lens.FieldOfView = DefaultZoom;
        //newZoom = CameraController.mainCamera.transform.localPosition;
    }
    private void Update()
    {
        if (UIGame.IsPointerOverUI())
            return;

        if (BuildingGhost.isDragging)
            return;
        HandleMouseInput();
        HandleZoom();
        ClampPositionWithinBounds();

        transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * movementTime);
        
    }

    void HandleMouseInput()
    {
        if (stopDrag) return;
        if (GridBuildingSystem3D.Instance.IsDragging)
        { 
            GameHandler.IsDragging = false;
            return; 
        }

        
        if(Input.GetMouseButtonDown(0))
        {
           

            dragStartPos = GetMouseWorldPosition();
        }

        if (Input.GetMouseButton(0))
        {
            if (dragStartPos == Vector3.zero)
                return;
            GameHandler.IsDragging = true;
            dragCurrentPos= GetMouseWorldPosition();
            newPosition = transform.position + dragStartPos - dragCurrentPos; 
        }
    }
        private Vector3 recentMousePos;
    private Vector3 GetMouseWorldPosition()
    {
   
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, cameraLayer))
        {
            recentMousePos = raycastHit.point;
            return raycastHit.point;
        }
        else
        {
            return recentMousePos;
        }
    }
    Vector3 HitNormal;

    void HandleZoom()
    {
        // Handle desktop zoom with mouse scroll
        float scrollData;
#if UNITY_EDITOR
        scrollData = Input.GetAxis("Mouse ScrollWheel");
#elif UNITY_IOS || UNITY_ANDROID
        // Handle mobile zoom with pinch gesture
        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            scrollData = touchDeltaMag - prevTouchDeltaMag;
            stopDrag = true;
        }
        else
        {
            stopDrag = false;

            scrollData = 0;
        }
#endif

        if (scrollData != 0)
        {
            // Check if the camera is orthographic or perspective
            if (mainCamera.m_Lens.Orthographic)
            {
                float newSize = mainCamera.m_Lens.OrthographicSize - scrollData * zoomSpeed;
                mainCamera.m_Lens.OrthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
            }
            else
            {
                float newFOV = mainCamera.m_Lens.FieldOfView - scrollData * zoomSpeed;
                mainCamera.m_Lens.FieldOfView = Mathf.Clamp(newFOV, minZoom, maxZoom);
            }
        }
    }

    void ClampPositionWithinBounds()
    {
        newPosition.x = Mathf.Clamp(newPosition.x, collider.bounds.min.x, collider.bounds.max.x);
        newPosition.z = Mathf.Clamp(newPosition.z, collider.bounds.min.z, collider.bounds.max.z);
    }
}
