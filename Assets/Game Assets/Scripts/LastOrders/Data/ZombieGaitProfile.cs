using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// How a zombie with a model of its own walks and reacts: the stride, the waddle and the hop,
    /// arms flapping out in front, the jelly squash when it is hit and the wobble of whatever is
    /// loose on top, the swing of a blow on a wall, the jiggle of a googly eye.
    ///
    /// Read by <see cref="LastOrders.Night.MascotZombieBody"/>, which works out from how the
    /// agent moves where in its stride it is; nothing here changes how fast it goes or how hard
    /// it hits, which are the <see cref="EnemyDefinition"/>'s. The maths is here rather than in
    /// the body so it can be checked without a scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Enemy Gait", fileName = "Gait_")]
    public sealed class ZombieGaitProfile : ScriptableObject
    {
        [Header("Walking")]
        [Tooltip("Metres covered by one step. Shorter steps at the same speed make a busier waddle.")]
        [Min(0.05f)] public float stepMetres = 0.42f;

        [Tooltip("Degrees each leg swings forward and back.")]
        [Range(0f, 60f)] public float legSwingDegrees = 30f;

        [Tooltip("Degrees it rolls onto each foot in turn.")]
        [Range(0f, 30f)] public float waddleDegrees = 10f;

        [Tooltip("Metres it bobs up between steps.")]
        [Min(0f)] public float hopHeight = 0.07f;

        [Tooltip("Degrees the arms flap up and down, out of step with each other.")]
        [Range(0f, 45f)] public float armFlapDegrees = 16f;

        [Header("Standing")]
        [Tooltip("Degrees it sways side to side while it stands.")]
        [Range(0f, 20f)] public float idleSwayDegrees = 4f;

        [Tooltip("Seconds for one sway, side to side and back.")]
        [Min(0.2f)] public float idleSwaySeconds = 2.2f;

        [Header("On top")]
        [Tooltip("How much of the body's roll whatever is loose on top swings back the other way - " +
                 "a sausage in a bun, a hat.")]
        [Range(0f, 2f)] public float topWobble = 0.7f;

        [Header("Hit")]
        [Tooltip("How far it squashes when hit, as a fraction of its height, before it boings back.")]
        [Range(0f, 0.5f)] public float hurtSquash = 0.2f;

        [Tooltip("Seconds from a hit to settled again.")]
        [Min(0.05f)] public float hurtSeconds = 0.55f;

        [Tooltip("Degrees the top lurches over on a hit.")]
        [Range(0f, 45f)] public float hurtWobbleDegrees = 14f;

        [Header("Battering")]
        [Tooltip("Seconds one blow on a wall takes: arms up, then down on it.")]
        [Min(0.05f)] public float poundSeconds = 0.5f;

        [Tooltip("Degrees the arms swing up before they come down.")]
        [Range(0f, 120f)] public float poundLiftDegrees = 75f;

        [Header("Googly eyes")]
        [Tooltip("Metres a googly pupil can roll from the middle of its eye.")]
        [Min(0f)] public float googlyRange = 0.022f;

        [Tooltip("How hard a pupil is pulled back to where it hangs. Lower is floppier.")]
        [Min(1f)] public float googlyStiffness = 70f;

        [Tooltip("How fast its rattling dies away.")]
        [Min(0f)] public float googlyDamping = 5f;

        [Tooltip("How hard a hit or a footfall knocks it about.")]
        [Min(0f)] public float googlyKick = 0.5f;

        /// <summary>Degrees a leg is forward of straight down at <paramref name="phase"/> (radians; one step is half a turn). <paramref name="moving"/> is 0 standing to 1 walking.</summary>
        public float LegSwing(float phase, float moving) => Mathf.Sin(phase) * legSwingDegrees * moving;

        /// <summary>Degrees of roll: onto each foot as it lands while walking, an easy sway while it stands.</summary>
        public float Roll(float phase, float moving, float seconds) =>
            Mathf.Sin(phase) * waddleDegrees * moving +
            Mathf.Sin(seconds / idleSwaySeconds * Mathf.PI * 2f) * idleSwayDegrees * (1f - moving);

        /// <summary>Metres above the ground: highest as the legs pass each other, down on each footfall.</summary>
        public float Hop(float phase, float moving) => Mathf.Abs(Mathf.Cos(phase)) * hopHeight * moving;

        /// <summary>
        /// Taller than at rest by this fraction (negative is squashed), <paramref name="sinceHit"/>
        /// seconds after a hit: squashed at once, then boinging back up past its size and down again
        /// as the jelly settles. Zero once settled.
        /// </summary>
        public float HurtStretch(float sinceHit)
        {
            if (hurtSquash <= 0f || sinceHit < 0f || sinceHit >= hurtSeconds) return 0f;

            float u = sinceHit / hurtSeconds;
            return -hurtSquash * Mathf.Cos(u * Mathf.PI * 3f) * (1f - u) * (1f - u);
        }

        /// <summary>Degrees the top lurches, <paramref name="sinceHit"/> seconds after a hit: a wobble that dies away.</summary>
        public float HurtWobble(float sinceHit)
        {
            if (hurtWobbleDegrees <= 0f || sinceHit < 0f || sinceHit >= hurtSeconds) return 0f;

            float u = sinceHit / hurtSeconds;
            return hurtWobbleDegrees * Mathf.Sin(u * Mathf.PI * 4f) * (1f - u);
        }

        /// <summary>0 to 1 and back over a blow, <paramref name="sinceBlow"/> seconds in: a slow lift, a fast drop. Zero between blows.</summary>
        public float PoundLift(float sinceBlow)
        {
            if (sinceBlow < 0f || sinceBlow >= poundSeconds) return 0f;

            float t = sinceBlow / poundSeconds;
            return t < 0.66f ? Mathf.SmoothStep(0f, 1f, t / 0.66f) : 1f - (t - 0.66f) / 0.34f;
        }
    }
}
