using System;
using System.Collections.Generic;
using LastOrders.Consequence;
using LastOrders.Data;
using LastOrders.Night;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>
    /// The noise maker from the security hut: set down out past the walls before an attack, and
    /// sounded during it to pull every zombie within earshot off whatever it was doing and onto
    /// it - ideally in front of the defenses, which get a crowd standing still to shoot at.
    ///
    /// It sits between each zombie and the horde's own orders. A zombie asks, every half second,
    /// whether it should head for something other than the restaurant; while the noise maker is
    /// sounding and the zombie can hear it, the answer is the noise maker, and a zombie already on
    /// its way to a defense or a wreck drops it to come. Otherwise the question goes on to the
    /// horde exactly as it did before. Zombies at the noise maker batter it; enough of them
    /// silence it early, so where it stands and when it goes off both matter.
    /// </summary>
    public sealed class NoiseMakerService : IZombieOrders
    {
        /// <summary>The noise maker as something a zombie heads for: a ring round it to gather on.</summary>
        sealed class Lure : IRaidTarget
        {
            public Vector3 Centre;
            public float Gather = 0.9f;

            public Vector3 ClosestPoint(Vector3 from)
            {
                Vector3 away = from - Centre;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-4f) away = Vector3.back;
                return Centre + away.normalized * Gather;
            }

            public float FlatDistance(Vector3 from)
            {
                Vector3 d = from - Centre;
                d.y = 0f;
                return Mathf.Max(0f, d.magnitude - Gather);
            }

            /// <summary>Nobody breaks through a wall for a noise.</summary>
            public bool WorthBreakingThroughFor => false;
        }

        readonly IZombieOrders horde;
        readonly Func<NoiseMakerTuning> tuning;
        readonly Func<bool> isUnlocked;
        readonly Func<bool> attackUnderWay;
        readonly Func<bool> canPlace;
        readonly Func<Vector3, string> whyNotHere;

        readonly Lure lure = new Lure();
        readonly HashSet<object> drawn = new HashSet<object>();

        float secondsLeft;

        public event Action<Vector3> OnPlaced;
        public event Action OnSounded;

        /// <summary>It went quiet: true when the zombies broke it, false when it ran its course.</summary>
        public event Action<bool> OnSilenced;

        /// <summary>A zombie hit it: how much of its health is left, 0 to 1.</summary>
        public event Action<float> OnStruck;

        /// <param name="horde">What the zombies are told when the noise maker is not in it.</param>
        /// <param name="canPlace">Whether it may be moved now: not while the night's zombies are on the map.</param>
        /// <param name="whyNotHere">Why a spot will not do, or null when it will.</param>
        public NoiseMakerService(IZombieOrders horde, Func<NoiseMakerTuning> tuning, Func<bool> isUnlocked, Func<bool> attackUnderWay,
                                 Func<bool> canPlace, Func<Vector3, string> whyNotHere)
        {
            this.horde = horde;
            this.tuning = tuning;
            this.isUnlocked = isUnlocked;
            this.attackUnderWay = attackUnderWay;
            this.canPlace = canPlace;
            this.whyNotHere = whyNotHere;
        }

        NoiseMakerTuning Tuning => tuning?.Invoke() ?? new NoiseMakerTuning();

        public bool IsUnlocked => isUnlocked == null || isUnlocked();

        public bool IsPlaced { get; private set; }

        public Vector3 Position => lure.Centre;

        public bool IsSounding { get; private set; }

        public float SecondsLeft => IsSounding ? Mathf.Max(0f, secondsLeft) : 0f;

        public float Health { get; private set; }

        public float HealthFraction => Mathf.Clamp01(Health / Mathf.Max(1f, Tuning.health));

        public int UsesLeft { get; private set; }

        /// <summary>Zombies on their way to it or battering it right now.</summary>
        public int Drawn => drawn.Count;

        public float Radius => Tuning.radius;

        // --- Placing ---------------------------------------------------------------------------------

        /// <summary>Whether it can be picked up and set down somewhere now, wherever that turns out to be.</summary>
        public bool CanMove(out string reason)
        {
            if (!IsUnlocked)
            {
                reason = "Find the noise maker in the old security hut first.";
                return false;
            }

            if (IsSounding)
            {
                reason = "It is sounding: it cannot be moved now.";
                return false;
            }

            if (canPlace != null && !canPlace())
            {
                reason = "Not with zombies out there. Set it down in daylight.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool CanPlace(Vector3 at, out string reason)
        {
            if (!CanMove(out reason)) return false;

            reason = whyNotHere?.Invoke(at);
            return reason == null;
        }

        public bool TryPlace(Vector3 at, out string message)
        {
            if (!CanPlace(at, out message)) return false;

            lure.Centre = new Vector3(at.x, 0f, at.z);
            IsPlaced = true;
            OnPlaced?.Invoke(lure.Centre);

            message = "Noise maker set. Sound it when the attack comes.";
            return true;
        }

        // --- Sounding --------------------------------------------------------------------------------

        public bool CanSound(out string reason)
        {
            if (!IsUnlocked)
            {
                reason = "Find the noise maker in the old security hut first.";
                return false;
            }

            if (!IsPlaced)
            {
                reason = "Set the noise maker down outside the walls first.";
                return false;
            }

            if (IsSounding)
            {
                reason = "It is already sounding.";
                return false;
            }

            if (attackUnderWay != null && !attackUnderWay())
            {
                reason = "Nothing to draw yet. Sound it when the zombies come.";
                return false;
            }

            if (UsesLeft <= 0)
            {
                reason = "Its battery is flat until the next attack.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TrySound(out string message)
        {
            if (!CanSound(out message)) return false;

            NoiseMakerTuning t = Tuning;
            UsesLeft--;
            IsSounding = true;
            secondsLeft = t.soundSeconds;
            Health = t.health;
            lure.Gather = t.gatherRadius;
            drawn.Clear();

            OnSounded?.Invoke();
            message = "The noise maker wails - every zombie nearby is coming for it.";
            return true;
        }

        /// <summary>An attack has been called: the battery is charged for it.</summary>
        public void BeginAttack() => UsesLeft = Mathf.Max(1, Tuning.usesPerAttack);

        /// <summary>The attack is over: it falls silent and anything still on its way to it lets go.</summary>
        public void EndAttack()
        {
            if (IsSounding) Silence(broken: false);
            UsesLeft = 0;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsSounding || deltaSeconds <= 0f) return;

            secondsLeft -= deltaSeconds;
            if (secondsLeft <= 0f) Silence(broken: false);
        }

        void Silence(bool broken)
        {
            IsSounding = false;
            secondsLeft = 0f;

            // Everyone drawn to it is let go at their next look: StillWanted says no from here on.
            drawn.Clear();
            OnSilenced?.Invoke(broken);
        }

        /// <summary>Whether a zombie at <paramref name="from"/> can hear it.</summary>
        public bool Hears(Vector3 from)
        {
            if (!IsSounding) return false;

            Vector3 d = from - lure.Centre;
            d.y = 0f;
            float r = Tuning.radius;
            return d.sqrMagnitude <= r * r;
        }

        // --- What each zombie is told ----------------------------------------------------------------

        public bool TryAssign(object zombie, Vector3 from, out IRaidTarget target)
        {
            if (zombie != null && Hears(from))
            {
                // Whatever the horde had it doing, it is not doing it now.
                horde?.Release(zombie);
                drawn.Add(zombie);
                target = lure;
                return true;
            }

            target = null;
            return horde != null && horde.TryAssign(zombie, from, out target);
        }

        public bool StillWanted(object zombie, IRaidTarget target, Vector3 from)
        {
            if (ReferenceEquals(target, lure)) return IsSounding && drawn.Contains(zombie);

            // In earshot of it, on its way to something else: it stops, and its next look sends it here.
            if (Hears(from)) return false;

            return horde != null && horde.StillWanted(zombie, target, from);
        }

        public bool Strike(object zombie, IRaidTarget target, float damage, Vector3 from)
        {
            if (!ReferenceEquals(target, lure)) return horde == null || horde.Strike(zombie, target, damage, from);

            if (!IsSounding) return true;

            Health -= Mathf.Max(0f, damage);
            OnStruck?.Invoke(HealthFraction);

            if (Health > 0f) return false;

            Silence(broken: true);
            return true;
        }

        public void GaveUp(object zombie, IRaidTarget target)
        {
            if (ReferenceEquals(target, lure))
            {
                drawn.Remove(zombie);
                return;
            }

            horde?.GaveUp(zombie, target);
        }

        public void Release(object zombie)
        {
            drawn.Remove(zombie);
            horde?.Release(zombie);
        }
    }
}
