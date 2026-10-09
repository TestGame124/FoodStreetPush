namespace FoodStreet.Placement
{
    /// <summary>
    /// A behaviour on a building's visual that keeps running on the ghost while it is dragged over
    /// the grid. The ghost switches every other script off; one of these only changes how the
    /// preview looks for where it is - a defense standing up on the wall it is held over.
    /// </summary>
    public interface IPlacementPreview
    {
    }
}
