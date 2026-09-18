using System;
using System.Collections;
using System.Collections.Generic;
using FoodStreet.Saving;
using UnityEngine;

public class PlantPot : MonoBehaviour, IInteractableObject
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

    [SerializeField] MeshFilter plantMesh;
    [SerializeField] MeshRenderer plantMat;

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

    public void Plant(Item item, bool growInstantly = false)
    {
        if (currentState != PotState.Empty)
            return;
        activeItem = item;
        if(item.itemsAmount < 1)
        {
            return;
        }
        if (!InventorySystem.instance.RemoveItem(item))
            return;

        
        if(!growInstantly)
            timer = activeItem.itemDataSO.totalTimeInSeconds;

        seedinventoryUI?.ActivatePanel();

        StartCoroutine(Growing());
    }

    IEnumerator Growing()
    {

        currentState = PotState.IsBusy;

        plantPreview.SetActive(true);

        int meshIndex = 0;
        int totalMeshes = activeItem.itemDataSO.plantGrowMeshes.Length;

        plantMesh.mesh = activeItem.itemDataSO.plantGrowMeshes[meshIndex];
        plantMat.materials = activeItem.itemDataSO.plantMat;
        float meshChangeInterval = Mathf.RoundToInt(timer / totalMeshes);
        float nextMeshChangeTime = timer - meshChangeInterval;

        while (timer > 0)
        {
            yield return new WaitForSeconds(1);
            timer--;

            // Check if it's time to change the mesh
            if (timer <= nextMeshChangeTime && meshIndex < totalMeshes - 1)
            {
                meshIndex++;
                if(meshIndex < totalMeshes - 1)
                {

                }
                plantMesh.mesh = activeItem.itemDataSO.plantGrowMeshes[meshIndex];
                nextMeshChangeTime = timer - meshChangeInterval;
            }
        }
        plantMesh.mesh = activeItem.itemDataSO.plantGrowMeshes[totalMeshes - 1];

        finishedEffect.SetActive(true);

        currentState = PotState.Done;
     
    }

    

    public void OnTouch()
    {
        if (UIGame.IsPointerOverUI())
            return;
       
        switch (currentState)
        {
            case PotState.Empty:
                seedinventoryUI.gameObject.SetActive(true);
                seedinventoryUI.SetPlantingArea(this);
                CameraController.Focus(CameraController.CameraType.Focus ,transform, null,0.7f);
                break;
            case PotState.IsBusy:

                break;
            case PotState.Done:
                Harvest();
                break;
        }
    }

    private void Harvest() { 
        

        finishedEffect.SetActive(false);
        plantPreview.SetActive(false);
        InventorySystem inventory = InventorySystem.instance;
        int generateItems = 2;
        for (int i = 0; i < generateItems; i++)
        {


        Vector3 offset = new Vector3(UnityEngine.Random.Range(-1, 1), 1, UnityEngine.Random.Range(-1, 1));
            activeItem.itemDataSO.GenerateItem(ItemController.instance.itemReceiver,activeItem, plantPreview.transform.position + offset, inventory);

        }
        currentState= PotState.Empty;

        if (TutorialController.currentStage != TutorialStage.Done)
        {
            if (TutorialController.currentStage == TutorialStage.FirstStage)
            {
                TutorialController.OnPickingFirstPlant?.Invoke();
            }else if(TutorialController.currentStage == TutorialStage.PlantingStage)
            {
                TutorialController.OnPickingSecondPlant?.Invoke();

            }
        }
         
    }

    #region Saving Loading

    private PlacedObject_Done owner;

    /// <summary>
    /// The ID of the placed object this pot sits on. Pot state is keyed by it,
    /// so building or selling a pot cannot shift anybody else's crop onto the
    /// wrong pot the way saving by list position did.
    /// </summary>
    public int OwnerId
    {
        get
        {
            if (owner == null)
                owner = GetComponentInParent<PlacedObject_Done>();

            return owner != null ? owner.ID : 0;
        }
    }

    public PotSaveData Save()
    {
        PotSaveData data = new PotSaveData
        {
            ownerId = OwnerId,
            state = currentState,
            remainingSeconds = timer,
            hasSeed = activeItem != null
        };

        if (activeItem != null)
        {
            // Only the seed type, never the ItemDataSO itself: the old format
            // serialised the whole asset into the save, which bloated the file
            // and broke as soon as the asset was edited.
            data.seedType = activeItem.type;
            data.seedAmount = activeItem.itemsAmount;
        }

        return data;
    }

    public void Load(PotSaveData data)
    {
        if (data == null)
            return;

        currentState = data.state;
        timer = data.remainingSeconds;

        if (data.hasSeed)
        {
            ItemDataSO seed = ItemController.GetItem(data.seedType);
            activeItem = seed != null ? new Item(seed, data.seedAmount) : null;
        }
        else
        {
            activeItem = null;
        }

        // Without a seed there is nothing to grow, whatever the file claims.
        if (activeItem == null)
        {
            currentState = PotState.Empty;
            return;
        }

        switch (currentState)
        {
            case PotState.IsBusy:
                plantPreview.SetActive(true);
                StartCoroutine(Growing());
                break;

            case PotState.Done:
                // Restore the finished look directly. Replaying the growth
                // coroutine for a crop that already finished used to hand the
                // player a fresh timer instead of their harvest.
                ShowFinishedPlant();
                break;
        }
    }

    private void ShowFinishedPlant()
    {
        plantPreview.SetActive(true);

        Mesh[] growMeshes = activeItem.itemDataSO.plantGrowMeshes;
        if (growMeshes != null && growMeshes.Length > 0)
            plantMesh.mesh = growMeshes[growMeshes.Length - 1];

        plantMat.materials = activeItem.itemDataSO.plantMat;
        finishedEffect.SetActive(true);

        currentState = PotState.Done;
        timer = 0;
    }

    #endregion
}