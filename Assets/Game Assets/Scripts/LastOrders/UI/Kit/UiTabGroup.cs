using System;
using DG.Tweening;
using LastOrders.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// The shop's category tabs: one is raised and drawn in the drawer's own cream so it reads as
    /// the open page, the rest sit back in teal. Picking one shows its shelf through
    /// <see cref="UiItemsWindow.ActivatePanel"/>, the same call the old tab buttons made, and rewinds
    /// the shelf to its start.
    /// </summary>
    public sealed class UiTabGroup : MonoBehaviour
    {
        [Serializable]
        public sealed class Tab
        {
            public Button button;
            public RectTransform rect;
            public Image face;
            public Image icon;
            public TMP_Text label;
            public GameObject content;
        }

        [SerializeField] UiItemsWindow window;
        [SerializeField] ScrollRect scroll;
        [SerializeField] Tab[] tabs = new Tab[0];

        [SerializeField] Color onFace = Color.white;
        [SerializeField] Color offFace = Color.white;
        [SerializeField] Color onLabel = Color.black;
        [SerializeField] Color offLabel = Color.white;

        [Tooltip("How far the open tab stands up out of the row.")]
        [SerializeField] float raise = 8f;

        float[] restY;
        int current = -1;

        void Awake()
        {
            restY = new float[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].rect != null) restY[i] = tabs[i].rect.anchoredPosition.y;
                if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => Select(index));
            }
        }

        void OnEnable()
        {
            int open = Array.FindIndex(tabs, t => t.content != null && t.content.activeSelf);
            if (open < 0)
            {
                current = -1;
                Select(0);
            }
            else
            {
                Show(open);
            }
        }

        public void Select(int index)
        {
            if (index < 0 || index >= tabs.Length) return;

            bool changed = index != current;
            if (window != null && tabs[index].content != null) window.ActivatePanel(tabs[index].content);
            Show(index, animate: changed);

            if (changed && tabs[index].rect != null)
            {
                var motion = tabs[index].rect.GetComponent<HudMotion>();
                if (motion != null) motion.Punch(0.14f);
            }

            if (changed && scroll != null)
            {
                scroll.StopMovement();
                scroll.horizontalNormalizedPosition = 0f;
            }
        }

        void Show(int index, bool animate = false)
        {
            current = index;
            for (int i = 0; i < tabs.Length; i++)
            {
                Tab tab = tabs[i];
                bool on = i == index;
                if (tab.face != null) tab.face.color = on ? onFace : offFace;
                if (tab.label != null) tab.label.color = on ? onLabel : offLabel;
                if (tab.icon != null) tab.icon.color = on ? Color.white : new Color(1f, 1f, 1f, 0.9f);
                if (tab.rect != null && restY != null)
                {
                    float y = restY[i] + (on ? raise : 0f);
                    DOTween.Kill(tab.rect);
                    if (animate)
                    {
                        tab.rect.DOAnchorPosY(y, on ? 0.28f : 0.18f).SetEase(on ? Ease.OutBack : Ease.OutQuad).SetUpdate(true).SetTarget(tab.rect);
                    }
                    else
                    {
                        Vector2 at = tab.rect.anchoredPosition;
                        at.y = y;
                        tab.rect.anchoredPosition = at;
                    }
                }
            }
        }
    }
}
