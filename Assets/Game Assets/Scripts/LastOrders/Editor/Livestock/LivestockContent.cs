using System.Collections.Generic;
using System.Text;
using FoodStreet.Visitors;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// What the farmyard gives and what the kitchen makes of it: the Egg and Milk items, with icons
    /// modelled and baked like the crops'; two dishes that use them - a Custard Tart from the bakery
    /// (flour, two eggs, milk) and a Corn Chowder from the soup stove (corn and milk) - with their
    /// recipes added to those stoves beside what they already cook; and a place for each dish on a
    /// couple of the visitors' menus, so eggs and milk are worth collecting.
    ///
    /// Items and recipes are made if missing and kept pointing at their icons; prices, times and the
    /// visitors' menus are only filled in the first time, so tuning made in the assets is never reset.
    /// </summary>
    internal static class LivestockContent
    {
        internal const string ItemsFolder = "Assets/Game Assets/Scripts/Item/ItemsData";
        internal const string EggPath = ItemsFolder + "/Egg.asset";
        internal const string MilkPath = ItemsFolder + "/Milk.asset";

        /// <summary>What the hens eat: the corn crop.</summary>
        internal const string CornPath = ItemsFolder + "/Corn.asset";

        /// <summary>What the cow eats: the wheat crop, which the storehouse keeps as the Flour item.</summary>
        internal const string WheatPath = ItemsFolder + "/GrainFlour.asset";

        const string DishFolder = ItemsFolder + "/Recipe/Farmyard";
        const string RecipeFolder = "Assets/Game Assets/Scripts/Item/Cooking System/Recipies/Farmyard";
        const string IconFolder = "Assets/Game Assets/Content/LastOrders/Kitchen/UI";
        const string DatabasePath = ItemsFolder + "/ItemsDatabase.asset";
        const string VisitorFolder = "Assets/Game Assets/Content/Visitors/Visitors";
        const string BakeryPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/BreadStove/pfBreadStove.prefab";
        const string SoupPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/SoupStove/pfSoupStove.prefab";

        static readonly Quaternion IngredientView = Quaternion.Euler(26f, -22f, 0f);

        sealed class Dish
        {
            public CropModels.Dish Model;
            public ItemType Item;
            public string Name, File, Station;
            public int Price, Seconds, Stars, StartRewards;
            public float Pitch;
            public (ItemType item, int amount)[] Ingredients;
        }

        static readonly Dish[] Dishes =
        {
            new Dish
            {
                Model = CropModels.Dish.CustardTart, Item = ItemType.CustardTart, Name = "Custard Tart", File = "CustardTart", Station = BakeryPath,
                Price = 45, Seconds = 90, Stars = 2, StartRewards = 2, Pitch = 40f,
                Ingredients = new[] { (ItemType.Flour, 1), (ItemType.Egg, 2), (ItemType.Milk, 1) },
            },
            new Dish
            {
                Model = CropModels.Dish.CornChowder, Item = ItemType.CornChowder, Name = "Corn Chowder", File = "CornChowder", Station = SoupPath,
                Price = 30, Seconds = 75, Stars = 1, StartRewards = 2, Pitch = 38f,
                Ingredients = new[] { (ItemType.Corn, 1), (ItemType.Milk, 1) },
            },
        };

        /// <summary>Who orders the new dishes, how many and how often against what they already order.</summary>
        static readonly (string visitor, ItemType item, Vector2Int amount, float weight)[] Menus =
        {
            ("Visitor_Family", ItemType.CustardTart, new Vector2Int(1, 2), 1f),
            ("Visitor_Regular", ItemType.CustardTart, new Vector2Int(1, 1), 1f),
            ("Visitor_Farmhand", ItemType.CornChowder, new Vector2Int(1, 2), 1f),
            ("Visitor_Courier", ItemType.CornChowder, new Vector2Int(1, 1), 1f),
        };

        [MenuItem("Last Orders/Farming/Build Egg & Milk Items, Dishes + Icons", priority = 96)]
        static void Run() => Debug.Log(Apply());

        internal static string Apply()
        {
            var report = new StringBuilder("[Last Orders] Farmyard content:\n");
            ContentGenerator.EnsureFolder(IconFolder);
            ContentGenerator.EnsureFolder(DishFolder);
            ContentGenerator.EnsureFolder(RecipeFolder);

            var made = new List<ItemDataSO>
            {
                Produce(CropModels.Ingredient.Egg, ItemType.Egg, "Egg", EggPath),
                Produce(CropModels.Ingredient.Milk, ItemType.Milk, "Milk", MilkPath),
            };

            var recipes = new List<(CookingRecipe recipe, string station)>();
            foreach (Dish dish in Dishes)
            {
                Sprite icon = CropIcons.SaveIcon(IconFolder, $"Icon_{dish.File}", DishIcons.Render(dish.Model, dish.Pitch));
                ItemDataSO item = Item($"{DishFolder}/{dish.File}Item.asset", dish.Name, dish.Item, dish.Price, dish.Stars, dish.Seconds, icon);
                made.Add(item);
                recipes.Add((Recipe($"{RecipeFolder}/{dish.File}Recipe.asset", dish, item, icon, report), dish.Station));
            }

            report.Append("  ").Append(Register(made)).Append('\n');
            foreach (var (recipe, station) in recipes) report.Append("  ").Append(GiveToStation(recipe, station)).Append('\n');
            report.Append("  ").Append(AddToMenus());
            return report.ToString();
        }

        static ItemDataSO Produce(CropModels.Ingredient model, ItemType type, string name, string path)
        {
            Texture2D icon = CropIcons.Render(CropModels.Stocked(model), $"{model}_Stocked", IngredientView);
            Sprite sprite = CropIcons.SaveIcon(IconFolder, $"Icon_{name}", icon);
            return Item(path, name, type, 0, 0, 0, sprite);
        }

        /// <summary>The item at <paramref name="path"/>, made if missing; its numbers only set when it is new.</summary>
        static ItemDataSO Item(string path, string name, ItemType type, int coins, int stars, int seconds, Sprite icon)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
            bool created = item == null;
            if (created)
            {
                item = ScriptableObject.CreateInstance<ItemDataSO>();
                AssetDatabase.CreateAsset(item, path);
            }

            Undo.RecordObject(item, "Farmyard content");
            item.Name = name;
            item.Type = type;
            item.canBePlanted = false;
            item.itemImage = icon;
            if (created)
            {
                item.coinsReward = coins;
                item.starsReward = stars;
                item.totalTimeInSeconds = seconds;
            }

            LastOrders.Economy.EconomyRules.ApplyItem(item);
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(item);
            return item;
        }

        static CookingRecipe Recipe(string path, Dish dish, ItemDataSO result, Sprite icon, StringBuilder report)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<CookingRecipe>(path);
            bool created = recipe == null;
            if (created)
            {
                recipe = ScriptableObject.CreateInstance<CookingRecipe>();
                AssetDatabase.CreateAsset(recipe, path);
            }

            Undo.RecordObject(recipe, "Farmyard content");
            recipe.RecipeImage = icon;

            if (created)
            {
                var ingredients = new List<ItemAmount>();
                foreach (var (type, amount) in dish.Ingredients)
                {
                    ItemDataSO data = Find(type);
                    if (data == null)
                    {
                        report.Append($"  WARNING: {dish.Name} needs {type}, and there is no {type} item\n");
                        continue;
                    }

                    ingredients.Add(new ItemAmount { item = new Item(data, 1), Amount = amount });
                }

                recipe.Ingrediants = ingredients;
                recipe.TotalTimeToCook = dish.Seconds;
                recipe.startRewards = dish.StartRewards;
            }

            // Amount is the exact number of servings.
            recipe.Results = new ItemAmount { item = new Item(result, 1), Amount = 0 };
            LastOrders.Economy.EconomyRules.ApplyRecipe(recipe);
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssetIfDirty(recipe);
            return recipe;
        }

        static ItemDataSO Find(ItemType type)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDataSO"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.Type == type) return item;
            }

            return null;
        }

        static string Register(IEnumerable<ItemDataSO> items)
        {
            var database = AssetDatabase.LoadAssetAtPath<ItemsDatabase>(DatabasePath);
            if (database == null) return $"WARNING: no ItemsDatabase at {DatabasePath}; the items are not registered";

            var all = new List<ItemDataSO>(database.AllItems ?? new ItemDataSO[0]);
            int added = 0;
            foreach (ItemDataSO item in items)
            {
                if (all.Contains(item)) continue;
                all.Add(item);
                added++;
            }

            if (added == 0) return "ItemsDatabase already lists them all.";

            Undo.RecordObject(database, "Farmyard content");
            database.AllItems = all.ToArray();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);
            return $"{added} items added to the ItemsDatabase.";
        }

        /// <summary>Adds the recipe to the stove's list after whatever it already cooks, once.</summary>
        static string GiveToStation(CookingRecipe recipe, string stationPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(stationPath);
            try
            {
                var station = root.GetComponent<CookeryStation>();
                if (station == null) return $"WARNING: {stationPath} has no CookeryStation";

                var serialized = new SerializedObject(station);
                SerializedProperty list = serialized.FindProperty("recipies");
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == recipe)
                        return $"{root.name} already cooks {recipe.name}.";

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = recipe;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, stationPath);
                return $"{root.name} now cooks {recipe.name} ({list.arraySize} recipes).";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Puts the dishes on the visitors' menus - only where a visitor does not order them already.</summary>
        static string AddToMenus()
        {
            int added = 0;
            foreach (var (visitor, item, amount, weight) in Menus)
            {
                var definition = AssetDatabase.LoadAssetAtPath<VisitorDefinition>($"{VisitorFolder}/{visitor}.asset");
                if (definition == null) continue;

                var orders = new List<ItemRequest>(definition.orders ?? new ItemRequest[0]);
                if (orders.Exists(o => o.item == item)) continue;

                orders.Add(new ItemRequest { item = item, amountRange = amount, weight = weight });
                Undo.RecordObject(definition, "Farmyard content");
                definition.orders = orders.ToArray();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
                added++;
            }

            return added == 0 ? "Visitors' menus already have the farmyard dishes." : $"{added} visitors' menus now include a farmyard dish.";
        }
    }
}
