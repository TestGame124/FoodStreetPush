using System;
using System.Collections.Generic;
using FoodStreet.Navigation;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Economy;
using LastOrders.Expansion;
using LastOrders.Perimeter;
using UnityEngine;

namespace LastOrders.Restaurant
{
    /// <summary>What the "expand the restaurant?" popup shows.</summary>
    public sealed class RestaurantOffer
    {
        public string Title;
        public string Body;
        public readonly List<ExpansionRequirement> Requirements = new List<ExpansionRequirement>();

        /// <summary>True when there is a side left to grow and it is the time of day to do it.
        /// Whether the player can afford it is asked again when they pick the side.</summary>
        public bool CanChoose;

        public string Reason;
    }

    /// <summary>
    /// The restaurant building as the rules see it: the ground its walls stand round, how far
    /// each side has been pushed out, what the next push costs - and the line no zombie crosses.
    ///
    /// A plain class with no scene in it. The walls in the scene tell it where they stand and
    /// move themselves when it says a side has grown; whether the ground a side would grow into
    /// is clear is theirs to find out, because only the scene can see what is standing on it.
    ///
    /// The walls are the last line. A zombie that gets past the compound wall and the towers is
    /// stopped at them and batters them, and they never come down, so nothing that gets into the
    /// compound gets into the diner. It stays there, alive, until the defenses finish it - and
    /// every second it spends at the door is pressure on the night
    /// (<see cref="Night.NightDangerService"/>). The scene registers this as an
    /// <see cref="IMovementBarrier"/> while the walls stand.
    /// </summary>
    public sealed class RestaurantService : IMovementBarrier, IBreakable
    {
        readonly RestaurantTuning tuning;
        readonly ResourceLedger ledger;
        readonly IWallet coins;
        readonly DayCycleMachine machine;
        readonly Func<bool> breached;

        readonly int[] steps = new int[3];

        Rect footprint;
        Vector2 front = Vector2.down;

        public RestaurantService(RestaurantTuning tuning, ResourceLedger ledger, IWallet coins, DayCycleMachine machine,
                                 Func<bool> breached)
        {
            this.tuning = tuning ?? new RestaurantTuning();
            this.ledger = ledger;
            this.coins = coins;
            this.machine = machine;
            this.breached = breached;
        }

        public RestaurantTuning Tuning => tuning;

        /// <summary>True once the scene has said where the walls are. Without it nothing is
        /// stopped at them and nothing can grow.</summary>
        public bool HasFootprint { get; private set; }

        /// <summary>The outer faces of the walls, flat: x across, y along the world's Z.</summary>
        public Rect Footprint => footprint;

        /// <summary>Which way the front wall faces, flat. Out through the door.</summary>
        public Vector2 Front => front;

        public int TotalExpansions => steps[0] + steps[1] + steps[2];

        /// <summary>Blows the walls have taken since the game started.</summary>
        public int BlowsTaken { get; private set; }

        /// <summary>The restaurant's health in an attack. Full at the start of every one, carried
        /// from wave to wave, and the attack is lost the moment it reaches zero.</summary>
        public float Health { get; private set; } = 1f;

        public float MaxHealth { get; private set; } = 1f;

        public float HealthFraction => MaxHealth <= 0f ? 1f : Mathf.Clamp01(Health / MaxHealth);

        /// <summary>Below full: what the crew patches up in the morning.</summary>
        public bool IsDamaged => Health < MaxHealth - 0.01f;

        /// <summary>True while an attack is on: only then do blows on the walls cost health.</summary>
        public bool IsUnderAttack { get; private set; }

        /// <summary>The health changed, with the new value and the most it can be.</summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>The blow that took the last of it. Raised once per attack.</summary>
        public event Action OnHealthDepleted;

        /// <summary>The walls moved: the footprint they now stand round.</summary>
        public event Action<Rect> OnFootprintChanged;

        /// <summary>A side grew, with the strip of ground it took in.</summary>
        public event Action<RestaurantSide, Rect> OnExpanded;

        /// <summary>A zombie's blow landed on the walls, with the point of them it struck.</summary>
        public event Action<Vector3> OnHit;

        // --- The scene says where it is -------------------------------------------------------

        /// <summary>Where the walls stand, and which way is out through the door. The front is
        /// snapped to the nearest world axis: the building is square to the world.</summary>
        public void SetFootprint(Rect area, Vector2 frontDirection)
        {
            footprint = area;
            front = Snap(frontDirection);
            HasFootprint = area.width > 0f && area.height > 0f;
            OnFootprintChanged?.Invoke(footprint);
        }

        // --- Growing --------------------------------------------------------------------------

        public int StepsTaken(RestaurantSide side) => steps[Index(side)];

        public int StepsLeft(RestaurantSide side) =>
            Mathf.Max(0, (tuning.For(side)?.maxSteps ?? 0) - StepsTaken(side));

        public bool AnyStepsLeft =>
            StepsLeft(RestaurantSide.Forward) > 0 || StepsLeft(RestaurantSide.Left) > 0 || StepsLeft(RestaurantSide.Right) > 0;

        /// <summary>What the next expansion costs, whichever side it is. Each one costs more than
        /// the last.</summary>
        public Price NextPrice => tuning.firstExpansionCost.IsFree
            ? default
            : tuning.firstExpansionCost.Scaled(Mathf.Pow(Mathf.Max(1f, tuning.costGrowth), TotalExpansions));

        /// <summary>The flat direction a side grows in.</summary>
        public Vector2 DirectionOf(RestaurantSide side)
        {
            // Stood outside the front, facing the building: left and right are the player's.
            Vector2 facing = -front;

            switch (side)
            {
                case RestaurantSide.Left: return new Vector2(-facing.y, facing.x);
                case RestaurantSide.Right: return new Vector2(facing.y, -facing.x);
                default: return front;
            }
        }

        /// <summary>The ground the next step on that side would take in.</summary>
        public Rect StripFor(RestaurantSide side) =>
            Strip(footprint, DirectionOf(side), tuning.For(side)?.depth ?? 0f);

        /// <summary>True when the day allows building: not after dark, and nothing inside the walls.</summary>
        public bool IsExpansionTime(out string reason)
        {
            if (machine != null && !machine.CurrentPhase.AllowsBuilding())
            {
                reason = "The restaurant can only be expanded in daylight.";
                return false;
            }

            if (breached != null && breached())
            {
                reason = "Not with zombies inside the walls.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Everything but the ground itself: the side has room left to grow, it is the
        /// time of day for it, and the player can pay.</summary>
        public bool CanExpand(RestaurantSide side, out string reason)
        {
            if (!HasFootprint)
            {
                reason = "The restaurant's walls have not been found.";
                return false;
            }

            if (StepsLeft(side) <= 0)
            {
                reason = $"The restaurant cannot grow any further {Describe(side)}.";
                return false;
            }

            if (!IsExpansionTime(out reason)) return false;

            reason = Till.Shortfall(NextPrice, ledger, coins);
            return reason == null;
        }

        /// <summary>Pays and moves the side's wall out. The scene must have checked the strip is
        /// clear first - this cannot see it.</summary>
        public bool TryExpand(RestaurantSide side, out string message)
        {
            if (!CanExpand(side, out message)) return false;

            Price price = NextPrice;
            if (!Till.TryPay(price, ledger, coins, out message)) return false;

            Rect strip = StripFor(side);
            steps[Index(side)]++;
            footprint = Grow(footprint, strip);

            string paid = price.IsFree ? string.Empty : $" for {price.Describe(Till.CoinName(coins))}";
            message = $"The restaurant has grown {Describe(side)}{paid}.";

            Debug.Log($"[Last Orders] {message} Walls now at x {footprint.xMin:0.##}-{footprint.xMax:0.##}, " +
                      $"z {footprint.yMin:0.##}-{footprint.yMax:0.##}.");

            OnExpanded?.Invoke(side, strip);
            OnFootprintChanged?.Invoke(footprint);
            return true;
        }

        public RestaurantOffer GetOffer()
        {
            var offer = new RestaurantOffer { Title = "Expand the restaurant?" };

            Price price = NextPrice;
            if (price.coins > 0)
                offer.Requirements.Add(new ExpansionRequirement(Till.CoinName(coins), coins != null ? coins.Balance : 0, price.coins));

            if (price.resources != null)
            {
                for (int i = 0; i < price.resources.Length; i++)
                {
                    ResourceAmount cost = price.resources[i];
                    if (cost.amount <= 0) continue;

                    offer.Requirements.Add(new ExpansionRequirement(cost.type.DisplayName(),
                                                                    ledger != null ? ledger.Get(cost.type) : 0, cost.amount));
                }
            }

            offer.Body = "Push a wall out to make the dining room bigger. Choose where once you say yes - " +
                         "the ground has to be clear of anything standing on it.\n" +
                         $"Forward: {StepsLeft(RestaurantSide.Forward)} left   " +
                         $"Left: {StepsLeft(RestaurantSide.Left)} left   " +
                         $"Right: {StepsLeft(RestaurantSide.Right)} left";

            if (!HasFootprint) offer.Reason = "The restaurant's walls have not been found.";
            else if (!AnyStepsLeft) offer.Reason = "The restaurant is as big as it can get.";
            else IsExpansionTime(out offer.Reason);

            offer.CanChoose = offer.Reason == null;
            return offer;
        }

        public static string Describe(RestaurantSide side)
        {
            switch (side)
            {
                case RestaurantSide.Left: return "to the left";
                case RestaurantSide.Right: return "to the right";
                default: return "forward";
            }
        }

        // --- The line no zombie crosses -------------------------------------------------------

        /// <summary>
        /// Refuses a zombie any step deeper into the walls' footprint, the way the compound wall
        /// does, so it stops at them and starts hitting them. Staff and visitors are never
        /// stopped: they come and go through the door on routes of their own.
        /// </summary>
        public bool Blocks(Vector3 from, Vector3 to, float radius, MoverKind mover) =>
            mover == MoverKind.Hostile && HasFootprint &&
            MovementBarriers.StepEnters(from, to, new Vector3(footprint.center.x, 0f, footprint.center.y), 0f,
                                        footprint.size * 0.5f, radius);

        /// <summary>For as long as the walls stand: they never fall and never open to a zombie.</summary>
        public bool IsInTheWay => HasFootprint;

        public Vector3 ClosestPoint(Vector3 from) => new Vector3(
            Mathf.Clamp(from.x, footprint.xMin, footprint.xMax), from.y,
            Mathf.Clamp(from.z, footprint.yMin, footprint.yMax));

        /// <summary>
        /// A blow on the walls. It lands - the scene shows it - but they never come down, so it is
        /// never the blow that breaks them. During an attack it costs the restaurant health, and
        /// the blow that takes the last of it loses the attack.
        /// </summary>
        public bool TakeHit(float damage, Vector3 from)
        {
            if (!HasFootprint) return false;

            BlowsTaken++;
            OnHit?.Invoke(ClosestPoint(from));

            if (!IsUnderAttack || Health <= 0f || damage <= 0f) return false;

            Health = Mathf.Max(0f, Health - damage);
            OnHealthChanged?.Invoke(Health, MaxHealth);

            if (Health <= 0f) OnHealthDepleted?.Invoke();
            return false;
        }

        // --- Health in an attack ----------------------------------------------------------------

        /// <summary>An attack is locked in: the restaurant goes into it at full health.</summary>
        public void BeginEncounter(float maxHealth)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            Health = MaxHealth;
            IsUnderAttack = true;
            OnHealthChanged?.Invoke(Health, MaxHealth);
        }

        /// <summary>The attack is over. The health stays where it ended until the crew patches it up.</summary>
        public void EndEncounter() => IsUnderAttack = false;

        /// <summary>The crew's work in the morning, a little at a time.</summary>
        public void Restore(float amount)
        {
            if (amount <= 0f || !IsDamaged) return;

            Health = Mathf.Min(MaxHealth, Health + amount);
            OnHealthChanged?.Invoke(Health, MaxHealth);
        }

        public void RestoreFully() => Restore(MaxHealth);

        // --- Geometry -------------------------------------------------------------------------

        /// <summary>Flat distance from a point to a rectangle: zero on or inside it.</summary>
        public static float DistanceTo(Rect area, Vector3 point)
        {
            float dx = Mathf.Max(area.xMin - point.x, 0f, point.x - area.xMax);
            float dz = Mathf.Max(area.yMin - point.z, 0f, point.z - area.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The strip of ground a rectangle takes in growing <paramref name="depth"/> metres
        /// in an axis direction.</summary>
        public static Rect Strip(Rect area, Vector2 direction, float depth)
        {
            direction = Snap(direction);

            if (direction.x < -0.5f) return Rect.MinMaxRect(area.xMin - depth, area.yMin, area.xMin, area.yMax);
            if (direction.x > 0.5f) return Rect.MinMaxRect(area.xMax, area.yMin, area.xMax + depth, area.yMax);
            if (direction.y > 0.5f) return Rect.MinMaxRect(area.xMin, area.yMax, area.xMax, area.yMax + depth);

            return Rect.MinMaxRect(area.xMin, area.yMin - depth, area.xMax, area.yMin);
        }

        /// <summary>The smallest rectangle round both.</summary>
        public static Rect Grow(Rect area, Rect strip) =>
            Rect.MinMaxRect(Mathf.Min(area.xMin, strip.xMin), Mathf.Min(area.yMin, strip.yMin),
                            Mathf.Max(area.xMax, strip.xMax), Mathf.Max(area.yMax, strip.yMax));

        /// <summary>
        /// True when a turned box - a wall section, a parking bay - overlaps a square-to-the-world
        /// rectangle, by separating axes. <paramref name="yaw"/> in degrees about Y, as a
        /// transform's; <paramref name="half"/> along its own X and Z.
        /// </summary>
        public static bool Overlaps(Rect area, Vector3 centre, float yaw, Vector2 half)
        {
            float radians = yaw * Mathf.Deg2Rad;

            // The box's own X and Z, flat, as a transform turned by that yaw points them.
            var axisX = new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
            var axisZ = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));

            var c = new Vector2(centre.x, centre.z);
            Vector2 areaHalf = area.size * 0.5f;
            Vector2 d = c - area.center;

            // The rectangle's two axes.
            float reachX = Mathf.Abs(axisX.x) * half.x + Mathf.Abs(axisZ.x) * half.y;
            if (Mathf.Abs(d.x) > areaHalf.x + reachX) return false;

            float reachY = Mathf.Abs(axisX.y) * half.x + Mathf.Abs(axisZ.y) * half.y;
            if (Mathf.Abs(d.y) > areaHalf.y + reachY) return false;

            // The box's two axes.
            float areaOnX = Mathf.Abs(axisX.x) * areaHalf.x + Mathf.Abs(axisX.y) * areaHalf.y;
            if (Mathf.Abs(Vector2.Dot(d, axisX)) > half.x + areaOnX) return false;

            float areaOnZ = Mathf.Abs(axisZ.x) * areaHalf.x + Mathf.Abs(axisZ.y) * areaHalf.y;
            if (Mathf.Abs(Vector2.Dot(d, axisZ)) > half.y + areaOnZ) return false;

            return true;
        }

        static Vector2 Snap(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return Vector2.down;

            return Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0f)
                : new Vector2(0f, Mathf.Sign(direction.y));
        }

        static int Index(RestaurantSide side) => Mathf.Clamp((int)side, 0, 2);
    }
}
