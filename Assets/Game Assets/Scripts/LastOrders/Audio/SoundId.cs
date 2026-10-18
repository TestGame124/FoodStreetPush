namespace LastOrders.Audio
{
    /// <summary>
    /// Every sound the game asks for by name. What each one actually sounds like is the
    /// <see cref="SoundLibrary"/>'s business; code only ever says which moment it is.
    ///
    /// Numbered in hundreds by area and saved as numbers - in the library, and on any emitter set up
    /// in a scene - so a value must never be reused or renumbered. Add new ones at the end of their
    /// area. An id the library has no cue for is silence.
    /// </summary>
    public enum SoundId
    {
        None = 0,

        // --- The interface -------------------------------------------------------------------------

        UiTap = 100,
        UiConfirm = 101,
        UiBack = 102,
        UiPopupOpen = 103,
        UiPopupClose = 104,
        UiDenied = 105,
        UiToastInfo = 106,
        UiToastGood = 107,
        UiToastWarning = 108,
        UiToastDanger = 109,
        UiLevelUp = 110,
        UiConfetti = 111,
        UiCoinLand = 112,
        UiRewardLaunch = 113,
        UiOrderDelivered = 114,
        UiDragPick = 115,
        UiDragDrop = 116,
        UiDragMiss = 117,

        // --- Discovery sites ------------------------------------------------------------------------

        Discovery = 200,
        TakeawayArriving = 201,
        TakeawayServed = 202,
        TakeawayGaveUp = 203,
        HatchOpen = 204,
        HatchClose = 205,
        FoodTruckSent = 206,
        FoodTruckReturned = 207,
        RangeStart = 208,
        RangeTargetUp = 209,
        RangeTargetHit = 210,
        RangeMedal = 211,
        RangeNoMedal = 212,
        NoiseMakerWail = 213,

        // --- The kitchen ---------------------------------------------------------------------------

        CookStart = 300,
        CookDone = 301,
        GrillSizzle = 302,
        GriddleSizzle = 303,
        SoupBoil = 304,
        OvenRoar = 305,
        FridgeHum = 306,
        ToolChop = 307,
        ToolWhisk = 308,
        ToolRoll = 309,
        ToolSaw = 310,
        ToolKnead = 311,
        FoodFlip = 312,
        FatFlare = 313,
        SaladToss = 314,
        DressingPour = 315,
        LidRattle = 316,
        DishOnPass = 317,
        DishLifted = 318,
        DishServed = 319,
        ItemPop = 320,
        ItemCollect = 321,

        // --- The dining room -----------------------------------------------------------------------

        DoorSwing = 400,
        DoorShut = 401,
        CarDoor = 402,
        ChairOut = 403,
        ChairIn = 404,
        OrderBubble = 405,
        OrderReady = 406,
        OrderShort = 407,
        Chew = 408,
        VisitorDelighted = 409,
        VisitorHappy = 410,
        VisitorContent = 411,
        VisitorFrustrated = 412,
        TableWipe = 413,
        CrewPickUp = 414,
        CrewDrop = 415,
        DinerChatter = 416,
        CrewCheer = 417,

        // --- Building and clearing -----------------------------------------------------------------

        Place = 500,
        PlaceFloor = 501,
        PlaceMoved = 502,
        PlaceRefused = 503,
        PickUpObject = 504,
        Sell = 505,
        WorkChop = 506,
        WorkHammer = 507,
        DebrisCleared = 508,
        ExpansionSection = 509,
        ExpansionClaimed = 510,
        BuildDone = 511,
        RepairDone = 512,
        RepairAll = 513,

        // --- The farm ------------------------------------------------------------------------------

        SeedPlant = 600,
        CropReady = 601,
        Harvest = 602,
        FeedDrop = 603,
        ProduceReady = 604,
        ProduceCollect = 605,

        // --- The siege -----------------------------------------------------------------------------

        ZombieGroan = 700,
        ZombieHurt = 701,
        ZombieDeath = 702,
        ZombieBlow = 703,
        ShotFood = 704,
        ShotHeavy = 705,
        ShotThrow = 706,
        ShotRifle = 707,
        ImpactSplat = 708,
        ImpactSmash = 709,
        ImpactToast = 710,
        StructureBreak = 711,
        GateOpen = 712,
        GateClose = 713,
        TrapSnap = 714,
        FireIgnite = 715,
        FireCrackle = 716,
        FireOut = 717,
        WorkerHurt = 718,
        WorkerDowned = 719,
        CrisisAbility = 720,
        DefenseBuilt = 721,
        DefenseUpgraded = 722,
        DefenseWrecked = 723,

        // --- The day -------------------------------------------------------------------------------

        DawnStinger = 800,
        DayStinger = 801,
        DuskWarning = 802,
        NightStinger = 803,
        GroupIncoming = 804,
        WaveStart = 805,
        Breach = 806,
        AttackVictory = 807,
        AttackDefeat = 808,
        ZombiesAtDoor = 809,
        IncidentBegan = 810,
        DuskTick = 811,

        // --- The beds ------------------------------------------------------------------------------

        AmbienceDawn = 900,
        AmbienceDay = 901,
        AmbienceDusk = 902,
        AmbienceNight = 903,
    }
}
