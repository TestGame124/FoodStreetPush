using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Expansion;
using LastOrders.Progression;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>The flag a discovery is recorded under, built in one place so nothing spells it differently.</summary>
    public static class DiscoveryIds
    {
        public static string Discovered(string siteId) => $"site_discovered:{siteId}";
    }

    /// <summary>
    /// Which discovery sites have been found.
    ///
    /// A site is found when the plot it stands on is claimed - nothing else finds it - and the
    /// find is a progression flag, like the claim itself, so it is made once and every system
    /// asking whether the drive-through works gets the same answer. Knows nothing about the scene:
    /// the site in the world registers with it and draws whatever it says.
    /// </summary>
    public sealed class DiscoveryService
    {
        readonly ProgressionService progression;
        readonly ExpansionService expansion;
        readonly Dictionary<string, DiscoverySiteDefinition> sites = new Dictionary<string, DiscoverySiteDefinition>();
        readonly List<DiscoverySiteDefinition> found = new List<DiscoverySiteDefinition>();

        /// <summary>
        /// A site was found. True when it was found just now, by claiming its plot - worth a fanfare -
        /// and false when it was already found and is only being registered as it stands.
        /// </summary>
        public event Action<DiscoverySiteDefinition, bool> OnDiscovered;

        public DiscoveryService(ProgressionService progression, ExpansionService expansion)
        {
            this.progression = progression;
            this.expansion = expansion;

            if (expansion == null) return;

            expansion.OnAreaClaimed += HandleClaimed;
            expansion.OnAreaStateChanged += HandleStateChanged;
        }

        /// <summary>
        /// Adds a site. One whose plot is already claimed - or which was found before - counts as
        /// found at once, quietly.
        /// </summary>
        public void Register(DiscoverySiteDefinition site)
        {
            if (site == null || string.IsNullOrEmpty(site.id))
            {
                Debug.LogError("[Discovery] A site was registered with no definition or no id.");
                return;
            }

            if (sites.TryGetValue(site.id, out DiscoverySiteDefinition existing))
            {
                if (existing != site)
                    Debug.LogError($"[Discovery] Two sites share the id '{site.id}'. The second was ignored.");
                return;
            }

            sites.Add(site.id, site);

            if (site.area == null)
            {
                Debug.LogWarning($"[Discovery] '{site.id}' stands on no plot, so nothing will ever find it.");
                return;
            }

            if (IsDiscovered(site.id) || (expansion != null && expansion.StateOf(site.area.id) == AreaState.Claimed))
                Discover(site, announce: false);
        }

        public bool IsRegistered(string siteId) => !string.IsNullOrEmpty(siteId) && sites.ContainsKey(siteId);

        public bool IsDiscovered(string siteId) => progression.Has(DiscoveryIds.Discovered(siteId));

        /// <summary>Whether a site doing <paramref name="feature"/> has been found.</summary>
        public bool HasFeature(DiscoveryFeature feature)
        {
            foreach (DiscoverySiteDefinition site in sites.Values)
                if (site.feature == feature && IsDiscovered(site.id)) return true;

            return false;
        }

        public DiscoverySiteDefinition Find(string siteId) =>
            !string.IsNullOrEmpty(siteId) && sites.TryGetValue(siteId, out DiscoverySiteDefinition site) ? site : null;

        /// <summary>
        /// What a site not yet found says when it is tapped: what it is, and which plot to claim.
        /// Null once it is found.
        /// </summary>
        public string WhyUndiscovered(string siteId)
        {
            DiscoverySiteDefinition site = Find(siteId);
            if (site == null || IsDiscovered(siteId)) return null;

            string plot = site.area != null ? site.area.displayName : "the land it stands on";
            return $"{site.displayName}. Claim the {plot} to put it back to work: {site.featureName.ToLowerInvariant()}.";
        }

        /// <summary>
        /// Finds a site whatever its plot says - for the designer's debug menu, and for tests.
        /// Returns false if it was already found.
        /// </summary>
        public bool Discover(string siteId, bool announce = true)
        {
            DiscoverySiteDefinition site = Find(siteId);
            return site != null && Discover(site, announce);
        }

        bool Discover(DiscoverySiteDefinition site, bool announce)
        {
            bool first = progression.Set(DiscoveryIds.Discovered(site.id));

            // Raised quietly for a site registering as already found, so whatever draws it is told
            // even though the flag was set long ago - but only once per site per session.
            if (!first && found.Contains(site)) return false;

            if (!found.Contains(site)) found.Add(site);
            OnDiscovered?.Invoke(site, announce && first);
            return first;
        }

        void HandleClaimed(string areaId)
        {
            foreach (DiscoverySiteDefinition site in sites.Values)
                if (site.area != null && site.area.id == areaId) Discover(site, announce: true);
        }

        /// <summary>
        /// A plot came back as claimed: its sites are found, quietly. A plot being claimed right now
        /// passes through here too - before its claim is recorded - and is left to
        /// <see cref="HandleClaimed"/>, which announces it.
        /// </summary>
        void HandleStateChanged(string areaId, AreaState state)
        {
            if (state != AreaState.Claimed || !progression.Has(ExpansionIds.AreaClaimed(areaId))) return;

            foreach (DiscoverySiteDefinition site in sites.Values)
                if (site.area != null && site.area.id == areaId) Discover(site, announce: false);
        }
    }
}
