using System;
using System.Collections;
using System.Collections.Generic;
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

            case PotState.Done:
                Harvest();
                return true;

            // Still growing: nothing to open and nothing to pick, so the tap is not this pot's.
            default:
                return false;
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
    }

}