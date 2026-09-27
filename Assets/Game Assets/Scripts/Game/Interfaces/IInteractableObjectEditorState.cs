using UnityEngine;

/// <summary>
/// Something the player can tap while the build/edit mode is open.
/// </summary>
public interface IInteractableObjectEditorState
{
    /// <summary>
    /// Handle a tap that Mouse3D has already confirmed. See <see cref="IInteractableObject"/>.
    /// </summary>
    /// <returns>
    /// True when the tap was acted on. Declining matters most here: a floor tile, or any object
    /// tapped while a building is already being positioned, must let the tap through so
    /// BuildingGhost can use that same release to drop the building.
    /// </returns>
    bool OnTouch();
}
