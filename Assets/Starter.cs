using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Starter : MonoBehaviour
{

    [SerializeField] Image loadingBar;
    [SerializeField] int loadingTime = 3;
    private void Start()
    {
        // Loading the save here rather than in the game scene means the file is
        // read, verified and decrypted while the loading bar is already on
        // screen, instead of stalling the first frame of gameplay.
        FoodStreet.Saving.SaveSystem.Initialize();

        loadingBar.DOFillAmount(1, loadingTime).From(0).OnComplete(() =>
        {
            SceneManager.LoadScene(1);
        });
    }
}
