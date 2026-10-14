using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorBehaviour : MonoBehaviour
{
    private const float closedDoorAngle = 90f;

    [SerializeField] Transform door; 

    // Trigger messages reach disabled behaviours too; switched off, the door is someone else's to swing.
    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled) return;

        if (other.CompareTag("Visitor"))
        {
            OpenDoor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isActiveAndEnabled) return;

        if (other.CompareTag("Visitor"))
        {
            CloseDoor();
        }
    }

    void OpenDoor()
    {
        door.DORotate(new Vector3(0, 0, 0), .4f);

    }

    void CloseDoor()
    {
        door.DORotate(new Vector3(0, closedDoorAngle, 0), .4f);

    }
}
