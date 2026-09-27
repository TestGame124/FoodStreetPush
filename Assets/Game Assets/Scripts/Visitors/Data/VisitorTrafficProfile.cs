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

        [Tooltip("Hard ceiling on visitors on the map at once, whatever the chairs and bays " +
                 "say. A safety net, not the main gate.")]
        [Min(1)] public int maxConcurrent = 8;

        [Tooltip("Free-chair ratio (0 = full, 1 = empty) to a multiplier on the arrival " +
                 "interval. Left flat at 1 it has no effect.")]
        public AnimationCurve busySlowdown = new AnimationCurve(
            new Keyframe(0f, 2.5f), new Keyframe(0.5f, 1.4f), new Keyframe(1f, 1f));

        public VisitorDefinition PickVisitor()
        {
            if (visitors == null || visitors.Length == 0) return null;

            float total = 0f;
            for (int i = 0; i < visitors.Length; i++)
            {
                if (visitors[i] != null) total += Mathf.Max(0f, visitors[i].spawnWeight);
            }

            if (total <= 0f) return null;

            float pick = Random.value * total;

            for (int i = 0; i < visitors.Length; i++)
            {
                if (visitors[i] == null) continue;

                pick -= Mathf.Max(0f, visitors[i].spawnWeight);
                if (pick <= 0f) return visitors[i];
            }

            return null;
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
