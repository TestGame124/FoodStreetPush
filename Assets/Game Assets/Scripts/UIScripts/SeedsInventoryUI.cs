using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The seed tray that opens when an empty plant pot is tapped. Seeds are planted by dragging them
/// out of the tray across the pots (<see cref="SeedDragSource"/>); the tray closes once there is
/// no empty pot or no seed left to plant.
/// </summary>
public class SeedsInventoryUI : MonoBehaviour, ISeedDragHost
{
    PlantPot activePot;

    [SerializeField]Transform seedsContainer;

    [Tooltip("The strip the seeds sit in. A seed dragged over it is still in the tray and plants nothing. " +
             "Defaults to the seeds container's parent.")]
    [SerializeField] RectTransform seedTray;

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
        if (seedTray == null && seedsContainer != null)
            seedTray = seedsContainer.parent as RectTransform;

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
                    if (item == null)
                    {
                        item = new Item(ItemController.GetItem(tempItem.Type), 1);
                        inventory.AddItem(item);
                    }

                    itemSlot.amountText.SetText(inventory.ItemCount(tempItem.Type).ToString());
                    itemSlot.previewImage.sprite = item.itemDataSO.itemImage;

                    // Planted by dragging out onto the pots, not by tapping.
                    SeedDragSource drag = itemSlot.GetComponent<SeedDragSource>();
                    if (drag == null) drag = itemSlot.gameObject.AddComponent<SeedDragSource>();
                    drag.Bind(item.itemDataSO, this, itemSlot.previewImage);

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
        if(pot != null)
        {
            SetPlantingArea(pot);
            gameObject.SetActive(true);
            CameraController.Focus (CameraController.CameraType.Focus, activePot.transform, null, 0.7f);

        }
        else
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

    public bool Covers(Vector2 screen)
    {
        if (seedTray == null)
            return false;

        Canvas canvas = seedTray.GetComponentInParent<Canvas>();
        Camera eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(seedTray, screen, eye);
    }

    /// <summary>
    /// After a drag the tray stays open while there is still an empty pot and a seed to put in it,
    /// so the player can sweep another row; otherwise it closes and the camera goes back.
    /// </summary>
    public void SweepEnded(SeedSweep sweep)
    {
        if (!gameObject.activeInHierarchy)
            return;

        bool emptyPot = LevelController.Instance != null &&
                        LevelController.Instance.zone.PlantPots.Exists(x => x != null && x.currentState == PlantPot.PotState.Empty);

        if (!emptyPot || !AnySeeds())
            DeActivatePanel();
    }

    bool AnySeeds()
    {
        InventorySystem inventory = InventorySystem.instance;
        if (inventory == null)
            return false;

        foreach (ItemType type in slots.Keys)
            if (inventory.ItemCount(type) > 0)
                return true;

        return false;
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
            if (tempItem == null || !tempItem.canBePlanted || !slots.TryGetValue(tempItem.Type, out ItemSlotUI slot))
                continue;
            InventorySystem inventory = InventorySystem.instance;

            int tempAmount = inventory.ItemCount(tempItem.Type);
            slot.amountText.SetText(tempAmount.ToString());
        }
    }

}
