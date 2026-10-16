using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class UiItemsWindow : MonoBehaviour
{

    [Serializable]
    public struct ItemContainerData
    {

        public ObjectCategory type;
        public bool isActive;
        public Transform container;
    }

    [SerializeField] private ItemContainerData[] itemContainers;
    private Dictionary<ObjectCategory, Transform> itemContainerDict = new();

    [SerializeField]
    ItemUiButton itemUiPrefab;

    [SerializeField] public GameObject closeButton;

    [SerializeField] ScrollRect scrollRect;

    [Tooltip("A drawer that slides up from the bottom of the screen. Empty: the old left-hand column, which slides in from the left.")]
    [SerializeField] RectTransform drawer;
    [SerializeField] float drawerDrop = 420f;

    Vector2 drawerRest;
    bool drawerRestKnown;

    public void Initialize(ObjectDatabaseHandler.ObjectDatabases[] itemData)
    {

        RegisterItems();

    }

    public ItemUiButton InitializeShopItemUI(int id, ObjectDatabaseHandler.ObjectDatabases objectDatabase)
    {
        ItemUiButton itembtn = Instantiate(itemUiPrefab, GetContainer(objectDatabase.type));

        int index = id;
        itembtn.OnClickOnButton = () => ShopItemButton(index, objectDatabase);
        itembtn.Initialize(objectDatabase.placedObjectTypeSOList[index]);

        return itembtn;
    }

    public void ActivatePanel(GameObject panel)
    {
        foreach(ItemContainerData itemContainerData in itemContainers) {
            if (itemContainerData.container.gameObject.activeSelf)
            {
                itemContainerData.container.gameObject.SetActive(false);
                break;
            }
        }
        scrollRect.content = panel.GetComponent<RectTransform>();
        panel.SetActive(true);
    }

    private void ShopItemButton(int index,ObjectDatabaseHandler.ObjectDatabases objectDatabase)
    {
        if (!objectDatabase.placedObjectTypeSOList[index].CanPurchase())
            return;
        

        GridBuildingSystem3D.Instance.SetPlacingObject(objectDatabase.type, objectDatabase.placedObjectTypeSOList[index].type);
    }

    public Transform GetShopItemCategory(ObjectCategory cat)
    {
        if (itemContainerDict.ContainsKey(cat))
        {
            return itemContainerDict[cat];
        }
        return null;
    }

    private void RegisterItems()
    {
        foreach (ItemContainerData cont in itemContainers)
        {
            if (itemContainerDict.ContainsKey(cont.type))
            {
                continue;
            }
            itemContainerDict.Add(cont.type, cont.container);
        }
    }

   
    public void ActiveState(bool state)
    {
        if (drawer != null)
        {
            if (!drawerRestKnown)
            {
                drawerRest = drawer.anchoredPosition;
                drawerRestKnown = true;
            }

            bool opening = state && !gameObject.activeSelf;
            gameObject.SetActive(state);
            DOTween.Kill(drawer);
            if (opening)
                drawer.DOAnchorPos(drawerRest, 0.28f).From(drawerRest + Vector2.down * drawerDrop).SetEase(Ease.OutCubic).SetUpdate(true);
            else
                drawer.anchoredPosition = drawerRest;
            return;
        }

        gameObject.SetActive(state);
        transform.DOMoveX(0, .2f).From(-670);
    }
    private Transform GetContainer(ObjectCategory type)
    {
        return itemContainerDict[type];
    }
}
