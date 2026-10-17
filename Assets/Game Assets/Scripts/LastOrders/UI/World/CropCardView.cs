using System;
using DG.Tweening;
using LastOrders.UI.Hud;
using LastOrders.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.World
{
    /// <summary>
    /// The card a tap on a growing crop brings up - the only UI over a growing crop: the crop in a
    /// round badge, its name, the progress bar with the time left, and a gem button that finishes it
    /// on the spot - the gems it costs on the button, fewer as the crop comes on.
    ///
    /// Has a GraphicRaycaster, like the action prompt, so a press on the button is the button's alone.
    /// Closes when the player taps anywhere else in the world; the tap that opened it arrives in the
    /// same frame and is ignored. Finished, it bursts into gem sparkles and shrinks away on its own.
    /// </summary>
    public sealed class CropCardView : WorldUiElement
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text caption;

        [Tooltip("An Image set to Filled. Its fill amount is the progress.")]
        [SerializeField] Image fill;

        [SerializeField] TMP_Text time;

        [Header("Finish with gems")]
        [SerializeField] Button button;
        [SerializeField] Image buttonFace;
        [SerializeField] HudMotion buttonMotion;
        [SerializeField] TMP_Text cost;

        [Tooltip("The gem on the button; its sprite is what the sparkles are made of.")]
        [SerializeField] Image gem;

        [Tooltip("Where the sparkles go: a layer over the card that takes no raycasts.")]
        [SerializeField] RectTransform fx;

        [SerializeField] Color growingColour = new Color(0.42f, 0.70f, 0.42f);
        [SerializeField] Color stoppedColour = new Color(0.66f, 0.63f, 0.59f);
        [SerializeField] Color shortColour = new Color(1f, 0.55f, 0.48f);

        Func<bool> onFinish;
        int shownFrame = -1;
        bool closing;

        /// <summary>Pressed and paid for: the card is on its way out.</summary>
        public bool IsClosing => closing;

        protected override void Awake()
        {
            base.Awake();
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        /// <summary>What pressing the gem button does. It returns whether the crop was finished.</summary>
        public void Bind(Func<bool> finish) => onFinish = finish;

        public void Show(Sprite crop, string cropName, float progress, float secondsLeft, int gems, bool affordable, bool stopped)
        {
            if (closing) return;

            if (icon != null)
            {
                if (icon.sprite != crop) icon.sprite = crop;
                icon.enabled = crop != null;
            }

            if (caption != null) caption.text = cropName;

            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01(progress);
                fill.color = stopped ? stoppedColour : growingColour;
            }

            if (time != null) time.text = stopped ? "WRECKED" : FormatSeconds(secondsLeft);

            if (button != null && button.gameObject.activeSelf == stopped) button.gameObject.SetActive(!stopped);

            if (cost != null)
            {
                cost.text = gems.ToString();
                cost.color = affordable ? Color.white : shortColour;
            }

            if (buttonFace != null) buttonFace.color = affordable ? Color.white : new Color(0.86f, 0.84f, 0.82f);
        }

        public override void OnTakenFromPool()
        {
            shownFrame = Time.frameCount;
            closing = false;
            Mouse3D.WorldTapResolved += HandleWorldTap;
        }

        public override void OnReturnedToPool()
        {
            Mouse3D.WorldTapResolved -= HandleWorldTap;
            onFinish = null;
            shownFrame = -1;
            closing = false;
            if (button != null) button.gameObject.SetActive(true);
            base.OnReturnedToPool();
        }

        void HandleWorldTap(bool handled)
        {
            if (Time.frameCount == shownFrame || closing) return;
            Release();
        }

        void HandleClick()
        {
            if (closing) return;

            if (onFinish == null || !onFinish())
            {
                // Refused - not enough gems, or the bed is wrecked. The owner has said why.
                if (buttonMotion != null) buttonMotion.Shake(12f);
                return;
            }

            closing = true;
            Celebrate();
        }

        /// <summary>Gem sparkles out of the button, a ring, then the card springs up and shrinks away.</summary>
        void Celebrate()
        {
            if (buttonMotion != null) buttonMotion.Punch(0.3f);

            if (fx != null && button != null)
            {
                Vector2 at = UiFx.ScreenOf(button.transform);
                Sprite shard = gem != null ? gem.sprite : null;
                UiFx.Burst(fx, at, shard, Color.white, count: 10, distance: 110f, size: 30f);
                if (icon != null) UiFx.Ring(fx, UiFx.ScreenOf(icon.transform), shard, new Color(1f, 1f, 1f, 0.8f), 40f, 160f);
            }

            if (fill != null)
            {
                fill.fillAmount = 1f;
                fill.color = new Color(0.96f, 0.75f, 0.27f);
            }

            if (time != null) time.text = "READY!";

            if (body == null)
            {
                Release();
                return;
            }

            DOTween.Kill(body);
            DOTween.Sequence().SetTarget(body)
                   .Append(body.DOScale(1.12f, 0.12f).SetEase(Ease.OutQuad))
                   .AppendInterval(0.22f)
                   .Append(body.DOScale(0f, 0.22f).SetEase(Ease.InBack))
                   .OnComplete(Release);
        }

        /// <summary>"8s" under a minute, "1:05" from a minute up, as the other timers have it.</summary>
        public static string FormatSeconds(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return total >= 60 ? $"{total / 60}:{total % 60:00}" : $"{total}s";
        }

        /// <summary>Editor wiring, for the prefab builder.</summary>
        public void Configure(RectTransform animated, Image cropIcon, TMP_Text name, Image bar, TMP_Text timeLabel, Button finish,
                              Image face, HudMotion motion, TMP_Text costLabel, Image gemIcon, RectTransform sparkles)
        {
            body = animated;
            icon = cropIcon;
            caption = name;
            fill = bar;
            time = timeLabel;
            button = finish;
            buttonFace = face;
            buttonMotion = motion;
            cost = costLabel;
            gem = gemIcon;
            fx = sparkles;
        }
    }
}
