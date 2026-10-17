using UnityEngine;

namespace LastOrders.Economy
{
    /// <summary>What can be earned and fly to its counter on the HUD.</summary>
    public enum RewardKind
    {
        Coins,
        Gems,

        /// <summary>Experience: the star on the level badge.</summary>
        Stars,
    }

    /// <summary>
    /// Where the next reward came from, so the coins, gems and stars can be seen to fly from there to
    /// the counters. Whoever pays says where, just before paying - a diner's head, a station's pass, a
    /// crop - and anything earned in the same frame flies from that point:
    ///
    /// <code>RewardSource.At(visitor.transform.position + Vector3.up * 2f);
    /// CurrenciesController.AddCurrency(CurrencyType.Coins, payment);</code>
    ///
    /// A reward with no source said - a level-up bonus, the morning's results - just counts up, as before.
    /// The wallets stay as they are: this only says where, and the HUD decides whether to fly anything.
    /// </summary>
    public static class RewardSource
    {
        static Vector3 world;
        static Vector2 screen;
        static bool onScreen;
        static int frame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => frame = -1;

        /// <summary>Rewards earned this frame fly from <paramref name="position"/> in the world.</summary>
        public static void At(Vector3 position)
        {
            world = position;
            onScreen = false;
            frame = Time.frameCount;
        }

        /// <summary>Rewards earned this frame fly from <paramref name="point"/> on the screen - a button.</summary>
        public static void AtScreen(Vector2 point)
        {
            screen = point;
            onScreen = true;
            frame = Time.frameCount;
        }

        /// <summary>Nothing earned from here on this frame flies.</summary>
        public static void Clear() => frame = -1;

        /// <summary>
        /// Where on the screen this frame's rewards come from, if a source was given this frame and it is
        /// in front of the camera. A point off the edge is pulled in to it.
        /// </summary>
        public static bool TryGetScreen(out Vector2 point)
        {
            point = default;
            if (frame != Time.frameCount) return false;

            if (onScreen)
            {
                point = screen;
                return true;
            }

            Camera eye = Camera.main;
            if (eye == null) return false;

            Vector3 p = eye.WorldToScreenPoint(world);
            if (p.z <= 0f) return false;

            float margin = Mathf.Min(Screen.width, Screen.height) * 0.06f;
            point = new Vector2(Mathf.Clamp(p.x, margin, Screen.width - margin), Mathf.Clamp(p.y, margin, Screen.height - margin));
            return true;
        }
    }
}
