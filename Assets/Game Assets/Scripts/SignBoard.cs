using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SignBoard : MonoBehaviour, IInteractableObject
{
    [SerializeField] TextMeshPro signText;

    /// <summary>
    /// The restaurant name, for the length of the session only. Nothing is
    /// persisted, so it starts empty on every launch.
    /// </summary>
    public static string RestaurantName
    {
        get => restaurantName;
        set => restaurantName = value ?? string.Empty;
    }

    private static string restaurantName = string.Empty;

    public void Initialize()
    {
        signText.SetText(RestaurantName);
        UIGame.GetUI().signBoardUI.inputField.onValueChanged.AddListener(AssignName);

    }

    public bool OnTouch()
    {
        UIGame ui = UIGame.GetUI();
        if (ui == null || ui.signBoardUI == null) return false;

        ui.signBoardUI.gameObject.SetActive(true);
        return true;
    }





    private void AssignName(string input)
    {
        signText.SetText(input);
        RestaurantName = input;
    }

    
}
