using LastOrders.UI.Hud;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LastOrders.UI.Kit
{
    /// <summary>
    /// The pay-off for delivering an order: sparkles burst from the Deliver button, then coins fly
    /// up into the wallet and stars into the level badge, one after another.
    ///
    /// Sits on the Deliver button after its <see cref="UnityEngine.UI.Button"/>, so by the time a
    /// tap reaches it the order has already been claimed or refused; it only celebrates a claim.
    /// </summary>
    public sealed class OrderDeliverFx : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Sprite coin;
        [SerializeField] Sprite star;
        [SerializeField] Sprite sparkle;
        [SerializeField] int coins = 8;
        [SerializeField] int stars = 4;

        static int claimedFrame = -1;
        static bool listening;

        void OnEnable()
        {
            if (listening) return;
            listening = true;
            OrdersBoardUI.OnOrderClaimed += () => claimedFrame = Time.frameCount;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (claimedFrame != Time.frameCount) return;

            RectTransform layer = UiFx.Layer(this);
            if (layer == null) return;

            Vector2 from = UiFx.ScreenOf(transform);
            UiFx.Burst(layer, from, sparkle, new Color(1f, 0.9f, 0.5f), 10, 150f, 40f);

            GameplayHud hud = FindFirstObjectByType<GameplayHud>();
            Vector2 wallet = hud != null && hud.wallet.coins != null
                ? UiFx.ScreenOf(hud.wallet.coins.transform)
                : new Vector2(Screen.width * 0.8f, Screen.height * 0.95f);
            Vector2 level = hud != null && hud.profile.level != null
                ? UiFx.ScreenOf(hud.profile.level.transform)
                : new Vector2(Screen.width * 0.05f, Screen.height * 0.95f);

            UiFx.Fly(layer, coin, from, wallet, coins, 64f);
            UiFx.Fly(layer, star, from, level, stars, 60f);
        }
    }
}
