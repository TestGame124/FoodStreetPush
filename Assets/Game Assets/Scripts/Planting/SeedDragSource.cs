using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>What a seed drag needs from the panel it starts on.</summary>
public interface ISeedDragHost
{
    /// <summary>The panel covers <paramref name="screen"/>: a seed dragged there is over the panel, not the world.</summary>
    bool Covers(Vector2 screen);

    /// <summary>A drag has finished; <see cref="SeedSweep.Count"/> says how many seeds it planted.</summary>
    void SweepEnded(SeedSweep sweep);
}

/// <summary>
/// A seed in the seed tray that is planted by dragging it out onto the plots. The seed follows the
/// finger, and every empty plot it passes over is planted, one seed each, until the finger lifts
/// or the seeds run out. A tap only nudges the seed, as a hint that it wants dragging.
///
/// The camera is held still for the length of the drag, so sweeping across a bed plants it
/// instead of panning the view.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class SeedDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Tooltip("The seed's picture in the slot, copied for the seed that follows the finger.")]
    [SerializeField] Image icon;

    [Tooltip("How big the dragged seed is, in canvas units.")]
    [SerializeField] float dragSize = 130f;

    [Tooltip("How far above the pointer the dragged seed floats, in canvas units, so a finger does not hide it.")]
    [SerializeField] float lift = 24f;

    ItemDataSO seed;
    ISeedDragHost host;

    SeedSweep sweep;
    RectTransform dragged;
    Image draggedImage;
    Canvas canvas;
    Camera view;
    bool cameraWasBlocked;

    public ItemDataSO Seed => seed;

    /// <summary>The drag in progress, or null.</summary>
    public SeedSweep Sweep => sweep;

    public void Bind(ItemDataSO plantable, ISeedDragHost panel, Image picture)
    {
        seed = plantable;
        host = panel;
        if (picture != null) icon = picture;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (sweep != null || seed == null) return;

        if (Stock() < 1)
        {
            Nudge();
            // Nothing to plant: let go of the drag, so the rest of it is not delivered here.
            eventData.pointerDrag = null;
            return;
        }

        view = Camera.main;
        sweep = new SeedSweep(seed, screen => PlotPicker.Under<ISeedPlot>(view, screen), screen => host != null && host.Covers(screen));
        sweep.Planted += OnPlanted;

        cameraWasBlocked = CameraPanZoomRig.InputBlocked;
        CameraPanZoomRig.InputBlocked = true;

        ShowDragged(eventData.position);
        sweep.MoveTo(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (sweep == null) return;

        MoveDragged(eventData.position);
        sweep.MoveTo(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData) => Finish();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (sweep == null && !eventData.dragging) Nudge();
    }

    void OnDisable() => Finish();

    void Finish()
    {
        if (sweep == null) return;

        SeedSweep finished = sweep;
        sweep = null;
        finished.Planted -= OnPlanted;

        CameraPanZoomRig.InputBlocked = cameraWasBlocked;
        HideDragged();
        host?.SweepEnded(finished);
    }

    int Stock() => InventorySystem.instance != null ? InventorySystem.instance.ItemCount(seed.Type) : 0;

    void OnPlanted(ISeedPlot plot, Vector2 screen)
    {
        if (dragged == null) return;

        dragged.DOKill(true);
        dragged.DOPunchScale(Vector3.one * 0.18f, 0.25f, 6, 0.6f);

        // Out of seeds: the seed fades, and nothing more can be planted on this drag.
        if (Stock() < 1 && draggedImage != null) draggedImage.DOFade(0.35f, 0.15f);
    }

    /// <summary>A little wobble: this wants dragging, or there is none of it left.</summary>
    void Nudge()
    {
        transform.DOKill(true);
        transform.DOPunchRotation(new Vector3(0f, 0f, 14f), 0.4f, 8, 0.6f);
    }

    // --- The seed that follows the finger ----------------------------------------------------------

    void ShowDragged(Vector2 screen)
    {
        Canvas own = GetComponentInParent<Canvas>();
        canvas = own != null ? own.rootCanvas : null;
        if (canvas == null) return;

        var go = new GameObject("Dragged Seed", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragged = (RectTransform)go.transform;
        dragged.SetParent(canvas.transform, false);
        dragged.SetAsLastSibling();
        dragged.anchorMin = dragged.anchorMax = new Vector2(0.5f, 0.5f);
        dragged.pivot = new Vector2(0.5f, 0f);
        dragged.sizeDelta = new Vector2(dragSize, dragSize);

        draggedImage = go.GetComponent<Image>();
        draggedImage.sprite = icon != null ? icon.sprite : seed.itemImage;
        draggedImage.preserveAspect = true;
        draggedImage.raycastTarget = false;

        MoveDragged(screen);
        dragged.localScale = Vector3.one * 0.6f;
        dragged.DOScale(1f, 0.18f).SetEase(Ease.OutBack);
    }

    void MoveDragged(Vector2 screen)
    {
        if (dragged == null || canvas == null) return;

        var area = (RectTransform)canvas.transform;
        Camera eye = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, eye, out Vector2 local))
            dragged.localPosition = local + Vector2.up * lift;
    }

    void HideDragged()
    {
        if (dragged == null) return;

        RectTransform gone = dragged;
        dragged = null;
        draggedImage = null;

        gone.DOKill();
        if (!isActiveAndEnabled)
        {
            Destroy(gone.gameObject);
            return;
        }

        gone.DOScale(0f, 0.15f).SetEase(Ease.InBack).OnComplete(() => Destroy(gone.gameObject));
    }
}
