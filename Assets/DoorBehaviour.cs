using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorBehaviour : MonoBehaviour
{
    private const float closedDoorAngle = 90f;

    [SerializeField] Transform door; 

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Visitor"))
        {
            OpenDoor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
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
