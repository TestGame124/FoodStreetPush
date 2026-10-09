/// <summary>
/// Somewhere a seed can be planted by dragging it there: a plant pot, a bed, a field.
///
/// The seed drag only knows this, never what the plot is, so a new kind of plot needs no change
/// to the drag, and the drag can be swapped for another way of planting without touching plots.
/// </summary>
public interface ISeedPlot
{
    /// <summary>Whether <paramref name="seed"/> could go in here right now, leaving aside whether there is one to plant.</summary>
    bool CanPlant(ItemDataSO seed);

    /// <summary>
    /// Takes one <paramref name="seed"/> from the inventory and plants it. False, with nothing
    /// taken, when the plot cannot take it or there are none left.
    /// </summary>
    bool TryPlant(ItemDataSO seed);
}
