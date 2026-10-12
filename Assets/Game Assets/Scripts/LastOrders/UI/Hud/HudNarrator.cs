using System;
using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.DayCycle;
using LastOrders.DayCycle.States;
using LastOrders.Night;
using LastOrders.Progression;
using UnityEngine;

namespace LastOrders.UI.Hud
{
    /// <summary>How a line should look: calm, good news, a warning, or danger.</summary>
    public enum HudTone
    {
        Info,
        Good,
        Warning,
        Danger,
    }

    /// <summary>The one primary objective: a heading, a sentence, and the icon that goes with it.</summary>
    public readonly struct HudObjective
    {
        public readonly string Title;
        public readonly string Body;
        public readonly string Icon;
        public readonly HudTone Tone;

        public HudObjective(string title, string body, string icon, HudTone tone = HudTone.Info)
        {
            Title = title;
            Body = body;
            Icon = icon;
            Tone = tone;
        }

        public bool SameAs(HudObjective other) => Title == other.Title && Body == other.Body && Icon == other.Icon;
    }

    /// <summary>
    /// What the HUD says, in the player's words: the objective line and the news of the night.
    ///
    /// Lifted out of <see cref="Blockout.CompoundHud"/> so the grey-box HUD and the gameplay HUD say
    /// exactly the same things. It reads state and decides nothing.
    ///
    /// The objective is derived rather than authored. The opening is a chain - clear the entrance,
    /// cook something, open up, serve the people who notice - and every link is already a
    /// progression flag, so the line can never ask for something the player has already done.
    /// </summary>
    public sealed class HudNarrator : IDisposable
    {
        readonly GameComposition composition;
        readonly FoodStreet.Visitors.VisitorManager visitors;

        /// <summary>
        /// Whether this level has a blocked entrance at all. Read from the scene, because asking for
        /// it in a level without one would leave the objective stuck on step one forever.
        /// </summary>
        readonly bool waitsOnEntrance;

        /// <summary>Said once an attack, as the restaurant's health drops into the red.</summary>
        bool lowHealthWarned;

        /// <summary>News worth saying out loud: a wave, a wreck, the walls coming down.</summary>
        public event Action<string, HudTone> OnMessage;

        /// <summary>
        /// Whether a new wave and a fallen restaurant are said as messages. A HUD that announces
        /// them with a banner of its own turns this off, so the same news is not said twice.
        /// </summary>
        public bool AnnouncesWaves { get; set; } = true;

        public HudNarrator(GameComposition composition, FoodStreet.Visitors.VisitorManager visitors, bool waitsOnEntrance)
        {
            this.composition = composition;
            this.visitors = visitors;
            this.waitsOnEntrance = waitsOnEntrance;

            composition.Night.OnGroupIncoming += HandleGroupIncoming;
            composition.Danger.OnDoorReached += HandleDoorReached;
            composition.Compound.OnUnderAttack += HandleWallUnderAttack;
            composition.Compound.OnStructureDown += HandleWallDown;
            composition.Compound.OnBreached += HandleBreached;
            composition.Compound.OnCleared += HandleCleared;
            composition.Compound.OnRepaired += HandleWallsRepaired;
            composition.Encounters.OnWaveStarted += HandleWaveStarted;
            composition.Encounters.OnDefeated += HandleDefeated;
            composition.Outdoor.OnWrecked += HandleWrecked;
            composition.Outdoor.OnCapReached += HandleCapReached;
            composition.Restaurant.OnHealthChanged += HandleRestaurantHealth;
        }

        public void Dispose()
        {
            composition.Night.OnGroupIncoming -= HandleGroupIncoming;
            composition.Danger.OnDoorReached -= HandleDoorReached;
            composition.Compound.OnUnderAttack -= HandleWallUnderAttack;
            composition.Compound.OnStructureDown -= HandleWallDown;
            composition.Compound.OnBreached -= HandleBreached;
            composition.Compound.OnCleared -= HandleCleared;
            composition.Compound.OnRepaired -= HandleWallsRepaired;
            composition.Encounters.OnWaveStarted -= HandleWaveStarted;
            composition.Encounters.OnDefeated -= HandleDefeated;
            composition.Outdoor.OnWrecked -= HandleWrecked;
            composition.Outdoor.OnCapReached -= HandleCapReached;
            composition.Restaurant.OnHealthChanged -= HandleRestaurantHealth;
        }

        void Say(string message, HudTone tone) => OnMessage?.Invoke(message, tone);

        // --- The news ------------------------------------------------------------------------------

        /// <summary>The warning before each group. Said on screen as well as over the road, because
        /// the player may well be looking at the kitchen.</summary>
        void HandleGroupIncoming(WaveGroup group) =>
            Say($"{group.Label} coming from {composition.Encounters.SideName(group.Lane)}!", HudTone.Warning);

        void HandleWaveStarted(int index, int total, EncounterWave wave)
        {
            lowHealthWarned = false;
            if (!AnnouncesWaves) return;

            Say(total == 1
                    ? $"Here they come - {wave.Count} zombies from {composition.Encounters.SidesOf(wave)}!"
                    : $"Wave {index + 1} of {total}: {wave.Count} zombies from {composition.Encounters.SidesOf(wave)}!",
                HudTone.Danger);
        }

        void HandleDefeated()
        {
            if (AnnouncesWaves) Say("The restaurant has fallen! Defense failed.", HudTone.Danger);
        }

        void HandleWrecked(Consequence.OutdoorWreck wreck) =>
            Say($"Zombies wrecked the {wreck.DisplayName.ToLowerInvariant()}!", HudTone.Danger);

        void HandleCapReached() => Say("The zombies have turned on the restaurant - hold the door!", HudTone.Danger);

        void HandleRestaurantHealth(float health, float max)
        {
            if (!composition.Restaurant.IsUnderAttack || max <= 0f) return;
            if (health / max >= 0.3f || lowHealthWarned || health <= 0f) return;

            lowHealthWarned = true;
            Say("The restaurant is badly damaged!", HudTone.Danger);
        }

        void HandleDoorReached() => Say("A zombie is at the diner door!", HudTone.Danger);

        void HandleWallUnderAttack(Perimeter.ICompoundStructure hit) =>
            Say($"Zombies are smashing the {hit.DisplayName.ToLowerInvariant()}!", HudTone.Warning);

        void HandleWallDown(Perimeter.ICompoundStructure fallen) =>
            Say($"The {fallen.DisplayName.ToLowerInvariant()} is down - they can get in!", HudTone.Danger);

        void HandleBreached() =>
            Say("Zombies are inside the walls! The staff are taking cover in the shelters and nobody is eating.", HudTone.Danger);

        void HandleCleared() => Say("The compound is clear. Back to work.", HudTone.Good);

        void HandleWallsRepaired(int count) =>
            Say($"Overnight the walls were patched up ({count} section{(count == 1 ? "" : "s")} repaired).", HudTone.Good);

        // --- The objective ---------------------------------------------------------------------------

        /// <summary>The next link in the opening chain, or the night's news when there is any.</summary>
        public HudObjective Objective()
        {
            ProgressionService progression = composition.Progression;
            int day = Mathf.Max(1, composition.Time.CurrentDay);

            // The night outranks the opening chain. "Cook something" is the wrong thing to be told
            // while zombies are coming up the road, and a rough morning is news.
            switch (composition.Machine.CurrentPhase)
            {
                case DayPhase.Dusk:
                    return Dusk();

                case DayPhase.Night:
                    return Night();

                case DayPhase.Resolution:
                    return MorningAfter();

                case DayPhase.Dawn:
                    if (RoughMorning(day, out HudObjective rough)) return rough;
                    break;
            }

            if (waitsOnEntrance && !progression.Has(ProgressionFlags.EntranceCleared))
                return new HudObjective("A Fresh Start", "Clear the debris from the diner entrance", HudSprites.IconClear);

            if (!progression.Has(ProgressionFlags.FirstMealCooked))
                return new HudObjective("Something To Sell", "Cook something. Visitors sit down and wait until they are fed.",
                                        HudSprites.IconCook);

            if (composition.Machine.CurrentPhase == DayPhase.Dawn)
                return new HudObjective($"Day {day}", "Open up when you are ready", HudSprites.IconDawn);

            // The visitor manager knows exactly why the forecourt is empty, and an empty forecourt
            // with no explanation is the single most confusing state this system can be in.
            string blocked = visitors != null ? visitors.StatusReason : null;
            if (!string.IsNullOrEmpty(blocked))
                return new HudObjective("No Visitors", char.ToUpperInvariant(blocked[0]) + blocked.Substring(1),
                                        HudSprites.IconVisitors, HudTone.Warning);

            // Somebody is sat there waiting. That is the most actionable thing on screen, so it
            // outranks the meal count.
            int waiting = visitors != null ? visitors.WaitingToBeServed : 0;
            if (waiting > 0)
            {
                // With waiters on the floor the kitchen is the job: they carry whatever is on the pass.
                string ask = LastOrders.Restaurant.Waiter.OnDuty > 0
                    ? "Cook what they asked for - the waiters bring it from the pass. Tap a visitor to serve them first."
                    : waiting == 1
                        ? "Tap them to serve what they asked for."
                        : "Tap one to serve them what they asked for.";

                return new HudObjective(waiting == 1 ? "A Visitor Is Waiting" : $"{waiting} Visitors Waiting", ask, HudSprites.IconServe);
            }

            int ready = composition.Meals.TotalReady;
            string pass = ready == 1 ? "1 meal on the pass" : $"{ready} meals on the pass";
            return new HudObjective("Serve The Road", $"{pass}. Keep the kitchen busy.", HudSprites.IconServe);
        }

        /// <summary>The warning: what is coming tonight, and whether anyone is up there to meet it.</summary>
        HudObjective Dusk()
        {
            WavePlan plan = composition.Context.TonightsWave;
            int coming = plan != null ? plan.Spawns.Count : 0;

            string forecast = coming switch
            {
                0 => "Nothing on the road tonight, by the look of it.",
                1 => "One zombie is coming.",
                _ when plan != null && plan.IsEncounter =>
                    $"{coming} zombies in {plan.Waves.Count} wave{(plan.Waves.Count == 1 ? "" : "s")}. Place and repair your defenses!",
                _ => $"About {coming} zombies are drawn in by the lights.",
            };

            return new HudObjective("Get Ready", Join(forecast, WatchLine()), HudSprites.IconZombie, HudTone.Danger);
        }

        HudObjective Night()
        {
            int live = composition.Night.LiveEnemyCount;
            string zombies = live switch
            {
                0 => "Quiet for now.",
                1 => "1 zombie out there.",
                _ => $"{live} zombies out there.",
            };

            // The wave itself is on the clock and the banner; the objective is what to do about it.
            return new HudObjective("Hold The Diner", Join(zombies, WatchLine()), HudSprites.IconZombie, HudTone.Danger);
        }

        /// <summary>The zombies are going: how it went, while the crew comes out.</summary>
        HudObjective MorningAfter()
        {
            EncounterResult result = composition.Encounters.LastResult;
            DayReport report = composition.Context.Report;

            if (report.Encounter == null || result == null)
                return new HudObjective($"Day {Mathf.Max(1, composition.Time.CurrentDay)}", "A quiet night. The sun is coming up.",
                                        HudSprites.IconDawn, HudTone.Good);

            return result.IsVictory
                ? new HudObjective("Victory!", "The zombies are slinking off.", HudSprites.IconTrophy, HudTone.Good)
                : new HudObjective("Defense Failed", "The zombies are slinking off.", HudSprites.IconZombie, HudTone.Danger);
        }

        /// <summary>Last night, if it left a mark: who is in the hospital tent, or how close it came.</summary>
        bool RoughMorning(int day, out HudObjective objective)
        {
            DayReport report = composition.Context.Report;

            // The attack's result comes first: it is what the morning is about.
            EncounterResult result = report.Encounter;
            if (result != null)
            {
                objective = result.IsVictory
                    ? new HudObjective("Victory!", result.ReputationLine, HudSprites.IconTrophy, HudTone.Good)
                    : new HudObjective("Defense Failed", result.ReputationLine, HudSprites.IconZombie, HudTone.Danger);
                return true;
            }

            // Survivors by name; visitors by what they are, since that is all the player knows them
            // as - "a trucker", not "Trucker".
            var hurt = new List<string>(report.InjuredSurvivors);
            foreach (string visitor in report.InjuredVisitors) hurt.Add(Indefinite(visitor));

            if (hurt.Count > 0)
            {
                string who = string.Join(" and ", hurt);
                who = char.ToUpperInvariant(who[0]) + who.Substring(1);

                objective = new HudObjective("A Rough Night",
                                             $"{who} got hurt and {(hurt.Count == 1 ? "is" : "are")} recovering in the hospital tent.",
                                             HudSprites.IconMedic, HudTone.Warning);
                return true;
            }

            // Day 1's dawn is the start of the game, not the morning after anything.
            if (report.ZombiesAtTheDoor > 0 && day > 1)
            {
                objective = new HudObjective("A Close Call",
                                             report.ZombiesAtTheDoor == 1
                                                 ? "A zombie reached the diner door last night. Nobody was hurt."
                                                 : $"{report.ZombiesAtTheDoor} zombies reached the diner door last night. Nobody was hurt.",
                                             HudSprites.IconShield, HudTone.Warning);
                return true;
            }

            objective = default;
            return false;
        }

        static string Join(string a, string b) => string.IsNullOrEmpty(b) ? a : $"{a} {b}";

        /// <summary>"a trucker", "an old farmer".</summary>
        static string Indefinite(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return "a visitor";

            string lower = kind.ToLowerInvariant();
            return "aeiou".IndexOf(lower[0]) >= 0 ? $"an {lower}" : $"a {lower}";
        }

        /// <summary>
        /// Who is on watch, or the problem if nobody is. The line that matters most at dusk: a tower
        /// with nobody in it is the difference between a quiet night and one at the door.
        /// </summary>
        string WatchLine()
        {
            IReadOnlyList<Defense.DefenseUnit> units = composition.Defense.All;

            string onWatch = null;
            int watching = 0;
            bool anyTower = false;

            for (int i = 0; i < units.Count; i++)
            {
                Defense.DefenseUnit unit = units[i];
                if (!unit.IsBuilt || !unit.NeedsCrew) continue;

                anyTower = true;
                if (unit.Crew == null || !unit.CrewOnDuty) continue;

                watching++;
                onWatch ??= unit.Crew.Name;
            }

            if (!anyTower) return string.Empty;
            if (watching == 0) return "Nobody is in the guard tower!";

            return watching == 1 ? $"{onWatch} is in the tower." : $"{watching} guards are on watch.";
        }

        // --- The attack cycle's status line -----------------------------------------------------------

        /// <summary>The band the next attack would come from, as it stands: "Low", "Medium", "High".</summary>
        public static string ThreatName(GameComposition composition)
        {
            EncounterRequest request = composition.Encounters.RequestFor(composition.Time.CurrentDay);
            return request.Tier != null ? request.Tier.name : "Low";
        }

        /// <summary>"Zombies attack tonight", "Next zombie attack in 2 days".</summary>
        public static string NextAttack(GameComposition composition)
        {
            int day = Mathf.Max(1, composition.Time.CurrentDay);
            return composition.Encounters.DaysUntilAttack(day) switch
            {
                0 => "Zombies attack tonight",
                1 => "Zombies attack tomorrow night",
                int days => $"Next zombie attack in {days} days",
            };
        }

        /// <summary>"0:42" - the preparation countdown.</summary>
        public static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        /// <summary>"ZOMBIES IN 0:42  ·  Medium threat, 2 waves".</summary>
        public static string Countdown(GameComposition composition)
        {
            string clock = Clock(composition.Dusk.SecondsRemaining);

            WavePlan plan = composition.Encounters.Current;
            if (plan == null) return $"ZOMBIES IN {clock}";

            string waves = plan.Waves.Count == 1 ? "1 wave" : $"{plan.Waves.Count} waves";
            return $"ZOMBIES IN {clock}  ·  {plan.ThreatName} threat, {waves}";
        }

        /// <summary>"WAVE 2/3  ·  from the north  ·  7 left", or the break between waves.</summary>
        public static string Wave(GameComposition composition)
        {
            NightState night = composition.Night;
            WavePlan plan = composition.Encounters.Current;

            if (plan == null || night.WaveCount == 0) return $"ATTACK  ·  {night.LiveEnemyCount} zombies";

            int shown = Mathf.Clamp(night.WaveIndex + 1, 1, night.WaveCount);

            if (night.BreakSecondsRemaining > 0f)
                return $"WAVE {shown} BEATEN  ·  next in {Mathf.CeilToInt(night.BreakSecondsRemaining)}s";

            EncounterWave wave = plan.Waves[Mathf.Clamp(night.WaveIndex, 0, plan.Waves.Count - 1)];
            return $"WAVE {shown}/{night.WaveCount}  ·  from {composition.Encounters.SidesOf(wave)}  ·  {night.LiveEnemyCount} left";
        }

        /// <summary>The line under the morning after an attack.</summary>
        public static string Aftermath(GameComposition composition)
        {
            EncounterResult last = composition.Encounters.LastResult;
            if (last == null) return "A quiet night";
            return last.IsVictory ? "Held! The zombies are slinking off" : "Defense failed - the zombies are drifting away";
        }
    }
}
