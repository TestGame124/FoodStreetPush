using LastOrders.Interaction;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>The noise maker out on the ground: a tap on it is the security hut's.</summary>
    public sealed class NoiseMakerDevice : MonoBehaviour, ITappable, IInteractableObject
    {
        [SerializeField] SecurityHut hut;

        public string DisplayName => "Noise maker";

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped() => hut != null ? hut.DeviceTapped() : null;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(SecurityHut owner) => hut = owner;
    }
}
