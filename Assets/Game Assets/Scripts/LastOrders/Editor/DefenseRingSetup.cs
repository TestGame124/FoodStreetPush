using System.Text;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Core;
using LastOrders.Expansion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Lays the land round the safe zone out in its bands, in one go: the wall, then the defense
    /// ring - open ground <see cref="PerimeterStripLayout.Ring"/> metres deep all the way round, with
    /// the strip along the wall's foot and pads for guns further out - then, on the west, north and
    /// east, the land to claim, the courtyard and the plots, each walled on its own once claimed with
    /// a gate facing the safe zone's. On the south the ring runs to the road instead, and there is
    /// no land to claim.
    ///
    /// Runs the setups that own each piece, in the order they depend on one another: the road
    /// first - set back across the ring, with the world edge laid again round it when it moves -
    /// then the courtyard (the plots beside it are measured from it), its barrier row and garden,
    /// its obstacles and the patch its zombies amble over, then the plots and the compound walls,
    /// and last the strip, the wall sites and the ring pads, which stand on those walls. Safe to
    /// re-run: a road already where the layout puts it is left alone, world edge and all.
    /// </summary>
    public static class DefenseRingSetup
    {
        /// <summary>Hand-placed dressing beside the road, carried with it when it moves.</summary>
        static readonly string[] RoadsideDressing = { "SignBoard" };

        /// <summary>How far from the road's line a piece of dressing can stand and still be the road's.</summary>
        const float RoadsideReach = 12f;

        [MenuItem("Last Orders/Expansion/Set Up Defense Ring & Land (Current Scene)", priority = 31)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would drive the expansion.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                $"- Keeps a defense ring {PerimeterStripLayout.Ring:0} m deep clear all the way round the walls\n" +
                "- Sets the road back across it on the south, and lays the world edge again round the road if it moved\n" +
                "- Carries the courtyard out past it, and lays its obstacles out again\n" +
                "- Rebuilds the land plots past it, each walled on its own once claimed\n" +
                "- Lays the compound walls again, with a gate facing each piece of land\n" +
                $"- Rebuilds the strip and wall sites, and lays {PerimeterStripLayout.RingPads.Length} pads for guns out in the ring\n\n" +
                "Back the scene up first if it is not under version control.",
                "Set up", "Cancel");

            if (proceed) Debug.Log(Apply(scope, save: false));
        }

        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;
            var report = new StringBuilder("[Last Orders] Defense ring and the land beyond it laid out.\n");

            report.Append(SetRoadBack(scope, scene));

            report.Append(ExpansionSceneSetup.Apply(scope, save: false)).Append('\n');
            report.Append(CourtyardModels.Apply(save: false)).Append('\n');
            report.Append(LandPlotSetup.RefreshCourtyard(scope, save: false)).Append('\n');

            foreach (ExpansionArea area in Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (area.gameObject.scene != scene || area.name != "Area_Courtyard") continue;
                if (FieldWorkSceneSetup.FitPatch(area)) report.Append("  The courtyard's zombies amble over its new obstacles.\n");
            }

            report.Append(LandPlotSetup.Apply(scope, save: false));
            report.Append(PerimeterStripSetup.Apply(scope, save: false));

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return report.ToString();
        }

        /// <summary>
        /// Moves the road to where the layout puts it - across the ring from the south wall - when it
        /// runs anywhere else. The world edge is laid again round it (the suburbs' main road, the
        /// checkpoint, the junk kept off the tarmac), which also re-lays the visitors' road and drive
        /// and moves their route and the shuttle's stop; hand-placed dressing beside it moves with it.
        /// </summary>
        static string SetRoadBack(GameLifetimeScope scope, Scene scene)
        {
            Renderer road = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "VisitorSystem") continue;
                Transform t = root.transform.Find("Road/Road");
                if (t != null) road = t.GetComponent<Renderer>();
            }

            if (road == null) return "  No visitor road in the scene; the road side was left as it is.\n";

            float was = road.bounds.center.z;
            float shift = ZoneLayout.RoadCentreZ - was;
            if (Mathf.Abs(shift) < 0.05f) return $"  The road already runs across the ring from the south wall, at z {was:0.##}.\n";

            var moved = new StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(RoadsideDressing, root.name) < 0) continue;
                if (Mathf.Abs(root.transform.position.z - was) > RoadsideReach) continue;

                Undo.RecordObject(root.transform, "Move with the road");
                root.transform.position += new Vector3(0f, 0f, shift);
                moved.Append($" {root.name}");
            }

            return $"  Road set back {-shift:0.0} m, from z {was:0.##} to z {ZoneLayout.RoadCentreZ:0.##}" +
                   $"{(moved.Length > 0 ? $"; moved with it:{moved}" : "")}.\n" +
                   EnvironmentSceneSetup.Apply(scope, save: false, EnvironmentLayout.DefaultSeed);
        }
    }
}
