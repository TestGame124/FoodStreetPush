using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// A note left in the level by 'Set Up Main Characters': the diner starts with its two main
    /// characters and no hired hands, and these are the workers and staff it switched off for that -
    /// kept, not deleted, so they can be hired back in later or the old line-up restored.
    /// </summary>
    public sealed class StartingCrew : MonoBehaviour
    {
        [Tooltip("The second main character the setup put in.")]
        public GameObject crew;

        [Tooltip("Workers and staff switched off for the start, to switch back on to restore.")]
        public GameObject[] switchedOff = new GameObject[0];
    }
}
