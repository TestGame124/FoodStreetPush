using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IReward
{
    public Sprite PreviewImage { get; set; }
    public int Amount { get; set; }
    
    public void GiveReward();
}
