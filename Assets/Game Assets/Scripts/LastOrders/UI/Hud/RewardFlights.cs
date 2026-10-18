using System;
using System.Collections.Generic;
using DG.Tweening;
using LastOrders.Audio;
using LastOrders.Economy;
using LastOrders.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// Coins, gems and stars that fly to their counters when they are earned.
    ///
    /// A payment with a <see cref="RewardSource"/> - a diner served, a dish off the stove, a crop in -
    /// bursts into a handful of icons where it happened, with a "+24" rising off the spot; the icons
    /// hang a beat, then sweep in along a curve one after another to their counter on the HUD, which
    /// ticks up as the first lands and kicks as each one does, a little sparkle at each landing. More
    /// earned, more icons, up to a handful.
    ///
    /// One piece for every kind: what differs - the sprite, the colour, the counter - is a
    /// <see cref="Target"/> the HUD hands over. A reward with no source, or one earned while the HUD is
    /// out of sight behind a full-screen panel, counts up on the spot as it always did.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardFlights : MonoBehaviour
    {
        /// <summary>Where one kind of reward flies to, and what it looks like.</summary>
        public struct Target
        {
            public RectTransform icon;
            public HudMotion kick;
            public Sprite sprite;
            public Color tint;
            public int perIcon;
            public int maxIcons;
        }

        [Tooltip("Seconds an icon takes from where it hangs to its counter.")]
        [SerializeField] float flySeconds = 0.6f;

        [Tooltip("Size of a flying icon, in canvas pixels.")]
        [SerializeField] float iconSize = 64f;

        readonly Dictionary<RewardKind, Target> targets = new Dictionary<RewardKind, Target>();
        HudSkin skin;
        Func<bool> visible;
        RectTransform layer;

        public void Configure(HudSkin hudSkin, Func<bool> isVisible)
        {
            skin = hudSkin;
            visible = isVisible;
        }

        public void SetTarget(RewardKind kind, Target target) => targets[kind] = target;

        /// <summary>
        /// <paramref name="gained"/> of <paramref name="kind"/> was just earned: flies it in from this
        /// frame's source and calls <paramref name="show"/> as the first icon lands - or straight away
        /// when it cannot fly.
        /// </summary>
        public void Route(RewardKind kind, int gained, Action show)
        {
            if (gained <= 0 || !isActiveAndEnabled || (visible != null && !visible()) ||
                !targets.TryGetValue(kind, out Target target) || target.icon == null || target.sprite == null ||
                !RewardSource.TryGetScreen(out Vector2 from))
            {
                show?.Invoke();
                return;
            }

            Launch(target, gained, from, show);
        }

        void Launch(in Target target, int gained, Vector2 fromScreen, Action show)
        {
            if (layer == null) layer = UiFx.Layer(this);
            if (layer == null)
            {
                show?.Invoke();
                return;
            }

            int count = Mathf.Clamp(Mathf.CeilToInt(gained / (float)Mathf.Max(1, target.perIcon)), 1, Mathf.Max(1, target.maxIcons));
            Vector2 from = UiFx.ToLayer(layer, fromScreen);
            Vector2 to = UiFx.ToLayer(layer, UiFx.ScreenOf(target.icon));

            Label(target, gained, from);
            SoundController.Play(SoundId.UiRewardLaunch);

            bool shown = false;
            RectTransform icon = target.icon;
            HudMotion kick = target.kick;
            Sprite sprite = target.sprite;
            Color tint = target.tint;

            for (int i = 0; i < count; i++)
            {
                Image piece = UiFx.Spawn(layer, sprite, Color.white, from, iconSize * Random.Range(0.85f, 1.05f));
                RectTransform rt = piece.rectTransform;
                rt.localScale = Vector3.zero;

                // Out of the spot, up and to the sides, then a curve over to the counter.
                float angle = Mathf.Lerp(20f, 160f, count == 1 ? 0.5f : i / (float)(count - 1)) + Random.Range(-15f, 15f);
                Vector2 scatter = from + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * Random.Range(55f, 105f);
                Vector2 bend = (scatter + to) * 0.5f + Perpendicular(to - scatter) * Random.Range(-0.35f, 0.35f) + Vector2.up * 60f;
                float spin = Random.Range(-200f, 200f);

                Sequence s = DOTween.Sequence().SetUpdate(true).SetTarget(rt);
                s.Append(rt.DOAnchorPos(scatter, 0.3f).SetEase(Ease.OutCubic));
                s.Join(rt.DOScale(1.15f, 0.22f).SetEase(Ease.OutBack, 2.2f));
                s.AppendInterval(0.12f + i * 0.06f);
                s.Append(DOVirtual.Float(0f, 1f, flySeconds, t =>
                {
                    if (rt == null) return;
                    rt.anchoredPosition = Bezier(scatter, bend, to, t);
                    rt.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.7f, t);
                    rt.localEulerAngles = new Vector3(0f, 0f, spin * t);
                }).SetEase(Ease.InQuad));
                s.OnComplete(() =>
                {
                    if (!shown)
                    {
                        shown = true;
                        show?.Invoke();
                    }

                    if (kick != null) kick.Punch(0.12f);
                    if (icon != null) Sparkle(icon, tint);
                    SoundController.Play(SoundId.UiCoinLand);
                    if (rt != null) Destroy(rt.gameObject);
                });
            }
        }

        /// <summary>"+24" rising off the spot it was earned, in the counter's colour.</summary>
        void Label(in Target target, int gained, Vector2 at)
        {
            if (skin == null || skin.display == null) return;

            var go = new GameObject("RewardLabel", typeof(RectTransform)) { layer = layer.gameObject.layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchoredPosition = at + Vector2.up * 30f;
            rt.sizeDelta = new Vector2(260f, 70f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = skin.display;
            if (skin.displayOutline != null) text.fontSharedMaterial = skin.displayOutline;
            text.fontSize = 46f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = target.tint;
            text.raycastTarget = false;
            text.text = $"+{gained}";

            rt.localScale = Vector3.one * 0.3f;
            DOTween.Sequence().SetUpdate(true).SetTarget(rt)
                   .Append(rt.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 2.5f))
                   .Join(rt.DOAnchorPos(at + Vector2.up * 100f, 1.1f).SetEase(Ease.OutCubic))
                   .Insert(0.7f, text.DOFade(0f, 0.4f))
                   .OnComplete(() =>
                   {
                       if (rt != null) Destroy(rt.gameObject);
                   });
        }

        void Sparkle(RectTransform icon, Color tint)
        {
            if (skin == null || layer == null) return;
            Vector2 at = UiFx.ScreenOf(icon);
            UiFx.Burst(layer, at, skin[HudSprites.Glow] ?? skin[HudSprites.IconStar], tint, 4, 55f, 22f);
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        static Vector2 Perpendicular(Vector2 v) => new Vector2(-v.y, v.x);
    }
}
