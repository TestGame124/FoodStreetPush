using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Economy;
using LastOrders.Progression;
using LastOrders.Survivors;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>The two ways a defense can be paid for.</summary>
    public enum DefensePayment
    {
        /// <summary>From the stores - Trade Tokens, for a guard tower.</summary>
        Resources = 0,

        /// <summary>Premium currency instead.</summary>
        Premium = 1,
    }

    /// <summary>One way to pay, as the popup shows it: the price, and whether it can be paid now.</summary>
    public readonly struct DefensePaymentOption
    {
        public readonly DefensePayment Method;

        /// <summary>"60 Trade Tokens".</summary>
        public readonly string Label;

        public readonly bool Affordable;

        /// <summary>What is missing, when it cannot be paid. Null when it can.</summary>
        public readonly string ShortBy;

        public DefensePaymentOption(DefensePayment method, string label, bool affordable, string shortBy)
        {
            Method = method;
            Label = label;
            Affordable = affordable;
            ShortBy = shortBy;
        }
    }

    /// <summary>
    /// Everything the defense popup shows, gathered so the popup has nothing to work out for
    /// itself: what is on offer, what it does, where the post stands, each way to pay - and
    /// if it cannot be bought at all, the one sentence saying why.
    /// </summary>
    public sealed class DefenseOffer
    {
        public string Title;
        public string Description;

        /// <summary>Who is posted there, or that nobody is. Empty for a site with nothing built.</summary>
        public string Status;

        /// <summary>What the player gets, a line each: the weapon, the sight range, the crew rule.</summary>
        public readonly List<string> Details = new List<string>();

        public readonly List<DefensePaymentOption> Payments = new List<DefensePaymentOption>();

        /// <summary>Nothing to pay: one button that just builds it.</summary>
        public bool IsFree;

        /// <summary>Why nothing can be bought right now, whatever the player has. Null otherwise.</summary>
        public string Blocked;

        public bool CanBuy(DefensePayment method)
        {
            if (Blocked != null) return false;
            if (IsFree) return true;

            for (int i = 0; i < Payments.Count; i++)
                if (Payments[i].Method == method) return Payments[i].Affordable;

            return false;
        }
    }

    /// <summary>
    /// Every defense in the restaurant: where each stands, who is posted to it, what it shoots,
    /// and the rules for buying and upgrading one.
    ///
    /// Owns the rules and nothing visual. A post in the scene registers a unit and draws it;
    /// this decides everything the unit does. Ticked from the game loop like the day cycle, so
    /// the guards stop shooting when the game stops, and it never runs a clock of its own.
    ///
    /// Crewing is automatic. A defense that needs a Guard takes the first available one who is
    /// not already posted elsewhere, and a guard who is injured, sent away, or who flees the
    /// siege is replaced if anyone else can stand the watch. The player can still post a
    /// particular guard with <see cref="Assign"/>, and that sticks while they are on duty.
    ///
    /// What has been built and upgraded is recorded as progression flags, the same as reclaimed
    /// land, so a reload puts every tower back as it was and never charges for one twice.
    /// </summary>
    public sealed class DefenseService
    {
        readonly Dictionary<string, DefenseUnit> units = new Dictionary<string, DefenseUnit>();
        readonly List<DefenseUnit> order = new List<DefenseUnit>();
        readonly HashSet<Survivor> posted = new HashSet<Survivor>();

        readonly SurvivorService survivors;
        readonly ResourceLedger ledger;
        readonly IPremiumWallet premium;
        readonly ProgressionService progression;
        readonly DayCycleMachine machine;
        readonly Func<IReadOnlyList<IDefenseTarget>> hostiles;
        readonly Func<bool> isNamedNight;

        public event Action<DefenseUnit> OnBuilt;
        public event Action<DefenseUnit> OnUpgraded;

        /// <param name="hostiles">Whatever is on the map to be shot at. Read every tick.</param>
        /// <param name="isNamedNight">Whether tonight is a Named Night, which Superstitious
        /// guards refuse to stand watch through.</param>
        public DefenseService(SurvivorService survivors, ResourceLedger ledger, IPremiumWallet premium,
                              ProgressionService progression, DayCycleMachine machine,
                              Func<IReadOnlyList<IDefenseTarget>> hostiles, Func<bool> isNamedNight)
        {
            this.survivors = survivors;
            this.ledger = ledger;
            this.premium = premium;
            this.progression = progression;
            this.machine = machine;
            this.hostiles = hostiles;
            this.isNamedNight = isNamedNight;
        }

        /// <summary>Every registered defense, built or not, in the order they registered.</summary>
        public IReadOnlyList<DefenseUnit> All => order;

        public int BuiltCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < order.Count; i++) if (order[i].IsBuilt) count++;
                return count;
            }
        }

        public int ReadyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < order.Count; i++) if (order[i].CanFire) count++;
                return count;
            }
        }

        public DefenseUnit Find(string id) => TryGet(id, out DefenseUnit unit) ? unit : null;

        // --- Registration ---------------------------------------------------------------------

        /// <summary>
        /// Adds a defense site. <paramref name="startsBuilt"/> is the tower the restaurant opens
        /// with, which is never charged for; anything else starts as an empty site unless a
        /// purchase is already on record.
        /// </summary>
        public DefenseUnit Register(DefenseDefinition definition, string id, Vector3 position, bool startsBuilt)
        {
            if (definition == null || string.IsNullOrEmpty(id))
            {
                Debug.LogError("[Defense] A defense was registered with no definition or no id.");
                return null;
            }

            if (units.ContainsKey(id))
            {
                Debug.LogError($"[Defense] Two defenses share the id '{id}'. The second was ignored; " +
                               "give each post its own id.");
                return null;
            }

            var unit = new DefenseUnit(id, definition, position)
            {
                IsBuilt = startsBuilt || progression.Has(DefenseIds.Built(id)),
                LevelIndex = RecordedLevel(id, definition),
            };

            units.Add(id, unit);
            order.Add(unit);

            // Posted straight away rather than on the next tick, so the post shows its guard on
            // its very first frame instead of an empty platform that fills a frame later.
            Staff();

            return unit;
        }

        public void Unregister(string id)
        {
            if (!TryGet(id, out DefenseUnit unit)) return;

            unit.SetCrew(null);
            units.Remove(id);
            order.Remove(unit);
        }

        // --- The loop -------------------------------------------------------------------------

        /// <summary>Posts guards, then lets every defense look and shoot. Called once per frame.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            Staff();

            IReadOnlyList<IDefenseTarget> targets = hostiles?.Invoke();

            for (int i = 0; i < order.Count; i++)
                order[i].Tick(deltaSeconds, targets, ledger);
        }

        // --- Crew -----------------------------------------------------------------------------

        /// <summary>
        /// Posts a particular Survivor to a defense, taking them off any other post first. They
        /// stay there while they are on duty; the automatic crewing only replaces someone who
        /// is not.
        /// </summary>
        public bool Assign(string id, Survivor survivor, out string message)
        {
            if (!TryGet(id, out DefenseUnit unit))
            {
                message = "There is no defense there.";
                return false;
            }

            DefenseDefinition definition = unit.Definition;

            if (!unit.NeedsCrew)
            {
                message = $"The {definition.displayName} runs itself. Nobody needs to be posted there.";
                return false;
            }

            if (!unit.IsBuilt)
            {
                message = $"Build the {definition.displayName} first.";
                return false;
            }

            if (survivor == null || survivor.Role == null || survivor.Role.role != definition.crewRole)
            {
                message = $"Only a {definition.crewRole} can stand watch in the {definition.displayName}.";
                return false;
            }

            if (!survivor.IsAvailable)
            {
                message = $"{survivor.Name} is {(survivor.IsInjured ? "injured" : "away")} and cannot stand watch.";
                return false;
            }

            for (int i = 0; i < order.Count; i++)
                if (order[i] != unit && ReferenceEquals(order[i].Crew, survivor)) order[i].SetCrew(null);

            unit.SetCrew(survivor);
            Staff();

            message = $"{survivor.Name} is on watch in the {definition.displayName}.";
            return true;
        }

        /// <summary>Takes whoever is posted off a defense. The automatic crewing will fill it
        /// again on the next tick if anyone is free - call it to swap guards, not to disarm.</summary>
        public void Unassign(string id)
        {
            if (TryGet(id, out DefenseUnit unit)) unit.SetCrew(null);
        }

        /// <summary>Whether this Survivor would be at their post right now. A Nervous guard is
        /// not, once the siege starts; a Superstitious one is not on a Named Night.</summary>
        public bool IsOnDuty(Survivor survivor)
        {
            if (survivor == null) return false;

            DayPhase phase = machine != null ? machine.CurrentPhase : DayPhase.Day;
            if (!phase.IsSiege()) return survivor.IsAvailable;

            return survivor.WillWorkTonight(isNamedNight != null && isNamedNight());
        }

        /// <summary>
        /// Keeps every built defense that needs a crew crewed, in registration order.
        ///
        /// Someone injured or sent away leaves their post. Someone who is posted but not on
        /// duty - fled the siege - keeps it unless another guard is free to take over, so a
        /// Nervous guard is back in their tower at dawn without being told.
        /// </summary>
        void Staff()
        {
            posted.Clear();

            for (int i = 0; i < order.Count; i++)
            {
                DefenseUnit unit = order[i];

                if (!unit.IsBuilt || !unit.NeedsCrew)
                {
                    if (unit.Crew != null) unit.SetCrew(null);
                    continue;
                }

                if (unit.Crew != null && !IsEligible(unit.Crew, unit.Definition)) unit.SetCrew(null);
                if (unit.Crew != null) posted.Add(unit.Crew);
            }

            for (int i = 0; i < order.Count; i++)
            {
                DefenseUnit unit = order[i];
                if (!unit.IsBuilt || !unit.NeedsCrew) continue;

                if (unit.Crew == null || !IsOnDuty(unit.Crew))
                {
                    Survivor relief = FindRelief(unit.Definition);
                    if (relief != null)
                    {
                        if (unit.Crew != null) posted.Remove(unit.Crew);
                        unit.SetCrew(relief);
                        posted.Add(relief);
                    }
                }

                unit.CrewOnDuty = unit.Crew != null && IsOnDuty(unit.Crew);
            }
        }

        Survivor FindRelief(DefenseDefinition definition)
        {
            if (survivors == null) return null;

            IReadOnlyList<Survivor> roster = survivors.Roster;
            for (int i = 0; i < roster.Count; i++)
            {
                Survivor candidate = roster[i];
                if (posted.Contains(candidate)) continue;
                if (!IsEligible(candidate, definition) || !IsOnDuty(candidate)) continue;

                return candidate;
            }

            return null;
        }

        static bool IsEligible(Survivor survivor, DefenseDefinition definition) =>
            survivor != null && survivor.IsAvailable && survivor.Role != null && survivor.Role.role == definition.crewRole;

        // --- Building -------------------------------------------------------------------------

        public DefenseOffer GetBuildOffer(string id)
        {
            var offer = new DefenseOffer();

            if (!TryGet(id, out DefenseUnit unit))
            {
                offer.Blocked = "There is nowhere to build here.";
                return offer;
            }

            DefenseDefinition definition = unit.Definition;
            offer.Title = $"Build a {definition.displayName}";
            offer.Description = definition.description;
            offer.Status = string.Empty;

            AddLevelDetails(offer, definition, 0);
            offer.Details.Add("No worker needed - ready once it is paid for");

            AddPayments(offer, definition.buildPrice);
            offer.Blocked = WhyCannotBuild(unit);
            return offer;
        }

        public bool TryBuild(string id, DefensePayment payment, out string message)
        {
            if (!TryGet(id, out DefenseUnit unit))
            {
                message = "There is nowhere to build here.";
                return false;
            }

            message = WhyCannotBuild(unit);
            if (message != null) return false;

            DefenseDefinition definition = unit.Definition;
            if (!TryPay(definition.buildPrice, payment, out message)) return false;

            progression.Set(DefenseIds.Built(id));
            unit.MarkBuilt();
            Staff();

            OnBuilt?.Invoke(unit);

            message = unit.NeedsCrew && unit.Crew == null
                ? $"{definition.displayName} built. Hire a {definition.crewRole} at a shelter to stand watch in it."
                : $"{definition.displayName} built.";
            return true;
        }

        string WhyCannotBuild(DefenseUnit unit)
        {
            DefenseDefinition definition = unit.Definition;

            if (unit.IsBuilt) return $"There is already a {definition.displayName} here.";
            if (!AllowsBuilding()) return "Too dangerous to build after dark. Wait for dawn.";
            if (definition.Level(0)?.weapon == null) return $"The {definition.displayName} has no weapon set up yet.";

            return null;
        }

        // --- Sites that take a choice ----------------------------------------------------------
        //
        // A wall site, or a pad out in the defense ring, takes any defense the player picks. Buying
        // one there is paying its own build price; the choice is recorded under the site's id, which
        // is also the id the defense then registers under - as already built - so its upgrades are
        // kept with it.

        /// <summary>What was bought for the site out of <paramref name="options"/>, or null.</summary>
        public DefenseDefinition BuiltAt(string siteId, IReadOnlyList<DefenseDefinition> options)
        {
            if (options == null || string.IsNullOrEmpty(siteId)) return null;

            for (int i = 0; i < options.Count; i++)
                if (options[i] != null && progression.Has(DefenseIds.Site(siteId, options[i].id))) return options[i];

            return null;
        }

        /// <summary>
        /// The popup over an empty site: what each defense fires, and a button each with its price.
        /// <paramref name="onGround"/> for a pad out in the defense ring rather than a site on the wall.
        /// </summary>
        public BuildChoiceOffer GetSiteOffer(string siteId, IReadOnlyList<DefenseDefinition> options, bool onGround = false)
        {
            var offer = new BuildChoiceOffer
            {
                Title = onGround ? "Build on the pad" : "Build on the wall",
                Description = onGround
                    ? "The defense is set on this pad out past the wall, where it reaches further into the land beyond."
                    : "The wall here is thickened into a bastion, and the defense is mounted on top of it.",
                Status = string.Empty,
            };

            if (options != null)
            {
                foreach (DefenseDefinition option in options)
                {
                    if (option == null) continue;

                    WeaponDefinition weapon = option.Level(0)?.weapon;
                    string arms = weapon != null ? $"{weapon.displayName}, {weapon.Describe()}" : "no weapon yet";
                    string crew = option.requiresCrew ? $" - needs a {option.crewRole} posted" : string.Empty;
                    offer.Details.Add($"{option.displayName}: {arms}{crew}");

                    PriceChoice(option.buildPrice, out string label, out bool affordable, out string shortBy);
                    offer.Options.Add(new BuildChoice($"{option.displayName}\n{label}", affordable, shortBy));
                }
            }

            offer.Details.Add("No worker needed - ready once it is paid for");
            offer.Blocked = WhyCannotBuildAt(siteId, options);
            return offer;
        }

        /// <summary>
        /// Buys option <paramref name="index"/> of the site's offer: from the stores when its price
        /// has a materials option, in premium currency otherwise.
        /// </summary>
        public bool TryBuildAt(string siteId, IReadOnlyList<DefenseDefinition> options, int index, out string message,
                               out DefenseDefinition built, bool onGround = false)
        {
            built = null;
            message = WhyCannotBuildAt(siteId, options);
            if (message != null) return false;

            DefenseDefinition chosen = BuildChoices.Pick(options, index);
            if (chosen == null)
            {
                message = "That cannot be built here.";
                return false;
            }

            DefensePayment method = chosen.buildPrice.HasResourceOption ? DefensePayment.Resources : DefensePayment.Premium;
            if (!TryPay(chosen.buildPrice, method, out message)) return false;

            progression.Set(DefenseIds.Site(siteId, chosen.id));
            progression.Set(DefenseIds.Built(siteId));
            built = chosen;

            string mounted = $"{chosen.displayName} {(onGround ? "set up on the pad" : "mounted on the wall")}.";
            message = chosen.requiresCrew ? $"{mounted} Hire a {chosen.crewRole} at a shelter to stand watch in it." : mounted;
            return true;
        }

        /// <summary>Puts a defense out of action, or back in it: what it stands on has fallen, or been rebuilt.</summary>
        /// <summary>Wrecked in the yard, or put back together. Separate from being knocked over with
        /// a bastion, so mending one never sets a defense up that is still down for the other.</summary>
        public void SetWrecked(string id, bool value)
        {
            if (TryGet(id, out DefenseUnit unit)) unit.IsWrecked = value;
        }

        public void SetOutOfAction(string id, bool value)
        {
            if (TryGet(id, out DefenseUnit unit)) unit.IsOutOfAction = value;
        }

        string WhyCannotBuildAt(string siteId, IReadOnlyList<DefenseDefinition> options)
        {
            DefenseDefinition standing = BuiltAt(siteId, options);
            if (standing != null) return $"There is already a {standing.displayName} here.";
            if (BuildChoices.Pick(options, 0) == null) return "Nothing can be built here yet.";
            if (!AllowsBuilding()) return "Too dangerous to build after dark. Wait for dawn.";

            return null;
        }

        void PriceChoice(DefensePrice price, out string label, out bool affordable, out string shortBy)
        {
            if (price.IsFree)
            {
                label = "Free";
                affordable = true;
                shortBy = null;
                return;
            }

            if (price.HasResourceOption)
            {
                label = Label(price.resources);
                shortBy = ShortfallIn(price.resources);
                affordable = shortBy == null;
                return;
            }

            string name = premium != null ? premium.DisplayName : "premium currency";
            int have = premium != null ? premium.Balance : 0;
            label = $"{price.premium} {name}";
            shortBy = have >= price.premium ? null : $"Need {price.premium - have} more {name}.";
            affordable = shortBy == null;
        }

        // --- Upgrading ------------------------------------------------------------------------

        public DefenseOffer GetUpgradeOffer(string id)
        {
            var offer = new DefenseOffer();

            if (!TryGet(id, out DefenseUnit unit))
            {
                offer.Blocked = "There is no defense here.";
                return offer;
            }

            DefenseDefinition definition = unit.Definition;
            offer.Description = definition.description;
            offer.Status = Describe(unit);

            if (!unit.HasNextLevel)
            {
                offer.Title = $"{definition.displayName} - Level {unit.LevelNumber}";
                AddLevelDetails(offer, definition, unit.LevelIndex);
                offer.Blocked = "Already at its highest level.";
                return offer;
            }

            int next = unit.LevelIndex + 1;
            offer.Title = $"Upgrade {definition.displayName} to Level {next + 1}";

            AddLevelDetails(offer, definition, next);
            AddPayments(offer, definition.Level(next).upgradePrice);
            offer.Blocked = WhyCannotUpgrade(unit);
            return offer;
        }

        public bool TryUpgrade(string id, DefensePayment payment, out string message)
        {
            if (!TryGet(id, out DefenseUnit unit))
            {
                message = "There is no defense here.";
                return false;
            }

            message = WhyCannotUpgrade(unit);
            if (message != null) return false;

            int next = unit.LevelIndex + 1;
            DefenseDefinition definition = unit.Definition;

            if (!TryPay(definition.Level(next).upgradePrice, payment, out message)) return false;

            progression.Set(DefenseIds.Level(id, next));
            unit.SetLevel(next);

            OnUpgraded?.Invoke(unit);

            WeaponDefinition weapon = unit.Weapon;
            message = weapon != null
                ? $"{definition.displayName} is Level {unit.LevelNumber}: {weapon.displayName}, {weapon.Describe()}."
                : $"{definition.displayName} is Level {unit.LevelNumber}.";
            return true;
        }

        string WhyCannotUpgrade(DefenseUnit unit)
        {
            DefenseDefinition definition = unit.Definition;

            if (!unit.IsBuilt) return $"Build the {definition.displayName} first.";
            if (!unit.HasNextLevel) return "Already at its highest level.";
            if (!AllowsBuilding()) return "Too dangerous to work on it after dark. Wait for dawn.";
            if (definition.Level(unit.LevelIndex + 1).weapon == null) return $"Level {unit.LevelNumber + 1} has no weapon set up yet.";

            return null;
        }

        // --- Paying ---------------------------------------------------------------------------

        void AddPayments(DefenseOffer offer, DefensePrice price)
        {
            offer.IsFree = price.IsFree;
            if (offer.IsFree) return;

            if (price.HasResourceOption)
            {
                string shortBy = ShortfallIn(price.resources);
                offer.Payments.Add(new DefensePaymentOption(DefensePayment.Resources, Label(price.resources),
                                                            shortBy == null, shortBy));
            }

            if (price.HasPremiumOption)
            {
                string name = premium != null ? premium.DisplayName : "premium currency";
                int have = premium != null ? premium.Balance : 0;
                string shortBy = have >= price.premium ? null : $"Need {price.premium - have} more {name}.";

                offer.Payments.Add(new DefensePaymentOption(DefensePayment.Premium, $"{price.premium} {name}",
                                                            shortBy == null, shortBy));
            }
        }

        bool TryPay(DefensePrice price, DefensePayment payment, out string reason)
        {
            reason = null;
            if (price.IsFree) return true;

            switch (payment)
            {
                case DefensePayment.Resources:
                    if (!price.HasResourceOption)
                    {
                        reason = "This can only be bought with premium currency.";
                        return false;
                    }

                    if (ledger.TrySpend(price.resources)) return true;

                    reason = ShortfallIn(price.resources) ?? "Could not pay for it.";
                    return false;

                case DefensePayment.Premium:
                    if (!price.HasPremiumOption)
                    {
                        reason = "This cannot be bought with premium currency.";
                        return false;
                    }

                    if (premium != null && premium.TrySpend(price.premium)) return true;

                    int have = premium != null ? premium.Balance : 0;
                    reason = $"Need {Mathf.Max(1, price.premium - have)} more {(premium != null ? premium.DisplayName : "premium currency")}.";
                    return false;
            }

            reason = "Could not pay for it.";
            return false;
        }

        /// <summary>The first thing the stores are short of, in words. Null when they have it all.</summary>
        string ShortfallIn(ResourceAmount[] cost)
        {
            if (cost == null) return null;

            for (int i = 0; i < cost.Length; i++)
            {
                int shortBy = cost[i].amount - ledger.Get(cost[i].type);
                if (shortBy > 0) return $"Need {shortBy} more {cost[i].type.DisplayName()}.";
            }

            return null;
        }

        static string Label(ResourceAmount[] cost)
        {
            var parts = new List<string>(cost.Length);
            for (int i = 0; i < cost.Length; i++)
                if (cost[i].amount > 0) parts.Add($"{cost[i].amount} {cost[i].type.DisplayName()}");

            return string.Join(" + ", parts);
        }

        // --- Describing -----------------------------------------------------------------------

        /// <summary>The tap reply for a defense: what it is, who is up there, and what it fires.</summary>
        public string Describe(string id) => TryGet(id, out DefenseUnit unit) ? Describe(unit) : null;

        string Describe(DefenseUnit unit)
        {
            DefenseDefinition definition = unit.Definition;

            if (!unit.IsBuilt) return $"An empty site for a {definition.displayName}.";
            if (unit.IsOutOfAction) return $"The {definition.displayName} has been knocked over. Repair what it stood on to set it up again.";
            if (unit.IsWrecked) return $"The {definition.displayName} was wrecked by zombies. A repair kit puts it back together, level and all.";

            WeaponDefinition weapon = unit.Weapon;
            string arms = weapon != null ? $"{weapon.displayName}, {weapon.Describe()}" : "no weapon";
            string name = $"{definition.displayName} (Level {unit.LevelNumber})";

            if (unit.NeedsCrew)
            {
                if (unit.Crew == null)
                    return $"{name} has no {definition.crewRole.ToString().ToLowerInvariant()} and cannot fire. " +
                           $"Hire a {definition.crewRole} at a shelter to stand watch in it.";

                if (!unit.CrewOnDuty)
                    return $"{name}: {unit.Crew.Name} has left their post and will not stand watch tonight.";

                if (unit.IsOutOfAmmo) return $"{name}: {unit.Crew.Name} is on watch but out of Ammo.";

                return $"{name}: {unit.Crew.Name} on watch. {arms}.";
            }

            return unit.IsOutOfAmmo ? $"{name} is out of Ammo." : $"{name}: {arms}.";
        }

        static void AddLevelDetails(DefenseOffer offer, DefenseDefinition definition, int levelIndex)
        {
            DefenseLevel level = definition.Level(levelIndex);
            WeaponDefinition weapon = level?.weapon;

            if (weapon != null)
            {
                offer.Details.Add($"{weapon.displayName}: {weapon.Describe()}");
                offer.Details.Add($"Spots enemies {level.SpotRange:0.#} m out");
                if (weapon.ammoPerShot > 0) offer.Details.Add($"Uses {weapon.ammoPerShot} Ammo a shot");
            }

            offer.Details.Add(definition.requiresCrew
                ? $"Fires only while a {definition.crewRole} is posted to it"
                : "Fires on its own - nobody needs to be posted to it");
        }

        // --- Helpers --------------------------------------------------------------------------

        int RecordedLevel(string id, DefenseDefinition definition)
        {
            for (int i = definition.LevelCount - 1; i > 0; i--)
                if (progression.Has(DefenseIds.Level(id, i))) return i;

            return 0;
        }

        bool AllowsBuilding() => machine == null || machine.CurrentPhase.AllowsBuilding();

        bool TryGet(string id, out DefenseUnit unit)
        {
            unit = null;
            return !string.IsNullOrEmpty(id) && units.TryGetValue(id, out unit);
        }
    }

    /// <summary>
    /// The progression flags a defense is recorded under, built from its post's stable id. In
    /// one place so the service that sets a flag and the one that reads it cannot spell it
    /// differently.
    /// </summary>
    public static class DefenseIds
    {
        public static string Built(string postId) => $"defense_built:{postId}";

        /// <summary>A site that takes a choice had <paramref name="defenseId"/> bought for it.</summary>
        public static string Site(string siteId, string defenseId) => $"defense_site:{siteId}:{defenseId}";

        /// <summary>Reached level <paramref name="levelIndex"/> (zero-based; Level 1 needs no flag).</summary>
        public static string Level(string postId, int levelIndex) => $"defense_level:{postId}:{levelIndex}";
    }
}
