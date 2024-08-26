using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpUIPanel : MonoBehaviour
{
    [SerializeField] Transform rewardsPanel;
    [SerializeField] ItemSlotUI rewardItemSlotPrefab;

    [SerializeField] Button confirmButton;
    [SerializeField] Text levelUpText;
    
    CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        confirmButton.onClick.AddListener(() => ConfirmButton());

    }

    private void OnEnable()
    {
        levelUpText.text = $"Level Up To {LevelUpSystem.GetLevel()}";
    }
    public void OnLevelUp(IReward reward)
    {
        Debug.Log("Update Level up ui");
        ItemSlotUI slot = Instantiate(rewardItemSlotPrefab, rewardsPanel);
        slot.previewImage.sprite = reward.PreviewImage;
        if(reward.Amount > 0)
        {
            slot.amountText.SetText(reward.Amount.ToString());
        }
        else
        {
            slot.amountText.SetText("Unlocked");
        }
    }

    void ConfirmButton()
    {

        canvasGroup.DOFade(0, 0.3f).OnComplete(() =>
        {

            foreach (Transform item in rewardsPanel)
            {
                Destroy(item.gameObject);
            }
            
            LevelUpSystem.OnLevelUpFunc();
            gameObject.SetActive(false);
        });
    }
}
