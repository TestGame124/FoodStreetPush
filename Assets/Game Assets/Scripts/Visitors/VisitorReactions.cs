using LastOrders.Jobs;

namespace FoodStreet.Visitors
{
    /// <summary>How a diner felt about their meal, judged by how long they waited for it.</summary>
    public enum MealVerdict
    {
        /// <summary>Served quickly: a chef's kiss, a thumbs up, a little round of applause.</summary>
        Delighted,

        /// <summary>A fair wait: a pat of the belly, a thumbs up, the mouth wiped.</summary>
        Happy,

        /// <summary>A long wait, but fed: the mouth wiped, a stretch, a shrug.</summary>
        Content,
    }

    /// <summary>
    /// What a diner does at the table once they have eaten, before they get up: the reaction is picked
    /// from a short list for how they felt about it, so a quick service reads at a glance from across
    /// the room. All in one table here, so the moods and their weights are tuned in one place.
    /// </summary>
    public static class VisitorReactions
    {
        /// <summary>Served within this many seconds of sitting down: delighted.</summary>
        public const float DelightedWithin = 20f;

        /// <summary>Within this many: happy. Longer: content.</summary>
        public const float HappyWithin = 50f;

        static readonly (Gesture gesture, float weight)[] Delighted =
            { (Gesture.ChefKiss, 3f), (Gesture.ThumbsUp, 3f), (Gesture.Clap, 2f), (Gesture.PatBelly, 1f) };

        static readonly (Gesture gesture, float weight)[] Happy =
            { (Gesture.PatBelly, 3f), (Gesture.ThumbsUp, 2f), (Gesture.WipeMouth, 2f), (Gesture.Stretch, 1f) };

        static readonly (Gesture gesture, float weight)[] Content =
            { (Gesture.WipeMouth, 3f), (Gesture.PatBelly, 2f), (Gesture.Stretch, 2f), (Gesture.Shrug, 1f) };

        public static MealVerdict Judge(float waitedSeconds) =>
            waitedSeconds <= DelightedWithin ? MealVerdict.Delighted :
            waitedSeconds <= HappyWithin ? MealVerdict.Happy : MealVerdict.Content;

        /// <summary>A reaction for <paramref name="verdict"/>; <paramref name="roll"/> is a 0-1 draw.</summary>
        public static Gesture Pick(MealVerdict verdict, float roll)
        {
            (Gesture gesture, float weight)[] table =
                verdict == MealVerdict.Delighted ? Delighted : verdict == MealVerdict.Happy ? Happy : Content;

            float total = 0f;
            foreach ((Gesture _, float weight) in table) total += weight;

            float pick = roll * total;
            foreach ((Gesture gesture, float weight) in table)
            {
                pick -= weight;
                if (pick < 0f) return gesture;
            }

            return table[table.Length - 1].gesture;
        }
    }
}
