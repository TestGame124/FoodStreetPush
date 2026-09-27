using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using CodeMonkey.Utils;

public class CinemachineShake : MonoBehaviour {

    public static CinemachineShake Instance { get; private set; }

    // Cinemachine 3 puts the noise module on the camera itself rather than on a hidden
    // pipeline child, so it is a sibling component here.
    private CinemachineBasicMultiChannelPerlin noise;

    private void Awake() {
        Instance = this;
        noise = GetComponent<CinemachineBasicMultiChannelPerlin>();
    }

    public void ScreenShake() {
        if (noise == null)
            return;

        noise.AmplitudeGain = 1f;

        FunctionTimer.Create(() => { if (noise != null) noise.AmplitudeGain = 0f; }, .1f);
    }

}
