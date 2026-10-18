using System;
using UnityEngine;

namespace LastOrders.Data
{
    /// <summary>
    /// The four places found out on the land the restaurant expands into, and what each one gives
    /// the player to do once its plot is claimed: a drive-through window, a food truck, a shooting
    /// range and a noise maker. Everything a designer would tune about them, in one block of the
    /// balance asset.
    /// </summary>
    [Serializable]
    public sealed class DiscoveryTuning
    {
        public TakeawayTuning takeaway = new TakeawayTuning();
        public FoodTruckTuning foodTruck = new FoodTruckTuning();
        public ShootingRangeTuning range = new ShootingRangeTuning();
        public NoiseMakerTuning noiseMaker = new NoiseMakerTuning();
    }

    /// <summary>
    /// The drive-through: customers who stay in their cars, a bag each, filled from what the
    /// kitchen has already cooked and handed out of the window.
    /// </summary>
    [Serializable]
    public sealed class TakeawayTuning
    {
        [Tooltip("Cars that can wait in the lane at once, the one at the window included.")]
        [Range(1, 4)] public int laneLength = 3;

        [Tooltip("Seconds between one car turning in and the next, picked between the two.")]
        public Vector2 arrivalSeconds = new Vector2(22f, 40f);

        [Tooltip("Seconds a car waits in the lane for its bag before it gives up and drives off.")]
        [Min(5f)] public float patienceSeconds = 75f;

        [Tooltip("Dishes in one bag, picked between the two.")]
        public Vector2Int dishesPerBag = new Vector2Int(1, 3);

        [Tooltip("What a takeaway dish pays, against its price in the dining room.")]
        [Range(0.5f, 2f)] public float priceMultiplier = 1f;

        [Tooltip("Extra share of the bill for a bag handed over while the car still has more than half its patience.")]
        [Range(0f, 1f)] public float quickTip = 0.25f;

        [Tooltip("Experience for every bag handed over.")]
        [Min(0)] public int experiencePerBag = 1;

        [Tooltip("Chance each dish in a bag is one the kitchen can make but has none of on the pass, " +
                 "rather than one that is ready now. What makes the player cook for the window.")]
        [Range(0f, 1f)] public float cookToOrderChance = 0.35f;
    }

    /// <summary>One place the food truck can drive out to, and what sells there.</summary>
    [Serializable]
    public sealed class FoodTruckDestination
    {
        [Tooltip("Stable identity, for the popup and the log.")]
        public string id = "destination";

        public string displayName = "Destination";

        [Tooltip("One line about who is there, shown on the map.")]
        public string blurb;

        [Tooltip("Dishes the people there pay extra for.")]
        public ItemType[] favourites = new ItemType[0];

        [Tooltip("Seconds from the truck leaving to it being back with the takings.")]
        [Min(5f)] public float tripSeconds = 60f;

        [Tooltip("What a favourite dish sells for there, against its dining-room price.")]
        [Min(0f)] public float favouriteMultiplier = 1.6f;

        [Tooltip("What any other dish sells for there, against its dining-room price.")]
        [Min(0f)] public float otherMultiplier = 0.8f;

        [Tooltip("Which way along the road the truck drives to get there.")]
        public bool eastward;

        [Tooltip("Where the pin goes on the map in the popup, 0 to 1 across and up.")]
        public Vector2 mapPosition = new Vector2(0.5f, 0.5f);

        public bool IsFavourite(ItemType dish)
        {
            if (favourites == null) return false;
            for (int i = 0; i < favourites.Length; i++)
                if (favourites[i] == dish) return true;
            return false;
        }

        public FoodTruckDestination() { }

        public FoodTruckDestination(string id, string name, string blurb, float seconds, float favourite, float other, bool east,
                                    Vector2 pin, params ItemType[] likes)
        {
            this.id = id;
            displayName = name;
            this.blurb = blurb;
            tripSeconds = seconds;
            favouriteMultiplier = favourite;
            otherMultiplier = other;
            eastward = east;
            mapPosition = pin;
            favourites = likes;
        }
    }

    /// <summary>The food truck: one truck, loaded from the pass and sent to one place on a small map.</summary>
    [Serializable]
    public sealed class FoodTruckTuning
    {
        [Tooltip("Dishes the truck carries at most.")]
        [Range(1, 12)] public int capacity = 6;

        [Tooltip("Experience for every trip that comes back having sold something.")]
        [Min(0)] public int experiencePerTrip = 2;

        [Tooltip("Where it can go. Keep it to a handful: the map is meant to be small.")]
        public FoodTruckDestination[] destinations =
        {
            new FoodTruckDestination("checkpoint", "Survivors' checkpoint", "Guards on long shifts want something hot off the grill.",
                                     45f, 1.5f, 0.8f, false, new Vector2(0.2f, 0.42f),
                                     ItemType.ChickenSkewers, ItemType.GrilledFish, ItemType.GrilledCorn, ItemType.FriedEggs,
                                     ItemType.HashBrowns),
            new FoodTruckDestination("farm", "Hillside farm", "Farmhands trade hard for anything baked.",
                                     60f, 1.6f, 0.8f, true, new Vector2(0.82f, 0.7f),
                                     ItemType.Bread, ItemType.CornBread, ItemType.CustardTart, ItemType.FrenchToast),
            new FoodTruckDestination("school", "School shelter", "Families queue round the yard for soup and salad.",
                                     75f, 1.8f, 0.7f, false, new Vector2(0.3f, 0.82f),
                                     ItemType.TomatoSoup, ItemType.CornChowder, ItemType.GardenSalad, ItemType.SweetcornSalad,
                                     ItemType.CarrotSlaw, ItemType.TomatoCheeseSalad, ItemType.BeanSalad, ItemType.ChickenSalad),
        };
    }

    /// <summary>
    /// The shooting range: a short tap-to-fire round with one of the compound's own defense
    /// machines against cardboard zombies on runners, something to do while the kitchen cooks.
    /// </summary>
    [Serializable]
    public sealed class ShootingRangeTuning
    {
        [Tooltip("Seconds one round lasts.")]
        [Min(5f)] public float roundSeconds = 30f;

        [Tooltip("Seconds after a round before the range can be used again.")]
        [Min(0f)] public float cooldownSeconds = 60f;

        [Tooltip("Seconds a cardboard zombie stays up before it drops back behind the berm.")]
        [Min(0.5f)] public float targetUpSeconds = 2.8f;

        [Tooltip("Seconds between one target dropping and the next one popping up in its lane.")]
        public Vector2 targetDownSeconds = new Vector2(0.5f, 1.4f);

        [Tooltip("Metres per second a target slides along its runner while it is up.")]
        public Vector2 targetSpeed = new Vector2(0.8f, 1.9f);

        [Tooltip("Knock-downs for bronze, silver and gold.")]
        public int[] medals = { 5, 9, 13 };

        [Tooltip("Coins for bronze, silver and gold.")]
        public int[] coins = { 8, 16, 30 };

        [Tooltip("Experience for bronze, silver and gold.")]
        public int[] experience = { 1, 2, 4 };
    }

    /// <summary>
    /// The noise maker from the security hut: set down out past the walls in daylight, sounded in
    /// an attack to pull the zombies near it off their way and onto it, where the defenses can
    /// reach them.
    /// </summary>
    [Serializable]
    public sealed class NoiseMakerTuning
    {
        [Tooltip("Times it can be sounded in one attack.")]
        [Min(1)] public int usesPerAttack = 1;

        [Tooltip("Seconds it wails once sounded.")]
        [Min(1f)] public float soundSeconds = 12f;

        [Tooltip("Metres from it a zombie hears it and turns toward it.")]
        [Min(1f)] public float radius = 14f;

        [Tooltip("How much battering it takes before it falls silent early.")]
        [Min(1f)] public float health = 260f;

        [Tooltip("Metres from the noise maker the zombies gather, so a crowd forms round it rather than on one spot.")]
        [Min(0.2f)] public float gatherRadius = 0.9f;
    }
}
