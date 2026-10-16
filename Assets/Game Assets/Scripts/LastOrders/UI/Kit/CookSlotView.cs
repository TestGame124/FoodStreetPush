using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// One cooking slot above the station, drawn straight from the station's queue: the dish on
    /// the plate with a ring filling as it cooks and the time left under it, "Next" for a dish
    /// waiting its turn, and a "Drop here" mark on the slot the next dish goes into - glowing while
    /// a dish is being dragged and swelling when it is held over it.
    /// </summary>
    public sealed class CookSlotView : MonoBehaviour
    {
        [SerializeField] KitchenStationView kitchen;
        [SerializeField] int index;

        [SerializeField] RectTransform plate;
        [SerializeField] Image dish;
        [SerializeField] Image progress;
        [SerializeField] GameObject timerChip;
        [SerializeField] TMP_Text timer;
        [SerializeField] CanvasGroup freeMark;
        [SerializeField] TMP_Text freeLabel;
        [SerializeField] Image glow;

        [Header("Effects")]
        [SerializeField] Sprite sparkle;
        [SerializeField] Sprite ring;

        static readonly Dictionary<ItemType, Sprite> pictures = new Dictionary<ItemType, Sprite>();

        float swell = 1f;
        float bounce, bounceVelocity;
        CookRecipeData shown;
        bool primed;
        Sprite shownPicture;

        /// <summary>Set by the kitchen while a dragged dish is over this slot.</summary>
        public bool Hovered { get; set; }

        void OnEnable()
        {
            swell = 1f;
            bounce = bounceVelocity = 0f;
            Hovered = false;
            shown = null;
            primed = false;
        }

        void LateUpdate()
        {
            CookeryStation station = kitchen != null ? kitchen.Station : null;
            List<CookRecipeData> queue = station != null ? station.recipiesInProgress : null;
            CookRecipeData cook = queue != null && index < queue.Count ? queue[index] : null;
            bool busy = cook != null;
            bool next = !busy && kitchen != null && kitchen.NextFreeSlot() == index;

            if (!primed)
            {
                if (station != null)
                {
                    shown = cook;
                    primed = true;
                }
            }
            else if (cook != shown)
            {
                // The dish that was here is not cooking anywhere any more: it is done.
                if (shown != null && (queue == null || !queue.Contains(shown))) Finished();
                if (cook != null) Landed();
                shown = cook;
            }

            if (dish != null)
            {
                dish.gameObject.SetActive(busy);
                if (busy) dish.sprite = shownPicture = PictureOf(cook);
            }

            if (progress != null)
            {
                progress.gameObject.SetActive(busy && cook.active);
                if (busy && cook.totalTime > 0) progress.fillAmount = 1f - Mathf.Clamp01(cook.timer / cook.totalTime);
            }

            if (timerChip != null) timerChip.SetActive(busy);
            if (busy && timer != null) timer.text = cook.active ? UiSecondsText.Format(cook.timer) : "Next";

            if (freeMark != null)
            {
                freeMark.gameObject.SetActive(!busy);
                freeMark.alpha = next ? 1f : 0.4f;
                if (freeLabel != null) freeLabel.text = next ? "Drop here" : "Free";
            }

            bool lit = next && kitchen.Dragging;
            if (glow != null)
            {
                glow.gameObject.SetActive(lit);
                if (lit)
                {
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 7f);
                    Color c = glow.color;
                    c.a = Hovered ? 1f : pulse;
                    glow.color = c;
                }
            }

            float goal = Hovered ? 1.14f : lit ? 1.04f : 1f;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            swell = Mathf.MoveTowards(swell, goal, dt * 2.5f);
            bounceVelocity += (-bounce * 380f - bounceVelocity * 14f) * dt;
            bounce += bounceVelocity * dt;
            if (plate != null) plate.localScale = Vector3.one * (swell * (1f + bounce));
        }

        /// <summary>A dish has just been put on this slot: the plate jumps and a ring of sparkles goes off.</summary>
        void Landed()
        {
            bounceVelocity += 7f;
            RectTransform layer = UiFx.Layer(this);
            if (layer == null || plate == null) return;

            Vector2 at = UiFx.ScreenOf(plate);
            UiFx.Ring(layer, at, ring, new Color(1f, 0.88f, 0.45f, 0.9f), 100f, 300f);
            UiFx.Burst(layer, at, sparkle, new Color(1f, 0.93f, 0.6f), 7, 130f, 30f);
        }

        /// <summary>A dish is done: it lifts off the plate, grows and fades, in a shower of gold sparkles.</summary>
        void Finished()
        {
            bounceVelocity += 5f;
            RectTransform layer = UiFx.Layer(this);
            if (layer == null || plate == null) return;

            Vector2 at = UiFx.ScreenOf(plate);
            UiFx.Burst(layer, at, sparkle, new Color(1f, 0.82f, 0.32f), 12, 190f, 40f);
            UiFx.Ring(layer, at, ring, new Color(1f, 1f, 1f, 0.9f), 120f, 360f);

            if (shownPicture == null) return;
            Image lift = UiFx.Spawn(layer, shownPicture, Color.white, UiFx.ToLayer(layer, at), 112f);
            RectTransform rt = lift.rectTransform;
            DOTween.Sequence().SetUpdate(true).SetTarget(rt)
                   .Join(rt.DOAnchorPosY(rt.anchoredPosition.y + 170f, 0.7f).SetEase(Ease.OutCubic))
                   .Join(rt.DOScale(1.35f, 0.25f).SetEase(Ease.OutBack))
                   .Insert(0.35f, lift.DOFade(0f, 0.35f))
                   .OnComplete(() => { if (rt != null) Destroy(rt.gameObject); });
        }

        static Sprite PictureOf(CookRecipeData cook)
        {
            ItemType type = cook.results.item.type;
            if (pictures.TryGetValue(type, out Sprite sprite) && sprite != null) return sprite;

            ItemDataSO data = ItemController.GetItem(type);
            sprite = data != null ? data.itemImage : null;
            pictures[type] = sprite;
            return sprite;
        }
    }
}
