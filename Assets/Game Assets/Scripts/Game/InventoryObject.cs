using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InventoryObject : MonoBehaviour, IInteractableObject
{

    [SerializeField] GameObject foodPlatePrefab;
    [SerializeField] ObjectPool<GameObject> foodPlatePool;
    private void Awake()
    {
        foodPlatePool = new ObjectPool<GameObject>(() => { return CreateFoodPlate(transform.position); },
            plate => { 

                plate.SetActive(true); 
            },
            plate => {
                plate.SetActive(false);
                plate.transform.SetParent(null);
                plate.transform.localScale = Vector3.one;
                plate.transform.position = transform.position;
                plate.transform.rotation= Quaternion.identity;

            }, plate => { Destroy(plate); }, false,10,30);

       

    }
    public void OnTouch()
    {
        UIGame.GetUI().inventoryUIBehaviour.gameObject.SetActive(true);
    }
    private GameObject CreateFoodPlate(Vector3 pos)
    {
        return Instantiate(foodPlatePrefab, pos, Quaternion.identity);
    }

    public GameObject GetFoodPlate(Vector3 pos)
    {
        GameObject foodPlate = foodPlatePool.Get();
        foodPlate.transform.position = pos;
        //foodPlate.transform.localScale =  Vector3.one;
        return foodPlate;
    }

    public void ReturnFoodPlate(GameObject plate)
    {
        foodPlatePool.Release(plate);
    }
}
