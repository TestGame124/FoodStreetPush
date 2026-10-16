using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Little screen effects for the moments that should feel good: sparkles bursting out of a
    /// slot, a ring flashing round it, coins flying from an order into the wallet. Every piece is a
    /// throwaway image on a layer over everything else on the canvas, tweened on unscaled time and
    /// destroyed when it is done.
    /// </summary>
    public static class UiFx
    {
        const string LayerName = "FxLayer";

        /// <summary>The effects layer on <paramref name="near"/>'s canvas, made on first use and kept on top.</summary>
        public static RectTransform Layer(Component near)
        {
            Canvas canvas = near != null ? near.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;

            Transform found = canvas.transform.Find(LayerName);
            RectTransform layer;
            if (found == null)
            {
                var go = new GameObject(LayerName, typeof(RectTransform)) { layer = canvas.gameObject.layer };
                layer = (RectTransform)go.transform;
                layer.SetParent(canvas.transform, false);
                layer.anchorMin = Vector2.zero;
                layer.anchorMax = Vector2.one;
                layer.offsetMin = layer.offsetMax = Vector2.zero;
                var group = go.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            else
            {
                layer = (RectTransform)found;
            }

            layer.SetAsLastSibling();
            return layer;
        }

        /// <summary>A screen point (as a pointer or WorldToScreenPoint gives it) on <paramref name="layer"/>.</summary>
        public static Vector2 ToLayer(RectTransform layer, Vector2 screen)
        {
            Canvas canvas = layer.GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, eye, out Vector2 local);
            return local;
        }

        /// <summary>Where a UI element is on screen.</summary>
        public static Vector2 ScreenOf(Transform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(eye, target.position);
        }

        public static Image Spawn(RectTransform layer, Sprite sprite, Color colour, Vector2 local, float size)
        {
            var go = new GameObject("Fx", typeof(RectTransform)) { layer = layer.gameObject.layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchoredPosition = local;
            rt.sizeDelta = new Vector2(size, size);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Sparkles bursting outwards from a point, spinning, and fading as they go.</summary>
        public static void Burst(RectTransform layer, Vector2 screen, Sprite sprite, Color colour, int count = 8,
                                 float distance = 120f, float size = 34f)
        {
            if (layer == null || sprite == null) return;

            Vector2 at = ToLayer(layer, screen);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count + Random.Range(-0.04f, 0.04f)) * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float reach = distance * Random.Range(0.7f, 1.15f);

                Image piece = Spawn(layer, sprite, colour, at, size * Random.Range(0.7f, 1.2f));
                RectTransform rt = piece.rectTransform;
                rt.localScale = Vector3.one * 0.2f;

                DOTween.Sequence().SetUpdate(true).SetTarget(rt)
                       .Join(rt.DOAnchorPos(at + direction * reach, 0.5f).SetEase(Ease.OutCubic))
                       .Join(rt.DOScale(1f, 0.18f).SetEase(Ease.OutBack))
                       .Join(rt.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-220f, 220f)), 0.5f, RotateMode.FastBeyond360))
                       .Insert(0.26f, piece.DOFade(0f, 0.24f))
                       .Insert(0.26f, rt.DOScale(0.4f, 0.24f))
                       .OnComplete(() => Kill(rt));
            }
        }

        /// <summary>A ring that flashes outwards from a point and fades.</summary>
        public static void Ring(RectTransform layer, Vector2 screen, Sprite sprite, Color colour, float from = 80f, float to = 260f)
        {
            if (layer == null || sprite == null) return;

            Image ring = Spawn(layer, sprite, colour, ToLayer(layer, screen), to);
            RectTransform rt = ring.rectTransform;
            rt.localScale = Vector3.one * (from / to);
            DOTween.Sequence().SetUpdate(true).SetTarget(rt)
                   .Join(rt.DOScale(1f, 0.42f).SetEase(Ease.OutCubic))
                   .Join(ring.DOFade(0f, 0.42f).SetEase(Ease.InQuad))
                   .OnComplete(() => Kill(rt));
        }

        /// <summary>
        /// Icons that scatter out of one point and then fly into another one by one - coins from an
        /// order into the wallet. <paramref name="arrived"/> runs as each one lands.
        /// </summary>
        public static void Fly(RectTransform layer, Sprite sprite, Vector2 fromScreen, Vector2 toScreen, int count, float size,
                               Action arrived = null)
        {
            if (layer == null || sprite == null) return;

            Vector2 from = ToLayer(layer, fromScreen);
            Vector2 to = ToLayer(layer, toScreen);
            for (int i = 0; i < count; i++)
            {
                Image icon = Spawn(layer, sprite, Color.white, from, size);
                RectTransform rt = icon.rectTransform;
                rt.localScale = Vector3.one * 0.3f;
                Vector2 scatter = from + Random.insideUnitCircle.normalized * Random.Range(50f, 110f);

                DOTween.Sequence().SetUpdate(true).SetTarget(rt)
                       .Append(rt.DOAnchorPos(scatter, 0.2f).SetEase(Ease.OutCubic))
                       .Join(rt.DOScale(1.1f, 0.2f).SetEase(Ease.OutBack))
                       .AppendInterval(0.05f + i * 0.045f)
                       .Append(rt.DOAnchorPos(to, 0.45f).SetEase(Ease.InQuad))
                       .Join(rt.DOScale(0.65f, 0.45f).SetEase(Ease.InQuad))
                       .OnComplete(() =>
                       {
                           arrived?.Invoke();
                           Kill(rt);
                       });
            }
        }

        static void Kill(RectTransform rt)
        {
            if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
        }
    }
}
