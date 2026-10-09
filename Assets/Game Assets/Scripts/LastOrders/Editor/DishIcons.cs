using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The recipes' icons - bread, cornbread, tomato soup - in the style of the crop icons: each dish
    /// (<see cref="CropModels.Plated"/>) rendered with the crops' paints from a little above and to the
    /// right, lit from the top left, set in a square with the same warm dark outline and soft shadow
    /// (<see cref="CropIcons"/>). The soup is seen from higher, so the eye goes into the bowl.
    ///
    /// Each icon goes to the recipe that cooks the dish (its RecipeImage, shown on the stove's recipe
    /// list) and to the dish's own item (what the orders, the customers' bubbles and the pickups show).
    /// The clip art they replace, in Game Assets/UI/Items/Recipies, is left where it is; Restore
    /// Previous Dish Icons points both back at it.
    /// </summary>
    public static class DishIcons
    {
        const string Folder = "Assets/Game Assets/Content/LastOrders/Kitchen/UI";
        const string PreviousFolder = "Assets/Game Assets/UI/Items/Recipies";

        static readonly (CropModels.Dish dish, ItemType item, string name, string previous, float pitch)[] Icons =
        {
            (CropModels.Dish.Bread, ItemType.Bread, "Bread", "Bread", 26f),
            (CropModels.Dish.CornBread, ItemType.CornBread, "CornBread", "Cornbread", 30f),
            (CropModels.Dish.TomatoSoup, ItemType.TomatoSoup, "TomatoSoup", "TomatoSoup", 40f),
        };

        [MenuItem("Last Orders/Kitchen/Build Dish Icons (Recipes)", priority = 96)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This renders the dishes into icons and gives them to the recipes and the dishes' items:\n\n" +
                $"- {Folder}: Icon_Bread, Icon_CornBread, Icon_TomatoSoup\n" +
                "- the Bread, CornBread and TomatoSoup recipes and items: their image\n\n" +
                $"The previous icons in {PreviousFolder} are not touched.",
                "Build", "Cancel");

            if (proceed) Debug.Log(Apply());
        }

        [MenuItem("Last Orders/Kitchen/Restore Previous Dish Icons", priority = 97)]
        public static void RestorePrevious()
        {
            var report = new StringBuilder("[Last Orders] Previous dish icons restored.\n");
            foreach (var (_, item, name, previous, _) in Icons)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PreviousFolder}/{previous}.png");
                report.Append(sprite != null
                    ? $"  {name}: {CropIcons.AssignIcon(item, sprite)}; {AssignToRecipes(item, sprite)}\n"
                    : $"  {name}: WARNING: previous icon missing\n");
            }

            Debug.Log(report.ToString());
        }

        /// <summary>Renders every icon and gives each to its recipe and its item, with no dialogs.</summary>
        public static string Apply()
        {
            var report = new StringBuilder("[Last Orders] Dish icons built.\n");

            foreach (var (dish, item, name, _, pitch) in Icons)
            {
                Sprite sprite = CropIcons.SaveIcon(Folder, $"Icon_{name}", Render(dish, pitch));
                report.Append($"  Icon_{name}: {CropIcons.AssignIcon(item, sprite)}; {AssignToRecipes(item, sprite)}\n");
            }

            return report.ToString();
        }

        /// <summary>The finished icon of <paramref name="dish"/>; destroy it when done.</summary>
        public static Texture2D Render(CropModels.Dish dish, float pitch) =>
            CropIcons.Render(CropModels.Plated(dish), $"{dish}_Plated", Quaternion.Euler(pitch, -22f, 0f));

        /// <summary>Gives <paramref name="sprite"/> to every recipe that cooks an item of <paramref name="type"/>.</summary>
        static string AssignToRecipes(ItemType type, Sprite sprite)
        {
            int assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:CookingRecipe"))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<CookingRecipe>(AssetDatabase.GUIDToAssetPath(guid));
                ItemDataSO result = recipe != null && recipe.Results.item != null ? recipe.Results.item.itemDataSO : null;
                if (result == null || result.Type != type) continue;

                Undo.RecordObject(recipe, "Dish icons");
                recipe.RecipeImage = sprite;
                EditorUtility.SetDirty(recipe);
                AssetDatabase.SaveAssetIfDirty(recipe);
                assigned++;
            }

            return assigned > 0 ? $"given to {assigned} recipe{(assigned == 1 ? "" : "s")}" : $"WARNING: no recipe cooks {type}";
        }
    }
}
