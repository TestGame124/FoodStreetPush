using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LastOrders.UI.Hud
{
    /// <summary>The names of the sprites in the HUD atlas, shared by the baker, the builder and the HUD.</summary>
    public static class HudSprites
    {
        public const string Card = "card";
        public const string CardShadow = "card_shadow";
        public const string Glass = "glass";
        public const string GlassCard = "glass_card";
        public const string CreamPill = "cream_pill";

        public const string ButtonTeal = "button_teal";
        public const string ButtonRed = "button_red";
        public const string ButtonGold = "button_gold";
        public const string ButtonCream = "button_cream";
        public const string ButtonGrey = "button_grey";

        public const string RoundCream = "round_cream";
        public const string RoundTeal = "round_teal";
        public const string RoundRed = "round_red";
        public const string RoundGold = "round_gold";
        public const string RoundGlass = "round_glass";

        public const string Badge = "badge";
        public const string Disc = "disc";
        public const string DiscShaded = "disc_shaded";
        public const string BarTrack = "bar_track";
        public const string BarFill = "bar_fill";
        public const string Ring = "ring";
        public const string Glow = "glow";
        public const string Vignette = "vignette";
        public const string Ribbon = "ribbon";
        public const string Chip = "chip";
        public const string Segment = "segment";
        public const string White = "white";
        public const string AlertPointer = "alert_pointer";

        public const string IconCoin = "icon_coin";
        public const string IconGem = "icon_gem";
        public const string IconStar = "icon_star";
        public const string IconWood = "icon_wood";
        public const string IconScrap = "icon_scrap";
        public const string IconKit = "icon_kit";
        public const string IconWrench = "icon_wrench";
        public const string IconShield = "icon_shield";
        public const string IconDiner = "icon_diner";
        public const string IconZombie = "icon_zombie";
        public const string IconSun = "icon_sun";
        public const string IconMoon = "icon_moon";
        public const string IconDawn = "icon_dawn";
        public const string IconClock = "icon_clock";
        public const string IconShop = "icon_shop";
        public const string IconOrders = "icon_orders";
        public const string IconGear = "icon_gear";
        public const string IconTrophy = "icon_trophy";
        public const string IconCook = "icon_cook";
        public const string IconServe = "icon_serve";
        public const string IconClear = "icon_clear";
        public const string IconVisitors = "icon_visitors";
        public const string IconMedic = "icon_medic";
        public const string IconFlag = "icon_flag";
        public const string IconHammer = "icon_hammer";

        public const string GlyphPlus = "glyph_plus";
        public const string GlyphCheck = "glyph_check";
        public const string GlyphCross = "glyph_cross";
        public const string GlyphPlay = "glyph_play";
        public const string GlyphInfo = "glyph_info";
        public const string GlyphAlert = "glyph_alert";
        public const string GlyphUp = "glyph_up";
        public const string GlyphDown = "glyph_down";
    }

    /// <summary>
    /// Everything the gameplay HUD is drawn with: the baked atlas's sprites by name, the two fonts
    /// and their HUD materials. Written by 'Last Orders / UI / Bake HUD Art'.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/UI/HUD Skin", fileName = "HudSkin")]
    public sealed class HudSkin : ScriptableObject
    {
        public Sprite[] sprites = new Sprite[0];

        [Header("Type")]
        [Tooltip("Numbers and headings: a chunky rounded display face.")]
        public TMP_FontAsset display;

        [Tooltip("Sentences: the objective, toasts, the result card.")]
        public TMP_FontAsset body;

        [Tooltip("White display text with a warm outline and a soft drop, for text over the world.")]
        public Material displayOutline;

        [Tooltip("Display text with only the soft drop, for text on coloured buttons.")]
        public Material displayShadow;

        [Tooltip("Body text with a soft drop, for white sentences on dark glass.")]
        public Material bodyShadow;

        Dictionary<string, Sprite> byName;

        public Sprite this[string name] => Get(name);

        public Sprite Get(string name)
        {
            if (byName == null || byName.Count != sprites.Length) Rebuild();

            if (byName.TryGetValue(name, out Sprite sprite)) return sprite;

            Debug.LogWarning($"[HudSkin] No sprite '{name}' in {this.name}. Re-bake the HUD art.", this);
            return null;
        }

        public void Rebuild()
        {
            byName = new Dictionary<string, Sprite>();
            foreach (Sprite s in sprites)
            {
                if (s != null) byName[s.name] = s;
            }
        }

        void OnEnable() => byName = null;
    }
}
