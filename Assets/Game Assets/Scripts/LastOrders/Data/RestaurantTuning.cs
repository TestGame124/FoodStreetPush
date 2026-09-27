using System;
using LastOrders.Jobs;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// The three ways the restaurant can grow, named from the player's side of it: Forward is
    /// out through the front wall, toward the door and the gate; Left and Right are the side
    /// walls as seen from the front. The back wall faces the kitchen and does not move.
    /// </summary>
    public enum RestaurantSide
    {
        Forward = 0,
        Left = 1,
        Right = 2,
    }

    /// <summary>How far one side moves each time it is expanded, and how many times it can be.</summary>
    [Serializable]
    public sealed class RestaurantSideTuning
    {
        public RestaurantSide side;

        [Tooltip("Metres the wall moves out each time.")]
        [Min(0.5f)] public float depth = 3f;

        [Tooltip("How many times this side can be expanded. Zero keeps the wall where it is.")]
        [Min(0)] public int maxSteps = 2;

        public RestaurantSideTuning() { }

        public RestaurantSideTuning(RestaurantSide side, float depth, int maxSteps)
        {
            this.side = side;
            this.depth = depth;
            this.maxSteps = maxSteps;
        }
    }

    /// <summary>
    /// The restaurant building itself: how close a zombie can come to its walls, and what
    /// growing it costs.
    /// </summary>
    [Serializable]
    public sealed class RestaurantTuning
    {
        [Header("The inner wall")]
        [Tooltip("A zombie that comes this close to the restaurant's walls, in metres, dies there. " +
                 "The compound wall is the one they batter; this one they never get through.")]
        [Min(0f)] public float killDistance = 1f;

        [Header("Expanding")]
        [Tooltip("Out through the front wall. Short by default: the car park's north bays start " +
                 "a little under two metres in front of it.")]
        public RestaurantSideTuning forward = new RestaurantSideTuning(RestaurantSide.Forward, 1.5f, 2);

        public RestaurantSideTuning left = new RestaurantSideTuning(RestaurantSide.Left, 3f, 2);

        public RestaurantSideTuning right = new RestaurantSideTuning(RestaurantSide.Right, 3f, 2);

        [Tooltip("What the first expansion costs, on any side. Each one after costs Cost Growth " +
                 "times the one before.")]
        public Price firstExpansionCost = new Price(60,
            new ResourceAmount(ResourceType.Wood, 15),
            new ResourceAmount(ResourceType.Scrap, 10));

        [Min(1f)] public float costGrowth = 1.5f;

        [Tooltip("Seconds the walls take to slide out to their new line.")]
        [Min(0f)] public float buildSeconds = 0.8f;

        public RestaurantSideTuning For(RestaurantSide side)
        {
            switch (side)
            {
                case RestaurantSide.Left: return left;
                case RestaurantSide.Right: return right;
                default: return forward;
            }
        }
    }

    /// <summary>Who fixes what the night broke, how long it takes them, and what it costs.</summary>
    [Serializable]
    public sealed class RepairTuning
    {
        [Tooltip("Who is sent to do repairs. 6 gives the Helper the repair and building work.")]
        public WorkerRole role = WorkerRole.Helper;

        [Tooltip("Seconds of work on anything, once the worker is standing at it.")]
        [Min(0.1f)] public float baseSeconds = 3f;

        [Tooltip("Added on top, in proportion to the health missing: all of it for something at zero.")]
        [Min(0f)] public float secondsForFullDamage = 7f;

        [Tooltip("Added again for something that was knocked down rather than only hit.")]
        [Min(0f)] public float extraSecondsWhenDestroyed = 4f;

        [Tooltip("What rebuilding something from nothing costs. Something only damaged pays its " +
                 "share of this for the health it is missing. Free when left empty.")]
        public Price fullRepairCost;

        [Tooltip("Rebuild every wall and gate by magic at dawn, as the game used to. Off, a worker " +
                 "has to go and fix each one.")]
        public bool autoRepairAtDawn;
    }
}
