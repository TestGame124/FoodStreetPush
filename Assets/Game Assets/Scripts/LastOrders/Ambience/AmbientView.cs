using UnityEngine;

namespace LastOrders.Ambience
{
    /// <summary>
    /// Where the player is looking: the point of the ground at the middle of the screen, and roughly how
    /// far the view reaches round it. The wildlife gathers there, the drifting leaves and pollen follow
    /// it, and things only appear or vanish where it cannot see them.
    ///
    /// Worked out at most once a frame, from the main camera wherever the pan/zoom rig has put it.
    /// </summary>
    public static class AmbientView
    {
        static int frame = -1;
        static Vector3 focus;
        static float reach = 25f;
        static Camera camera;

        /// <summary>The ground point at the middle of the screen.</summary>
        public static Vector3 Focus
        {
            get
            {
                Refresh();
                return focus;
            }
        }

        /// <summary>About how far the visible ground reaches from <see cref="Focus"/>, metres.</summary>
        public static float Reach
        {
            get
            {
                Refresh();
                return reach;
            }
        }

        public static Camera Camera
        {
            get
            {
                Refresh();
                return camera;
            }
        }

        /// <summary>True if <paramref name="point"/> is on screen, or within <paramref name="margin"/>
        /// of its edge (a share of the screen: 0.1 is a tenth beyond each side).</summary>
        public static bool Sees(Vector3 point, float margin = 0.05f)
        {
            Refresh();
            if (camera == null) return false;

            Vector3 v = camera.WorldToViewportPoint(point);
            return v.z > 0f && v.x > -margin && v.x < 1f + margin && v.y > -margin && v.y < 1f + margin;
        }

        /// <summary>Where a ray from the camera through <paramref name="screenPoint"/> meets the ground.</summary>
        public static bool GroundUnder(Vector2 screenPoint, out Vector3 point)
        {
            Refresh();
            point = default;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(screenPoint);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;

            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f) return false;

            point = ray.GetPoint(t);
            return true;
        }

        static void Refresh()
        {
            if (frame == Time.frameCount && camera != null) return;
            frame = Time.frameCount;

            if (camera == null || !camera.isActiveAndEnabled) camera = Camera.main;
            if (camera == null) return;

            Transform t = camera.transform;
            Vector3 forward = t.forward;
            if (forward.y < -0.05f)
            {
                float along = -t.position.y / forward.y;
                focus = t.position + forward * along;

                // The view's half-height on the ground, near enough: the distance times the half-angle,
                // stretched by how slanted the ground is to the view.
                float half = camera.orthographic
                    ? camera.orthographicSize
                    : along * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                reach = Mathf.Clamp(half * Mathf.Max(camera.aspect, 1f) / Mathf.Max(0.3f, -forward.y), 8f, 120f);
            }
            else
            {
                focus = new Vector3(t.position.x, 0f, t.position.z);
            }

            focus.y = 0f;
        }
    }
}
