using System;
using UnityEngine;

/// <summary>
/// One drag of a tool across the screen - a seed being planted, a sickle harvesting - acting on
/// every plot the pointer passes over. A plot is acted on once on the way in; moving off it and
/// back is a new visit, by when it has usually changed (planted, harvested) and declines.
///
/// The pointer can jump far between two frames on a fast swipe, so the path between them is
/// walked in short steps, and no plot it crossed is skipped.
///
/// Plain C#, with the screen-to-plot lookup and the action handed in, so it can be driven without a scene.
/// </summary>
public sealed class PlotSweep<TPlot> where TPlot : class
{
    readonly Func<Vector2, TPlot> plotAt;
    readonly Func<TPlot, bool> act;
    readonly Func<Vector2, bool> blocked;
    readonly float stepPixels;

    Vector2 last;
    bool started;
    TPlot current;

    /// <summary>Raised each time the action takes, with the plot and the screen point it took at.</summary>
    public event Action<TPlot, Vector2> Acted;

    /// <summary>How many plots this drag has acted on.</summary>
    public int Count { get; private set; }

    /// <param name="plotAt">The plot under a screen point, or null.</param>
    /// <param name="act">Does the tool's work on a plot; false when the plot declines.</param>
    /// <param name="blocked">True where the pointer is over something that hides the world, such as a panel.</param>
    /// <param name="stepPixels">How finely the path between two pointer positions is walked.</param>
    public PlotSweep(Func<Vector2, TPlot> plotAt, Func<TPlot, bool> act, Func<Vector2, bool> blocked = null, float stepPixels = 12f)
    {
        this.plotAt = plotAt ?? throw new ArgumentNullException(nameof(plotAt));
        this.act = act ?? throw new ArgumentNullException(nameof(act));
        this.blocked = blocked;
        this.stepPixels = Mathf.Max(1f, stepPixels);
    }

    /// <summary>The pointer is now at <paramref name="screen"/>: act on whatever it passed over getting there.</summary>
    public void MoveTo(Vector2 screen)
    {
        if (!started)
        {
            started = true;
            last = screen;
            Visit(screen);
            return;
        }

        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(last, screen) / stepPixels));
        for (int i = 1; i <= steps; i++) Visit(Vector2.Lerp(last, screen, i / (float)steps));
        last = screen;
    }

    void Visit(Vector2 screen)
    {
        TPlot plot = blocked != null && blocked(screen) ? null : plotAt(screen);
        if (ReferenceEquals(plot, current)) return;

        current = plot;
        if (plot == null || !act(plot)) return;

        Count++;
        Acted?.Invoke(plot, screen);
    }
}
