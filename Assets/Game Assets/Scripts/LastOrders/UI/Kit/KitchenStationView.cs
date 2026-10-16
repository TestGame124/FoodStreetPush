using DG.Tweening;
using LastOrders.UI.Hud;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Cooking at a station, in the world rather than in a window: the camera swings round to put
    /// the station in the middle of the screen, its cooking slots hang just above it, and the
    /// recipes wait in a tray along the bottom to be dragged up onto a free slot.
    ///
    /// <see cref="CookingUIPanel"/> still opens the station and lists its recipes, and
    /// <see cref="RecipeUiBox"/> still starts the cook - this is the part that frames the station,
    /// keeps the slots over it as the camera moves, and answers a drag.
    /// </summary>
    [RequireComponent(typeof(CookingUIPanel))]
    public sealed class KitchenStationView : MonoBehaviour
    {
        [SerializeField] CookingUIPanel panel;

        [Tooltip("The row of cooking slots that hangs above the station.")]
        [SerializeField] RectTransform rack;

        [Tooltip("The recipe tray, which slides up from the bottom as the kitchen opens.")]
        [SerializeField] RectTransform tray;

        [SerializeField] CookSlotView[] slots = new CookSlotView[0];

        [Tooltip("Where a dragged dish is drawn, over everything else on this screen.")]
        [SerializeField] RectTransform dragLayer;

        [SerializeField] TMP_Text hint;
        [SerializeField] HudMotion hintMotion;
        [SerializeField] string usualHint = "Drag a dish up onto the free slot to cook it";

        [Tooltip("Where the camera holds the station on screen, as the focus camera's height setting (0.5 is the middle).")]
        [SerializeField] float focusHeight = 0.42f;

        [Tooltip("The focus camera's field of view while cooking - narrower than its own, so the station fills the view. 0 leaves it alone.")]
        [SerializeField] float focusFieldOfView = 10f;

        [Tooltip("How far above the middle of the screen the station is held, in canvas pixels, to sit clear of the recipe tray.")]
        [SerializeField] float stationRaise = 140f;

        [Tooltip("How far above the top of the station the slots hang, in metres.")]
        [SerializeField] float rackLift = 0.35f;

        [Tooltip("Keeps the slots this far from the screen's top, and clear of the recipe tray at the bottom.")]
        [SerializeField] float topMargin = 150f;
        [SerializeField] float bottomMargin = 360f;

        [Tooltip("A dish dropped this close to the station, in canvas pixels, counts as dropped on the free slot.")]
        [SerializeField] float dropRadius = 220f;

        CookeryStation framed;
        float stationTop;
        Vector3 stationCentre;
        float hintUntil = -1f;

        // The camera frames this point rather than the station's own transform: stations pivot on a
        // corner, so framing the transform put the station off to one side.
        Transform focusPoint;
        float savedFieldOfView = -1f;
        Tween restoreLens;

        public bool Dragging { get; private set; }

        public CookeryStation Station => panel != null ? panel.Station : null;

        public RectTransform DragLayer => dragLayer;

        void Reset() => panel = GetComponent<CookingUIPanel>();

        Vector2 trayRest;
        bool trayKnown;

        void OnEnable()
        {
            framed = null;
            Dragging = false;
            SayUsual();

            if (tray != null)
            {
                if (!trayKnown)
                {
                    trayRest = tray.anchoredPosition;
                    trayKnown = true;
                }

                DOTween.Kill(tray);
                tray.anchoredPosition = trayRest + Vector2.down * 420f;
                tray.DOAnchorPos(trayRest, 0.38f).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(tray);
            }

            if (rack != null)
            {
                DOTween.Kill(rack);
                rack.localScale = Vector3.one * 0.2f;
                rack.DOScale(1f, 0.45f).SetDelay(0.25f).SetEase(Ease.OutBack, 2f).SetUpdate(true).SetTarget(rack);
            }
        }

        void OnDisable()
        {
            Dragging = false;
            if (framed != null)
            {
                CameraController.SetCameraToDefault();

                // Back to the focus camera's own lens once the camera has blended away, so the seed
                // tray and anything else that uses it frames as it always did.
                if (savedFieldOfView > 0f)
                {
                    restoreLens?.Kill();
                    restoreLens = DOVirtual.DelayedCall(1.5f, RestoreLens, true);
                }
            }

            framed = null;
        }

        void OnDestroy()
        {
            restoreLens?.Kill();
            RestoreLens();
            if (focusPoint != null) Destroy(focusPoint.gameObject);
        }

        void LateUpdate()
        {
            CookeryStation station = Station;
            if (station != framed && station != null)
            {
                framed = station;
                Frame(station);
            }

            if (station != null) PlaceRack(station);
            if (hintUntil > 0f && Time.unscaledTime >= hintUntil) SayUsual();
        }

        /// <summary>
        /// Swings the focus camera onto the middle of the station, a touch closer than it usually
        /// sits, with the station lifted above the screen's middle so the tray does not cover it.
        /// </summary>
        void Frame(CookeryStation station)
        {
            Bounds bounds = BoundsOf(station);
            stationCentre = bounds.center;
            stationTop = bounds.max.y;

            var focusCamera = CameraController.GetCamera(CameraController.CameraType.Focus) as CinemachineCamera;
            float fieldOfView = 15f;
            float distance = 40f;
            Vector3 up = Vector3.up;
            if (focusCamera != null)
            {
                restoreLens?.Kill();
                if (focusFieldOfView > 0f)
                {
                    if (savedFieldOfView < 0f) savedFieldOfView = focusCamera.Lens.FieldOfView;
                    LensSettings lens = focusCamera.Lens;
                    lens.FieldOfView = focusFieldOfView;
                    focusCamera.Lens = lens;
                }

                fieldOfView = focusCamera.Lens.FieldOfView;
                var follow = focusCamera.GetComponent<CinemachineFollow>();
                if (follow != null) distance = follow.FollowOffset.magnitude;
                up = focusCamera.transform.up;
            }

            float metresPerPixel = 2f * distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) / 1080f;

            if (focusPoint == null) focusPoint = new GameObject("KitchenFocusPoint").transform;
            focusPoint.position = stationCentre - up * (stationRaise * metresPerPixel);
            CameraController.Focus(CameraController.CameraType.Focus, focusPoint, null, focusHeight);
        }

        void RestoreLens()
        {
            if (savedFieldOfView <= 0f) return;
            var focusCamera = CameraController.GetCamera(CameraController.CameraType.Focus) as CinemachineCamera;
            if (focusCamera != null)
            {
                LensSettings lens = focusCamera.Lens;
                lens.FieldOfView = savedFieldOfView;
                focusCamera.Lens = lens;
            }

            savedFieldOfView = -1f;
        }

        /// <summary>Everything the station draws, so the camera and the slots fit it whatever its size.</summary>
        static Bounds BoundsOf(CookeryStation station)
        {
            bool any = false;
            var bounds = new Bounds(station.transform.position, Vector3.zero);
            foreach (Renderer r in station.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }

            if (!any) bounds = new Bounds(station.transform.position + Vector3.up, new Vector3(2f, 2f, 2f));
            return bounds;
        }

        void PlaceRack(CookeryStation station)
        {
            Camera eye = Camera.main;
            if (rack == null || eye == null) return;

            Vector3 world = stationCentre;
            world.y = stationTop + rackLift;
            Vector3 screen = eye.WorldToScreenPoint(world);
            if (screen.z < 0f) return;

            var area = (RectTransform)rack.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 local)) return;

            Rect r = area.rect;
            float height = rack.rect.height;
            local.x = Mathf.Clamp(local.x, r.xMin + rack.rect.width * 0.5f + 20f, r.xMax - rack.rect.width * 0.5f - 20f);
            local.y = Mathf.Clamp(local.y, r.yMin + bottomMargin, r.yMax - topMargin - height);
            rack.anchoredPosition = local;
        }

        /// <summary>The slot the next dish goes into, or -1 when every slot is busy.</summary>
        public int NextFreeSlot()
        {
            CookeryStation station = Station;
            if (station == null || station.recipiesInProgress == null) return -1;

            int busy = station.recipiesInProgress.Count;
            int count = panel != null ? panel.GetSlotsCount() : slots.Length;
            return busy < count && busy < slots.Length ? busy : -1;
        }

        public void BeginDrag() => Dragging = true;

        public void EndDrag()
        {
            Dragging = false;
            foreach (CookSlotView slot in slots)
                if (slot != null) slot.Hovered = false;
        }

        /// <summary>Lights up the free slot while a dish is held over it; true when a drop there would cook.</summary>
        public bool Hover(Vector2 screen)
        {
            int free = NextFreeSlot();
            bool over = free >= 0 && Over(screen, free);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].Hovered = over && i == free;
            return over;
        }

        /// <summary>The free slot's screen position, for a dropped dish to fly into.</summary>
        public Vector3 SlotPosition()
        {
            int free = NextFreeSlot();
            CookSlotView slot = free >= 0 ? slots[free] : null;
            return slot != null ? slot.transform.position : rack != null ? rack.position : transform.position;
        }

        bool Over(Vector2 screen, int index)
        {
            CookSlotView slot = slots[index];
            if (slot == null) return false;

            // Generous: the slot itself, the whole rack, or anywhere near the station counts.
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slot.transform, screen, null)) return true;
            if (rack != null && RectTransformUtility.RectangleContainsScreenPoint(rack, screen, null)) return true;

            CookeryStation station = Station;
            Camera eye = Camera.main;
            if (station == null || eye == null) return false;

            Vector3 at = eye.WorldToScreenPoint(stationCentre);
            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            return at.z > 0f && Vector2.Distance(at, screen) <= dropRadius * scale;
        }

        public void Say(string message, HudTone tone)
        {
            if (hint == null) return;
            hint.text = message;
            hint.color = tone == HudTone.Danger ? HudPalette.Red : tone == HudTone.Warning ? HudPalette.GoldDark : HudPalette.TealDark;
            hintUntil = Time.unscaledTime + 3f;
            if (hintMotion != null) hintMotion.Punch(0.1f);
        }

        void SayUsual()
        {
            hintUntil = -1f;
            if (hint == null) return;
            hint.text = usualHint;
            hint.color = HudPalette.InkSoft;
        }
    }
}
