using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// DEPRECATED. Replaced by <see cref="CameraPanZoomRig"/>, which moves the CinemachineCamera
/// directly instead of having it chase this proxy object.
///
/// All behaviour is gone; only the serialized fields remain, so that
/// "Tools > Camera > Convert All Scenes To Pan-Zoom Rig" can read the values you tuned and
/// carry them across. Delete this file, its .meta and CameraRigMigration.cs once every scene
/// has been converted.
/// </summary>
[AddComponentMenu("")]
public class CameraTarget : MonoBehaviour
{
    // Read only by the migration tool, through SerializedObject.
#pragma warning disable 0414, 0169
    [SerializeField] float zoomSpeed = 10f;
    [SerializeField] float minZoom = 10f;
    [SerializeField] float maxZoom = 100f;
    [SerializeField] float DefaultZoom = 20f;

    [SerializeField] Vector2 boundsCenter;
    [SerializeField] Vector2 boundsSize;

    [FormerlySerializedAs("collider")]
    [SerializeField] BoxCollider legacyBoundsCollider;

    [SerializeField] float movementTime;
    [SerializeField] LayerMask cameraLayer;
#pragma warning restore 0414, 0169

    void Awake()
    {
        Debug.LogWarning(
            $"{name} still carries the deprecated CameraTarget component. Run "
            + "Tools > Camera > Convert All Scenes To Pan-Zoom Rig to finish the migration.",
            this);

        enabled = false;
    }
}
