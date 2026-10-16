using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrderSystem : MonoBehaviour
{
    public static OrderSystem instance;

    [SerializeField] List<Order> allOrders = new();
    [SerializeField] int totalOrdersCount = 3;
    public int TotalOrdersCount => totalOrdersCount;

    private static List<Item> allAvailableItems = new();

    static List<Item> UnlockedItems() => allAvailableItems.FindAll(item => item != null && item.itemDataSO != null &&
        (!LastOrders.Economy.EconomyRules.TryDish(item.type, out var dish) || LevelUpSystem.GetLevel() >= dish.Level));

    InventorySystem inventory;

    [SerializeField] protected int totalOrdersClaimed;
    public int TotalOrdersClaimed => totalOrdersClaimed;

    

    private void Awake()
    {
        instance= this;
    }

    private void OnEnable()
    {
        OrdersBoardUI.OnOrderClaimed += IncreaseOrdersClaimed;
    }

    private void OnDisable()
    {
        OrdersBoardUI.OnOrderClaimed -= IncreaseOrdersClaimed;
    }



    public void Initialize(InventorySystem inventory) 
    {
        this.inventory = inventory;
        StartCoroutine(InitializeAfterDelay());
    }
    IEnumerator InitializeAfterDelay()
    {
        yield return null;
        allOrders.Clear();
        for (int i = 0; i < totalOrdersCount; i++)
        {
            Order newOrder = new Order(UnlockedItems(), LastOrders.Economy.EconomyRules.OrderKinds(LevelUpSystem.GetLevel()));

            allOrders.Add(newOrder);
        }

        UIGame.GetUI().ordersUIBehaviour.Initialize(allOrders,inventory, this);
    }
    public static void AddAvailableItems(Item item)
    {
        if(item != null && !allAvailableItems.Exists(existing => existing.type == item.type))
            allAvailableItems.Add(item);
      
    }
    public static void RemoveAvailableItem(Item item)
    {
        if (allAvailableItems.Contains(item))
            allAvailableItems.Remove(item);
    }
    public Order RenewOrder(OrdersBoardUI board, Order orderToRemove)
    {
        allOrders.Remove(orderToRemove);

        Order newOrder = new Order(UnlockedItems(), LastOrders.Economy.EconomyRules.OrderKinds(LevelUpSystem.GetLevel()));
        allOrders.Add(newOrder);
        board.ResetState();
        board.Initialize(newOrder, inventory,this);
        return newOrder;
    }

    private void IncreaseOrdersClaimed()
    {
        totalOrdersClaimed++;
    }
     public void DecreaseOrdersClaimed()
    {
        if(totalOrdersClaimed > 0)
            totalOrdersClaimed--;
    }
    
    public List<Order> GetAllOrders()
    {
        return allOrders;
    }
    [System.Serializable]
    public class Order
    {
        public int CoinsReward;
        public int StarsReward;

        public List<Item> ordersData = new();

        /// <summary>The most dishes one order asks for: an order ticket has six spots.</summary>
        public const int MaxDishes = 6;

        public Order(List<Item> availableItems, int totalItems)
        {
            var choices = new List<Item>();
            if (availableItems != null)
                foreach (Item entry in availableItems)
                    if (entry != null && entry.itemDataSO != null && !choices.Exists(other => other.type == entry.type)) choices.Add(entry);
            int kinds = Mathf.Min(choices.Count, Mathf.Clamp(totalItems, 1, LastOrders.Economy.EconomyRules.OrderKinds(LevelUpSystem.GetLevel())));
            for (int i = 0; i < kinds; i++)
            {
                int pick = Random.Range(0, choices.Count);
                Item template = choices[pick];
                choices.RemoveAt(pick);
                Item newItem = new Item(template.itemDataSO, Random.Range(1, LastOrders.Economy.EconomyRules.OrderQuantity(LevelUpSystem.GetLevel()) + 1));
                ordersData.Add(newItem);
                CoinsReward += newItem.itemDataSO.coinsReward * newItem.itemsAmount;
                StarsReward += newItem.itemDataSO.starsReward * newItem.itemsAmount;
            }
        }
        // create Order with Specific Recipe
        public Order(Item item, int totalItems, int starReward)
        {
            if (item == null || item.itemDataSO == null || totalItems <= 0) return;
            Item newItem = new Item(item.itemDataSO, Mathf.Clamp(totalItems, 1, 3));
            ordersData.Add(newItem);
            CoinsReward = newItem.itemDataSO.coinsReward * newItem.itemsAmount;
            StarsReward = Mathf.Max(0, starReward);
        }

        public bool CanClaim(InventorySystem inventory)
        {
            if (inventory == null || ordersData.Count == 0) return false;
            foreach (Item order in ordersData)
            {
                if (inventory.ItemCount(order.type) < order.itemsAmount || !inventory.ContainsItem(order.type))
                {
                    Debug.Log("Not Enough Items!");
                    return false;
                }
            }

            return true;
        }
        

       
    }
}
