using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Names the item a slot is showing. The storehouse and seed tray only hand their slots a
    /// picture, so this looks the picture up in the item database and writes the item's name under
    /// it - a cabbage icon is easy to read, but "Flour" and "Wheat" are not.
    /// </summary>
    public sealed class UiItemName : MonoBehaviour
    {
        [SerializeField] Image picture;
        [SerializeField] TMP_Text label;

        static Dictionary<Sprite, string> names;

        Sprite shown;

        void OnEnable() => shown = null;

        void LateUpdate()
        {
            if (picture == null || label == null || picture.sprite == shown) return;
            shown = picture.sprite;
            label.text = NameOf(shown);
        }

        static string NameOf(Sprite sprite)
        {
            if (sprite == null) return string.Empty;

            if (names == null || names.Count == 0)
            {
                names = new Dictionary<Sprite, string>();
                ItemController items = ItemController.instance;
                if (items != null && items.itemsDatabase != null)
                {
                    foreach (ItemDataSO item in items.itemsDatabase.AllItems)
                    {
                        if (item == null || item.itemImage == null || names.ContainsKey(item.itemImage)) continue;
                        names[item.itemImage] = UiText.Pretty(string.IsNullOrWhiteSpace(item.Name) ? item.name : item.Name);
                    }
                }
            }

            return names.TryGetValue(sprite, out string name) ? name : string.Empty;
        }
    }
}
