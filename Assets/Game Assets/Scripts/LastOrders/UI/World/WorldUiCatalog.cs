using UnityEngine;

namespace LastOrders.UI.World
{
    /// <summary>
    /// The UI prefabs, one per kind of element - the world-space ones and where each sits
    /// relative to the thing it is about, and the screen popups.
    ///
    /// One asset rather than prefab fields scattered over every component that shows UI, so
    /// restyling the action button is swapping one reference - and every piece of debris,
    /// now and later, picks it up.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/UI/World UI Catalog", fileName = "WorldUiCatalog")]
    public sealed class WorldUiCatalog : ScriptableObject
    {
        [Header("Prefabs")]
        [Tooltip("The tap-to-act button. The hammer, by default.")]
        public ActionPromptView actionPrompt;

        [Tooltip("The timer bar shown over work in progress.")]
        public WorkProgressView workProgress;

        [Tooltip("The rising \"+2 XP\" text.")]
        public FloatingTextView floatingText;

        [Tooltip("The standing button over cleared land that opens the expand popup.")]
        public ActionPromptView expandPrompt;

        [Tooltip("The timer over land while its new boundary is built.")]
        public WorkProgressView expansionProgress;

        [Tooltip("The health bar over a worker while they fight.")]
        public HealthBarView healthBar;

        [Tooltip("The standing wrench over anything broken. Pressing it sends a worker to fix it.")]
        public ActionPromptView repairPrompt;

        [Tooltip("The card a tap on a growing crop opens: the progress bar, the time left and the gem button that finishes it now.")]
        public CropCardView cropCard;

        [Header("Popups (screen space)")]
        [Tooltip("What expanding into a piece of land needs.")]
        public Popups.ExpansionPopupView expansionPopup;

        [Tooltip("Buying a defense for an empty site, or upgrading one already built.")]
        public Popups.DefensePopupView defensePopup;

        [Tooltip("Building a shelter on an empty site, or hiring someone into a free bed.")]
        public Popups.ShelterPopupView shelterPopup;

        [Tooltip("A worker's info panel - health, what they are doing, Recall Worker - and the " +
                 "panel for sending someone to clear an area.")]
        public Popups.WorkerPanelView workerPanel;

        [Tooltip("\"Expand the restaurant?\" - what the next expansion costs, and yes or no.")]
        public Popups.RestaurantExpansionPopupView restaurantExpansionPopup;

        [Tooltip("Feeding a chicken coop or cow shelter: how many eggs or churns to make, and the feed it costs.")]
        public Popups.FeedPopupView feedPopup;

        [Header("Discovery sites")]
        [Tooltip("The standing button over a discovery site that has been found: opens what it does.")]
        public ActionPromptView sitePrompt;

        [Tooltip("The standing button over a discovery site still to be found: says which plot finds it.")]
        public ActionPromptView mysteryPrompt;

        [Tooltip("The standing button over the noise maker in an attack: sounds it.")]
        public ActionPromptView alarmPrompt;

        [Tooltip("The drive-through window: the cars in the lane, their bags, and the dishes on the pass to pack them with.")]
        public Popups.TakeawayPopupView takeawayPopup;

        [Tooltip("The food truck: what to load from the pass, and where on the little map to send it.")]
        public Popups.FoodTruckPopupView foodTruckPopup;

        [Header("Placement (metres above the anchor)")]
        public Vector3 promptOffset = new Vector3(0f, 0.9f, 0f);
        public Vector3 progressOffset = new Vector3(0f, 0.4f, 0f);
        public Vector3 floatingTextOffset = new Vector3(0f, 0.8f, 0f);
        public Vector3 expandPromptOffset = new Vector3(0f, 1.2f, 0f);
        public Vector3 expansionProgressOffset = new Vector3(0f, 2.2f, 0f);

        [Tooltip("Above the top of whatever is broken.")]
        public Vector3 repairPromptOffset = new Vector3(0f, 0.9f, 0f);

        [Tooltip("Above a worker's feet: clear of the head and of a hammer held overhead.")]
        public Vector3 healthBarOffset = new Vector3(0f, 2.35f, 0f);

        [Tooltip("Above the middle of a pot's soil, for the tap card: clear of the crop.")]
        public Vector3 cropCardOffset = new Vector3(0f, 1.25f, 0f);

        [Header("Colours")]
        public Color experienceTextColour = new Color(1f, 0.86f, 0.3f);

        [Tooltip("Hits taken, and the low-health warning.")]
        public Color dangerTextColour = new Color(1f, 0.38f, 0.3f);
    }
}
