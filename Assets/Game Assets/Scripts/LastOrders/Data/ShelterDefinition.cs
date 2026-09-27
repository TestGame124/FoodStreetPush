using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// A price in the level's Coins, the stores, or both. Every part of it is paid, so
    /// "40 Coins + 10 Wood" costs forty coins and ten wood. A price with neither is free.
    /// </summary>
    [Serializable]
    public struct ShelterPrice
    {
        [Tooltip("The level's own Coins - what visitors pay and the HUD counts.")]
        [Min(0)] public int coins;

        [Tooltip("Taken from the stores, on top of the coins.")]
        public ResourceAmount[] resources;

        public ShelterPrice(int coins, params ResourceAmount[] resources)
        {
            this.coins = coins;
            this.resources = resources;
        }

        public bool HasResources
        {
            get
            {
                if (resources == null) return false;
                for (int i = 0; i < resources.Length; i++)
                    if (resources[i].amount > 0) return true;
                return false;
            }
        }

        public bool IsFree => coins <= 0 && !HasResources;

        /// <summary>"40 Coins + 10 Wood". Empty when it is free.</summary>
        public string Describe(string coinName = "Coins")
        {
            var parts = new List<string>();
            if (coins > 0) parts.Add($"{coins} {coinName}");

            if (resources != null)
            {
                for (int i = 0; i < resources.Length; i++)
                    if (resources[i].amount > 0) parts.Add($"{resources[i].amount} {resources[i].type.DisplayName()}");
            }

            return string.Join(" + ", parts);
        }
    }

    /// <summary>Someone the shelter can take in: what they do, and what taking them on costs.</summary>
    [Serializable]
    public sealed class ShelterHire
    {
        [Tooltip("The role they arrive with.")]
        public SurvivorRoleDefinition role;

        [Tooltip("One line on what they will do here, for the hire popup.")]
        public string pitch;

        public ShelterPrice price;
    }

    /// <summary>
    /// The little shelter: a hut with a bed for each survivor who lives in it.
    ///
    /// Beds are the hiring limit. Every survivor the player hires needs one, so the number of
    /// people who can be taken on is the number of beds in built shelters, and building another
    /// shelter is how the limit goes up. The crew the restaurant opens with sleep in the diner
    /// and do not take a bed.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Orders/Housing/Shelter", fileName = "Shelter_")]
    public sealed class ShelterDefinition : ScriptableObject
    {
        [Tooltip("Stable identity for this kind of shelter.")]
        public string id = "shelter";

        public string displayName = "Shelter";

        [TextArea(2, 4)] public string description;

        [Tooltip("Survivors one shelter houses. Each is one more person the player can hire.")]
        [Min(1)] public int beds = 2;

        [Tooltip("What an additional one costs. The one a scene starts with is not charged.")]
        public ShelterPrice buildPrice;

        [Tooltip("Who can be hired into a free bed, in the order the popup lists them.")]
        public ShelterHire[] hires;

        public int HireCount => hires != null ? hires.Length : 0;

        void OnValidate()
        {
            if (HireCount == 0)
            {
                Debug.LogWarning($"[Shelter] '{displayName}' lists nobody to hire, so its beds can never be filled.", this);
                return;
            }

            for (int i = 0; i < hires.Length; i++)
            {
                if (hires[i] == null || hires[i].role == null)
                    Debug.LogWarning($"[Shelter] '{displayName}' hire {i + 1} has no role, so it cannot be hired.", this);
            }
        }
    }
}
