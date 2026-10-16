using UnityEngine;

namespace LastOrders.UI.Kit
{
    /// <summary>Turns slowly and steadily, on unscaled time: the rays behind a celebration.</summary>
    public sealed class UiSpin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = -12f;

        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
