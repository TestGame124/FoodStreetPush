using LastOrders.Data;
using LastOrders.Jobs;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The expansion loop's starting content: the kinds of debris and the first area.
    ///
    /// Unlike the GDD tables in <see cref="ContentGenerator"/>, these values are ones a
    /// designer is expected to tune by hand in the inspector, so an asset is filled in only on
    /// the run that creates it. Re-running never resets a tuned work time or reward.
    ///
    /// Experience is kept small on purpose. The level curve asks 3, 5, 7... XP per level and
    /// every level-up shows a popup and pays coin; a yard worth a level on its own would bury
    /// the player in popups before the first customer sits down.
    /// </summary>
    public static class ExpansionContentGenerator
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Expansion";

        public const string RubblePath = Folder + "/Debris_RubblePile.asset";
        public const string JunkPath = Folder + "/Debris_Junk.asset";
        public const string BarricadePath = Folder + "/Debris_Barricade.asset";
        public const string CourtyardPath = Folder + "/ExpansionArea_Courtyard.asset";

        public static void Generate()
        {
            ContentGenerator.EnsureFolder(Folder);

            Debris(RubblePath, "rubble pile", 6f,
                   new RewardBundle(2, new ResourceAmount(ResourceType.Wood, 3)));

            Debris(JunkPath, "junk heap", 4f,
                   new RewardBundle(1, new ResourceAmount(ResourceType.Scrap, 2)));

            Debris(BarricadePath, "sandbag barricade", 8f,
                   new RewardBundle(3, new ResourceAmount(ResourceType.Wood, 4)));

            Area(CourtyardPath, "courtyard", "Overgrown courtyard",
                 "Kitchen garden and optional seating",
                 new RewardBundle(5, new ResourceAmount(ResourceType.Wood, 10)));

            // Wood and scrap rather than coin: coin in the ledger does not reach the wallet the
            // HUD shows. The price is within reach of the starting kit plus what the yard's own
            // debris pays, so the first expansion teaches the loop rather than gating it.
            Expansion(CourtyardPath, level: 2, seconds: 12f,
                      new ResourceAmount(ResourceType.Wood, 15),
                      new ResourceAmount(ResourceType.Scrap, 10));

            AssetDatabase.SaveAssets();
        }

        public static DebrisDefinition LoadRubble() => AssetDatabase.LoadAssetAtPath<DebrisDefinition>(RubblePath);
        public static DebrisDefinition LoadJunk() => AssetDatabase.LoadAssetAtPath<DebrisDefinition>(JunkPath);
        public static DebrisDefinition LoadBarricade() => AssetDatabase.LoadAssetAtPath<DebrisDefinition>(BarricadePath);
        public static ExpansionAreaDefinition LoadCourtyard() => AssetDatabase.LoadAssetAtPath<ExpansionAreaDefinition>(CourtyardPath);

        static void Debris(string path, string name, float seconds, RewardBundle reward)
        {
            if (!CreateIfMissing(path, out DebrisDefinition asset)) return;

            asset.displayName = name;
            asset.workSeconds = seconds;
            asset.reward = reward;
            asset.role = WorkerRole.MainCharacter;

            EditorUtility.SetDirty(asset);
        }

        static void Area(string path, string id, string name, string benefit, RewardBundle reward)
        {
            if (!CreateIfMissing(path, out ExpansionAreaDefinition asset)) return;

            asset.id = id;
            asset.displayName = name;
            asset.benefit = benefit;
            asset.unlockedBy = new ExpansionAreaDefinition[0];
            asset.claimReward = reward;

            EditorUtility.SetDirty(asset);
        }

        /// <summary>
        /// The price and build time of expanding into an area. Filled while the price is still
        /// empty - which covers areas created before expanding existed - and left alone after.
        /// </summary>
        static void Expansion(string path, int level, float seconds, params ResourceAmount[] cost)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ExpansionAreaDefinition>(path);
            if (asset == null || (asset.expandCost != null && asset.expandCost.Length > 0)) return;

            asset.expandCost = cost;
            asset.requiredLevel = level;
            asset.expandSeconds = seconds;

            EditorUtility.SetDirty(asset);
        }

        /// <summary>True only when the asset was created on this call.</summary>
        static bool CreateIfMissing<T>(string path, out T asset) where T : ScriptableObject
        {
            asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return false;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return true;
        }
    }
}
