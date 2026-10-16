using System.Collections.Generic;
using System.Text;
using FoodStreet.Visitors;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// What the griddle and the grill make, from the design sheet: three dishes each, with what goes
    /// into them, as recipes on pfGriddleStation and pfGrillStation and dish items the inventory, the
    /// orders and the pickups know.
    ///
    ///   Griddle   Fried Eggs        egg x2
    ///   Griddle   Hash Browns       potato x2
    ///   Griddle   French Toast      wheat, egg, milk
    ///   Grill     Grilled Corn      corn x2
    ///   Grill     Chicken Skewers   chicken x2, onion
    ///   Grill     Grilled Fish      fish x2, tomato
    ///
    /// "Wheat" is the Flour item, which is what the wheat crop is harvested into. The sheet gives no
    /// prices or times; the ones here sit with the bakery's and the farmyard's dishes. Potatoes,
    /// onions and fish are made as items too, so the recipes can ask for them - nothing grows or
    /// sells them yet. Every dish and new ingredient gets an icon rendered from its model
    /// (<see cref="CropModels.Plated"/>, <see cref="CropModels.Stocked"/>).
    ///
    /// The three dishes the farm can already supply go onto a few visitors' menus, so the stations
    /// get used. Items and recipes are made if missing and kept pointing at their icons; prices,
    /// times, ingredients and the menus are only filled in the first time, so tuning made in the
    /// assets is never reset.
    /// </summary>
    public static class GriddleGrillContent
    {
        const string ItemsFolder = "Assets/Game Assets/Scripts/Item/ItemsData";
        const string RecipeRoot = "Assets/Game Assets/Scripts/Item/Cooking System/Recipies";
        const string IconFolder = "Assets/Game Assets/Content/LastOrders/Kitchen/UI";
        const string DatabasePath = ItemsFolder + "/ItemsDatabase.asset";
        const string VisitorFolder = "Assets/Game Assets/Content/Visitors/Visitors";

        sealed class Dish
        {
            public CropModels.Dish Model;
            public ItemType Item;
            public string Name, File;
            public bool Griddle;
            public int Price, Seconds, Stars, StartRewards;
            public float Pitch;
            public (ItemType item, int amount)[] Ingredients;
        }

        static readonly Dish[] Dishes =
        {
            new Dish
            {
                Model = CropModels.Dish.FriedEggs, Item = ItemType.FriedEggs, Name = "Fried Eggs", File = "FriedEggs", Griddle = true,
                Price = 25, Seconds = 45, Stars = 1, StartRewards = 1, Pitch = 46f,
                Ingredients = new[] { (ItemType.Egg, 2) },
            },
            new Dish
            {
                Model = CropModels.Dish.HashBrowns, Item = ItemType.HashBrowns, Name = "Hash Browns", File = "HashBrowns", Griddle = true,
                Price = 25, Seconds = 60, Stars = 1, StartRewards = 1, Pitch = 40f,
                Ingredients = new[] { (ItemType.Potato, 2) },
            },
            new Dish
            {
                Model = CropModels.Dish.FrenchToast, Item = ItemType.FrenchToast, Name = "French Toast", File = "FrenchToast", Griddle = true,
                Price = 40, Seconds = 90, Stars = 2, StartRewards = 2, Pitch = 40f,
                Ingredients = new[] { (ItemType.Flour, 1), (ItemType.Egg, 1), (ItemType.Milk, 1) },
            },
            new Dish
            {
                Model = CropModels.Dish.GrilledCorn, Item = ItemType.GrilledCorn, Name = "Grilled Corn", File = "GrilledCorn", Griddle = false,
                Price = 30, Seconds = 60, Stars = 1, StartRewards = 1, Pitch = 38f,
                Ingredients = new[] { (ItemType.Corn, 2) },
            },
            new Dish
            {
                Model = CropModels.Dish.ChickenSkewers, Item = ItemType.ChickenSkewers, Name = "Chicken Skewers", File = "ChickenSkewers", Griddle = false,
                Price = 45, Seconds = 120, Stars = 2, StartRewards = 2, Pitch = 42f,
                Ingredients = new[] { (ItemType.Chicken, 2), (ItemType.Onion, 1) },
            },
            new Dish
            {
                Model = CropModels.Dish.GrilledFish, Item = ItemType.GrilledFish, Name = "Grilled Fish", File = "GrilledFish", Griddle = false,
                Price = 55, Seconds = 150, Stars = 2, StartRewards = 3, Pitch = 44f,
                Ingredients = new[] { (ItemType.Fish, 2), (ItemType.Tomato, 1) },
            },
        };

        static readonly (CropModels.Ingredient model, ItemType item, string name, string file)[] Ingredients =
        {
            (CropModels.Ingredient.Potato, ItemType.Potato, "Potato", "Potato"),
            (CropModels.Ingredient.Onion, ItemType.Onion, "Onion", "Onion"),
            (CropModels.Ingredient.Fish, ItemType.Fish, "Fish", "Fish"),
        };

        /// <summary>Who orders the dishes the farm can already supply, how many and how often against what they already order.</summary>
        static readonly (string visitor, ItemType item, Vector2Int amount, float weight)[] Menus =
        {
            ("Visitor_Trucker", ItemType.FriedEggs, new Vector2Int(1, 2), 1f),
            ("Visitor_Courier", ItemType.FriedEggs, new Vector2Int(1, 1), 1f),
            ("Visitor_Family", ItemType.FrenchToast, new Vector2Int(1, 2), 1f),
            ("Visitor_Regular", ItemType.FrenchToast, new Vector2Int(1, 1), 1f),
            ("Visitor_Farmhand", ItemType.GrilledCorn, new Vector2Int(1, 2), 1f),
        };

        static readonly Quaternion IngredientView = Quaternion.Euler(26f, -22f, 0f);

        [MenuItem("Last Orders/Kitchen/Build Griddle & Grill Recipes + Icons", priority = 104)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This makes the griddle's dishes (Fried Eggs, Hash Browns, French Toast) and the grill's (Grilled Corn, Chicken " +
                "Skewers, Grilled Fish) as recipes and dish items, and Potato, Onion and Fish as ingredient items.\n\n" +
                $"Every one gets an icon rendered into {IconFolder}. The items go into the ItemsDatabase, the recipes onto " +
                "pfGriddleStation and pfGrillStation (build those first), and Fried Eggs, French Toast and Grilled Corn onto a few " +
                "visitors' menus. Running it again updates icons in place and leaves tuned numbers alone.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Makes or updates every item, recipe and icon, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(IconFolder);

            var report = new StringBuilder("[Last Orders] Griddle & grill recipes built.\n");
            var made = new List<ItemDataSO>();

            foreach (var (model, type, name, file) in Ingredients)
            {
                Texture2D icon = CropIcons.Render(CropModels.Stocked(model), $"{model}_Stocked", IngredientView);
                Sprite sprite = CropIcons.SaveIcon(IconFolder, $"Icon_{file}", icon);
                made.Add(Item($"{ItemsFolder}/{file}.asset", name, type, 0, 0, 0, sprite));
                report.Append($"  {name}: item, Icon_{file}\n");
            }

            var griddle = new List<CookingRecipe>();
            var grill = new List<CookingRecipe>();
            foreach (Dish dish in Dishes)
            {
                string station = dish.Griddle ? "GriddleStation" : "GrillStation";
                string dishFolder = $"{ItemsFolder}/Recipe/{station}";
                string recipeFolder = $"{RecipeRoot}/{(dish.Griddle ? "Griddle" : "Grill")}";
                ContentGenerator.EnsureFolder(dishFolder);
                ContentGenerator.EnsureFolder(recipeFolder);

                Texture2D icon = DishIcons.Render(dish.Model, dish.Pitch);
                Sprite sprite = CropIcons.SaveIcon(IconFolder, $"Icon_{dish.File}", icon);
                ItemDataSO item = Item($"{dishFolder}/{dish.File}Item.asset", dish.Name, dish.Item, dish.Price, dish.Stars, dish.Seconds, sprite);
                made.Add(item);

                CookingRecipe recipe = Recipe($"{recipeFolder}/{dish.File}Recipe.asset", dish, item, sprite, report);
                (dish.Griddle ? griddle : grill).Add(recipe);
                report.Append($"  {dish.Name}: {item.coinsReward} coins, {recipe.TotalTimeToCook} s, {AssetDatabase.GetAssetPath(recipe)}\n");
            }

            report.Append("  ").Append(Register(made)).Append('\n');
            report.Append("  ").Append(GiveToStation(GriddleStationModel.PrefabPath, griddle)).Append('\n');
            report.Append("  ").Append(GiveToStation(GrillStationModel.PrefabPath, grill)).Append('\n');
            report.Append("  ").Append(AddToMenus());
            return report.ToString();
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

            Undo.RecordObject(item, "Griddle & grill content");
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

            Undo.RecordObject(recipe, "Griddle & grill content");
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

            Undo.RecordObject(database, "Griddle & grill content");
            database.AllItems = all.ToArray();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);
            return $"{added} items added to the ItemsDatabase.";
        }

        /// <summary>Puts the recipes on the station in the sheet's order, in place of whatever it had.</summary>
        static string GiveToStation(string stationPath, List<CookingRecipe> recipes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(stationPath) == null)
                return $"WARNING: no station at {stationPath} - build it, then run this again";

            GameObject root = PrefabUtility.LoadPrefabContents(stationPath);
            try
            {
                var station = root.GetComponent<CookeryStation>();
                if (station == null) return $"WARNING: {stationPath} has no CookeryStation";

                var serialized = new SerializedObject(station);
                SerializedProperty list = serialized.FindProperty("recipies");
                list.arraySize = recipes.Count;
                for (int i = 0; i < recipes.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, stationPath);
                return $"{root.name} cooks {recipes.Count} dishes.";
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
                Undo.RecordObject(definition, "Griddle & grill content");
                definition.orders = orders.ToArray();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
                added++;
            }

            return added == 0 ? "Visitors' menus already have the griddle and grill dishes." : $"{added} visitors' menus now include a griddle or grill dish.";
        }
    }
}
