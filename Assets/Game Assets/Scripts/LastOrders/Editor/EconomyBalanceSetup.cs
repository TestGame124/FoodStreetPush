using System;
using System.Collections.Generic;
using System.Linq;
using LastOrders.Data;
using LastOrders.Economy;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>Applies the production balance to existing content without rebuilding scenes or art.</summary>
    public static class EconomyBalanceSetup
    {
        const string Root = "Assets/Game Assets/Content/LastOrders";

        static IEnumerable<T> Assets<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/Game Assets", "Assets/GridBuildingSystem" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset != null);

        static void Save(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        [MenuItem("Last Orders/Economy/Apply Balanced Economy")]
        public static void Apply()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
            foreach (ItemDataSO item in Assets<ItemDataSO>())
            {
                int grow = EconomyRules.CropSeconds(item.Type);
                if (grow > 0) { item.totalTimeInSeconds = grow; item.harvestYield = 3; }
                if (EconomyRules.TryDish(item.Type, out var dish))
                { item.coinsReward = dish.Coins; item.starsReward = dish.Experience; item.totalTimeInSeconds = dish.Seconds; }
                Save(item);
            }
            foreach (CookingRecipe recipe in Assets<CookingRecipe>())
            {
                if (recipe.Results.item == null || !EconomyRules.TryDish(recipe.Results.item.type, out var dish)) continue;
                recipe.TotalTimeToCook = dish.Seconds;
                recipe.requiredLevel = dish.Level;
                recipe.startRewards = 2 + (dish.Level - 1) / 2;
                var output = recipe.Results;
                output.Amount = 2;
                output.item = new Item(output.item.itemDataSO, 1);
                recipe.Results = output;
                if (dish.Type == ItemType.CarrotSlaw)
                {
                    ItemDataSO cabbage = Assets<ItemDataSO>().First(item => item.Type == ItemType.Cabbage);
                    ItemDataSO carrot = Assets<ItemDataSO>().First(item => item.Type == ItemType.Carrot);
                    recipe.Ingrediants = new List<ItemAmount>
                    {
                        new ItemAmount { item = new Item(cabbage, 1), Amount = 1 },
                        new ItemAmount { item = new Item(carrot, 1), Amount = 1 },
                    };
                }
                Save(recipe);
            }
            foreach (DefenseDefinition defense in Assets<DefenseDefinition>())
            {
                var stats = DefenseStats(defense.id);
                defense.requiredLevel = stats.level;
                defense.buildPrice = new DefensePrice(defense.buildPrice.premium, new ResourceAmount(ResourceType.Coin, stats.coins));
                for (int i = 0; i < defense.LevelCount; i++)
                {
                    defense.levels[i].requiredLevel = i == 0 ? stats.level : stats.level + 2 * i;
                    if (i > 0) defense.levels[i].upgradePrice = new DefensePrice(defense.levels[i].upgradePrice.premium,
                        new ResourceAmount(ResourceType.Coin, Mathf.RoundToInt(stats.coins * (1.5f + 0.5f * (i - 1)))));
                }
                Save(defense);
            }
            foreach (PlacedObjectTypeSO shop in Assets<PlacedObjectTypeSO>())
            {
                if (ShopStats(shop.name, out int level, out int coins))
                {
                    shop.requiredLevel = level;
                    shop.isUnlocked = level == 1;
                    shop.PriceAmount = coins;
                    Save(shop);
                }
            }
            foreach (EnemyDefinition enemy in Assets<EnemyDefinition>())
            {
                switch (enemy.name)
                {
                    case "Enemy_Shambler": Enemy(enemy, 30, 4, 1); break;
                    case "Enemy_Runner": Enemy(enemy, 20, 3, 6); break;
                    case "Enemy_HotDog": Enemy(enemy, 45, 5, 9); break;
                    case "Enemy_Brute": Enemy(enemy, 120, 12, 12); break;
                    case "Enemy_Flanker": Enemy(enemy, 35, 5, 15); break;
                    case "Enemy_Burner": Enemy(enemy, 45, 6, 18); break;
                    case "Enemy_Glutton": Enemy(enemy, 80, 4, 18); break;
                    case "Enemy_Raider": Enemy(enemy, 60, 8, 24); break;
                    case "Enemy_Screecher": Enemy(enemy, 30, 0, 30); break;
                    default: continue;
                }
                Save(enemy);
            }
            foreach (ExpansionAreaDefinition area in Assets<ExpansionAreaDefinition>())
            {
                var cost = new List<ResourceAmount>(area.expandCost ?? Array.Empty<ResourceAmount>());
                cost.RemoveAll(entry => entry.type.Canonical() == ResourceType.Coin);
                int level = Mathf.Max(2, area.requiredLevel);
                cost.Insert(0, new ResourceAmount(ResourceType.Coin, 30 + 15 * level * level));
                area.expandCost = cost.ToArray();
                area.expandSeconds = 10 + 5 * level;
                area.claimReward.experience = 10 + 5 * level;
                Save(area);
            }
            GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root + "/GameBalance.asset");
            if (balance != null)
            {
                balance.startingResources = balance.startingResources.Select(entry => new ResourceAmount(entry.type.Canonical(),
                    entry.type.Canonical() == ResourceType.Coin ? 120 : entry.amount)).ToArray();
                balance.restaurant.firstExpansionCost = new Price(120, new ResourceAmount(ResourceType.Wood, 15), new ResourceAmount(ResourceType.Scrap, 10));
                balance.restaurant.costGrowth = 1.6f;
                balance.encounter = BalancedEncounters();
                Save(balance);
            }
            SetLivestock("ChickenCoop", 45f);
            SetLivestock("CowShelter", 60f);
            MigrateCurrencyAssets();
            UpdatePopupLabels();
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log("[Economy] Applied crop, recipe, shop, defense, expansion and enemy balance. Currency uses the HUD coin wallet.");
        }

        static void Enemy(EnemyDefinition enemy, float hp, float dps, int day)
        { enemy.maxHealth = hp; enemy.structureDps = dps; enemy.earliestDay = day; }

        public static EncounterTuning BalancedEncounters()
        {
            return new EncounterTuning
            {
                restaurantHealth = 450f, outdoorObjectHealth = 60f, maxEnemyRisePerEncounter = 2,
                maxRaidersPerObject = 2, repairKitCoinPrice = 30,
                tiers = new[]
                {
                    new ThreatTierTuning("Low", 0, 2, 4, 0.35f, 8, 1, 1, 1.5f, 50, 15, 1),
                    new ThreatTierTuning("Medium", 45, 2, 8, 0.5f, 14, 2, 1, 4f, 90, 25, 1),
                    new ThreatTierTuning("High", 75, 3, 12, 0.6f, 20, 3, 2, 6f, 140, 40, 2),
                }
            };
        }

        static (int level, int coins) DefenseStats(string id)
        {
            switch (id)
            {
                case "guard_tower": return (1, 80);
                case "corn_shooter": return (2, 90);
                case "tomato_shooter": return (3, 130);
                case "toastapult": return (4, 160);
                case "cabbage_shooter": return (5, 200);
                case "plate_shooter": return (6, 230);
                case "scrap_turret": return (7, 280);
                default: return (1, 80);
            }
        }

        static bool ShopStats(string name, out int level, out int coins)
        {
            level = 1; coins = 0;
            switch (name)
            {
                case "Bakery": coins = 100; break;
                case "Soup Kitchen": coins = 100; break;
                case "PlantPot": coins = 30; break;
                case "Table": coins = 40; break;
                case "Salad Station": level = 2; coins = 140; break;
                case "Chicken Coop": level = 3; coins = 160; break;
                case "Griddle Station": level = 3; coins = 200; break;
                case "Grill Station": level = 4; coins = 260; break;
                case "Cow Shelter": level = 5; coins = 280; break;
                case "Corn Shooter": level = 2; coins = 90; break;
                case "Tomato Shooter": level = 3; coins = 130; break;
                case "Cabbage Shooter": level = 5; coins = 200; break;
                case "Plate Shooter": level = 6; coins = 230; break;
                default: return false;
            }
            return true;
        }

        static void SetLivestock(string name, float seconds)
        {
            string path = $"{Root}/Prefabs/Livestock/Livestock_{name}.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pen = prefab.GetComponentInChildren<LastOrders.Livestock.LivestockPen>(true);
                if (pen == null) return;
                var serialized = new SerializedObject(pen);
                serialized.FindProperty("secondsPerUnit").floatValue = seconds;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        static void MigrateCurrencyAssets()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var serialized = new SerializedObject(asset);
                var field = serialized.GetIterator();
                bool changed = false;
                while (field.Next(true))
                    if (field.propertyType == SerializedPropertyType.Enum && field.type == "ResourceType" && field.intValue == 6)
                    { field.intValue = 0; changed = true; }
                if (!changed) continue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Save(asset);
            }
        }

        static void UpdatePopupLabels()
        {
            foreach (string path in new[] { Root + "/Prefabs/UI/UI_DefensePopup.prefab", Root + "/Prefabs/UI/Screens/Popups/UI_DefensePopup.prefab" })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (TMPro.TMP_Text label in prefab.GetComponentsInChildren<TMPro.TMP_Text>(true))
                        if (label.text.Contains("Trade Tokens")) label.text = label.text.Replace("Trade Tokens", "Coins");
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
        }
    }
}
