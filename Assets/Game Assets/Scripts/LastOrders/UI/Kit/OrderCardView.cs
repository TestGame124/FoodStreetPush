using LastOrders.UI.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// The look of one order ticket around <see cref="OrdersBoardUI"/>, which still claims and
    /// renews it: every dish the order asks for shows green once the storehouse holds enough of it
    /// and red while it does not, a tick lands on each one that is ready, unused dish spots are
    /// hidden, and the Deliver button lights up gold only when the whole order can go.
    /// </summary>
    [RequireComponent(typeof(OrdersBoardUI))]
    public sealed class OrderCardView : MonoBehaviour
    {
        [SerializeField] OrdersBoardUI board;
        [SerializeField] Image[] wells = new Image[0];
        [SerializeField] GameObject[] ticks = new GameObject[0];
        [SerializeField] Image deliverFace;
        [SerializeField] HudMotion deliverMotion;
        [SerializeField] Sprite readySprite;
        [SerializeField] Sprite blockedSprite;

        [SerializeField] Color enoughColour = new Color(0.20f, 0.52f, 0.28f);
        [SerializeField] Color shortColour = new Color(0.78f, 0.31f, 0.25f);

        bool dirty = true;
        bool claimable;
        OrderSystem.Order shown;

        void Reset() => board = GetComponent<OrdersBoardUI>();

        void OnEnable()
        {
            InventorySystem.onInventoryChangedCallback += MarkDirty;
            dirty = true;
        }

        void OnDisable() => InventorySystem.onInventoryChangedCallback -= MarkDirty;

        void MarkDirty() => dirty = true;

        void LateUpdate()
        {
            if (board == null) return;

            OrderSystem.Order order = board.ActiveOrder;
            if (order != shown)
            {
                shown = order;
                dirty = true;
            }

            if (!dirty || order == null || board.Inventory == null) return;
            dirty = false;

            InventorySystem inventory = board.Inventory;
            for (int i = 0; i < wells.Length; i++)
            {
                bool used = i < order.ordersData.Count;
                // Unused spots are switched off so the grid centres the dishes there are; the order
                // script only fills spots up to the order's length, so nothing lands in one that is off.
                if (wells[i] != null && wells[i].gameObject.activeSelf != used) wells[i].gameObject.SetActive(used);

                bool enough = false;
                if (used)
                {
                    Item want = order.ordersData[i];
                    enough = inventory.ItemCount(want.type) >= want.itemsAmount;

                    var slot = wells[i] != null ? wells[i].GetComponentInChildren<ItemSlotUI>() : null;
                    if (slot != null && slot.amountText != null) slot.amountText.color = enough ? enoughColour : shortColour;
                }

                if (i < ticks.Length && ticks[i] != null)
                {
                    ticks[i].SetActive(used && enough);
                    ticks[i].transform.SetAsLastSibling();
                }
            }

            bool canClaim = order.CanClaim(inventory);
            if (deliverFace != null) deliverFace.sprite = canClaim ? readySprite : blockedSprite;
            if (deliverMotion != null)
            {
                if (canClaim && !claimable) deliverMotion.Punch(0.18f);
                deliverMotion.Breathe = canClaim;
            }

            claimable = canClaim;
        }
    }
}
