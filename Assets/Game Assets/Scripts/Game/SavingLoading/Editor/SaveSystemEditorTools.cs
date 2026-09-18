using System.IO;
using UnityEditor;
using UnityEngine;

namespace FoodStreet.Saving.EditorTools
{
    /// <summary>
    /// Editor-only access to the save file. The file is encrypted, so without
    /// these you cannot see what the game actually wrote - which is exactly when
    /// a save bug is hardest to chase.
    /// </summary>
    internal static class SaveSystemEditorTools
    {
        private const string MenuRoot = "Saving/";

        [MenuItem(MenuRoot + "Open Save Folder", false, 20)]
        private static void OpenSaveFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath + Path.DirectorySeparatorChar);
        }

        [MenuItem(MenuRoot + "Log Save As JSON", false, 21)]
        private static void LogSaveAsJson()
        {
            if (!EnsureLoaded())
                return;

            Debug.Log($"[SaveSystem] {SaveSystem.LastLoadStatus} save:\n{SaveSystem.ExportJson()}");
        }

        [MenuItem(MenuRoot + "Export Decrypted Save...", false, 22)]
        private static void ExportDecrypted()
        {
            if (!EnsureLoaded())
                return;

            string path = EditorUtility.SaveFilePanel(
                "Export decrypted save", Application.persistentDataPath, "foodstreet-save", "json");

            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, SaveSystem.ExportJson());
            Debug.Log($"[SaveSystem] Exported to {path}");
        }

        [MenuItem(MenuRoot + "Force Save Now", false, 40)]
        private static void ForceSave()
        {
            if (!EnsureLoaded())
                return;

            SaveSystem.Flush(true);
            Debug.Log("[SaveSystem] Save written.");
        }

        [MenuItem(MenuRoot + "Force Save Now", true)]
        private static bool ForceSaveValidate()
        {
            // Capturing scene state outside play mode would write a half-built
            // world over a good save.
            return Application.isPlaying;
        }

        /// <summary>
        /// Loads the file without starting the game, so the menu items work from
        /// a cold editor as well as mid-play.
        /// </summary>
        private static bool EnsureLoaded()
        {
            if (SaveSystem.IsReady)
                return true;

            SaveSystem.Initialize();

            if (SaveSystem.IsReady)
                return true;

            Debug.LogError("[SaveSystem] Could not load a save file.");
            return false;
        }
    }
}
