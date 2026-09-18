using System.Collections.Generic;
using System.IO;
using LastOrders.Data;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Generates the starting content set from the GDD's tables.
    ///
    /// Written as a generator rather than hand-authored assets for one reason: these numbers
    /// come from a design document that is going to change, and regenerating from a readable
    /// C# table is far easier to review in a diff than forty YAML files. The GDD's 14.1
    /// combat table and 6.4 wall table appear below almost verbatim, which means a balance
    /// change can be applied by editing one line here and re-running.
    ///
    /// Idempotent. Re-running updates existing assets in place rather than duplicating them,
    /// so it is safe to run after every GDD revision. It deliberately does NOT overwrite
    /// hand-tuned values on assets it did not create.
    /// </summary>
    public static class ContentGenerator
    {
        const string Root = "Assets/Game Assets/Content/LastOrders";

        [MenuItem("Last Orders/Generate Starting Content", priority = 0)]
        public static void Generate()
        {
            EnsureFolders();

            HeatWeightsConfig heat = CreateOrLoad<HeatWeightsConfig>($"{Root}/HeatWeights.asset");
            GameBalanceConfig balance = CreateOrLoad<GameBalanceConfig>($"{Root}/GameBalance.asset");

            balance.heatWeights = heat;
            balance.enemies = GenerateEnemies();
            balance.wallMaterials = GenerateWallMaterials();
            balance.customerArchetypes = GenerateCustomers();
            balance.recipes = GenerateRecipes();
            balance.crisisAbilities = GenerateCrisisAbilities();
            balance.plotTiers = GeneratePlotTiers();
            balance.survivorRoles = GenerateRoles();
            balance.survivorTraits = GenerateTraits();
            balance.waveManifests = GenerateWaveManifests(balance.enemies);
            balance.namedNights = GenerateNamedNights(balance.waveManifests);
            balance.dispatchDestinations = GenerateDispatch();

            EditorUtility.SetDirty(balance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Last Orders] Generated starting content under {Root}.\n" +
                      $"{balance.enemies.Length} enemies, {balance.customerArchetypes.Length} archetypes, " +
                      $"{balance.recipes.Length} recipes, {balance.plotTiers.Length} plot tiers.");

            Selection.activeObject = balance;
        }

        // --- Enemies (GDD 14 and 14.1) ---------------------------------------
        //
        // Every archetype exists to break one specific dominant strategy. The behavioural
        // difference is entirely the navigation field - there is no per-enemy code anywhere.

        static EnemyDefinition[] GenerateEnemies()
        {
            return new[]
            {
                Enemy("Shambler", ThreatNavigation.Doors, dps: 6, hp: 30, speed: 1.0f,
                      colour: new Color(0.55f, 0.65f, 0.45f), cost: 1f, day: 1,
                      desc: "Slow ones on the road. Plenty of them."),

                Enemy("Runner", ThreatNavigation.Doors, dps: 4, hp: 20, speed: 2.2f,
                      colour: new Color(1f, 0.55f, 0.1f), cost: 1.5f, day: 3,
                      desc: "Something fast came through the fence at the depot.",
                      immuneToSlow: true),

                Enemy("Brute", ThreatNavigation.Breach, dps: 25, hp: 180, speed: 0.7f,
                      colour: new Color(0.6f, 0.15f, 0.15f), cost: 6f, day: 8,
                      desc: "Wall came down at the depot. Not the door. The wall."),

                Enemy("Burner", ThreatNavigation.Doors, dps: 10, hp: 40, speed: 1.1f,
                      colour: new Color(1f, 0.75f, 0.2f), cost: 4f, day: 13,
                      desc: "Burned out the tent camp on Fourth. Whole thing went up in about a minute.",
                      ignites: true),

                Enemy("Flanker", ThreatNavigation.WeakestPoint, dps: 8, hp: 35, speed: 1.4f,
                      colour: new Color(0.55f, 0.3f, 0.7f), cost: 3f, day: 15,
                      desc: "They do not come at the front. They find the thin part."),

                Enemy("Glutton", ThreatNavigation.Pantry, dps: 5, hp: 90, speed: 0.9f,
                      colour: new Color(0.95f, 0.9f, 0.5f), cost: 3.5f, day: 18,
                      desc: "Went straight past the people. Straight to the stores."),

                Enemy("Screecher", ThreatNavigation.Doors, dps: 0, hp: 25, speed: 1.0f,
                      colour: new Color(0.6f, 0.8f, 0.95f), cost: 4f, day: 35,
                      desc: "Something out there is calling the others in.",
                      nonCombatant: true, heatPerSecond: 0.6f),

                Enemy("Raider", ThreatNavigation.Doors, dps: 12, hp: 50, speed: 1.2f,
                      colour: new Color(0.7f, 0.65f, 0.4f), cost: 5f, day: 22,
                      desc: "People this time. Armed, and they know what a pantry looks like.",
                      steals: true),
            };
        }

        static EnemyDefinition Enemy(string name, ThreatNavigation nav, float dps, float hp, float speed,
                                     Color colour, float cost, int day, string desc,
                                     bool ignites = false, bool steals = false, bool immuneToSlow = false,
                                     bool nonCombatant = false, float heatPerSecond = 0f)
        {
            var asset = CreateOrLoad<EnemyDefinition>($"{Root}/Enemies/Enemy_{name}.asset");

            asset.displayName = name;
            asset.description = desc;
            asset.silhouetteColour = colour;
            asset.navigation = nav;
            asset.structureDps = dps;
            asset.maxHealth = hp;
            asset.moveSpeed = speed;
            asset.threatCost = cost;
            asset.earliestDay = day;
            asset.ignitesStructures = ignites;
            asset.stealsResources = steals;
            asset.immuneToSlow = immuneToSlow;
            asset.isNonCombatant = nonCombatant;
            asset.heatPerSecond = heatPerSecond;
            asset.agentRadius = 0.35f;
            asset.attackRange = 0.6f;
            asset.poolPrewarm = 16;

            asset.drops = new[] { new ResourceAmount(ResourceType.Scrap, 1) };

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Walls (GDD 6.4) --------------------------------------------------

        static WallMaterialDefinition[] GenerateWallMaterials()
        {
            return new[]
            {
                Wall("ScrapWood", hp: 100, minutes: 2f, flammable: true, fireproof: false, heat: 0f, tier: 1,
                     new ResourceAmount(ResourceType.Scrap, 4)),

                Wall("Corrugated", hp: 180, minutes: 3f, flammable: false, fireproof: false, heat: 1f, tier: 1,
                     new ResourceAmount(ResourceType.Scrap, 8)),

                Wall("Brick", hp: 350, minutes: 6f, flammable: false, fireproof: true, heat: 0f, tier: 1,
                     new ResourceAmount(ResourceType.Scrap, 6), new ResourceAmount(ResourceType.Parts, 4)),

                Wall("Reinforced", hp: 600, minutes: 10f, flammable: false, fireproof: true, heat: 0f, tier: 4,
                     new ResourceAmount(ResourceType.Scrap, 12), new ResourceAmount(ResourceType.Parts, 8)),
            };
        }

        static WallMaterialDefinition Wall(string name, float hp, float minutes, bool flammable,
                                           bool fireproof, float heat, int tier, params ResourceAmount[] cost)
        {
            var asset = CreateOrLoad<WallMaterialDefinition>($"{Root}/Walls/Wall_{name}.asset");

            asset.displayName = name;
            asset.health = hp;
            asset.buildClockMinutes = minutes;
            asset.flammable = flammable;
            asset.fireproof = fireproof;
            asset.heatPerSegment = heat;
            asset.requiredPlotTier = tier;
            asset.cost = cost;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Customers (GDD 9.6) ----------------------------------------------
        //
        // The weights per Reputation tier are what make the clientele visibly improve as the
        // restaurant does, which the GDD calls a strong and free progression signal.

        static CustomerArchetypeDefinition[] GenerateCustomers()
        {
            return new[]
            {
                Customer("Drifter", 1, 1, coin: 12, patience: 4f, weights: new[] { 4f, 2f, 1f, 0.5f }),

                Customer("Family", 3, 4, coin: 22, patience: 9f, weights: new[] { 1f, 3f, 3f, 2f },
                         satisfied: 2f, recruit: 0.05f),

                Customer("Trader", 1, 2, coin: 10, patience: 6f, weights: new[] { 1f, 2f, 2f, 2f },
                         tips: new[] { new ResourceAmount(ResourceType.Scrap, 3), new ResourceAmount(ResourceType.Parts, 1) }),

                Customer("Soldier", 1, 2, coin: 18, patience: 3f, weights: new[] { 0.5f, 1.5f, 2f, 2f },
                         tips: new[] { new ResourceAmount(ResourceType.Ammo, 2) }, recruit: 0.15f),

                Customer("Regular", 1, 1, coin: 16, patience: 14f, weights: new[] { 0.5f, 2f, 3f, 3f },
                         satisfied: 1.5f, regular: true),

                Customer("Envoy", 2, 3, coin: 45, patience: 2f, weights: new[] { 0f, 0f, 2f, 3f },
                         satisfied: 4f, failed: 5f, rareDrop: 0.3f),

                Customer("Wounded", 1, 1, coin: 0, patience: 8f, weights: new[] { 1f, 1f, 1f, 1f },
                         satisfied: 5f, wounded: true, recruit: 0.35f,
                         treatment: new[] { new ResourceAmount(ResourceType.Medicine, 1) }),
            };
        }

        static CustomerArchetypeDefinition Customer(
            string name, int minSize, int maxSize, int coin, float patience, float[] weights,
            float satisfied = 1f, float failed = 2f, bool regular = false, float recruit = 0f,
            float rareDrop = 0f, bool wounded = false,
            ResourceAmount[] tips = null, ResourceAmount[] treatment = null)
        {
            var asset = CreateOrLoad<CustomerArchetypeDefinition>($"{Root}/Customers/Customer_{name}.asset");

            asset.displayName = name;
            asset.minPartySize = minSize;
            asset.maxPartySize = maxSize;
            asset.baseCoinPayment = coin;
            asset.patienceClockMinutes = patience;
            asset.weightByReputationTier = weights;
            asset.reputationOnSatisfied = satisfied;
            asset.reputationOnFailed = failed;
            asset.becomesRegular = regular;
            asset.recruitChance = recruit;
            asset.rareDropChance = rareDrop;
            asset.isWounded = wounded;
            asset.tips = tips ?? new ResourceAmount[0];
            asset.treatmentCost = treatment ?? new ResourceAmount[0];
            asset.heatMultiplier = 1f;
            asset.poolPrewarm = 8;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Recipes (GDD 8) --------------------------------------------------
        //
        // The menu is a risk decision: grilled and smoked dishes are worth more and generate
        // double Heat, so a player running a salad bar is safe and poor.

        static RecipeDefinition[] GenerateRecipes()
        {
            return new[]
            {
                // Safe and poor: no Meat, single Heat, available from day one.
                Recipe("GardenSalad", coin: 10, heat: 1f, unlocked: true, ingredients: new[]
                {
                    new ResourceAmount(ResourceType.Veg, 2),
                }),

                Recipe("Flatbread", coin: 14, heat: 1f, unlocked: true, ingredients: new[]
                {
                    new ResourceAmount(ResourceType.Grain, 2),
                }),

                Recipe("VegStew", coin: 18, heat: 1f, unlocked: true, ingredients: new[]
                {
                    new ResourceAmount(ResourceType.Veg, 2),
                    new ResourceAmount(ResourceType.Grain, 1),
                }),

                // From here the menu starts costing Heat. Triple the money of a salad, and
                // double the Heat per dish - this is where the menu becomes a risk decision.
                Recipe("GrilledSkewers", coin: 30, heat: 2f, unlocked: false, rep: 15, ingredients: new[]
                {
                    new ResourceAmount(ResourceType.Meat, 2),
                    new ResourceAmount(ResourceType.Spice, 1),
                }),

                Recipe("SmokedBrisket", coin: 48, heat: 2f, unlocked: false, rep: 40, ingredients: new[]
                {
                    new ResourceAmount(ResourceType.Meat, 3),
                    new ResourceAmount(ResourceType.Spice, 2),
                }),

                Recipe("FeastPlatter", coin: 80, heat: 2f, unlocked: false, rep: 70, ingredients: new[]
                {
                    new ResourceAmount(ResourceType.Meat, 3),
                    new ResourceAmount(ResourceType.Luxury, 2),
                    new ResourceAmount(ResourceType.Spice, 1),
                }),
            };
        }

        /// <summary>
        /// Ingredients are an explicit array rather than a params tail.
        ///
        /// With params last and an optional int before it, a positional ResourceAmount binds
        /// to that int instead of the array - a trap that costs a confusing compile error
        /// every time someone adds a recipe without naming every argument.
        /// </summary>
        static RecipeDefinition Recipe(string name, int coin, float heat, bool unlocked,
                                       ResourceAmount[] ingredients, int rep = 0)
        {
            var asset = CreateOrLoad<RecipeDefinition>($"{Root}/Recipes/Recipe_{name}.asset");

            asset.displayName = name;
            asset.coinValue = coin;
            asset.heatMultiplier = heat;
            asset.unlockedFromStart = unlocked;
            asset.requiredReputation = rep;
            asset.ingredients = ingredients;
            asset.cookSeconds = 10f + coin * 0.2f;
            asset.reputationWeight = coin / 20f;
            asset.allowsSignature = true;
            asset.signatureMultiplier = 2f;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Crisis Bar (GDD 13.3) --------------------------------------------

        static CrisisAbilityDefinition[] GenerateCrisisAbilities()
        {
            return new[]
            {
                Crisis(CrisisVerb.Patch, 15f, 0.4f, "Repair a wall or barricade mid-fight."),
                Crisis(CrisisVerb.Reload, 20f, 1f, "Refill a turret or reset a trap."),
                Crisis(CrisisVerb.Bait, 30f, 1f, "Stew pot. Enemies in radius redirect for 8 seconds.",
                       radius: 3f, duration: 8f),
                Crisis(CrisisVerb.Reroute, 25f, 1f, "Shift generator output between kitchen, lights and turrets.",
                       tag: "Generator"),
                Crisis(CrisisVerb.Evacuate, 12f, 1f, "Move a panicking customer to the cellar."),
                Crisis(CrisisVerb.Rally, 18f, 1f, "Reposition a Survivor to a defense post."),
                Crisis(CrisisVerb.Douse, 20f, 1f, "Extinguish fire and wet a tile.", tag: "WaterTank"),
            };
        }

        static CrisisAbilityDefinition Crisis(CrisisVerb verb, float cooldown, float magnitude,
                                              string description, float radius = 0f,
                                              float duration = 0f, string tag = null)
        {
            var asset = CreateOrLoad<CrisisAbilityDefinition>($"{Root}/Crisis/Crisis_{verb}.asset");

            asset.verb = verb;
            asset.displayName = verb.ToString();
            asset.description = description;
            asset.cooldownSeconds = cooldown;
            asset.magnitude = magnitude;
            asset.radiusTiles = radius;
            asset.durationSeconds = duration;
            asset.requiredBuildableTag = tag;
            asset.unlockedFromStart = string.IsNullOrEmpty(tag);

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Plot tiers (GDD 6.2) ---------------------------------------------
        //
        // Expansion is fixed east and south, away from the highway, so the north wall stays
        // the front of house across all five tiers.

        static PlotTierDefinition[] GeneratePlotTiers()
        {
            return new[]
            {
                Tier(1, "The Shack", 8, 8, 0, "Start"),
                Tier(2, "The Lot", 10, 10, 20, "Clear the rubble east"),
                Tier(3, "The Corner", 12, 12, 40, "Reclaim the storefront"),
                Tier(4, "The Block", 14, 12, 60, "Wall in the alley"),
                Tier(5, "The Compound", 16, 14, 80, "Take the parking lot"),
            };
        }

        static PlotTierDefinition Tier(int tier, string name, int w, int h, int rep, string narrative)
        {
            var asset = CreateOrLoad<PlotTierDefinition>($"{Root}/PlotTiers/Tier_{tier}_{name.Replace(" ", "")}.asset");

            asset.tier = tier;
            asset.displayName = name;
            asset.gridWidth = w;
            asset.gridHeight = h;
            asset.requiredReputation = rep;
            asset.unlockNarrative = narrative;
            asset.expansionOffset = Vector2Int.zero;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Survivors (GDD 17) ------------------------------------------------

        static SurvivorRoleDefinition[] GenerateRoles()
        {
            return new[]
            {
                Role(SurvivorRole.Cook, station: 1.35f),
                Role(SurvivorRole.Server, service: 1.4f),
                Role(SurvivorRole.Builder, build: 1.3f, repairsAtNight: true),
                Role(SurvivorRole.Guard, guardDps: 14f, wage: 7),
                Role(SurvivorRole.Scout, dispatchRisk: 0.25f),
                Role(SurvivorRole.Medic, injuryRecovery: 0.5f, wage: 6),
            };
        }

        static SurvivorRoleDefinition Role(SurvivorRole role, float station = 1f, float service = 1f,
                                           float build = 1f, bool repairsAtNight = false,
                                           float guardDps = 0f, float dispatchRisk = 0f,
                                           float injuryRecovery = 1f, int wage = 5)
        {
            var asset = CreateOrLoad<SurvivorRoleDefinition>($"{Root}/Roles/Role_{role}.asset");

            asset.role = role;
            asset.displayName = role.ToString();
            asset.stationSpeedMultiplier = station;
            asset.serviceThroughputMultiplier = service;
            asset.buildSpeedMultiplier = build;
            asset.repairsDuringNight = repairsAtNight;
            asset.guardDps = guardDps;
            asset.dispatchRiskReduction = dispatchRisk;
            asset.injuryRecoveryMultiplier = injuryRecovery;
            asset.dailyWage = wage;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static SurvivorTraitDefinition[] GenerateTraits()
        {
            return new[]
            {
                Trait("Nervous", "Quick hands, no nerve.", effectiveness: 1.2f, flees: true),
                Trait("ExSoldier", "Steady under the lights. Eats like two people.", wage: 2f),
                Trait("Regular", "Was a customer first. Still greets everyone by name.", rep: 0.5f),
                Trait("LightSleeper", "Takes a while to get going.", firstTaskDelay: 5f),
                Trait("Superstitious", "Will not work when the radio sounds like that.", refusesNamed: true),
            };
        }

        static SurvivorTraitDefinition Trait(string name, string description, float effectiveness = 1f,
                                             bool flees = false, float wage = 1f, float rep = 0f,
                                             float firstTaskDelay = 0f, bool refusesNamed = false)
        {
            var asset = CreateOrLoad<SurvivorTraitDefinition>($"{Root}/Traits/Trait_{name}.asset");

            asset.displayName = name;
            asset.description = description;
            asset.roleEffectivenessMultiplier = effectiveness;
            asset.fleesDuringSiege = flees;
            asset.wageMultiplier = wage;
            asset.passiveReputationPerDay = rep;
            asset.firstTaskDelayClockMinutes = firstTaskDelay;
            asset.refusesNamedNights = refusesNamed;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Waves (GDD 13) ----------------------------------------------------

        static WaveManifest[] GenerateWaveManifests(EnemyDefinition[] enemies)
        {
            EnemyDefinition Find(string name)
            {
                for (int i = 0; i < enemies.Length; i++)
                    if (enemies[i].displayName == name) return enemies[i];
                return null;
            }

            var manifests = new List<WaveManifest>
            {
                Manifest("Tier0_FirstNights", 0, 1, 1, 120f,
                         Entry(Find("Shambler"), 10f, 2)),

                Manifest("Tier1_Shamble", 1, 2, 1, 140f,
                         Entry(Find("Shambler"), 8f), Entry(Find("Runner"), 3f)),

                Manifest("Tier2_Pressure", 2, 3, 8, 160f,
                         Entry(Find("Shambler"), 6f), Entry(Find("Runner"), 4f),
                         Entry(Find("Brute"), 2f, max: 3)),

                Manifest("Tier3_Mixed", 3, 4, 15, 180f,
                         Entry(Find("Shambler"), 4f), Entry(Find("Runner"), 4f),
                         Entry(Find("Brute"), 3f), Entry(Find("Flanker"), 3f),
                         Entry(Find("Burner"), 2f)),

                Manifest("Tier4_LateCampaign", 4, 6, 25, 200f,
                         Entry(Find("Brute"), 4f), Entry(Find("Flanker"), 4f),
                         Entry(Find("Burner"), 3f), Entry(Find("Glutton"), 3f),
                         Entry(Find("Raider"), 3f), Entry(Find("Screecher"), 1f, max: 2)),
            };

            return manifests.ToArray();
        }

        static WaveManifest.Entry Entry(EnemyDefinition enemy, float weight, int guaranteed = 0, int max = 0) =>
            new WaveManifest.Entry
            {
                enemy = enemy,
                weight = weight,
                guaranteedMinimum = guaranteed,
                maximum = max,
            };

        static WaveManifest Manifest(string name, int minTier, int maxTier, int earliestDay,
                                     float duration, params WaveManifest.Entry[] entries)
        {
            var asset = CreateOrLoad<WaveManifest>($"{Root}/Waves/Wave_{name}.asset");

            asset.minTier = minTier;
            asset.maxTier = maxTier;
            asset.earliestDay = earliestDay;
            asset.nightDurationSeconds = duration;
            asset.entries = entries;
            asset.approachLanes = Mathf.Clamp(1 + minTier, 1, 4);
            asset.budgetPerHeat = 0.35f;
            asset.minimumBudget = 3f;

            // Weighted toward the back half of the night, so the player can be nearly
            // finished and then tested. That is where 15.7's near-miss lives.
            asset.spawnRateOverNight = new AnimationCurve(
                new Keyframe(0f, 0.7f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0.85f));

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Named Nights (GDD 16.2) -------------------------------------------

        static NamedNightDefinition[] GenerateNamedNights(WaveManifest[] manifests)
        {
            WaveManifest ForTier(int tier)
            {
                for (int i = 0; i < manifests.Length; i++)
                    if (manifests[i].minTier == tier) return manifests[i];
                return manifests.Length > 0 ? manifests[manifests.Length - 1] : null;
            }

            return new[]
            {
                Night(7, "First Frost", ForTier(1),
                      "Cold coming in off the highway tonight. Keep something burning.",
                      requiresGenerator: true),

                Night(12, "The Convoy", ForTier(2),
                      "Convoy on the road, running light. They will want somewhere to stop.",
                      protectTarget: true),

                Night(18, "Blackout", ForTier(2),
                      "Someone has been at the lines. No power south of the bridge tonight.",
                      disablesPower: true),

                Night(25, "The Marauders", ForTier(3),
                      "They sent word ahead. That is either a courtesy or a price.",
                      bribe: true),

                Night(33, "Rain of Ash", ForTier(3),
                      "Ash on the wind all afternoon. Anything dry is going to take.",
                      visibility: 0.5f, woodFireRisk: true),

                Night(41, "The Long Night", ForTier(4),
                      "Sun is going down early and it is not coming back up in a hurry.",
                      doubleLength: true),

                Night(50, "The Feast", ForTier(4),
                      "Word got out. Everyone still alive on this road is coming tonight."),
            };
        }

        static NamedNightDefinition Night(int day, string title, WaveManifest wave, string broadcast,
                                          bool requiresGenerator = false, bool disablesPower = false,
                                          float visibility = 1f, bool woodFireRisk = false,
                                          bool doubleLength = false, bool bribe = false,
                                          bool protectTarget = false)
        {
            var asset = CreateOrLoad<NamedNightDefinition>(
                $"{Root}/NamedNights/Night_{day:00}_{title.Replace(" ", "")}.asset");

            asset.day = day;
            asset.title = title;
            asset.broadcast = broadcast;
            asset.waveOverride = wave;
            asset.requiresGenerator = requiresGenerator;
            asset.disablesPoweredDefenses = disablesPower;
            asset.visibilityMultiplier = visibility;
            asset.woodFireRisk = woodFireRisk;
            asset.doubleLength = doubleLength;
            asset.offersBribe = bribe;
            asset.hasProtectTarget = protectTarget;
            asset.reputationReward = 5;
            asset.completionReward = new[] { new ResourceAmount(ResourceType.Parts, 4) };

            if (bribe)
                asset.bribeCost = new[] { new ResourceAmount(ResourceType.Coin, 250) };

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // --- Dispatch (GDD 12) --------------------------------------------------

        static DispatchDestinationDefinition[] GenerateDispatch()
        {
            var pharmacy = CreateOrLoad<DispatchDestinationDefinition>(
                $"{Root}/Dispatch/Dispatch_FloodedPharmacy.asset");

            pharmacy.title = "The Flooded Pharmacy";
            pharmacy.teaser = "Half the shelves are underwater but nobody has been inside since the rain.";
            pharmacy.risk = DispatchRisk.Moderate;
            pharmacy.durationDays = 2;
            pharmacy.earliestDay = 4;
            pharmacy.likelyRewards = new[]
            {
                new ResourceAmount(ResourceType.Parts, 6),
                new ResourceAmount(ResourceType.Medicine, 3),
            };
            pharmacy.possibleRareRewards = new[] { "Chem Blueprint" };
            pharmacy.outcomes = new[]
            {
                Outcome(DispatchOutcomeTier.Success, 6f, "The dispensary was untouched. Most of it came home dry.",
                        new ResourceAmount(ResourceType.Parts, 6), new ResourceAmount(ResourceType.Medicine, 3)),
                Outcome(DispatchOutcomeTier.Partial, 3f, "Waist deep the whole way. Half of what was there is ruined.",
                        new ResourceAmount(ResourceType.Medicine, 1)),
                Outcome(DispatchOutcomeTier.Setback, 1f, "Something gave way underfoot. Nothing broken, but nothing carried either.",
                        extraDays: 2),
            };

            // The choice vignette from 12.1, kept close to the GDD's own wording. This is the
            // shape all fifty destinations should take - the decision is the content.
            pharmacy.choices = new[]
            {
                new DispatchDestinationDefinition.Choice
                {
                    setup = "Kira found the dispensary intact - and a family living in it. Four of them, hungry.",
                    optionALabel = "Take the supplies",
                    optionA = Outcome(DispatchOutcomeTier.Success, 1f,
                                      "She took what was on the shelves. Nobody stopped her. Nobody said anything either.",
                                      reputation: -8,
                                      haul: new[]
                                      {
                                          new ResourceAmount(ResourceType.Parts, 6),
                                          new ResourceAmount(ResourceType.Medicine, 4),
                                      }),
                    optionBLabel = "Leave them the medicine",
                    optionB = Outcome(DispatchOutcomeTier.Partial, 1f,
                                      "She left the medicine and took the rest. They said they would find the restaurant.",
                                      reputation: 12,
                                      haul: new[] { new ResourceAmount(ResourceType.Parts, 3) }),
                },
            };

            EditorUtility.SetDirty(pharmacy);

            var depot = CreateOrLoad<DispatchDestinationDefinition>($"{Root}/Dispatch/Dispatch_RoadsideDepot.asset");
            depot.title = "The Roadside Depot";
            depot.teaser = "Picked over twice already. There is always something in the back.";
            depot.risk = DispatchRisk.Low;
            depot.durationDays = 1;
            depot.earliestDay = 4;
            depot.likelyRewards = new[] { new ResourceAmount(ResourceType.Scrap, 10) };
            depot.outcomes = new[]
            {
                Outcome(DispatchOutcomeTier.Success, 7f, "Nothing dramatic. A full bag of scrap and a quiet walk back.",
                        new ResourceAmount(ResourceType.Scrap, 10)),
                Outcome(DispatchOutcomeTier.Windfall, 1f, "The office safe was still shut. It is not any more.",
                        new ResourceAmount(ResourceType.Scrap, 14), new ResourceAmount(ResourceType.Parts, 4)),
                Outcome(DispatchOutcomeTier.Partial, 2f, "Someone got there first. Scraps of scraps.",
                        new ResourceAmount(ResourceType.Scrap, 4)),
            };
            EditorUtility.SetDirty(depot);

            return new[] { pharmacy, depot };
        }

        static DispatchDestinationDefinition.Outcome Outcome(
            DispatchOutcomeTier tier, float weight, string narrative,
            params ResourceAmount[] haul) =>
            Outcome(tier, weight, narrative, 0, 0, haul);

        static DispatchDestinationDefinition.Outcome Outcome(
            DispatchOutcomeTier tier, float weight, string narrative, int extraDays) =>
            Outcome(tier, weight, narrative, 0, extraDays, null);

        static DispatchDestinationDefinition.Outcome Outcome(
            DispatchOutcomeTier tier, float weight, string narrative, int reputation,
            ResourceAmount[] haul) =>
            Outcome(tier, weight, narrative, reputation, 0, haul);

        static DispatchDestinationDefinition.Outcome Outcome(
            DispatchOutcomeTier tier, float weight, string narrative,
            int reputation, int extraDays, ResourceAmount[] haul) =>
            new DispatchDestinationDefinition.Outcome
            {
                tier = tier,
                weight = weight,
                narrative = narrative,
                reputationDelta = reputation,
                extraDaysAway = extraDays,
                haul = haul ?? new ResourceAmount[0],
            };

        // --- Asset plumbing -----------------------------------------------------

        static void EnsureFolders()
        {
            string[] folders =
            {
                $"{Root}/Enemies", $"{Root}/Walls", $"{Root}/Customers", $"{Root}/Recipes",
                $"{Root}/Crisis", $"{Root}/PlotTiers", $"{Root}/Roles", $"{Root}/Traits",
                $"{Root}/Waves", $"{Root}/NamedNights", $"{Root}/Dispatch",
            };

            foreach (string folder in folders) EnsureFolder(folder);

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Creates a folder and every missing ancestor, through the AssetDatabase.
        ///
        /// Recursive rather than a flat loop because "Assets/Game Assets/Content/LastOrders"
        /// has two levels that may not exist, and AssetDatabase.CreateFolder silently fails
        /// when its parent is not itself a known asset folder. Creating the directory on disk
        /// instead would leave the AssetDatabase unaware of it until a refresh, which is the
        /// same bug one step later.
        /// </summary>
        internal static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Loads the asset if it exists, creates it otherwise. Keeps the generator
        /// idempotent so it can be re-run after every GDD revision without duplicating.</summary>
        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
