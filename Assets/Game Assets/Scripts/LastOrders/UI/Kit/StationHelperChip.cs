using LastOrders.Crew;
using LastOrders.Interaction;
using LastOrders.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// A pill over the cooking slots that offers the crew's help at the station being cooked at: "Mia:
    /// help here +15%". Tapped, she walks over and the station cooks faster while she stays; then it
    /// reads "Mia is helping +15%" in teal, and a tap sends her back to the tables. Hidden when there is
    /// nobody who can help.
    ///
    /// The same as dragging her onto the station in the world (<see cref="CrewHandle"/>) - both go
    /// through <see cref="StationAssist"/>.
    /// </summary>
    public sealed class StationHelperChip : MonoBehaviour
    {
        [SerializeField] KitchenStationView view;
        [SerializeField] Button button;
        [SerializeField] Image face;
        [SerializeField] TMP_Text label;
        [SerializeField] Image cross;
        [SerializeField] HudMotion motion;
        [SerializeField] CanvasGroup group;

        [Tooltip("The pill's face while offering help, and while helping.")]
        [SerializeField] Sprite offerFace, helpingFace;

        [SerializeField] Sprite sparkle;

        StationAssist crew;
        int drawn = -1;

        void Awake()
        {
            if (button != null) button.onClick.AddListener(Clicked);
        }

        void LateUpdate()
        {
            CookeryStation station = view != null ? view.Station : null;
            crew = Find();

            int state = station == null || crew == null ? 0 : crew.Station != station ? 1 : crew.IsHelping ? 3 : 2;
            if (group != null)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, state == 0 ? 0f : 1f, Time.unscaledDeltaTime * 6f);
                group.interactable = group.blocksRaycasts = state != 0;
            }

            if (state == drawn || state == 0) return;
            drawn = state;

            string who = crew.Brain.DisplayName;
            int boost = Mathf.RoundToInt(crew.Boost * 100f);
            if (label != null)
            {
                label.text = state == 1 ? $"{who}: help here  +{boost}%"
                           : state == 2 ? $"{who} is coming over..."
                           : $"{who} is helping  +{boost}%";
                label.color = state == 1 ? HudPalette.Ink : Color.white;
            }

            if (face != null) face.sprite = state == 1 ? offerFace : helpingFace;
            if (cross != null) cross.enabled = state != 1;
            if (motion != null && state == 3) motion.Punch(0.18f);
        }

        void Clicked()
        {
            CookeryStation station = view != null ? view.Station : null;
            if (crew == null || station == null) return;

            if (crew.Station == station)
            {
                crew.Release();
                TapFeedback.Report($"{crew.Brain.DisplayName} is back on the floor, serving and tidying up.");
            }
            else
            {
                crew.AssignTo(station);
                if (motion != null) motion.Punch(0.2f);
                RectTransform layer = UiFx.Layer(this);
                if (layer != null && sparkle != null) UiFx.Burst(layer, UiFx.ScreenOf(transform), sparkle, HudPalette.Gold, 8, 120f, 30f);
            }

            drawn = -1;
        }

        static StationAssist Find()
        {
            for (int i = 0; i < CrewBrain.All.Count; i++)
            {
                StationAssist assist = CrewBrain.All[i] != null ? CrewBrain.All[i].Duty<StationAssist>() : null;
                if (assist != null && assist.isActiveAndEnabled) return assist;
            }

            return null;
        }
    }
}
