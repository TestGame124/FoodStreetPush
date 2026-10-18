using FoodStreet.Interaction;
using LastOrders.Audio;
using LastOrders.Core;
using LastOrders.Effects;
using LastOrders.Interaction;
using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// The player's hand on a crew member. Hold a finger on them and they are picked up - lifted off
    /// their feet, swinging a little, a ring on the floor under them - and carried wherever the finger
    /// goes. Let go over a kitchen station and they land at it and start helping (it cooks faster);
    /// let go anywhere else in the diner and they land there, back to serving and tidying. The ring
    /// lights up teal over a station, so it is plain where a drop will do something.
    ///
    /// A quick tap instead says what they are up to - and, while they are helping in the kitchen,
    /// sends them back to the floor.
    ///
    /// Being carried is a duty of its own, the most important there is: whatever they were doing is
    /// dropped the moment they leave the ground.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CrewBrain))]
    public sealed class CrewHandle : MonoBehaviour, ICrewDuty, IInteractableObject, ITappable
    {
        [SerializeField] int priority = 100;

        [Tooltip("Seconds a finger rests on them before they are picked up.")]
        [SerializeField] float holdSeconds = 0.3f;

        [Tooltip("How far a finger may wander while it rests, as a share of the screen's shorter side.")]
        [SerializeField, Range(0f, 0.1f)] float holdSlop = 0.025f;

        [Tooltip("How high they hang off the ground while carried.")]
        [SerializeField] float liftHeight = 0.6f;

        [Tooltip("Seconds for the carried body to catch up with the finger.")]
        [SerializeField] float follow = 0.07f;

        CrewBrain brain;
        StationAssist assist;
        Camera eye;
        Transform model;
        Vector3 modelScale = Vector3.one;

        bool pressed, held;
        Vector3 pickedFrom, velocity, ground;
        CookeryStation over;
        float clock, landing = -1f;

        Transform marker;
        MeshFilter markerMesh;

        public int Priority => priority;
        public bool HasWork => held;
        public string Activity => held ? "Being moved" : "";
        public string DisplayName => brain != null ? brain.DisplayName : name;

        /// <summary>Up in the player's hand right now.</summary>
        public bool IsHeld => held;

        void Awake()
        {
            brain = GetComponent<CrewBrain>();
            assist = GetComponent<StationAssist>();

            Animator animator = GetComponentInChildren<Animator>();
            model = animator != null ? animator.transform : null;
            if (model != null) modelScale = model.localScale;
        }

        void OnDisable()
        {
            if (held) Drop(cancelled: true);
            pressed = false;
        }

        void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }

        // --- The finger ---------------------------------------------------------------------------------

        void Update()
        {
            Squash();

            if (!held)
            {
                WatchPress();
                return;
            }

            if (TryGround(out Vector3 point)) ground = point;
            Hover(StationUnderPointer());
            if (PointerInput.Released || !PointerInput.Held || PointerInput.MultiTouch)
            {
                PointerInput.Consume();
                PutDown();
            }
        }

        void WatchPress()
        {
            if (PointerInput.Pressed)
                pressed = !PointerInput.OverUI && !PointerInput.MultiTouch && UnderPointer();

            if (!pressed) return;

            if (!PointerInput.Held || PointerInput.GestureUsedMultiTouch || PointerInput.Consumed)
            {
                pressed = false;
                return;
            }

            // Moved off before the hold: that was a camera pan.
            if (PointerInput.DragPixels > Mathf.Min(Screen.width, Screen.height) * holdSlop)
            {
                pressed = false;
                return;
            }

            if (PointerInput.HoldSeconds >= holdSeconds) PickUp();
        }

        // --- Picked up, carried, put down ---------------------------------------------------------------
        //
        // What the finger does, as steps of their own: the pointer only feeds them.

        /// <summary>Up off their feet: whatever they were doing is dropped, and the camera holds still.</summary>
        public void PickUp()
        {
            if (held) return;

            pressed = false;
            held = true;
            pickedFrom = ground = transform.position;
            SoundController.Play(SoundId.CrewPickUp, transform.position);
            velocity = Vector3.zero;
            clock = 0f;
            over = null;

            CameraPanZoomRig.InputBlocked = true;
            ShowMarker(true);
        }

        /// <summary>Carried over <paramref name="floorPoint"/>, with <paramref name="station"/> under the finger (or none).</summary>
        public void CarryOver(Vector3 floorPoint, CookeryStation station)
        {
            if (!held) return;

            ground = floorPoint;
            Hover(station);
        }

        /// <summary>Let go: onto the station they are over, else onto the floor there, else back where they were.</summary>
        public void PutDown()
        {
            if (held) Drop(cancelled: false);
        }

        void Hover(CookeryStation station)
        {
            clock += Time.deltaTime;
            over = station != null && station.isActiveAndEnabled ? station : null;

            Vector3 target = ground + Vector3.up * liftHeight;
            transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, follow, Mathf.Infinity, Time.deltaTime);

            // Dangling: a sway that leans the way they are dragged, and a slow turn to the camera.
            Vector3 drag = velocity;
            drag.y = 0f;
            Vector3 toCamera = Eye != null ? Eye.transform.position - transform.position : -transform.forward;
            toCamera.y = 0f;
            Quaternion facing = toCamera.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toCamera) : transform.rotation;
            float swing = Mathf.Sin(clock * 7f) * 7f;
            Vector3 lean = Vector3.ClampMagnitude(drag * 4f, 20f);
            transform.rotation = Quaternion.Slerp(transform.rotation, facing, Time.deltaTime * 6f) *
                                 Quaternion.Euler(lean.z, 0f, -lean.x + swing);

            // The ring: under them on the floor, or at the station's working spot when over one.
            if (marker != null)
            {
                Vector3 at = ground;
                bool good = over != null;
                if (good) at = FixtureChores.StationSpot(over, 0.45f, out _).position;

                markerMesh.sharedMesh = CrewMeshes.Ring(good);
                marker.position = at + Vector3.up * 0.06f;
                float pulse = 1f + 0.08f * Mathf.Sin(clock * 9f);
                marker.localScale = Vector3.one * (good ? 1.25f * pulse : 1f);
            }
        }

        void Drop(bool cancelled)
        {
            held = false;
            CameraPanZoomRig.InputBlocked = false;
            ShowMarker(false);

            CookeryStation station = cancelled ? null : over;
            Vector3 land;
            if (station != null)
            {
                land = FixtureChores.StationSpot(station, 0.45f, out _).position;
            }
            else if (!cancelled && DinerPlaces.Inside(ground, 0.4f) && ChoreBoard.IsOpen(ground, 0.25f))
            {
                land = ground;
            }
            else
            {
                land = pickedFrom;
            }

            land.y = pickedFrom.y;

            Vector3 facing = transform.forward;
            facing.y = 0f;
            transform.rotation = Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing : Vector3.forward);
            brain.Legs.Land(land);
            landing = 0f;
            PlacementJuice.Burst(land, 0.7f);
            SoundController.Play(SoundId.CrewDrop, land);

            if (cancelled || assist == null) return;

            if (station != null)
            {
                assist.AssignTo(station);
                Say(transform.position + Vector3.up * 2.2f, $"+{Mathf.RoundToInt(assist.Boost * 100f)}% cooking speed");
                TapFeedback.Report($"{DisplayName} is helping at the {FixtureChores.StationName(station)}: it cooks {Mathf.RoundToInt(assist.Boost * 100f)}% faster. Tap {brain.Them} to send {brain.Them} back to the tables.");
            }
            else if (assist.Station != null)
            {
                assist.Release();
                TapFeedback.Report($"{DisplayName} is back on the floor, serving and tidying up.");
            }
        }

        // --- The duty -----------------------------------------------------------------------------------

        public void SetActive(bool active)
        {
            if (active)
            {
                brain.Legs.Lift();
                brain.Gestures.Play(Gesture.Wave);
            }
        }

        public void Tick(float deltaTime)
        {
            // Carry() does the moving, from the finger, in Update.
        }

        // --- The tap ------------------------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (assist != null && assist.Station != null)
            {
                assist.Release();
                return $"{DisplayName} is back on the floor, serving and tidying up.";
            }

            int boost = assist != null ? Mathf.RoundToInt(assist.Boost * 100f) : 0;
            string help = assist != null
                ? $" Hold {brain.Them} and drag {brain.Them} onto a kitchen station to make it cook {boost}% faster."
                : "";
            return $"{DisplayName}: {brain.Activity}.{help}";
        }

        // --- Helpers ------------------------------------------------------------------------------------

        Camera Eye => eye != null ? eye : eye = Camera.main;

        bool UnderPointer()
        {
            Camera camera = Eye;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(PointerInput.Position);
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide))
                if (hit.collider != null && hit.collider.transform.IsChildOf(transform)) return true;

            return false;
        }

        bool TryGround(out Vector3 point)
        {
            point = default;
            Camera camera = Eye;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(PointerInput.Position);
            var floor = new Plane(Vector3.up, new Vector3(0f, pickedFrom.y, 0f));
            if (!floor.Raycast(ray, out float distance)) return false;

            point = ray.GetPoint(distance);
            return true;
        }

        CookeryStation StationUnderPointer()
        {
            Camera camera = Eye;
            if (camera == null) return null;

            Ray ray = camera.ScreenPointToRay(PointerInput.Position);
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                CookeryStation station = hit.collider.GetComponentInParent<CookeryStation>();
                if (station != null && station.isActiveAndEnabled) return station;
            }

            return null;
        }

        void ShowMarker(bool on)
        {
            if (marker == null)
            {
                if (!on) return;

                var go = new GameObject("CrewDropMarker");
                marker = go.transform;
                markerMesh = go.AddComponent<MeshFilter>();
                markerMesh.sharedMesh = CrewMeshes.Ring(false);
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = CrewMeshes.Paint;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            marker.gameObject.SetActive(on);
        }

        /// <summary>A squash as they land, springing back up.</summary>
        void Squash()
        {
            if (model == null || landing < 0f) return;

            landing += Time.deltaTime / 0.35f;
            float t = Mathf.Clamp01(landing);
            float squash = Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.6f;
            model.localScale = Vector3.Scale(modelScale, new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f));
            if (t >= 1f)
            {
                model.localScale = modelScale;
                landing = -1f;
            }
        }

        static void Say(Vector3 at, string text)
        {
            var scope = FindFirstObjectByType<GameLifetimeScope>();
            GameComposition composition = scope != null ? scope.Composition : null;
            if (composition != null && composition.WorldUi != null) composition.WorldUi.ShowReward(at, text);
        }
    }
}
