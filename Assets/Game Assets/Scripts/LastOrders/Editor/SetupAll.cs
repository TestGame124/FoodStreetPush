using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Runs the whole first-time setup in the one order that works.
    ///
    /// The order is not arbitrary and is the reason this exists as a single command: the
    /// buildables have to be configured before the scene is built, because the scene places
    /// them and reads their footprints, and the content has to exist before either, because
    /// the balance asset is what the scene's composition root is handed.
    /// </summary>
    public static class SetupAll
    {
        [MenuItem("Last Orders/Set Up Everything", priority = -10)]
        public static void Run()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This will:\n\n" +
                "1. Generate the starting content assets\n" +
                "2. Fill in Last Orders data on the project's buildables\n" +
                "3. Build a new scene with the Tier 1 starting area\n\n" +
                "Existing content assets are updated in place, not duplicated. " +
                "Any unsaved changes in the current scene will be lost.",
                "Run setup", "Cancel");

            if (!proceed) return;

            try
            {
                EditorUtility.DisplayProgressBar("Last Orders", "Generating content...", 0.1f);
                ContentGenerator.Generate();

                EditorUtility.DisplayProgressBar("Last Orders", "Configuring buildables...", 0.5f);
                BuildableConfigurator.Configure();

                EditorUtility.DisplayProgressBar("Last Orders", "Building scene...", 0.8f);
                SceneBuilder.Build();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("[Last Orders] Setup complete.");
        }
    }
}
