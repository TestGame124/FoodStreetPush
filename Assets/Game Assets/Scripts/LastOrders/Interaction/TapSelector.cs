using UnityEngine;
using UnityEngine.EventSystems;

namespace LastOrders.Interaction
{
    /// <summary>Something the player can tap. Section 4's object panel hangs off this.</summary>
    public interface ITappable
    {
        /// <summary>Shown when the object is tapped, so the panel can name what it is.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Handles the tap. Returns the message to show the player - 4 is explicit that a job
        /// which cannot start must say why, and that a tap is never silently accepted.
        /// </summary>
        string OnTapped();
    }

    /// <summary>
    /// Turns taps into interactions, per section 4.
    ///
    /// Two rules from that section are the whole reason this is a component rather than three
    /// lines in Update somewhere: selection happens on pointer RELEASE and only if motion
    /// stayed below the drag threshold, so panning the camera never also selects the ground;
    /// and UI consumes its own touches, so tapping Build never also selects terrain.
    /// </summary>
    [DefaultExecutionOrder(1160)]
    public sealed class TapSelector : MonoBehaviour
    {
        [Tooltip("4: begin pan after 12 logical pixels. Anything below that is still a tap.")]
        [SerializeField] float dragThresholdPixels = 12f;

        [SerializeField] float maxDistance = 400f;

        [Tooltip("Optional. Shown the message returned by the tapped object.")]
        [SerializeField] Blockout.CompoundHud hud;

        Camera cam;
        Vector2 pressPosition;
        bool pressed;
        bool pressStartedOverUi;

        void Start()
        {
            cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (hud == null) hud = FindFirstObjectByType<Blockout.CompoundHud>();

            if (cam == null)
                Debug.LogError("[TapSelector] No camera, so nothing can be tapped.", this);
        }

        void Update()
        {
            if (cam == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                pressed = true;
                pressPosition = Input.mousePosition;

                // Decided on press, not release: a drag that starts on the Build button and
                // ends over the world is still the button's gesture.
                pressStartedOverUi = IsPointerOverUi();
                return;
            }

            if (!Input.GetMouseButtonUp(0) || !pressed) return;

            pressed = false;
            if (pressStartedOverUi) return;

            float moved = Vector2.Distance(pressPosition, Input.mousePosition);
            if (moved > dragThresholdPixels) return;

            Resolve(Input.mousePosition);
        }

        void Resolve(Vector2 screenPoint)
        {
            Ray ray = cam.ScreenPointToRay(screenPoint);

            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                Report(null);
                return;
            }

            var tappable = hit.collider.GetComponentInParent<ITappable>();
            if (tappable == null)
            {
                Report(null);
                return;
            }

            Report(tappable.OnTapped());
        }

        /// <summary>
        /// 4: never accept a tap and leave the player guessing whether it worked. A tap on
        /// bare ground is not an error, so it clears the line rather than complaining.
        /// </summary>
        void Report(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (hud != null) hud.ShowMessage(message);
            else TapFeedback.Report(message);
        }

        static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
