using System;
using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.Housing;
using LastOrders.UI.Popups;
using LastOrders.UI.World;

namespace LastOrders.Defense
{
    /// <summary>One thing a site can be built as, as the pick-one popup shows it.</summary>
    public readonly struct BuildChoice
    {
        /// <summary>"Barricade\n10 Wood + 4 Scrap".</summary>
        public readonly string Label;

        public readonly bool Affordable;

        /// <summary>What is missing, when it cannot be paid. Null when it can.</summary>
        public readonly string ShortBy;

        public BuildChoice(string label, bool affordable, string shortBy)
        {
            Label = label;
            Affordable = affordable;
            ShortBy = shortBy;
        }
    }

    /// <summary>
    /// Everything the popup over a site that takes a choice shows: what each option does, a button
    /// each, and why none can be built when none can. A strip slot's barricade, trap or wire; a wall
    /// site's turret, toaster or corn shooter.
    /// </summary>
    public sealed class BuildChoiceOffer
    {
        public string Title;
        public string Description;
        public string Status;

        public readonly List<string> Details = new List<string>();
        public readonly List<BuildChoice> Options = new List<BuildChoice>();

        /// <summary>Why nothing can be built right now, whatever the player has. Null otherwise.</summary>
        public string Blocked;
    }

    /// <summary>
    /// The pick-one popup for a site that takes a choice. The shelters' popup is the game's panel
    /// with a title, a few lines and a row of up to three buttons, so it is the one shown; this only
    /// turns a <see cref="BuildChoiceOffer"/> into the offer it reads.
    /// </summary>
    public static class BuildChoicePopup
    {
        /// <summary>Opens it for <paramref name="owner"/>. False when there is no popup to open.</summary>
        public static bool Open(GameComposition composition, object owner, Func<BuildChoiceOffer> offer, Action<int> take)
        {
            WorldUiCatalog catalog = composition.WorldUi.Catalog;
            ShelterPopupView prefab = catalog != null ? catalog.shelterPopup : null;
            if (prefab == null) return false;

            ShelterPopupView popup = composition.Popups.Show(prefab);
            if (popup == null) return false;

            popup.Bind(owner, () => Convert(offer()), take);
            return true;
        }

        /// <summary>Closes it, if it is open and <paramref name="owner"/>'s.</summary>
        public static void Close(GameComposition composition, object owner)
        {
            if (composition.Popups.Current is ShelterPopupView open && open.Owner == owner) composition.Popups.Hide();
        }

        static ShelterOffer Convert(BuildChoiceOffer source)
        {
            var offer = new ShelterOffer
            {
                Title = source.Title,
                Description = source.Description,
                Status = source.Status,
                Blocked = source.Blocked,
            };

            offer.Details.AddRange(source.Details);
            foreach (BuildChoice option in source.Options)
                offer.Options.Add(new ShelterOption(option.Label, option.Affordable, option.ShortBy));

            return offer;
        }
    }

    /// <summary>Picking from a list of options that may have gaps in it, the way the popup counts them.</summary>
    internal static class BuildChoices
    {
        /// <summary>The <paramref name="index"/>th option that is set, counting only those.</summary>
        public static T Pick<T>(IReadOnlyList<T> options, int index) where T : class
        {
            if (options == null || index < 0) return null;

            for (int i = 0, n = 0; i < options.Count; i++)
            {
                if (options[i] == null) continue;
                if (n++ == index) return options[i];
            }

            return null;
        }
    }
}
