using UnityEngine;

namespace LastOrders.Perimeter
{
    /// <summary>
    /// When an automatic gate opens and when it shuts, with no scene in it.
    ///
    /// Somebody whose route runs through the gate asks for it every step of the way in; it opens
    /// for them and stays open a moment after the last of them is through. Nothing a zombie does
    /// ever asks, so it never opens for one.
    ///
    /// With a zombie at it, a shut gate stays shut however many people want it: whoever is
    /// waiting waits. That is the lock. It does not slam on someone already half through,
    /// though - a gate that has started to open finishes letting them in, and then shuts at
    /// once rather than lingering, because the thing outside is what matters now.
    /// </summary>
    public sealed class GateMechanism
    {
        /// <summary>Seconds from shut to fully open.</summary>
        public float OpenSeconds = 0.9f;

        /// <summary>Seconds from fully open to shut.</summary>
        public float CloseSeconds = 1.2f;

        /// <summary>How long it stays open after the last request, with nothing dangerous near.</summary>
        public float HoldOpenSeconds = 1.5f;

        /// <summary>How long it stays open after the last request with a zombie close by.</summary>
        public float HoldOpenThreatenedSeconds = 0.2f;

        float sinceRequest = float.PositiveInfinity;

        /// <summary>0 shut, 1 fully open.</summary>
        public float OpenAmount { get; private set; }

        /// <summary>Somebody wants through and a zombie is keeping it shut.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>Far enough open for a person or a car to go through.</summary>
        public bool LetsFriendliesThrough => OpenAmount >= 0.9f;

        /// <summary>Shut enough that nothing can squeeze past the leaves.</summary>
        public bool StopsHostiles => OpenAmount < 0.7f;

        /// <summary>Somebody is on their way through.</summary>
        public void Request() => sinceRequest = 0f;

        public void Tick(float deltaSeconds, bool hostileNear)
        {
            if (deltaSeconds < 0f) deltaSeconds = 0f;

            float hold = hostileNear ? HoldOpenThreatenedSeconds : HoldOpenSeconds;
            bool wanted = sinceRequest <= hold;

            // A shut gate does not start opening with a zombie at it; one already moving does
            // not stop on whoever is in it.
            bool mayOpen = !hostileNear || OpenAmount > 0f;

            IsLocked = wanted && !mayOpen;

            bool opening = wanted && mayOpen;
            float seconds = opening ? OpenSeconds : CloseSeconds;
            float step = seconds > 0f ? deltaSeconds / seconds : 1f;

            OpenAmount = Mathf.MoveTowards(OpenAmount, opening ? 1f : 0f, step);

            sinceRequest += deltaSeconds;
        }

        /// <summary>Straight to shut, forgetting any request: it has just been knocked down or rebuilt.</summary>
        public void Reset()
        {
            OpenAmount = 0f;
            IsLocked = false;
            sinceRequest = float.PositiveInfinity;
        }
    }
}
