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
        if (PlayerPrefs.GetInt("FirstGame",0) == 0)
        {
            PlayerPrefs.SetInt("FirstGame", 1);
        //SaveController.RemoveAllSaveDevice();
        }
        loadingBar.DOFillAmount(1, loadingTime).From(0).OnComplete(() =>
        {
            SceneManager.LoadScene(1);
        });
    }
}
