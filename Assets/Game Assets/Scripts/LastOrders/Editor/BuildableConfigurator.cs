using System.Collections.Generic;
using System.IO;
using LastOrders.Data;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Fills in the Last Orders fields on the project's buildables, and adds the handful the
    /// GDD needs that do not exist yet.
    ///
    /// The existing assets - Table, Wall, Door, the stoves, the plant pot - already have
    /// prefabs and art. Rather than authoring a parallel set, this enriches them in place
    /// with cost, health, Heat and role, which is what turns a decorative table into a thing
    /// that competes with a barricade for a tile.
    ///
    /// New buildables reuse existing prefabs deliberately. A grey-box barricade that looks
    /// like a wall is the correct Phase A answer: Gate 1 asks whether "one more table before
    /// dusk" creates tension, and that question does not need bespoke art to answer.
    /// </summary>
    public static class BuildableConfigurator
    {
        const string SourceFolder = "Assets/GridBuildingSystem/Scripts/BuildingTypes";
        const string NewFolder = "Assets/Game Assets/Content/LastOrders/Buildables";

        [MenuItem("Last Orders/Configure Buildables", priority = 1)]
        public static void Configure()
        {
            var existing = LoadExisting();

            ConfigureExisting(existing);
            CreateNew(existing);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Last Orders] Buildables configured. Existing assets enriched in place; " +
                      "defense and utility pieces created under " + NewFolder + ".");
        }

        static Dictionary<string, PlacedObjectTypeSO> LoadExisting()
        {
            var map = new Dictionary<string, PlacedObjectTypeSO>();

            string[] guids = AssetDatabase.FindAssets("t:PlacedObjectTypeSO", new[] { SourceFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(path);
                if (asset != null) map[asset.name] = asset;
            }

            return map;
        }

        static void ConfigureExisting(Dictionary<string, PlacedObjectTypeSO> existing)
        {
            // --- Service ---
            // A 4-top. Seating is the revenue lever and the thing that competes with defense
            // for floor area.
            Apply(existing, "Table", BuildCategory.Service, minutes: 1f, hp: 60f,
                  seats: 4, heatPerTile: 0.5f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 5) });

            // --- Structure ---
            // The default wall. Cheap, flammable, and a speed bump rather than an answer.
            Apply(existing, "Wall", BuildCategory.Structure, minutes: 2f, hp: 100f,
                  isWall: true, isPerimeter: true, heatPerTile: 0f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 4) });

            Apply(existing, "BigWall", BuildCategory.Structure, minutes: 6f, hp: 350f,
                  isWall: true, isPerimeter: true, heatPerTile: 0f,
                  cost: new[]
                  {
                      new ResourceAmount(ResourceType.Scrap, 6),
                      new ResourceAmount(ResourceType.Parts, 4),
                  });

            // The load-bearing object of the whole design (7). Softest point in the wall, and
            // the only way revenue gets in.
            Apply(existing, "Door", BuildCategory.Structure, minutes: 2f, hp: 70f,
                  isDoor: true, isPerimeter: true, heatPerTile: 0f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 6) });

            // --- Production ---
            // The Hearth. Indestructible by 15.1, which is the single flag that makes a
            // softlock impossible: the player can always cook tomorrow.
            Apply(existing, "SoupStove", BuildCategory.Production, minutes: 4f, hp: 120f,
                  isHearth: true, heatPerTile: 1f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 8) });

            Apply(existing, "BreadStove", BuildCategory.Production, minutes: 4f, hp: 120f,
                  heatPerTile: 1f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 8) });

            Apply(existing, "PlantPot", BuildCategory.Production, minutes: 2f, hp: 40f,
                  heatPerTile: 0.2f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 3) });

            // --- Comfort ---
            foreach (string name in new[] { "BarrelProp", "WellProp", "LetterBoxProp", "Cafe" })
            {
                Apply(existing, name, BuildCategory.Comfort, minutes: 1f, hp: 30f,
                      heatPerTile: 0.3f, comfort: 1.05f,
                      cost: new[] { new ResourceAmount(ResourceType.Scrap, 2) });
            }
        }

        static void Apply(Dictionary<string, PlacedObjectTypeSO> map, string name,
                          BuildCategory category, float minutes, float hp,
                          ResourceAmount[] cost, int seats = 0, bool isDoor = false,
                          bool isWall = false, bool isPerimeter = false, bool isHearth = false,
                          float heatPerTile = 0.5f, float comfort = 1f)
        {
            if (!map.TryGetValue(name, out PlacedObjectTypeSO asset))
            {
                Debug.LogWarning($"[Last Orders] Buildable '{name}' not found under {SourceFolder}. Skipped.");
                return;
            }

            asset.buildCategory = category;
            asset.buildClockMinutes = minutes;
            asset.maxHealth = hp;
            asset.buildCost = cost;
            asset.seatCapacity = seats;
            asset.isDoor = isDoor;
            asset.isWall = isWall;
            asset.isPerimeter = isPerimeter;
            asset.isHearth = isHearth;
            asset.heatPerTile = heatPerTile;
            asset.comfortMultiplier = comfort;
            asset.damagedEfficiency = 0.75f;

            EditorUtility.SetDirty(asset);
        }

        /// <summary>
        /// Creates the buildables the GDD's categories require but the project has no art
        /// for yet, cloning an existing asset so they arrive with a working prefab.
        /// </summary>
        static void CreateNew(Dictionary<string, PlacedObjectTypeSO> existing)
        {
            if (!AssetDatabase.IsValidFolder(NewFolder))
            {
                Directory.CreateDirectory(NewFolder);
                AssetDatabase.Refresh();
            }

            PlacedObjectTypeSO wallSource = Get(existing, "Wall");
            PlacedObjectTypeSO propSource = Get(existing, "BarrelProp") ?? Get(existing, "WellProp");

            // --- Defense (GDD 6.1) ---
            Clone(wallSource, "Barricade", BuildCategory.Defense, minutes: 1f, hp: 80f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 3) },
                  isPerimeter: true);

            Clone(wallSource, "Spikes", BuildCategory.Defense, minutes: 2f, hp: 40f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 5) },
                  isPerimeter: true, dps: 6f, range: 1f);

            // Ten clock-minutes is a sixth of the working day. The GDD prices turrets this
            // high on purpose - the answer to a breach is never a thicker wall, it is a
            // turret, and it has to cost a real part of the day to build one.
            Clone(propSource, "NailTurret", BuildCategory.Defense, minutes: 10f, hp: 90f,
                  cost: new[]
                  {
                      new ResourceAmount(ResourceType.Scrap, 8),
                      new ResourceAmount(ResourceType.Parts, 6),
                  },
                  dps: 22f, range: 4f, consumesAmmo: true);

            Clone(propSource, "Floodlight", BuildCategory.Defense, minutes: 4f, hp: 50f,
                  cost: new[]
                  {
                      new ResourceAmount(ResourceType.Scrap, 4),
                      new ResourceAmount(ResourceType.Parts, 3),
                  },
                  isPoweredLight: true, heatPerTile: 3f);

            // --- Utility ---
            // The cellar earns its cost twice: it shelters customers caught by dusk (9.8) and
            // its contents cannot be stolen (15.6).
            Clone(propSource, "Cellar", BuildCategory.Utility, minutes: 8f, hp: 200f,
                  cost: new[]
                  {
                      new ResourceAmount(ResourceType.Scrap, 10),
                      new ResourceAmount(ResourceType.Parts, 2),
                  },
                  isCellar: true, heatPerTile: 0.2f);

            Clone(propSource, "Pantry", BuildCategory.Utility, minutes: 3f, hp: 70f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 6) },
                  isPantry: true);

            Clone(propSource, "Generator", BuildCategory.Utility, minutes: 15f, hp: 110f,
                  cost: new[]
                  {
                      new ResourceAmount(ResourceType.Scrap, 10),
                      new ResourceAmount(ResourceType.Parts, 8),
                  },
                  isGenerator: true, heatPerTile: 5f);

            Clone(propSource, "WaterTank", BuildCategory.Utility, minutes: 5f, hp: 90f,
                  cost: new[]
                  {
                      new ResourceAmount(ResourceType.Scrap, 6),
                      new ResourceAmount(ResourceType.Parts, 2),
                  });

            Clone(propSource, "TakeawayWindow", BuildCategory.Service, minutes: 3f, hp: 60f,
                  cost: new[] { new ResourceAmount(ResourceType.Scrap, 5) },
                  isTakeaway: true);

            // --- Comfort ---
            Clone(propSource, "Jukebox", BuildCategory.Comfort, minutes: 2f, hp: 40f,
                  cost: new[]
                  {
                      new ResourceAmount(ResourceType.Scrap, 4),
                      new ResourceAmount(ResourceType.Parts, 2),
                  },
                  isJukebox: true, heatPerTile: 4f, comfort: 1.15f);
        }

        static PlacedObjectTypeSO Get(Dictionary<string, PlacedObjectTypeSO> map, string name) =>
            map.TryGetValue(name, out PlacedObjectTypeSO asset) ? asset : null;

        static void Clone(PlacedObjectTypeSO source, string name, BuildCategory category,
                          float minutes, float hp, ResourceAmount[] cost,
                          bool isPerimeter = false, float dps = 0f, float range = 0f,
                          bool consumesAmmo = false, bool isPoweredLight = false,
                          bool isCellar = false, bool isPantry = false, bool isGenerator = false,
                          bool isJukebox = false, bool isTakeaway = false,
                          float heatPerTile = 0.5f, float comfort = 1f)
        {
            string path = $"{NewFolder}/Build_{name}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<PlacedObjectTypeSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PlacedObjectTypeSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.nameString = name;
            asset.isUnlocked = true;

            if (source != null)
            {
                // Borrowing the source's prefab, visual and footprint. These are grey-box
                // stand-ins and are expected to be replaced once the art pass lands.
                asset.prefab = source.prefab;
                asset.visual = source.visual;
                asset.width = source.width;
                asset.height = source.height;
                asset.categoryType = source.categoryType;
                asset.type = source.type;
            }
            else
            {
                asset.width = 1;
                asset.height = 1;
            }

            asset.buildCategory = category;
            asset.buildClockMinutes = minutes;
            asset.maxHealth = hp;
            asset.buildCost = cost;
            asset.isPerimeter = isPerimeter;
            asset.defenseDps = dps;
            asset.defenseRangeTiles = range;
            asset.consumesAmmo = consumesAmmo;
            asset.isPoweredLight = isPoweredLight;
            asset.isCellar = isCellar;
            asset.isPantry = isPantry;
            asset.isGenerator = isGenerator;
            asset.isJukebox = isJukebox;
            asset.isTakeawayWindow = isTakeaway;
            asset.heatPerTile = heatPerTile;
            asset.comfortMultiplier = comfort;
            asset.damagedEfficiency = 0.75f;
            asset.PriceAmount = 0;

            EditorUtility.SetDirty(asset);
        }
    }
}
