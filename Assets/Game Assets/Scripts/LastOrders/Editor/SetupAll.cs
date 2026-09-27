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

        /// <summary>
        /// The same content and buildable passes, but building the measured blockout scene of
        /// The Last Restaurant v0.2 sections 15-18 instead of deriving one from Main Level.
        ///
        /// A separate command rather than a flag on Run, because the two produce different
        /// scenes for different purposes and neither replaces the other: Run gives the project
        /// its art, terrain and shop UI, this gives an exact site plan with nothing in it that
        /// did not come from the document.
        /// </summary>
        [MenuItem("Last Orders/Set Up Everything (Blockout)", priority = -9)]
        public static void RunBlockout()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                "This will:\n\n" +
                "1. Generate the starting content assets\n" +
                "2. Fill in Last Orders data on the project's buildables\n" +
                "3. Build the measured blockout scene from BlockoutSchedule\n" +
                "4. Validate it against that schedule\n\n" +
                "The blockout is a standalone scene and does not touch LastOrders_Start. " +
                "Any unsaved changes in the current scene will be lost.",
                "Run setup", "Cancel");

            if (!proceed) return;

            try
            {
                EditorUtility.DisplayProgressBar("Last Orders", "Generating content...", 0.1f);
                ContentGenerator.Generate();

                EditorUtility.DisplayProgressBar("Last Orders", "Configuring buildables...", 0.4f);
                BuildableConfigurator.Configure();

                EditorUtility.DisplayProgressBar("Last Orders", "Building blockout scene...", 0.7f);
                BlockoutSceneBuilder.Build();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Run last and unconditionally, so a scene that came out wrong says so immediately
            // rather than at whatever point someone happens to look.
            BlockoutValidator.Validate();
        }
    }
}
