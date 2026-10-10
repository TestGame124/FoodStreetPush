using LastOrders.Blockout;
using LastOrders.UI.Hud;
using UnityEngine;

namespace LastOrders.Interaction
{
    /// <summary>
    /// Shows the result of a tap on whichever HUD this level happens to have.
    ///
    /// Exists because the Last Orders objects are tapped by two different input layers. The
    /// grey-box scene uses <see cref="TapSelector"/>, which takes the message the object
    /// returns and displays it itself. The working level uses the project's own Mouse3D, which
    /// dispatches <see cref="IInteractableObject.OnTouch"/> and has nowhere to put a string -
    /// so an object tapped there would do the work and say nothing.
    ///
    /// Rather than teach either input layer about the other, every tappable implements both
    /// interfaces and routes its message through here. The rule from section 4 is that a tap
    /// is never silently accepted, and that rule should not depend on which scene the object
    /// is standing in.
    /// </summary>
    public static class TapFeedback
    {
        static CompoundHud hud;
        static GameplayHud gameplay;

        /// <summary>
        /// Displays a message and reports the tap as handled.
        ///
        /// Always returns true, including when there is no HUD to show it on. The object DID
        /// act on the tap - it queued a job, or it explained why it could not - and returning
        /// false would pass the same tap on to whatever stands behind it, which is how one tap
        /// opens a stove and drops a building.
        /// </summary>
        public static bool Report(string message)
        {
            if (string.IsNullOrEmpty(message)) return true;

            // The gameplay HUD shows it as a toast; the grey-box HUD on its objective line.
            GameplayHud toastHud = Gameplay;
            if (toastHud != null) toastHud.ShowMessage(message);
            else if (Hud != null) Hud.ShowMessage(message);

            // Logged as well as shown. The HUD line is small and easy to miss, and a tap that
            // did something has to be distinguishable from a tap that missed - otherwise
            // "nothing happens when I tap" is unanswerable.
            Debug.Log($"[Tap] {message}");

            return true;
        }

        /// <summary>Clears the cached HUD on scene change, so a reloaded scene does not hold a
        /// reference to the previous one's canvas.</summary>
        public static void Reset()
        {
            hud = null;
            gameplay = null;
        }

        /// <summary>
        /// The HUD to show messages on.
        ///
        /// Re-searched whenever the cached one is gone, rather than latched after a single
        /// miss. A static that gives up permanently is a static that stays given-up across
        /// play sessions when domain reloading is off - and the symptom is taps that worked
        /// the first time and silently stop working after that.
        /// </summary>
        static CompoundHud Hud
        {
            get
            {
                if (hud == null) hud = Object.FindFirstObjectByType<CompoundHud>();
                return hud;
            }
        }

        static GameplayHud Gameplay
        {
            get
            {
                if (gameplay == null) gameplay = Object.FindFirstObjectByType<GameplayHud>();
                return gameplay;
            }
        }
    }
}
