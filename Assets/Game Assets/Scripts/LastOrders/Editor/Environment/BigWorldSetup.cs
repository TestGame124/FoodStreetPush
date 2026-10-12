using System.Text;
using LastOrders.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Lays the whole world out round the compound in one go, in the order its pieces depend on one
    /// another: the land first - the courtyard with gates out to the plots beyond it, three rings of
    /// plots with the night's corridors kept open through them, the compound walls with a gate facing
    /// each plot of the first ring, the strip and the ring pads - then the world's edge round all of
    /// it, measured from the land: the open ground, the old perimeter fence broken open where each lane
    /// comes through, the suburbs and the forest, the fog with a tongue down each corridor, the lanes,
    /// the road and the camera's limits. Last the ambient life, whose birds perch on the new walls and
    /// roofs.
    ///
    /// Every step is one of the setups the level already has, each re-runnable on its own; this only
    /// runs them in the right order. Nothing is saved.
    /// </summary>
    public static class BigWorldSetup
    {
        [MenuItem("Last Orders/Environment/Lay Out The Whole World (Current Scene)", priority = 79)]
        public static void Run()
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope.\n\nOpen the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scope.gameObject.scene.name}' in place:\n\n" +
                "- Lays out three rings of land plots round the compound, keeping a corridor open through them for each of the night's lanes\n" +
                "- Gives the courtyard gates out to the plots beyond it, and lays the compound walls, strip and ring pads again\n" +
                $"- Builds the world's edge round it all: open ground X {L.ClearMinX:0}..{L.ClearMaxX:0}, Z {L.ClearMinZ:0}..{L.ClearMaxZ:0}, " +
                "the old perimeter fence with a breach at each lane, the suburbs, the forest and the fog\n" +
                $"- Moves the night's {L.Lanes.Length} lanes into their corridors and widens the camera's limits\n" +
                "- Sets the ambient life up again on the new world\n\n" +
                "Takes a minute or two. The scene is not saved; back it up first if it is not under version control.",
                "Lay out", "Cancel");

            if (proceed) Debug.Log(Apply(scope));
        }

        public static string Apply(GameLifetimeScope scope)
        {
            Scene scene = scope.gameObject.scene;
            var report = new StringBuilder("[Last Orders] The whole world laid out.\n");

            report.Append(DefenseRingSetup.Apply(scope, save: false)).Append('\n');
            report.Append(EnvironmentSceneSetup.Apply(scope, save: false, L.DefaultSeed)).Append('\n');
            report.Append(AmbientLifeSetup.Apply()).Append('\n');

            EditorSceneManager.MarkSceneDirty(scene);
            return report.ToString();
        }
    }
}
