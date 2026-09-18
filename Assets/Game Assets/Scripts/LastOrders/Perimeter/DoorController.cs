using System;
using UnityEngine;

namespace LastOrders.Perimeter
{
    /// <summary>
    /// One door in the perimeter.
    ///
    /// The whole of 7 lives in this component's two states. A door is the only way a customer
    /// gets in, and it is the softest point in any wall. While it is admitting a party it is
    /// physically open for 90 clock-seconds - a hole in the perimeter that daytime stragglers
    /// can walk through.
    ///
    /// That last point is what moves the door trade-off from a layout decision made once into
    /// a live decision made forty times a day. The player can always hold the queue and eat
    /// the Reputation cost instead.
    /// </summary>
    [RequireComponent(typeof(PlacedObject_Done))]
    public sealed class DoorController : MonoBehaviour
    {
        /// <summary>Clock-seconds one admission takes. Authored on the balance config and
        /// pushed in at boot rather than read statically, so it stays tunable.</summary>
        public float AdmissionClockSeconds { get; set; } = 90f;

        /// <summary>Barred doors are as strong as the wall around them and admit nobody.
        /// The player's explicit "we are not open to that" lever.</summary>
        public bool IsBarred { get; private set; }

        /// <summary>True while a party is walking through. For this whole window the door is
        /// a gap in the shell, and the perimeter integrity readout reflects it.</summary>
        public bool IsOpen => admissionRemaining > 0f;

        /// <summary>True when the door can begin admitting someone new.</summary>
        public bool IsAvailable => !IsBarred && admissionRemaining <= 0f;

        /// <summary>0 to 1 through the current admission, for the progress ring on the door.</summary>
        public float AdmissionProgress =>
            AdmissionClockSeconds <= 0f ? 0f : 1f - (admissionRemaining / AdmissionClockSeconds);

        public event Action OnOpened;
        public event Action OnClosed;
        public event Action<bool> OnBarredChanged;

        float admissionRemaining;

        /// <summary>Begins admitting a party. Returns false when the door is busy or barred,
        /// which is the throughput cap that makes door count equal revenue capacity.</summary>
        public bool BeginAdmission()
        {
            if (!IsAvailable) return false;

            admissionRemaining = AdmissionClockSeconds;
            OnOpened?.Invoke();
            return true;
        }

        /// <summary>
        /// Advanced in clock-seconds, not real seconds, because admission is part of the
        /// working day's economy: sixty clock-minutes have to cover building, repairing,
        /// prepping and serving, and every admission spends some of it.
        /// </summary>
        public void TickClockSeconds(float clockSeconds)
        {
            if (admissionRemaining <= 0f) return;

            admissionRemaining -= clockSeconds;

            if (admissionRemaining <= 0f)
            {
                admissionRemaining = 0f;
                OnClosed?.Invoke();
            }
        }

        /// <summary>
        /// Bars or unbars the door. Barring mid-admission slams it shut immediately - the
        /// player has decided the thing outside matters more than the party walking in, and
        /// the game should let them make that call instantly.
        /// </summary>
        public void SetBarred(bool barred)
        {
            if (IsBarred == barred) return;

            IsBarred = barred;

            if (barred && admissionRemaining > 0f)
            {
                admissionRemaining = 0f;
                OnClosed?.Invoke();
            }

            OnBarredChanged?.Invoke(barred);
        }

        /// <summary>Called at dusk. Shutters come down, and anything mid-admission is cut
        /// off - which is exactly the moment the player finds out whether leaving a door
        /// cycling until minute 59 was worth it.</summary>
        public void ForceClose()
        {
            if (admissionRemaining <= 0f) return;

            admissionRemaining = 0f;
            OnClosed?.Invoke();
        }
    }
}
