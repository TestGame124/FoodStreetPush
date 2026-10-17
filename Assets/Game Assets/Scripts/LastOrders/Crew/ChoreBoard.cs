using System.Collections.Generic;
using FoodStreet.Navigation;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// The diner's list of little jobs, and who is on which.
    ///
    /// Asks every <see cref="IChoreSource"/> what it has on offer and hands one out: a table left in a
    /// mess first, the nearest of them; otherwise a weighted pick among the rest, the closer the likelier
    /// so nobody crosses the room to dust a lamp beside the one they just walked past. A chore is held by
    /// one crew member at a time, and one finished is left alone for a while before it comes round again,
    /// so the same table is not wiped twice in a minute.
    ///
    /// The sources are the tables, the floor and the fittings; another kind of chore is another source
    /// <see cref="Register"/>ed here, with nothing else to change.
    /// </summary>
    public static class ChoreBoard
    {
        /// <summary>Seconds before something done is offered again, unless it is urgent.</summary>
        public const float AgainAfter = 45f;

        /// <summary>Metres at which a chore's pull halves.</summary>
        const float HalfPullDistance = 7f;

        static readonly List<IChoreSource> sources = new List<IChoreSource>();
        static readonly Dictionary<object, CrewBrain> taken = new Dictionary<object, CrewBrain>();
        static readonly Dictionary<object, float> doneAt = new Dictionary<object, float>();
        static readonly List<Chore> offered = new List<Chore>();
        static readonly List<float> scores = new List<float>();

        static ChoreBoard() => Defaults();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();

        /// <summary>Back to the diner's own sources, with nothing taken and nothing done.</summary>
        public static void Reset()
        {
            taken.Clear();
            doneAt.Clear();
            putOffUntil.Clear();
            Defaults();
        }

        static void Defaults()
        {
            sources.Clear();
            sources.Add(new TableChores());
            sources.Add(new FloorChores());
            sources.Add(new FixtureChores());
        }

        public static IReadOnlyList<IChoreSource> Sources => sources;

        public static void Register(IChoreSource source)
        {
            if (source != null && !sources.Contains(source)) sources.Add(source);
        }

        public static void Unregister(IChoreSource source) => sources.Remove(source);

        /// <summary>Only the given sources - for tests.</summary>
        public static void UseOnly(params IChoreSource[] only)
        {
            Reset();
            sources.Clear();
            sources.AddRange(only);
        }

        /// <summary>
        /// Picks the next chore for <paramref name="who"/>, standing at <paramref name="from"/>, and
        /// holds it for them. <paramref name="roll"/> is the 0-1 draw for the weighted pick.
        /// </summary>
        public static bool TryTake(CrewBrain who, Vector3 from, float roll, float now, out Chore chore)
        {
            chore = default;

            offered.Clear();
            for (int i = 0; i < sources.Count; i++) sources[i].Offer(from, offered);

            // A mess first: the nearest of them.
            int best = -1;
            float nearest = float.MaxValue;
            for (int i = 0; i < offered.Count; i++)
            {
                Chore c = offered[i];
                if (!c.Urgent || !Free(c, who) || PutOffAt(c.Key, now)) continue;

                float d = Flat(c.Stand - from);
                if (d < nearest)
                {
                    nearest = d;
                    best = i;
                }
            }

            if (best < 0)
            {
                scores.Clear();
                float total = 0f;
                for (int i = 0; i < offered.Count; i++)
                {
                    Chore c = offered[i];
                    bool fresh = !doneAt.TryGetValue(c.Key, out float when) || now - when >= AgainAfter;
                    float score = c.IsValid && fresh && Free(c, who) && !PutOffAt(c.Key, now)
                        ? Mathf.Max(0f, c.Weight) / (1f + Flat(c.Stand - from) / HalfPullDistance)
                        : 0f;
                    scores.Add(score);
                    total += score;
                }

                if (total <= 0f) return false;

                float pick = Mathf.Clamp01(roll) * total;
                for (int i = 0; i < scores.Count; i++)
                {
                    if (scores[i] <= 0f) continue;
                    best = i;
                    pick -= scores[i];
                    if (pick <= 0f) break;
                }
            }

            if (best < 0) return false;

            chore = offered[best];
            if (who != null) taken[chore.Key] = who;
            return true;
        }

        /// <summary>True while <paramref name="chore"/> is still held by <paramref name="who"/> and still wants doing.</summary>
        public static bool StillOn(in Chore chore, CrewBrain who)
        {
            if (chore.Key == null) return false;
            if (who != null && (!taken.TryGetValue(chore.Key, out CrewBrain holder) || holder != who)) return false;
            return chore.StillWanted == null || chore.StillWanted();
        }

        /// <summary>Lets go of a chore without doing it, for somebody else to take.</summary>
        public static void Drop(in Chore chore, CrewBrain who)
        {
            if (chore.Key != null && taken.TryGetValue(chore.Key, out CrewBrain holder) && holder == who) taken.Remove(chore.Key);
        }

        /// <summary>Seconds a chore that could not be reached is left before it is offered again.</summary>
        public const float PutOffFor = 20f;

        static readonly Dictionary<object, float> putOffUntil = new Dictionary<object, float>();

        /// <summary>Could not be got to: let go, and not offered again - mess or not - for a while.</summary>
        public static void PutOff(in Chore chore, CrewBrain who, float now)
        {
            if (chore.Key == null) return;

            Drop(chore, who);
            putOffUntil[chore.Key] = now + PutOffFor;
        }

        static bool PutOffAt(object key, float now) => putOffUntil.TryGetValue(key, out float until) && now < until;

        /// <summary>Done: let go, remembered as done, and whatever finishing it does is done.</summary>
        public static void Finish(in Chore chore, CrewBrain who, float now)
        {
            if (chore.Key == null) return;

            Drop(chore, who);
            doneAt[chore.Key] = now;
            chore.Done?.Invoke();
        }

        /// <summary>Who holds the chore on <paramref name="key"/>, or null.</summary>
        public static CrewBrain HolderOf(object key) => key != null && taken.TryGetValue(key, out CrewBrain who) ? who : null;

        static bool Free(in Chore chore, CrewBrain who) =>
            chore.Key != null && (!taken.TryGetValue(chore.Key, out CrewBrain holder) || holder == who || holder == null);

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        // --- Shared by the sources -------------------------------------------------------------------

        /// <summary>True if a body <paramref name="radius"/> wide can stand at <paramref name="world"/> on the nav grid -
        /// or there is no grid to ask.</summary>
        internal static bool IsOpen(Vector3 world, float radius)
        {
            NavigationSystem nav = NavigationSystem.Instance;
            GridService grid = nav != null ? nav.Grid : null;
            if (grid == null) return true;

            Vector2Int cell = grid.Grid.WorldToCell(world);
            if (!grid.Grid.InBounds(cell) || !grid.IsWalkable(cell)) return false;
            return grid.GetClearance(cell.x, cell.y) >= grid.ClearanceForRadius(radius);
        }
    }
}
