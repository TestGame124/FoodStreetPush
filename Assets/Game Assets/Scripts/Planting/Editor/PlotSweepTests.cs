#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PlotSweepTests
{
    /// <summary>A pot 100 px wide along the screen's x axis, ripe or not.</summary>
    sealed class FakePot : IHarvestPlot
    {
        public bool Ripe;
        public int Harvests;

        public bool CanHarvest => Ripe;

        public bool TryHarvest()
        {
            if (!Ripe) return false;
            Ripe = false;
            Harvests++;
            return true;
        }
    }

    List<FakePot> pots;

    [SetUp] public void SetUp()
    {
        pots = new List<FakePot>();
        for (int i = 0; i < 5; i++) pots.Add(new FakePot { Ripe = true });
    }

    // Pots sit at x 100-199 ... 500-599, y 0-99.
    IHarvestPlot PotAt(Vector2 screen)
    {
        int i = Mathf.FloorToInt(screen.x / 100f) - 1;
        return screen.y >= 0f && screen.y < 100f && i >= 0 && i < pots.Count ? pots[i] : null;
    }

    PlotSweep<IHarvestPlot> Sickle() => new PlotSweep<IHarvestPlot>(PotAt, pot => pot.CanHarvest && pot.TryHarvest(), null, 10f);

    [Test] public void HarvestsEveryRipePotTheSickleCrosses()
    {
        PlotSweep<IHarvestPlot> sweep = Sickle();
        sweep.MoveTo(new Vector2(50f, 50f));
        sweep.MoveTo(new Vector2(650f, 50f));

        Assert.AreEqual(5, sweep.Count);
        foreach (FakePot pot in pots) Assert.AreEqual(1, pot.Harvests);
    }

    [Test] public void PassesOverPotsStillGrowing()
    {
        pots[1].Ripe = false;
        pots[3].Ripe = false;

        PlotSweep<IHarvestPlot> sweep = Sickle();
        sweep.MoveTo(new Vector2(50f, 50f));
        sweep.MoveTo(new Vector2(650f, 50f));

        Assert.AreEqual(3, sweep.Count);
        Assert.AreEqual(0, pots[1].Harvests);
        Assert.AreEqual(0, pots[3].Harvests);
    }

    [Test] public void SweepingBackHarvestsNothingTwice()
    {
        PlotSweep<IHarvestPlot> sweep = Sickle();
        sweep.MoveTo(new Vector2(150f, 50f));
        sweep.MoveTo(new Vector2(350f, 50f));
        sweep.MoveTo(new Vector2(150f, 50f));

        Assert.AreEqual(3, sweep.Count);
        foreach (FakePot pot in pots) Assert.That(pot.Harvests, Is.LessThanOrEqualTo(1));
    }

    [Test] public void NothingIsCutWhereTheWorldIsHidden()
    {
        var sweep = new PlotSweep<IHarvestPlot>(PotAt, pot => pot.TryHarvest(), screen => screen.x < 300f, 10f);
        sweep.MoveTo(new Vector2(150f, 50f));
        sweep.MoveTo(new Vector2(450f, 50f));

        Assert.AreEqual(0, pots[0].Harvests);
        Assert.AreEqual(0, pots[1].Harvests);
        Assert.AreEqual(2, sweep.Count);
    }

    [Test] public void NeedsSomewhereToLookAndSomethingToDo()
    {
        Assert.Throws<System.ArgumentNullException>(() => new PlotSweep<IHarvestPlot>(null, pot => true));
        Assert.Throws<System.ArgumentNullException>(() => new PlotSweep<IHarvestPlot>(PotAt, null));
    }
}
#endif
