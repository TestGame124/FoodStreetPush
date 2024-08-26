using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrdersUiBehaviour : MonoBehaviour
{
    [SerializeField] OrdersBoardUI ordersBoardUIPrefab;
    [SerializeField] Transform boardsContainer;

    List<OrdersBoardUI> allOrdersBoard = new();
    InventorySystem inventory;
    CanvasGroup canvasGroup;
    public CanvasGroup CanvasGroupComp => canvasGroup;
    private void Awake()
    {
        canvasGroup= GetComponent<CanvasGroup>();
    }
    public void Initialize(List<OrderSystem.Order> allOrders, InventorySystem inventory, OrderSystem orderSystem)
    {
        this.inventory = inventory;

        foreach (OrderSystem.Order order in allOrders)
        {
            OrdersBoardUI board = Instantiate(ordersBoardUIPrefab, boardsContainer);
            allOrdersBoard.Add(board);
            board.Initialize(order, inventory, orderSystem);
        }
    }

    public void OrderCloseButton()
    {
        if (!TutorialController.TutorialCompleted())
            TutorialController.OnOrderButtonClick?.Invoke();

        gameObject.SetActive(false);

    }
    public List<OrdersBoardUI> GetAllOrdersBoard()
    {
        return allOrdersBoard;
    }

}
