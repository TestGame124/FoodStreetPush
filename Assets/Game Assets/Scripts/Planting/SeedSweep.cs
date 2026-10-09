using System;
using UnityEngine;

/// <summary>
/// One drag of a seed across the screen: every plot the pointer passes over gets a seed, while
/// there are seeds left. A <see cref="PlotSweep{TPlot}"/> that plants.
/// </summary>
public sealed class SeedSweep
{
    readonly ItemDataSO seed;
    readonly PlotSweep<ISeedPlot> sweep;

    /// <summary>Raised for each plot planted, with the screen point it was planted at.</summary>
    public event Action<ISeedPlot, Vector2> Planted;

    /// <summary>How many seeds this drag has planted.</summary>
    public int Count => sweep.Count;

    public ItemDataSO Seed => seed;

    /// <param name="seed">What is being planted.</param>
    /// <param name="plotAt">The plot under a screen point, or null.</param>
    /// <param name="blocked">True where the pointer is over something that hides the world, such as the seed tray.</param>
    /// <param name="stepPixels">How finely the path between two pointer positions is walked.</param>
    public SeedSweep(ItemDataSO seed, Func<Vector2, ISeedPlot> plotAt, Func<Vector2, bool> blocked = null, float stepPixels = 12f)
    {
        this.seed = seed;
        sweep = new PlotSweep<ISeedPlot>(plotAt, plot => plot.CanPlant(seed) && plot.TryPlant(seed), blocked, stepPixels);
        sweep.Acted += (plot, screen) => Planted?.Invoke(plot, screen);
    }

    /// <summary>The pointer is now at <paramref name="screen"/>: plant whatever it passed over getting there.</summary>
    public void MoveTo(Vector2 screen) => sweep.MoveTo(screen);
}
