using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SeedsInventoryUI : MonoBehaviour
{
    PlantPot activePot;

    [SerializeField]Transform seedsContainer;

    [SerializeField] ItemSlotUI itemSlotTemplate;

    [SerializeField] List<PlantPot> EmptyPlantPots = new();

    Dictionary<ItemType,ItemSlotUI> slots = new();

    private void OnEnable()
    {
        InventorySystem.onInventoryChangedCallback += RefreshInventory;
        transform.DOLocalMoveX(0, 0.4f).From(-1000).SetEase(Ease.OutBack);

    }
    private void OnDisable()
    {
        InventorySystem.onInventoryChangedCallback -= RefreshInventory;

    }
    public void Initialise()
    {
      
        ItemDataSO[] itemsData = ItemController.instance.itemsDatabase.AllItems;
        for (int i = 0; i < itemsData.Length; i++)
        {
            ItemDataSO tempItem = itemsData[i];
            if(tempItem != null) {
                if (tempItem.canBePlanted)
                {
                    ItemSlotUI itemSlot = Instantiate(itemSlotTemplate, seedsContainer);
                    InventorySystem inventory = InventorySystem.instance;
                    Item item = inventory.GetItem(tempItem.Type);
                    if (inventory.GetItem(tempItem.Type) != null)
                    {
                        itemSlot.amountText.SetText(item.itemsAmount.ToString());
                        itemSlot.GetComponent<Button>().onClick.AddListener(() => SeedSlotButton(item));
                        itemSlot.previewImage.sprite = item.itemDataSO.itemImage;
                    }
                    else
                    {

                        Item newItem = new Item(ItemController.GetItem(tempItem.Type), 1);
                        
                        inventory.AddItem(newItem);
                        itemSlot.amountText.SetText(newItem.itemsAmount.ToString());
                        itemSlot.previewImage.sprite = newItem.itemDataSO.itemImage;
                        itemSlot.GetComponent<Button>().onClick.AddListener(() => SeedSlotButton(newItem));
                    }

                    slots.Add(tempItem.Type,itemSlot);
                }
            
            }
        }

    }

    public void SetPlantingArea(PlantPot activePot)
    {
        this.activePot = activePot;   
        RefreshInventory();
    }

    public void DeActivatePanel()
    {
        CameraController.SetCameraToDefault();
        gameObject.SetActive(false);
    }
    public void ActivatePanel()
    {
       PlantPot pot = LevelController.Instance.zone.PlantPots.Find(x => x.currentState == PlantPot.PotState.Empty);
        Debug.Log($"PLANT POT : {pot}");
        if(pot != null)
        {
            SetPlantingArea(pot);
            gameObject.SetActive(true);
            CameraController.Focus (CameraController.CameraType.Focus, activePot.transform, null, 0.7f);

        }
        else if(TutorialController.currentStage == TutorialStage.Done)
        {
            DeActivatePanel();
        }
    }

    public void AddEmptyPots(PlantPot pot) 
    {
        if (EmptyPlantPots.Contains(pot))
            return;
        EmptyPlantPots.Add(pot);
    }

    public void RemoveEmptyPot(PlantPot pot)
    {
        if(EmptyPlantPots.Contains(pot))
            EmptyPlantPots.Remove(pot);
    }

    public void SeedSlotButton(Item seed)
    {
        if (activePot== null) return;

        activePot.Plant(seed);

        if(TutorialController.currentStage != TutorialStage.Done)
        {
            if (TutorialController.currentStage == TutorialStage.PlantingStage)
            {
                TutorialController.OnPlanting?.Invoke();
            }
        }
        
        ActivatePanel();
      
    }

    public ItemSlotUI GetItemSlot(ItemType type)
    {
        if (!slots.ContainsKey(type))
            return null;

        return slots[type];
    }

    private void RefreshInventory()
    {
        ItemDataSO[] itemsData = ItemController.instance.itemsDatabase.AllItems;
        for (int i = 0; i < itemsData.Length; i++)
        {
            ItemDataSO tempItem = itemsData[i];
            if (!tempItem.canBePlanted)
                continue;
            InventorySystem inventory = InventorySystem.instance;
            
            int tempAmount = inventory.ItemCount(tempItem.Type);
            slots[tempItem.Type].amountText.SetText(tempAmount.ToString());
        }
    }

}
