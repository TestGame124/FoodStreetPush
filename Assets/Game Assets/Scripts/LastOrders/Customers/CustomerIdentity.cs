using System;
using LastOrders.Data;
using UnityEngine;

namespace LastOrders.Customers
{
    /// <summary>How a party's visit ended. Drives the Reputation delta and the dawn report.</summary>
    public enum VisitOutcome
    {
        Satisfied,
        LeftHungry,
        RefusedAtDoor,
        ShelteredInCellar,
        FledAtDusk,
        Killed,
    }

    /// <summary>
    /// Everything that makes one arriving party different from another (9.6), attached
    /// alongside the existing VisitorBehaviour rather than replacing it.
    ///
    /// The movement, seating and chair logic in VisitorBehaviour already works and already
    /// runs on the new navigation layer, so the refactor adds what the GDD needs - archetype,
    /// party size, patience, payment, and the dusk resolution - and leaves the working parts
    /// alone.
    /// </summary>
    [RequireComponent(typeof(VisitorBehaviour))]
    public sealed class CustomerIdentity : MonoBehaviour, Core.IPoolable
    {
        public CustomerArchetypeDefinition Archetype { get; private set; }

        public int PartySize { get; private set; } = 1;

        /// <summary>True for the guaranteed 9.4 party. The UI marks them, because the whole
        /// point is that the player knows what they are deciding about.</summary>
        public bool IsLastParty { get; private set; }

        /// <summary>Clock-minutes remaining before this party gives up on its current stage.</summary>
        public float PatienceRemaining { get; private set; }

        public bool HasBeenServed { get; private set; }

        /// <summary>Raised when the visit resolves, with the outcome and this party.</summary>
        public event Action<CustomerIdentity, VisitOutcome> OnVisitResolved;

        public void Configure(CustomerArchetypeDefinition archetype, int partySize, bool isLastParty)
        {
            Archetype = archetype;
            PartySize = Mathf.Max(1, partySize);
            IsLastParty = isLastParty;
            PatienceRemaining = archetype != null ? archetype.patienceClockMinutes : 6f;
            HasBeenServed = false;
        }

        /// <summary>
        /// Advanced in clock-minutes, because patience is measured against the working day
        /// the player is spending, not against real time.
        /// </summary>
        public void TickPatience(float clockMinutes)
        {
            if (HasBeenServed || PatienceRemaining <= 0f) return;

            PatienceRemaining -= clockMinutes;
            if (PatienceRemaining <= 0f)
            {
                PatienceRemaining = 0f;
                Resolve(VisitOutcome.LeftHungry);
            }
        }

        public void MarkServed()
        {
            HasBeenServed = true;
            Resolve(VisitOutcome.Satisfied);
        }

        /// <summary>
        /// 9.8. What happens to a party still in the restaurant when the shutters come down.
        ///
        /// The cellar is what makes this survivable, which is why it is one of the most
        /// valuable buildables in the game. Without one, the party panics and becomes
        /// something the player has to physically manage with the Evacuate verb - their
        /// greed at minute 50 arriving as their problem at minute 62.
        /// </summary>
        public VisitOutcome ResolveAtDusk(bool hasCellar, bool finishedEating)
        {
            if (finishedEating)
            {
                Resolve(VisitOutcome.Satisfied);
                return VisitOutcome.Satisfied;
            }

            if (hasCellar)
            {
                Resolve(VisitOutcome.ShelteredInCellar);
                return VisitOutcome.ShelteredInCellar;
            }

            Resolve(VisitOutcome.FledAtDusk);
            return VisitOutcome.FledAtDusk;
        }

        /// <summary>
        /// What this party pays, given how the visit ended.
        ///
        /// Sheltering in the cellar pays in full at dawn and adds Reputation, which is the
        /// reward for having built one. Fleeing pays half: the player got the money but
        /// spent the Heat and lost the goodwill.
        /// </summary>
        public int PaymentFor(VisitOutcome outcome)
        {
            if (Archetype == null || Archetype.isWounded) return 0;

            int full = Archetype.baseCoinPayment * PartySize;

            switch (outcome)
            {
                case VisitOutcome.Satisfied:
                case VisitOutcome.ShelteredInCellar:
                    return full;
                case VisitOutcome.FledAtDusk:
                    return full / 2;
                default:
                    return 0;
            }
        }

        public float ReputationFor(VisitOutcome outcome)
        {
            if (Archetype == null) return 0f;

            switch (outcome)
            {
                case VisitOutcome.Satisfied:
                    return Archetype.reputationOnSatisfied * PartySize;
                case VisitOutcome.ShelteredInCellar:
                    return Archetype.reputationOnSatisfied * PartySize * 1.5f;
                case VisitOutcome.LeftHungry:
                case VisitOutcome.RefusedAtDoor:
                    return -Archetype.reputationOnFailed * PartySize;
                case VisitOutcome.Killed:
                    // Customers can be killed. That costs Reputation, never progress (15.5).
                    return -Archetype.reputationOnFailed * PartySize * 2f;
                default:
                    return 0f;
            }
        }

        /// <summary>Heat this party generates when served. A six-person family costs more
        /// than a lone Drifter, which is what makes the big payout a real decision.</summary>
        public float HeatMultiplier =>
            Archetype != null ? Archetype.heatMultiplier * PartySize : PartySize;

        void Resolve(VisitOutcome outcome) => OnVisitResolved?.Invoke(this, outcome);

        // --- Pooling ----------------------------------------------------------

        public void OnTakenFromPool()
        {
            HasBeenServed = false;
        }

        public void OnReturnedToPool()
        {
            // The event is the dangerous one. A subscription left attached means the next
            // party to occupy this instance reports its outcome to whoever was listening for
            // the previous one, and Reputation quietly gets paid twice.
            OnVisitResolved = null;

            Archetype = null;
            PartySize = 1;
            IsLastParty = false;
            PatienceRemaining = 0f;
            HasBeenServed = false;
        }
    }
}
