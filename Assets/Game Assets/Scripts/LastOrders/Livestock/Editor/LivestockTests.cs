#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using LastOrders.Livestock;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The farmyard, without a scene: a pen lays one every interval of working time and keeps what it
/// has laid until collected, never more than it holds, banking no time while full; collecting
/// empties it and starts the next from nothing. The chicken coop and the cow shelter are sold in
/// the shop's Farming tab as things to set down on the grid, each a pen that gives its own item -
/// eggs, milk - with as many eggs or churns to show as it can hold and its animals' ground inside
/// its footprint; and the kitchen has something to make with what they give.
/// </summary>
public class LivestockTests
{
    const string ShopFolder = "Assets/Game Assets/Content/LastOrders/Farming/Shop";
    const string ItemsFolder = "Assets/Game Assets/Scripts/Item/ItemsData";

    // --- The clock ---------------------------------------------------------------------------

    /// <summary>Quarter-second steps: exact in binary.</summary>
    static int Run(LivestockClock clock, float seconds)
    {
        int made = 0;
        int steps = Mathf.RoundToInt(seconds / 0.25f);
        for (int i = 0; i < steps; i++) made += clock.Tick(0.25f);
        return made;
    }

    [Test]
    public void OneIsLaidEveryIntervalOfWorkingTime()
    {
        var clock = new LivestockClock(30f, 6);
        Assert.AreEqual(0, Run(clock, 29.75f), "Nothing before the first interval is up.");
        Assert.AreEqual(1, Run(clock, 0.25f));
        Assert.AreEqual(2, Run(clock, 60f));
        Assert.AreEqual(3, clock.Stored);
    }

    [Test]
    public void APenNeverHoldsMoreThanItsCapacity()
    {
        var clock = new LivestockClock(10f, 3);
        Run(clock, 1000f);
        Assert.AreEqual(3, clock.Stored);
        Assert.IsTrue(clock.Full);
        Assert.AreEqual(0f, clock.SecondsToNext, "A full pen is waiting on nobody but the player.");
    }

    [Test]
    public void TimeSpentFullIsNotBankedTowardTheNextOne()
    {
        var clock = new LivestockClock(10f, 2);
        Run(clock, 500f);
        clock.TakeAll();
        Assert.AreEqual(0, Run(clock, 9.75f), "Emptying a pen that sat full all day does not lay one at once.");
        Assert.AreEqual(1, Run(clock, 0.25f));
    }

    [Test]
    public void CollectingTakesEverythingAndStartsTheNextFromNothing()
    {
        var clock = new LivestockClock(10f, 6);
        Run(clock, 45f);
        Assert.AreEqual(4, clock.TakeAll());
        Assert.AreEqual(0, clock.Stored);
        Assert.AreEqual(0, clock.TakeAll(), "A second collection finds nothing.");
        Assert.AreEqual(5f, clock.SecondsToNext, 1e-4f, "Time toward the next one is kept across a collection.");
    }

    [Test]
    public void AFreshPenCanStartWithSomeAlreadyLaidButNoMoreThanItHolds()
    {
        var clock = new LivestockClock(30f, 3);
        clock.Reset(2);
        Assert.AreEqual(2, clock.Stored);
        clock.Reset(10);
        Assert.AreEqual(3, clock.Stored);
        clock.Reset(-1);
        Assert.AreEqual(0, clock.Stored);
    }

    [Test]
    public void NoTimeOrNegativeTimeLaysNothing()
    {
        var clock = new LivestockClock(1f, 3);
        Assert.AreEqual(0, clock.Tick(0f));
        Assert.AreEqual(0, clock.Tick(-5f));
        Assert.AreEqual(0, clock.Stored);
    }

    // --- Feeding ------------------------------------------------------------------------------

    [Test]
    public void AnEmptyTroughIsHungryAndEachItemIsGoodForItsShare()
    {
        var trough = new FeedTrough(3, 2);
        Assert.IsTrue(trough.Hungry, "Nothing in it to start with.");
        Assert.AreEqual(3, trough.Room);

        Assert.AreEqual(1, trough.Fill(1));
        Assert.IsFalse(trough.Hungry);
        Assert.AreEqual(2, trough.Portions, "One corn is two eggs.");
        Assert.IsTrue(trough.Eat());
        Assert.AreEqual(1, trough.ItemsLeft, "Half eaten still shows as one.");
        Assert.IsTrue(trough.Eat());
        Assert.IsTrue(trough.Hungry);
        Assert.IsFalse(trough.Eat(), "Nothing left to eat.");
    }

    [Test]
    public void ATroughTakesOnlyWhatFits()
    {
        var trough = new FeedTrough(3, 1);
        Assert.AreEqual(3, trough.Fill(10), "Three fit; the rest stay in the storehouse.");
        Assert.AreEqual(0, trough.Room);
        Assert.AreEqual(0, trough.Fill(1));
        trough.Eat();
        Assert.AreEqual(1, trough.Room);

        var hens = new FeedTrough(3, 2);
        hens.Fill(3);
        hens.Eat();
        Assert.AreEqual(0, hens.Room, "Half a corn's room is not room for a whole one.");
    }

    [Test]
    public void NothingIsMadeBeyondWhatTheyWereFedFor()
    {
        var clock = new LivestockClock(10f, 6);
        int made = 0;
        int portions = 2;
        for (int i = 0; i < 400; i++)
        {
            int now = clock.Tick(0.25f, portions);
            made += now;
            portions -= now;
        }

        Assert.AreEqual(2, made, "Fed for two, two are laid.");
        float waiting = clock.SecondsToNext;
        Assert.AreEqual(0, clock.Tick(100f, 0), "An empty trough lays nothing.");
        Assert.AreEqual(waiting, clock.SecondsToNext, 1e-4f, "Nor does time on an empty trough count toward the next.");
        Assert.AreEqual(1, clock.Tick(10f, 1), "Fed again, the next comes in its usual time.");
    }

    [TestCase("Chicken Coop", ItemType.Corn)]
    [TestCase("Cow Shelter", ItemType.Flour)]
    public void EachBuildingEatsItsCropAndShowsItsFeed(string title, ItemType eats)
    {
        var pen = ShopItem(title).prefab.GetComponent<LivestockPen>();
        Assert.IsNotNull(pen.Feed, "Has something to eat.");
        Assert.AreEqual(eats, pen.Feed.Type);
        Assert.IsTrue(pen.Feed.canBePlanted, "Fed on a crop the player grows.");

        var so = new SerializedObject(pen);
        Assert.AreEqual(so.FindProperty("feedCapacity").intValue, so.FindProperty("feedShown").arraySize, "Something to show for every item it holds.");
        Assert.AreEqual(0, so.FindProperty("startingFeed").intValue, "Set down hungry: it has to be fed first.");
        Assert.IsNotNull(so.FindProperty("cardPrefab").objectReferenceValue, "A card to show on a tap.");
        Assert.IsNotNull(so.FindProperty("bubblePrefab").objectReferenceValue, "A bubble over it when it wants the player.");
        Assert.IsNotNull(so.FindProperty("uiAnchor").objectReferenceValue);
    }

    [Test]
    public void TheMostThatCanBeFedIsWhatFitsAndCanBeSpared()
    {
        FeedOffer Offer(int have, int spare, int inTrough) =>
            new FeedOffer("Chicken Coop", "", null, null, "egg", "eggs", "corn", 2, 30f, have, spare, true, inTrough, 3, 0, 6,
                          ProduceProgressView.State.Hungry, "", 0f, 30f);

        Assert.AreEqual(2, Offer(3, 2, 0).Most, "Two to spare, room for three: two.");
        Assert.AreEqual(1, Offer(9, 8, 2).Most, "Plenty to spare, room for one: one.");
        Assert.IsNull(Offer(9, 8, 2).Blocked);

        StringAssert.Contains("full", Offer(9, 8, 3).Blocked);
        StringAssert.Contains("kept for sowing", Offer(1, 0, 0).Blocked, "The last corn is the seed.");
        StringAssert.Contains("grow some", Offer(0, 0, 0).Blocked);

        Assert.AreEqual(4, Offer(3, 2, 0).Makes(2), "Two corn make four eggs.");
        Assert.AreEqual(120f, Offer(3, 2, 0).Takes(2), 1e-4f, "Thirty seconds each.");
    }

    [TestCase("Chicken Coop", ItemType.Corn, 2)]
    [TestCase("Cow Shelter", ItemType.Flour, 1)]
    public void FeedingTakesWhatWasChosenAndNeverTheSeed(string title, ItemType eats, int perFeed)
    {
        ItemDataSO data = ShopItem(title).prefab.GetComponent<LivestockPen>().Feed;
        Assert.AreEqual(eats, data.Type);

        typeof(InventorySystem).GetField("onInventoryChangedCallback",
                                         System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                               ?.SetValue(null, null);

        string path = AssetDatabase.GetAssetPath(ShopItem(title).prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var store = new GameObject("Storehouse");
        InventorySystem before = InventorySystem.instance;
        try
        {
            var inventory = store.AddComponent<InventorySystem>();
            inventory.inventorySize = 10;
            InventorySystem.instance = inventory;
            var pen = root.GetComponent<LivestockPen>();

            Assert.IsTrue(pen.Hungry);
            Assert.IsTrue(pen.WantsFeeding, "Hungry with nothing to give: the popup still says what they need.");
            Assert.IsFalse(pen.GiveFeed(1, null, out string refused), "Nothing in the storehouse.");
            StringAssert.Contains("grow some", refused);

            inventory.AddItem(new Item(data, 1));
            Assert.AreEqual(0, pen.Offer().Most, "One in the storehouse is the seed.");
            Assert.IsFalse(pen.GiveFeed(1, null, out refused));
            StringAssert.Contains("kept for sowing", refused);

            for (int i = 0; i < 4; i++) inventory.AddItem(new Item(data, 1));
            Assert.AreEqual(5, inventory.ItemCount(eats));

            int fedEvents = 0;
            pen.Fed += (_, items) => fedEvents += items;

            Assert.IsTrue(pen.GiveFeed(2, null, out string fed), fed);
            Assert.AreEqual(3, inventory.ItemCount(eats), "Two taken, as chosen - not all that fits.");
            Assert.AreEqual(2, pen.FeedLeft);
            Assert.AreEqual(2, fedEvents, "Fed as the feed lands - at once, out of play.");
            Assert.IsFalse(pen.Hungry);
            StringAssert.Contains($"{2 * perFeed}", fed, "Says how many it makes.");

            FeedOffer offer = pen.Offer();
            Assert.AreEqual(1, offer.Most, "Room for one more.");
            Assert.AreEqual(2, offer.InTrough);
            Assert.AreEqual(3, offer.Have);
            Assert.AreEqual(perFeed, offer.PerFeed);

            Assert.IsTrue(pen.GiveFeed(5, null, out fed));
            Assert.AreEqual(2, inventory.ItemCount(eats), "Only what fits leaves the storehouse.");
            Assert.AreEqual(0, pen.Offer().Most);
            Assert.IsFalse(pen.WantsFeeding, "Full: a tap shows the timer instead.");
        }
        finally
        {
            InventorySystem.instance = before;
            Object.DestroyImmediate(store);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [TestCase("Chicken Coop")]
    [TestCase("Cow Shelter")]
    public void FeedIsSeenGoingInAndThePopupIsThereToPickHowMany(string title)
    {
        var pen = ShopItem(title).prefab.GetComponent<LivestockPen>();
        var so = new SerializedObject(pen);
        Assert.IsNotNull(so.FindProperty("dropPrefab").objectReferenceValue, "An icon to throw in for each item fed.");
        var into = (Transform)so.FindProperty("feedPoint").objectReferenceValue;
        Assert.IsNotNull(into, "Somewhere for the feed to go in.");
        Assert.IsTrue(into.IsChildOf(pen.transform));

        SerializedProperty bursts = so.FindProperty("feedBurst");
        bool taken = false;
        for (int i = 0; i < bursts.arraySize; i++)
        {
            var ps = (ParticleSystem)bursts.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.IsNotNull(ps);
            taken |= ps.name.StartsWith("Gulp");
        }
        Assert.IsTrue(taken, "The feed is seen taken in.");

        var catalog = AssetDatabase.LoadAssetAtPath<LastOrders.UI.World.WorldUiCatalog>("Assets/Game Assets/Content/LastOrders/WorldUiCatalog.asset");
        Assert.IsNotNull(catalog);
        Assert.IsNotNull(catalog.feedPopup, "The feed popup is in the UI catalog.");

        var popup = new SerializedObject(catalog.feedPopup);
        foreach (string field in new[] { "panel", "backdrop", "closeButton", "title", "nowIcon", "bar", "barTime", "feedIcon", "produceIcon",
                                         "amount", "summary", "less", "more", "most", "reason", "feedButton", "feedButtonLabel" })
            Assert.IsNotNull(popup.FindProperty(field).objectReferenceValue, $"The popup's {field} is wired.");
    }

    [TestCase("Chicken Coop")]
    [TestCase("Cow Shelter")]
    public void SomebodyGoesToWorkSomewhereInsideThePen(string title)
    {
        var herd = ShopItem(title).prefab.GetComponentInChildren<AnimalHerd>(true);
        var so = new SerializedObject(herd);
        Assert.IsTrue(so.FindProperty("hasWork").boolValue);
        Rect area = so.FindProperty("area").rectValue;
        Vector3 approach = so.FindProperty("work").FindPropertyRelative("approach").vector3Value;
        Assert.IsTrue(area.Contains(new Vector2(approach.x, approach.z)), $"The way to the work, {approach}, is inside the pen.");
        Assert.IsNotNull(so.FindProperty("workEffect").objectReferenceValue, "The work shows.");
    }

    // --- The shop ----------------------------------------------------------------------------

    static PlacedObjectTypeSO ShopItem(string title)
    {
        var item = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>($"{ShopFolder}/{title}.asset");
        if (item == null) Assert.Inconclusive($"No shop item '{title}': run 'Last Orders/Farming/Set Up Farmyard (Current Scene)'.");
        return item;
    }

    [TestCase("Chicken Coop", ObjectType.ChickenCoop, ItemType.Egg)]
    [TestCase("Cow Shelter", ObjectType.CowShelter, ItemType.Milk)]
    public void EachBuildingIsSoldInTheFarmingTabAsAPenThatGivesItsItem(string title, ObjectType type, ItemType gives)
    {
        PlacedObjectTypeSO item = ShopItem(title);
        Assert.AreEqual(ObjectCategory.Farming, item.categoryType);
        Assert.AreEqual(type, item.type);
        Assert.Greater(item.PriceAmount, 0, "Paid for in coins.");
        Assert.IsNotNull(item.shopIcon, "Has an icon on its shop button.");
        Assert.IsNotNull(item.placementProfile, "Has a placement profile.");
        Assert.IsTrue(System.Array.Exists(item.placementProfile.rules, r => r is FarmFootingRule), "Only on open ground.");

        Assert.IsNotNull(item.prefab);
        Assert.IsNotNull(item.prefab.GetComponent<PlacedObject_Done>(), "The grid places it by its root.");
        var pen = item.prefab.GetComponent<LivestockPen>();
        Assert.IsNotNull(pen, "It is a pen.");
        Assert.IsNotNull(pen.Produce);
        Assert.AreEqual(gives, pen.Produce.Type);
        Assert.IsFalse(pen.Produce.canBePlanted, "Not something to sow.");

        Transform model = item.prefab.Find("Model");
        Assert.IsNotNull(model);
        Assert.AreEqual(new Vector3(item.width * 0.5f, 0f, item.height * 0.5f), model.localPosition, "Stands in the middle of its footprint.");
    }

    [TestCase("Chicken Coop")]
    [TestCase("Cow Shelter")]
    public void TheGhostIsOnlyWhatTheBuildingLooksLike(string title)
    {
        PlacedObjectTypeSO item = ShopItem(title);
        Assert.IsNotNull(item.visual);
        Assert.IsEmpty(item.visual.GetComponentsInChildren<MonoBehaviour>(true), "No scripts on the ghost.");
        Assert.IsEmpty(item.visual.GetComponentsInChildren<Collider>(true), "No colliders on the ghost.");
        Assert.IsEmpty(item.visual.GetComponentsInChildren<ParticleSystem>(true), "No effects on the ghost.");
        Assert.Greater(item.visual.GetComponentsInChildren<MeshRenderer>(true).Length, 1);
    }

    [TestCase("Chicken Coop")]
    [TestCase("Cow Shelter")]
    public void TheAnimalsGroundAndBedsAreInsideTheFootprint(string title)
    {
        PlacedObjectTypeSO item = ShopItem(title);
        var herd = item.prefab.GetComponentInChildren<AnimalHerd>(true);
        Assert.IsNotNull(herd);
        Assert.Greater(herd.transform.childCount, 0);

        var so = new SerializedObject(herd);
        Rect area = so.FindProperty("area").rectValue;
        float halfW = item.width * 0.5f, halfH = item.height * 0.5f;
        Assert.GreaterOrEqual(area.xMin, -halfW);
        Assert.LessOrEqual(area.xMax, halfW);
        Assert.GreaterOrEqual(area.yMin, -halfH);
        Assert.LessOrEqual(area.yMax, halfH);

        SerializedProperty beds = so.FindProperty("beds");
        Assert.Greater(beds.arraySize, 0, "Somewhere to sleep.");
        for (int i = 0; i < beds.arraySize; i++)
        {
            Vector3 bed = beds.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;
            Assert.IsTrue(area.Contains(new Vector2(bed.x, bed.z)), $"Bed {i} at {bed} is inside the pen.");
        }

        SerializedProperty animals = so.FindProperty("animals");
        for (int i = 0; i < animals.arraySize; i++)
        {
            var animal = (Transform)animals.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.IsNotNull(animal);
            Assert.IsTrue(animal.GetComponent<IAnimalBody>() != null, $"{animal.name} has a body to move.");
        }
    }

    [TestCase("Chicken Coop")]
    [TestCase("Cow Shelter")]
    public void ThereIsSomethingToShowForEveryOneItCanHold(string title)
    {
        var pen = ShopItem(title).prefab.GetComponent<LivestockPen>();
        var so = new SerializedObject(pen);
        Assert.AreEqual(so.FindProperty("capacity").intValue, so.FindProperty("stock").arraySize);
        Assert.IsNotNull(so.FindProperty("collectPoint").objectReferenceValue, "Somewhere to throw the pickups from.");
    }

    // --- The kitchen -------------------------------------------------------------------------

    [Test]
    public void EggsAndMilkAreItemsTheGameKnowsAndTheKitchenUses()
    {
        var database = AssetDatabase.LoadAssetAtPath<ItemsDatabase>($"{ItemsFolder}/ItemsDatabase.asset");
        var types = new List<ItemType>();
        foreach (ItemDataSO item in database.AllItems)
            if (item != null) types.Add(item.Type);

        foreach (ItemType type in new[] { ItemType.Egg, ItemType.Milk, ItemType.CustardTart, ItemType.CornChowder })
            Assert.Contains(type, types, $"{type} is in the items database.");

        var uses = new HashSet<ItemType>();
        foreach (string guid in AssetDatabase.FindAssets("t:CookingRecipe"))
        {
            var recipe = AssetDatabase.LoadAssetAtPath<CookingRecipe>(AssetDatabase.GUIDToAssetPath(guid));
            if (recipe == null || recipe.Ingrediants == null) continue;
            foreach (ItemAmount amount in recipe.Ingrediants)
                if (amount.item != null && amount.item.itemDataSO != null) uses.Add(amount.item.itemDataSO.Type);
        }

        Assert.IsTrue(uses.Contains(ItemType.Egg), "A recipe takes eggs.");
        Assert.IsTrue(uses.Contains(ItemType.Milk), "A recipe takes milk.");
    }

    [Test]
    public void AFullStorehouseStillTakesMoreOfWhatItHolds()
    {
        // A play session can leave the inventory panel listening: it subscribes when initialised and
        // unsubscribes in OnDestroy, which never runs for a panel that was never shown. Out of play
        // mode that listener tries to rebuild a panel that is gone, so drop any left over first.
        typeof(InventorySystem).GetField("onInventoryChangedCallback",
                                         System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                               ?.SetValue(null, null);

        var go = new GameObject("Inventory");
        try
        {
            var inventory = go.AddComponent<InventorySystem>();
            inventory.inventorySize = 1;
            var egg = AssetDatabase.LoadAssetAtPath<ItemDataSO>($"{ItemsFolder}/Egg.asset");
            var milk = AssetDatabase.LoadAssetAtPath<ItemDataSO>($"{ItemsFolder}/Milk.asset");
            if (egg == null || milk == null) Assert.Inconclusive("No Egg or Milk item yet.");

            Assert.IsTrue(inventory.AddItem(new Item(egg, 1)));
            Assert.IsTrue(inventory.IsFull());
            Assert.IsTrue(inventory.AddItem(new Item(egg, 1)), "Another egg stacks on the eggs.");
            Assert.AreEqual(2, inventory.ItemCount(ItemType.Egg));
            Assert.IsFalse(inventory.CanAdd(ItemType.Milk), "No room for a new kind of thing.");
            Assert.IsFalse(inventory.AddItem(new Item(milk, 1)));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
#endif
