using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The sickle that brings in ripe crops. Tap a ripe pot and the sickle comes up in a badge over it;
/// drag the sickle across the pots and every ripe one it passes over is harvested. It goes away
/// when it is let go, when the player taps anywhere else, or when the crop it waits over has gone.
///
/// While it waits it follows its pot round the screen as the camera moves. While it is dragged
/// the badge fades, the sickle rides a little above the finger so it stays in sight, swings with
/// the stroke, and slashes at each crop it cuts. The camera is held still for the drag.
///
/// It harvests through <see cref="IHarvestPlot"/> and finds pots through <see cref="PlotPicker"/>,
/// so it works on anything harvestable. One per scene, on the game's UI canvas, set up by
/// Last Orders / Farming / Build Harvest Effects &amp; Sickle.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public sealed class HarvestTool : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    /// <summary>The sickle in the scene, if there is one; ripe pots fall back to a plain tap without it.</summary>
    public static HarvestTool Instance { get; private set; }

    [Tooltip("The round badge behind the sickle while it waits over a pot. Fades while the sickle is dragged.")]
    [SerializeField] Graphic badge;

    [Tooltip("The sickle itself.")]
    [SerializeField] RectTransform sickle;

    [Tooltip("How far above the crop the sickle waits, in canvas units.")]
    [SerializeField] float hover = 85f;

    [Tooltip("How far above the pointer the sickle rides while it is dragged, in canvas units, so a finger does not hide it.")]
    [SerializeField] float lift = 55f;

    [Tooltip("How finely a fast swipe is checked for pots, in screen pixels.")]
    [SerializeField] float stepPixels = 12f;

    [Tooltip("How far the sickle swings with the stroke, in degrees.")]
    [SerializeField] float maxSwing = 35f;

    RectTransform rect;
    CanvasGroup group;
    Camera view;

    IHarvestPlot waitingOver;
    Vector3 waitingAt;
    int offeredFrame = -1;
    bool shown;

    PlotSweep<IHarvestPlot> sweep;
    bool cameraWasBlocked;
    float swing;

    public bool IsShown => shown;
    public bool IsDragging => sweep != null;

    void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        HideNow();
    }

    void OnEnable()
    {
        Instance = this;
        Mouse3D.WorldTapResolved += OnWorldTap;
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        Mouse3D.WorldTapResolved -= OnWorldTap;
        EndSweep();
        HideNow();
    }

    /// <summary>
    /// Brings the sickle up over <paramref name="plot"/>, waiting at the world point
    /// <paramref name="top"/>. False when there is nothing ripe there or a harvest is under way.
    /// </summary>
    public bool Offer(IHarvestPlot plot, Vector3 top)
    {
        if (plot == null || !plot.CanHarvest || sweep != null)
            return false;

        bool moving = shown;
        waitingOver = plot;
        waitingAt = top;
        offeredFrame = Time.frameCount;
        shown = true;

        group.alpha = 1f;
        group.blocksRaycasts = true;
        SetBadge(1f);
        Place();

        rect.DOKill();
        rect.localRotation = Quaternion.identity;
        if (moving)
        {
            rect.localScale = Vector3.one;
            rect.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.6f);
        }
        else
        {
            rect.localScale = Vector3.one * 0.3f;
            rect.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
        }

        Beckon();
        return true;
    }

    /// <summary>Puts the sickle away.</summary>
    public void Hide()
    {
        EndSweep();
        if (!shown) return;

        shown = false;
        waitingOver = null;
        group.blocksRaycasts = false;

        rect.DOKill();
        sickle.DOKill();
        rect.DOScale(0.3f, 0.15f).SetEase(Ease.InBack).OnComplete(() => group.alpha = 0f);
    }

    void HideNow()
    {
        shown = false;
        waitingOver = null;
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }

    /// <summary>A tap anywhere else in the world puts the sickle away; the tap that brought it up does not.</summary>
    void OnWorldTap(bool handled)
    {
        if (shown && sweep == null && Time.frameCount != offeredFrame)
            Hide();
    }

    void LateUpdate()
    {
        if (!shown || sweep != null)
            return;

        // Picked some other way, or the pot went: nothing left here to cut.
        if (waitingOver == null || (waitingOver is Object unityObject && unityObject == null) || !waitingOver.CanHarvest)
        {
            Hide();
            return;
        }

        Place();
    }

    // --- The harvest stroke ----------------------------------------------------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!shown || sweep != null)
        {
            eventData.pointerDrag = null;
            return;
        }

        view = Camera.main;
        sweep = new PlotSweep<IHarvestPlot>(screen => PlotPicker.Under<IHarvestPlot>(view, screen),
                                            plot => plot.CanHarvest && plot.TryHarvest(), null, stepPixels);
        sweep.Acted += OnHarvested;

        cameraWasBlocked = CameraPanZoomRig.InputBlocked;
        CameraPanZoomRig.InputBlocked = true;

        // Picked up: the badge falls away and the sickle comes up bigger, into the hand.
        rect.DOKill();
        sickle.DOKill();
        sickle.localRotation = Quaternion.identity;
        swing = 0f;
        rect.DOScale(1.2f, 0.12f).SetEase(Ease.OutQuad);
        DOTween.To(() => badge != null ? badge.color.a : 0f, SetBadge, 0f, 0.12f).SetTarget(rect);

        Follow(eventData.position);
        sweep.MoveTo(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (sweep == null) return;

        // Leans into the stroke: the blade trails the way the hand is moving.
        float target = Mathf.Clamp(-eventData.delta.x * 1.6f, -maxSwing, maxSwing);
        swing = Mathf.Lerp(swing, target, 0.35f);
        if (!DOTween.IsTweening(sickle)) sickle.localRotation = Quaternion.Euler(0f, 0f, swing);

        Follow(eventData.position);
        sweep.MoveTo(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (sweep == null) return;

        EndSweep();
        Hide();
    }

    /// <summary>A tap on the sickle itself: a nudge, as a hint that it wants dragging.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!shown || sweep != null || eventData.dragging) return;

        sickle.DOKill(true);
        sickle.DOPunchRotation(new Vector3(0f, 0f, 22f), 0.4f, 8, 0.6f).OnComplete(Beckon);
    }

    void OnHarvested(IHarvestPlot plot, Vector2 at)
    {
        // A quick slash at each crop cut.
        sickle.DOKill(true);
        sickle.localRotation = Quaternion.Euler(0f, 0f, swing);
        sickle.DOPunchRotation(new Vector3(0f, 0f, -40f), 0.22f, 4, 0.5f);
        rect.DOKill(true);
        rect.localScale = Vector3.one * 1.2f;
        rect.DOPunchScale(Vector3.one * 0.12f, 0.2f, 5, 0.5f);
    }

    void EndSweep()
    {
        if (sweep == null) return;

        sweep.Acted -= OnHarvested;
        sweep = null;
        CameraPanZoomRig.InputBlocked = cameraWasBlocked;
    }

    // --- Placing it ------------------------------------------------------------------------------

    /// <summary>A slow beckoning rock while the sickle waits.</summary>
    void Beckon()
    {
        if (!shown || sweep != null) return;

        sickle.DOKill();
        sickle.localRotation = Quaternion.Euler(0f, 0f, 8f);
        sickle.DOLocalRotate(new Vector3(0f, 0f, -10f), 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    void Place()
    {
        if (view == null) view = Camera.main;
        if (view == null) return;

        Vector3 screen = view.WorldToScreenPoint(waitingAt);
        if (screen.z < 0f) return;

        if (ToLocal(screen, out Vector2 local))
            rect.localPosition = local + Vector2.up * hover;
    }

    void Follow(Vector2 screen)
    {
        if (ToLocal(screen, out Vector2 local))
            rect.localPosition = local + Vector2.up * lift;
    }

    bool ToLocal(Vector2 screen, out Vector2 local)
    {
        local = default;
        var area = rect.parent as RectTransform;
        if (area == null) return false;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera eye = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, eye, out local);
    }

    void SetBadge(float alpha)
    {
        if (badge == null) return;
        Color c = badge.color;
        c.a = alpha;
        badge.color = c;
    }
}
