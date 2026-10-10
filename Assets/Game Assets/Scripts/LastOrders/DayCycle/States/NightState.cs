using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Night;
using UnityEngine;

namespace LastOrders.DayCycle.States
{
    /// <summary>
    /// The attack. The restaurant is shut, nobody is on the road, and the dead come.
    ///
    /// A zombie attack runs in up to three short waves. Each one sets off from its own side of the
    /// compound once the last has been beaten - or once it has been given long enough, so a
    /// straggler nothing can see never holds the night open. The placed defenses do the fighting;
    /// this state only runs the schedule and watches for the two ways it ends: every wave beaten,
    /// which is a victory, or the restaurant's health gone, which ends it on the spot.
    ///
    /// Either way the zombies still standing are not killed when it ends: they turn and slink off
    /// into the dark as the morning comes.
    ///
    /// A night handed a plan with no waves - the old timed design, kept for its tests - releases
    /// its spawns on the clock and ends when the clock and the grace run out.
    /// </summary>
    public sealed class NightState : IDayPhaseState
    {
        readonly DayCycleContext context;
        readonly DayCycleMachine machine;
        readonly List<EnemyAgent> live = new List<EnemyAgent>();

        float elapsed;
        int nextSpawnIndex;
        int nextWarningIndex;

        // The wave in hand, when the night runs in waves.
        int waveIndex;
        float waveElapsed;
        float breakLeft;
        bool waveRunning;

        public NightState(DayCycleContext context, DayCycleMachine machine)
        {
            this.context = context;
            this.machine = machine;
        }

        public DayPhase Phase => DayPhase.Night;

        WavePlan Plan => context.TonightsWave;

        bool RunsInWaves => Plan != null && Plan.IsEncounter;

        /// <summary>0 to 1 through the night, for the dawn-approaching indicator. Knowing how
        /// close morning is turns a grim wait into a countdown worth holding on through.</summary>
        public float Progress
        {
            get
            {
                if (RunsInWaves)
                {
                    int waves = Plan.Waves.Count;
                    float within = waveRunning ? Mathf.Clamp01(waveElapsed / Mathf.Max(1f, Tuning.waveTimeoutSeconds)) : 0f;
                    return Mathf.Clamp01((Mathf.Min(waveIndex, waves) + within) / Mathf.Max(1, waves));
                }

                float duration = Plan?.DurationSeconds ?? 1f;
                return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            }
        }

        /// <summary>The wave under way, from 0. Equal to the number of waves once they are all out.</summary>
        public int WaveIndex => waveIndex;

        public int WaveCount => RunsInWaves ? Plan.Waves.Count : 0;

        /// <summary>Seconds of breathing room left before the next wave, or zero.</summary>
        public float BreakSecondsRemaining => Mathf.Max(0f, breakLeft);

        public int LiveEnemyCount => live.Count;

        /// <summary>Everything on the map tonight that is still alive. What the guard towers
        /// and turrets choose their targets from.</summary>
        public IReadOnlyList<EnemyAgent> LiveEnemies => live;

        /// <summary>Raised when an enemy should be spawned, with its definition and lane.
        /// The spawner owns the pool and the road positions; this state owns the schedule.</summary>
        public event Action<EnemyDefinition, int> OnSpawnRequested;

        /// <summary>
        /// A group is about to arrive, a few seconds before the first of it does. The warning the
        /// player gets for each knot of zombies coming up the road - raised once per group, not
        /// once per zombie.
        /// </summary>
        public event Action<WaveGroup> OnGroupIncoming;

        /// <summary>The zombies left standing as the night ends, about to slink off. The spawner
        /// turns them round.</summary>
        public event Action<IReadOnlyList<EnemyAgent>> OnRetreat;

        /// <summary>Seconds of warning before each group, from the balance asset.</summary>
        float WarningLead => context.Balance != null ? context.Balance.incomingWarningSeconds : 6f;

        /// <summary>Seconds past the night's length before dawn comes whatever is still out there.</summary>
        float DawnGrace => context.Balance != null ? context.Balance.dawnGraceSeconds : 20f;

        EncounterTuning Tuning => context.Balance != null && context.Balance.encounter != null
            ? context.Balance.encounter
            : DefaultTuning;

        static readonly EncounterTuning DefaultTuning = new EncounterTuning();

        public void OnEnter(DayPhase previous)
        {
            elapsed = 0f;
            nextSpawnIndex = 0;
            nextWarningIndex = 0;
            live.Clear();

            waveIndex = 0;
            waveElapsed = 0f;
            breakLeft = 0f;
            waveRunning = false;

            if (RunsInWaves) StartWave(0);
        }

        public void OnTick(float deltaSeconds)
        {
            elapsed += deltaSeconds;

            context.CrisisBar?.Tick(deltaSeconds);
            DrainEmittedHeat();

            // Lost: the restaurant's health is gone, and the attack ends there and then.
            if (context.Encounters != null && context.Encounters.IsDefeated)
            {
                machine.RequestTransition(DayPhase.Resolution);
                return;
            }

            if (RunsInWaves)
            {
                TickWaves(deltaSeconds);
                return;
            }

            RaiseDueWarnings();
            ReleaseDueSpawns();

            WavePlan plan = Plan;
            if (plan == null)
            {
                machine.RequestTransition(DayPhase.Resolution);
                return;
            }

            // Dawn comes when the night's duration is up AND nothing is still standing in
            // the restaurant. Ending on the timer alone would cut away mid-breach; waiting
            // only for the last kill would let one wandering Shambler hold the player
            // hostage. Both conditions together is what makes the dawn sting land.
            bool timeUp = elapsed >= plan.DurationSeconds;
            bool allSpawned = nextSpawnIndex >= plan.Spawns.Count;

            if (!timeUp || !allSpawned) return;

            // The hostage case is real: a zombie that reaches a door no tower can see stands
            // there, alive, for as long as it is allowed to. So the survivors get a grace
            // period to finish it, and after that the sun comes up regardless.
            bool graceOver = elapsed >= plan.DurationSeconds + DawnGrace;

            if (live.Count == 0 || graceOver)
                machine.RequestTransition(DayPhase.Resolution);
        }

        // --- Waves --------------------------------------------------------------------------------

        void StartWave(int index)
        {
            waveIndex = index;
            waveElapsed = 0f;
            breakLeft = 0f;
            nextSpawnIndex = 0;
            nextWarningIndex = 0;
            waveRunning = true;

            context.Encounters?.NoteWaveStarted(index);
        }

        void TickWaves(float deltaSeconds)
        {
            WavePlan plan = Plan;

            if (!waveRunning)
            {
                breakLeft -= deltaSeconds;
                if (breakLeft <= 0f) StartWave(waveIndex + 1);
                return;
            }

            EncounterWave wave = plan.Waves[waveIndex];
            waveElapsed += deltaSeconds;

            RaiseDueWarnings(wave.Groups, waveElapsed);
            ReleaseDueSpawns(wave.Spawns, waveElapsed);

            bool allOut = nextSpawnIndex >= wave.Spawns.Count;
            if (!allOut) return;

            bool beaten = live.Count == 0;
            bool last = waveIndex >= plan.Waves.Count - 1;
            float timeout = Mathf.Max(10f, Tuning.waveTimeoutSeconds);

            if (last)
            {
                // The last wave: held when it is beaten, or - if something out there cannot be
                // reached - once it has had its time and a grace on top, and whatever is left
                // slinks off with the morning.
                if (beaten || waveElapsed >= timeout + Tuning.stragglerGraceSeconds)
                {
                    context.Encounters?.NoteWaveBeaten(waveIndex);
                    waveRunning = false;
                    waveIndex = plan.Waves.Count;
                    machine.RequestTransition(DayPhase.Resolution);
                }

                return;
            }

            if (beaten)
            {
                context.Encounters?.NoteWaveBeaten(waveIndex);
                waveRunning = false;
                breakLeft = Mathf.Max(0.01f, Tuning.breakBetweenWavesSeconds);
                return;
            }

            // Not beaten, but given its time: the next one comes anyway, and these stay in the fight.
            if (waveElapsed >= timeout)
            {
                waveRunning = false;
                breakLeft = 0.01f;
            }
        }

        void RaiseDueWarnings(List<WaveGroup> groups, float now)
        {
            float lead = WarningLead;

            while (nextWarningIndex < groups.Count && groups[nextWarningIndex].TimeSeconds - lead <= now)
            {
                OnGroupIncoming?.Invoke(groups[nextWarningIndex]);
                nextWarningIndex++;
            }
        }

        void ReleaseDueSpawns(List<WaveSpawn> spawns, float now)
        {
            while (nextSpawnIndex < spawns.Count && spawns[nextSpawnIndex].TimeSeconds <= now)
            {
                WaveSpawn spawn = spawns[nextSpawnIndex];
                OnSpawnRequested?.Invoke(spawn.Enemy, spawn.Lane);
                nextSpawnIndex++;
            }
        }

        // --- The timed night ------------------------------------------------------------------------

        /// <summary>Warns about each group a few seconds before it arrives. A group due inside
        /// the lead at the very start of the night is warned about at once.</summary>
        void RaiseDueWarnings()
        {
            WavePlan plan = Plan;
            if (plan == null) return;

            RaiseDueWarnings(plan.Groups, elapsed);
        }

        void ReleaseDueSpawns()
        {
            WavePlan plan = Plan;
            if (plan == null) return;

            ReleaseDueSpawns(plan.Spawns, elapsed);
        }

        public void OnExit(DayPhase next)
        {
            // Anything still standing is not killed: the night is over, and it slinks off into the
            // dark. It is off the list the defenses and the breach check read from this moment.
            if (live.Count > 0)
            {
                var leaving = new List<EnemyAgent>(live);
                live.Clear();

                for (int i = 0; i < leaving.Count; i++)
                    if (leaving[i] != null) leaving[i].OnDied -= HandleDied;

                OnRetreat?.Invoke(leaving);
            }

            waveRunning = false;
            breakLeft = 0f;
        }

        /// <summary>
        /// Collects Heat emitted by Screechers.
        ///
        /// Pulled from the agents rather than pushed by them, so a pooled agent released
        /// mid-emission cannot report the same Heat twice. It lands on tomorrow's total,
        /// because tonight's is locked.
        /// </summary>
        void DrainEmittedHeat()
        {
            if (context.Heat == null) return;

            for (int i = 0; i < live.Count; i++)
            {
                if (live[i] == null) continue;

                float emitted = live[i].ConsumeEmittedHeat();
                if (emitted > 0f) context.Heat.AddFromEnemy(emitted);
            }
        }

        /// <summary>Registers a spawned agent so the night can track it. Called by the
        /// spawner immediately after it takes one from the pool.</summary>
        public void Register(EnemyAgent agent)
        {
            if (agent == null) return;

            live.Add(agent);
            agent.OnDied += HandleDied;
        }

        void HandleDied(EnemyAgent agent, EnemyDefinition definition)
        {
            agent.OnDied -= HandleDied;
            live.Remove(agent);

            context.Report.EnemiesKilled++;

            if (definition != null && definition.drops != null)
                context.Ledger.Add(definition.drops);
        }
    }
}
