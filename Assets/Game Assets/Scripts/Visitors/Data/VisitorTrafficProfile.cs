using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// How busy the place gets, in one asset.
    ///
    /// The director owns no tuning numbers of its own. Everything that decides whether a day
    /// feels busy or lonely is here, so it can be changed without entering play mode and
    /// without dirtying a scene - and so two levels can run different traffic off one set of
    /// code.
    ///
    /// <see cref="busySlowdown"/> is the one non-obvious field and it earns its place. Arrivals
    /// are already hard-gated on a free chair and a free bay, but a gate on its own produces a
    /// restaurant that is either empty or permanently at capacity. Stretching the interval as
    /// the room fills means the last chair is taken slowly enough that the player can see it
    /// coming.
    /// </summary>
    [CreateAssetMenu(menuName = "Food Street/Visitors/Traffic Profile", fileName = "VisitorTraffic")]
    public sealed class VisitorTrafficProfile : ScriptableObject
    {
        [Header("Who comes")]
        public VisitorDefinition[] visitors;

        [Header("How often")]
        [Tooltip("Real seconds between arrival attempts, before the busy slowdown.")]
        public Vector2 arrivalInterval = new Vector2(8f, 16f);

        [Tooltip("Real seconds after the scene loads before the first car sets off.")]
        public Vector2 firstArrivalDelay = new Vector2(2f, 4f);

        [Tooltip("Hard ceiling on visitors on the map at once at level 1, whatever the chairs and " +
                 "bays say. A safety net, not the main gate.")]
        [Min(1)] public int maxConcurrent = 8;

        [Tooltip("Visitors added to the ceiling for each level above 1.")]
        [Min(0f)] public float extraConcurrentPerLevel = 0.5f;

        [Tooltip("Never more than this added to the ceiling from the level.")]
        [Min(0)] public int maxExtraConcurrent = 6;

        [Tooltip("Free-chair ratio (0 = full, 1 = empty) to a multiplier on the arrival " +
                 "interval. Left flat at 1 it has no effect.")]
        public AnimationCurve busySlowdown = new AnimationCurve(
            new Keyframe(0f, 2.5f), new Keyframe(0.5f, 1.4f), new Keyframe(1f, 1f));

        /// <summary>The ceiling on visitors on the map at once at <paramref name="level"/>.</summary>
        public int MaxConcurrent(int level) =>
            Mathf.Max(1, maxConcurrent) + Mathf.Min(Mathf.Max(0, maxExtraConcurrent),
                                                   Mathf.FloorToInt(Mathf.Max(0, level - 1) * Mathf.Max(0f, extraConcurrentPerLevel)));

        /// <summary>Anyone at all, whatever the level and Safety.</summary>
        public VisitorDefinition PickVisitor() => Pick(v => true, Random.value);

        /// <summary>
        /// One of the kinds that come at <paramref name="level"/> and <paramref name="safety"/>, by
        /// weight. When none of them does - a profile tuned past the start - anyone at all, so the
        /// road is never empty for want of a match.
        /// </summary>
        public VisitorDefinition PickVisitor(int level, float safety, float roll01) =>
            Pick(v => v.ComesAt(level, safety), roll01) ?? Pick(v => true, roll01);

        public VisitorDefinition PickVisitor(int level, float safety) => PickVisitor(level, safety, Random.value);

        /// <summary>The kinds that come at <paramref name="level"/> and <paramref name="safety"/>.</summary>
        public System.Collections.Generic.List<VisitorDefinition> Coming(int level, float safety)
        {
            var coming = new System.Collections.Generic.List<VisitorDefinition>();
            if (visitors == null) return coming;

            for (int i = 0; i < visitors.Length; i++)
                if (visitors[i] != null && visitors[i].spawnWeight > 0f && visitors[i].ComesAt(level, safety)) coming.Add(visitors[i]);

            return coming;
        }

        VisitorDefinition Pick(System.Func<VisitorDefinition, bool> allowed, float roll01)
        {
            if (visitors == null || visitors.Length == 0) return null;

            float total = 0f;
            for (int i = 0; i < visitors.Length; i++)
            {
                if (visitors[i] != null && allowed(visitors[i])) total += Mathf.Max(0f, visitors[i].spawnWeight);
            }

            if (total <= 0f) return null;

            float pick = Mathf.Clamp01(roll01) * total;
            VisitorDefinition last = null;

            for (int i = 0; i < visitors.Length; i++)
            {
                if (visitors[i] == null || !allowed(visitors[i]) || visitors[i].spawnWeight <= 0f) continue;

                last = visitors[i];
                pick -= visitors[i].spawnWeight;
                if (pick <= 0f) return visitors[i];
            }

            return last;
        }

        /// <summary>Seconds until the next attempt, stretched by how full the room is.</summary>
        public float RollInterval(float freeChairRatio)
        {
            float basis = Random.Range(arrivalInterval.x, arrivalInterval.y);

            float scale = busySlowdown != null && busySlowdown.length > 0
                ? Mathf.Max(0.1f, busySlowdown.Evaluate(Mathf.Clamp01(freeChairRatio)))
                : 1f;

            return basis * scale;
        }

        public float RollFirstDelay() => Random.Range(firstArrivalDelay.x, firstArrivalDelay.y);

        /// <summary>
        /// The largest bay any vehicle in the profile needs.
        ///
        /// The parking builder sizes every bay to this rather than to each car, so the mix of
        /// vehicles can change without re-laying the car park - and so a long car can never be
        /// handed a bay it overhangs.
        /// </summary>
        public Vector2 LargestBay()
        {
            var largest = new Vector2(2.6f, 5f);
            if (visitors == null) return largest;

            for (int i = 0; i < visitors.Length; i++)
            {
                VehicleDefinition vehicle = visitors[i] != null ? visitors[i].vehicle : null;
                if (vehicle == null) continue;

                largest = Vector2.Max(largest, vehicle.bayFootprint);
            }

            return largest;
        }

        void OnValidate()
        {
            arrivalInterval.x = Mathf.Max(0.5f, arrivalInterval.x);
            arrivalInterval.y = Mathf.Max(arrivalInterval.x, arrivalInterval.y);
            firstArrivalDelay.y = Mathf.Max(firstArrivalDelay.x, firstArrivalDelay.y);
        }
    }
}
