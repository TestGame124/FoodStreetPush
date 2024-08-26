using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class OrderSystem : MonoBehaviour
{
    public static OrderSystem instance;

    [SerializeField] List<Order> allOrders = new();
    [SerializeField] int totalOrdersCount = 3;
    public int TotalOrdersCount => totalOrdersCount;

    private static List<Item> allAvailableItems = new();

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
        for (int i = 0; i <= totalOrdersCount; i++)
        {
            int randAmountItem = Random.Range(1, allAvailableItems.Count);
            Order newOrder = new Order(allAvailableItems, randAmountItem);

            if (TutorialController.currentStage != TutorialStage.Done && i == 0)
            {
                newOrder = new Order(new Item(ItemController.GetItem(ItemType.Bread), 1), 1, 3);   
            }
           
           
            allOrders.Add(newOrder);
        }

        UIGame.GetUI().ordersUIBehaviour.Initialize(allOrders,inventory, this);
    }
    public static void AddAvailableItems(Item item)
    {
        if(!allAvailableItems.Contains(item))
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

        int randAmountItem = Random.Range(1, allAvailableItems.Count);
        Order newOrder = new Order(allAvailableItems, randAmountItem);
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
        
        public Order(List<Item> availableItems, int totalItems)
        {
            
            for(int i =0; i < totalItems; i++)
            {
                int randomItem = Random.Range(0, availableItems.Count);
                Item newItem = availableItems[randomItem];

                int randomAmount = Random.Range(1, availableItems.Count + 1);
                newItem.itemsAmount = randomAmount;
                if (!ordersData.Contains(newItem))
                {
                    ordersData.Add(newItem);

                    CoinsReward += newItem.itemDataSO.coinsReward * newItem.itemsAmount;
                    StarsReward += newItem.itemDataSO.starsReward * newItem.itemsAmount;
                }
            }
        }
        // create Order with Specific Recipe
        public Order(Item item, int totalItems, int starReward)
        {
            for (int i = 0; i < totalItems; i++)
            {
                
                Item newItem = item;

                newItem.itemsAmount = 1;
                if (!ordersData.Contains(newItem))
                {
                    ordersData.Add(newItem);

                    CoinsReward += newItem.itemDataSO.coinsReward * newItem.itemsAmount;
                    StarsReward = starReward;
                }
            }
        }

        public bool CanClaim(InventorySystem inventory)
        {
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
