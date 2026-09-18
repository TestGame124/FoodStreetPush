using System.Collections.Generic;
using FoodStreet.Saving;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Survivors;
using UnityEngine;

namespace LastOrders.Save
{
    /// <summary>
    /// Captures and restores the Last Orders state through the existing save system.
    ///
    /// Content assets are stored by name rather than by reference. A direct ScriptableObject
    /// reference cannot be serialised into a save file, and storing an asset path would
    /// break the moment anything is renamed or moved - which, for a game whose entire
    /// content pipeline is 250 data rows being edited continuously, would happen weekly.
    /// </summary>
    public sealed class LastOrdersSaveParticipant : ISaveParticipant
    {
        readonly GameComposition composition;

        public LastOrdersSaveParticipant(GameComposition composition)
        {
            this.composition = composition;
        }

        /// <summary>Runs after the build grid, which owns the placed objects the ghost
        /// records refer to.</summary>
        public int SaveOrder => 100;

        public void CaptureState(GameSaveData data)
        {
            LastOrdersSaveData save = data.lastOrders;
            save.EnsureComplete();

            save.day = composition.Time.CurrentDay;
            save.phase = composition.Machine.CurrentPhase;
            save.clockMinutes = composition.Time.ClockMinutes;
            save.partiesReleased = composition.Customers.Consumed;

            save.heatCurrent = composition.Heat.CurrentHeat;
            save.heatLocked = composition.Heat.LockedHeat;
            save.heatIsLocked = composition.Heat.IsLocked;

            save.reputation = composition.Reputation.Current;
            save.highestReputationTier = composition.Reputation.HighestTierReached;

            save.resources.Clear();
            foreach (KeyValuePair<ResourceType, int> entry in composition.Ledger.All)
            {
                save.resources.Add(new LastOrdersSaveData.ResourceRecord
                {
                    type = entry.Key,
                    amount = entry.Value,
                });
            }

            save.survivors.Clear();
            IReadOnlyList<Survivor> roster = composition.Survivors.Roster;
            for (int i = 0; i < roster.Count; i++)
            {
                Survivor survivor = roster[i];
                save.survivors.Add(new LastOrdersSaveData.SurvivorRecord
                {
                    id = survivor.Id,
                    name = survivor.Name,
                    roleAssetName = survivor.Role != null ? survivor.Role.name : null,
                    traitAssetName = survivor.Trait != null ? survivor.Trait.name : null,
                    injuryDaysRemaining = survivor.InjuryDaysRemaining,
                    awayDaysRemaining = survivor.AwayDaysRemaining,
                });
            }

            save.ghosts.Clear();
            IReadOnlyList<Consequence.GhostRegistry.Ghost> ghosts = composition.Ghosts.Ghosts;
            for (int i = 0; i < ghosts.Count; i++)
            {
                save.ghosts.Add(new LastOrdersSaveData.GhostRecord
                {
                    typeAssetName = ghosts[i].Type != null ? ghosts[i].Type.name : null,
                    originX = ghosts[i].Origin.x,
                    originZ = ghosts[i].Origin.y,
                    dir = (int)ghosts[i].Dir,
                    dayLost = ghosts[i].DayLost,
                });
            }
        }

        public void RestoreState(GameSaveData data)
        {
            LastOrdersSaveData save = data.lastOrders;
            if (save == null) return;

            save.EnsureComplete();

            composition.Time.RestoreFromSave(save.day, save.clockMinutes);
            composition.Heat.RestoreFromSave(save.heatCurrent, save.heatLocked, save.heatIsLocked);
            composition.Reputation.RestoreFromSave(save.reputation, save.highestReputationTier);

            var resources = new List<ResourceAmount>(save.resources.Count);
            for (int i = 0; i < save.resources.Count; i++)
                resources.Add(new ResourceAmount(save.resources[i].type, save.resources[i].amount));

            composition.Ledger.RestoreFromSave(resources);

            RestoreSurvivors(save);
            RestoreGhosts(save);

            // Dropped straight into the saved phase rather than walked forward through the
            // cycle to reach it. A file written mid-night has to come back up in Night.
            composition.Machine.ForceTo(save.phase);
            composition.Customers.RestoreFromSave(save.partiesReleased);
        }

        void RestoreSurvivors(LastOrdersSaveData save)
        {
            var roster = new List<Survivor>(save.survivors.Count);

            for (int i = 0; i < save.survivors.Count; i++)
            {
                LastOrdersSaveData.SurvivorRecord record = save.survivors[i];

                roster.Add(new Survivor
                {
                    Id = record.id,
                    Name = record.name,
                    Role = FindByName(composition.Balance.survivorRoles, record.roleAssetName),
                    Trait = FindByName(composition.Balance.survivorTraits, record.traitAssetName),
                    InjuryDaysRemaining = record.injuryDaysRemaining,
                    AwayDaysRemaining = record.awayDaysRemaining,
                });
            }

            composition.Survivors.RestoreFromSave(roster);
        }

        void RestoreGhosts(LastOrdersSaveData save)
        {
            composition.Ghosts.Clear();

            for (int i = 0; i < save.ghosts.Count; i++)
            {
                LastOrdersSaveData.GhostRecord record = save.ghosts[i];

                PlacedObjectTypeSO type = FindBuildable(record.typeAssetName);
                if (type == null)
                {
                    // The asset was renamed or removed between builds. Dropping the ghost is
                    // the right failure: the player loses an outline, not a structure, and
                    // nothing in the save can dangle.
                    Debug.LogWarning($"[LastOrdersSave] Ghost references missing buildable " +
                                     $"'{record.typeAssetName}'. Skipping it.");
                    continue;
                }

                composition.Ghosts.Add(type, new Vector2Int(record.originX, record.originZ),
                                       (PlacedObjectTypeSO.Dir)record.dir, record.dayLost);
            }
        }

        static T FindByName<T>(T[] pool, string assetName) where T : Object
        {
            if (pool == null || string.IsNullOrEmpty(assetName)) return null;

            for (int i = 0; i < pool.Length; i++)
                if (pool[i] != null && pool[i].name == assetName) return pool[i];

            return null;
        }

        static PlacedObjectTypeSO FindBuildable(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            // Resources is the lookup of record until the Addressables migration lands; the
            // catalogue is small and this runs once, on load.
            PlacedObjectTypeSO[] all = Resources.LoadAll<PlacedObjectTypeSO>(string.Empty);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == assetName) return all[i];

            return null;
        }
    }
}
