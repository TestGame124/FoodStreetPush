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
    /// Every weapon here is hitscan: the shot lands the moment it is fired. That covers a
    /// pistol, a rifle and a machine gun. A slow projectile - a mortar, a rocket - would be a
    /// new way of delivering the damage, not new numbers on this asset.
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

        public float DamagePerSecond => damagePerShot / secondsBetweenShots;

        /// <summary>"16 damage every 1.5s, 6 m" - for the tower popup and the tap reply.</summary>
        public string Describe() =>
            $"{damagePerShot:0.#} damage every {secondsBetweenShots:0.##}s, {rangeMetres:0.#} m";
    }
}
