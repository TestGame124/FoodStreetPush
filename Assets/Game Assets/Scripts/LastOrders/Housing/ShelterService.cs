using System;
using System.Collections.Generic;
using System.Text;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Economy;
using LastOrders.Progression;
using LastOrders.Survivors;
using UnityEngine;

namespace LastOrders.Housing
{
    /// <summary>
    /// One shelter in the world, as the rules see it: where it stands, whether it is built, and
    /// who sleeps in each of its beds.
    ///
    /// A plain class with no scene in it, like a defense unit. The hut in the scene registers
    /// one of these and draws what it says.
    /// </summary>
    public sealed class ShelterUnit
    {
        public readonly string Id;
        public readonly ShelterDefinition Definition;
        public readonly Vector3 Position;

        readonly Survivor[] beds;

        public ShelterUnit(string id, ShelterDefinition definition, Vector3 position)
        {
            Id = id;
            Definition = definition;
            Position = position;
            beds = new Survivor[definition != null ? Mathf.Max(1, definition.beds) : 2];
        }

        public bool IsBuilt { get; internal set; }

        public int BedCount => beds.Length;

        /// <summary>Who sleeps in a bed. Null when it is free.</summary>
        public Survivor Resident(int bed) => bed >= 0 && bed < beds.Length ? beds[bed] : null;

        public int Occupied
        {
            get
            {
                int count = 0;
                for (int i = 0; i < beds.Length; i++) if (beds[i] != null) count++;
                return count;
            }
        }

        /// <summary>Beds nobody has yet. None until it is built: an empty site houses nobody.</summary>
        public int FreeBeds => IsBuilt ? BedCount - Occupied : 0;

        public string DisplayName => Definition != null ? Definition.displayName : "Shelter";

        /// <summary>It was built, or somebody moved in. What the hut redraws on.</summary>
        public event Action OnChanged;

        internal void MarkBuilt()
        {
            if (IsBuilt) return;

            IsBuilt = true;
            OnChanged?.Invoke();
        }

        /// <summary>Gives <paramref name="survivor"/> the first free bed. The bed's index, or -1
        /// when there is none.</summary>
        internal int MoveIn(Survivor survivor)
        {
            if (!IsBuilt || survivor == null) return -1;

            for (int i = 0; i < beds.Length; i++)
            {
                if (beds[i] != null) continue;

                beds[i] = survivor;
                OnChanged?.Invoke();
                return i;
            }

            return -1;
        }

        public bool Houses(Survivor survivor)
        {
            for (int i = 0; i < beds.Length; i++)
                if (ReferenceEquals(beds[i], survivor)) return true;

            return false;
        }
    }

    /// <summary>One button on the shelter popup: what it says, and whether it can be pressed now.</summary>
    public readonly struct ShelterOption
    {
        /// <summary>"Hire a Guard\n40 Coins".</summary>
        public readonly string Label;

        public readonly bool Affordable;

        /// <summary>What is missing, when it cannot be paid. Null when it can.</summary>
        public readonly string ShortBy;

        public ShelterOption(string label, bool affordable, string shortBy)
        {
            Label = label;
            Affordable = affordable;
            ShortBy = shortBy;
        }
    }

    /// <summary>
    /// Everything the shelter popup shows, gathered so the popup has nothing to work out for
    /// itself: the beds, who is in them, a button for each thing on offer - and if none of them
    /// can be taken at all, the one sentence saying why.
    /// </summary>
    public sealed class ShelterOffer
    {
        public string Title;
        public string Description;

        /// <summary>How many beds are taken across every shelter - the hiring limit, as it stands.</summary>
        public string Status;

        public readonly List<string> Details = new List<string>();

        public readonly List<ShelterOption> Options = new List<ShelterOption>();

        /// <summary>Why nothing can be taken right now, whatever the player has. Null otherwise.</summary>
        public string Blocked;

        public bool CanTake(int index) =>
            Blocked == null && index >= 0 && index < Options.Count && Options[index].Affordable;
    }

    /// <summary>
    /// Every shelter in the restaurant, and the hiring limit they set.
    ///
    /// The rule is one sentence: everybody the player hires needs a bed. A shelter has two, so
    /// the number of people who can be taken on is two for every shelter built, and building
    /// another is the only way to take on more. The crew the restaurant opens with sleep in the
    /// diner and are not counted.
    ///
    /// Hiring puts a new survivor on the roster, and nothing else needs telling: a Guard is
    /// posted to an empty tower, or stands by to relieve a hurt one, by the defense service's own
    /// crewing; a Medic stands in the hospital tent by the hospital's.
    ///
    /// What has been built is recorded as progression flags, the same as a guard tower, so a
    /// reload puts every shelter back and never charges for one twice.
    /// </summary>
    public sealed class ShelterService
    {
        readonly Dictionary<string, ShelterUnit> units = new Dictionary<string, ShelterUnit>();
        readonly List<ShelterUnit> order = new List<ShelterUnit>();

        readonly GameBalanceConfig balance;
        readonly SurvivorService survivors;
        readonly ResourceLedger ledger;
        readonly IWallet coins;
        readonly ProgressionService progression;
        readonly DayCycleMachine machine;

        int hiredCount;

        public event Action<ShelterUnit> OnBuilt;

        /// <summary>Somebody new moved in, and which shelter took them.</summary>
        public event Action<ShelterUnit, Survivor> OnHired;

        /// <param name="balance">Where new hires' names come from. Null names them by number.</param>
        /// <param name="coins">The level's Coins, which hiring and building are paid in.</param>
        public ShelterService(GameBalanceConfig balance, SurvivorService survivors, ResourceLedger ledger,
                              IWallet coins, ProgressionService progression, DayCycleMachine machine)
        {
            this.balance = balance;
            this.survivors = survivors;
            this.ledger = ledger;
            this.coins = coins;
            this.progression = progression;
            this.machine = machine;
        }

        /// <summary>Every registered shelter, built or not, in the order they registered.</summary>
        public IReadOnlyList<ShelterUnit> All => order;

        public int BuiltCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < order.Count; i++) if (order[i].IsBuilt) count++;
                return count;
            }
        }

        /// <summary>Beds in built shelters: how many people can be hired in all.</summary>
        public int Capacity
        {
            get
            {
                int count = 0;
                for (int i = 0; i < order.Count; i++) if (order[i].IsBuilt) count += order[i].BedCount;
                return count;
            }
        }

        /// <summary>Hired survivors with a bed.</summary>
        public int Housed
        {
            get
            {
                int count = 0;
                for (int i = 0; i < order.Count; i++) count += order[i].Occupied;
                return count;
            }
        }

        /// <summary>How many more can be hired before another shelter is needed.</summary>
        public int FreeBeds => Mathf.Max(0, Capacity - Housed);

        public ShelterUnit Find(string id) => TryGet(id, out ShelterUnit unit) ? unit : null;

        /// <summary>The shelter a survivor sleeps in. Null for the starting crew, who sleep in the diner.</summary>
        public ShelterUnit HomeOf(Survivor survivor)
        {
            if (survivor == null) return null;

            for (int i = 0; i < order.Count; i++)
                if (order[i].Houses(survivor)) return order[i];

            return null;
        }

        string CoinName => coins != null ? coins.DisplayName : "Coins";

        // --- Registration ---------------------------------------------------------------------

        /// <summary>
        /// Adds a shelter site. <paramref name="startsBuilt"/> is the shelter the restaurant
        /// opens with, which is never charged for; anything else starts as an empty site unless
        /// a purchase is already on record.
        /// </summary>
        public ShelterUnit Register(ShelterDefinition definition, string id, Vector3 position, bool startsBuilt)
        {
            if (definition == null || string.IsNullOrEmpty(id))
            {
                Debug.LogError("[Shelter] A shelter was registered with no definition or no id.");
                return null;
            }

            if (units.ContainsKey(id))
            {
                Debug.LogError($"[Shelter] Two shelters share the id '{id}'. The second was ignored; " +
                               "give each shelter its own id.");
                return null;
            }

            var unit = new ShelterUnit(id, definition, position)
            {
                IsBuilt = startsBuilt || progression.Has(ShelterIds.Built(id)),
            };

            units.Add(id, unit);
            order.Add(unit);
            return unit;
        }

        public void Unregister(string id)
        {
            if (!TryGet(id, out ShelterUnit unit)) return;

            units.Remove(id);
            order.Remove(unit);
        }

        // --- Building -------------------------------------------------------------------------

        public ShelterOffer GetBuildOffer(string id)
        {
            var offer = new ShelterOffer();

            if (!TryGet(id, out ShelterUnit unit))
            {
                offer.Blocked = "There is nowhere to build here.";
                return offer;
            }

            ShelterDefinition definition = unit.Definition;
            offer.Title = $"Build a {definition.displayName}";
            offer.Description = definition.description;
            offer.Status = BedsLine();

            offer.Details.Add($"{unit.BedCount} beds - room to hire {unit.BedCount} more survivors");
            offer.Details.Add("Everyone you hire needs a bed");
            offer.Details.Add("No worker needed - ready once it is paid for");

            ShelterPrice price = definition.buildPrice;
            string shortBy = Shortfall(price);
            offer.Options.Add(new ShelterOption(price.IsFree ? "BUILD" : $"BUILD\n{price.Describe(CoinName)}",
                                                shortBy == null, shortBy));

            offer.Blocked = WhyCannotBuild(unit);
            return offer;
        }

        public bool TryBuild(string id, out string message)
        {
            if (!TryGet(id, out ShelterUnit unit))
            {
                message = "There is nowhere to build here.";
                return false;
            }

            message = WhyCannotBuild(unit);
            if (message != null) return false;

            if (!TryPay(unit.Definition.buildPrice, out message)) return false;

            progression.Set(ShelterIds.Built(id));
            unit.MarkBuilt();

            OnBuilt?.Invoke(unit);

            message = $"{unit.DisplayName} built. {BedsLine()} - room for {FreeBeds} more.";
            return true;
        }

        string WhyCannotBuild(ShelterUnit unit)
        {
            if (unit.IsBuilt) return $"There is already a {unit.DisplayName} here.";
            if (!AllowsWork()) return "Too dangerous to build after dark. Wait for dawn.";

            return null;
        }

        // --- Hiring ---------------------------------------------------------------------------

        public ShelterOffer GetHireOffer(string id)
        {
            var offer = new ShelterOffer();

            if (!TryGet(id, out ShelterUnit unit))
            {
                offer.Blocked = "There is no shelter here.";
                return offer;
            }

            ShelterDefinition definition = unit.Definition;
            offer.Title = definition.displayName;
            offer.Description = definition.description;
            offer.Status = BedsLine();

            if (!unit.IsBuilt)
            {
                offer.Blocked = $"Build the {definition.displayName} first.";
                return offer;
            }

            for (int i = 0; i < unit.BedCount; i++)
            {
                Survivor resident = unit.Resident(i);
                offer.Details.Add(resident != null ? $"{resident.Name} - {RoleName(resident.Role)}" : "Empty bed");
            }

            // One option per entry on the definition, even a broken one, so an option's index is
            // always the hire's index - which is what TryHire is handed back.
            for (int i = 0; i < definition.HireCount; i++)
            {
                ShelterHire hire = HireAt(definition, i);
                if (hire == null)
                {
                    offer.Options.Add(new ShelterOption("-", false, "Nobody like that is looking for work."));
                    continue;
                }

                string role = RoleName(hire.role);
                if (!string.IsNullOrEmpty(hire.pitch)) offer.Details.Add($"{role}: {hire.pitch}");

                string shortBy = Shortfall(hire.price);
                string label = hire.price.IsFree
                    ? $"Hire {Article(role)} {role}"
                    : $"Hire {Article(role)} {role}\n{hire.price.Describe(CoinName)}";

                offer.Options.Add(new ShelterOption(label, shortBy == null, shortBy));
            }

            offer.Blocked = WhyCannotHire(unit);
            return offer;
        }

        /// <inheritdoc cref="TryHire(string, int, out Survivor, out string)"/>
        public bool TryHire(string id, int option, out string message) => TryHire(id, option, out _, out message);

        /// <summary>
        /// Takes someone on, by the index of the hire on the shelter's definition - the same
        /// order the popup lists them in.
        ///
        /// They sleep in the shelter that was tapped when it has a free bed, otherwise in any
        /// that does: the limit is the whole compound's beds, not this hut's.
        /// </summary>
        public bool TryHire(string id, int option, out Survivor hired, out string message)
        {
            hired = null;

            if (!TryGet(id, out ShelterUnit unit))
            {
                message = "There is no shelter here.";
                return false;
            }

            ShelterHire hire = HireAt(unit.Definition, option);
            if (hire == null)
            {
                message = "Nobody like that is looking for work.";
                return false;
            }

            message = WhyCannotHire(unit);
            if (message != null) return false;

            if (!TryPay(hire.price, out message)) return false;

            ShelterUnit home = unit.FreeBeds > 0 ? unit : FirstWithFreeBed();

            hired = new Survivor
            {
                Id = NextId(),
                Name = NextName(),
                Role = hire.role,
                Trait = null,
            };

            survivors.Add(hired);
            home.MoveIn(hired);

            OnHired?.Invoke(home, hired);

            message = $"{hired.Name} the {RoleName(hire.role)} moved into the {home.DisplayName}. {BedsLine()}.";
            return true;
        }

        string WhyCannotHire(ShelterUnit unit)
        {
            if (!unit.IsBuilt) return $"Build the {unit.DisplayName} first.";

            if (FreeBeds <= 0)
            {
                return HasEmptySite()
                    ? $"Every bed is taken. Build another {unit.DisplayName.ToLowerInvariant()} to hire more."
                    : "Every bed is taken.";
            }

            if (!AllowsWork()) return "Nobody travels after dark. Hire someone in the morning.";

            return null;
        }

        ShelterUnit FirstWithFreeBed()
        {
            for (int i = 0; i < order.Count; i++)
                if (order[i].FreeBeds > 0) return order[i];

            return null;
        }

        bool HasEmptySite()
        {
            for (int i = 0; i < order.Count; i++)
                if (!order[i].IsBuilt) return true;

            return false;
        }

        static ShelterHire HireAt(ShelterDefinition definition, int index)
        {
            if (definition == null || index < 0 || index >= definition.HireCount) return null;

            ShelterHire hire = definition.hires[index];
            return hire != null && hire.role != null ? hire : null;
        }

        /// <summary>The first of the balance's names nobody on the roster already has.</summary>
        string NextName()
        {
            string[] names = balance != null ? balance.survivorNames : null;

            if (names != null)
            {
                for (int i = 0; i < names.Length; i++)
                    if (!string.IsNullOrEmpty(names[i]) && !NameTaken(names[i])) return names[i];
            }

            return $"Survivor {survivors.Roster.Count + 1}";
        }

        bool NameTaken(string name)
        {
            IReadOnlyList<Survivor> roster = survivors.Roster;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].Name == name) return true;

            return false;
        }

        string NextId()
        {
            string id;
            do id = $"hired_{++hiredCount}";
            while (survivors.FindById(id) != null);

            return id;
        }

        // --- Paying ---------------------------------------------------------------------------

        /// <summary>Takes the whole price or none of it. The stores are refunded if the coins
        /// then cannot be taken, so a half-paid price never happens.</summary>
        bool TryPay(ShelterPrice price, out string reason)
        {
            reason = Shortfall(price);
            if (reason != null) return false;
            if (price.IsFree) return true;

            if (price.HasResources && !ledger.TrySpend(price.resources))
            {
                reason = "Could not pay for it.";
                return false;
            }

            if (price.coins > 0 && (coins == null || !coins.TrySpend(price.coins)))
            {
                if (price.HasResources) ledger.Add(price.resources);

                reason = $"Need {price.coins} {CoinName}.";
                return false;
            }

            return true;
        }

        /// <summary>The first thing the player is short of, in words. Null when they have it all.</summary>
        string Shortfall(ShelterPrice price)
        {
            if (price.coins > 0)
            {
                int have = coins != null ? coins.Balance : 0;
                if (have < price.coins) return $"Need {price.coins - have} more {CoinName}.";
            }

            if (price.resources != null)
            {
                for (int i = 0; i < price.resources.Length; i++)
                {
                    int shortBy = price.resources[i].amount - ledger.Get(price.resources[i].type);
                    if (shortBy > 0) return $"Need {shortBy} more {price.resources[i].type.DisplayName()}.";
                }
            }

            return null;
        }

        // --- Describing -----------------------------------------------------------------------

        /// <summary>The tap reply for a shelter: who sleeps there, and how many beds are left.</summary>
        public string Describe(string id)
        {
            if (!TryGet(id, out ShelterUnit unit)) return null;

            if (!unit.IsBuilt)
                return $"An empty site for a {unit.DisplayName}: {unit.BedCount} more beds, so {unit.BedCount} more people to hire.";

            var text = new StringBuilder($"{unit.DisplayName}: ");

            for (int i = 0; i < unit.BedCount; i++)
            {
                if (i > 0) text.Append(", ");

                Survivor resident = unit.Resident(i);
                text.Append(resident != null ? $"{resident.Name} ({RoleName(resident.Role)})" : "empty bed");
            }

            text.Append($". {BedsLine()}.");
            return text.ToString();
        }

        /// <summary>"3 of 4 beds taken across 2 shelters". The hiring limit, in one line.</summary>
        string BedsLine()
        {
            int built = BuiltCount;
            string line = $"{Housed} of {Capacity} beds taken";
            return built > 1 ? $"{line} across {built} shelters" : line;
        }

        static string RoleName(SurvivorRoleDefinition role)
        {
            if (role == null) return "Survivor";
            return string.IsNullOrEmpty(role.displayName) ? role.role.ToString() : role.displayName;
        }

        /// <summary>"a Guard", "an Engineer".</summary>
        static string Article(string word) =>
            !string.IsNullOrEmpty(word) && "AEIOUaeiou".IndexOf(word[0]) >= 0 ? "an" : "a";

        bool AllowsWork() => machine == null || machine.CurrentPhase.AllowsBuilding();

        bool TryGet(string id, out ShelterUnit unit)
        {
            unit = null;
            return !string.IsNullOrEmpty(id) && units.TryGetValue(id, out unit);
        }
    }

    /// <summary>
    /// The progression flags a shelter is recorded under, built from its stable id. In one place
    /// so the service that sets a flag and the one that reads it cannot spell it differently.
    /// </summary>
    public static class ShelterIds
    {
        public static string Built(string shelterId) => $"shelter_built:{shelterId}";
    }
}
