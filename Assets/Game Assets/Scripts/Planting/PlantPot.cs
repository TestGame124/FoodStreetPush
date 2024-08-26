using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
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
    public PotData Save()
    {
        PotData potData = new PotData(currentState, activeItem, timer);

        return potData;
    }

    public void Load(PotData potData)
    {

        if(potData== null)
            potData = new PotData(currentState,activeItem, timer);

        currentState = potData.currentState;

        if (potData.activeItem != null)
        {
            Item item = new Item(ItemController.GetItem(potData.activeItem.type), potData.activeItem.itemsAmount);
            //activeItem = potData.activeItem;
            activeItem = item;
        }
        timer = potData.timer;

        
        if (currentState == PotState.IsBusy || currentState == PotState.Done)
        {
            plantPreview.SetActive(true);
            StartCoroutine(Growing());
        }
    }
    #endregion
}

[Serializable]
public class PotData
{
    public PlantPot.PotState currentState;
    public Item activeItem;
    public float timer;

    public PotData(PlantPot.PotState currentState, Item activeItem, float timer)
    {
        this.currentState = currentState;
        this.activeItem = activeItem;
        this.timer = timer;
    }


    // Add other relevant fields if needed
}