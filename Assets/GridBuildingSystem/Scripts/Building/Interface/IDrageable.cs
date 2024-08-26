using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDrageable
{

    public void OnBeginDrag();
    public void OnDrag();
    public void OnEndDrag();

}
