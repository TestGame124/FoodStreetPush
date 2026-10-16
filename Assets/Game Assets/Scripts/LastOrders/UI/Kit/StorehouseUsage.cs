using LastOrders.UI.Hud;
using TMPro;
using UnityEngine;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// How full the storehouse is, over its item grid: kinds stored out of the slots it has, as a
    /// bar that turns amber and then red as it fills - a full storehouse is why a harvest or a cook
    /// suddenly stops, and nothing on screen used to say so.
    /// </summary>
    public sealed class StorehouseUsage : MonoBehaviour
    {
        [SerializeField] HudBar bar;
        [SerializeField] TMP_Text label;

        void OnEnable()
        {
            InventorySystem.onInventoryChangedCallback += Refresh;
            Refresh();
        }

        void OnDisable() => InventorySystem.onInventoryChangedCallback -= Refresh;

        void Refresh()
        {
            InventorySystem inventory = InventorySystem.instance;
            if (inventory == null) return;

            int used = inventory.GetItemList().Count;
            int size = Mathf.Max(1, inventory.inventorySize);
            float t = Mathf.Clamp01((float)used / size);

            if (label != null) label.text = used >= size ? $"Full  {used}/{size}" : $"{used}/{size} slots";
            if (bar == null) return;
            bar.Set(t);
            bar.SetColour(HudPalette.Meter(1f - t));
        }
    }
}
