using UnityEngine;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// Which older screen each redesigned one stands in for, kept on the new screens' canvas so the
    /// swap can be undone: the old panels are left in the scene untouched, and every reference the
    /// game held to one of them was pointed at its replacement - restoring points them back.
    /// </summary>
    public sealed class ScreensReplaced : MonoBehaviour
    {
        [Tooltip("The older screens and their parts, in the same order as their replacements.")]
        public Object[] previous = new Object[0];

        public Object[] replacements = new Object[0];

        [Tooltip("The panels the HUD stepped aside for before the swap.")]
        public GameObject[] hudCovering = new GameObject[0];
    }
}
