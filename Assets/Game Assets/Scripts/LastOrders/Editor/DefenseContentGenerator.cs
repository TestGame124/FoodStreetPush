using LastOrders.Data;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The defenses' starting content: the weapons, and the guard tower with its levels.
    ///
    /// Like the expansion content, these are values a designer is expected to tune by hand, so
    /// an asset is filled in only on the run that creates it. Re-running never resets a tuned
    /// damage figure or price.
    ///
    /// Level 1 is the design's own numbers: a pistol, 16 damage every 1.5 seconds within
    /// 6 m, and 60 Trade Tokens for each tower after the first. The spot range, the premium
    /// price and all of Level 2 - the rifle - are placeholders the design has not given
    /// numbers for yet, marked as such below.
    /// </summary>
    public static class DefenseContentGenerator
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Defense";

        public const string PistolPath = Folder + "/Weapon_Pistol.asset";
        public const string RiflePath = Folder + "/Weapon_Rifle.asset";
        public const string GuardTowerPath = Folder + "/Defense_GuardTower.asset";

        public static DefenseDefinition Generate()
        {
            ContentGenerator.EnsureFolder(Folder);

            WeaponDefinition pistol = Weapon(PistolPath, "Pistol", damage: 16f, interval: 1.5f, range: 6f);

            // Placeholder: the design says only that the rifle improves damage and range.
            WeaponDefinition rifle = Weapon(RiflePath, "Rifle", damage: 24f, interval: 1.5f, range: 9f);

            DefenseDefinition tower = GuardTower(pistol, rifle);

            AssetDatabase.SaveAssets();
            return tower;
        }

        public static DefenseDefinition LoadGuardTower() => AssetDatabase.LoadAssetAtPath<DefenseDefinition>(GuardTowerPath);

        static WeaponDefinition Weapon(string path, string name, float damage, float interval, float range)
        {
            if (!CreateIfMissing(path, out WeaponDefinition asset)) return asset;

            asset.displayName = name;
            asset.damagePerShot = damage;
            asset.secondsBetweenShots = interval;
            asset.rangeMetres = range;
            asset.ammoPerShot = 0;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static DefenseDefinition GuardTower(WeaponDefinition pistol, WeaponDefinition rifle)
        {
            if (!CreateIfMissing(GuardTowerPath, out DefenseDefinition asset)) return asset;

            asset.id = "guard_tower";
            asset.displayName = "Guard Tower";
            asset.description = "A raised post inside the perimeter wall where one survivor watches " +
                                "the road and protects the restaurant at night.";

            asset.requiresCrew = true;
            asset.crewRole = SurvivorRole.Guard;

            // Placeholder premium price: the design says "or optional premium currency" and names no figure.
            asset.buildPrice = new DefensePrice(20, new ResourceAmount(ResourceType.TradeToken, 60));

            asset.levels = new[]
            {
                // Placeholder spot range. Far enough to see the road beyond the gate - the
                // early warning - and about twice what the pistol reaches.
                new DefenseLevel { weapon = pistol, spotRangeMetres = 14f },

                // Placeholder throughout: the rifle, how far it sees, and what the upgrade costs.
                new DefenseLevel
                {
                    weapon = rifle,
                    spotRangeMetres = 18f,
                    upgradePrice = new DefensePrice(30, new ResourceAmount(ResourceType.TradeToken, 90)),
                },
            };

            EditorUtility.SetDirty(asset);
            return asset;
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
