using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Pans and zooms the main camera by moving the CinemachineCamera itself.
///
/// The camera used to chase a dummy "CameraTarget" transform through a CinemachineFollow
/// component. That cost a GameObject whose only job was to be followed, split one camera's
/// tuning across two objects, and stacked this script's smoothing on top of Cinemachine's
/// damping so neither could be tuned on its own. A CinemachineCamera with no tracking target
/// simply reports its own transform, so this rig keeps a ground pivot, sits at a fixed offset
/// from it, and stays a first-class camera the brain can still blend to and from.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
[DisallowMultipleComponent]
public class CameraPanZoomRig : MonoBehaviour
{
    [Header("Framing")]
    [Tooltip("Where the camera sits relative to the ground point it frames. The direction " +
             "also sets the view angle: the camera always looks back down at the pivot.")]
    [SerializeField] Vector3 followOffset = new Vector3(-20f, 20f, -20f);

    [Tooltip("Ground point the camera frames when the scene loads.")]
    [SerializeField] Vector3 startPivot;

    [Tooltip("Height of the ground plane a drag grabs onto.")]
    [SerializeField] float groundHeight;

    [Header("Pan Bounds (world space, XZ)")]
    [Tooltip("Centre of the rectangle the pivot is allowed to move inside.")]
    [SerializeField] Vector2 boundsCenter;
    [Tooltip("Width (X) and depth (Z) of that rectangle. Leave at zero to pan freely.")]
    [SerializeField] Vector2 boundsSize;

    [Header("Pan")]
    [Tooltip("How sharply the camera catches up to the drag, per second. Zero sticks to the " +
             "finger exactly; higher values are snappier without being frame rate dependent.")]
    [SerializeField] float panSharpness = 20f;

    [Header("Zoom")]
    [Tooltip("Applied to the mouse wheel axis.")]
    [SerializeField] float scrollZoomSpeed = 0.4f;
    [Tooltip("Applied per pixel of pinch, so it needs a far smaller factor than the wheel.")]
    [SerializeField] float pinchZoomSpeed = 0.04f;
    [SerializeField] float minZoom = 10f;
    [SerializeField] float maxZoom = 23f;
    [SerializeField] float defaultZoom = 15f;

    /// <summary>
    /// Set while another system owns the pointer, e.g. dragging a shop item onto the grid.
    /// </summary>
    public static bool InputBlocked;

    CinemachineCamera vcam;
    Plane ground;
    Vector3 pivot;
    Vector3 targetPivot;
    Vector3 grabPoint;
    bool dragging;

    /// <summary>The ground point under the centre of the screen.</summary>
    public Vector3 Pivot
    {
        get => pivot;
        set
        {
            pivot = targetPivot = ClampToBounds(value);
            ApplyFraming();
        }
    }

    /// <summary>Glides the camera over to frame a ground point, at the pan's own pace.</summary>
    public void PanTo(Vector3 point) => targetPivot = ClampToBounds(point);

    void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
        ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));

        pivot = targetPivot = ClampToBounds(startPivot);
        SetZoom(defaultZoom);
        ApplyFraming();
    }

    void Update()
    {
        if (InputAvailable())
        {
            HandleZoom();
            HandlePan();
        }
        else
        {
            dragging = false;
        }

        // Exponential smoothing rather than Lerp(a, b, speed * deltaTime): the old form is a
        // different curve at every frame rate, so the camera felt heavier on a slow device.
        pivot = panSharpness > 0f
            ? Vector3.Lerp(pivot, targetPivot, 1f - Mathf.Exp(-panSharpness * Time.unscaledDeltaTime))
            : targetPivot;

        ApplyFraming();
    }

    // --- Input ------------------------------------------------------------------
    //
    // Legacy Input Manager, matching the rest of the project: the Input System package is
    // not installed, so the new API is simply not available here.

    bool InputAvailable()
    {
        if (InputBlocked)
            return false;

        if (UIGame.IsPointerOverUI())
            return false;

        if (BuildingGhost.isDragging)
            return false;

        if (GridBuildingSystem3D.Instance != null && GridBuildingSystem3D.Instance.IsDragging)
        {
            GameHandler.IsDragging = false;
            return false;
        }

        return true;
    }

    void HandlePan()
    {
        // Two fingers means pinch, not pan; panning at the same time makes the zoom feel like
        // it is sliding out from under the thumb.
        if (Input.touchCount == 2)
        {
            dragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            dragging = TryGroundPoint(Input.mousePosition, out grabPoint);
            return;
        }

        if (Input.GetMouseButtonUp(0))
            dragging = false;

        if (!dragging || !TryGroundPoint(Input.mousePosition, out Vector3 current))
            return;

        GameHandler.IsDragging = true;

        // Move the ground under the cursor rather than the camera: whatever was grabbed on
        // mouse down stays under the pointer.
        targetPivot = ClampToBounds(pivot + grabPoint - current);
    }

    void HandleZoom()
    {
        float delta;

        if (Input.touchCount == 2)
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);

            float previous = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
            float current = (a.position - b.position).magnitude;

            delta = (current - previous) * pinchZoomSpeed;
        }
        else
        {
            delta = Input.GetAxis("Mouse ScrollWheel") * scrollZoomSpeed;
        }

        if (!Mathf.Approximately(delta, 0f))
            SetZoom(CurrentZoom - delta);
    }

    /// <summary>
    /// The ground point under a screen position. A mathematical plane rather than a physics
    /// raycast: it needs no colliders and no layer mask, it cannot miss over a gap in the
    /// scenery, and dragging across a tall prop no longer jumps the world by its height.
    /// </summary>
    bool TryGroundPoint(Vector3 screenPoint, out Vector3 worldPoint)
    {
        worldPoint = default;

        Camera rendering = Camera.main;
        if (rendering == null)
            return false;

        Ray ray = rendering.ScreenPointToRay(screenPoint);
        if (!ground.Raycast(ray, out float distance))
            return false;

        worldPoint = ray.GetPoint(distance);
        return true;
    }

    // --- Framing -----------------------------------------------------------------

    void ApplyFraming()
    {
        if (followOffset.sqrMagnitude < 1e-4f)
            return;

        Vector3 position = pivot + followOffset;
        Quaternion rotation = Quaternion.LookRotation(-followOffset, Vector3.up);

        // Only write when it actually moved, so opening a scene does not mark it dirty.
        if ((transform.position - position).sqrMagnitude > 1e-8f
            || Quaternion.Angle(transform.rotation, rotation) > 1e-4f)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }

    bool HasBounds => boundsSize.x > 0f && boundsSize.y > 0f;

    Vector3 ClampToBounds(Vector3 point)
    {
        point.y = groundHeight;

        if (!HasBounds)
            return point;

        Vector2 extents = boundsSize * 0.5f;
        point.x = Mathf.Clamp(point.x, boundsCenter.x - extents.x, boundsCenter.x + extents.x);
        point.z = Mathf.Clamp(point.z, boundsCenter.y - extents.y, boundsCenter.y + extents.y);
        return point;
    }

    float CurrentZoom => vcam.Lens.Orthographic ? vcam.Lens.OrthographicSize : vcam.Lens.FieldOfView;

    void SetZoom(float value)
    {
        value = Mathf.Clamp(value, minZoom, maxZoom);

        if (vcam.Lens.Orthographic)
            vcam.Lens.OrthographicSize = value;
        else
            vcam.Lens.FieldOfView = value;
    }

    // --- Authoring ---------------------------------------------------------------

    /// <summary>
    /// Adopts whatever the camera is already framing, so dropping this onto an existing rig
    /// does not move the shot.
    /// </summary>
    void Reset()
    {
        startPivot = transform.position;

        if (TryGetComponent(out CinemachineFollow follow))
        {
            followOffset = follow.FollowOffset;
            startPivot = transform.position - followOffset;
        }

        groundHeight = startPivot.y;
        boundsCenter = new Vector2(startPivot.x, startPivot.z);
        boundsSize = new Vector2(100f, 100f);
    }

    void OnValidate()
    {
        boundsSize.x = Mathf.Max(0f, boundsSize.x);
        boundsSize.y = Mathf.Max(0f, boundsSize.y);
        maxZoom = Mathf.Max(maxZoom, minZoom);
        panSharpness = Mathf.Max(0f, panSharpness);

        if (!Application.isPlaying)
        {
            pivot = targetPivot = ClampToBounds(startPivot);
            ApplyFraming();
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 shown = Application.isPlaying ? pivot : ClampToBounds(startPivot);

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        if (HasBounds)
        {
            Gizmos.DrawWireCube(new Vector3(boundsCenter.x, groundHeight, boundsCenter.y),
                                new Vector3(boundsSize.x, 0f, boundsSize.y));
        }

        Gizmos.DrawLine(shown, shown + followOffset);
        Gizmos.DrawWireSphere(shown, 0.5f);
    }
}
