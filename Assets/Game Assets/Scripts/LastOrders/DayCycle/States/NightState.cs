using System;
using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Night;
using UnityEngine;

namespace LastOrders.DayCycle.States
{
    /// <summary>
    /// The siege. Two to four real minutes, and there is no lose state (13.1).
    ///
    /// The player's build resolves the combat; this state only runs the schedule, drains
    /// Screecher Heat, and counts what died. Enemies need no direction because the flow
    /// fields already know where everything is going.
    ///
    /// Surviving to dawn is the win condition, and it is the only one. What varies is how
    /// much of the restaurant is still standing, which 15 denominates entirely in days of
    /// slower growth.
    /// </summary>
    public sealed class NightState : IDayPhaseState
    {
        readonly DayCycleContext context;
        readonly DayCycleMachine machine;
        readonly List<EnemyAgent> live = new List<EnemyAgent>();

        float elapsed;
        int nextSpawnIndex;
        int nextWarningIndex;

        public NightState(DayCycleContext context, DayCycleMachine machine)
        {
            this.context = context;
            this.machine = machine;
        }

        public DayPhase Phase => DayPhase.Night;

        /// <summary>0 to 1 through the night, for the dawn-approaching indicator. Knowing how
        /// close morning is turns a grim wait into a countdown worth holding on through.</summary>
        public float Progress
        {
            get
            {
                float duration = context.TonightsWave?.DurationSeconds ?? 1f;
                return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            }
        }

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

        /// <summary>Seconds of warning before each group, from the balance asset.</summary>
        float WarningLead => context.Balance != null ? context.Balance.incomingWarningSeconds : 6f;

        /// <summary>Seconds past the night's length before dawn comes whatever is still out there.</summary>
        float DawnGrace => context.Balance != null ? context.Balance.dawnGraceSeconds : 20f;

        public void OnEnter(DayPhase previous)
        {
            elapsed = 0f;
            nextSpawnIndex = 0;
            nextWarningIndex = 0;
            live.Clear();
        }

        public void OnTick(float deltaSeconds)
        {
            elapsed += deltaSeconds;

            context.CrisisBar.Tick(deltaSeconds);
            RaiseDueWarnings();
            ReleaseDueSpawns();
            DrainEmittedHeat();

            WavePlan plan = context.TonightsWave;
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
            // period to finish it, and after that the sun comes up regardless. Whatever is left
            // is dismissed on exit, and what it did at the door has already counted.
            bool graceOver = elapsed >= plan.DurationSeconds + DawnGrace;

            if (live.Count == 0 || graceOver)
                machine.RequestTransition(DayPhase.Resolution);
        }

        /// <summary>Warns about each group a few seconds before it arrives. A group due inside
        /// the lead at the very start of the night is warned about at once.</summary>
        void RaiseDueWarnings()
        {
            WavePlan plan = context.TonightsWave;
            if (plan == null) return;

            float lead = WarningLead;

            while (nextWarningIndex < plan.Groups.Count &&
                   plan.Groups[nextWarningIndex].TimeSeconds - lead <= elapsed)
            {
                OnGroupIncoming?.Invoke(plan.Groups[nextWarningIndex]);
                nextWarningIndex++;
            }
        }

        public void OnExit(DayPhase next)
        {
            // Anything still alive is dismissed rather than killed: the sun is up, and 15
            // does not allow the night to keep taking things after it has ended.
            live.Clear();
        }

        void ReleaseDueSpawns()
        {
            WavePlan plan = context.TonightsWave;
            if (plan == null) return;

            while (nextSpawnIndex < plan.Spawns.Count &&
                   plan.Spawns[nextSpawnIndex].TimeSeconds <= elapsed)
            {
                WaveSpawn spawn = plan.Spawns[nextSpawnIndex];
                OnSpawnRequested?.Invoke(spawn.Enemy, spawn.Lane);
                nextSpawnIndex++;
            }
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
