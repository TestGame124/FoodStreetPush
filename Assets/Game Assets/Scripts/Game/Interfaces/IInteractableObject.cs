using UnityEngine;

/// <summary>
/// Something the player can tap during play.
/// </summary>
public interface IInteractableObject
{
    /// <summary>
    /// Handle a tap that Mouse3D has already confirmed: it is a real tap rather than a camera
    /// drag or a pinch, and it neither began nor ended on the UI. There is nothing left to
    /// re-check here.
    /// </summary>
    /// <returns>
    /// True when the tap was acted on. Return false to decline it -- a station that is mid-cook
    /// or otherwise not interactive right now -- and the tap carries on to whatever stands behind
    /// this object, and then to the systems that run later in the frame.
    /// </returns>
    bool OnTouch();
}
