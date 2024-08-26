using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SignBoard : MonoBehaviour, IInteractableObject
{
    [SerializeField] TextMeshPro signText;

    public const string NameSave = "nameSave";

    public void Initialize()
    {
        signText.SetText(PlayerPrefs.GetString(NameSave));
        UIGame.GetUI().signBoardUI.inputField.onValueChanged.AddListener(AssignName);

    }

    public void OnTouch()
    {
        UIGame.GetUI().signBoardUI.gameObject.SetActive(true);

        if (!TutorialController.TutorialCompleted())
        {
            TutorialController.OnRestaurantNameSubmit?.Invoke();
        }

    }





    private void AssignName(string input)
    {
        signText.SetText(input);

    }

    
}
