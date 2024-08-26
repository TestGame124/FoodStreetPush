using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrdersBoardUI : MonoBehaviour
{
    InventorySystem inventorySystem;

    [SerializeField] Text CoinsReward;
    [SerializeField] Text StarRewards;

    [SerializeField] public Button ClaimButton, TrashButton;

    [SerializeField] Transform[] ItemSlotsParents;
    private List<ItemSlotUI> ItemsSlotsUIList = new();

    [SerializeField] ItemSlotUI itemslotPrefab;
    OrderSystem orderSystem;

    RectTransform rectTransform;

    OrderSystem.Order activeOrder;

    bool cantPressBtn;

    public static Action OnOrderClaimed;

    private void OnEnable()
    {
        InventorySystem.onInventoryChangedCallback += RefreshItems;
        transform.DOScaleX(1, 0.3f).From(0).SetEase(Ease.OutBack);
        RefreshItems();
    }
    private void OnDisable()
    {
        InventorySystem.onInventoryChangedCallback -= RefreshItems;

    }
    public void Initialize(OrderSystem.Order order, InventorySystem inventory, OrderSystem orderSystem)
    {

        inventorySystem= inventory;
        if(rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
        this.orderSystem = orderSystem;
        CoinsReward.text = order.CoinsReward.ToString();
        StarRewards.text = order.StarsReward.ToString();

        for(int i = 0; i < order.ordersData.Count; i++)
        {
            ItemSlotUI itemSlot = Instantiate(itemslotPrefab, ItemSlotsParents[i]);
            itemSlot.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
            itemSlot.transform.localScale = Vector3.one * 2;
            itemSlot.previewImage.sprite = order.ordersData[i].itemDataSO.itemImage;
            itemSlot.amountText.text = $"{inventory.ItemCount(order.ordersData[i].type)}/{order.ordersData[i].itemsAmount}";
            ItemsSlotsUIList.Add(itemSlot);
        }
        activeOrder = order;

        ClaimButton.onClick.RemoveAllListeners();
        ClaimButton.onClick.AddListener(() => { Claim(order,inventory); });

        TrashButton.onClick.RemoveAllListeners();
        TrashButton.onClick.AddListener(() => { Trash(); });

    }

    private void ClearItemSlots()
    {
        foreach (ItemSlotUI slot in ItemsSlotsUIList)
        {
                Destroy(slot.gameObject);
        }
            ItemsSlotsUIList.Clear();
    }
    
    private void RefreshItems()
    {
        if (ItemsSlotsUIList.Count == 0)
            return;
        Debug.Log("Refresh Items!");
        for (int i = 0; i < activeOrder.ordersData.Count; i++)
        {
            ItemsSlotsUIList[i].amountText.text = $"{inventorySystem.ItemCount(activeOrder.ordersData[i].type)}/{activeOrder.ordersData[i].itemsAmount}";
        }
    }
    public void Claim(OrderSystem.Order order, InventorySystem inventory)
    {
        if (!order.CanClaim(inventory))
        {
            //UIGame.GetUI().notEnoughIngPanel.SetActive(true);
            UIGame.GetUI().notEnoughIngPanel.SetActive(true);
            //UIGame.GetUI().notEnoughIngPanel.transform.DOMoveY(0, 0.5f).SetEase(Ease.OutBack).From(UIGame.GetUI().notEnoughIngPanel.transform.position.y - );
            for (int i = 0; i < UIGame.GetUI().notEnoughIngPanel.transform.childCount; i++)
            {
                Transform child = UIGame.GetUI().notEnoughIngPanel.transform.GetChild(i);
                child.DOLocalMoveY(0,0.5f).From(-500).SetEase(Ease.OutBack);
                child.DOScale(1,0.5f).From(0.5f).SetEase(Ease.OutBack);
                
            } 
            return;
        }
        if (cantPressBtn)
            return;
        cantPressBtn = true;

        for (int i = 0; i < order.ordersData.Count; i++)
        {
            inventory.RemoveItem(order.ordersData[i]);

        }

        CurrenciesController.AddCurrency(CurrencyType.Coins, order.CoinsReward);
        LevelUpSystem.AddXP(order.StarsReward);
        OnOrderClaimed?.Invoke();

        if(TutorialController.currentStage != TutorialStage.Done)
            TutorialController.OnOrderButtonClick?.Invoke();

        transform.DOScale(0, .5f).SetEase(Ease.InBack).OnComplete(() =>
        {
            cantPressBtn= false;
            activeOrder = orderSystem.RenewOrder(this, order);
            transform.DOScale(1, .5f).SetEase(Ease.OutQuad).SetDelay(.5f);
        });
       
    }

    public void Trash()
    {

        if(cantPressBtn) return;

        cantPressBtn = true;

        OrderSystem.Order tempOrder = activeOrder;

        rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y - rectTransform.sizeDelta.y, 0.5f).SetEase(Ease.InBack).OnComplete(() =>
        {
            cantPressBtn= false;
            activeOrder = orderSystem.RenewOrder(this, tempOrder);
            rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + rectTransform.sizeDelta.y, 0.5f).SetDelay(.7f);

        });

      
    }

    public void ResetState()
    {
        //transform.localScale = Vector3.one;
        ClearItemSlots();
    }
}
