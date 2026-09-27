using UnityEngine;

namespace LastOrders.Blockout
{
    /// <summary>
    /// The landscape base camera of section 18: orthographic, fixed 60 degree pitch, fixed yaw
    /// with south toward the screen bottom, panned and pinched within clamped bounds.
    ///
    /// A plain Camera rather than Cinemachine. The rig never blends, damps or aims, and it
    /// owns its own position and zoom outright, which a virtual camera would only contest.
    ///
    /// The two CM2-specific objections that originally settled this -- no CinemachineConfiner3D,
    /// and a CameraController.Focus path that read a composer unconditionally -- both went away
    /// with the move to Cinemachine 3.1.7, so routing this rig through Cinemachine is now a
    /// question of whether it wants blending, not of whether it can.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(50)]
    public sealed class BlockoutCameraRig : MonoBehaviour
    {
        [Header("Framing (18)")]
        [SerializeField] float pitch = 60f;
        [SerializeField] float yaw;

        [Tooltip("Metres of ground visible outside the world bounds, so the player can see " +
                 "the land they have not unlocked yet.")]
        [SerializeField] float border = 6f;

        [Tooltip("Distance the camera sits back along its view axis. Arbitrary under an " +
                 "orthographic projection; only large enough to keep the near plane clear.")]
        [SerializeField] float rigDistance = 60f;

        [Header("Bounds")]
        [Tooltip("The unlocked area, in world XZ. Shrinks to the compound if the design ever " +
                 "wants the camera held tighter before the first expansion.")]
        [SerializeField] Rect worldBounds = new Rect(0f, 0f, 48f, 40f);

        [Header("Zoom")]
        [SerializeField] float minOrthoSize = 8f;
        [SerializeField] float maxOrthoSize = 24f;
        [SerializeField] float zoomSpeed = 0.02f;

        [Header("Pan")]
        [SerializeField] float panSpeed = 1f;

        Camera cam;
        Vector3 pivot;
        Vector3 dragOrigin;
        bool dragging;

        /// <summary>The ground point under the centre of the screen.</summary>
        public Vector3 Pivot
        {
            get => pivot;
            set { pivot = value; ApplyClamp(); }
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;

            pivot = new Vector3(worldBounds.center.x, 0f, worldBounds.center.y);
            ApplyClamp();
        }

        void LateUpdate()
        {
            HandleZoom();
            HandlePan();
            ApplyClamp();
        }

        // --- Input ------------------------------------------------------------------
        //
        // Legacy Input Manager: the project has no Input System package installed
        // (activeInputHandler is 0), so the new API is simply not available here.

        void HandleZoom()
        {
            float delta = 0f;

            if (Input.touchCount == 2)
            {
                Touch a = Input.GetTouch(0);
                Touch b = Input.GetTouch(1);

                float previous = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
                float current = (a.position - b.position).magnitude;

                delta = (previous - current) * zoomSpeed;
            }
            else
            {
                delta = Input.mouseScrollDelta.y * -zoomSpeed * 20f;
            }

            if (Mathf.Approximately(delta, 0f)) return;

            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize + delta, minOrthoSize, maxOrthoSize);
        }

        void HandlePan()
        {
            // Two fingers means pinch, not pan; panning at the same time makes the zoom feel
            // like it is sliding out from under the thumb.
            if (Input.touchCount == 2)
            {
                dragging = false;
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                dragging = true;
                dragOrigin = Input.mousePosition;
                return;
            }

            if (Input.GetMouseButtonUp(0)) dragging = false;
            if (!dragging) return;

            Vector3 screenDelta = Input.mousePosition - dragOrigin;
            dragOrigin = Input.mousePosition;

            // Screen pixels to metres of ground. Vertical drag covers more ground than
            // horizontal at a pitched camera, by exactly the same 1/sin(pitch) that sets the
            // visible depth.
            float unitsPerPixel = cam.orthographicSize * 2f / Mathf.Max(Screen.height, 1);
            float sin = Mathf.Max(Mathf.Sin(pitch * Mathf.Deg2Rad), 1e-3f);

            Vector3 move = new Vector3(
                -screenDelta.x * unitsPerPixel * panSpeed,
                0f,
                -screenDelta.y * unitsPerPixel * panSpeed / sin);

            pivot += Quaternion.Euler(0f, yaw, 0f) * move;
        }

        // --- Framing -----------------------------------------------------------------

        void ApplyClamp()
        {
            if (cam == null) cam = GetComponent<Camera>();

            float halfWidth = cam.orthographicSize * Mathf.Max(cam.aspect, 0.1f);
            float sin = Mathf.Max(Mathf.Sin(pitch * Mathf.Deg2Rad), 1e-3f);
            float halfDepth = cam.orthographicSize / sin;

            float minX = worldBounds.xMin - border + halfWidth;
            float maxX = worldBounds.xMax + border - halfWidth;
            float minZ = worldBounds.yMin - border + halfDepth;
            float maxZ = worldBounds.yMax + border - halfDepth;

            // When the view is wider than the world plus its border the clamp inverts. Locking
            // to the centre there is the difference between a stable frame and one that snaps
            // between two impossible limits every frame.
            pivot.x = minX <= maxX ? Mathf.Clamp(pivot.x, minX, maxX) : worldBounds.center.x;
            pivot.z = minZ <= maxZ ? Mathf.Clamp(pivot.z, minZ, maxZ) : worldBounds.center.y;
            pivot.y = 0f;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = rotation;
            transform.position = pivot - rotation * Vector3.forward * rigDistance;
        }

        /// <summary>Called by the scene builder so the authored values match the schedule.</summary>
        public void Configure(float pitchDegrees, float yawDegrees, Rect bounds,
                              float viewBorder, float minSize, float maxSize)
        {
            pitch = pitchDegrees;
            yaw = yawDegrees;
            worldBounds = bounds;
            border = viewBorder;
            minOrthoSize = minSize;
            maxOrthoSize = maxSize;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(
                new Vector3(worldBounds.center.x, 0f, worldBounds.center.y),
                new Vector3(worldBounds.width, 0.1f, worldBounds.height));
        }
    }
}
