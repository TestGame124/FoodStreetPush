using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mouse3D : MonoBehaviour {

    public static Mouse3D Instance { get; private set; }

    [SerializeField] private LayerMask mouseColliderLayerMask = new LayerMask();
    [SerializeField] private LayerMask touchLayerMask = new LayerMask();

    private void Awake() {
        Instance = this;
    }

    private void Update() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, mouseColliderLayerMask)) {
            transform.position = raycastHit.point;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (UIGame.IsPointerOverUI())
                return;
            if (Physics.Raycast(ray, out RaycastHit raycastHit2, 999f, touchLayerMask))
            {
                HitNormal = raycastHit2.point;

                switch (GameHandler.currentState)
                {
                    case GameHandler.GameState.EditState:
                        if (raycastHit2.transform.TryGetComponent(out IInteractableObjectEditorState EditObject))
                        {
                            EditObject.OnTouch();
                        }
                        break;
                    case GameHandler.GameState.PlayState:

                        if (raycastHit2.transform.TryGetComponent(out IInteractableObject interactable))
                        {
                            interactable.OnTouch();
                        }
                        break;

                }
            }
        }
    }

    public static Vector3 GetMouseWorldPosition() => Instance.GetMouseWorldPosition_Instance();

    private Vector3 recentMousePos;
    private Vector3 GetMouseWorldPosition_Instance() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, mouseColliderLayerMask)) {
            recentMousePos= raycastHit.point;
            return raycastHit.point;
        } else {
            return recentMousePos;
        }
    }
    Vector3 HitNormal;
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(HitNormal, 0.3f);
    }
}
