using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// What the salad station makes, from the design sheet: six salads, each with its price, its
    /// time and what goes into it, as recipes on pfSaladStation and dish items the inventory, the
    /// orders and the pickups know.
    ///
    ///   Garden Salad              4   lettuce, tomato                  45 s
    ///   Sweetcorn Salad           5   corn, lettuce, tomato            1 min
    ///   Carrot Slaw               5   carrot x2, lettuce               90 s
    ///   Tomato and Cheese Salad  10   tomato x2, cheese                3 min
    ///   Bean Salad               12   canned beans, corn, tomato       4 min
    ///   Chicken Salad            14   chicken, lettuce, tomato         5 min
    ///
    /// The ingredients the farm does not grow - lettuce, carrots, cheese, tinned beans, chicken - are
    /// made as items too, so the recipes can ask for them. Every dish and every new ingredient gets
    /// an icon rendered from its model (<see cref="CropModels.Plated"/>, <see cref="CropModels.Stocked"/>)
    /// in the style of the other dish icons. Running it again updates what is there rather than
    /// making copies.
    /// </summary>
    public static class SaladContent
    {
        const string ItemsFolder = "Assets/Game Assets/Scripts/Item/ItemsData";
        const string DishFolder = ItemsFolder + "/Recipe/SaladStation";
        const string RecipeFolder = "Assets/Game Assets/Scripts/Item/Cooking System/Recipies/Salad";
        const string IconFolder = "Assets/Game Assets/Content/LastOrders/Kitchen/UI";
        const string DatabasePath = ItemsFolder + "/ItemsDatabase.asset";
        const string StationPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Stoves/SaladStation/pfSaladStation.prefab";

        sealed class Salad
        {
            public CropModels.Dish Dish;
            public ItemType Item;
            public string Name, File;
            public int Price, Seconds, Stars, StartRewards;
            public float Pitch;
            public (ItemType item, int amount)[] Ingredients;
        }

        static readonly Salad[] Salads =
        {
            new Salad
            {
                Dish = CropModels.Dish.GardenSalad, Item = ItemType.GardenSalad, Name = "Garden Salad", File = "GardenSalad",
                Price = 4, Seconds = 45, Stars = 1, StartRewards = 1, Pitch = 36f,
                Ingredients = new[] { (ItemType.Lettuce, 1), (ItemType.Tomato, 1) },
            },
            new Salad
            {
                Dish = CropModels.Dish.SweetcornSalad, Item = ItemType.SweetcornSalad, Name = "Sweetcorn Salad", File = "SweetcornSalad",
                Price = 5, Seconds = 60, Stars = 1, StartRewards = 1, Pitch = 38f,
                Ingredients = new[] { (ItemType.Corn, 1), (ItemType.Lettuce, 1), (ItemType.Tomato, 1) },
            },
            new Salad
            {
                Dish = CropModels.Dish.CarrotSlaw, Item = ItemType.CarrotSlaw, Name = "Carrot Slaw", File = "CarrotSlaw",
                Price = 5, Seconds = 90, Stars = 1, StartRewards = 1, Pitch = 36f,
                Ingredients = new[] { (ItemType.Carrot, 2), (ItemType.Lettuce, 1) },
            },
            new Salad
            {
                Dish = CropModels.Dish.TomatoCheeseSalad, Item = ItemType.TomatoCheeseSalad, Name = "Tomato and Cheese Salad", File = "TomatoCheeseSalad",
                Price = 10, Seconds = 180, Stars = 2, StartRewards = 2, Pitch = 44f,
                Ingredients = new[] { (ItemType.Tomato, 2), (ItemType.Cheese, 1) },
            },
            new Salad
            {
                Dish = CropModels.Dish.BeanSalad, Item = ItemType.BeanSalad, Name = "Bean Salad", File = "BeanSalad",
                Price = 12, Seconds = 240, Stars = 2, StartRewards = 2, Pitch = 40f,
                Ingredients = new[] { (ItemType.CannedBeans, 1), (ItemType.Corn, 1), (ItemType.Tomato, 1) },
            },
            new Salad
            {
                Dish = CropModels.Dish.ChickenSalad, Item = ItemType.ChickenSalad, Name = "Chicken Salad", File = "ChickenSalad",
                Price = 14, Seconds = 300, Stars = 2, StartRewards = 3, Pitch = 40f,
                Ingredients = new[] { (ItemType.Chicken, 1), (ItemType.Lettuce, 1), (ItemType.Tomato, 1) },
            },
        };

        static readonly (CropModels.Ingredient model, ItemType item, string name, string file)[] Ingredients =
        {
            (CropModels.Ingredient.Lettuce, ItemType.Lettuce, "Lettuce", "Lettuce"),
            (CropModels.Ingredient.Carrot, ItemType.Carrot, "Carrot", "Carrot"),
            (CropModels.Ingredient.Cheese, ItemType.Cheese, "Cheese", "Cheese"),
            (CropModels.Ingredient.CannedBeans, ItemType.CannedBeans, "Canned Beans", "CannedBeans"),
            (CropModels.Ingredient.Chicken, ItemType.Chicken, "Chicken", "Chicken"),
        };

        /// <summary>Looking a little down on an ingredient from its front right, as the crop icons do.</summary>
        static readonly Quaternion IngredientView = Quaternion.Euler(26f, -22f, 0f);

        [MenuItem("Last Orders/Kitchen/Build Salad Recipes + Icons", priority = 100)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This makes the salad station's six salads - Garden Salad, Sweetcorn Salad, Carrot Slaw, Tomato and Cheese Salad, " +
                "Bean Salad, Chicken Salad - as recipes and dish items, and the ingredients they need that do not exist yet " +
                "(Lettuce, Carrot, Cheese, Canned Beans, Chicken) as items.\n\n" +
                $"Every one gets an icon rendered into {IconFolder}. The items go into the ItemsDatabase and the recipes onto " +
                "pfSaladStation. Running it again updates them in place.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        /// <summary>Makes or updates every item, recipe and icon, with no dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(DishFolder);
            ContentGenerator.EnsureFolder(RecipeFolder);
            ContentGenerator.EnsureFolder(IconFolder);

            var report = new StringBuilder("[Last Orders] Salad recipes built.\n");
            var made = new Dictionary<ItemType, ItemDataSO>();

            foreach (var (model, type, name, file) in Ingredients)
            {
                Texture2D icon = CropIcons.Render(CropModels.Stocked(model), $"{model}_Stocked", IngredientView);
                Sprite sprite = CropIcons.SaveIcon(IconFolder, $"Icon_{file}", icon);
                ItemDataSO item = Item($"{ItemsFolder}/{file}.asset", name, type, 0, 0, 0, sprite);
                made[type] = item;
                report.Append($"  {name}: item {AssetDatabase.GetAssetPath(item)}, Icon_{file}\n");
            }

            var recipes = new List<CookingRecipe>();
            foreach (Salad salad in Salads)
            {
                Texture2D icon = DishIcons.Render(salad.Dish, salad.Pitch);
                Sprite sprite = CropIcons.SaveIcon(IconFolder, $"Icon_{salad.File}", icon);
                ItemDataSO dish = Item($"{DishFolder}/{salad.File}Item.asset", salad.Name, salad.Item, salad.Price, salad.Stars, salad.Seconds, sprite);
                made[salad.Item] = dish;

                CookingRecipe recipe = Recipe($"{RecipeFolder}/{salad.File}Recipe.asset", salad, dish, sprite, report);
                recipes.Add(recipe);
                report.Append($"  {salad.Name}: {salad.Price} coins, {salad.Seconds} s, recipe {AssetDatabase.GetAssetPath(recipe)}\n");
            }

            report.Append($"  {Register(made.Values)}\n");
            report.Append($"  {GiveToStation(recipes)}\n");
            return report.ToString();
        }

        /// <summary>The item at <paramref name="path"/>, made if it is not there, with these values.</summary>
        static ItemDataSO Item(string path, string name, ItemType type, int coins, int stars, int seconds, Sprite icon)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDataSO>();
                AssetDatabase.CreateAsset(item, path);
            }

            Undo.RecordObject(item, "Salad content");
            item.Name = name;
            item.Type = type;
            item.coinsReward = coins;
            item.starsReward = stars;
            item.canBePlanted = false;
            item.totalTimeInSeconds = seconds;
            item.itemImage = icon;
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(item);
            return item;
        }

        static CookingRecipe Recipe(string path, Salad salad, ItemDataSO dish, Sprite icon, StringBuilder report)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<CookingRecipe>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<CookingRecipe>();
                AssetDatabase.CreateAsset(recipe, path);
            }

            var ingredients = new List<ItemAmount>();
            foreach (var (type, amount) in salad.Ingredients)
            {
                ItemDataSO data = Find(type);
                if (data == null)
                {
                    report.Append($"  WARNING: {salad.Name} needs {type}, and there is no {type} item\n");
                    continue;
                }

                ingredients.Add(new ItemAmount { item = new Item(data, 1), Amount = amount });
            }

            Undo.RecordObject(recipe, "Salad content");
            recipe.RecipeImage = icon;
            recipe.TotalTimeToCook = salad.Seconds;
            recipe.Ingrediants = ingredients;

            // One serving, as every other recipe makes: the cook hands out results.Amount + 1.
            recipe.Results = new ItemAmount { item = new Item(dish, 1), Amount = 0 };
            recipe.startRewards = salad.StartRewards;
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssetIfDirty(recipe);
            return recipe;
        }

        /// <summary>The item of <paramref name="type"/>, wherever it is kept.</summary>
        static ItemDataSO Find(ItemType type)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDataSO"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.Type == type) return item;
            }

            return null;
        }

        /// <summary>Adds the items to the ItemsDatabase, which is what the game looks items up in by type.</summary>
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

            if (added == 0) return "ItemsDatabase already lists them all";

            Undo.RecordObject(database, "Salad content");
            database.AllItems = all.ToArray();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);
            return $"{added} items added to the ItemsDatabase";
        }

        /// <summary>Puts the recipes on pfSaladStation in the sheet's order, in place of whatever it had.</summary>
        static string GiveToStation(List<CookingRecipe> recipes)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(StationPath);
            try
            {
                var station = root.GetComponent<CookeryStation>();
                if (station == null) return "WARNING: pfSaladStation has no CookeryStation";

                var serialized = new SerializedObject(station);
                SerializedProperty list = serialized.FindProperty("recipies");
                list.arraySize = recipes.Count;
                for (int i = 0; i < recipes.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, StationPath);
                return $"pfSaladStation cooks {recipes.Count} salads";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
