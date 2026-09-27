using Unity.Cinemachine;
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

        // Typed as the base class, not CinemachineCamera: it is what Priority, Follow and
        // LookAt live on, it lets the registry hold any rig (mixing, orbital, blend list),
        // and it is the type Cinemachine's own CM2 -> CM3 upgrader can repoint in place.
        public CinemachineVirtualCameraBase VirtualCamera;
    }

    public enum CameraType
    {
        Main,
        Focus
    }

    public CameraCase[] cameras;
    private static Dictionary<CameraType, CinemachineVirtualCameraBase> cameraData = new();

    private static CinemachineVirtualCameraBase currentCam;
    public static Camera mainCamera;

    private void Awake()
    {
        RegisterCameras();
    }

    private void RegisterCameras()
    {
        mainCamera = Camera.main;

        // Rebuilt rather than appended to: the dictionary is static, so reloading a scene
        // used to leave it holding the previous scene's destroyed cameras.
        cameraData.Clear();
        currentCam = null;

        foreach (CameraCase cam in cameras)
        {
            if (cam.VirtualCamera != null)
                cameraData[cam.type] = cam.VirtualCamera;
        }

        SetCameraToDefault();
    }

    public static CinemachineVirtualCameraBase GetCamera(CameraType type)
    {
        return cameraData.TryGetValue(type, out CinemachineVirtualCameraBase cam) ? cam : null;
    }
    
    public static void Focus(CameraType camType,Transform focusOn, Action onComp = null, float Yoffset = 0.5f)
    {
        

        if (!Activate(camType))
            return;

        currentCam.Follow = focusOn;
        currentCam.LookAt = focusOn;

        // Cinemachine 3 keeps the pipeline stages as plain components on the camera, so the
        // composer is a sibling rather than something fetched off a hidden child pipeline.
        // Not every rig carries one -- an orthographic fixed-yaw camera has no reason to --
        // and reading it unconditionally used to throw here.
        CinemachineRotationComposer composer = currentCam.GetComponent<CinemachineRotationComposer>();
        if (composer != null)
        {
            // CM2 screen coordinates ran 0..1 with 0.5 at the centre; CM3 runs -0.5..0.5
            // with 0 at the centre, so callers keep passing the values they always did.
            composer.Composition.ScreenPosition.y = Yoffset - 0.5f;
        }

        DOVirtual.DelayedCall(.5f, () => onComp?.Invoke(),false);
    }

    /// <summary>
    /// Returns to the main camera. It drives its own transform through CameraPanZoomRig, so
    /// unlike the focus cameras it wants no tracking target -- assigning one would hand its
    /// position back to Cinemachine and fight the rig.
    /// </summary>
    public static void SetCameraToDefault()
    {
        Activate(CameraType.Main);
    }

    private static bool Activate(CameraType camType)
    {
        CinemachineVirtualCameraBase next = GetCamera(camType);
        if (next == null)
        {
            Debug.LogError($"No camera is registered for {camType} on the CameraController.");
            return false;
        }

        if (currentCam != null)
            currentCam.Priority = 0;

        currentCam = next;
        currentCam.Priority = 100;
        return true;
    }
}
