using UnityEngine;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// The pieces of the previous HUD the gameplay HUD switched off when it was built - the old
    /// wallet, shop and orders buttons, the grey-box CompoundHUD - kept so 'Restore Previous HUD'
    /// can switch them back on. Nothing is deleted.
    /// </summary>
    public sealed class HudReplaced : MonoBehaviour
    {
        public GameObject[] objects = new GameObject[0];
    }
}
