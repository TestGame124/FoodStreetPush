/// <summary>
/// Somewhere a ripe crop can be harvested by sweeping a tool over it: a plant pot, a bed, a field.
/// The sickle only knows this, never what the plot is.
/// </summary>
public interface IHarvestPlot
{
    /// <summary>There is a ripe crop here to pick.</summary>
    bool CanHarvest { get; }

    /// <summary>Picks the crop. False, with nothing done, when there is nothing ripe here.</summary>
    bool TryHarvest();
}
