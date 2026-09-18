using System.Collections;
using System.Collections.Generic;
using FoodStreet.Saving;
using TMPro;
using UnityEngine;

public class SignBoard : MonoBehaviour, IInteractableObject
{
    [SerializeField] TextMeshPro signText;

    /// <summary>
    /// The restaurant name now lives in the save file rather than PlayerPrefs,
    /// so clearing the save clears the name with it.
    /// </summary>
    public static string RestaurantName
    {
        get
        {
            return SaveSystem.IsReady ? SaveSystem.Data.profile.restaurantName : string.Empty;
        }
        set
        {
            if (!SaveSystem.IsReady)
                return;

            string name = value ?? string.Empty;
            if (SaveSystem.Data.profile.restaurantName == name)
                return;

            SaveSystem.Data.profile.restaurantName = name;
            SaveSystem.MarkDirty();
        }
    }

    public void Initialize()
    {
        signText.SetText(RestaurantName);
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
        RestaurantName = input;
    }

    
}
