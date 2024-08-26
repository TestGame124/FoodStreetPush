using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemReceiver : MonoBehaviour/*,IInteractableObject*/
{

    [SerializeField] SpriteRenderer itemImage;
    
    public void Initialize(Item item,InventorySystem inventory)
    {
       
        itemImage.sprite = item.itemDataSO.itemImage;

        inventory.AddItem(item);

        transform.DOScale(0, .3f).SetDelay(1f).SetEase(Ease.InBack).OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }

    //public void OnTouch()
    //{
    //    Destroy(gameObject);
    //}
}
