using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Repaints the level's generated ground, floors and walls - the grass, the road and streets,
    /// the drive, kerbs, pavements, the dirt path, the car park, the compound and diner walls and
    /// the restaurant's tiles - and sets their materials up again, without rebuilding anything in
    /// the scene.
    ///
    /// Each texture is painted again only if its version in <see cref="TextureGenerator"/> has
    /// changed; the materials are always set up again, so they pick up a normal map that is new.
    /// Textures keep their paths, so everything that uses them shows the new paint at once.
    /// </summary>
    public static class SurfaceRepaint
    {
        /// <summary>The materials this sets up, saved when it is done.</summary>
        static readonly string[] Materials =
        {
            EnvKit.MaterialFolder + "/Ground_Grass.mat",
            EnvKit.MaterialFolder + "/Surface_Road.mat",
            EnvKit.MaterialFolder + "/Surface_Street.mat",
            EnvKit.MaterialFolder + "/Surface_Asphalt.mat",
            EnvKit.MaterialFolder + "/Surface_Kerb.mat",
            EnvKit.MaterialFolder + "/Surface_Pavement.mat",
            EnvKit.MaterialFolder + "/Surface_DirtPath.mat",
            EnvKit.MaterialFolder + "/Surface_Parking.mat",
            EnvKit.MaterialFolder + "/Wall_ConcreteBlock.mat",
            EnvKit.MaterialFolder + "/Wall_ConcreteBlock_Dark.mat",
            EnvKit.MaterialFolder + "/Wall_Planks.mat",
            EnvKit.MaterialFolder + "/Wall_Tin.mat",
            EnvKit.MaterialFolder + "/Wall_Diner.mat",
            RestaurantSceneSetup.DiningMaterialPath,
            RestaurantSceneSetup.KitchenMaterialPath,
        };

        [MenuItem("Last Orders/Environment/Repaint Ground & Floor Textures", priority = 82)]
        public static void Run() => Debug.Log(Apply());

        public static string Apply()
        {
            // Materials set up earlier this session would otherwise be handed back as they were.
            EnvKit.Reset();

            var report = new StringBuilder("Ground and floor textures (each painted again only if its version changed):\n");
            report.Append($"  {GroundBuilder.Restyle(SceneManager.GetActiveScene())}\n");
            report.Append($"  {SurfaceKit.RestyleSurfaces()}\n");
            report.Append($"  {RestaurantSceneSetup.RestyleFloor()}\n");

            foreach (string path in Materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null) AssetDatabase.SaveAssetIfDirty(material);
            }

            return report.ToString();
        }
    }
}
