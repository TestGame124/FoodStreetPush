#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SeedSweepTests
{
    /// <summary>A plot 100 px wide on a row of plots along the screen's x axis, sharing one seed stock.</summary>
    sealed class FakePlot : ISeedPlot
    {
        readonly Stock stock;
        public int Plantings;

        public FakePlot(Stock stock) => this.stock = stock;

        public bool CanPlant(ItemDataSO seed) => Plantings == 0;

        public bool TryPlant(ItemDataSO seed)
        {
            if (!CanPlant(seed) || stock.Seeds < 1) return false;
            stock.Seeds--;
            Plantings++;
            return true;
        }
    }

    sealed class Stock
    {
        public int Seeds;
    }

    ItemDataSO seed;
    Stock stock;
    List<FakePlot> plots;

    [SetUp] public void SetUp()
    {
        seed = ScriptableObject.CreateInstance<ItemDataSO>();
        seed.canBePlanted = true;
        stock = new Stock { Seeds = 10 };
        plots = new List<FakePlot>();
        for (int i = 0; i < 5; i++) plots.Add(new FakePlot(stock));
    }

    [TearDown] public void TearDown() => Object.DestroyImmediate(seed);

    // Plots sit at x 100-199, 200-299 ... 500-599, y 0-99; the tray is everything above y 500.
    ISeedPlot PlotAt(Vector2 screen)
    {
        int i = Mathf.FloorToInt(screen.x / 100f) - 1;
        return screen.y >= 0f && screen.y < 100f && i >= 0 && i < plots.Count ? plots[i] : null;
    }

    static bool OverTray(Vector2 screen) => screen.y > 500f;

    SeedSweep Sweep() => new SeedSweep(seed, PlotAt, OverTray, 10f);

    [Test] public void PlantsEveryPlotThePointerCrosses()
    {
        SeedSweep sweep = Sweep();
        sweep.MoveTo(new Vector2(50f, 50f));
        for (float x = 60f; x <= 650f; x += 7f) sweep.MoveTo(new Vector2(x, 50f));

        Assert.AreEqual(5, sweep.Count);
        foreach (FakePlot plot in plots) Assert.AreEqual(1, plot.Plantings);
        Assert.AreEqual(5, stock.Seeds);
    }

    [Test] public void AFastSwipeSkipsNothing()
    {
        SeedSweep sweep = Sweep();
        sweep.MoveTo(new Vector2(50f, 50f));
        sweep.MoveTo(new Vector2(650f, 50f));

        Assert.AreEqual(5, sweep.Count);
    }

    [Test] public void GoingBackOverAPlantedPlotPlantsNothingMore()
    {
        SeedSweep sweep = Sweep();
        sweep.MoveTo(new Vector2(150f, 50f));
        sweep.MoveTo(new Vector2(50f, 50f));
        sweep.MoveTo(new Vector2(150f, 50f));

        Assert.AreEqual(1, sweep.Count);
        Assert.AreEqual(1, plots[0].Plantings);
    }

    [Test] public void NothingIsPlantedThroughTheTray()
    {
        SeedSweep sweep = new SeedSweep(seed, screen => plots[0], OverTray, 10f);
        sweep.MoveTo(new Vector2(150f, 700f));
        sweep.MoveTo(new Vector2(150f, 600f));

        Assert.AreEqual(0, sweep.Count);

        sweep.MoveTo(new Vector2(150f, 400f));
        Assert.AreEqual(1, sweep.Count);
    }

    [Test] public void StopsPlantingWhenTheSeedsRunOut()
    {
        stock.Seeds = 2;
        SeedSweep sweep = Sweep();
        sweep.MoveTo(new Vector2(50f, 50f));
        sweep.MoveTo(new Vector2(650f, 50f));

        Assert.AreEqual(2, sweep.Count);
        Assert.AreEqual(0, stock.Seeds);
        Assert.AreEqual(0, plots[4].Plantings);
    }

    [Test] public void ReportsEachPlanting()
    {
        var planted = new List<ISeedPlot>();
        SeedSweep sweep = Sweep();
        sweep.Planted += (plot, screen) => planted.Add(plot);

        sweep.MoveTo(new Vector2(150f, 50f));
        sweep.MoveTo(new Vector2(350f, 50f));

        CollectionAssert.AreEqual(new ISeedPlot[] { plots[0], plots[1], plots[2] }, planted);
    }
}
#endif
