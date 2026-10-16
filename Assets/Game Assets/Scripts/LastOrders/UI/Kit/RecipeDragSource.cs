using DG.Tweening;
using LastOrders.UI.Hud;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Cooking by hand: a recipe card is picked up and its dish dragged up out of the tray onto the
    /// station's free slot, where letting go starts the cook (through the card's own
    /// <see cref="RecipeUiBox"/>, exactly as its Cook button did).
    ///
    /// A card that cannot be cooked right now does not lift at all - it shakes and the tray says
    /// why: a missing ingredient, both slots busy, a full storehouse. A sideways drag still scrolls
    /// the tray, and a plain tap says what to do.
    /// </summary>
    [RequireComponent(typeof(RecipeUiBox))]
    public sealed class RecipeDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] RecipeUiBox box;

        [Tooltip("The card's visible part; shaken and bounced without touching the card's own scale.")]
        [SerializeField] RectTransform body;

        [SerializeField] Image picture;

        [Tooltip("The dish is held this far above the finger, so the finger does not hide it.")]
        [SerializeField] float lift = 70f;

        [SerializeField] float ghostSize = 150f;

        KitchenStationView kitchen;
        ScrollRect shelf;
        bool scrolling;
        bool carrying;
        RectTransform ghost;

        KitchenStationView Kitchen
        {
            get
            {
                if (kitchen == null) kitchen = GetComponentInParent<KitchenStationView>();
                return kitchen;
            }
        }

        void Reset() => box = GetComponent<RecipeUiBox>();

        void OnDisable()
        {
            if (carrying && Kitchen != null) Kitchen.EndDrag();
            carrying = scrolling = false;
            if (ghost != null) Destroy(ghost.gameObject);
            if (picture != null) picture.color = Color.white;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (shelf == null) shelf = GetComponentInParent<ScrollRect>();
            if (shelf != null && shelf.horizontal && !shelf.vertical && Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y))
            {
                scrolling = true;
                shelf.OnBeginDrag(eventData);
                return;
            }

            if (!CanStart()) return;

            carrying = true;
            Kitchen.BeginDrag();
            MakeGhost(eventData.position);
            if (picture != null) picture.color = new Color(1f, 1f, 1f, 0.35f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (scrolling)
            {
                if (shelf != null) shelf.OnDrag(eventData);
                return;
            }

            if (!carrying) return;
            MoveGhost(eventData.position);
            Kitchen.Hover(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (scrolling)
            {
                scrolling = false;
                if (shelf != null) shelf.OnEndDrag(eventData);
                return;
            }

            if (!carrying) return;
            carrying = false;

            KitchenStationView k = Kitchen;
            bool dropped = k.Hover(eventData.position);
            Vector3 slot = k.SlotPosition();
            k.EndDrag();
            if (picture != null) picture.color = Color.white;

            if (dropped)
            {
                box.CookBtn.onClick.Invoke();
                Land(slot, 0.55f);
                Bounce();
            }
            else
            {
                Land(picture != null ? picture.transform.position : transform.position, 1f);
                k.Say("Drop the dish onto the free slot above the station", HudTone.Info);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging) return;

            if (!CanStart()) return;
            Bounce();
            int supply = box.Recipe.SupplyCost(box.Inventory);
            string cost = supply > 0 ? $" · {supply} Coins for missing ingredients" : string.Empty;
            Kitchen.Say($"Makes {box.Recipe.Results.Amount} servings in {UiSecondsText.Format(box.Recipe.TotalTimeToCook)}{cost}. Drag onto a free slot.", HudTone.Info);
        }

        /// <summary>Whether this card can be cooked now; says why not, and shakes, when it cannot.</summary>
        bool CanStart()
        {
            KitchenStationView k = Kitchen;
            if (k == null || box == null || box.Recipe == null || box.Inventory == null) return false;

            if (k.NextFreeSlot() < 0)
            {
                Shake();
                k.Say("Both slots are busy - wait for one to finish", HudTone.Warning);
                return false;
            }

            if (!box.Recipe.CanCook(box.Inventory))
            {
                Shake();
                k.Say(box.Recipe.BlockReason(box.Inventory), HudTone.Danger);
                return false;
            }

            if (!box.Inventory.CanAdd(box.Recipe.Results.item.type))
            {
                Shake();
                k.Say("Your storehouse is full - deliver or use something first", HudTone.Danger);
                return false;
            }

            return true;
        }

        void MakeGhost(Vector2 screen)
        {
            RectTransform layer = Kitchen.DragLayer != null ? Kitchen.DragLayer : (RectTransform)Kitchen.transform;

            var go = new GameObject("HeldDish", typeof(RectTransform)) { layer = gameObject.layer };
            ghost = (RectTransform)go.transform;
            ghost.SetParent(layer, false);
            ghost.sizeDelta = new Vector2(ghostSize, ghostSize);

            var image = go.AddComponent<Image>();
            image.sprite = box.CookingItemImage != null ? box.CookingItemImage.sprite : null;
            image.preserveAspect = true;
            image.raycastTarget = false;

            ghost.localScale = Vector3.one * 0.6f;
            ghost.DOScale(1f, 0.15f).SetEase(Ease.OutBack).SetUpdate(true);
            MoveGhost(screen);
        }

        void MoveGhost(Vector2 screen)
        {
            if (ghost == null) return;
            var layer = (RectTransform)ghost.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out Vector2 local))
                ghost.anchoredPosition = local + Vector2.up * lift;
        }

        /// <summary>The held dish settles where it was dropped - into the slot, or back onto its card - and goes.</summary>
        void Land(Vector3 at, float scale)
        {
            if (ghost == null) return;
            RectTransform landing = ghost;
            ghost = null;

            DOTween.Kill(landing);
            DOTween.Sequence().SetUpdate(true).SetTarget(landing)
                   .Join(landing.DOMove(at, 0.2f).SetEase(Ease.InQuad))
                   .Join(landing.DOScale(scale, 0.2f))
                   .OnComplete(() => { if (landing != null) Destroy(landing.gameObject); });
        }

        void Shake()
        {
            if (body == null) return;
            DOTween.Kill(body, true);
            body.DOShakeAnchorPos(0.35f, new Vector2(12f, 0f), 18, 0f).SetUpdate(true).SetTarget(body);
        }

        void Bounce()
        {
            if (body == null) return;
            DOTween.Kill(body, true);
            body.localScale = Vector3.one;
            body.DOPunchScale(Vector3.one * 0.08f, 0.25f, 6, 0.6f).SetUpdate(true).SetTarget(body);
        }
    }
}
