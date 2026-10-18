using System.Collections.Generic;
using System.Text;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Effects;
using LastOrders.Expansion;
using LastOrders.Jobs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using B = LastOrders.EditorTools.BuildingKit;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;
using Kind = LastOrders.EditorTools.ObstacleModels.Kind;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Lays out the land round the compound in plots to clear and claim, the way the courtyard is:
    /// each plot is an <see cref="ExpansionArea"/> covered in obstacles (<see cref="ObstacleModels"/>)
    /// that a worker sent out clears one at a time. Once a plot is clear the player pays to expand
    /// into it: a wall goes up all round it, with a gate facing the safe zone's, and the land is
    /// inside.
    ///
    /// The plots start across the defense ring (<see cref="PerimeterStripLayout.Ring"/>) from the
    /// safe zone's walls, never against them: the wall, then open ground the player builds defenses
    /// on, then the land to claim. Claiming one leaves the wall, the ring and what was built in them
    /// alone; the claimed plot is walled ground of its own out past the ring, reached from a gate in
    /// the safe zone's wall straight across from its own.
    ///
    /// Three rings of plots on the three sides away from the road (none on the road side: that is the
    /// customers'), every plot touching the ones round it. The first ring - two plots west of the
    /// compound, two north beside the courtyard, two east - faces gates in the safe zone's wall
    /// across the ring; its corner plots and every plot further out open from the plot in front of
    /// them, through a gate in that plot's wall, so the land is taken ring by ring. Outer plots are
    /// bigger, dearer and need a higher level. The splits between plots run down the night's lanes,
    /// so each trail runs between two plots rather than across one. Every plot is staked out with
    /// twine until it is claimed.
    ///
    /// Claimed land joins the restaurant's: a first-ring plot walls in the stretch of defense ring
    /// in front of it too (<see cref="Walled"/>), its walls running across the ring to the safe
    /// zone's, so it is inside the same walls - reached through the safe zone's gate, with the
    /// defenses in that stretch of ring behind it. Two claimed plots side by side have no wall
    /// between them, and a plot claimed beside one that is already the restaurant's does not raise
    /// one: each wall section knows the land beyond it (<see cref="ExpansionNeighbours"/>) and stands
    /// only while that land is not claimed too. So the claimed land grows out as one piece.
    ///
    /// The courtyard (<see cref="ExpansionSceneSetup"/>) is in the layout as an outside plot: plots
    /// open from it through gates its own setup leaves in its wall (<see cref="GatesInto"/>).
    ///
    /// Re-running rebuilds every plot from this layout - hand edits to them are lost - removes the
    /// world edge's loose props from the ground the plots cover, and lays the compound walls again.
    /// Run it again after 'Set Up Courtyard', 'Set Up Visitor System' or a world-edge rebuild.
    /// </summary>
    public static class LandPlotSetup
    {
        public const string PlotFolder = ExpansionContentGenerator.Folder + "/Plots";
        public const string DebrisFolder = ExpansionContentGenerator.Folder + "/Obstacles";
        const string StakeFolder = ObstacleModels.Folder + "/Plots";

        const string AreasRootName = "ExpansionAreas";
        const string WallTemplatePath = "Restaurant/Zone/OuterBoundries/Cube (5)";

        /// <summary>How far each plot reaches out from the defense ring.</summary>
        const float Depth = 12f;

        /// <summary>The safe zone's own wall, standing for good, across a plot's front: its gate the way out to the plot.</summary>
        public const string CompoundFrontName = "CompoundFront";

        const float GateWidth = 3f;

        /// <summary>How far either side of a gate the way through it is walked from and to.</summary>
        const float GateStep = 1.6f;

        /// <summary>Length of one section of a plot's new wall, as the courtyard's.</summary>
        const float SectionLength = 2.5f;

        /// <summary>From every edge of a plot to its obstacles: the ring and the stakes stay clear of them.</summary>
        const float EdgeClear = 0.9f;

        /// <summary>Between two obstacles' footprints: room for a worker to stand and swing.</summary>
        const float Spacing = 0.6f;

        /// <summary>From a trail's line to an obstacle's footprint: off the worn dirt. Zombies walk the line and through whatever is on it.</summary>
        const float TrailClear = 2.3f;

        /// <summary>The way in from a gate, kept clear for this far into the plot.</summary>
        const float Approach = 3.5f;

        const float ApproachClear = 1.2f;

        const float StakeSpacing = 3f;

        const int Seed = 20261002;

        static readonly Color StakeWood = B.Hex("9C846C");
        static readonly Color StakeTip = B.Hex("C8553D");

        // --- The layout ---------------------------------------------------------------------------

        /// <summary>The sides of a rectangle in the order a walk round it anticlockwise, seen from above, meets them.</summary>
        public enum Side { South, East, North, West }

        public sealed class Plot
        {
            public string Id;
            public string Name;
            public string DisplayName;
            public string ShortName;
            public Rect Land;

            /// <summary>The side it is entered through: facing the compound across the ring, or against the plot it opens from.</summary>
            public Side Front;

            /// <summary>
            /// Where along <see cref="Front"/> the gate is, in world metres along that side. A plot facing
            /// the compound has the compound's gate straight across the ring from its own.
            /// </summary>
            public float Gate;

            /// <summary>
            /// The plot that has to be claimed first, and is walked through to reach this one - its gate
            /// is in that plot's wall; null for one facing the compound.
            /// </summary>
            public string UnlockedBy;

            /// <summary>Laid out by another setup (the courtyard): routes and gates pass through it, but nothing here builds it.</summary>
            public bool External;

            /// <summary>0 for the ring next to the defense ring, 1 and 2 further out: what it costs and needs.</summary>
            public int Ring;

            public int Level;
            public int Wood;
            public int Scrap;
            public float Seconds;
            public int ClaimExperience;
            public int ClaimWood;
            public Kind[] Obstacles;
        }

        /// <summary>The courtyard's id, as a plot other plots open from.</summary>
        public const string CourtyardId = "courtyard";

        /// <summary>How deep each ring of plots is, out from the one inside it: the first as deep as it always was.</summary>
        public static readonly float[] RingDepth = { Depth, 16f, 18f };

        /// <summary>The north rows' depths past the courtyard's back edge, for the second and third rings.</summary>
        const float NorthRowB = 16f, NorthRowC = 18f;

        // What each kind of land is mostly covered in.
        static readonly Kind[] Scrapyard = { Kind.ScrapHeap, Kind.CarWreck, Kind.ScrapHeap, Kind.Rubble, Kind.Barricade, Kind.ScrapHeap, Kind.Thicket };
        static readonly Kind[] CarLot = { Kind.CarWreck, Kind.CarWreck, Kind.ScrapHeap, Kind.CarWreck, Kind.Rubble, Kind.Barricade, Kind.Thicket };
        static readonly Kind[] Allotments = { Kind.Thicket, Kind.Stump, Kind.Barricade, Kind.Thicket, Kind.Rubble, Kind.Boulders, Kind.Thicket };
        static readonly Kind[] BuildersYard = { Kind.Rubble, Kind.Barricade, Kind.Rubble, Kind.ScrapHeap, Kind.Boulders, Kind.Rubble, Kind.CarWreck };
        static readonly Kind[] Brambles = { Kind.Thicket, Kind.Thicket, Kind.Stump, Kind.Thicket, Kind.Boulders, Kind.Thicket, Kind.FallenTree };
        static readonly Kind[] Woodlot = { Kind.FallenTree, Kind.Stump, Kind.FallenTree, Kind.Thicket, Kind.Stump, Kind.FallenTree, Kind.Boulders, Kind.Thicket };
        static readonly Kind[] Orchard = { Kind.FallenTree, Kind.Stump, Kind.Stump, Kind.Thicket, Kind.FallenTree, Kind.Boulders, Kind.Thicket };
        static readonly Kind[] Meadow = { Kind.Boulders, Kind.Thicket, Kind.Stump, Kind.FallenTree, Kind.Thicket, Kind.Rubble, Kind.Boulders };
        static readonly Kind[] Quarry = { Kind.Boulders, Kind.Boulders, Kind.Rubble, Kind.Boulders, Kind.Thicket, Kind.Rubble, Kind.Stump };
        static readonly Kind[] Woods = { Kind.FallenTree, Kind.FallenTree, Kind.Stump, Kind.Thicket, Kind.FallenTree, Kind.Stump, Kind.Boulders, Kind.Thicket };
        static readonly Kind[] Garage = { Kind.CarWreck, Kind.Barricade, Kind.ScrapHeap, Kind.Rubble, Kind.CarWreck, Kind.Thicket, Kind.Rubble };

        /// <summary>
        /// The plots, measured from the compound's walls and the courtyard, across the defense ring
        /// from the walls, split down the lanes. Gate positions on the first ring were picked to open
        /// onto clear ground inside the compound: between the shelters and the north-west tower, north
        /// of the hospital tent, clear of the corner towers and the car park. Further out a gate
        /// stands in the middle of the stretch a plot shares with the one it opens from. The courtyard
        /// is in the list, <see cref="Plot.External"/>.
        /// </summary>
        public static List<Plot> Layout(Rect compound, Rect courtyard)
        {
            float west = compound.xMin, east = compound.xMax, south = compound.yMin, north = compound.yMax;

            // The plots' inner edges: the far side of the ring. The road side has no ring and no plots.
            float westIn = west - PerimeterStripLayout.Ring, eastIn = east + PerimeterStripLayout.Ring;
            float northIn = north + PerimeterStripLayout.Ring;

            float top = courtyard.height > 1f ? courtyard.yMax : northIn + 12.3f;
            float yardWest = courtyard.width > 1f ? courtyard.xMin : 21.5f;
            float yardEast = courtyard.width > 1f ? courtyard.xMax : eastIn;

            // Where the plots split: down the lanes, so their trails run along the lines between plots.
            float w1Lo = L.WestLaneZ, w1Hi = L.WestLaneZ;
            float w2Lo = L.NorthWestLaneZ, w2Hi = L.NorthWestLaneZ;
            float e1Lo = L.EastLaneZ, e1Hi = L.EastLaneZ;
            float e2Lo = L.NorthEastLaneZ, e2Hi = L.NorthEastLaneZ;
            float nLo = L.NorthLaneX, nHi = L.NorthLaneX;

            // Columns out from the ring, west and east; rows out past the courtyard, north.
            float wA = westIn - RingDepth[0], wB = wA - RingDepth[1], wC = wB - RingDepth[2];
            float eA = eastIn + RingDepth[0], eB = eA + RingDepth[1], eC = eB + RingDepth[2];
            float nB = top + NorthRowB, nC = nB + NorthRowC;

            // Where the north rows split over the courtyard.
            float yardSplit = Mathf.Round((nHi + yardEast) * 0.5f * 10f) * 0.1f;

            var plots = new List<Plot>
            {
                // --- The first ring: facing the compound across the defense ring ---------------------

                Make("plot_south_west", "SouthWest", "South-west lot", Rect.MinMaxRect(wA, south, westIn, w1Lo), Side.East, 11.5f, null, 0,
                     Kind.Barricade, Kind.CarWreck, Kind.Rubble, Kind.ScrapHeap, Kind.Thicket),
                Make("plot_west", "West", "West lot", Rect.MinMaxRect(wA, w1Hi, westIn, w2Lo), Side.East, 30.5f, null, 0,
                     Kind.CarWreck, Kind.ScrapHeap, Kind.Thicket, Kind.Rubble, Kind.Boulders, Kind.Stump),
                Make("plot_north_west", "NorthWest", "North-west field", Rect.MinMaxRect(westIn, northIn, nLo, top), Side.South, -1.5f, null, 0,
                     Kind.FallenTree, Kind.Boulders, Kind.Thicket, Kind.Stump, Kind.Boulders, Kind.Thicket),
                Make("plot_north", "North", "North field", Rect.MinMaxRect(nHi, northIn, yardWest, top), Side.South, 19.5f, null, 0,
                     Kind.FallenTree, Kind.Stump, Kind.Thicket, Kind.Boulders),
                new Plot
                {
                    Id = CourtyardId, Name = "Courtyard", DisplayName = "Courtyard", ShortName = "Courtyard", External = true,
                    Land = Rect.MinMaxRect(yardWest, northIn, yardEast, top), Front = Side.South, Gate = ExpansionSceneSetup.GapCentreX,
                    Obstacles = CourtyardObstacles,
                },
                Make("plot_east", "East", "East lot", Rect.MinMaxRect(eastIn, e1Hi, eA, e2Lo), Side.West, 27.5f, null, 0,
                     Kind.CarWreck, Kind.Barricade, Kind.ScrapHeap, Kind.Thicket, Kind.Stump, Kind.Boulders),
                Make("plot_south_east", "SouthEast", "South-east lot", Rect.MinMaxRect(eastIn, south, eA, e1Lo), Side.West, 8f, null, 0,
                     Kind.CarWreck, Kind.Rubble, Kind.ScrapHeap, Kind.Barricade, Kind.Thicket),

                // The corners past the north-west and north-east corridors, opened from beside them.
                Make("plot_far_west", "FarWest", "Far west corner", Rect.MinMaxRect(wA, w2Hi, westIn, top), Side.East, 0f, "plot_north_west", 0,
                     Kind.FallenTree, Kind.Boulders, Kind.Thicket, Kind.Stump, Kind.Rubble),
                Make("plot_far_east", "FarEast", "Far east corner", Rect.MinMaxRect(eastIn, e2Hi, eA, top), Side.West, 0f, CourtyardId, 0,
                     Kind.CarWreck, Kind.FallenTree, Kind.Boulders, Kind.Thicket, Kind.ScrapHeap),

                // --- West, out toward the suburbs -----------------------------------------------------

                Make("plot_west_scrapyard", "WestScrapyard", "West scrapyard", Rect.MinMaxRect(wB, south, wA, w1Lo), Side.East, 0f, "plot_south_west", 1, Scrapyard),
                Make("plot_west_car_lot", "WestCarLot", "Old car lot", Rect.MinMaxRect(wC, south, wB, w1Lo), Side.East, 0f, "plot_west_scrapyard", 2, CarLot),
                Make("plot_west_allotments", "WestAllotments", "West allotments", Rect.MinMaxRect(wB, w1Hi, wA, w2Lo), Side.East, 0f, "plot_west", 1, Allotments),
                Make("plot_west_builders_yard", "WestBuildersYard", "Builders' yard", Rect.MinMaxRect(wC, w1Hi, wB, w2Lo), Side.East, 0f,
                     "plot_west_allotments", 2, BuildersYard),

                // The north-west corner, out from the far west corner.
                Make("plot_west_brambles", "WestBrambles", "Bramble patch", Rect.MinMaxRect(wB, w2Hi, wA, top), Side.East, 0f, "plot_far_west", 1, Brambles),
                Make("plot_west_woodlot", "WestWoodlot", "West woodlot", Rect.MinMaxRect(wC, w2Hi, wB, nB), Side.East, 0f, "plot_west_brambles", 2, Woodlot),
                Make("plot_west_orchard", "WestOrchard", "Old orchard", Rect.MinMaxRect(wA, top, westIn, nB), Side.South, 0f, "plot_far_west", 1, Orchard),
                Make("plot_west_meadow", "WestMeadow", "Hay meadow", Rect.MinMaxRect(wB, top, wA, nB), Side.South, 0f, "plot_west_brambles", 1, Meadow),
                Make("plot_north_west_woods", "NorthWestWoods", "North-west woods", Rect.MinMaxRect(wB, nB, westIn, nC), Side.South, 0f,
                     "plot_west_orchard", 2, Woods),
                Make("plot_west_quarry", "WestQuarry", "Stone quarry", Rect.MinMaxRect(wC, nB, wB, nC), Side.South, 0f, "plot_west_woodlot", 2, Quarry),

                // --- North, out toward the trees -------------------------------------------------------

                Make("plot_back_paddock", "BackPaddock", "Back paddock", Rect.MinMaxRect(westIn, top, nLo, nB), Side.South, 0f, "plot_north_west", 1, Meadow),
                Make("plot_kitchen_field", "KitchenField", "Kitchen field", Rect.MinMaxRect(nHi, top, yardSplit, nB), Side.South, 0f, "plot_north", 1, Allotments),
                Make("plot_yard_orchard", "YardOrchard", "Courtyard orchard", Rect.MinMaxRect(yardSplit, top, eastIn, nB), Side.South, 0f, CourtyardId, 1, Orchard),
                Make("plot_pine_stand", "PineStand", "Pine stand", Rect.MinMaxRect(westIn, nB, nLo, nC), Side.South, 0f, "plot_back_paddock", 2, Woods),
                Make("plot_forest_edge", "ForestEdge", "Forest edge", Rect.MinMaxRect(nHi, nB, yardSplit, nC), Side.South, 0f, "plot_kitchen_field", 2, Woodlot),
                Make("plot_deadfall", "Deadfall", "Deadfall", Rect.MinMaxRect(yardSplit, nB, eastIn, nC), Side.South, 0f, "plot_yard_orchard", 2, Woods),

                // --- East, out toward the suburbs ------------------------------------------------------

                Make("plot_east_garage", "EastGarage", "Burnt-out garage", Rect.MinMaxRect(eA, south, eB, e1Lo), Side.West, 0f, "plot_south_east", 1, Garage),
                Make("plot_east_wreckers", "EastWreckers", "Wreckers' yard", Rect.MinMaxRect(eB, south, eC, e1Lo), Side.West, 0f, "plot_east_garage", 2, CarLot),
                Make("plot_east_allotments", "EastAllotments", "East allotments", Rect.MinMaxRect(eA, e1Hi, eB, e2Lo), Side.West, 0f, "plot_east", 1, Allotments),
                Make("plot_east_rubble", "EastRubble", "Rubble field", Rect.MinMaxRect(eB, e1Hi, eC, e2Lo), Side.West, 0f, "plot_east_allotments", 2, BuildersYard),

                // The north-east corner, out from the far east corner.
                Make("plot_east_brambles", "EastBrambles", "East brambles", Rect.MinMaxRect(eA, e2Hi, eB, top), Side.West, 0f, "plot_far_east", 1, Brambles),
                Make("plot_east_woodlot", "EastWoodlot", "East woodlot", Rect.MinMaxRect(eB, e2Hi, eC, nB), Side.West, 0f, "plot_east_brambles", 2, Woodlot),
                Make("plot_east_orchard", "EastOrchard", "Pear orchard", Rect.MinMaxRect(eastIn, top, eA, nB), Side.South, 0f, "plot_far_east", 1, Orchard),
                Make("plot_east_meadow", "EastMeadow", "East meadow", Rect.MinMaxRect(eA, top, eB, nB), Side.South, 0f, "plot_east_brambles", 1, Meadow),
                Make("plot_north_east_woods", "NorthEastWoods", "North-east woods", Rect.MinMaxRect(eastIn, nB, eB, nC), Side.South, 0f,
                     "plot_east_orchard", 2, Woods),
                Make("plot_east_boulders", "EastBoulders", "Boulder field", Rect.MinMaxRect(eB, nB, eC, nC), Side.South, 0f, "plot_east_woodlot", 2, Quarry),
            };

            var byId = new Dictionary<string, Plot>();
            foreach (Plot plot in plots) byId[plot.Id] = plot;

            // A gate stays well inside the side it is on, whatever the walls have done - and for a plot
            // facing the compound, well inside the stretch of compound wall across from it too. One
            // opened from another plot stands in the middle of the stretch the two share.
            foreach (Plot plot in plots)
            {
                var (_, _, from, to) = SideLine(plot.Land, plot.Front);
                float low = Mathf.Min(from, to) + GateWidth, high = Mathf.Max(from, to) - GateWidth;

                if (plot.UnlockedBy == null)
                {
                    var (_, _, wallFrom, wallTo) = SideLine(compound, Opposite(plot.Front));
                    low = Mathf.Max(low, Mathf.Min(wallFrom, wallTo) + GateWidth);
                    high = Mathf.Min(high, Mathf.Max(wallFrom, wallTo) - GateWidth);
                }
                else if (byId.TryGetValue(plot.UnlockedBy, out Plot via))
                {
                    var (_, _, viaFrom, viaTo) = SideLine(via.Land, Opposite(plot.Front));
                    low = Mathf.Max(low, Mathf.Min(viaFrom, viaTo) + GateWidth);
                    high = Mathf.Min(high, Mathf.Max(viaFrom, viaTo) - GateWidth);
                    plot.Gate = Mathf.Round((low + high) * 0.5f * 10f) * 0.1f;
                }

                plot.Gate = Mathf.Clamp(plot.Gate, low, high);
            }

            return plots;
        }

        /// <summary>
        /// A plot and what claiming it takes. The first ring's prices are its own; further out a plot
        /// is priced by its ring and its size. They are written only on the run that creates its asset.
        /// </summary>
        static Plot Make(string id, string name, string displayName, Rect land, Side front, float gate, string unlockedBy, int ring,
                         params Kind[] obstacles)
        {
            var plot = new Plot
            {
                Id = id, Name = name, DisplayName = displayName, ShortName = displayName, Land = land, Front = front, Gate = gate,
                UnlockedBy = unlockedBy, Ring = ring,
            };

            float area = land.width * land.height;

            if (ring == 0)
            {
                // As the first eight were priced: the near pair cheapest, the corners dearest.
                bool corner = unlockedBy != null;
                bool near = id == "plot_west" || id == "plot_north_west";
                bool south = id == "plot_south_west" || id == "plot_south_east";
                plot.Level = corner ? 5 : south ? 4 : near ? 2 : 3;
                plot.Wood = corner ? 40 : south ? 30 : near ? 20 : 25;
                plot.Scrap = corner ? 25 : south ? 20 : near ? 10 : 15;
                plot.Seconds = corner ? 20f : south ? 18f : 15f;
                plot.ClaimExperience = corner ? 6 : south ? 5 : 4;
                plot.ClaimWood = corner ? 10 : south ? 8 : 6;
                plot.Obstacles = obstacles;
                return plot;
            }

            // Every 100 m2 over the ring's usual size adds to the price.
            float size = Mathf.Max(0f, area - 250f) / 100f;
            plot.Level = ring == 1 ? 6 + (area > 300f ? 1 : 0) : 9 + (area > 450f ? 1 : 0);
            plot.Wood = Mathf.RoundToInt((ring == 1 ? 50f : 80f) + size * 4f);
            plot.Scrap = Mathf.RoundToInt((ring == 1 ? 30f : 50f) + size * 3f);
            plot.Seconds = ring == 1 ? 25f : 30f;
            plot.ClaimExperience = ring == 1 ? 8 : 12;
            plot.ClaimWood = ring == 1 ? 12 : 18;

            // Bigger land, more to clear: about one obstacle per 45 m2, between five and nine.
            int count = Mathf.Clamp(Mathf.RoundToInt(area / 45f), 5, 9);
            plot.Obstacles = new Kind[count];
            int start = (DeterministicRandom.StableHash(id) & 0x7FFFFFFF) % obstacles.Length;
            for (int i = 0; i < count; i++) plot.Obstacles[i] = obstacles[(start + i) % obstacles.Length];
            return plot;
        }

        /// <summary>
        /// The gates other plots open from <paramref name="id"/> by, on each side of its land: for a plot
        /// laid out elsewhere (the courtyard), whose own setup has to leave room for them in its wall.
        /// </summary>
        public static List<(Side side, float along)> GatesInto(string id, Rect compound, Rect courtyard)
        {
            var gates = new List<(Side, float)>();
            foreach (Plot plot in Layout(compound, courtyard))
                if (plot.UnlockedBy == id) gates.Add((Opposite(plot.Front), plot.Gate));
            return gates;
        }

        /// <summary>The plots the setup builds: every one in the layout but those laid out elsewhere.</summary>
        static List<Plot> Built(List<Plot> plots) => plots.FindAll(p => !p.External);

        /// <summary>
        /// The ground a plot's wall goes round once it is claimed. For a plot facing the compound,
        /// its land and the stretch of defense ring in front of it, out to the safe zone's wall: the
        /// plot's sides run on across the ring to meet that wall, so the claimed land is inside the
        /// same walls as the restaurant, reached through the safe zone's gate. Any other plot walls in
        /// just its own land.
        /// </summary>
        public static Rect Walled(Plot plot, Rect compound)
        {
            if (plot.UnlockedBy != null) return plot.Land;

            Rect r = plot.Land;
            switch (plot.Front)
            {
                case Side.South: r.yMin = compound.yMax; break;
                case Side.North: r.yMax = compound.yMin; break;
                case Side.West: r.xMin = compound.xMax; break;
                default: r.xMax = compound.xMin; break;
            }

            return r;
        }

        /// <summary>
        /// Where the walls of claimed land meet the safe zone's from across the defense ring: the
        /// ends of each first-ring plot's front, carried onto the compound's wall line - including the
        /// courtyard's. Nothing built on the wall or its strip should stand on one of these.
        /// </summary>
        public static List<Vector2> RingCrossings(Rect compound, Rect courtyard)
        {
            var points = new List<Vector2>();
            foreach (Plot plot in Layout(compound, courtyard))
            {
                if (plot.UnlockedBy != null) continue;

                (bool alongX, float line, float from, float to) = SideLine(Walled(plot, compound), plot.Front);
                var (_, _, wallFrom, wallTo) = SideLine(compound, Opposite(plot.Front));
                float low = Mathf.Min(wallFrom, wallTo), high = Mathf.Max(wallFrom, wallTo);

                foreach (float end in new[] { from, to })
                    if (end > low + 0.3f && end < high - 0.3f) points.Add(OnSide(alongX, line, end));
            }

            return points;
        }

        /// <summary><see cref="RingCrossings(Rect, Rect)"/> for the compound and courtyard as they stand in <paramref name="scene"/>.</summary>
        public static List<Vector2> RingCrossings(Scene scene) =>
            TryMeasureCompound(scene, out Rect compound) ? RingCrossings(compound, MeasureCourtyard(scene)) : new List<Vector2>();

        // --- Sides --------------------------------------------------------------------------------

        public static Side Opposite(Side side) => (Side)(((int)side + 2) % 4);

        /// <summary>
        /// A side of <paramref name="land"/> as a line: whether it runs along X, where it stands across,
        /// and where it starts and ends going round anticlockwise.
        /// </summary>
        public static (bool alongX, float line, float from, float to) SideLine(Rect land, Side side)
        {
            switch (side)
            {
                case Side.South: return (true, land.yMin, land.xMin, land.xMax);
                case Side.East: return (false, land.xMax, land.yMin, land.yMax);
                case Side.North: return (true, land.yMax, land.xMax, land.xMin);
                default: return (false, land.xMin, land.yMax, land.yMin);
            }
        }

        /// <summary>Out of the land, across the side.</summary>
        public static Vector2 Outward(Side side)
        {
            switch (side)
            {
                case Side.South: return Vector2.down;
                case Side.East: return Vector2.right;
                case Side.North: return Vector2.up;
                default: return Vector2.left;
            }
        }

        static Vector2 OnSide(bool alongX, float line, float along) => alongX ? new Vector2(along, line) : new Vector2(line, along);

        /// <summary>The middle of a plot's gate, on its front.</summary>
        public static Vector2 GatePoint(Plot plot)
        {
            var (alongX, line, _, _) = SideLine(plot.Land, plot.Front);
            return OnSide(alongX, line, plot.Gate);
        }

        /// <summary>The way through a plot's gate: the step before it, then the step after it, inside the plot.</summary>
        static (Vector2 before, Vector2 after) GateSteps(Plot plot)
        {
            Vector2 gate = GatePoint(plot);
            Vector2 outward = Outward(plot.Front);
            return (gate + outward * GateStep, gate - outward * GateStep);
        }

        /// <summary>
        /// The middle of the gate in the compound's wall straight across the ring from a plot facing
        /// the compound: on the compound's side opposite the plot's front.
        /// </summary>
        public static Vector2 CompoundGatePoint(Plot plot, Rect compound)
        {
            var (alongX, line, _, _) = SideLine(compound, Opposite(plot.Front));
            return OnSide(alongX, line, plot.Gate);
        }

        /// <summary>
        /// The way out to a plot from inside the compound: through the compound's gate across from it
        /// and over the ring to its own - or, for one opened from another plot, out through that plot
        /// first.
        /// </summary>
        public static List<Vector2> Route(Plot plot, Dictionary<string, Plot> byId, Rect compound)
        {
            var steps = new List<Vector2>();

            if (plot.UnlockedBy != null && byId.TryGetValue(plot.UnlockedBy, out Plot via))
                steps.AddRange(Route(via, byId, compound));
            else
            {
                Vector2 gate = CompoundGatePoint(plot, compound);
                Vector2 outward = Outward(Opposite(plot.Front));
                steps.Add(gate - outward * GateStep);
                steps.Add(gate + outward * GateStep);
            }

            (Vector2 before, Vector2 after) = GateSteps(plot);
            steps.Add(before);
            steps.Add(after);
            return steps;
        }

        /// <summary>The gates other plots are entered by that stand on <paramref name="side"/> of <paramref name="plot"/>.</summary>
        static List<float> GatesOn(Plot plot, Side side, List<Plot> plots)
        {
            var gates = new List<float>();
            foreach (Plot other in plots)
                if (other.UnlockedBy == plot.Id && Opposite(other.Front) == side) gates.Add(other.Gate);
            return gates;
        }

        /// <summary>
        /// Every gate in <paramref name="side"/> of the plot's own wall: those other plots are entered
        /// by, and on the front of one facing the compound, its own.
        /// </summary>
        static List<float> GatesIn(Plot plot, Side side, List<Plot> plots)
        {
            List<float> gates = GatesOn(plot, side, plots);
            if (side == plot.Front && plot.UnlockedBy == null) gates.Add(plot.Gate);
            return gates;
        }

        /// <summary>The plot beyond <paramref name="side"/> of <paramref name="plot"/>, or null for open ground or the compound.</summary>
        static Plot Beyond(Plot plot, Side side, List<Plot> plots)
        {
            (bool alongX, float line, float from, float to) = SideLine(plot.Land, side);
            Vector2 probe = OnSide(alongX, line, (from + to) * 0.5f) + Outward(side);

            foreach (Plot other in plots)
                if (other != plot && other.Land.Contains(probe)) return other;
            return null;
        }

        // --- Menu ---------------------------------------------------------------------------------

        [MenuItem("Last Orders/Expansion/Set Up Land Plots (Current Scene)", priority = 32)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();

            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders",
                    "This scene has no GameLifetimeScope, so nothing would drive the expansion.\n\n" +
                    "Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Last Orders",
                $"This edits '{scene.name}' in place:\n\n" +
                $"- Lays out {RingDepth.Length} rings of land plots round the compound, from {PerimeterStripLayout.Ring:0} m out past the defense ring, each covered in obstacles to clear\n" +
                "- Keeps a corridor open through them for each of the night's lanes\n" +
                "- Puts a gate in the compound wall facing each plot of the first ring, opens the rest from the plot in front, and stakes them all out\n" +
                "- Removes the world edge's loose props from the ground the plots cover\n" +
                "- Lays the compound walls again; a claimed plot gets a wall of its own and the compound's stays\n\n" +
                "Plots made by an earlier run are rebuilt from scratch.",
                "Set up", "Cancel");

            if (proceed) Debug.Log(Apply(scope, save: true));
        }

        /// <summary>The setup against the open scene's scope, with no dialogs - for automation.</summary>
        public static string ApplyToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null
                ? Apply(scope, save)
                : "[Last Orders] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        // --- The courtyard's debris ---------------------------------------------------------------

        static readonly Kind[] CourtyardObstacles =
            { Kind.CarWreck, Kind.Barricade, Kind.ScrapHeap, Kind.Rubble, Kind.Boulders, Kind.Thicket, Kind.Stump };

        [MenuItem("Last Orders/Expansion/Refresh Courtyard Obstacles (Current Scene)", priority = 33)]
        public static void RefreshCourtyardMenu()
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (scope != null) Debug.Log(RefreshCourtyard(scope, save: false));
        }

        /// <summary>
        /// Replaces the courtyard's debris - the old environment art 'Set Up Courtyard' adopted - with the
        /// low-poly obstacle models the plots use. Only the Debris group under Area_Courtyard is rebuilt;
        /// its walls, gap, route and zombies are left alone.
        /// </summary>
        public static string RefreshCourtyard(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;
            Rect yard = MeasureCourtyard(scene);
            if (yard.width < 1f) return "[Last Orders] No Area_Courtyard with a boundary plan in the scene; nothing was changed.";

            Transform area = null;
            foreach (ExpansionArea found in Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (found.gameObject.scene == scene && found.name == "Area_Courtyard") area = found.transform;

            K.Reset();
            E.Reset();
            GroundSplats.Reset();
            if (!ObstacleModels.AllBuilt()) ObstacleModels.Apply();

            Transform old = area.Find("Debris");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            // Entered through the gate in its south wall, across the ring from the compound's yard gate.
            var plot = new Plot
            {
                Id = CourtyardId, Name = "Courtyard", DisplayName = "Courtyard", ShortName = "Courtyard", Land = yard,
                Front = Side.South, Gate = ExpansionSceneSetup.GapCentreX, Obstacles = CourtyardObstacles,
            };

            // Behind the old barrier row across the yard, clear of its feet.
            float frontClear = CourtyardModels.RowFromFront + 0.9f;

            // Clear of the ways through to the plots opened from it, too.
            List<Plot> plots = TryMeasureCompound(scene, out Rect compound) ? Layout(compound, yard) : new List<Plot>();
            plots.RemoveAll(p => p.Id == CourtyardId);
            plots.Add(plot);

            (Vector2 _, Vector2 after) = GateSteps(plot);
            var rng = new DeterministicRandom(Seed + 7);
            int placed = PlaceObstacles(scope, area, plot, plots, DebrisTypes(), Trails(), new Dictionary<Kind, int>(),
                                        after, frontClear, ref rng, out int wanted);

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            return $"[Last Orders] Courtyard: {placed} of {wanted} obstacles placed in X {yard.xMin:0.0}-{yard.xMax:0.0}, Z {yard.yMin:0.0}-{yard.yMax:0.0}.";
        }

        // --- Setup --------------------------------------------------------------------------------

        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;
            var report = new StringBuilder("[Last Orders] Land plots set up.\n");

            K.Reset();
            E.Reset();
            GroundSplats.Reset();

            if (!ObstacleModels.AllBuilt()) report.Append($"  {ObstacleModels.Apply()}\n");

            if (!TryMeasureCompound(scene, out Rect compound))
                return "[Last Orders] No compound boundary (Restaurant/Zone/OuterBoundries) in the scene; nothing was changed.";

            Rect courtyard = MeasureCourtyard(scene);
            List<Plot> plots = Layout(compound, courtyard);
            var byId = new Dictionary<string, Plot>();
            foreach (Plot plot in plots) byId[plot.Id] = plot;

            // --- Content -----------------------------------------------------------------------------

            Dictionary<Kind, DebrisDefinition> types = DebrisTypes();
            var definitions = new Dictionary<string, ExpansionAreaDefinition>();
            foreach (Plot plot in Built(plots)) definitions[plot.Id] = AreaDefinition(plot);

            ExpansionAreaDefinition yard = ExpansionContentGenerator.LoadCourtyard();
            if (yard != null) definitions[CourtyardId] = yard;

            foreach (Plot plot in Built(plots))
            {
                ExpansionAreaDefinition definition = definitions[plot.Id];
                definition.unlockedBy = plot.UnlockedBy != null && definitions.TryGetValue(plot.UnlockedBy, out ExpansionAreaDefinition gate)
                    ? new[] { gate }
                    : new ExpansionAreaDefinition[0];
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance").objectReferenceValue as GameBalanceConfig;
            WorldUiPrefabGenerator.Ensure(balance);
            PooledEffect dust = EffectPrefabGenerator.EnsureBuildDust();

            // --- The plots -----------------------------------------------------------------------------

            GameObject template = GameObject.Find(WallTemplatePath);
            Transform areasRoot = FindOrCreateRoot(scene, AreasRootName);
            List<(Vector2 a, Vector2 b)> trails = Trails();

            var rng = new DeterministicRandom(Seed);
            var variants = new Dictionary<Kind, int>();

            // Plots an earlier layout had that this one does not: gone, walls and all.
            var keep = new HashSet<string>();
            foreach (Plot plot in Built(plots)) keep.Add($"Area_Plot_{plot.Name}");

            var stale = new List<GameObject>();
            foreach (Transform child in areasRoot)
                if (child.name.StartsWith("Area_Plot_") && !keep.Contains(child.name)) stale.Add(child.gameObject);
            foreach (GameObject go in stale)
            {
                report.Append($"  {go.name} is no longer in the layout; removed.\n");
                Undo.DestroyObjectImmediate(go);
            }

            int built = 0;
            for (int i = 0; i < plots.Count; i++)
            {
                Plot plot = plots[i];
                if (plot.External) continue;

                string line = BuildPlot(scope, areasRoot, plot, i, plots, byId, compound, definitions[plot.Id], types, template, dust,
                                        trails, variants, ref rng);
                report.Append($"  {line}\n");
                built++;
            }

            report.Append($"  {built} plots in {RingDepth.Length} rings, each touching the next; the first ring's walls reach across the defense ring to the compound's.\n");

            // --- What was there, and the walls ---------------------------------------------------------

            Rect covered = plots[0].Land;
            foreach (Plot plot in plots)
                covered = Rect.MinMaxRect(Mathf.Min(covered.xMin, plot.Land.xMin), Mathf.Min(covered.yMin, plot.Land.yMin),
                                          Mathf.Max(covered.xMax, plot.Land.xMax), Mathf.Max(covered.yMax, plot.Land.yMax));

            // The margin the world-edge builder keeps from the expansion areas, so a rebuild of it agrees.
            int cleared = ClearOutskirts(scene, Inflate(covered, 3f));
            report.Append($"  {cleared} of the world edge's loose props removed from the plots' ground.\n");

            report.Append($"  {ExpansionNeighbours.Assign(scene)} wall sections face a neighbouring plot.\n");
            report.Append(CompoundWallSceneSetup.Apply(scope, save: false));

            // The discovery sites stand on four of the plots just rebuilt: put them back, and their debris off them.
            report.Append(DiscoverySiteSetup.Apply(scope, save: false));

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);

            return report.ToString();
        }

        static string BuildPlot(GameLifetimeScope scope, Transform areasRoot, Plot plot, int index, List<Plot> plots,
                                Dictionary<string, Plot> byId, Rect compound, ExpansionAreaDefinition definition,
                                Dictionary<Kind, DebrisDefinition> types, GameObject template, PooledEffect dust,
                                List<(Vector2 a, Vector2 b)> trails, Dictionary<Kind, int> variants, ref DeterministicRandom rng)
        {
            string rootName = $"Area_Plot_{plot.Name}";
            Transform old = areasRoot.Find(rootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            Transform area = K.Group(rootName, areasRoot);
            area.position = new Vector3(plot.Land.center.x, 0f, plot.Land.center.y);
            Undo.RegisterCreatedObjectUndo(area.gameObject, rootName);

            // The way out to it: out of the compound and over the ring, through the plot it opens from first if it has one.
            List<Vector2> steps = Route(plot, byId, compound);
            (Vector2 _, Vector2 after) = GateSteps(plot);

            Transform routeRoot = K.Group("Route", area);
            var route = new Transform[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                route[i] = K.Group($"Step_{i}", routeRoot);
                route[i].position = new Vector3(steps[i].x, 0f, steps[i].y);
            }

            Transform centre = K.Group("Centre", area);
            centre.position = new Vector3(plot.Land.center.x, 1f, plot.Land.center.y);

            // For good: the compound's wall across the ring from it, with the gate out to it.
            if (plot.UnlockedBy == null) BuildCompoundFront(area, plot, compound, template);

            // Shown until claimed: the stakes.
            Transform locked = K.Group("LockedUntilClaimed", area);
            var lockedVisuals = new List<GameObject> { BuildStakes(locked, plot, index, plots, trails) };

            ExpansionBuildPlan plan = BuildBoundary(area, plot, plots, compound, template, out int sections);

            Transform claimed = K.Group("ShownWhenClaimed", area);
            claimed.gameObject.SetActive(false);

            int placed = PlaceObstacles(scope, area, plot, plots, types, trails, variants, after, EdgeClear, ref rng, out int wanted);

            var expansion = area.gameObject.AddComponent<ExpansionArea>();
            expansion.Configure(scope, definition, route, centre, lockedVisuals.ToArray(), plan, dust, new[] { claimed.gameObject });
            EditorUtility.SetDirty(expansion);

            string unlock = plot.UnlockedBy != null ? $", opens after {byId[plot.UnlockedBy].DisplayName}" : "";
            string missed = placed < wanted ? $" ({wanted - placed} did not fit)" : "";
            return $"{plot.DisplayName}: X {plot.Land.xMin:0.0}-{plot.Land.xMax:0.0}, Z {plot.Land.yMin:0.0}-{plot.Land.yMax:0.0}, " +
                   $"{placed} obstacles{missed}, {sections} wall sections, gate on the {plot.Front.ToString().ToLower()} side{unlock}.";
        }

        // --- Walls --------------------------------------------------------------------------------

        /// <summary>
        /// The stretch of the compound's wall across the ring from a plot's front - as far as the two
        /// overlap - in two pieces either side of the gate out to it. They stand for good: the wall
        /// setup lays the compound's wall along them in place of its own there, so the gate is the
        /// compound's and stays when the plot is claimed.
        /// </summary>
        static void BuildCompoundFront(Transform area, Plot plot, Rect compound, GameObject template)
        {
            Transform root = K.Group(CompoundFrontName, area);
            root.localPosition = Vector3.zero;

            (bool alongX, float line, float wallFrom, float wallTo) = SideLine(compound, Opposite(plot.Front));
            var (_, _, from, to) = SideLine(plot.Land, plot.Front);

            float low = Mathf.Max(Mathf.Min(from, to), Mathf.Min(wallFrom, wallTo));
            float high = Mathf.Min(Mathf.Max(from, to), Mathf.Max(wallFrom, wallTo));

            Section(root, template, "CompoundWall_A", alongX, line, low, plot.Gate - GateWidth * 0.5f, active: true);
            Section(root, template, "CompoundWall_B", alongX, line, plot.Gate + GateWidth * 0.5f, high, active: true);
        }

        /// <summary>
        /// The plot's new wall round <see cref="Walled"/>, in sections in the order they go up: along
        /// the side to one end of the front, across the far side, back along the other, then across
        /// the front. Along its front a plot raises nothing where a wall already stands: a first-ring
        /// plot's front is the safe zone's own wall, with its gate in it, and only any stretch past the
        /// ends of that wall is built; a plot opened from another has that plot's wall there, with
        /// this plot's gate in it, and builds only along any of its front that reaches past it.
        /// </summary>
        static ExpansionBuildPlan BuildBoundary(Transform area, Plot plot, List<Plot> plots, Rect compound, GameObject template,
                                                out int sections)
        {
            Transform root = K.Group("NewBoundary", area);
            root.position = Vector3.zero;

            var pieces = new List<Transform>();
            Plot via = plot.UnlockedBy != null ? plots.Find(p => p.Id == plot.UnlockedBy) : null;
            Rect walled = Walled(plot, compound);

            for (int k = 1; k <= 4; k++)
            {
                var side = (Side)(((int)plot.Front + k) % 4);
                (bool alongX, float line, float from, float to) = SideLine(walled, side);

                List<(float, float)> runs = Without(from, to, GatesIn(plot, side, plots));
                if (side == plot.Front)
                {
                    // What already stands along it: the compound's wall, or the wall of the plot it opens from.
                    Rect there = via != null ? Walled(via, compound) : compound;
                    var (_, _, thereFrom, thereTo) = SideLine(there, Opposite(side));
                    runs = Outside(runs, Mathf.Min(thereFrom, thereTo), Mathf.Max(thereFrom, thereTo));
                }

                foreach ((float a, float b) in runs)
                {
                    float length = Mathf.Abs(b - a);
                    int count = Mathf.Max(1, Mathf.CeilToInt(length / SectionLength - 0.05f));
                    float step = (b - a) / count;

                    for (int i = 0; i < count; i++)
                    {
                        float start = a + step * i;
                        GameObject piece = Section(root, template, $"Wall_{pieces.Count:00}", alongX, line, start, start + step, active: false);
                        pieces.Add(piece.transform);
                    }
                }
            }

            var plan = root.gameObject.AddComponent<ExpansionBuildPlan>();
            plan.Configure(pieces.ToArray());
            EditorUtility.SetDirty(plan);

            sections = pieces.Count;
            return plan;
        }

        /// <summary>
        /// The stretch <paramref name="from"/> to <paramref name="to"/> (either way round) with a gate's
        /// width taken out at each of <paramref name="gates"/>, in the same direction.
        /// </summary>
        static List<(float, float)> Without(float from, float to, List<float> gates)
        {
            float low = Mathf.Min(from, to), high = Mathf.Max(from, to);
            gates.Sort();

            var parts = new List<(float, float)>();
            float cursor = low;
            foreach (float gate in gates)
            {
                float a = gate - GateWidth * 0.5f, b = gate + GateWidth * 0.5f;
                if (a - cursor > 0.3f) parts.Add((cursor, a));
                cursor = Mathf.Max(cursor, b);
            }

            if (high - cursor > 0.3f) parts.Add((cursor, high));

            if (from <= to) return parts;

            var reversed = new List<(float, float)>();
            for (int i = parts.Count - 1; i >= 0; i--) reversed.Add((parts[i].Item2, parts[i].Item1));
            return reversed;
        }

        /// <summary>
        /// The parts of each run that lie outside <paramref name="low"/>..<paramref name="high"/>, each
        /// kept in its run's direction; slivers too short for a section are dropped.
        /// </summary>
        static List<(float, float)> Outside(List<(float, float)> runs, float low, float high)
        {
            var parts = new List<(float, float)>();
            foreach ((float a, float b) in runs)
            {
                float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
                var pieces = new List<(float, float)>();
                if (Mathf.Min(hi, low) - lo > 0.3f) pieces.Add((lo, Mathf.Min(hi, low)));
                if (hi - Mathf.Max(lo, high) > 0.3f) pieces.Add((Mathf.Max(lo, high), hi));

                if (a > b)
                {
                    pieces.Reverse();
                    for (int i = 0; i < pieces.Count; i++) pieces[i] = (pieces[i].Item2, pieces[i].Item1);
                }

                parts.AddRange(pieces);
            }

            return parts;
        }

        /// <summary>
        /// A boundary piece from <paramref name="from"/> to <paramref name="to"/> along a line: a copy of the
        /// compound's own boundary cube - same height, thickness and look - which the wall setup lays a
        /// proper wall along.
        /// </summary>
        static GameObject Section(Transform parent, GameObject template, string name, bool alongX, float line, float from, float to, bool active)
        {
            float length = Mathf.Max(0.1f, Mathf.Abs(to - from));
            float mid = (from + to) * 0.5f;
            Vector2 centre = OnSide(alongX, line, mid);

            GameObject piece;
            if (template != null)
            {
                piece = Object.Instantiate(template, parent);
                piece.name = name;
                piece.transform.localScale = new Vector3(template.transform.localScale.x, template.transform.localScale.y, length);
                piece.transform.rotation = Quaternion.Euler(0f, alongX ? 90f : 0f, 0f);
                piece.transform.position = new Vector3(centre.x, template.transform.position.y, centre.y);
            }
            else
            {
                piece = K.Cube(name, parent, Vector3.zero, Vector3.one, K.Stone, 0f, collider: true);
                piece.transform.localScale = new Vector3(1f, 1f, length);
                piece.transform.rotation = Quaternion.Euler(0f, alongX ? 90f : 0f, 0f);
                piece.transform.position = new Vector3(centre.x, 0f, centre.y);
            }

            piece.SetActive(active);
            return piece;
        }

        // --- Stakes -------------------------------------------------------------------------------

        /// <summary>
        /// Survey stakes and twine all round the plot, a red-tipped stake every few metres and a flag at
        /// each corner and either side of each gate: land marked out to be taken. A side shared with
        /// another plot is staked by whichever comes first in the layout, and the twine leaves a gap
        /// where a trail crosses and where a gate will be.
        /// </summary>
        static GameObject BuildStakes(Transform parent, Plot plot, int index, List<Plot> plots, List<(Vector2 a, Vector2 b)> trails)
        {
            var m = new B.Model();
            Vector3 origin = parent.parent.position;

            for (int k = 1; k <= 4; k++)
            {
                var side = (Side)(((int)plot.Front + k) % 4);
                // Staked once: by whichever of the two comes first - but the courtyard has its own border, no stakes.
                Plot beyond = Beyond(plot, side, plots);
                if (beyond != null && !beyond.External && plots.IndexOf(beyond) < index) continue;

                (bool alongX, float line, float from, float to) = SideLine(plot.Land, side);
                List<float> gates = GatesIn(plot, side, plots);
                List<(float along, bool flagged)> points = StakePoints(from, to, gates);

                Vector3 previous = default;
                float previousAlong = from;
                bool havePrevious = false;

                for (int i = 0; i < points.Count; i++)
                {
                    (float along, bool corner) = points[i];
                    Vector2 at = OnSide(alongX, line, along);
                    if (NearTrail(at, trails, L.TrailHalfWidth + 0.3f))
                    {
                        havePrevious = false;
                        continue;
                    }

                    // No twine across a gateway.
                    foreach (float gate in gates)
                        if ((gate - previousAlong) * (gate - along) < 0f) havePrevious = false;
                    previousAlong = along;

                    float height = corner ? 0.85f : 0.6f;
                    Vector3 foot = new Vector3(at.x, 0f, at.y) - origin;
                    Quaternion lean = Quaternion.Euler(4f * B.Jitter(index * 131 + k * 17 + i), 0f, 4f * B.Jitter(index * 37 + k * 7 + i));

                    m.Box(StakeWood * (1f + 0.05f * B.Jitter(i + k * 11)), foot + lean * (Vector3.up * (height * 0.5f - 0.05f)),
                          new Vector3(0.06f, height, 0.06f), 0.012f, lean);
                    m.Box(StakeTip, foot + lean * (Vector3.up * (height - 0.08f)), new Vector3(0.068f, 0.09f, 0.068f), 0.012f, lean);

                    Vector3 knot = foot + lean * (Vector3.up * (height > 0.7f ? 0.5f : height - 0.12f));
                    if (corner)
                    {
                        Vector3 flag = foot + lean * (Vector3.up * (height - 0.1f)) + new Vector3(0.11f, 0f, 0f);
                        m.Box(StakeTip, flag, new Vector3(0.2f, 0.13f, 0.012f), 0.004f, lean);
                    }

                    if (havePrevious)
                    {
                        // Twine sagging between the two.
                        Vector3 sag = (previous + knot) * 0.5f + Vector3.down * 0.06f;
                        m.Tube(B.Twine, previous, sag, 0.008f, 4);
                        m.Tube(B.Twine, sag, knot, 0.008f, 4);
                    }

                    previous = knot;
                    havePrevious = true;
                }
            }

            Mesh mesh = m.Save($"{StakeFolder}/Stakes_{plot.Name}.asset", $"Stakes_{plot.Name}");

            Transform stakes = K.Group("SurveyStakes", parent);
            stakes.localPosition = Vector3.zero;
            B.Show(stakes.gameObject, mesh);
            return stakes.gameObject;
        }

        /// <summary>
        /// Where the stakes go along one side, walked <paramref name="from"/> to <paramref name="to"/>:
        /// evenly every few metres, flagged at the two ends, with a flagged pair standing for the posts
        /// of each gate in place of any that would stand in the gateway.
        /// </summary>
        static List<(float along, bool flagged)> StakePoints(float from, float to, List<float> gates)
        {
            const float Clear = GateWidth * 0.5f + 0.6f;

            int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(to - from) / StakeSpacing));
            var points = new List<(float along, bool flagged)>();

            for (int i = 0; i <= count; i++)
            {
                float along = Mathf.Lerp(from, to, i / (float)count);
                bool end = i == 0 || i == count;

                bool inGateway = false;
                foreach (float gate in gates) inGateway |= Mathf.Abs(along - gate) < Clear;
                if (!inGateway || end) points.Add((along, end));
            }

            foreach (float gate in gates)
            {
                points.Add((gate - GateWidth * 0.5f, true));
                points.Add((gate + GateWidth * 0.5f, true));
            }

            float direction = Mathf.Sign(to - from);
            points.Sort((a, b) => (direction * a.along).CompareTo(direction * b.along));
            return points;
        }

        // --- Obstacles ----------------------------------------------------------------------------

        static int PlaceObstacles(GameLifetimeScope scope, Transform area, Plot plot, List<Plot> plots, Dictionary<Kind, DebrisDefinition> types,
                                  List<(Vector2 a, Vector2 b)> trails, Dictionary<Kind, int> variants, Vector2 workFrom, float frontClear,
                                  ref DeterministicRandom rng, out int wanted)
        {
            Transform root = K.Group("Debris", area);
            root.localPosition = Vector3.zero;

            // A little in from every edge, and further where something stands across the side it is entered by.
            Rect usable = plot.Land;
            foreach (Side side in new[] { Side.South, Side.North, Side.West, Side.East })
                usable = Shrink(usable, side, side == plot.Front ? frontClear : EdgeClear);

            // The ways in: through its own gate, and through any gate in its walls to the plot beyond.
            var approaches = new List<(Vector2 a, Vector2 b)>();
            Vector2 gate = GatePoint(plot);
            approaches.Add((gate, gate - Outward(plot.Front) * (GateStep + Approach)));
            foreach (Plot other in plots)
            {
                if (other.UnlockedBy != plot.Id) continue;
                Vector2 theirs = GatePoint(other);
                approaches.Add((theirs, theirs + Outward(other.Front) * (GateStep + Approach)));
            }

            // The biggest first, while there is room for them.
            var order = new List<Kind>(plot.Obstacles);
            var sizes = new Dictionary<Kind, float>();
            foreach (Kind kind in order) sizes[kind] = Area(ObstacleModels.LoadPrefab(kind, 0));
            order.Sort((a, b) => sizes[b].CompareTo(sizes[a]));

            var standing = new List<Footprint>();
            var counts = new Dictionary<Kind, int>();
            wanted = order.Count;

            // Cars lie along the plot, roughly; everything else any way round.
            float along = plot.Land.width > plot.Land.height ? 0f : 90f;

            foreach (Kind asked in order)
            {
                // Where a big one will not go, something small that grows anywhere.
                Kind kind = asked;
                GameObject prefab = null;
                Footprint place = default;
                bool placed = false;

                foreach (Kind candidate in new[] { asked, Kind.Stump, Kind.Thicket })
                {
                    variants.TryGetValue(candidate, out int v);
                    prefab = ObstacleModels.LoadPrefab(candidate, v % ObstacleModels.Variants);

                    for (int attempt = 0; attempt < 600 && !placed; attempt++)
                    {
                        float yaw = candidate == Kind.CarWreck ? along + (rng.Chance(0.5f) ? 180f : 0f) + rng.Range(-25f, 25f) : rng.Range(0f, 360f);
                        Vector2 at = new Vector2(rng.Range(usable.xMin, usable.xMax), rng.Range(usable.yMin, usable.yMax));
                        place = Footprint.Of(prefab, at, yaw);
                        placed = Fits(place, usable, standing, approaches, trails);
                    }

                    if (!placed) continue;

                    variants[candidate] = v + 1;
                    kind = candidate;
                    break;
                }

                if (!placed) continue;

                standing.Add(place);
                counts.TryGetValue(kind, out int n);
                counts[kind] = n + 1;

                string label = kind.ToString().ToLower();
                MakeDebris(scope, root, $"{plot.Id}/{label}_{n + 1}", $"Debris_{kind}_{n + 1}", types[kind], prefab, kind, place,
                           workFrom, ref rng);
            }

            return standing.Count;
        }

        /// <summary>
        /// The ground an obstacle covers, as a capsule: a line down its long axis and a radius round it.
        /// A circle round a car is twice the car.
        /// </summary>
        readonly struct Footprint
        {
            public readonly Vector2 At, A, B;
            public readonly float Yaw, Radius, Length;

            Footprint(Vector2 at, Vector2 a, Vector2 b, float yaw, float radius, float length)
            {
                At = at;
                A = a;
                B = b;
                Yaw = yaw;
                Radius = radius;
                Length = length;
            }

            public static Footprint Of(GameObject prefab, Vector2 at, float yaw)
            {
                var filter = prefab != null ? prefab.GetComponent<MeshFilter>() : null;
                Bounds bounds = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 2f);

                float ex = bounds.extents.x, ez = bounds.extents.z;
                bool alongX = ex >= ez;
                float radius = Mathf.Min(ex, ez) * 0.95f;
                float half = Mathf.Max(0f, Mathf.Max(ex, ez) - radius);

                Vector2 centre = at + Turn(new Vector2(bounds.center.x, bounds.center.z), yaw);
                Vector2 axis = Turn(alongX ? Vector2.right : Vector2.up, yaw);
                return new Footprint(at, centre - axis * half, centre + axis * half, yaw, radius, Mathf.Max(ex, ez) * 2f);
            }

            /// <summary>A ground-plane vector (x, z) turned the way a yaw about Y turns it.</summary>
            static Vector2 Turn(Vector2 v, float yaw)
            {
                Vector3 t = Quaternion.Euler(0f, yaw, 0f) * new Vector3(v.x, 0f, v.y);
                return new Vector2(t.x, t.z);
            }
        }

        static bool Fits(Footprint f, Rect usable, List<Footprint> standing, List<(Vector2 a, Vector2 b)> approaches,
                         List<(Vector2 a, Vector2 b)> trails)
        {
            Rect inner = Rect.MinMaxRect(usable.xMin + f.Radius, usable.yMin + f.Radius, usable.xMax - f.Radius, usable.yMax - f.Radius);
            if (!inner.Contains(f.A) || !inner.Contains(f.B)) return false;

            foreach ((Vector2 a, Vector2 b) in trails)
                if (SegmentDistance(f.A, f.B, a, b) < f.Radius + TrailClear) return false;

            foreach ((Vector2 a, Vector2 b) in approaches)
                if (SegmentDistance(f.A, f.B, a, b) < f.Radius + ApproachClear) return false;

            foreach (Footprint other in standing)
                if (SegmentDistance(f.A, f.B, other.A, other.B) < f.Radius + other.Radius + Spacing) return false;

            return true;
        }

        /// <summary>The ground a model covers, for putting the biggest out first.</summary>
        static float Area(GameObject prefab)
        {
            var filter = prefab != null ? prefab.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) return 1f;
            Vector3 size = filter.sharedMesh.bounds.size;
            return size.x * size.z;
        }

        /// <summary>The shortest distance between two segments on the ground: zero where they cross.</summary>
        static float SegmentDistance(Vector2 a1, Vector2 b1, Vector2 a2, Vector2 b2)
        {
            if (Cross(a1, b1, a2) * Cross(a1, b1, b2) < 0f && Cross(a2, b2, a1) * Cross(a2, b2, b1) < 0f) return 0f;

            return Mathf.Min(Mathf.Min(Distance(a1, a2, b2), Distance(b1, a2, b2)), Mathf.Min(Distance(a2, a1, b1), Distance(b2, a1, b1)));
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        /// <summary>
        /// One obstacle as a piece of debris, the way the courtyard's are: a root on the ground with a
        /// box round it for taps, the model and the stain it stands in under one pivot that shakes and
        /// collapses, the UI floating over the top, and a spot to stand on the side the worker comes from.
        /// </summary>
        static void MakeDebris(GameLifetimeScope scope, Transform parent, string id, string name, DebrisDefinition type, GameObject prefab,
                               Kind kind, Footprint place, Vector2 workFrom, ref DeterministicRandom rng)
        {
            Vector2 at = place.At;
            Transform root = K.Group(name, parent);
            root.position = new Vector3(at.x, 0f, at.y);

            Transform visual = K.Group("Visual", root);
            visual.localPosition = Vector3.zero;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, visual);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, place.Yaw, 0f);

            // The ground it has worn or stained, stretched along it.
            bool growth = kind == Kind.Boulders || kind == Kind.Stump || kind == Kind.FallenTree || kind == Kind.Thicket;
            Color stain = growth ? E.Pick(E.ForestFloor, ref rng) : E.Pick(E.Dirt, ref rng);
            Vector2 middle = (place.A + place.B) * 0.5f - at;
            Vector2 axis = place.B - place.A;
            float axisYaw = axis.sqrMagnitude > 1e-4f ? Mathf.Atan2(-axis.y, axis.x) * Mathf.Rad2Deg : place.Yaw;
            GroundSplats.Splat("Ground", visual, middle, place.Length * 1.15f + 0.6f, place.Radius * 2.6f + 0.6f,
                               axisYaw + rng.Range(-8f, 8f), stain, growth ? 0.35f : 0.45f, 3, ref rng);

            Bounds bounds = model.GetComponent<Renderer>().bounds;

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(bounds.center.x - at.x, Mathf.Max(0.6f, bounds.max.y * 0.5f), bounds.center.z - at.y);
            box.size = new Vector3(bounds.size.x, Mathf.Max(1.2f, bounds.max.y), bounds.size.z);

            Transform anchor = K.Group("UiAnchor", root);
            anchor.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

            // At the edge of it on the side the worker arrives from, with room to swing.
            Vector2 toward = workFrom - new Vector2(bounds.center.x, bounds.center.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector2.down;
            toward.Normalize();
            float reach = Mathf.Abs(toward.x) * bounds.extents.x + Mathf.Abs(toward.y) * bounds.extents.z + 0.5f;

            Transform stand = K.Group("WorkPoint", root);
            stand.position = new Vector3(bounds.center.x + toward.x * reach, 0f, bounds.center.z + toward.y * reach);

            var debris = root.gameObject.AddComponent<Debris>();
            debris.Configure(scope, id, type, stand, anchor, visual);
            EditorUtility.SetDirty(debris);
        }

        static Rect Shrink(Rect r, Side side, float by)
        {
            switch (side)
            {
                case Side.South: r.yMin += by; break;
                case Side.North: r.yMax -= by; break;
                case Side.West: r.xMin += by; break;
                default: r.xMax -= by; break;
            }

            return r;
        }

        static Rect Inflate(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        // --- Content ------------------------------------------------------------------------------

        /// <summary>
        /// What each kind of obstacle takes and pays. Filled only on the run that creates an asset, as
        /// the courtyard's debris are, so tuning in the inspector is never undone. Wood and scrap, the
        /// two the expansions are paid in: a plot's obstacles pay toward the plot's own price.
        /// </summary>
        static Dictionary<Kind, DebrisDefinition> DebrisTypes()
        {
            ContentGenerator.EnsureFolder(DebrisFolder);

            var table = new (Kind kind, string name, float seconds, int xp, ResourceType type, int amount)[]
            {
                (Kind.Boulders, "boulders", 7f, 2, ResourceType.Scrap, 3),
                (Kind.FallenTree, "fallen tree", 7f, 2, ResourceType.Wood, 5),
                (Kind.Stump, "tree stump", 5f, 1, ResourceType.Wood, 3),
                (Kind.Thicket, "thicket", 4f, 1, ResourceType.Wood, 2),
                (Kind.CarWreck, "car wreck", 10f, 3, ResourceType.Scrap, 6),
                (Kind.ScrapHeap, "scrap heap", 6f, 2, ResourceType.Scrap, 4),
                (Kind.Rubble, "rubble", 6f, 2, ResourceType.Scrap, 3),
                (Kind.Barricade, "old barricade", 7f, 2, ResourceType.Wood, 4),
            };

            var types = new Dictionary<Kind, DebrisDefinition>();
            foreach (var row in table)
            {
                string path = $"{DebrisFolder}/Debris_{row.kind}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<DebrisDefinition>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<DebrisDefinition>();
                    asset.displayName = row.name;
                    asset.workSeconds = row.seconds;
                    asset.reward = new RewardBundle(row.xp, new ResourceAmount(row.type, row.amount));
                    asset.role = WorkerRole.MainCharacter;
                    AssetDatabase.CreateAsset(asset, path);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }

                types[row.kind] = asset;
            }

            return types;
        }

        /// <summary>
        /// A plot's area definition. Its id and wall name are set every run - the scene depends on them -
        /// and its price, level, build time and reward only on the run that creates it.
        /// </summary>
        static ExpansionAreaDefinition AreaDefinition(Plot plot)
        {
            ContentGenerator.EnsureFolder(PlotFolder);

            string path = $"{PlotFolder}/ExpansionArea_Plot_{plot.Name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ExpansionAreaDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ExpansionAreaDefinition>();
                asset.displayName = plot.DisplayName;
                asset.benefit = "More room inside the walls";
                asset.expandCost = new[] { new ResourceAmount(ResourceType.Wood, plot.Wood), new ResourceAmount(ResourceType.Scrap, plot.Scrap) };
                asset.requiredLevel = plot.Level;
                asset.expandSeconds = plot.Seconds;
                asset.claimReward = new RewardBundle(plot.ClaimExperience, new ResourceAmount(ResourceType.Wood, plot.ClaimWood));
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.id = plot.Id;
            if (string.IsNullOrEmpty(asset.shortName)) asset.shortName = plot.ShortName;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        // --- Measuring ----------------------------------------------------------------------------

        /// <summary>The compound as its boundary pieces stand.</summary>
        internal static bool TryMeasureCompound(Scene scene, out Rect compound)
        {
            compound = default;
            GameObject boundary = GameObject.Find("Restaurant/Zone/OuterBoundries");
            if (boundary == null || boundary.scene != scene) return false;

            float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;
            bool any = false;

            foreach (Transform piece in boundary.transform)
            {
                if (!piece.gameObject.activeSelf || !ExpansionNeighbours.TryLine(piece, out bool alongX, out Vector3 centre, out float length))
                    continue;

                Vector3 half = (alongX ? Vector3.right : Vector3.forward) * (length * 0.5f);
                foreach (Vector3 end in new[] { centre - half, centre + half })
                {
                    xMin = Mathf.Min(xMin, end.x);
                    xMax = Mathf.Max(xMax, end.x);
                    zMin = Mathf.Min(zMin, end.z);
                    zMax = Mathf.Max(zMax, end.z);
                }

                any = true;
            }

            if (any) compound = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            return any && compound.width > 10f && compound.height > 10f;
        }

        /// <summary>
        /// The courtyard's land, from the wall its expansion builds - less the stretch of defense ring
        /// that wall takes in in front of it, which is not land to clear. Empty when there is none.
        /// </summary>
        internal static Rect MeasureCourtyard(Scene scene)
        {
            foreach (ExpansionArea area in Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (area.gameObject.scene != scene || area.name != "Area_Courtyard") continue;

                var plan = new SerializedObject(area).FindProperty("buildPlan").objectReferenceValue as ExpansionBuildPlan;
                if (plan == null || !ExpansionNeighbours.TryLand(plan, out Rect land)) continue;

                land.yMin = Mathf.Max(land.yMin, PerimeterStripLayout.NorthLine + PerimeterStripLayout.Ring);
                return land;
            }

            return default;
        }

        /// <summary>The night's approach trails on the open ground, as the world edge lays them.</summary>
        static List<(Vector2 a, Vector2 b)> Trails()
        {
            var trails = new List<(Vector2 a, Vector2 b)>();
            foreach (L.Lane lane in L.Lanes)
                trails.Add((new Vector2(lane.TrailStart.x, lane.TrailStart.z), new Vector2(lane.WallEnd.x, lane.WallEnd.z)));

            return trails;
        }

        static bool NearTrail(Vector2 p, List<(Vector2 a, Vector2 b)> trails, float within)
        {
            foreach ((Vector2 a, Vector2 b) in trails)
                if (Distance(p, a, b) < within) return true;
            return false;
        }

        static float Distance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// Takes the world edge's loose props - junk, wrecks, rocks, fences - off the ground the plots
        /// cover. The world-edge builder keeps clear of the expansion areas when it next runs, so this is
        /// what a rebuild would leave anyway.
        /// </summary>
        static int ClearOutskirts(Scene scene, Rect area)
        {
            Transform outskirts = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "WorldEnvironment") outskirts = root.transform.Find("Outskirts");

            if (outskirts == null) return 0;

            int removed = 0;
            foreach (string sector in new[] { "North", "West", "East", "South" })
            {
                Transform group = outskirts.Find(sector);
                if (group == null) continue;

                var doomed = new List<GameObject>();
                foreach (Transform prop in group)
                {
                    Renderer[] renderers = prop.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) continue;

                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                    if (area.Contains(new Vector2(bounds.center.x, bounds.center.z))) doomed.Add(prop.gameObject);
                }

                foreach (GameObject prop in doomed)
                {
                    Undo.DestroyObjectImmediate(prop);
                    removed++;
                }
            }

            return removed;
        }

        static Transform FindOrCreateRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go.transform;
        }
    }
}
