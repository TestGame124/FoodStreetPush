using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// Readies the visitor animator and the diner table for visitors who get into their chairs
    /// rather than appearing in them.
    ///
    /// The animator: the IK pass on its base layer, which <see cref="SeatedPose"/> needs to put
    /// hands on the table; the stand-to-sit and sit-to-stand blends as long as the visitor's own
    /// sitting down and getting up, so the hips go down while the body goes back; and eating on
    /// the seated clip - it was on a clip of somebody shouting, with the fork now done by IK - and
    /// back to sitting when the meal is done rather than standing up and sitting down again.
    ///
    /// The table: a <see cref="TableSeating"/> on pfTable with the cushion's height measured off
    /// the chair model. Tables get one at runtime anyway; this one is there to be tuned.
    ///
    /// Safe to run again: everything it sets, it sets to the same thing.
    /// </summary>
    public static class VisitorSeatingSetup
    {
        const string ControllerPath = "Assets/Game Assets/Prefabs/Characters/VisitorAnimator.controller";
        const string TablePrefabPath = "Assets/GridBuildingSystem/Prefabs/Furniture/Table/pfTable.prefab";

        /// <summary>Seconds to put the fork down and sit back when the meal is done.</summary>
        const float FinishMealSeconds = 0.25f;

        [MenuItem("Food Street/Visitors/Set Up Seated Visitors (Chairs & Animator)", priority = 2)]
        public static void Run() => Debug.Log(Apply());

        public static string Apply()
        {
            var report = new StringBuilder("[Visitors] Seated visitors set up.\n");
            report.Append("  ").Append(SetUpAnimator()).Append('\n');
            report.Append("  ").Append(SetUpTable()).Append('\n');
            return report.ToString();
        }

        static string SetUpAnimator()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) return $"WARNING: no animator at {ControllerPath}.";

            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length == 0) return "WARNING: the visitor animator has no layers.";

            if (!layers[0].iKPass)
            {
                layers[0].iKPass = true;
                controller.layers = layers;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState movement = Find(machine, "Movement");
            AnimatorState sitting = Find(machine, "Sitting");
            AnimatorState eating = Find(machine, "Eating");

            if (movement == null || sitting == null || eating == null)
                return "WARNING: the visitor animator is missing its Movement, Sitting or Eating state; only the IK pass was set.";

            eating.motion = sitting.motion;

            Timing(Transition(movement, sitting, "Sit", AnimatorConditionMode.If), Visitor.SitSeconds);
            Timing(Transition(sitting, movement, "Sit", AnimatorConditionMode.IfNot), Visitor.StandSeconds);

            // Finishing the meal goes back to sitting, not to standing: the old transition stood
            // them up and sat them straight back down whenever they stayed on after eating.
            foreach (AnimatorStateTransition transition in eating.transitions)
            {
                if (transition.destinationState == movement && Has(transition, "Eat", AnimatorConditionMode.IfNot))
                    transition.destinationState = sitting;
            }

            Timing(Transition(eating, sitting, "Eat", AnimatorConditionMode.IfNot), FinishMealSeconds);
            Timing(Transition(eating, movement, "Sit", AnimatorConditionMode.IfNot), Visitor.StandSeconds);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            return $"VisitorAnimator: IK pass on; sit {Visitor.SitSeconds:0.##} s, stand {Visitor.StandSeconds:0.##} s; " +
                   "eating on the seated clip, and back to sitting after.";
        }

        static string SetUpTable()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(TablePrefabPath);

            try
            {
                var table = root.GetComponent<TableBehaviour>();
                if (table == null) return $"WARNING: no TableBehaviour on {TablePrefabPath}.";

                var seating = root.GetComponent<TableSeating>();
                bool added = seating == null;
                if (added) seating = root.AddComponent<TableSeating>();

                string measured = "cushion height left as it was (no chair model to measure)";
                float? cushion = MeasureCushion(table);
                if (cushion.HasValue)
                {
                    var serialized = new SerializedObject(seating);
                    serialized.FindProperty("seatHeight").floatValue = cushion.Value;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    measured = $"cushion {cushion.Value:0.###} m above the chair's pivot";
                }

                PrefabUtility.SaveAsPrefabAsset(root, TablePrefabPath);
                return $"pfTable: TableSeating {(added ? "added" : "kept")}, {measured}.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The top of the seat: straight down onto the chair model over a grid round its pivot,
        /// the middle answer of the lot. The grid's back row can land on the foot of the backrest,
        /// and the median ignores it where the highest point would not.
        /// </summary>
        static float? MeasureCushion(TableBehaviour table)
        {
            foreach (TableBehaviour.Chair chair in table.Chairs)
            {
                Transform piece = chair != null ? chair.sitPos : null;
                MeshFilter filter = piece != null ? piece.GetComponentInChildren<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null) continue;

                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                float ceiling = mesh.bounds.min.y + mesh.bounds.size.y * 0.7f;

                var heights = new System.Collections.Generic.List<float>();
                for (int i = -2; i <= 2; i++)
                {
                    for (int k = -2; k <= 2; k++)
                    {
                        float? hit = HighestBelow(vertices, triangles, new Vector2(i * 0.05f, k * 0.05f), ceiling);
                        if (hit.HasValue)
                            heights.Add(filter.transform.TransformPoint(new Vector3(i * 0.05f, hit.Value, k * 0.05f)).y - piece.position.y);
                    }
                }

                if (heights.Count == 0) continue;

                heights.Sort();
                return heights[heights.Count / 2];
            }

            return null;
        }

        /// <summary>The highest surface of the mesh below <paramref name="ceiling"/> at one point of
        /// its floor plan, in the mesh's own space.</summary>
        static float? HighestBelow(Vector3[] vertices, int[] triangles, Vector2 at, float ceiling)
        {
            float? best = null;

            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];

                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-9f) continue;

                float u = ((b.z - c.z) * (at.x - c.x) + (c.x - b.x) * (at.y - c.z)) / d;
                float v = ((c.z - a.z) * (at.x - c.x) + (a.x - c.x) * (at.y - c.z)) / d;
                if (u < 0f || v < 0f || u + v > 1f) continue;

                float y = u * a.y + v * b.y + (1f - u - v) * c.y;
                if (y <= ceiling && (!best.HasValue || y > best.Value)) best = y;
            }

            return best;
        }

        static AnimatorState Find(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (child.state.name == name) return child.state;

            return null;
        }

        /// <summary>The transition from one state to another on this condition, made if missing.</summary>
        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode)
        {
            foreach (AnimatorStateTransition transition in from.transitions)
            {
                if (transition.destinationState == to && Has(transition, parameter, mode)) return transition;
            }

            AnimatorStateTransition made = from.AddTransition(to);
            made.AddCondition(mode, 0f, parameter);
            return made;
        }

        static bool Has(AnimatorStateTransition transition, string parameter, AnimatorConditionMode mode)
        {
            foreach (AnimatorCondition condition in transition.conditions)
                if (condition.parameter == parameter && condition.mode == mode) return true;

            return false;
        }

        static void Timing(AnimatorStateTransition transition, float seconds)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = seconds;
            transition.offset = 0f;
        }
    }
}
