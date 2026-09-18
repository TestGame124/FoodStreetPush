using System;
using System.Collections.Generic;

namespace FoodStreet.Saving
{
    /// <summary>
    /// Tutorial position, player level, and which shop items have been unlocked.
    /// All of it lives inside the encrypted payload.
    /// </summary>
    [Serializable]
    public class ProgressSaveData
    {
        public TutorialStage tutorialStage = TutorialStage.FirstStage;
        public int tutorialEventRuns;

        public int level = 1;
        public int currentXp;
        public int xpToNextLevel = 3;

        /// <summary>
        /// Unlocks are normally implied by <see cref="level"/>, but recording
        /// them explicitly means an unlock can never be taken back - and leaves
        /// room for unlocks that are bought or granted rather than levelled into.
        /// </summary>
        public List<UnlockedItemSaveData> unlockedItems = new List<UnlockedItemSaveData>();

        public void EnsureComplete()
        {
            if (unlockedItems == null)
                unlockedItems = new List<UnlockedItemSaveData>();

            if (level < 1)
                level = 1;
            if (currentXp < 0)
                currentXp = 0;
            if (xpToNextLevel < 1)
                xpToNextLevel = 3;
        }

        public bool IsUnlocked(ObjectCategory category, ObjectType type)
        {
            for (int i = 0; i < unlockedItems.Count; i++)
            {
                UnlockedItemSaveData entry = unlockedItems[i];
                if (entry.category == category && entry.type == type)
                    return true;
            }
            return false;
        }

        /// <summary>Records an unlock. Returns true if this was new.</summary>
        public bool MarkUnlocked(ObjectCategory category, ObjectType type)
        {
            if (IsUnlocked(category, type))
                return false;

            unlockedItems.Add(new UnlockedItemSaveData
            {
                category = category,
                type = type
            });
            return true;
        }
    }

    [Serializable]
    public class UnlockedItemSaveData
    {
        public ObjectCategory category;
        public ObjectType type;
    }
}
