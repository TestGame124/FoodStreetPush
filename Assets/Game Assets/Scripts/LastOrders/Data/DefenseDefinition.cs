using System;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// A price with two ways to pay it: materials from the stores, or premium currency.
    ///
    /// Either can be left out. A price with neither is free - which is what the tower the
    /// restaurant starts with costs.
    /// </summary>
    [Serializable]
    public struct DefensePrice
    {
        [Tooltip("Taken from the stores. Empty: there is no materials option.")]
        public ResourceAmount[] resources;

        [Tooltip("Premium currency, as the alternative to the resources. Zero: not offered.")]
        [Min(0)] public int premium;

        public DefensePrice(int premium, params ResourceAmount[] resources)
        {
            this.premium = premium;
            this.resources = resources;
        }

        public bool HasResourceOption
        {
            get
            {
                if (resources == null) return false;
                for (int i = 0; i < resources.Length; i++)
                    if (resources[i].amount > 0) return true;
                return false;
            }
        }

        public bool HasPremiumOption => premium > 0;

        public bool IsFree => !HasResourceOption && !HasPremiumOption;
    }

    /// <summary>One level of a defense: what it fires, how far out it sees, and what reaching it costs.</summary>
    [Serializable]
    public sealed class DefenseLevel
    {
        [Tooltip("What this level fires. An upgrade that hands the guard a rifle is this field.")]
        public WeaponDefinition weapon;

        [Tooltip("Metres. How far out an approaching enemy is noticed and tracked - further than " +
                 "the weapon reaches, which is what a raised post is for. Never less than the " +
                 "weapon's range.")]
        [Min(0f)] public float spotRangeMetres = 12f;

        [Tooltip("Paid to reach this level from the one before. Ignored on the first level, " +
                 "which comes with building the defense.")]
        public DefensePrice upgradePrice;

        /// <summary>The spot range, never shorter than what the weapon can hit: a guard does
        /// not fire at what they have not seen.</summary>
        public float SpotRange => weapon != null ? Mathf.Max(spotRangeMetres, weapon.rangeMetres) : spotRangeMetres;
    }

    /// <summary>
    /// One kind of defense - the guard tower, and later the automated turret - with every
    /// level it can be upgraded through.
    ///
    /// A defense is its levels plus two rules: whether someone has to be up there for it to
    /// fire, and what building one costs. A guard tower needs a Guard; a machine-gun turret
    /// will not, and will spend Ammo instead, which is a field on its weapon. Neither needs a
    /// worker to build - paying for one is building it.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Defense/Defense", fileName = "Defense_")]
    public sealed class DefenseDefinition : ScriptableObject
    {
        [Tooltip("Stable identity for this kind of defense.")]
        public string id = "guard_tower";

        public string displayName = "Guard Tower";

        [TextArea(2, 4)] public string description;

        [Header("Crew")]
        [Tooltip("Fires only while a Survivor is posted to it. A guard tower with nobody in it " +
                 "does nothing at all. Off for anything automated.")]
        public bool requiresCrew = true;

        [Tooltip("The role a Survivor needs to be posted here.")]
        public SurvivorRole crewRole = SurvivorRole.Guard;

        [Header("Building")]
        [Tooltip("What an additional one costs. The one a scene starts with is not charged.")]
        public DefensePrice buildPrice;

        [Tooltip("The post placed for it on a site that takes a choice of defenses - a wall site. " +
                 "Empty: it cannot be chosen there.")]
        public GameObject prefab;

        [Header("Levels")]
        [Tooltip("Level 1 first. Each upgrade is the next entry.")]
        public DefenseLevel[] levels;

        public int LevelCount => levels != null ? levels.Length : 0;

        public bool HasLevel(int index) => index >= 0 && index < LevelCount && levels[index] != null;

        /// <summary>The level at <paramref name="index"/>, clamped to the ones that exist. Null
        /// only when there are none.</summary>
        public DefenseLevel Level(int index) => LevelCount == 0 ? null : levels[Mathf.Clamp(index, 0, LevelCount - 1)];

        void OnValidate()
        {
            if (LevelCount == 0)
            {
                Debug.LogWarning($"[Defense] '{displayName}' has no levels, so it has no weapon and can never fire.", this);
                return;
            }

            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] != null && levels[i].weapon == null)
                    Debug.LogWarning($"[Defense] '{displayName}' level {i + 1} has no weapon, so it cannot fire.", this);
            }
        }
    }
}
