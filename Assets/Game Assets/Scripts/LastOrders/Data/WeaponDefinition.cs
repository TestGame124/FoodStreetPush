using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// What a defense fires, as numbers. The guard's pistol, the rifle a tower upgrade hands
    /// them, and the machine gun an automated turret will carry are all one of these.
    ///
    /// Kept apart from the defense that carries it, so an upgrade is "swap the weapon" rather
    /// than a second copy of every stat, and one rifle tuned once is the rifle everywhere.
    ///
    /// A weapon is hitscan unless it says otherwise: the shot lands the moment it is fired, which
    /// covers a pistol, a rifle and a machine gun. One with a <see cref="projectileSpeed"/> throws
    /// something that has to get there - a slice of toast lobbed by a living toaster - and its
    /// damage lands when that does, or is wasted if the target has died in the meantime.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Defense/Weapon", fileName = "Weapon_")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string displayName = "Pistol";

        [Tooltip("Damage dealt to an enemy by one shot.")]
        [Min(0f)] public float damagePerShot = 16f;

        [Tooltip("Seconds between shots. The first shot at a target walking into range is " +
                 "immediate - whoever is up there has been watching it come.")]
        [Min(0.05f)] public float secondsBetweenShots = 1.5f;

        [Tooltip("Metres, measured flat along the ground from the foot of the defense.")]
        [Min(0.1f)] public float rangeMetres = 6f;

        [Tooltip("Ammo taken from the stores per shot. Zero for a guard's sidearm; an automated " +
                 "turret spends it, and falls silent when there is none.")]
        [Min(0)] public int ammoPerShot;

        [Tooltip("Metres per second the shot travels, measured flat. Zero: hitscan, it lands as it " +
                 "is fired. Anything else is thrown or lobbed, and lands distance / speed later.")]
        [Min(0f)] public float projectileSpeed;

        [Header("Where it lands")]
        [Tooltip("Metres round what a shot hits within which everything else is hit too - a cabbage " +
                 "bursting, a plate shattering. Zero: only what it was aimed at.")]
        [Min(0f)] public float splashRadius;

        [Tooltip("The share of the damage everything else in the splash takes.")]
        [Range(0f, 1f)] public float splashFraction = 0.5f;

        [Tooltip("What a hit leaves the target moving at, as a share of its own speed - tomato pulp " +
                 "it has to wade through. 1: it is not slowed.")]
        [Range(0.05f, 1f)] public float slowMultiplier = 1f;

        [Tooltip("Seconds a hit holds it back for.")]
        [Min(0f)] public float slowSeconds;

        public float DamagePerSecond => damagePerShot / secondsBetweenShots;

        public bool IsProjectile => projectileSpeed > 0f;

        public bool Splashes => splashRadius > 0f && splashFraction > 0f;

        public bool Slows => slowMultiplier < 1f && slowSeconds > 0f;

        /// <summary>Seconds a shot takes to cover <paramref name="flatDistance"/>. Zero for hitscan.</summary>
        public float FlightSeconds(float flatDistance) => IsProjectile ? Mathf.Max(0f, flatDistance) / projectileSpeed : 0f;

        /// <summary>"16 damage every 1.5s, 6 m" - for the tower popup and the tap reply.</summary>
        public string Describe() =>
            $"{damagePerShot:0.#} damage every {secondsBetweenShots:0.##}s, {rangeMetres:0.#} m{(IsProjectile ? ", lobbed" : "")}" +
            $"{(Splashes ? $", splashes {splashRadius:0.#} m" : "")}{(Slows ? ", slows" : "")}";
    }
}
