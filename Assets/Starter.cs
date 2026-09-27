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
        loadingBar.DOFillAmount(1, loadingTime).From(0).OnComplete(() =>
        {
            SceneManager.LoadScene(1);
        });
    }
}
