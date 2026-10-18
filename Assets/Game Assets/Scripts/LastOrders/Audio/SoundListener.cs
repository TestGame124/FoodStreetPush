using UnityEngine;

namespace LastOrders.Audio
{
    /// <summary>
    /// Hears the world from just above the ground the camera is looking at, rather than from the
    /// camera.
    ///
    /// The camera is a long lens nearly forty metres up. Heard from there, the grill under the
    /// middle of the screen and a zombie off its edge are nearly the same distance away, so 3D sound
    /// says nothing about where things are. Heard from the middle of the screen, what is in view is
    /// loud, what is off it fades, and panning is the screen's left and right.
    ///
    /// The ears rise as the camera pulls back - a fixed share of its distance - so zooming out
    /// softens the whole yard a little instead of keeping a close-up's level on everything.
    ///
    /// Takes over from the camera's own AudioListener while enabled and gives it back after, so
    /// removing this never leaves the scene deaf.
    /// </summary>
    [DefaultExecutionOrder(1300)] // After the camera rig has moved the camera this frame.
    [DisallowMultipleComponent]
    public sealed class SoundListener : MonoBehaviour
    {
        [Tooltip("The camera whose view is heard. Main Camera when left empty.")]
        [SerializeField] Camera view;

        [Tooltip("World height of the ground the camera looks at.")]
        [SerializeField] float groundHeight;

        [Tooltip("Share of the camera's distance from the ground the ears sit above it.")]
        [SerializeField, Range(0f, 1f)] float lift = 0.25f;

        AudioListener ears;
        AudioListener displaced;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Camera camera, float ground)
        {
            view = camera;
            groundHeight = ground;
        }

        void OnEnable()
        {
            if (view == null) view = Camera.main;

            // The camera's listener goes first: two enabled at once is a warning every frame.
            if (view != null && view.TryGetComponent(out AudioListener own) && own.enabled)
            {
                own.enabled = false;
                displaced = own;
            }

            if (!TryGetComponent(out ears)) ears = gameObject.AddComponent<AudioListener>();
            ears.enabled = true;

            Follow();
        }

        void OnDisable()
        {
            if (ears != null) ears.enabled = false;
            if (displaced != null) displaced.enabled = true;
            displaced = null;
        }

        void LateUpdate() => Follow();

        void Follow()
        {
            if (view == null)
            {
                view = Camera.main;
                if (view == null) return;
            }

            Transform eye = view.transform;
            Vector3 focus = Focus(eye.position, eye.forward, groundHeight);
            float distance = Vector3.Distance(eye.position, focus);

            transform.SetPositionAndRotation(focus + Vector3.up * (distance * lift),
                                             Quaternion.Euler(0f, eye.eulerAngles.y, 0f));
        }

        /// <summary>
        /// Where a camera at <paramref name="eye"/> looking along <paramref name="forward"/> meets
        /// the ground. Looking level or up it never does, and the point under the camera stands in.
        /// Pure, for the tests.
        /// </summary>
        public static Vector3 Focus(Vector3 eye, Vector3 forward, float ground)
        {
            if (forward.y > -0.05f) return new Vector3(eye.x, ground, eye.z);

            float along = (ground - eye.y) / forward.y;
            return eye + forward * along;
        }
    }
}
