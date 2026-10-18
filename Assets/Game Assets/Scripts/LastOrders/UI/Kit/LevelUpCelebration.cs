using DG.Tweening;
using LastOrders.Audio;
using LastOrders.UI.Hud;
using UnityEngine;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// The level-up as a moment: the star drops in and bounces, confetti bursts out of it, the
    /// ribbon unrolls, the unlocks slide up into place and the Continue button pops in and keeps
    /// gently breathing until it is pressed.
    /// </summary>
    public sealed class LevelUpCelebration : MonoBehaviour
    {
        [SerializeField] RectTransform star;
        [SerializeField] RectTransform ribbon;
        [SerializeField] RectTransform card;
        [SerializeField] CanvasGroup cardGroup;
        [SerializeField] RectTransform carryOn;
        [SerializeField] CanvasGroup carryOnGroup;
        [SerializeField] HudMotion carryOnMotion;
        [SerializeField] UiConfetti confetti;

        Vector2 starRest, cardRest;
        bool known;

        void OnEnable()
        {
            if (!known)
            {
                if (star != null) starRest = star.anchoredPosition;
                if (card != null) cardRest = card.anchoredPosition;
                known = true;
            }

            DOTween.Kill(this);
            Sequence play = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            SoundController.Play(SoundId.UiLevelUp);

            if (star != null)
            {
                star.anchoredPosition = starRest + Vector2.up * 460f;
                star.localScale = Vector3.one * 0.7f;
                star.localRotation = Quaternion.Euler(0f, 0f, -20f);
                play.Insert(0.05f, star.DOAnchorPos(starRest, 0.6f).SetEase(Ease.OutBounce));
                play.Insert(0.05f, star.DOScale(1f, 0.6f).SetEase(Ease.OutBack));
                play.Insert(0.05f, star.DOLocalRotate(Vector3.zero, 0.7f).SetEase(Ease.OutElastic));
            }

            if (confetti != null && star != null)
                play.InsertCallback(0.45f, () =>
                {
                    confetti.Play(starRest);
                    SoundController.Play(SoundId.UiConfetti);
                });

            if (ribbon != null)
            {
                ribbon.localScale = new Vector3(0f, 1f, 1f);
                play.Insert(0.4f, ribbon.DOScaleX(1f, 0.4f).SetEase(Ease.OutBack, 2f));
            }

            if (card != null)
            {
                card.anchoredPosition = cardRest + Vector2.down * 70f;
                if (cardGroup != null) cardGroup.alpha = 0f;
                play.Insert(0.55f, card.DOAnchorPos(cardRest, 0.35f).SetEase(Ease.OutCubic));
                if (cardGroup != null) play.Insert(0.55f, cardGroup.DOFade(1f, 0.25f));
            }

            if (carryOnGroup != null) carryOnGroup.alpha = 0f;
            if (carryOnMotion != null) carryOnMotion.Breathe = false;
            play.InsertCallback(0.85f, () =>
            {
                if (carryOnGroup != null) carryOnGroup.alpha = 1f;
                if (carryOnMotion != null)
                {
                    carryOnMotion.Appear(0.3f);
                    carryOnMotion.Breathe = true;
                }
            });
        }

        void OnDisable()
        {
            DOTween.Kill(this);
            if (star != null && known)
            {
                star.anchoredPosition = starRest;
                star.localScale = Vector3.one;
                star.localRotation = Quaternion.identity;
            }

            if (ribbon != null) ribbon.localScale = Vector3.one;
            if (card != null && known) card.anchoredPosition = cardRest;
            if (cardGroup != null) cardGroup.alpha = 1f;
            if (carryOnGroup != null) carryOnGroup.alpha = 1f;
        }
    }
}
