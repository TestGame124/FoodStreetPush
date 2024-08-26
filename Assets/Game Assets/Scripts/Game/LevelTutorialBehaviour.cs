using Coffee.UIExtensions;
using DG.Tweening;
using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class LevelTutorialBehaviour : MonoBehaviour
{
    public static LevelTutorialBehaviour instance;

    [SerializeField] Text tutorialText;

    [Header("Stage Text")]
    public const string FirstStageText = "Let's Harvest Crops!";
    public const string SecondStageText = "Now Let's Plant Seed!";
    public const string ThirdStageText = "Tap On Bakery!";
    public const string ThirdStage2Text = "Tap On Cook Button!";
    //public const string ThirdStage2Text = "";
    //public const string ThirdStage3Text = "Tap On Skip Button To Skip Timer!";
    public const string ThirdStage3Text = "";
    public const string FourthStageText = "Tap On Order Button.";
    public const string FourthStage2Text = "Claim Order.";
    public const string FifthStageText = "Tap On Shop Button.";
    public const string FifthStage2Text = "Now Drag and Drop Soup Station To The Highlighted Area.";
    public const string FifthStage3Text = "Confirm The Placement.";
    public const string SixthStageText = "Name your Restaurant.";

    public const string CloseButtonText= "Tap On Close Button.";

    [Header("Tutorial Settings")]
    public GameObject arrow2dPrefab;
    public GameObject arrow3dPrefab;
    [Space]
    private GameObject tutorialArrow2d;
    private GameObject tutorialArrow3d;
    [Header("Zones thing")]
    [SerializeField]CookeryStation breadOven;
    [Header("Plant Pots")]
    [SerializeField] PlantPot[] pots;

    [SerializeField] RectTransform unmaskingArea;
    [SerializeField] Unmask unmask;

    [SerializeField] public GameObject soupStationPos;


    [Space]
    [SerializeField] SignBoard signBoard;
    public static int numOfTimesEventWillRun = 0;
    private void Awake()
    {
        instance = this;
    }
    private void OnEnable()
    {
        TutorialController.onStageActivation += ActivateStage;

    }

    public void Initialize()
    {
        if (TutorialController.TutorialCompleted())
        {
            ActivateStage(TutorialStage.Done);
            return;
        }
        RegisterStates();

        TutorialController.EnableTutorial(TutorialController.currentStage);
    }

    private void RegisterStates()
    {
        TutorialController.OnPickingFirstPlant += SwitchToSecondPotFirstStage;
        TutorialController.OnPlanting += AfterPlanting;
        TutorialController.OnTappingFirstCookingStation += OnTappingStation;

        TutorialController.OnCookButtonClick += BreadCookingStage;
        TutorialController.OnOrderButtonClick += OrdersStage;

        TutorialController.ObjectPlacingStageEvent += PlacingObjectStage;

        TutorialController.OnRestaurantNameSubmit += FinishTutorial;
    }

    public void ActivateStage(TutorialStage stage)
    {
        Debug.Log($"Current Stage : {stage}");
        if(stage == TutorialStage.FirstStage)
        {
            
            int plantItemIndex = 0;  
            foreach(PlantPot pot in pots)
            {
                ItemType seedType = ItemType.Flour;
                switch (plantItemIndex)
                {
                    case 0:
                    case 1:
                        seedType = ItemType.Flour;
                        break;
                    case 2:
                    case 3:
                        seedType = ItemType.Tomato;
                        break;
                    case 4:
                    case 5:
                        seedType = ItemType.Corn;
                        break;
                }

                CameraController.Focus(CameraController.CameraType.Tutorial, pots[0].centerPoint, () =>
                {
                    SetTutorialText(FirstStageText);
                    ActivateArrow(pots[0].centerPoint,false);

                    //unmaskingArea.position = UIGame.GetScreenPostion(pots[0].centerPoint.position);
                });

                plantItemIndex++;
                Item seed = new Item(ItemController.GetItem(seedType), 1);

                //if (GameHandler.FirstTime)
                //{
                    for (int i = 0; i < 2; i++)
                    {
                        InventorySystem.instance.AddItem(seed);
                    }
                //}
                pot.Plant(seed, true);
            }
        }
        else if (stage == TutorialStage.PlantingStage)
        {
            // Add Seeds
            DeactivateArrows();

            SetTutorialText(SecondStageText);


            UIGame.GetUI().seedInventory.ActivatePanel();
            Transform firstItemSlot = UIGame.GetUI().seedInventory.GetItemSlot(ItemType.Flour).transform;

            ActivateArrow(firstItemSlot, true, new Vector3(0,-100,0));

        }else if(stage == TutorialStage.BreadCookingStage)
        {
            SetTutorialText(ThirdStageText);

            UIGame.GetUI().seedInventory.DeActivatePanel();
            BreadCookingStage();
            

        }else if(stage == TutorialStage.OrdersStage)
        {
            // Focus On Order Button
            SetTutorialText(FourthStageText);

            ActivateArrow(UIGame.GetUI().orderButton.transform, true, new Vector3(-110,-27,0),-90);

        }else if(stage == TutorialStage.GuideStage)
        {
            
            UIGame.GetUI().guidePanel.SetActive(true);
            SetUnmasking(UIGame.GetUI().guidePanel.transform, true);

            StartCoroutine(WaitForGuidePanelClose());
        }else if(stage == TutorialStage.PLacingObjectStage)
        {
            SetTutorialText(FifthStageText);

            ActivateArrow(UIGame.GetUI().shopBtn, true, new Vector3(40,-26),90);
        }else if(stage == TutorialStage.NamingRestaurantStage)
        {
            SetTutorialText(SixthStageText);

            CameraController.Focus(CameraController.CameraType.Tutorial, signBoard.transform,() => { ActivateArrow(signBoard.transform, false); });
        }else if(stage == TutorialStage.Done)
        {
            unmask.transform.parent.gameObject.SetActive(false);
            unmaskingArea.gameObject.SetActive(false);
            DeactivateArrows();

        }
    }

    bool plantPicked;
    private void SwitchToSecondPotFirstStage()
    {
        if(plantPicked)
        {
            TutorialController.EnableTutorial(TutorialStage.PlantingStage);
            return;
        }
        plantPicked = true;
        ActivateArrow(pots[1].centerPoint, false);
        CameraController.Focus(CameraController.CameraType.Tutorial, pots[1].centerPoint);
        
    }

    private void AfterPlanting()
    {
        if(numOfTimesEventWillRun > 0)
        {
            numOfTimesEventWillRun = 0;
        TutorialController.EnableTutorial(TutorialStage.BreadCookingStage);
        }
        else
        {
            numOfTimesEventWillRun++;
        }
    }

    private void OnTappingStation()
    {
        //UIGame.GetUI().cookingUIPanel.GetSlots()[0].
        DeactivateArrows();
        SetTutorialText(ThirdStage2Text);

        ActivateArrow(UIGame.GetUI().cookingUIPanel.recipeBoxesUI[0].CookBtn.transform,true, new Vector3(0,-2,0));
        numOfTimesEventWillRun++;

    }

    private void BreadCookingStage()
    {
        DeactivateArrows();
        Debug.Log($"Step : {numOfTimesEventWillRun}");
        //TutorialController.Save();

        if (numOfTimesEventWillRun == 0)
        {
            CameraController.Focus(CameraController.CameraType.Tutorial, breadOven.centerPoint, () => ActivateArrow(breadOven.centerPoint, false));

        }
        else if (numOfTimesEventWillRun == 1)
        {

            SetTutorialText("");
            ActivateArrow(UIGame.GetUI().cookingUIPanel.recipeBoxesUI[0].CookBtn.transform, true, new Vector3(0, -4, 0), 0,true);

            //ActivateArrow(UIGame.GetUI().cookingUIPanel.GetSlots()[1].transform, true, new Vector3(50, 0), 90, false);
            numOfTimesEventWillRun++;
            //skip Button

        } else if (numOfTimesEventWillRun == 2)
        {
            SetTutorialText(ThirdStage3Text);

            ActivateArrow(UIGame.GetUI().cookingUIPanel.skipButton.transform, true, new Vector3(-100, 0, 0), -90);
           
            UIGame.GetUI().cookingUIPanel.skipButton.SetActive(true);
            numOfTimesEventWillRun++;
        } else if (numOfTimesEventWillRun == 3) {

            SetTutorialText(CloseButtonText);

            ActivateArrow(UIGame.GetUI().cookingUIPanel.closeButton.transform, true, new Vector3(0, -70, 0));
            numOfTimesEventWillRun++;
        }
        else 
        {
            Debug.Log("Fourth Stage Enable");
            numOfTimesEventWillRun = 0;
            TutorialController.EnableTutorial(TutorialStage.OrdersStage);

        }
    }
    
    private void SetTutorialText(string txt = "")
    {
        if(string.IsNullOrEmpty(txt))
            tutorialText.transform.parent.gameObject.SetActive(false);
        else
        {
            if (tutorialText.transform.parent.gameObject.activeInHierarchy)
            {
                tutorialText.DOFade(1, 0.5f).From(0);
                //tutorialText.transform.parent.DOShakeRotation(1).SetEase(Ease.OutBack);

            }
            else
            {

            tutorialText.transform.parent.DOScale(1, 0.3f).From(0).SetEase(Ease.OutBack);
            tutorialText.transform.parent.gameObject.SetActive(true);
            }
        }
        tutorialText.text = txt;
    }
    private void OrdersStage()
    {
        //TutorialController.Save();
        if(numOfTimesEventWillRun == 0)
        {

            SetTutorialText(FourthStage2Text);
            //Focus On Order Claim Button
            Transform focusOn = UIGame.GetUI().ordersUIBehaviour.GetAllOrdersBoard()[0].ClaimButton.transform;
            ActivateArrow(focusOn, true, new Vector3(50,0),90,true,1);
            numOfTimesEventWillRun++;
        }else if(numOfTimesEventWillRun == 1)
        {
            SetTutorialText(CloseButtonText);

            ActivateArrow(UIGame.GetUI().orderCloseBtton.transform, true, new Vector3(0, -70, 0));
            numOfTimesEventWillRun++;
        }
        else
        {

            numOfTimesEventWillRun = 0;
            TutorialController.EnableTutorial(TutorialStage.GuideStage);

        }

    }

    private void PlacingObjectStage()
    {
        if (numOfTimesEventWillRun == 0)
        {
            // Get the second Child
            SetTutorialText(FifthStage2Text);
            Transform focusOn = UIGame.GetUI().uiItemsWindow.GetShopItemCategory(ObjectCategory.Stoves).GetChild(1);
            ActivateArrow(focusOn, true, new Vector3(-118, 0));
            numOfTimesEventWillRun++;
        }
        else if (numOfTimesEventWillRun == 1)
        {
            // On Item Pick
            unmask.transform.parent.gameObject.SetActive(false);
            unmaskingArea.gameObject.SetActive(false);
            soupStationPos.SetActive(true);
            CameraController.Focus(CameraController.CameraType.Tutorial, soupStationPos.transform);

            numOfTimesEventWillRun++;
        }
        else if(numOfTimesEventWillRun == 2)
        {
            SetTutorialText(FifthStage3Text);

            // On Item Drop
            unmask.transform.parent.gameObject.SetActive(false);
            unmaskingArea.gameObject.SetActive(false);
            ActivateArrow(UIGame.GetUI().confirmationPopup.ConfirmButton.transform, true, new Vector3(0,-50));
            soupStationPos.SetActive(false);
            numOfTimesEventWillRun++;


        }
        else if(numOfTimesEventWillRun == 3)
        {
            SetTutorialText(CloseButtonText);

            ActivateArrow(UIGame.GetUI().uiItemsWindow.closeButton.transform, true, new Vector3(80,0), 90);
            numOfTimesEventWillRun++;

        }
        else
        {
            numOfTimesEventWillRun = 0;
            TutorialController.EnableTutorial(TutorialStage.NamingRestaurantStage);
        }
       
    }

    IEnumerator WaitForGuidePanelClose()
    {
        while (UIGame.GetUI().guidePanel.activeInHierarchy)
        {
            yield return null;
        }
        UIGame.GetUI().guidePanel.SetActive(false);
        UIGame.GetUI().levelUpUi.gameObject.SetActive(true);
        UIGame.GetUI().levelUpUi.GetComponent<CanvasGroup>().DOFade(1, 0.5f).From(0);

        unmaskingArea.gameObject.SetActive(false);
        unmask.transform.parent.gameObject.SetActive(false);
        //LevelUpSystem.OnLevelUpFunc();
        while (UIGame.GetUI().levelUpUi.gameObject.activeInHierarchy)
        {
            yield return null;
        }
        TutorialController.EnableTutorial(TutorialStage.PLacingObjectStage);
    }

    private void FinishTutorial()
    {
        TutorialController.EnableTutorial(TutorialStage.Done);
        StartCoroutine(SetCamToDefault());
        TutorialController.Save();
    }
    IEnumerator SetCamToDefault()
    {
        while (UIGame.GetUI().signBoardUI.gameObject.activeInHierarchy)
        {
            yield return null;
        }
        CameraController.SetCameraToDefault();


    }
    public void ActivateArrow(Transform objTransform, bool is2d, Vector3? offset = null, int angle = 0, bool changeUnMaskObj = true, float delay = 0)
    {
        if(offset == null)
            offset = Vector3.zero;

        Vector3 offsetVal = offset.Value;
        DeactivateArrows();
        GameObject arrow = null;
        unmask.transform.parent.gameObject.SetActive(true);
        if (is2d)
        {
            unmaskingArea.gameObject.SetActive(false);
            if (changeUnMaskObj)
            {
                unmask.fitTarget = objTransform.GetComponent<RectTransform>();
            }
            if (tutorialArrow2d != null)
            {
                DOVirtual.DelayedCall(delay, () =>
                {
                    RectTransform rectTran = tutorialArrow2d.GetComponent<RectTransform>();
                    tutorialArrow2d.SetActive(true);
                    tutorialArrow2d.transform.SetParent(objTransform, false);
                    tutorialArrow2d.transform.localScale = Vector3.one;
                    Vector3 arrowScale = new Vector3(tutorialArrow2d.transform.localScale.x / tutorialArrow2d.transform.parent.localScale.x, tutorialArrow2d.transform.localScale.y / tutorialArrow2d.transform.parent.localScale.y);
                    //tutorialArrow2d.transform.localScale = ;
                    tutorialArrow2d.transform.DOScale(arrowScale, 0.3f);

                    //rectTran.anchoredPosition = objTransform.GetComponent<RectTransform>().anchoredPosition * 0;
                    //rectTran.anchoredPosition = objTransform.GetComponent<RectTransform>().anchoredPosition + offsetVal;
                    rectTran.position = objTransform.GetComponent<RectTransform>().position + offsetVal;

                    //tutorialArrow2d.transform.localPosition = Vector3.zero;
                    //tutorialArrow2d.transform.localPosition = objTransform.localPosition + offsetVal;
                    tutorialArrow2d.transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
                    

                });
               
               

                return;
            }

            arrow = Instantiate(arrow2dPrefab, objTransform);
            RectTransform arrowrectTran = arrow.GetComponent<RectTransform>();

            //arrowrectTran.anchoredPosition = objTransform.GetComponent<RectTransform>().anchoredPosition * 0;
            arrowrectTran.position = objTransform.GetComponent<RectTransform>().position + offsetVal;
            //arrow.transform.localPosition = Vector3.zero;
            //arrow.transform.localPosition = objTransform.localPosition + offsetVal;

            arrow.transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
            tutorialArrow2d = arrow;

        }
        else
        {
            unmaskingArea.gameObject.SetActive(true);

            unmask.fitTarget = unmaskingArea;
            Vector3 offset3d = new Vector3(0, 3, 0);

            if (tutorialArrow3d != null)
            {
                tutorialArrow3d.SetActive(true);
                tutorialArrow3d.transform.position = objTransform.position + offset3d + offset.Value;
                tutorialArrow3d.transform.rotation = Quaternion.identity;
                return;
            }

            unmaskingArea.position = UIGame.GetScreenPostion(objTransform.position);
            arrow = Instantiate(arrow3dPrefab, objTransform.position + offset3d + offset.Value, Quaternion.identity);
            arrow.transform.DOMoveY(2, .5f).SetLoops(-1,LoopType.Yoyo).SetEase(Ease.InOutSine);
            tutorialArrow3d = arrow;

        }
    }

    public void SetUnmasking(Transform unmaskObj,bool is2d)
    {
        if (is2d)
        {
            unmask.fitTarget = unmaskObj.GetComponent<RectTransform>();
        }
        else
        {
            unmask.fitTarget = unmaskingArea;
            unmaskingArea.position = UIGame.GetScreenPostion(unmaskObj.position);
        }
    }

    public void DeactivateArrows()
    {
        if(tutorialArrow2d != null)
            tutorialArrow2d.SetActive(false);
        if(tutorialArrow3d != null)
            tutorialArrow3d.SetActive(false);

        //unmask.fitTarget = null;
    }


    #region Saving/Loading

   
    #endregion
}

