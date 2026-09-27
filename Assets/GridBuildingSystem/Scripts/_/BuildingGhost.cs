using System;
using UnityEngine;
using FoodStreet.Interaction;

/// <summary>Presentation only: follows the pointer and displays PlacementService validation.</summary>
public class BuildingGhost : MonoBehaviour
{
    Transform visual;
    Transform centerObject;
    Renderer[] renderers = Array.Empty<Renderer>();
    Renderer[] originalRenderers = Array.Empty<Renderer>();
    bool[] originalVisibility = Array.Empty<bool>();
    // Built on first use, never in a field initializer: Unity constructs MonoBehaviours
    // during deserialization, where the engine-side call behind this type is illegal and
    // throws before Awake ever runs.
    MaterialPropertyBlock tint;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    GridBuildingSystem3D system;
    public bool selectedState;
    public static Action<Vector3> OnObjectPicked;
    public ConfirmationUiPanel activeConfirmPanel;
    public static bool isDragging;
    public Vector3 selectedObjOldPos;
    [SerializeField] Vector3 popupOffset = new Vector3(0, 100, 0);

    void Start() => Bind();
    void Bind()
    {
        var current = GridBuildingSystem3D.Instance;
        if (system == current) return;
        if (system != null) system.OnSelectedChanged -= SelectedChanged;
        system = current;
        if (system != null) { system.OnSelectedChanged += SelectedChanged; RefreshVisual(); }
    }
    void SelectedChanged(object sender, EventArgs e) => RefreshVisual();
    void LateUpdate()
    {
        Bind();
        if (system == null || system.GetPlacedObjectTypeSO() == null || !selectedState) return;
        var position = system.GetMouseWorldSnappedPosition();
        transform.SetPositionAndRotation(position, system.GetPlacedObjectRotation());
        ApplyTint();
        if (Input.GetMouseButtonDown(0) && !PointerInput.OverUI && activeConfirmPanel != null)
            activeConfirmPanel.gameObject.SetActive(false);
        // PointerInput.Consumed: the tap that picked this object up arrives in Update, in this same
        // frame. Without the guard the release that selected the object would immediately read as
        // the release that drops it again, and the confirm panel would open on the pick-up tap.
        if (!Input.GetMouseButtonUp(0) || PointerInput.OverUI || PointerInput.Consumed) return;
        OnObjectPicked?.Invoke(transform.position);
        if (centerObject != null && activeConfirmPanel != null)
        {
            activeConfirmPanel.transform.position = UIGame.GetScreenPostion(transform.position) + popupOffset;
            activeConfirmPanel.gameObject.SetActive(true);
        }
    }
    void ApplyTint()
    {
        var settings = system.Settings;
        var color = system.CanBuild
            ? settings != null ? settings.validColor : new Color(.2f, 1, .4f, .65f)
            : settings != null ? settings.invalidColor : new Color(1, .15f, .15f, .65f);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            tint ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(tint);
            tint.SetColor(BaseColor, color); tint.SetColor(ColorId, color);
            renderer.SetPropertyBlock(tint);
        }
    }
    void RefreshVisual()
    {
        RestoreOriginalVisibility();
        if (visual != null) Destroy(visual.gameObject);
        visual = null; centerObject = null; renderers = Array.Empty<Renderer>();
        var type = system.GetPlacedObjectTypeSO();
        selectedState = type != null;
        if (type == null || type.visual == null) return;
        visual = Instantiate(type.visual, transform);
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        renderers = visual.GetComponentsInChildren<Renderer>();
        centerObject = visual;
        var ui = UIGame.GetUI();
        activeConfirmPanel = ui != null ? ui.confirmationPopup : null;
    }
    public void SelectObject(PlacedObject_Done target)
    {
        if (selectedState || target == null) return;
        Bind();
        if (system == null) return;
        selectedState = true;
        selectedObjOldPos = target.transform.position;
        system.selectedObject = target;
        system.SetPlacedObjectTypeSO(target.PlacedObjectTypeSO);
        system.RotateObjectToAngle(target.Dir);
        // Move a visual copy; keep the real object and its reservation untouched until commit.
        RefreshVisual();
        if (visual != null)
        {
            transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            originalRenderers = target.GetComponentsInChildren<Renderer>();
            originalVisibility = new bool[originalRenderers.Length];
            for (int i = 0; i < originalRenderers.Length; i++)
            {
                originalVisibility[i] = originalRenderers[i].enabled;
                originalRenderers[i].enabled = false;
            }
        }
        var ui = UIGame.GetUI();
        if (ui != null)
        {
            activeConfirmPanel = ui.placeObjectPanel;
            ui.uiItemsWindow.ActiveState(false);
        }
    }
    void OnDestroy()
    {
        RestoreOriginalVisibility();
        if (system != null) system.OnSelectedChanged -= SelectedChanged;
        if (visual != null) Destroy(visual.gameObject);
    }
    void RestoreOriginalVisibility()
    {
        for (int i = 0; i < originalRenderers.Length; i++)
            if (originalRenderers[i] != null) originalRenderers[i].enabled = originalVisibility[i];
        originalRenderers = Array.Empty<Renderer>(); originalVisibility = Array.Empty<bool>();
    }
}
