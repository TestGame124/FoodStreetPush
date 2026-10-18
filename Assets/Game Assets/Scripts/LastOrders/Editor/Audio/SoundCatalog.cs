using System.Collections.Generic;
using LastOrders.Audio;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// What every <see cref="SoundId"/> is made of, out of the pack in Assets/Game Assets/Audio:
    /// which takes, on which bus, how loud, how far it carries, how often it may repeat.
    ///
    /// The pack is a cooking game's, so a good deal here is a stand-in chosen for how it sounds
    /// rather than what it was recorded as - a cabinet door for the compound gate, a crack for a
    /// rifle, tongs clinking for a coin. It has nothing for engines or animals, so the cars only
    /// slam their doors and the cows and hens stay quiet.
    ///
    /// Read once, by <see cref="SoundSetup"/>, to make each cue the first time. After that the cue
    /// asset is the truth and is never overwritten, so tuning one by hand sticks; delete it to have
    /// it made again from here.
    ///
    /// A take is named by its folder under the pack and its file name with the pack's
    /// "Cooking_Game_" prefix left off, and * matches anything: "Chop/Action_Chop_*".
    /// </summary>
    internal static class SoundCatalog
    {
        internal sealed class Spec
        {
            public readonly SoundId Id;
            public readonly SoundBus Bus;
            public readonly string[] Takes;

            public float Level = 0.85f;
            public float LevelSpread = 0.12f;
            public Vector2 Pitch = new Vector2(0.95f, 1.05f);
            public bool Spatial;
            public bool Loop;
            public float Cooldown;
            public int MaxVoices = 4;
            public int Priority = 128;
            public float MinDistance = 14f;
            public float MaxDistance = 50f;

            public Spec(SoundId id, SoundBus bus, string[] takes)
            {
                Id = id;
                Bus = bus;
                Takes = takes;
                Spatial = bus == SoundBus.Sfx || bus == SoundBus.Voice;
                if (bus == SoundBus.Ui) Priority = 64;
            }

            public Spec Vol(float level, float spread = 0.12f) { Level = level; LevelSpread = spread; return this; }
            public Spec Pitched(float low, float high) { Pitch = new Vector2(low, high); return this; }
            public Spec Flat() { Spatial = false; return this; }
            public Spec Limit(float cooldown, int voices = 4) { Cooldown = cooldown; MaxVoices = voices; return this; }
            public Spec Important(int priority) { Priority = priority; return this; }
            public Spec Reach(float min, float max) { MinDistance = min; MaxDistance = max; return this; }

            public Spec Looping()
            {
                Loop = true;
                LevelSpread = 0f;
                Pitch = new Vector2(Pitch.x, Pitch.x);
                MaxVoices = Mathf.Max(MaxVoices, 6);
                Priority = Mathf.Min(Priority, 40);
                return this;
            }
        }

        static Spec Sfx(SoundId id, params string[] takes) => new Spec(id, SoundBus.Sfx, takes);
        static Spec Ui(SoundId id, params string[] takes) => new Spec(id, SoundBus.Ui, takes);
        static Spec Voice(SoundId id, params string[] takes) => new Spec(id, SoundBus.Voice, takes);

        static Spec Bed(SoundId id, string take) =>
            new Spec(id, SoundBus.Ambience, new[] { take }).Vol(0.9f, 0f).Pitched(1f, 1f).Looping().Important(0);

        internal static IReadOnlyList<Spec> All => all ?? (all = Build());

        static List<Spec> all;

        static List<Spec> Build() => new List<Spec>
        {
            // --- The interface ---------------------------------------------------------------------

            Ui(SoundId.UiTap, "UI/UI_Menu_Click_*").Vol(0.55f).Pitched(0.97f, 1.06f).Limit(0.03f, 3),
            Ui(SoundId.UiConfirm, "UI/UI_Menu_Forward_*").Vol(0.6f).Limit(0.05f, 2),
            Ui(SoundId.UiBack, "UI/UI_Menu_Click_*").Vol(0.5f).Pitched(0.8f, 0.88f).Limit(0.05f, 2),
            Ui(SoundId.UiPopupOpen, "Ingredient_Pickup/Ingredient_Pickup_Swish_*").Vol(0.35f).Pitched(1.05f, 1.15f).Limit(0.1f, 2),
            Ui(SoundId.UiPopupClose, "Ingredient_Pickup/Ingredient_Pickup_Swish_*").Vol(0.28f).Pitched(0.85f, 0.92f).Limit(0.1f, 2),
            Ui(SoundId.UiDenied, "Alerts/Alert_Buzz_01").Vol(0.35f).Pitched(1.05f, 1.15f).Limit(0.25f, 1),
            Ui(SoundId.UiToastInfo, "UI/UI_Chime_*").Vol(0.3f).Limit(0.4f, 1),
            Ui(SoundId.UiToastGood, "UI/UI_Dish_Level_Up_*").Vol(0.4f).Limit(0.4f, 1),
            Ui(SoundId.UiToastWarning, "UI/UI_Alert_*").Vol(0.42f).Limit(0.6f, 1),
            Ui(SoundId.UiToastDanger, "Alerts/Alert_Bad_Dish_*").Vol(0.48f).Limit(0.8f, 1),
            Ui(SoundId.UiLevelUp, "UI/UI_Objective_Complete_*").Vol(0.8f, 0f).Pitched(1f, 1f).Limit(1f, 1).Important(8),
            Ui(SoundId.UiConfetti, "Cooking_Power_Ups/Power_Up_Wind_Shimmer_01").Vol(0.45f).Limit(0.5f, 1),
            Ui(SoundId.UiCoinLand, "General Utensils/Small_Metal_Tongs_Clink_*").Vol(0.3f).Pitched(1.4f, 1.65f).Limit(0.05f, 3),
            Ui(SoundId.UiRewardLaunch, "Ingredient_Pickup/Ingredient_Pickup_Zip_A_*").Vol(0.28f).Limit(0.15f, 2),
            Ui(SoundId.UiOrderDelivered, "UI/UI_Dish_Level_Up_*").Vol(0.6f).Limit(0.2f, 2),
            Ui(SoundId.UiDragPick, "Ingredient_Pickup/Ingredient_Pickup_Pop_*").Vol(0.45f).Limit(0.05f, 2),
            Ui(SoundId.UiDragDrop, "Ingredient_Actions/Ingredient_Actions_Place_*").Vol(0.55f).Limit(0.05f, 2),
            Ui(SoundId.UiDragMiss, "UI/UI_Dish_Level_Down_*").Vol(0.32f).Limit(0.2f, 1),

            // --- Discovery sites -------------------------------------------------------------------

            Ui(SoundId.Discovery, "Cooking_Power_Ups/Power_Up_Transform_01").Vol(0.8f, 0f).Pitched(1f, 1f).Limit(1f, 1).Important(8),
            Ui(SoundId.TakeawayArriving, "UI/UI_Chime_*").Vol(0.25f).Pitched(0.88f, 0.92f).Limit(1f, 1),
            Ui(SoundId.TakeawayServed, "Cooking_Items/Aluminum_Foil_Crinkle_Move_1", "Cooking_Items/Parchment_Paper_Rip_Roll_*").Vol(0.45f).Limit(0.3f, 1),
            Voice(SoundId.TakeawayGaveUp, "Emotes/Emotes_Frustrated_*").Flat().Vol(0.5f).Limit(1f, 1),
            Sfx(SoundId.HatchOpen, "Open_Close/Microwave_Open_*").Vol(0.75f).Limit(0.2f, 2),
            Sfx(SoundId.HatchClose, "Open_Close/Microwave_Close_*").Vol(0.75f).Limit(0.2f, 2),
            Ui(SoundId.FoodTruckSent, "Cooking_Power_Ups/Power_Up_Speed_01").Vol(0.55f).Limit(1f, 1),
            Ui(SoundId.FoodTruckReturned, "UI/UI_Objective_Complete_*").Vol(0.5f).Limit(1f, 1),
            Ui(SoundId.RangeStart, "Cooking_Abilities/Abilities_Rush_01").Vol(0.55f).Limit(1f, 1),
            Sfx(SoundId.RangeTargetUp, "Ingredient_Actions/Ingredient_Actions_Flip_0*").Vol(0.45f).Limit(0.05f, 3),
            Sfx(SoundId.RangeTargetHit, "General Utensils/Metal_Tongs_Hit_Glass_Bottle_*").Vol(0.8f).Pitched(1f, 1.15f).Limit(0.04f, 3),
            Ui(SoundId.RangeMedal, "UI/UI_Objective_Complete_*").Vol(0.75f).Limit(1f, 1).Important(20),
            Ui(SoundId.RangeNoMedal, "UI/UI_Dish_Level_Down_*").Vol(0.45f).Limit(1f, 1),
            Sfx(SoundId.NoiseMakerWail, "Alerts/Alert_Alarm_Ascend_0*").Vol(0.8f).Looping().Reach(18f, 75f).Limit(0f, 1),

            // --- The kitchen -----------------------------------------------------------------------

            Sfx(SoundId.CookStart, "On_Off/Stovetop_Burner_On_Off_Click_Flame_*").Vol(0.65f).Limit(0.1f, 2),
            Sfx(SoundId.CookDone, "Ingredient_Actions/Ingredient_Actions_Upgrade_*").Vol(0.65f).Limit(0.1f, 2),
            Sfx(SoundId.GrillSizzle, "Ambience_Loops/Ambience_Frying_Loop").Vol(0.5f).Looping().Reach(8f, 32f),
            Sfx(SoundId.GriddleSizzle, "Ambience_Loops/Ambience_Frying_Loop").Vol(0.42f).Pitched(1.08f, 1.08f).Looping().Reach(8f, 32f),
            Sfx(SoundId.SoupBoil, "Ambience_Loops/Ambience_Boiling_Loop").Vol(0.5f).Looping().Reach(8f, 32f),
            Sfx(SoundId.OvenRoar, "Ambience_Loops/Ambience_Gas_Loop").Vol(0.4f).Looping().Reach(8f, 32f),
            Sfx(SoundId.FridgeHum, "Ambience_Loops/Ambience_Mini_Fridge_Tone_1").Vol(0.3f).Looping().Reach(4f, 20f),
            Sfx(SoundId.ToolChop, "Chop/Action_Chop_*").Vol(0.42f).Limit(0.08f, 3).Reach(10f, 36f),
            Sfx(SoundId.ToolWhisk, "Organic_Actions/Actions_Egg_Beat_*").Vol(0.38f).Limit(0.15f, 2).Reach(10f, 36f),
            Sfx(SoundId.ToolRoll, "Ingredient_Actions/Ingredient_Actions_Swipe_01").Vol(0.32f).Limit(0.2f, 2).Reach(10f, 36f),
            Sfx(SoundId.ToolSaw, "Organic_Actions/Actions_Slice_Bread_Thick_*").Vol(0.38f).Limit(0.15f, 2).Reach(10f, 36f),
            Sfx(SoundId.ToolKnead, "Organic_Actions/Actions_Squish_Vegetable_*", "Mouth/Action_Squish_Food_*").Vol(0.32f).Limit(0.15f, 2).Reach(10f, 36f),
            Sfx(SoundId.FoodFlip, "Ingredient_Actions/Ingredient_Actions_Flip_0*", "Cooking_Abilities/Abilities_Flip_*").Vol(0.42f).Limit(0.1f, 2).Reach(10f, 36f),
            Sfx(SoundId.FatFlare, "Organic_Actions/Actions_Frying_Intro_*").Vol(0.38f).Limit(0.5f, 2).Reach(10f, 36f),
            Sfx(SoundId.SaladToss, "Ingredient_Actions/Ingredient_Actions_Toss_Fast_01", "Ingredient_Actions/Ingredient_Actions_Mix_01").Vol(0.38f).Limit(0.2f, 2).Reach(10f, 36f),
            Sfx(SoundId.DressingPour, "Organic_Actions/Actions_Liquid_Pour_*").Vol(0.32f).Limit(1f, 1).Reach(10f, 36f),
            Sfx(SoundId.LidRattle, "Pots & Pans/Set_Down_Metal_Pot_Top_Stovetop_*").Vol(0.3f).Limit(0.6f, 1).Reach(10f, 36f),
            Sfx(SoundId.DishOnPass, "Plates & Bowls/Set_Down_Small_Ceramic_Plate_Cabinet_*").Vol(0.5f).Limit(0.06f, 3),
            Sfx(SoundId.DishLifted, "Plates & Bowls/Pick_Up_Small_Ceramic_Plate_Cabinet_*").Vol(0.45f).Limit(0.06f, 3),
            Sfx(SoundId.DishServed, "Plates & Bowls/Set_Down_Large_Ceramic_Plate_Cabinet_*").Vol(0.55f).Limit(0.06f, 3),
            Sfx(SoundId.ItemPop, "Ingredient_Pickup/Ingredient_Pickup_Pop_*").Vol(0.42f).Limit(0.05f, 3),
            Sfx(SoundId.ItemCollect, "Ingredient_Pickup/Ingredient_Pickup_Zip_B_*").Vol(0.38f).Limit(0.05f, 3),

            // --- The dining room -------------------------------------------------------------------

            Sfx(SoundId.DoorSwing, "Open_Close/Medium_Wood_Cabinet_Plastic_Stopper_Rattle_Open_*").Vol(0.42f).Pitched(0.85f, 0.95f).Limit(0.3f, 2),
            Sfx(SoundId.DoorShut, "Open_Close/Wood_Cabinet_Plastic_Stopper_Close_*").Vol(0.38f).Pitched(0.85f, 0.95f).Limit(0.3f, 2),
            Sfx(SoundId.CarDoor, "Open_Close/Oven_Close_*").Vol(0.5f).Pitched(0.85f, 0.95f).Limit(0.15f, 2),
            Sfx(SoundId.ChairOut, "Open_Close/Small_Wood_Utility_Drawer_Slide_Open_*").Vol(0.3f).Pitched(0.8f, 0.9f).Limit(0.2f, 2).Reach(8f, 30f),
            Sfx(SoundId.ChairIn, "Open_Close/Small_Wood_Utility_Drawer_Slide_Close_*").Vol(0.3f).Pitched(0.8f, 0.9f).Limit(0.2f, 2).Reach(8f, 30f),
            Sfx(SoundId.OrderBubble, "Ingredient_Pickup/Ingredient_Pickup_Pop_*").Vol(0.35f).Pitched(1.1f, 1.2f).Limit(0.1f, 2),
            Sfx(SoundId.OrderReady, "UI/UI_Chime_*").Vol(0.35f).Limit(0.3f, 2),
            Sfx(SoundId.OrderShort, "UI/UI_Dish_Level_Down_*").Vol(0.35f).Limit(0.3f, 1),
            Sfx(SoundId.Chew, "Mouth/Action_Chew_Single_*").Vol(0.25f).Limit(0.25f, 2).Reach(6f, 24f),
            Voice(SoundId.VisitorDelighted, "Emotes/Emotes_Customer_0*").Vol(0.6f).Pitched(1f, 1.12f).Limit(0.8f, 2),
            Voice(SoundId.VisitorHappy, "Emotes/Emotes_Customer_0*").Vol(0.5f).Pitched(0.95f, 1.08f).Limit(0.8f, 2),
            Voice(SoundId.VisitorContent, "Emotes/Emotes_Chatter_0*").Vol(0.42f).Limit(0.8f, 2),
            Voice(SoundId.VisitorFrustrated, "Emotes/Emotes_Frustrated_0*").Vol(0.5f).Limit(0.8f, 2),
            Sfx(SoundId.TableWipe, "Ingredient_Actions/Ingredient_Actions_Swipe_01", "Mouth/Action_Swish_Liquid_Short_01").Vol(0.38f).Limit(0.2f, 2),
            Sfx(SoundId.CrewPickUp, "Ingredient_Pickup/Ingredient_Pickup_Zip_A_*").Vol(0.45f).Limit(0.1f, 1),
            Sfx(SoundId.CrewDrop, "Ingredient_Actions/Ingredient_Actions_Place_*").Vol(0.5f).Pitched(0.85f, 0.95f).Limit(0.1f, 1),
            Sfx(SoundId.DinerChatter, "Ambience_Loops/Ambience_Restaurant").Vol(0.45f).Looping().Reach(10f, 42f).Limit(0f, 1),
            Voice(SoundId.CrewCheer, "Emotes/Emotes_Chef_A_0*", "Emotes/Emotes_Chef_B_0*").Vol(0.6f).Limit(0.8f, 1),

            // --- Building and clearing -------------------------------------------------------------

            Sfx(SoundId.Place, "Cooking_Abilities/Abilities_Earth_Smash_0*").Vol(0.42f).Pitched(1.1f, 1.25f).Limit(0.05f, 2),
            Sfx(SoundId.PlaceFloor, "Ingredient_Actions/Ingredient_Actions_Place_*").Vol(0.28f).Limit(0.04f, 3),
            Sfx(SoundId.PlaceMoved, "Ingredient_Actions/Ingredient_Actions_Lock_01").Vol(0.5f).Limit(0.05f, 2),
            Ui(SoundId.PlaceRefused, "Alerts/Alert_Buzz_01").Vol(0.38f).Limit(0.3f, 1),
            Ui(SoundId.PickUpObject, "Ingredient_Pickup/Ingredient_Pickup_Zip_B_*").Vol(0.38f).Limit(0.1f, 1),
            Ui(SoundId.Sell, "UI/UI_Meter_Drain_*").Vol(0.45f).Limit(0.2f, 1),
            Sfx(SoundId.WorkChop, "Organic_Actions/Actions_Veggie_Hard_Break_*").Vol(0.38f).Limit(0.06f, 3),
            Sfx(SoundId.WorkHammer, "Chop/Action_Chop_B_*").Vol(0.4f).Pitched(0.72f, 0.82f).Limit(0.06f, 3),
            Sfx(SoundId.DebrisCleared, "Cooking_Items/Item_Break_Wood_*").Vol(0.7f).Limit(0.1f, 2),
            Sfx(SoundId.ExpansionSection, "Cooking_Abilities/Abilities_Earth_Smash_0*").Vol(0.28f).Pitched(1.2f, 1.35f).Limit(0.06f, 3),
            Ui(SoundId.ExpansionClaimed, "Cooking_Power_Ups/Power_Up_Dish_Grow_*").Vol(0.75f).Limit(1f, 1).Important(10),
            Sfx(SoundId.BuildDone, "Cooking_Abilities/Abilities_Earth_Smash_0*").Vol(0.6f).Limit(0.1f, 2),
            Sfx(SoundId.RepairDone, "Ingredient_Actions/Ingredient_Actions_Upgrade_*").Vol(0.6f).Limit(0.1f, 2),
            Ui(SoundId.RepairAll, "Cooking_Power_Ups/Power_Up_0*").Vol(0.6f).Limit(1f, 1),

            // --- The farm --------------------------------------------------------------------------

            Sfx(SoundId.SeedPlant, "Ingredient_Pickup/Ingredient_Pickup_Squish_*").Vol(0.38f).Limit(0.04f, 3),
            Sfx(SoundId.CropReady, "Cooking_Power_Ups/Power_Up_Dish_Grow_*").Vol(0.32f).Limit(0.3f, 2),
            Sfx(SoundId.Harvest, "Organic_Actions/Actions_Veggie_Crunch_01", "Ingredient_Pickup/Ingredient_Pickup_Pop_*").Vol(0.5f).Limit(0.04f, 3),
            Sfx(SoundId.FeedDrop, "Organic_Actions/Actions_Spice_Shake_A_*").Vol(0.45f).Limit(0.06f, 3),
            Sfx(SoundId.ProduceReady, "UI/UI_Chime_*").Vol(0.32f).Pitched(1.05f, 1.15f).Limit(0.5f, 2),
            Sfx(SoundId.ProduceCollect, "Ingredient_Pickup/Ingredient_Pickup_Pop_*").Vol(0.5f).Limit(0.05f, 2),

            // --- The siege -------------------------------------------------------------------------

            Sfx(SoundId.ZombieGroan, "zombies/zombie-*").Vol(0.7f).Pitched(0.85f, 1.05f).Limit(0.25f, 3).Reach(10f, 45f).Important(140),
            Sfx(SoundId.ZombieHurt, "Mouth/Action_Squelch_0*").Vol(0.45f).Limit(0.06f, 3),
            Sfx(SoundId.ZombieDeath, "zombies/zombie-*").Vol(0.8f).Pitched(0.62f, 0.78f).Limit(0.1f, 3).Important(110),
            Sfx(SoundId.ZombieBlow, "Cooking_Abilities/Abilities_Punch_0*").Vol(0.5f).Pitched(0.85f, 1f).Limit(0.12f, 3),
            Sfx(SoundId.ShotFood, "Ingredient_Pickup/Ingredient_Pickup_Pop_*").Vol(0.62f).Pitched(0.72f, 0.88f).Limit(0.04f, 4),
            Sfx(SoundId.ShotHeavy, "Cooking_Abilities/Abilities_Crack_Open_*", "Cooking_Abilities/Abilities_Punch_Big_01").Vol(0.55f).Pitched(0.82f, 0.95f).Limit(0.05f, 3),
            Sfx(SoundId.ShotThrow, "Cooking_Abilities/Abilities_Zip_01", "Ingredient_Actions/Ingredient_Actions_Toss_Fast_01").Vol(0.6f).Limit(0.05f, 3),
            Sfx(SoundId.ShotRifle, "Cooking_Abilities/Abilities_Crack_Open_*").Vol(0.55f).Pitched(1.1f, 1.25f).Limit(0.05f, 4),
            Sfx(SoundId.ImpactSplat, "Organic_Actions/Actions_Squish_Vegetable_*", "Mouth/Action_Squish_Food_*").Vol(0.52f).Limit(0.04f, 4),
            Sfx(SoundId.ImpactSmash, "Cooking_Items/Item_Break_Ceramic_*").Vol(0.5f).Limit(0.05f, 3),
            Sfx(SoundId.ImpactToast, "Cooking_Abilities/Abilities_Ignite_01", "Organic_Actions/Actions_Frying_Intro_*").Vol(0.5f).Limit(0.05f, 3),
            Sfx(SoundId.StructureBreak, "Cooking_Items/Item_Break_Wood_*").Vol(0.85f).Pitched(0.85f, 1f).Limit(0.08f, 3).Reach(16f, 60f).Important(60),
            Sfx(SoundId.GateOpen, "Open_Close/Large_Wood_Silverware_Drawer_Slide_Open_*").Vol(0.62f).Pitched(0.62f, 0.72f).Limit(0.3f, 2).Reach(12f, 45f),
            Sfx(SoundId.GateClose, "Open_Close/Large_Wood_Silverware_Drawer_Slide_Close_*").Vol(0.68f).Pitched(0.62f, 0.72f).Limit(0.3f, 2).Reach(12f, 45f),
            Sfx(SoundId.TrapSnap, "General Utensils/Meat_Scissors_Snip_Click_*", "General Utensils/Large_Metal_Tongs_Clink_*").Vol(0.62f).Pitched(0.75f, 0.85f).Limit(0.06f, 3),
            Sfx(SoundId.FireIgnite, "Cooking_Abilities/Abilities_Ignite_01").Vol(0.7f).Limit(0.2f, 2),
            Sfx(SoundId.FireCrackle, "Ambience_Loops/Ambience_Frying_Loop").Vol(0.5f).Pitched(0.7f, 0.7f).Looping().Reach(10f, 40f),
            Sfx(SoundId.FireOut, "Water_Ice/Pour_Out_Water_Medium_Pot_Sink_*").Vol(0.45f).Limit(0.3f, 2),
            Voice(SoundId.WorkerHurt, "Emotes/Emotes_Distressed_0*").Vol(0.55f).Limit(1f, 2),
            Voice(SoundId.WorkerDowned, "Emotes/Emotes_Distressed_0*").Vol(0.8f).Pitched(0.85f, 0.92f).Limit(0.5f, 2).Important(40),
            Sfx(SoundId.CrisisAbility, "Cooking_Abilities/Abilities_Earth_Smash_0*", "Cooking_Abilities/Abilities_Punch_Big_01").Vol(0.85f).Limit(0.1f, 2).Reach(20f, 70f).Important(30),
            Sfx(SoundId.DefenseBuilt, "Cooking_Abilities/Abilities_Earth_Smash_0*").Vol(0.62f).Limit(0.1f, 2),
            Sfx(SoundId.DefenseUpgraded, "Ingredient_Actions/Ingredient_Actions_Upgrade_*").Vol(0.7f).Limit(0.1f, 2),
            Ui(SoundId.DefenseWrecked, "Alerts/Alert_Broken_0*").Vol(0.5f).Limit(2f, 1),

            // --- The day ---------------------------------------------------------------------------

            Ui(SoundId.DawnStinger, "Cooking_Power_Ups/Power_Up_Wind_Shimmer_01").Vol(0.6f, 0f).Pitched(1f, 1f).Limit(2f, 1).Important(10),
            Ui(SoundId.DayStinger, "Cooking_Power_Ups/Power_Up_0*").Vol(0.55f, 0f).Pitched(1f, 1f).Limit(2f, 1).Important(10),
            Ui(SoundId.DuskWarning, "Alerts/Alert_Alarm_Ascend_0*").Vol(0.75f, 0f).Pitched(1f, 1f).Limit(2f, 1).Important(5),
            Ui(SoundId.NightStinger, "Alerts/Alert_Alarm_Descend_01").Vol(0.75f, 0f).Pitched(1f, 1f).Limit(2f, 1).Important(5),
            Ui(SoundId.GroupIncoming, "UI/UI_Alert_0*").Vol(0.5f).Limit(1f, 1).Important(20),
            Ui(SoundId.WaveStart, "zombies/zombie-*").Vol(0.8f).Pitched(0.58f, 0.66f).Limit(1f, 1).Important(10),
            Ui(SoundId.Breach, "Alerts/Alert_Penalty_0*").Vol(0.7f, 0f).Limit(3f, 1).Important(5),
            Ui(SoundId.AttackVictory, "UI/UI_Objective_Complete_*").Vol(0.8f, 0f).Pitched(1f, 1f).Limit(2f, 1).Important(5),
            Ui(SoundId.AttackDefeat, "Alerts/Alert_Ruined_01").Vol(0.8f, 0f).Pitched(1f, 1f).Limit(2f, 1).Important(5),
            Ui(SoundId.ZombiesAtDoor, "Alerts/Alert_Buzz_01").Vol(0.55f).Pitched(0.78f, 0.84f).Limit(3f, 1).Important(20),
            Ui(SoundId.IncidentBegan, "UI/UI_Alert_0*").Vol(0.5f).Limit(2f, 1).Important(20),
            Ui(SoundId.DuskTick, "UI/UI_Menu_Click_0*").Vol(0.6f, 0f).Pitched(1.32f, 1.38f).Limit(0.5f, 1).Important(20),

            // --- The beds --------------------------------------------------------------------------

            Bed(SoundId.AmbienceDawn, "Ambience_Loops/Ambience_Forest"),
            Bed(SoundId.AmbienceDay, "Ambience_Loops/Ambience_Farm"),
            Bed(SoundId.AmbienceDusk, "Ambience_Loops/Ambience_Desert"),
            Bed(SoundId.AmbienceNight, "Ambience_Loops/Ambience_Night"),
        };
    }
}
