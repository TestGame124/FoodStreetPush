using UnityEngine;

namespace FoodStreet.Saving
{
    /// <summary>
    /// Drives the autosave cadence and catches the moments where a mobile
    /// process can be killed without warning.
    ///
    /// Created automatically by <see cref="SaveSystem.Initialize"/> - it is never
    /// placed in a scene, so it cannot be forgotten when a new scene is authored.
    /// </summary>
    [DefaultExecutionOrder(short.MaxValue)]
    public sealed class SaveRunner : MonoBehaviour
    {
        private static SaveRunner instance;

        internal static void Ensure()
        {
            if (instance != null)
                return;

            // Editor tooling loads the save outside play mode to inspect it.
            // There is no autosave cadence or lifecycle to drive there, and
            // spawning a DontDestroyOnLoad object would only leak into the scene.
            if (!Application.isPlaying)
                return;

            GameObject host = new GameObject("[SaveRunner]");
            host.hideFlags = HideFlags.HideAndDontSave;
            instance = host.AddComponent<SaveRunner>();
            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            SaveSystem.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            // On Android and iOS this is the last callback guaranteed to run
            // before the OS may kill the process, so the write has to be
            // synchronous rather than handed to a worker.
            if (paused)
                SaveSystem.Flush();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                SaveSystem.Flush();
        }

        private void OnApplicationQuit()
        {
            SaveSystem.Flush();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
