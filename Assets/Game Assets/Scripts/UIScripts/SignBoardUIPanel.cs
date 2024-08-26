using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Windows;

public class SignBoardUIPanel : MonoBehaviour
{
    public TMP_InputField inputField;
    CanvasGroup canvasGroup;
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }
    private void OnEnable()
    {
        canvasGroup.DOFade(1, 0.3f).From(0);
        inputField.transform.parent.DOScale(1,0.2f).From(0).SetEase(Ease.OutBack);
    }
    public void Submit()
    {
        if (inputField.text == string.Empty)
            return;
        PlayerPrefs.SetString(SignBoard.NameSave, inputField.text);

        gameObject.SetActive(false);

    }

}
