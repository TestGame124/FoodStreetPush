using System;

namespace LastOrders.Night
{
    /// <summary>
    /// "Something is on the road." One flag, raised by whatever eventually threatens the
    /// compound during working hours.
    ///
    /// The incident content itself does not exist yet - the siege is a night phase - but the
    /// customer reaction to one is specified exactly: approaching visitors turn round and head
    /// west, and customers already inside shelter in the diner rather than walking out into
    /// it. That behaviour is worth building now and worthless without a trigger, so this is
    /// the trigger: a service with no opinion about what the danger is, which any future
    /// daytime event can raise.
    ///
    /// Deliberately not a bool on some other system. A raid, a breach during the day, and a
    /// scripted story beat should all be able to close the road, and they should not each have
    /// to know about the visitor director to do it.
    /// </summary>
    public sealed class DaytimeIncidentService
    {
        public bool IsActive { get; private set; }

        /// <summary>What closed the road, for the HUD line. "Something on the west road."</summary>
        public string Reason { get; private set; }

        /// <summary>Raised when the road closes, with the reason.</summary>
        public event Action<string> OnBegan;

        /// <summary>Raised when it reopens.</summary>
        public event Action OnEnded;

        /// <summary>
        /// Raises an incident. Re-raising while one is already running updates the reason and
        /// does nothing else, so two overlapping threats do not produce two rounds of
        /// customers fleeing the same danger.
        /// </summary>
        public void Begin(string reason)
        {
            Reason = reason;

            if (IsActive) return;

            IsActive = true;
            OnBegan?.Invoke(reason);
        }

        public void End()
        {
            if (!IsActive) return;

            IsActive = false;
            Reason = null;
            OnEnded?.Invoke();
        }
    }
}
