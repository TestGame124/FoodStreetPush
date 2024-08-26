using Cinemachine;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Transactions;
using UnityEngine;
using UnityEngine.UIElements;

public class CameraController : MonoBehaviour
{

    [Serializable]
    public struct CameraCase
    {
        public CameraType type;
        public CinemachineVirtualCamera VirtualCamera;
    }

    public enum CameraType
    {
        Main,
        Focus,
        Tutorial
    }

    public CameraCase[] cameras;
    private static Dictionary<CameraType, CinemachineVirtualCamera> cameraData = new();

    [SerializeField] Transform _mainTarget;
    public static Transform MainTarget;
    private static CinemachineVirtualCamera currentCam;
    public static Camera mainCamera;

    private void Awake()
    {
        RegisterCameras();
    }

    private void RegisterCameras()
    {
        mainCamera = Camera.main;
        foreach (CameraCase cam in cameras)
        {
            if (!cameraData.ContainsKey(cam.type))
                cameraData.Add(cam.type, cam.VirtualCamera);

        }

        MainTarget = _mainTarget;
        SetCameraToDefault();
    }

    public static CinemachineVirtualCamera GetCamera(CameraType type)
    {
        return cameraData[type];
    }
    
    public static void Focus(CameraType camType,Transform focusOn, Action onComp = null, float Yoffset = 0.5f)
    {
        

        if(currentCam != null)
            currentCam.Priority = 0;

            currentCam = GetCamera(camType);
        
           currentCam.m_Follow = focusOn;
        currentCam.m_LookAt = focusOn;
        
        currentCam.GetCinemachineComponent<CinemachineComposer>().m_ScreenY = Yoffset;
        currentCam.Priority = 100;

        DOVirtual.DelayedCall(.5f, () => onComp?.Invoke(),false);
    }


    public static void SetCameraToDefault()
    {
        Focus(CameraType.Main, MainTarget);
    }
}
