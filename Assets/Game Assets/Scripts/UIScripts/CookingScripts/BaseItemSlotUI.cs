using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseItemSlotUI : MonoBehaviour
{
    protected Item _item;
    public Item Item => _item;

    public enum SlotState
    {
        Empty,
        IsActive,
        Done
    }

    public SlotState currentState = SlotState.Empty;

    public void Initialize()
    {

    }
}
