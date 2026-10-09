using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlantPot : MonoBehaviour, IInteractableObject, ISeedPlot, IHarvestPlot
{
    public enum PotState
    {
        Empty,
        IsBusy,
        Done,
    }

    public PotState currentState;

    public Transform centerPoint;

    private Item activeItem;

    public GameObject plantPreview;

    public float timer;

    public SeedsInventoryUI seedinventoryUI;

    [Tooltip("The growing crop: a plant on each of the seed's spots, each showing the stage reached.")]
    [SerializeField] CropPatch patch;

    [Tooltip("Used only when there is no patch: one sprite showing each of the seed's grow stages in turn.")]
    [SerializeField] CropSprite crop;

    /// <summary>A seed has just gone in: for the planting effects and anything else that wants to know.</summary>
    public event Action<PlantPot> Planted;

    /// <summary>The ripe crop has just been picked.</summary>
    public event Action<PlantPot> Harvested;

    public bool CanHarvest => currentState == PotState.Done;

    /// <summary>Where a tool offered over this pot waits: above the middle of the crop.</summary>
    public Vector3 CropTop => (centerPoint != null ? centerPoint.position : transform.position) + Vector3.up * 0.35f;

    [Header("VFX")]
    [SerializeField] GameObject finishedEffect;

    
    private void OnEnable()
    {
        if(LevelController.Instance)
            LevelController.Instance.zone.RegisterPot(this);
    }

    private void OnDisable()
    {
        if (LevelController.Instance)
            LevelController.Instance.zone.UnRegisterPot(this);

    }
    private void Start()
    {
            LevelController.Instance.zone.RegisterPot(this);

        if (seedinventoryUI == null)
            seedinventoryUI = UIGame.GetUI().seedInventory;

        if(currentState == PotState.Empty)
            seedinventoryUI.AddEmptyPots(this);
    }

    public bool CanPlant(ItemDataSO seed) => currentState == PotState.Empty && seed != null && seed.canBePlanted;

    public bool TryPlant(ItemDataSO seed)
    {
        InventorySystem inventory = InventorySystem.instance;
        if (!CanPlant(seed) || inventory == null || inventory.ItemCount(seed.Type) < 1)
            return false;

        // A fresh item rather than the inventory's own: the pot keeps it until the harvest, and the
        // inventory's entry is changed in place or dropped as seeds are spent.
        Plant(new Item(seed, 1));
        return currentState != PotState.Empty;
    }

    public void Plant(Item item, bool growInstantly = false)
    {
        if (currentState != PotState.Empty || item == null || item.itemsAmount < 1)
            return;

        if (!InventorySystem.instance.RemoveItem(item))
            return;

        activeItem = item;

        if(!growInstantly)
            timer = activeItem.itemDataSO.totalTimeInSeconds;

        StartCoroutine(Growing());
        Planted?.Invoke(this);
    }

    IEnumerator Growing()
    {

        currentState = PotState.IsBusy;

        plantPreview.SetActive(true);

        ItemDataSO seed = activeItem.itemDataSO;
        int stageCount = seed.GrowStageCount;
        float growTime = Mathf.Max(1, seed.totalTimeInSeconds);

        while (timer > 0)
        {
            ShowStage(seed, GrowingStage(1f - timer / growTime, stageCount));
            yield return new WaitForSeconds(1);
            timer--;
        }

        // The ripe crop appears only once it can be picked.
        ShowStage(seed, stageCount - 1);

        finishedEffect.SetActive(true);

        currentState = PotState.Done;

    }

    /// <summary>
    /// The stage to show at <paramref name="progress"/> (0 just planted, 1 grown): the growing time is
    /// shared evenly between every stage but the last, the ripe crop, which waits for the harvest.
    /// </summary>
    static int GrowingStage(float progress, int stageCount)
    {
        if (stageCount <= 1)
            return 0;

        return Mathf.Clamp(Mathf.FloorToInt(progress * (stageCount - 1)), 0, stageCount - 2);
    }

    void ShowStage(ItemDataSO seed, int index)
    {
        Sprite[] stages = seed.growStages ?? Array.Empty<Sprite>();
        Sprite single = stages.Length > 0 ? stages[Mathf.Clamp(index, 0, stages.Length - 1)] : null;

        if (patch != null)
        {
            if (seed.cropLook != null)
                patch.Show(seed.cropLook, index);
            else
                patch.ShowSingle(single);
        }
        else if (crop != null && single != null)
        {
            crop.Show(single);
        }
    }

    

    /// <summary>
    /// Called by Mouse3D once a tap has been confirmed. The router has already ruled out
    /// the UI, camera drags and pinches, so there is nothing left for the pot to re-check.
    /// </summary>
    public bool OnTouch()
    {
        switch (currentState)
        {
            case PotState.Empty:
                seedinventoryUI.gameObject.SetActive(true);
                seedinventoryUI.SetPlantingArea(this);
                CameraController.Focus(CameraController.CameraType.Focus ,transform, null,0.7f);
                return true;

            // Ripe: the sickle comes up over the pot, to be swept across every ripe pot. With no
            // sickle in the scene, the tap picks the crop straight away as it always did.
            case PotState.Done:
                HarvestTool tool = HarvestTool.Instance;
                if (tool == null || !tool.Offer(this, CropTop))
                    Harvest();
                return true;

            // Still growing: nothing to open and nothing to pick, so the tap is not this pot's.
            default:
                return false;
        }
    }

    public bool TryHarvest()
    {
        if (!CanHarvest)
            return false;

        Harvest();
        return true;
    }

    private void Harvest() { 
        

        finishedEffect.SetActive(false);
        plantPreview.SetActive(false);
        if (patch != null)
            patch.Clear();
        InventorySystem inventory = InventorySystem.instance;
        int generateItems = 2;

        // Thrown out of the crop together, so they fan out rather than land on each other.
        activeItem.itemDataSO.GenerateItem(ItemController.instance.itemReceiver, activeItem, CropTop, inventory, generateItems);

        currentState= PotState.Empty;
        Harvested?.Invoke(this);
    }

}