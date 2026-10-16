using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// The look of one recipe card around <see cref="RecipeUiBox"/>, which still does the cooking:
    /// each ingredient's count turns green once there is enough of it and red while there is not,
    /// and a tick (or the Cook button's colour) says when the dish can be made - so the player
    /// sees what is missing before trying, not after.
    /// </summary>
    [RequireComponent(typeof(RecipeUiBox))]
    public sealed class RecipeCardView : MonoBehaviour
    {
        [SerializeField] RecipeUiBox box;
        [SerializeField] TMP_Text title;

        [Tooltip("The dish picture, dimmed while it cannot be cooked.")]
        [SerializeField] Image picture;

        [Tooltip("A tick that shows on cards that can be cooked right now.")]
        [SerializeField] GameObject readyMark;

        [SerializeField] Image cookFace;
        [SerializeField] TMP_Text cookLabel;
        [SerializeField] Sprite readySprite;
        [SerializeField] Sprite blockedSprite;

        [SerializeField] Color enoughColour = new Color(0.20f, 0.52f, 0.28f);
        [SerializeField] Color shortColour = new Color(0.78f, 0.31f, 0.25f);

        bool dirty = true;
        bool? wasReady;

        void Reset() => box = GetComponent<RecipeUiBox>();

        void OnEnable()
        {
            InventorySystem.onInventoryChangedCallback += MarkDirty;
            CurrenciesController.OnCurrencyAmountChanged += CurrencyChanged;
            LevelUpSystem.OnLevelUp += LevelChanged;
            dirty = true;
        }

        void OnDisable()
        {
            InventorySystem.onInventoryChangedCallback -= MarkDirty;
            CurrenciesController.OnCurrencyAmountChanged -= CurrencyChanged;
            LevelUpSystem.OnLevelUp -= LevelChanged;
        }

        void CurrencyChanged(CurrencyType type, int amount) => MarkDirty();
        void LevelChanged(int level) => MarkDirty();

        void MarkDirty() => dirty = true;

        void LateUpdate()
        {
            if (!dirty || box == null || box.Recipe == null || box.Inventory == null) return;
            dirty = false;

            CookingRecipe recipe = box.Recipe;
            InventorySystem inventory = box.Inventory;
            if (box.TimerText != null) box.TimerText.text = LevelUpSystem.GetLevel() < recipe.requiredLevel
                ? $"Lv {recipe.requiredLevel}" : UiSecondsText.Format(recipe.TotalTimeToCook);

            if (title != null)
            {
                ItemDataSO dish = recipe.Results.item != null ? recipe.Results.item.itemDataSO : null;
                title.text = UiText.Pretty(dish != null && !string.IsNullOrWhiteSpace(dish.Name) ? dish.Name : recipe.name);
            }

            if (box.ingredientsArea != null)
            {
                int i = 0;
                foreach (Transform child in box.ingredientsArea)
                {
                    if (i >= recipe.Ingrediants.Count) break;
                    var slot = child.GetComponent<ItemSlotUI>();
                    if (slot == null || slot.amountText == null) continue;

                    ItemAmount need = recipe.Ingrediants[i++];
                    bool enough = inventory.ItemCount(need.item.type) >= need.Amount;
                    slot.amountText.color = enough ? enoughColour : shortColour;
                    int missing = Mathf.Max(0, need.Amount - inventory.ItemCount(need.item.type));
                    int unitPrice = LastOrders.Economy.EconomyRules.SupplyPrice(need.item.type);
                    if (missing > 0 && unitPrice > 0) slot.amountText.text = $"Buy {missing} · {missing * unitPrice} Coins";
                }
            }

            bool ready = recipe.CanCook(inventory);
            if (picture != null) picture.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            if (readyMark != null)
            {
                readyMark.SetActive(ready);
                if (ready && wasReady == false)
                {
                    DOTween.Kill(readyMark.transform, true);
                    readyMark.transform.DOPunchScale(Vector3.one * 0.6f, 0.4f, 6, 0.5f).SetUpdate(true).SetTarget(readyMark.transform);
                }
            }

            wasReady = ready;
            if (cookFace != null) cookFace.sprite = ready ? readySprite : blockedSprite;
            if (cookLabel != null)
            {
                cookLabel.alpha = ready ? 1f : 0.85f;
                int supply = recipe.SupplyCost(inventory);
                cookLabel.text = LevelUpSystem.GetLevel() < recipe.requiredLevel ? $"Level {recipe.requiredLevel}" :
                    supply > 0 ? $"Cook · {supply} Coins" : "Cook";
            }
        }
    }
}
