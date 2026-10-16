using LastOrders.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// The look of one shop card around <see cref="ItemUiButton"/>, which still does the buying: a
    /// gold price tag while the player can pay for it and a pale one with a red price when they
    /// cannot, a MAX stamp once the limit is placed, and a dimmed picture while it is out of reach.
    ///
    /// Items are bought by dragging them onto the land, which nothing used to say: tapping a card
    /// did nothing at all. A tap now answers - the card bounces and the drawer's hint says what to
    /// do, or why this one cannot be had yet.
    /// </summary>
    [RequireComponent(typeof(ItemUiButton))]
    public sealed class ShopCardView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] ItemUiButton item;
        [SerializeField] Image picture;
        [SerializeField] TMP_Text title;
        [SerializeField] Image priceTag;
        [SerializeField] TMP_Text price;
        [SerializeField] GameObject maxStamp;
        [SerializeField] HudMotion motion;

        [SerializeField] Sprite affordableTag;
        [SerializeField] Sprite shortTag;
        [SerializeField] Color affordableText = Color.white;
        [SerializeField] Color shortText = new Color(0.78f, 0.31f, 0.25f);
        [SerializeField] Material affordableMaterial;
        [SerializeField] Material shortMaterial;

        bool drawn, named, unlocked, available, affordable;

        void Reset() => item = GetComponent<ItemUiButton>();

        void OnEnable()
        {
            CurrenciesController.OnCurrencyAmountChanged += CurrencyChanged;
            drawn = false;
            Refresh();
        }

        void OnDisable() => CurrenciesController.OnCurrencyAmountChanged -= CurrencyChanged;

        void CurrencyChanged(CurrencyType type, int amount) => drawn = false;

        void LateUpdate() => Refresh();

        void Refresh()
        {
            PlacedObjectTypeSO data = item != null ? item.Data : null;
            if (data == null) return;

            if (!named && title != null)
            {
                // The old card shows the asset's name ("PlantPot"); show it as words.
                title.text = UiText.Pretty(data.name);
                named = true;
            }

            ShopItem shop = item.Item;
            bool isUnlocked = shop == null || shop.IsUnlocked;
            bool isAvailable = isUnlocked && (shop == null || shop.CanPurchase());
            bool canPay = data.PriceAmount <= 0 || CurrenciesController.HasCurrency(CurrencyType.Coins, data.PriceAmount);

            if (drawn && isUnlocked == unlocked && isAvailable == available && canPay == affordable) return;
            drawn = true;
            unlocked = isUnlocked;
            available = isAvailable;
            affordable = canPay;

            if (priceTag != null) priceTag.sprite = canPay ? affordableTag : shortTag;
            if (price != null)
            {
                price.color = canPay ? affordableText : shortText;
                Material material = canPay ? affordableMaterial : shortMaterial;
                if (material != null) price.fontSharedMaterial = material;
            }

            if (maxStamp != null) maxStamp.SetActive(isUnlocked && !isAvailable);
            if (picture != null) picture.color = isAvailable ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging || item == null || item.Data == null) return;

            ShopDrawerHint hint = GetComponentInParent<ShopDrawerHint>();
            if (!unlocked)
            {
                if (motion != null) motion.Shake(9f);
                hint?.Say($"Unlocks at level {item.Data.requiredLevel}", HudTone.Warning);
            }
            else if (!available)
            {
                if (motion != null) motion.Shake(9f);
                hint?.Say("You have built all of these", HudTone.Warning);
            }
            else if (!affordable)
            {
                if (motion != null) motion.Shake(9f);
                hint?.Say($"Needs {item.Data.PriceAmount} coins", HudTone.Danger);
            }
            else
            {
                if (motion != null) motion.Punch(0.12f);
                hint?.Say("Drag it up onto your land to build it", HudTone.Info);
            }
        }
    }
}
