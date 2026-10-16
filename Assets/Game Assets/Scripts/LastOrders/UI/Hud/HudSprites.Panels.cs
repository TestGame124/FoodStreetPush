namespace LastOrders.UI.Hud
{
    /// <summary>
    /// The sprites the game's screens are built from - the shop drawer, the kitchen, storehouse,
    /// orders and level-up windows, the popups and the world prompts - baked into the same atlas as
    /// the HUD so every screen shares one look.
    /// </summary>
    public static partial class HudSprites
    {
        /// <summary>A cream window with a teal title band baked into its top border.</summary>
        public const string Window = "window";

        /// <summary>A sunken cream area inside a window: lists, grids, input fields.</summary>
        public const string Well = "well";

        /// <summary>A light raised card for one item in a grid or a row.</summary>
        public const string Tile = "tile";

        /// <summary>A tab, rounded on top and flat underneath, white so it takes a tint.</summary>
        public const string Tab = "tab";

        /// <summary>The chunky button and round button in greys, for screens that tint them.</summary>
        public const string ButtonTint = "button_tint";
        public const string RoundTint = "round_tint";

        /// <summary>The diner's checkerboard trim, for a tiled strip.</summary>
        public const string Checker = "checker";

        /// <summary>Soft sun rays, for celebrations.</summary>
        public const string Rays = "rays";

        public const string IconLock = "icon_lock";
        public const string IconCrate = "icon_crate";
        public const string IconSeeds = "icon_seeds";
        public const string IconSprout = "icon_sprout";
        public const string IconTiles = "icon_tiles";
        public const string IconFrame = "icon_frame";
        public const string IconBarrel = "icon_barrel";
        public const string IconHand = "icon_hand";

        public const string GlyphRotate = "glyph_rotate";
        public const string GlyphTrash = "glyph_trash";
        public const string GlyphMinus = "glyph_minus";
        public const string GlyphNext = "glyph_next";
        public const string GlyphLock = "glyph_lock";
        public const string GlyphSkip = "glyph_skip";
    }
}
