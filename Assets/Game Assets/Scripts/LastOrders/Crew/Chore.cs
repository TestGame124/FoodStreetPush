using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>How a chore is done: which work loop the body plays, what is in hand, where the hands go.</summary>
    public enum ChoreMotion
    {
        /// <summary>A cloth going round on a surface: a table top, the pass, a counter.</summary>
        Wipe,

        /// <summary>A broom swept side to side across the floor.</summary>
        Sweep,

        /// <summary>A feather duster flicked over something standing: a lamp, a shelf, a jukebox.</summary>
        Dust,
    }

    /// <summary>
    /// One small job that keeps the diner nice: where to stand, what to face, where the hands work,
    /// how it is done and for how long. Chores are offered by <see cref="IChoreSource"/>s and handed
    /// out by <see cref="ChoreBoard"/>; <see cref="ChoreRoutine"/> does any of them the same way, so a
    /// new kind of chore is a new source, not a new behaviour.
    /// </summary>
    public struct Chore
    {
        /// <summary>What it is, for the tap: "Wiping a table".</summary>
        public string Label;

        /// <summary>Where to stand to do it, on the floor.</summary>
        public Vector3 Stand;

        /// <summary>Where the hands work: the middle of the table top, the floor in front, the top of the lamp.</summary>
        public Vector3 Work;

        /// <summary>How far round <see cref="Work"/> the hands range.</summary>
        public float Reach;

        public ChoreMotion Motion;
        public float Seconds;

        /// <summary>How much it is wanted, against the others on offer.</summary>
        public float Weight;

        /// <summary>Done before anything that is not: a table left in a mess by the last diner.</summary>
        public bool Urgent;

        /// <summary>What it is done to. Two crew are never on the same one, and one done is not done
        /// again straight away. Compared with Equals, so a value pair works.</summary>
        public object Key;

        /// <summary>Optional. False once it no longer wants doing - somebody sat down at the table.</summary>
        public Func<bool> StillWanted;

        /// <summary>Optional. What finishing it does - the mess goes.</summary>
        public Action Done;

        public bool IsValid => Key != null && Seconds > 0f;

        /// <summary>The way to face while doing it.</summary>
        public Vector3 Facing
        {
            get
            {
                Vector3 to = Work - Stand;
                to.y = 0f;
                return to.sqrMagnitude > 1e-4f ? to.normalized : Vector3.forward;
            }
        }
    }

    /// <summary>
    /// Something in the diner that sometimes wants doing: the tables, the floor, the fittings. Asked a
    /// few times a minute for what it has to offer right now, from where the crew member is standing.
    /// </summary>
    public interface IChoreSource
    {
        void Offer(Vector3 from, List<Chore> into);
    }
}
