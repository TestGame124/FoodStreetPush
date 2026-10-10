using UnityEngine;

namespace LastOrders.UI.Hud
{
    /// <summary>
    /// The gameplay HUD's colours, shared by the art baker and the runtime so a tint set in code
    /// and a colour baked into a sprite can never drift apart.
    ///
    /// Taken from the diner itself rather than invented: the off-white and teal livery, the red
    /// rail, the oak furniture, the warm dark outline every item icon already has. Colour comes from
    /// the objects; there is no global tint, so the HUD sits in the neutral daylight the world is lit
    /// by instead of washing it orange.
    /// </summary>
    public static class HudPalette
    {
        /// <summary>The warm dark outline of the item icons (<c>CropIcons</c>), used on every HUD edge.</summary>
        public static readonly Color Outline = new Color(0.23f, 0.15f, 0.09f);

        /// <summary>Text on cream: the outline's colour, a touch lighter.</summary>
        public static readonly Color Ink = new Color(0.27f, 0.19f, 0.13f);
        public static readonly Color InkSoft = new Color(0.47f, 0.39f, 0.32f);

        public static readonly Color Cream = new Color(0.98f, 0.96f, 0.91f);
        public static readonly Color CreamShade = new Color(0.93f, 0.89f, 0.81f);
        public static readonly Color CreamLip = new Color(0.79f, 0.71f, 0.59f);

        public static readonly Color Glass = new Color(0.16f, 0.14f, 0.12f, 0.84f);

        public static readonly Color Teal = new Color(0.22f, 0.60f, 0.57f);
        public static readonly Color TealLight = new Color(0.38f, 0.74f, 0.69f);
        public static readonly Color TealDark = new Color(0.13f, 0.39f, 0.37f);

        public static readonly Color Red = new Color(0.78f, 0.31f, 0.25f);
        public static readonly Color RedDark = new Color(0.50f, 0.17f, 0.13f);

        public static readonly Color Gold = new Color(0.96f, 0.75f, 0.27f);
        public static readonly Color GoldDark = new Color(0.66f, 0.44f, 0.11f);

        public static readonly Color Mint = new Color(0.42f, 0.70f, 0.42f);
        public static readonly Color Amber = new Color(0.92f, 0.60f, 0.24f);
        public static readonly Color Rose = new Color(0.86f, 0.36f, 0.45f);
        public static readonly Color Night = new Color(0.36f, 0.45f, 0.62f);
        public static readonly Color Steel = new Color(0.62f, 0.66f, 0.70f);

        public static readonly Color Disabled = new Color(0.66f, 0.63f, 0.59f);

        /// <summary>The bar tints along a 0-1 scale: red when low, amber in the middle, green when high.</summary>
        public static Color Meter(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? Color.Lerp(Red, Amber, t * 2f) : Color.Lerp(Amber, Mint, (t - 0.5f) * 2f);
        }

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
