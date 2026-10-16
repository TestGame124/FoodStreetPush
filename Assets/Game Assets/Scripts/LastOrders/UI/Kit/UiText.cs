using System.Text;

namespace LastOrders.UI.Kit
{
    /// <summary>Text helpers for showing the game's asset names to a player.</summary>
    public static class UiText
    {
        /// <summary>
        /// "PlantPot" or "plant_pot" as "Plant Pot": asset names are what the older screens show,
        /// and they are written for code, not for people.
        /// </summary>
        public static string Pretty(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            var sb = new StringBuilder(name.Length + 4);
            char previous = ' ';
            foreach (char raw in name.Trim())
            {
                char c = raw == '_' || raw == '-' ? ' ' : raw;
                bool split = char.IsUpper(c) && (char.IsLower(previous) || char.IsDigit(previous)) ||
                             char.IsDigit(c) && char.IsLetter(previous);
                if (split) sb.Append(' ');
                if (c == ' ' && previous == ' ') continue;

                sb.Append(sb.Length == 0 || previous == ' ' ? char.ToUpperInvariant(c) : c);
                previous = c;
            }

            return sb.ToString();
        }
    }
}
