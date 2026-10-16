using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Economy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EconomyBalanceTests
{
    sealed class Wallet : IResourceWallet
    {
        public string DisplayName => "Coins";
        public int Balance { get; private set; }
        public void Add(int amount) { Balance += amount; }
        public bool TrySpend(int amount) { if (amount > Balance) return false; Balance -= amount; return true; }
    }

    [Test]
    public void CoinRewardsAndDefenseCostsUseTheSameWallet()
    {
        var wallet = new Wallet();
        var ledger = new ResourceLedger(wallet);
        wallet.Add(50);
        ledger.Add(ResourceType.Coin, 40);
        Assert.AreEqual(90, ledger.Get(ResourceType.Coin));
        Assert.IsTrue(ledger.TrySpend(new[] { new ResourceAmount(ResourceType.Coin, 60) }));
        Assert.AreEqual(30, wallet.Balance);
    }

    [Test]
    public void RetiredCurrencyCombinesWithCoinsBeforeAffordabilityChecks()
    {
        var ledger = new ResourceLedger();
        ledger.Add((ResourceType)6, 60);
        Assert.AreEqual(60, ledger.Get(ResourceType.Coin));
        Assert.AreEqual("Coins", ((ResourceType)6).DisplayName());
        Assert.IsFalse(ledger.TrySpend(new[] { new ResourceAmount((ResourceType)6, 40), new ResourceAmount(ResourceType.Coin, 40) }));
        Assert.AreEqual(60, ledger.Get(ResourceType.Coin));
    }

    [Test]
    public void CombinedCoinPriceCannotPartiallyChargeTheSharedWallet()
    {
        var wallet = new Wallet(); wallet.Add(70);
        var ledger = new ResourceLedger(wallet);
        Price price = new Price(40, new ResourceAmount((ResourceType)6, 40));
        Assert.IsFalse(Till.TryPay(price, ledger, wallet, out _));
        Assert.AreEqual(70, wallet.Balance);
        wallet.Add(10);
        Assert.IsTrue(Till.TryPay(price, ledger, wallet, out _));
        Assert.AreEqual(0, wallet.Balance);
    }

    [Test]
    public void NegativeCostsCannotMintCoins()
    {
        var ledger = new ResourceLedger(); ledger.Add(ResourceType.Coin, 10);
        Assert.IsFalse(ledger.TrySpend(new[] { new ResourceAmount(ResourceType.Coin, -5) }));
        Assert.AreEqual(10, ledger.Get(ResourceType.Coin));
    }

    [Test]
    public void TicketsDoNotMutateTheMenuAndUseSmallQuantities()
    {
        var data = ScriptableObject.CreateInstance<ItemDataSO>();
        data.Type = ItemType.Bread; data.coinsReward = 12; data.starsReward = 2;
        try
        {
            var source = new Item(data, 99);
            var first = new OrderSystem.Order(new List<Item> { source }, 10);
            int quantity = first.ordersData[0].itemsAmount;
            var second = new OrderSystem.Order(new List<Item> { source }, 10);
            Assert.AreEqual(99, source.itemsAmount);
            Assert.AreEqual(quantity, first.ordersData[0].itemsAmount);
            Assert.AreNotSame(first.ordersData[0], second.ordersData[0]);
            Assert.That(quantity, Is.InRange(1, 3));
            Assert.AreEqual(12 * quantity, first.CoinsReward);
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void SpecificTicketPreservesQuantityAndPaysForAllItems()
    {
        var data = ScriptableObject.CreateInstance<ItemDataSO>(); data.coinsReward = 12;
        try
        {
            var ticket = new OrderSystem.Order(new Item(data, 99), 3, 6);
            Assert.AreEqual(3, ticket.ordersData[0].itemsAmount);
            Assert.AreEqual(36, ticket.CoinsReward);
        }
        finally { Object.DestroyImmediate(data); }
    }

    [Test]
    public void ProgressionSlowsGraduallyAndDoesNotUseTheOldThreeXpCurve()
    {
        Assert.AreEqual(20, EconomyRules.XpForLevel(1));
        Assert.AreEqual(40, EconomyRules.XpForLevel(2));
        for (int level = 2; level <= 20; level++)
            Assert.Greater(EconomyRules.XpForLevel(level), EconomyRules.XpForLevel(level - 1));
    }

    [Test]
    public void AuthoredRecipesMatchBalanceAndHavePositiveMargins()
    {
        int checkedRecipes = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CookingRecipe"))
        {
            var recipe = AssetDatabase.LoadAssetAtPath<CookingRecipe>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsNotNull(recipe.Results.item);
            Assert.IsTrue(EconomyRules.TryDish(recipe.Results.item.type, out var dish), recipe.name);
            Assert.AreEqual(dish.Seconds, recipe.TotalTimeToCook, recipe.name);
            Assert.AreEqual(dish.Level, recipe.requiredLevel, recipe.name);
            Assert.AreEqual(2, recipe.Results.Amount, recipe.name);
            int supply = 0;
            foreach (ItemAmount ingredient in recipe.Ingrediants)
                supply += EconomyRules.SupplyPrice(ingredient.item.type) * ingredient.Amount;
            Assert.Greater(recipe.Results.item.itemDataSO.coinsReward * recipe.Results.Amount, supply, recipe.name);
            checkedRecipes++;
        }
        Assert.AreEqual(17, checkedRecipes);
    }

    [Test]
    public void SeedReservePreventsCookingTheLastPlantableIngredient()
    {
        var crop = ScriptableObject.CreateInstance<ItemDataSO>(); crop.Type = ItemType.Flour; crop.Name = "Flour"; crop.canBePlanted = true;
        var dish = ScriptableObject.CreateInstance<ItemDataSO>(); dish.Type = ItemType.Bread;
        var recipe = ScriptableObject.CreateInstance<CookingRecipe>();
        var go = new GameObject("Economy test inventory"); var inventory = go.AddComponent<InventorySystem>();
        try
        {
            recipe.Ingrediants = new List<ItemAmount> { new ItemAmount { item = new Item(crop, 1), Amount = 2 } };
            recipe.Results = new ItemAmount { item = new Item(dish, 1), Amount = 2 };
            inventory.AddItem(new Item(crop, 2));
            StringAssert.Contains("seed", recipe.BlockReason(inventory));
            inventory.AddItem(new Item(crop, 1));
            Assert.IsTrue(recipe.TryCook(inventory));
            Assert.AreEqual(1, inventory.ItemCount(ItemType.Flour));
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(recipe); Object.DestroyImmediate(crop); Object.DestroyImmediate(dish); }
    }

    [Test]
    public void StarterDefenseCanKillBasicZombiesBeforeASingleZombieBreaksAYardObject()
    {
        const string root = "Assets/Game Assets/Content/LastOrders/";
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(root + "Enemies/Enemy_Shambler.asset");
        var pistol = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(root + "Defense/Weapon_Pistol.asset");
        var balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(root + "GameBalance.asset");
        float timeToKill = (Mathf.Ceil(enemy.maxHealth / pistol.damagePerShot) - 1) * pistol.secondsBetweenShots;
        Assert.Less(timeToKill, balance.encounter.outdoorObjectHealth / enemy.structureDps);
        Assert.AreEqual(30, balance.encounter.repairKitCoinPrice);
        Assert.Less(balance.encounter.repairKitCoinPrice, balance.encounter.tiers[0].victoryCoins);
    }

    [Test]
    public void MissingSuppliesArePaidOnceAndAnUnaffordableBatchConsumesNothing()
    {
        int previous = CurrenciesController.GetCurrencyAmount(CurrencyType.Coins);
        CurrenciesController.SubtractCurrency(CurrencyType.Coins, previous);
        CurrenciesController.AddCurrency(CurrencyType.Coins, 11);
        var chicken = ScriptableObject.CreateInstance<ItemDataSO>(); chicken.Type = ItemType.Chicken;
        var lettuce = ScriptableObject.CreateInstance<ItemDataSO>(); lettuce.Type = ItemType.Lettuce;
        var dish = ScriptableObject.CreateInstance<ItemDataSO>(); dish.Type = ItemType.ChickenSalad;
        var recipe = ScriptableObject.CreateInstance<CookingRecipe>();
        var go = new GameObject("Supply test inventory"); var inventory = go.AddComponent<InventorySystem>();
        try
        {
            recipe.Ingrediants = new List<ItemAmount>
            {
                new ItemAmount { item = new Item(chicken, 1), Amount = 2 },
                new ItemAmount { item = new Item(lettuce, 1), Amount = 1 },
            };
            recipe.Results = new ItemAmount { item = new Item(dish, 1), Amount = 2 };
            inventory.AddItem(new Item(chicken, 1));
            Assert.AreEqual(12, recipe.SupplyCost(inventory));
            Assert.IsFalse(recipe.TryCook(inventory));
            Assert.AreEqual(1, inventory.ItemCount(ItemType.Chicken));
            Assert.AreEqual(11, CurrenciesController.GetCurrencyAmount(CurrencyType.Coins));
            CurrenciesController.AddCurrency(CurrencyType.Coins, 1);
            Assert.IsTrue(recipe.TryCook(inventory));
            Assert.AreEqual(0, inventory.ItemCount(ItemType.Chicken));
            Assert.AreEqual(0, CurrenciesController.GetCurrencyAmount(CurrencyType.Coins));
        }
        finally
        {
            CurrenciesController.SubtractCurrency(CurrencyType.Coins, CurrenciesController.GetCurrencyAmount(CurrencyType.Coins));
            CurrenciesController.AddCurrency(CurrencyType.Coins, previous);
            Object.DestroyImmediate(go); Object.DestroyImmediate(recipe); Object.DestroyImmediate(chicken); Object.DestroyImmediate(lettuce); Object.DestroyImmediate(dish);
        }
    }

    [Test]
    public void DefenseBuildAndUpgradeEnforcePlayerLevelBeforeCharging()
    {
        var balance = ScriptableObject.CreateInstance<GameBalanceConfig>();
        var definition = ScriptableObject.CreateInstance<DefenseDefinition>();
        var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        try
        {
            var ledger = new ResourceLedger(); ledger.Add(ResourceType.Coin, 500);
            var survivors = new LastOrders.Survivors.SurvivorService(balance, ledger);
            var progression = new LastOrders.Progression.ProgressionService();
            var defense = new LastOrders.Defense.DefenseService(survivors, ledger, null, progression, null, () => null, () => false, () => 2);
            definition.requiredLevel = 3;
            definition.requiresCrew = false;
            definition.buildPrice = new DefensePrice(0, new ResourceAmount(ResourceType.Coin, 90));
            definition.levels = new[] { new DefenseLevel { weapon = weapon }, new DefenseLevel { weapon = weapon, requiredLevel = 5 } };
            defense.Register(definition, "unbuilt", Vector3.zero, false);
            Assert.IsFalse(defense.TryBuild("unbuilt", LastOrders.Defense.DefensePayment.Resources, out string reason));
            StringAssert.Contains("level 3", reason);
            defense.Register(definition, "starter", Vector3.zero, true);
            Assert.IsFalse(defense.TryUpgrade("starter", LastOrders.Defense.DefensePayment.Resources, out reason));
            StringAssert.Contains("level 5", reason);
            Assert.AreEqual(500, ledger.Get(ResourceType.Coin));
        }
        finally { Object.DestroyImmediate(balance); Object.DestroyImmediate(definition); Object.DestroyImmediate(weapon); }
    }

    [Test]
    public void QueuedRecipeWaitsUntilItsStationActivatesIt()
    {
        var go = new GameObject("Queue test station"); go.SetActive(false);
        var station = go.AddComponent<CookeryStation>();
        var inventory = go.AddComponent<InventorySystem>();
        var recipe = ScriptableObject.CreateInstance<CookingRecipe>(); recipe.TotalTimeToCook = 60;
        try
        {
            var batch = new CookRecipeData(station, recipe, inventory) { active = false };
            var timer = batch.StartCookingTimer(station);
            for (int i = 0; i < 10; i++) Assert.IsTrue(timer.MoveNext());
            Assert.AreEqual(60, batch.timer);
            batch.active = true;
            Assert.IsTrue(timer.MoveNext());
            Assert.IsInstanceOf<WaitForSeconds>(timer.Current);
            Assert.IsTrue(timer.MoveNext());
            Assert.AreEqual(59, batch.timer);
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(recipe); }
    }
}
