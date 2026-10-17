using UnityEditor;
using UnityEngine;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// Who turns up at which level and Safety: the everyday trade from the start, the better-paying
    /// kinds as the restaurant grows and is believed safe. Re-runnable; it only writes the two gates
    /// on the visitor assets it knows by name.
    /// </summary>
    public static class VisitorGates
    {
        const string Folder = "Assets/Game Assets/Content/Visitors/Visitors";

        // name, level from, Safety from
        static readonly (string name, int level, float safety)[] Gates =
        {
            ("Visitor_Regular", 1, 0f),
            ("Visitor_Farmhand", 1, 0f),
            ("Visitor_Trucker", 2, 0f),
            ("Visitor_Courier", 3, 20f),
            ("Visitor_Family", 4, 45f),
        };

        [MenuItem("Food Street/Visitors/Apply Level & Safety Gates")]
        static void ApplyFromMenu() => Debug.Log($"[Visitors] {Apply()}");

        public static string Apply()
        {
            var done = new System.Collections.Generic.List<string>();

            foreach (var gate in Gates)
            {
                var visitor = AssetDatabase.LoadAssetAtPath<VisitorDefinition>($"{Folder}/{gate.name}.asset");
                if (visitor == null) continue;

                Undo.RecordObject(visitor, "Apply Visitor Gates");
                visitor.minLevel = gate.level;
                visitor.minSafety = gate.safety;
                EditorUtility.SetDirty(visitor);
                AssetDatabase.SaveAssetIfDirty(visitor);

                done.Add($"{visitor.displayName} L{gate.level}/S{gate.safety:0}");
            }

            return $"Visitor gates: {string.Join(", ", done)}.";
        }
    }
}
