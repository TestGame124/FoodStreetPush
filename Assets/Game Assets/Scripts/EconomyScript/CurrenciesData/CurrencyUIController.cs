using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CurrencyUIController : MonoBehaviour
{

    [Header("Currency UI")]
    [SerializeField] Text CoinsText;
    [SerializeField] Text GemsText;

    [Header("Level UI")]
    [SerializeField] Text levelText;
    [SerializeField] Text xpText;

    [SerializeField] Image levelFill;


    private void OnEnable()
    {

        CurrenciesController.OnCurrencyAmountChanged += RedrawUI;
        LevelUpSystem.OnXpChanged += RedrawXpUi;

    }

    private void OnDisable()
    {
        CurrenciesController.OnCurrencyAmountChanged -= RedrawUI;

    }
   
    public void RedrawUI(CurrencyType type,int amount)
    {
        switch(type)
        {
            case CurrencyType.Coins:
                CoinsText.text = amount.ToString(); break;
            case CurrencyType.Gems:
                GemsText.text = amount.ToString(); break;
        }
    }

    public void RedrawXpUi(int level, float xp, float xpToNext)
    {
        levelText.text = level.ToString();
        xpText.text = xp.ToString();
       
        float fill = (xp / xpToNext);
        if(levelFill.fillAmount > fill)
        {
            levelFill.fillAmount = 0;
        }
        levelFill.DOFillAmount(fill, 0.5f);
        //levelFill.fillAmount = xp/xpToNext;
    }
}
