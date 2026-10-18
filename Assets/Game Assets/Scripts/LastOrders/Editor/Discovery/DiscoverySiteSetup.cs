using System.Collections.Generic;
using System.Linq;
using System.Text;
using FoodStreet.Visitors;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.Discovery;
using LastOrders.EditorTools.Screens;
using LastOrders.Expansion;
using LastOrders.UI.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using B = LastOrders.EditorTools.BuildingKit;
using D = LastOrders.EditorTools.DiscoveryKit;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the four discovery sites into the open scene, each on - or by - a plot of land still to
    /// be claimed, standing abandoned until it is:
    ///
    /// - an abandoned drive-through booth on the verge in front of the south-east lot, its lane off
    ///   the road and back onto it: the takeaway service;
    /// - a food truck stranded on a pad in front of the south-west lot: the food truck;
    /// - an old shooting range filling the west lot: target practice with the compound's own machines;
    /// - an abandoned security hut on the east lot, with the noise maker found in it.
    ///
    /// Builds the models, the site definitions, the truck, the target and the bag prefabs and the
    /// popups first. Debris on a plot that stands where its site does is moved to open ground on the
    /// same plot, keeping its identity; loose props on the verge are cleared off the road-side sites.
    ///
    /// Safe to re-run: the sites are rebuilt from scratch, the definitions keep any text tuned in the
    /// inspector, and debris already moved out of the way stays where it went. 'Set Up Land Plots'
    /// runs this again at its end, since it rebuilds the plots and their debris.
    /// </summary>
    public static class DiscoverySiteSetup
    {
        const string RootName = "DiscoverySites";
        const string VehicleFolder = "Assets/Game Assets/Content/Visitors/Vehicles";

        static string Content(string name) => $"{D.Folder}/{name}";

        // --- Where everything goes (world metres; north is +Z, the road runs east-west at Z -13.7) ---

        /// <summary>The booth: on the verge between the south-east lot and the road, its window facing the road.</summary>
        static readonly Vector3 BoothAt = new Vector3(63.5f, 0f, -5.6f);

        /// <summary>The lane, east to west the way the cars drive it: off the road's westbound lane, past the window, back on.</summary>
        static readonly Vector3[] LaneLine =
        {
            new Vector3(90f, 0f, -12.5f), new Vector3(84.5f, 0f, -10.4f), new Vector3(79.5f, 0f, -9f), new Vector3(70f, 0f, -9f),
            new Vector3(63.3f, 0f, -9f), new Vector3(58f, 0f, -9f), new Vector3(55.2f, 0f, -10.2f), new Vector3(52f, 0f, -12.5f),
        };

        static readonly Vector3 CarSpawn = new Vector3(140f, 0f, -12.5f);
        static readonly Vector3[] CarTurnIn = { new Vector3(92f, 0f, -12.5f), new Vector3(85f, 0f, -10.6f), new Vector3(79f, 0f, -9f) };
        static readonly Vector3[] CarSlots = { new Vector3(63.3f, 0f, -9f), new Vector3(68.6f, 0f, -9f), new Vector3(73.9f, 0f, -9f) };
        static readonly Vector3[] CarOff = { new Vector3(58f, 0f, -9f), new Vector3(55.4f, 0f, -10.3f), new Vector3(51.5f, 0f, -12.5f), new Vector3(-95f, 0f, -12.5f) };
        static readonly Vector3 MenuBoardAt = new Vector3(69.4f, 0f, -6.75f);

        /// <summary>The food truck's pad, in front of the south-west lot, the truck facing east along it.</summary>
        static readonly Vector3 PadAt = new Vector3(-24.5f, 0f, -6.4f);

        static readonly Vector3[] TruckWestOut =
            { new Vector3(-20.5f, 0f, -6.8f), new Vector3(-18.6f, 0f, -9.4f), new Vector3(-20.6f, 0f, -12f), new Vector3(-26f, 0f, -12.5f), new Vector3(-95f, 0f, -12.5f) };
        static readonly Vector3[] TruckEastOut =
            { new Vector3(-20.5f, 0f, -6.8f), new Vector3(-18.4f, 0f, -9.6f), new Vector3(-15.5f, 0f, -13.6f), new Vector3(-12f, 0f, -14.9f), new Vector3(125f, 0f, -14.9f) };
        static readonly Vector3[] TruckWestHome =
            { new Vector3(-95f, 0f, -14.9f), new Vector3(-34f, 0f, -14.9f), new Vector3(-31f, 0f, -12f), new Vector3(-29.6f, 0f, -8.2f) };
        static readonly Vector3[] TruckEastHome =
            { new Vector3(125f, 0f, -12.5f), new Vector3(-27f, 0f, -12.5f), new Vector3(-31.5f, 0f, -11.2f), new Vector3(-30f, 0f, -8f) };

        /// <summary>The security hut, turned so its windows and sandbags face west across the field toward the compound.</summary>
        static readonly Vector3 HutLocal = new Vector3(1.85f, 0f, 5.5f);
        const float HutYaw = 90f;

        /// <summary>Where the noise maker is set down when the hut is found: out past the east wall, between the east lanes.</summary>
        static readonly Vector3 NoiseMakerStart = new Vector3(49.5f, 0f, 27f);

        /// <summary>The ground the road-side sites cover - and the routes their vehicles drive - for the world edge to keep clear of.</summary>
        public static IEnumerable<Rect> Footprints()
        {
            yield return Rect.MinMaxRect(50.5f, -11.4f, 91f, -3.2f);
            yield return Rect.MinMaxRect(-34f, -11f, -15f, -3.2f);
        }

        // --- The sites, as content -------------------------------------------------------------------

        sealed class SiteSpec
        {
            public string Asset, Id, Plot, PlotName, DisplayName, FeatureName, Blurb;
            public DiscoveryFeature Feature;
        }

        static readonly SiteSpec[] Sites =
        {
            new SiteSpec
            {
                Asset = "Discovery_DriveThrough", Id = "drive_through", Plot = "plot_south_east", PlotName = "SouthEast",
                DisplayName = "Abandoned drive-through booth", FeatureName = "Takeaway service", Feature = DiscoveryFeature.Takeaway,
                Blurb = "Customers stay in their cars. Pack each bag with dishes already cooked on the pass, then hand it out of the window.",
            },
            new SiteSpec
            {
                Asset = "Discovery_FoodTruck", Id = "food_truck", Plot = "plot_south_west", PlotName = "SouthWest",
                DisplayName = "Stranded food truck", FeatureName = "Food truck", Feature = DiscoveryFeature.FoodTruck,
                Blurb = "Load a few dishes from the pass and send the truck out to one place on the map. Each place pays extra for its own favourites.",
            },
            new SiteSpec
            {
                Asset = "Discovery_ShootingRange", Id = "shooting_range", Plot = "plot_west", PlotName = "West",
                DisplayName = "Old shooting range", FeatureName = "Target practice", Feature = DiscoveryFeature.ShootingRange,
                Blurb = "Set one of your defense machines on the bench and tap the cardboard zombies to fire. Short rounds pay coins while the kitchen cooks.",
            },
            new SiteSpec
            {
                Asset = "Discovery_SecurityHut", Id = "security_hut", Plot = "plot_east", PlotName = "East",
                DisplayName = "Abandoned security hut", FeatureName = "Noise maker", Feature = DiscoveryFeature.NoiseMaker,
                Blurb = "Set the noise maker down out past the walls. In an attack, sound it to pull the zombies near it off their trail and onto it - in front of your guns.",
            },
        };

        // --- Menu ---------------------------------------------------------------------------------------

        [MenuItem("Last Orders/Discovery/Set Up Discovery Sites (Current Scene)", priority = 60)]
        static void Run()
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (scope == null)
            {
                EditorUtility.DisplayDialog("Last Orders", "This scene has no GameLifetimeScope. Open the level that has the Last Orders systems in it.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog("Last Orders",
                $"This edits '{scope.gameObject.scene.name}' in place:\n\n" +
                "- Builds the four discovery sites' models, prefabs and popups\n" +
                "- Places a drive-through by the road in front of the south-east lot, a food truck in front of the south-west lot, " +
                "a shooting range on the west lot and a security hut on the east lot\n" +
                "- Moves any debris on those lots that stands where a site does, and clears loose props off the verge\n\n" +
                "Sites from an earlier run are rebuilt from scratch.", "Set up", "Cancel");

            if (proceed) Debug.Log(Apply(scope, save: true));
        }

        [MenuItem("Last Orders/Discovery/Discover All Sites (Play Mode)", priority = 61)]
        static void DiscoverAll()
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            if (!Application.isPlaying || scope == null || scope.Composition == null)
            {
                Debug.Log("[Discovery] Enter play mode in the level first.");
                return;
            }

            foreach (SiteSpec spec in Sites) scope.Composition.Discovery.Discover(spec.Id);
        }

        [MenuItem("Last Orders/Discovery/Discover All Sites (Play Mode)", true)]
        static bool CanDiscoverAll() => Application.isPlaying;

        public static string ApplyToActiveScene(bool save)
        {
            GameLifetimeScope scope = Object.FindFirstObjectByType<GameLifetimeScope>();
            return scope != null ? Apply(scope, save) : "[Discovery] No GameLifetimeScope in the open scene; nothing was changed.";
        }

        public static string Apply(GameLifetimeScope scope, bool save)
        {
            Scene scene = scope.gameObject.scene;
            var report = new StringBuilder("[Discovery] Discovery sites set up.\n");

            K.Reset();
            UiKit.Load();
            ContentGenerator.EnsureFolder(D.Folder);
            ContentGenerator.EnsureFolder(D.MeshFolder);
            ContentGenerator.EnsureFolder(D.PrefabFolder);

            GameBalanceConfig balance = new SerializedObject(scope).FindProperty("balance").objectReferenceValue as GameBalanceConfig;
            WorldUiPrefabGenerator.Ensure(balance);
            report.Append($"  {DiscoveryPopupsBuilder.Build()}\n");

            if (!LandPlotSetup.TryMeasureCompound(scene, out Rect compound))
                return "[Discovery] No compound boundary in the scene; nothing was changed.";

            List<LandPlotSetup.Plot> plots = LandPlotSetup.Layout(compound, LandPlotSetup.MeasureCourtyard(scene));

            var definitions = new Dictionary<string, DiscoverySiteDefinition>();
            foreach (SiteSpec spec in Sites)
            {
                definitions[spec.Id] = Definition(spec, out string problem);
                if (problem != null) report.Append($"  {problem}\n");
            }

            HudSkin skin = UiKit.Skin;
            Sprite Icon(string sprite) => skin != null ? skin[sprite] : null;
            Sprite mystery = Icon(HudSprites.GlyphInfo);

            Transform root = FindOrCreateRoot(scene, RootName);
            for (int i = root.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            report.Append($"  {DriveThrough(scope, root, definitions["drive_through"], mystery, Icon(HudSprites.IconServe))}\n");
            report.Append($"  {FoodTruck(scope, root, definitions["food_truck"], mystery, Icon(HudSprites.IconCrate))}\n");

            LandPlotSetup.Plot west = plots.Find(p => p.Id == "plot_west");
            LandPlotSetup.Plot east = plots.Find(p => p.Id == "plot_east");
            report.Append($"  {Range(scope, root, definitions["shooting_range"], west, mystery, Icon(HudSprites.IconTrophy))}\n");
            report.Append($"  {Hut(scope, root, definitions["security_hut"], east, mystery, Icon(HudSprites.IconShield), Icon(HudSprites.IconZombie))}\n");

            int cleared = 0;
            foreach (Rect footprint in Footprints()) cleared += ClearProps(scene, footprint);
            report.Append($"  {cleared} loose props cleared off the verge.\n");

            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            return report.ToString();
        }

        /// <summary>
        /// A site's definition. Its id, feature and plot are set every run - the scene depends on them -
        /// and its words only when it is created, so tuning them in the inspector sticks.
        /// </summary>
        static DiscoverySiteDefinition Definition(SiteSpec spec, out string problem)
        {
            problem = null;
            string path = Content(spec.Asset + ".asset");
            var asset = AssetDatabase.LoadAssetAtPath<DiscoverySiteDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DiscoverySiteDefinition>();
                asset.displayName = spec.DisplayName;
                asset.featureName = spec.FeatureName;
                asset.featureBlurb = spec.Blurb;
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.id = spec.Id;
            asset.feature = spec.Feature;
            asset.area = AssetDatabase.LoadAssetAtPath<ExpansionAreaDefinition>($"{LandPlotSetup.PlotFolder}/ExpansionArea_Plot_{spec.PlotName}.asset");
            if (asset.area == null)
                problem = $"WARNING: no plot definition for {spec.PlotName}; run 'Set Up Land Plots' first, or '{spec.DisplayName}' can never be found.";

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        // --- The drive-through ------------------------------------------------------------------------------

        static string DriveThrough(GameLifetimeScope scope, Transform root, DiscoverySiteDefinition definition, Sprite mystery, Sprite icon)
        {
            Transform site = Group(root, "Site_DriveThrough", BoothAt, 0f);

            D.Part(site, "Booth", D.Save(DriveThroughModel.Booth(), "DriveThrough_Booth"));

            // Kerbed along its straight, the stop line just ahead of the car at the window.
            var lane = new List<Vector3>();
            foreach (Vector3 p in LaneLine) lane.Add(p - BoothAt);
            float length = Along(LaneLine, LaneLine[LaneLine.Length - 1]);
            float atWindow = Along(LaneLine, CarSlots[0]);
            GameObject laneMesh = D.Part(site, "Lane", D.Save(DriveThroughModel.Lane(lane, 3.2f, 6f, length - 5.5f, atWindow + 2.4f), "DriveThrough_Lane"));
            DisableShadows(laneMesh);

            Transform menu = Group(site, "MenuBoard", MenuBoardAt, 0f);
            D.Part(menu, "Board", D.Save(DriveThroughModel.MenuBoard(), "DriveThrough_MenuBoard"));
            GameObject menuDerelict = D.Part(menu, "Derelict", D.Save(DriveThroughModel.MenuBoardDerelict(), "DriveThrough_MenuBoard_Derelict"));
            GameObject menuRestored = D.Part(menu, "Restored", D.Save(DriveThroughModel.MenuBoardRestored(), "DriveThrough_MenuBoard_Restored"));

            GameObject derelict = D.Part(site, "Derelict", D.Save(DriveThroughModel.Derelict(), "DriveThrough_Derelict"));
            GameObject restored = D.Part(site, "Restored", D.Save(DriveThroughModel.Restored(), "DriveThrough_Restored"));

            GameObject hatch = D.Part(site, "Hatch", D.Save(DriveThroughModel.Hatch(), "DriveThrough_Hatch"));
            hatch.transform.localPosition = DriveThroughModel.HatchClosed;

            Transform window = Point(site, "Window", BoothAt + DriveThroughModel.ServePoint);
            Transform marker = Point(site, "Marker", BoothAt + DriveThroughModel.Marker);

            Transform route = Group(site, "Route", BoothAt, 0f);
            Transform spawn = Point(route, "Spawn", CarSpawn);
            Transform[] turnIn = Points(route, "TurnIn", CarTurnIn);
            Transform[] slots = Points(route, "Slot", CarSlots);
            foreach (Transform slot in slots) slot.rotation = Quaternion.LookRotation(Vector3.left);
            Transform[] off = Points(route, "Off", CarOff);

            GameObject bag = BagPrefab();

            var cars = new List<VehicleDefinition>();
            foreach (string id in new[] { "Hatchback", "Saloon", "Pickup", "Van" })
            {
                var car = AssetDatabase.LoadAssetAtPath<VehicleDefinition>($"{VehicleFolder}/Vehicle_{id}.asset");
                if (car != null && car.prefab != null) cars.Add(car);
            }

            TapBox(site, new Vector3(0f, 1.5f, 0f), new Vector3(DriveThroughModel.Width + 0.6f, 3.2f, DriveThroughModel.Depth + 0.8f));

            var discovery = site.gameObject.AddComponent<DiscoverySite>();
            discovery.Configure(scope, definition, new[] { derelict, menuDerelict }, new[] { restored, menuRestored }, marker, mystery);
            AsFound(new[] { derelict, menuDerelict }, new[] { restored, menuRestored });

            var booth = site.gameObject.AddComponent<DriveThroughBooth>();
            booth.Configure(spawn, turnIn, slots, off, cars.ToArray(), hatch.transform, window, bag, icon);
            var so = new SerializedObject(booth);
            so.FindProperty("hatchOpen").floatValue = DriveThroughModel.HatchSlide;
            so.ApplyModifiedPropertiesWithoutUndo();

            return $"Drive-through at {BoothAt}: {cars.Count} kinds of car, {slots.Length} places in the lane, found by claiming the " +
                   $"{(definition.area != null ? definition.area.displayName : "(missing plot)")}.";
        }

        /// <summary>How far along <paramref name="line"/> the point nearest <paramref name="at"/> lies, in metres.</summary>
        static float Along(Vector3[] line, Vector3 at)
        {
            float walked = 0f, best = 0f, nearest = float.MaxValue;
            for (int i = 1; i < line.Length; i++)
            {
                Vector3 a = line[i - 1], b = line[i];
                float length = Vector3.Distance(a, b);
                float t = Mathf.Clamp01(Vector3.Dot(at - a, b - a) / Mathf.Max(1e-4f, length * length));
                float d = Vector3.Distance(at, Vector3.Lerp(a, b, t));
                if (d < nearest)
                {
                    nearest = d;
                    best = walked + t * length;
                }

                walked += length;
            }

            return best;
        }

        /// <summary>The paper bag passed out of the window, as a prefab the booth throws.</summary>
        static GameObject BagPrefab()
        {
            string path = $"{D.PrefabFolder}/Discovery_TakeawayBag.prefab";
            Mesh mesh = D.Save(DriveThroughModel.Bag(), "DriveThrough_Bag");

            var go = new GameObject("TakeawayBag");
            B.Show(go, mesh);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // --- The food truck ----------------------------------------------------------------------------------

        static string FoodTruck(GameLifetimeScope scope, Transform root, DiscoverySiteDefinition definition, Sprite mystery, Sprite icon)
        {
            Transform site = Group(root, "Site_FoodTruck", PadAt, 0f);

            GameObject pad = D.Part(site, "Pad", D.Save(FoodTruckPadModel.Pad(), "FoodTruck_Pad"));
            DisableShadows(pad);
            GameObject derelict = D.Part(site, "Derelict", D.Save(FoodTruckPadModel.Derelict(), "FoodTruck_Pad_Derelict"));
            GameObject restored = D.Part(site, "Restored", D.Save(FoodTruckPadModel.Restored(), "FoodTruck_Pad_Restored"));

            Transform parked = Point(site, "Parked", PadAt + Vector3.right * 0.3f);
            parked.rotation = Quaternion.LookRotation(Vector3.right);

            VehicleDefinition kind = TruckDefinition();
            GameObject prefab = VehicleModels.BuildFoodTruck(kind, $"{D.MeshFolder}/FoodTruck", $"{D.PrefabFolder}/Discovery_FoodTruck.prefab");

            var truck = (GameObject)PrefabUtility.InstantiatePrefab(prefab, site);
            truck.name = "FoodTruck";
            truck.transform.SetPositionAndRotation(parked.position, parked.rotation);
            Undo.RegisterCreatedObjectUndo(truck, "Food truck");

            Transform body = truck.transform.Find("Body");
            GameObject truckDerelict = body != null ? body.Find("Derelict")?.gameObject : null;
            GameObject truckRestored = body != null ? body.Find("Restored")?.gameObject : null;
            Transform awning = body != null ? body.Find("Awning") : null;

            Transform marker = Point(site, "Marker", PadAt + Vector3.up * 4.3f);
            Transform routes = Group(site, "Routes", PadAt, 0f);

            var discovery = site.gameObject.AddComponent<DiscoverySite>();
            discovery.Configure(scope, definition, new[] { derelict, truckDerelict }, new[] { restored, truckRestored }, marker, mystery);
            AsFound(new[] { derelict, truckDerelict }, new[] { restored, truckRestored });

            var depot = site.gameObject.AddComponent<FoodTruckDepot>();
            depot.Configure(truck.GetComponent<VisitorVehicle>(), parked, Points(routes, "WestOut", TruckWestOut), Points(routes, "EastOut", TruckEastOut),
                            Points(routes, "WestHome", TruckWestHome), Points(routes, "EastHome", TruckEastHome), awning, icon);

            return $"Food truck at {PadAt}, found by claiming the {(definition.area != null ? definition.area.displayName : "(missing plot)")}.";
        }

        /// <summary>The truck as a vehicle: what it drives like. Made if missing; its numbers are the designer's after that.</summary>
        static VehicleDefinition TruckDefinition()
        {
            string path = Content("Vehicle_FoodTruck.asset");
            var asset = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<VehicleDefinition>();
            asset.displayName = "Food truck";
            asset.bodySize = new Vector3(2.0f, 2.5f, 4.8f);
            asset.bodyColour = D.Teal;
            asset.cabinColour = D.Cream;
            asset.wheelRadius = 0.44f;
            asset.driveSpeed = 5.5f;
            asset.manoeuvreSpeed = 2.4f;
            asset.turnSpeed = 110f;
            asset.arriveRadius = 0.7f;
            asset.bayFootprint = new Vector2(2.8f, 5.4f);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // --- The shooting range ------------------------------------------------------------------------------

        static string Range(GameLifetimeScope scope, Transform root, DiscoverySiteDefinition definition, LandPlotSetup.Plot plot, Sprite mystery, Sprite icon)
        {
            if (plot == null) return "WARNING: no west lot in the layout; the shooting range was not placed.";

            var centre = new Vector3(plot.Land.center.x, 0f, plot.Land.center.y);
            Transform site = Group(root, "Site_ShootingRange", centre, 0f);

            D.Part(site, "Range", D.Save(ShootingRangeModel.Range(), "ShootingRange"));
            GameObject derelict = D.Part(site, "Derelict", D.Save(ShootingRangeModel.Derelict(), "ShootingRange_Derelict"));
            GameObject restored = D.Part(site, "Restored", D.Save(ShootingRangeModel.Restored(), "ShootingRange_Restored"));

            Transform mount = Point(site, "Mount", centre + ShootingRangeModel.Mount);
            mount.rotation = Quaternion.LookRotation(Vector3.forward);
            Transform view = Point(site, "View", centre + new Vector3(0f, 0f, -0.5f));
            Transform marker = Point(site, "Marker", centre + ShootingRangeModel.Marker);

            // The live targets, and the score and the clock, only once the range is back at work.
            GameObject targetPrefab = TargetPrefab();
            var targets = new List<RangeTarget>();
            foreach (Vector3 rail in ShootingRangeModel.Rails)
            {
                var target = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab, restored.transform);
                target.transform.position = centre + rail;
                var component = target.GetComponent<RangeTarget>();
                Transform carriage = target.transform.Find("Carriage");
                component.Configure(carriage.Find("Hinge"), carriage, new Vector3(-ShootingRangeModel.RailHalf, 0f, 0f), new Vector3(ShootingRangeModel.RailHalf, 0f, 0f));
                targets.Add(component);
            }

            Vector3 board = centre + ShootingRangeModel.BoardAt + new Vector3(0f, ShootingRangeModel.BoardLift + ShootingRangeModel.BoardSize.y * 0.5f - 0.06f, -0.1f);
            TMP_Text score = BoardText(restored.transform, "Score", board + new Vector3(-0.42f, 0f, 0f), new Vector2(0.62f, 0.5f), HudPalette.Gold);
            TMP_Text time = BoardText(restored.transform, "Time", board + new Vector3(0.48f, 0f, 0f), new Vector2(0.62f, 0.4f), HudPalette.Cream);

            TapBox(site, new Vector3(0f, 1.4f, -5.1f), new Vector3(5.4f, 2.8f, 3.2f));

            var discovery = site.gameObject.AddComponent<DiscoverySite>();
            discovery.Configure(scope, definition, new[] { derelict }, new[] { restored }, marker, mystery);
            AsFound(new[] { derelict }, new[] { restored });

            var machines = new[]
            {
                DefenseContentGenerator.ScrapTurretPath, DefenseContentGenerator.CornShooterPath, DefenseContentGenerator.TomatoShooterPath,
                DefenseContentGenerator.CabbageShooterPath, DefenseContentGenerator.PlateShooterPath, DefenseContentGenerator.ToastapultPath,
            }.Select(AssetDatabase.LoadAssetAtPath<DefenseDefinition>).Where(d => d != null && d.prefab != null).ToArray();

            var range = site.gameObject.AddComponent<ShootingRange>();
            range.Configure(mount, targets.ToArray(), machines, view, score, time, icon);

            // Keep-outs, in the range's own frame: the firing point, and the berms and runners.
            var keepOut = new List<Rect>
            {
                Rect.MinMaxRect(centre.x - 5.7f, centre.z - 6.9f, centre.x + 5.7f, centre.z - 3.1f),
                Rect.MinMaxRect(centre.x - 5.9f, centre.z + 1.6f, centre.x + 5.9f, centre.z + 7.2f),
            };
            string moved = Relocate(plot, keepOut);

            return $"Shooting range on the {plot.DisplayName}, {machines.Length} machines to practise with, {targets.Count} targets; {moved}";
        }

        /// <summary>
        /// The cardboard zombie target as a prefab: a trolley on the runner, the hinge on top of it, the
        /// cut-out on the hinge, and a tap box round the cut-out that folds with it.
        /// </summary>
        static GameObject TargetPrefab()
        {
            string path = $"{D.PrefabFolder}/Discovery_RangeTarget.prefab";
            Mesh trolley = D.Save(ShootingRangeModel.Trolley(), "RangeTarget_Trolley");
            Mesh cutOut = D.Save(ShootingRangeModel.CardboardZombie(), "RangeTarget_CardboardZombie");

            var root = new GameObject("RangeTarget");
            Transform carriage = new GameObject("Carriage").transform;
            carriage.SetParent(root.transform, false);
            B.Show(carriage.gameObject, trolley);

            Transform hinge = new GameObject("Hinge").transform;
            hinge.SetParent(carriage, false);
            hinge.localPosition = new Vector3(0f, ShootingRangeModel.HingeHeight, -0.02f);
            D.Part(hinge, "CutOut", cutOut);

            var box = hinge.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.85f, 0f);
            box.size = new Vector3(1.4f, 1.7f, 0.35f);

            root.AddComponent<RangeTarget>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>A line of lettering on the scoreboard's face, in the HUD's display font, sized to fit its box.</summary>
        static TMP_Text BoardText(Transform parent, string name, Vector3 at, Vector2 size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.identity;

            var text = go.AddComponent<TextMeshPro>();
            ((RectTransform)go.transform).sizeDelta = size;
            if (UiKit.Skin != null && UiKit.Skin.display != null) text.font = UiKit.Skin.display;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.5f;
            text.fontSizeMax = 6f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = colour;
            text.text = "--";
            return text;
        }

        // --- The security hut ----------------------------------------------------------------------------------

        static string Hut(GameLifetimeScope scope, Transform root, DiscoverySiteDefinition definition, LandPlotSetup.Plot plot, Sprite mystery, Sprite icon,
                            Sprite alarmIcon)
        {
            if (plot == null) return "WARNING: no east lot in the layout; the security hut was not placed.";

            Vector3 at = new Vector3(plot.Land.center.x, 0f, plot.Land.center.y) + HutLocal;
            Transform site = Group(root, "Site_SecurityHut", at, HutYaw);

            D.Part(site, "Hut", D.Save(SecurityHutModel.Hut(), "SecurityHut"));
            GameObject derelict = D.Part(site, "Derelict", D.Save(SecurityHutModel.Derelict(), "SecurityHut_Derelict"));
            GameObject restored = D.Part(site, "Restored", D.Save(SecurityHutModel.Restored(), "SecurityHut_Restored"));
            GameObject alarm = D.Part(site, "AlarmBeacon", D.Save(SecurityHutModel.AlarmBeacon(), "SecurityHut_AlarmBeacon"));
            alarm.SetActive(false);

            Transform marker = Point(site, "Marker", site.TransformPoint(SecurityHutModel.Marker));

            // The noise maker: out on the ground once it is set down, its parts to spin and pulse.
            Transform device = Group(site, "NoiseMaker", NoiseMakerStart, 0f);
            D.Part(device, "Body", D.Save(SecurityHutModel.Device(), "NoiseMaker"));
            GameObject beacon = D.Part(device, "Beacon", D.Save(SecurityHutModel.DeviceBeacon(), "NoiseMaker_Beacon"));
            beacon.transform.localPosition = Vector3.up * (SecurityHutModel.DeviceHead + 0.23f);

            Mesh ringMesh = D.Save(SecurityHutModel.SoundRing(), "NoiseMaker_SoundRing");
            var rings = new Transform[3];
            for (int i = 0; i < rings.Length; i++)
            {
                GameObject ring = D.Part(device, $"SoundRing_{i}", ringMesh);
                ring.transform.localPosition = Vector3.up * (SecurityHutModel.DeviceHead + 0.11f);
                DisableShadows(ring);
                rings[i] = ring.transform;
            }

            GameObject earshot = D.Part(device, "Earshot", D.Save(SecurityHutModel.Earshot(), "NoiseMaker_Earshot"));
            DisableShadows(earshot);
            earshot.SetActive(false);

            Transform deviceMarker = Point(device, "Marker", NoiseMakerStart + Vector3.up * 2.4f);
            var tap = device.gameObject.AddComponent<BoxCollider>();
            tap.center = new Vector3(0f, 1.0f, 0f);
            tap.size = new Vector3(1.4f, 2.0f, 1.4f);
            device.gameObject.AddComponent<NoiseMakerDevice>();

            Transform start = Point(site, "NoiseMakerStart", NoiseMakerStart);

            TapBox(site, new Vector3(0f, 1.5f, -0.4f), new Vector3(SecurityHutModel.Width + 0.8f, 3.2f, SecurityHutModel.Depth + 1.6f));

            var discovery = site.gameObject.AddComponent<DiscoverySite>();
            discovery.Configure(scope, definition, new[] { derelict }, new[] { restored }, marker, mystery);
            AsFound(new[] { derelict }, new[] { restored });

            var hut = site.gameObject.AddComponent<SecurityHut>();
            hut.Configure(device, start, deviceMarker, earshot.transform, beacon.transform, rings, alarm, icon, alarmIcon);
            device.GetComponent<NoiseMakerDevice>().Configure(hut);
            device.gameObject.SetActive(false);

            // The hut's footprint in the world, for the plot's debris to keep off.
            var corners = new[] { new Vector3(-3.5f, 0f, -3.7f), new Vector3(2.7f, 0f, -3.7f), new Vector3(-3.5f, 0f, 2.0f), new Vector3(2.7f, 0f, 2.0f) }
                .Select(site.TransformPoint).ToArray();
            var keepOut = new List<Rect>
            {
                Rect.MinMaxRect(corners.Min(c => c.x), corners.Min(c => c.z), corners.Max(c => c.x), corners.Max(c => c.z)),
            };
            string moved = Relocate(plot, keepOut);

            return $"Security hut on the {plot.DisplayName} at {at}; its noise maker starts at {NoiseMakerStart}; {moved}";
        }

        // --- Moving debris out of a site's way -------------------------------------------------------------------

        /// <summary>
        /// Moves every piece of debris on <paramref name="plot"/> that stands on any of <paramref name="keepOut"/>
        /// to the nearest open ground on the same plot - off the keep-outs, clear of the other debris and of the
        /// way in from the gate - turning a long piece a quarter turn if that is what lets it fit. A piece with
        /// nowhere at all to go is cleared away, as long as the plot keeps enough to be worth clearing.
        /// </summary>
        static string Relocate(LandPlotSetup.Plot plot, List<Rect> keepOut)
        {
            ExpansionArea area = Object.FindObjectsByType<ExpansionArea>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(a => a.Definition != null && a.Definition.id == plot.Id);
            if (area == null) return "no plot in the scene to clear.";

            Transform debrisRoot = area.transform.Find("Debris");
            if (debrisRoot == null) return "no debris on the plot.";

            var pieces = debrisRoot.GetComponentsInChildren<Debris>(true).Select(d => d.transform).ToList();
            var footprints = pieces.ToDictionary(t => t, Footprint);

            Vector2 gate = LandPlotSetup.GatePoint(plot);
            Vector2 approachEnd = gate - LandPlotSetup.Outward(plot.Front) * 5.1f;

            Rect land = plot.Land;
            int moved = 0, turned = 0, removed = 0;

            foreach (Transform piece in pieces)
            {
                Rect here = footprints[piece];
                if (!Overlaps(here, keepOut, 0f)) continue;

                var pivot = new Vector2(piece.position.x, piece.position.z);
                Vector2 bestAt = default;
                bool bestTurned = false;
                float bestScore = float.MaxValue;

                foreach (bool turn in new[] { false, true })
                {
                    // The footprint about its pivot, a quarter turn round it if turned.
                    Vector2 offset = here.center - pivot;
                    Vector2 half = here.size * 0.5f;
                    if (turn)
                    {
                        offset = new Vector2(offset.y, -offset.x);
                        half = new Vector2(half.y, half.x);
                    }

                    for (float x = land.xMin + 0.9f + half.x; x <= land.xMax - 0.9f - half.x; x += 0.4f)
                    {
                        for (float z = land.yMin + 0.9f + half.y; z <= land.yMax - 0.9f - half.y; z += 0.4f)
                        {
                            var footprint = new Rect(new Vector2(x, z) - half, half * 2f);
                            if (Overlaps(footprint, keepOut, 0.3f)) continue;
                            if (RectToSegment(footprint, gate, approachEnd) < 1.1f) continue;

                            bool crowded = false;
                            foreach (KeyValuePair<Transform, Rect> other in footprints)
                                if (other.Key != piece && Inflate(other.Value, 0.6f).Overlaps(footprint)) crowded = true;
                            if (crowded) continue;

                            float score = Vector2.Distance(footprint.center, here.center) + (turn ? 1.5f : 0f);
                            if (score >= bestScore) continue;

                            bestScore = score;
                            bestAt = new Vector2(x, z) - offset;
                            bestTurned = turn;
                        }
                    }
                }

                if (bestScore == float.MaxValue)
                {
                    // Left standing on the site it would be the first thing anybody saw; better gone, while the plot has plenty more.
                    if (footprints.Count > 3)
                    {
                        footprints.Remove(piece);
                        Undo.DestroyObjectImmediate(piece.gameObject);
                        removed++;
                    }

                    continue;
                }

                Undo.RecordObject(piece, "Move debris off a discovery site");
                if (bestTurned) piece.RotateAround(piece.position, Vector3.up, 90f);
                piece.position = new Vector3(bestAt.x, piece.position.y, bestAt.y);
                footprints[piece] = Footprint(piece);
                moved++;
                if (bestTurned) turned++;
            }

            var line = new StringBuilder($"{moved} pieces of debris moved off it");
            if (turned > 0) line.Append($" ({turned} turned to fit)");
            if (removed > 0) line.Append($", {removed} with nowhere to go cleared away");
            return line.Append(".").ToString();
        }

        /// <summary>
        /// The ground a piece of debris covers, from its tap box: worked out from the box itself rather than
        /// the collider's bounds, which in the editor can come back empty before physics has caught up.
        /// </summary>
        static Rect Footprint(Transform piece)
        {
            var box = piece.GetComponent<BoxCollider>();
            if (box == null) return new Rect(piece.position.x - 1f, piece.position.z - 1f, 2f, 2f);

            float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3(box.center.x + box.size.x * ((i & 1) == 0 ? -0.5f : 0.5f), box.center.y,
                                         box.center.z + box.size.z * ((i & 2) == 0 ? -0.5f : 0.5f));
                Vector3 world = piece.TransformPoint(corner);
                xMin = Mathf.Min(xMin, world.x);
                xMax = Mathf.Max(xMax, world.x);
                zMin = Mathf.Min(zMin, world.z);
                zMax = Mathf.Max(zMax, world.z);
            }

            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        static bool Overlaps(Rect footprint, List<Rect> keepOut, float margin)
        {
            foreach (Rect r in keepOut)
                if (Inflate(r, margin).Overlaps(footprint)) return true;
            return false;
        }

        static Rect Inflate(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        /// <summary>How close a segment comes to a rectangle, near enough: sampled along the segment.</summary>
        static float RectToSegment(Rect r, Vector2 a, Vector2 b)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i <= 16; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / 16f);
                float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
                float dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
                nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dz * dz));
            }

            return nearest;
        }

        /// <summary>
        /// Takes the world edge's loose props - and the old environment's - off <paramref name="area"/>: anything
        /// small enough to be a prop whose middle stands in it. The world-edge builder keeps clear of these
        /// footprints when it next runs, so this is what a rebuild would leave anyway.
        /// </summary>
        static int ClearProps(Scene scene, Rect area)
        {
            var groups = new List<Transform>();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == "WorldEnvironment")
                {
                    Transform outskirts = go.transform.Find("Outskirts");
                    if (outskirts == null) continue;
                    foreach (string sector in new[] { "North", "West", "East", "South" })
                    {
                        Transform group = outskirts.Find(sector);
                        if (group != null) groups.Add(group);
                    }
                }
            }

            int removed = 0;
            foreach (Transform group in groups)
            {
                var doomed = new List<GameObject>();
                foreach (Transform prop in group)
                {
                    Renderer[] renderers = prop.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) continue;

                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                    if (bounds.size.x > 8f || bounds.size.z > 8f) continue;
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

        /// <summary>
        /// Leaves a site looking as it will when the game starts: its found dressing showing, its working
        /// dressing put away - so the level reads the same in the editor as it does in play.
        /// </summary>
        static void AsFound(GameObject[] derelict, GameObject[] restored)
        {
            foreach (GameObject part in derelict)
                if (part != null) part.SetActive(true);
            foreach (GameObject part in restored)
                if (part != null) part.SetActive(false);
        }

        // --- Plumbing -----------------------------------------------------------------------------------------

        static Transform Group(Transform parent, string name, Vector3 at, float yaw)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            return go.transform;
        }

        static Transform Point(Transform parent, string name, Vector3 world)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = world;
            return go.transform;
        }

        static Transform[] Points(Transform parent, string name, Vector3[] world)
        {
            Transform group = Point(parent, name, parent.position);
            var points = new Transform[world.Length];
            for (int i = 0; i < world.Length; i++) points[i] = Point(group, $"{name}_{i}", world[i]);
            return points;
        }

        static void TapBox(Transform site, Vector3 centre, Vector3 size)
        {
            var box = site.gameObject.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        static void DisableShadows(GameObject go)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Transform FindOrCreateRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;

            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, name);
            return root.transform;
        }
    }
}
