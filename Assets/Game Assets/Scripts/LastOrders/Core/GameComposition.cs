using System.Collections.Generic;
using LastOrders.Build;
using LastOrders.Consequence;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.DayCycle.States;
using LastOrders.Defense;
using LastOrders.Dispatch;
using LastOrders.Economy;
using LastOrders.Effects;
using LastOrders.Expansion;
using LastOrders.Field;
using LastOrders.Heat;
using LastOrders.Housing;
using LastOrders.Jobs;
using LastOrders.Kitchen;
using LastOrders.Medical;
using LastOrders.Night;
using LastOrders.Progression;
using LastOrders.Perimeter;
using LastOrders.Repair;
using LastOrders.Reputation;
using LastOrders.Restaurant;
using LastOrders.Survivors;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Core
{
    /// <summary>
    /// Builds the whole object graph, in dependency order, in one readable place.
    ///
    /// Kept as a plain class with no DI-framework types in it. VContainer is the registered
    /// container per 24.1, but the actual construction order - which service needs which -
    /// is game knowledge rather than framework knowledge, and it is far easier to read and
    /// to debug as a sequence of constructor calls than as a set of registrations whose
    /// order the container decides.
    ///
    /// It also means a package resolution problem with VContainer costs one thin adapter
    /// file rather than the ability to start the game.
    /// </summary>
    public sealed class GameComposition
    {
        public readonly GameBalanceConfig Balance;

        public readonly TimeService Time;
        public readonly HeatService Heat;
        public readonly ReputationService Reputation;
        public readonly ResourceLedger Ledger;
        public readonly PerimeterService Perimeter;
        public readonly WaveDirector Waves;
        public readonly ThreatProfileService ThreatProfile;
        public readonly CrisisBarService CrisisBar;
        public readonly SurvivorService Survivors;
        public readonly DispatchService Dispatch;
        public readonly GhostRegistry Ghosts;

        /// <summary>Meals cooked and not yet eaten, per dish, and who has claimed one. The
        /// "food" half of the arrival rule.</summary>
        public readonly MealStock Meals;

        /// <summary>"Something is on the road." Raised by whatever threatens the compound
        /// during working hours, and read by the visitor director, which sends approaching
        /// travellers home and moves customers already inside into the diner.</summary>
        public readonly DaytimeIncidentService Incidents;

        /// <summary>The task queue of 21. Nothing in 6, 8 or 13 works without it.</summary>
        public readonly JobService Jobs;

        /// <summary>Unlock flags and idempotent one-time grants (21).</summary>
        public readonly ProgressionService Progression;

        public readonly PoolService Pools;

        /// <summary>Pays reward bundles once per ID: resources to the ledger, experience to the
        /// level's XP bar.</summary>
        public readonly RewardService Rewards;

        /// <summary>Which land is locked, being cleared, ready, being built or claimed.</summary>
        public readonly ExpansionService Expansion;

        /// <summary>Turns "clear this debris" into a job and a finished job into cleared debris.</summary>
        public readonly DebrisClearingService Clearing;

        /// <summary>Pooled world-space UI: the action prompt, work timers, floating text. On its
        /// own pool, which dawn does not recall.</summary>
        public readonly WorldUiService WorldUi;

        /// <summary>Pooled one-shot particle effects, on their own pool.</summary>
        public readonly EffectsService Effects;

        /// <summary>Screen popups, one at a time, each prefab instantiated once.</summary>
        public readonly PopupService Popups;

        /// <summary>Guard towers now, automated turrets later: who is posted where, what each
        /// one shoots, and what building and upgrading one costs.</summary>
        public readonly DefenseService Defense;

        /// <summary>The barricades, traps and wire in the strip outside the walls, and the slots they stand in.</summary>
        public readonly GroundDefenseService GroundDefenses;

        /// <summary>The hospital tent: who is in which cot, how far along they are, and the
        /// medic who makes it faster.</summary>
        public readonly HospitalService Hospital;

        /// <summary>The shelters: a bed for everyone hired, and so the limit on how many can be.</summary>
        public readonly ShelterService Shelters;

        /// <summary>How close the night's zombies got to the people, and whether a difficult
        /// night hurt somebody.</summary>
        public readonly NightDangerService Danger;

        /// <summary>Workers sent out past the walls: who is clearing which area, what they fight
        /// there, and who carries them to the tent when they go down.</summary>
        public readonly FieldWorkService Field;

        /// <summary>The compound's outer wall and its gates: what is still standing, and whether
        /// anything has got inside - which sends the staff to the shelters and stops the diner.</summary>
        public readonly CompoundPerimeterService Compound;

        /// <summary>What the night broke, and who has been sent to fix it. Nothing mends by itself.</summary>
        public readonly RepairService Repairs;

        /// <summary>The restaurant building: how big it is, growing it, and the line at its walls
        /// that no zombie crosses.</summary>
        public readonly RestaurantService Restaurant;

        /// <summary>The yard in an attack: what a zombie over the wall smashes, the cap on how much
        /// one attack may wreck, and the one repair-kit repair that puts it all back.</summary>
        public readonly OutdoorDamageService Outdoor;

        /// <summary>The zombie attack cycle: an attack every few days, its locked difficulty, its
        /// waves, and the one result it leaves on the Safety Reputation.</summary>
        public readonly EncounterService Encounters;

        public readonly BuildContext BuildContext;
        public readonly BuildCommandStack Commands;

        public readonly DayCycleMachine Machine;
        public readonly DayCycleContext Context;

        public readonly DawnState Dawn;
        public readonly DayState Day;
        public readonly DuskState Dusk;
        public readonly NightState Night;
        public readonly ResolutionState Resolution;

        public GameComposition(GameBalanceConfig balance, Transform poolRoot, Transform worldUiRoot,
                               Transform effectsRoot, Transform popupRoot, GridBuildingSystem3D grid)
        {
            Balance = balance;

            // Leaf services first: nothing here depends on anything else in the graph.
            Time = new TimeService(balance.realSecondsPerClockMinute);
            Heat = new HeatService(balance.heatWeights);
            Reputation = new ReputationService(balance);
            Ledger = new ResourceLedger();
            Ghosts = new GhostRegistry();

            // Leaf services too: the queue knows nothing about who works it, and progression
            // knows nothing about what sets its flags.
            Jobs = new JobService();
            Progression = new ProgressionService();
            Perimeter = new PerimeterService();
            Pools = new PoolService(poolRoot);
            Meals = new MealStock();
            Incidents = new DaytimeIncidentService();

            // Then the ones that compose them.
            Waves = new WaveDirector(balance, balance.heatWeights);
            ThreatProfile = new ThreatProfileService(Waves, balance);
            CrisisBar = new CrisisBarService(balance, Ledger);
            Survivors = new SurvivorService(balance, Ledger);
            Dispatch = new DispatchService(balance, Ledger, Reputation, Survivors);

            BuildContext = new BuildContext(Ledger, Time, grid);
            Commands = new BuildCommandStack(BuildContext, balance.undoWindowSeconds);

            Machine = new DayCycleMachine(Time);

            // The expansion loop. Rewards first: both of the others pay through it.
            Rewards = new RewardService(Progression, Ledger, LastOrdersBridge.AddExperience);
            Expansion = new ExpansionService(Progression, Rewards, Ledger, Machine, () => LastOrdersBridge.PlayerLevel);
            Clearing = new DebrisClearingService(Jobs, Expansion, Rewards, Progression, Machine);
            WorldUi = new WorldUiService(balance.worldUi, worldUiRoot);
            Effects = new EffectsService(effectsRoot);
            Popups = new PopupService(popupRoot);

            Context = new DayCycleContext(
                balance, Time, Heat, Reputation, Ledger, Perimeter, Waves,
                ThreatProfile, CrisisBar, Survivors, Dispatch, Ghosts, Commands,
                BuildContext, Pools);

            // States last: they need the context, which needs everything else.
            Dawn = new DawnState(Context);
            Day = new DayState(Context, Machine);
            Dusk = new DuskState(Context, Machine);
            Night = new NightState(Context, Machine);
            Resolution = new ResolutionState(Context, Machine);

            Machine.AddState(Dawn);
            Machine.AddState(Day);
            Machine.AddState(Dusk);
            Machine.AddState(Night);
            Machine.AddState(Resolution);

            // After the states, because what the defenses shoot at is what the night has put on
            // the map - and the zombies on land still to be cleared - and whether a Superstitious
            // guard turns up is tonight's wave's business.
            Defense = new DefenseService(Survivors, Ledger, new GemWallet(), Progression, Machine,
                                         DefenseTargets,
                                         () => Context.TonightsWave?.NamedNight != null);

            // What the strip outside the walls catches is only ever the night's: the zombies lurking on
            // uncleared land never come out into it.
            GroundDefenses = new GroundDefenseService(Ledger, Progression, Machine, () => Night.LiveEnemies);

            // Recovery stops with the clock at dawn, so a stay is never spent on the morning screen.
            Hospital = new HospitalService(balance.hospital, Survivors, () => Time.IsPaused);

            // Paid in the level's own Coins, which is what visitors pay into and the HUD counts.
            Shelters = new ShelterService(balance, Survivors, Ledger, new CoinWallet(), Progression, Machine);

            // Last, because who can be hurt is whoever the towers have posted.
            Danger = new NightDangerService(balance, Hospital, Defense, () => Night.LiveEnemies);

            // After the hospital: a worker knocked down out there is admitted to it.
            Field = new FieldWorkService(Clearing, Expansion, Hospital, Progression, Machine);

            // What counts as getting in is whatever the night has put on the map.
            Compound = new CompoundPerimeterService(() => Night.LiveEnemies, Machine);

            // A worker fixes what fell, so dawn no longer rebuilds it - unless the tuning asks for the old rule.
            RepairTuning repair = balance.repair ?? new RepairTuning();
            Compound.RepairsAtDawn = repair.autoRepairAtDawn;
            Repairs = new RepairService(repair, Jobs, Ledger, new CoinWallet(), Machine, () => Compound.IsBreached);

            // Priced in the level's Coins and the stores, like the shelters.
            Restaurant = new RestaurantService(balance.restaurant, Ledger, new CoinWallet(), Machine,
                                               () => Compound.IsBreached);

            // Last of all, because an attack touches nearly everything above: the restaurant's
            // health, the yard inside the walls, the turrets in it, the reputation and the purse.
            EncounterTuning encounter = balance.encounter ?? (balance.encounter = new EncounterTuning());

            Outdoor = new OutdoorDamageService(encounter, Ledger, new CoinWallet(), Machine,
                                               at => Compound.HasInterior && Compound.IsInside(at), Defense)
            {
                Scanner = () => OutdoorObjects.Tag(Compound, Restaurant, encounter.outdoorObjectHealth),
            };

            Encounters = new EncounterService(balance, Reputation, Ledger, Restaurant, Outdoor,
                                              LastOrdersBridge.NotifyCoinsPaid, LastOrdersBridge.AddExperience);
            Context.Encounters = Encounters;

            // The morning forecast reads the next attack rather than guessing at it.
            ThreatProfile.Planner = day => Encounters.Preview(Encounters.NextAttackDay(day));

            WireCrossCuttingEvents();
        }

        readonly List<IDefenseTarget> defenseTargets = new List<IDefenseTarget>();

        /// <summary>
        /// Everything a tower or turret may shoot at: the night's zombies, and the ones lurking on
        /// land still to be cleared, for a defense standing near enough to it. Refilled on every
        /// call - once a frame, by the defenses - into the same list.
        /// </summary>
        IReadOnlyList<IDefenseTarget> DefenseTargets()
        {
            defenseTargets.Clear();

            IReadOnlyList<EnemyAgent> night = Night.LiveEnemies;
            for (int i = 0; i < night.Count; i++) defenseTargets.Add(night[i]);

            IReadOnlyList<IAreaZombie> lurking = Field != null ? Field.Zombies : null;
            if (lurking == null) return defenseTargets;

            for (int i = 0; i < lurking.Count; i++)
                if (lurking[i] is IDefenseTarget target && lurking[i].IsAlive) defenseTargets.Add(target);

            return defenseTargets;
        }

        /// <summary>
        /// The subscriptions that would otherwise be scattered across the systems they
        /// connect. Gathered here so that "what reacts to what" is answerable by reading one
        /// method rather than by grepping for every subscription in the project.
        /// </summary>
        void WireCrossCuttingEvents()
        {
            // The build layer is the only thing that changes the shape of the restaurant, so
            // it is the only thing that needs to invalidate the perimeter and the nav seeds.
            Commands.OnCommandExecuted += _ => RefreshSpatialState();
            Commands.OnCommandUndone += _ => RefreshSpatialState();

            // A daytime incident cannot outlive the day. The siege is its own phase with its
            // own rules, and an incident still standing at dusk would leave the road closed
            // through a night and into a morning with nothing left to end it.
            Machine.OnPhaseChanged += (_, current) =>
            {
                if (current != DayPhase.Day) Incidents.End();
            };

            // Clearing the last piece of debris on a patch of land is what readies it to expand into.
            Clearing.OnDebrisCleared += (debrisId, _) =>
                Expansion.HandleDebrisCleared(Clearing.AreaOf(debrisId), debrisId);

            // A night's danger is measured from its first second and settled on its way out -
            // before Resolution tallies the report and hands it to the dawn screen, so who was
            // hurt is on that screen.
            Machine.OnPhaseChanged += (_, current) =>
            {
                if (current == DayPhase.Night) Danger.BeginNight();
            };

            Machine.OnPhaseExiting += (previous, next) =>
            {
                if (previous == DayPhase.Night && next == DayPhase.Resolution) SettleNight();
            };

            // Whether a zombie ever got inside the walls is one of the things an attack's result
            // is measured by.
            Compound.OnBreached += Encounters.NoteBreach;
        }

        /// <summary>Rolls for the night's injury and writes the night's danger into the report.</summary>
        void SettleNight()
        {
            DayReport report = Context.Report;
            report.ZombiesAtTheDoor = Danger.ReachedDoorCount;

            NightInjury injury = Danger.Resolve(Time.CurrentDay);
            if (!injury.Happened) return;

            if (injury.Survivor != null)
            {
                report.InjuredSurvivors.Add(injury.Survivor.Name);
                Debug.Log($"[Last Orders] A difficult night ({injury.Pressure:0.#} zombie-seconds at the door): " +
                          $"{injury.Survivor.Name} was hurt and is in the {Hospital.DisplayName}.");
            }
            else
            {
                report.InjuredVisitors.Add(injury.Patient.DisplayName);
                Debug.Log($"[Last Orders] A difficult night ({injury.Pressure:0.#} zombie-seconds at the door): " +
                          $"a visitor, {injury.Patient.DisplayName}, was hurt and is in the {Hospital.DisplayName}.");
            }
        }

        void RefreshSpatialState()
        {
            Perimeter.Recompute();
            Perimeter.SeedNavigationFields();
        }

        /// <summary>
        /// Seeds a brand new campaign. Called only when there is no save to restore, so a
        /// returning player is never handed a second starting kit.
        /// </summary>
        public void SeedNewGame()
        {
            Ledger.Add(Balance.startingResources);

            // The 14 opening ledger, for the resources that balance asset predates. Added here
            // rather than edited into the asset so the other design's starting kit is untouched.
            Ledger.Add(ResourceType.Wood, 20);
            Ledger.Add(ResourceType.Tomato, 4);

            // Enough to put the yard back together once, before the first attack has paid any.
            int kits = Balance.encounter != null ? Balance.encounter.startingRepairKits : 0;
            if (kits > 0) Ledger.Add(ResourceType.RepairKit, kits);

            for (int i = 0; i < Balance.startingSurvivorCount; i++)
            {
                Survivors.Add(new Survivor
                {
                    Id = $"survivor_{i}",
                    Name = NameFor(i),

                    // The first Survivor is a Cook. With nobody in the kitchen the player
                    // does every station themselves and the clock destroys them, so this is
                    // the one role the game cannot open without.
                    Role = RoleFor(i),
                    Trait = null,
                });
            }

            // The starter guard, for the tower the restaurant opens with. On top of the roster
            // above rather than one of its role slots: the tower cannot fire without them, and
            // cycling roles would only reach Guard with a four-person opening roster.
            SurvivorRoleDefinition guard = FindRole(SurvivorRole.Guard);
            if (Balance.startingGuardCount > 0 && guard == null)
            {
                Debug.LogWarning("[Last Orders] GameBalance has no Guard role, so the starting guard " +
                                 "tower has nobody to stand watch in it and will not fire. Run " +
                                 "'Last Orders / Generate Starting Content'.");
            }

            for (int i = 0; guard != null && i < Balance.startingGuardCount; i++)
            {
                int index = Balance.startingSurvivorCount + i;

                Survivors.Add(new Survivor
                {
                    Id = $"survivor_{index}",
                    Name = NameFor(index),
                    Role = guard,
                    Trait = null,
                });
            }
        }

        string NameFor(int index)
        {
            string[] names = Balance.survivorNames;
            if (names == null || names.Length == 0) return $"Survivor {index + 1}";
            return names[index % names.Length];
        }

        SurvivorRoleDefinition RoleFor(int index)
        {
            SurvivorRoleDefinition[] roles = Balance.survivorRoles;
            if (roles == null || roles.Length == 0) return null;

            if (index == 0)
            {
                SurvivorRoleDefinition cook = FindRole(SurvivorRole.Cook);
                if (cook != null) return cook;
            }

            return roles[index % roles.Length];
        }

        SurvivorRoleDefinition FindRole(SurvivorRole role)
        {
            SurvivorRoleDefinition[] roles = Balance.survivorRoles;
            if (roles == null) return null;

            for (int i = 0; i < roles.Length; i++)
                if (roles[i] != null && roles[i].role == role) return roles[i];

            return null;
        }

        /// <summary>Starts the cycle. Separate from construction so every listener can
        /// subscribe before the first Dawn fires - otherwise the game misses the event that
        /// starts it.</summary>
        public void Start(DayPhase phase = DayPhase.Dawn) => Machine.Start(phase);

        public void Tick(float deltaSeconds)
        {
            Machine.Tick(deltaSeconds);

            // Builds run on the game loop's clock too, so they stop when the game does.
            Expansion.Tick(deltaSeconds);

            // And so do the guards: a paused game is not one where the towers keep shooting.
            Defense.Tick(deltaSeconds);
            GroundDefenses.Tick(deltaSeconds);

            Hospital.Tick(deltaSeconds);

            if (Machine.CurrentPhase == DayPhase.Night) Danger.Tick(deltaSeconds);

            Compound.Tick(deltaSeconds);
        }
    }
}
