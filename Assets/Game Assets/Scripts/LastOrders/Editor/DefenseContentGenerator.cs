using LastOrders.Data;
using UnityEditor;
using UnityEngine;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The defenses' starting content: the weapons, and the guard tower with its levels.
    ///
    /// Like the expansion content, these are values a designer is expected to tune by hand, so
    /// an asset is filled in only on the run that creates it. Re-running never resets a tuned
    /// damage figure or price.
    ///
    /// Level 1 is the design's own numbers: a pistol, 16 damage every 1.5 seconds within
    /// 6 m, and 60 Trade Tokens for each tower after the first. The spot range, the premium
    /// price and all of Level 2 - the rifle - are placeholders the design has not given
    /// numbers for yet, marked as such below.
    /// </summary>
    public static class DefenseContentGenerator
    {
        public const string Folder = "Assets/Game Assets/Content/LastOrders/Defense";

        public const string PistolPath = Folder + "/Weapon_Pistol.asset";
        public const string RiflePath = Folder + "/Weapon_Rifle.asset";
        public const string GuardTowerPath = Folder + "/Defense_GuardTower.asset";

        public const string ScrapGunPath = Folder + "/Weapon_ScrapGun.asset";
        public const string TwinScrapGunPath = Folder + "/Weapon_TwinScrapGun.asset";
        public const string ScrapTurretPath = Folder + "/Defense_ScrapTurret.asset";
        public const string ScrapTurretMotionPath = Folder + "/DefenseMotion_ScrapTurret.asset";

        public const string ToastPath = Folder + "/Weapon_Toast.asset";
        public const string DoubleToastPath = Folder + "/Weapon_DoubleToast.asset";
        public const string ToastapultPath = Folder + "/Defense_Toastapult.asset";
        public const string ToastapultMotionPath = Folder + "/DefenseMotion_Toastapult.asset";

        public const string CornShotPath = Folder + "/Weapon_CornShot.asset";
        public const string CornBlastPath = Folder + "/Weapon_CornBlast.asset";
        public const string CornShooterPath = Folder + "/Defense_CornShooter.asset";
        public const string CornShooterMotionPath = Folder + "/DefenseMotion_CornShooter.asset";

        public const string TomatoShotPath = Folder + "/Weapon_TomatoShot.asset";
        public const string TomatoVolleyPath = Folder + "/Weapon_TomatoVolley.asset";
        public const string TomatoShooterPath = Folder + "/Defense_TomatoShooter.asset";
        public const string TomatoShooterMotionPath = Folder + "/DefenseMotion_TomatoShooter.asset";

        public const string CabbageLobPath = Folder + "/Weapon_CabbageLob.asset";
        public const string CabbageHeavyPath = Folder + "/Weapon_CabbageHeavy.asset";
        public const string CabbageShooterPath = Folder + "/Defense_CabbageShooter.asset";
        public const string CabbageShooterMotionPath = Folder + "/DefenseMotion_CabbageShooter.asset";

        public const string PlateTossPath = Folder + "/Weapon_PlateToss.asset";
        public const string PlateStackPath = Folder + "/Weapon_PlateStack.asset";
        public const string PlateShooterPath = Folder + "/Defense_PlateShooter.asset";
        public const string PlateShooterMotionPath = Folder + "/DefenseMotion_PlateShooter.asset";

        /// <summary>The ground defenses the strip outside the walls takes, one asset each.</summary>
        public const string StripFolder = Folder + "/Strip";

        public const string BarricadePath = StripFolder + "/GroundDefense_Barricade.asset";
        public const string SpikePlatePath = StripFolder + "/GroundDefense_SpikePlate.asset";
        public const string RazorWirePath = StripFolder + "/GroundDefense_RazorWire.asset";

        public static DefenseDefinition Generate()
        {
            ContentGenerator.EnsureFolder(Folder);

            // 8 m: enough for the gate tower to reach a zombie at the middle of the 10 m gate.
            WeaponDefinition pistol = Weapon(PistolPath, "Pistol", damage: 16f, interval: 1.5f, range: 8f);

            // Placeholder: the design says only that the rifle improves damage and range.
            WeaponDefinition rifle = Weapon(RiflePath, "Rifle", damage: 24f, interval: 1.5f, range: 9f);

            DefenseDefinition tower = GuardTower(pistol, rifle);

            AssetDatabase.SaveAssets();
            return tower;
        }

        public static DefenseDefinition LoadGuardTower() => AssetDatabase.LoadAssetAtPath<DefenseDefinition>(GuardTowerPath);

        /// <summary>
        /// The scrap turret the restaurant starts with: its two guns, its levels and how it moves.
        ///
        /// Placeholder numbers throughout - the design names an automated turret and nothing
        /// more. Pitched to sit just under a crewed tower: Level 1 does 10 damage a second to the
        /// pistol's 10.7, a Shambler (30) falls to six shots over 2.5 seconds, and it reaches 7 m,
        /// short of the pistol's 8. It needs nobody posted, which is what it is for. It spends no Ammo,
        /// so the defense the restaurant opens with never falls silent; set the guns'
        /// ammoPerShot to make it a turret that has to be kept fed.
        /// </summary>
        public static DefenseDefinition GenerateScrapTurret()
        {
            ContentGenerator.EnsureFolder(Folder);

            WeaponDefinition gun = Weapon(ScrapGunPath, "Scrap Gun", damage: 5f, interval: 0.5f, range: 7f);
            WeaponDefinition twin = Weapon(TwinScrapGunPath, "Twin Scrap Gun", damage: 6f, interval: 0.35f, range: 8.5f);

            DefenseDefinition turret = ScrapTurret(gun, twin);
            ScrapTurretMotion();

            AssetDatabase.SaveAssetIfDirty(gun);
            AssetDatabase.SaveAssetIfDirty(twin);
            AssetDatabase.SaveAssetIfDirty(turret);
            return turret;
        }

        public static DefenseMotionProfile LoadScrapTurretMotion() =>
            AssetDatabase.LoadAssetAtPath<DefenseMotionProfile>(ScrapTurretMotionPath);

        static DefenseDefinition ScrapTurret(WeaponDefinition gun, WeaponDefinition twin)
        {
            if (!CreateIfMissing(ScrapTurretPath, out DefenseDefinition asset)) return asset;

            asset.id = "scrap_turret";
            asset.displayName = "Scrap Turret";
            asset.description = "A gun on a turntable behind sandbags, bolted together from scrap. It " +
                                "watches the diner door on its own and shoots whatever comes close.";

            asset.requiresCrew = false;
            asset.crewRole = SurvivorRole.Guard;

            // Placeholder: cheaper than a tower's 60, since it fires a little softer.
            asset.buildPrice = new DefensePrice(15, new ResourceAmount(ResourceType.TradeToken, 45));

            asset.levels = new[]
            {
                // Sees a little under twice as far as it shoots: time to swing round before the first shot.
                new DefenseLevel { weapon = gun, spotRangeMetres = 11f },
                new DefenseLevel
                {
                    weapon = twin,
                    spotRangeMetres = 13f,
                    upgradePrice = new DefensePrice(25, new ResourceAmount(ResourceType.TradeToken, 70)),
                },
            };

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static DefenseMotionProfile ScrapTurretMotion()
        {
            if (!CreateIfMissing(ScrapTurretMotionPath, out DefenseMotionProfile asset)) return asset;

            asset.turnSpeed = 240f;
            asset.targetHeight = 0.9f;
            asset.idleScanDegrees = 40f;
            asset.idleScanSeconds = 7f;
            asset.flashSeconds = 0.05f;

            // Back and home in well under the twin gun's 0.35 s, so every shot is a separate kick.
            asset.recoilDistance = 0.09f;
            asset.recoilReturnSeconds = 0.12f;
            asset.casingsPerShot = 1;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        /// <summary>One line on what the Toastapult is, for its popup. Kept here so the asset can be brought up to date with it.</summary>
        public const string ToastapultDescription =
            "A toaster left on a hot plate too long, until it came alive. It glares at anything that " +
            "shambles too close and lobs flaming toast at it.";

        /// <summary>
        /// The Toastapult: a living toaster that lobs flaming toast. Its guns, its levels, how it moves.
        ///
        /// Placeholder numbers, pitched as the slow, heavy partner to the scrap turret: fewer, bigger
        /// shots that land a second after they are thrown. Level 1 is 12 a slice every 1.6 seconds
        /// (7.5 a second), so a Zombie (30) takes three slices and a Hot Dog Zombie (45) four; it
        /// reaches 8.5 m, further than the turret, because a lob goes over things. No crew, no Ammo.
        /// </summary>
        public static DefenseDefinition GenerateToastapult()
        {
            ContentGenerator.EnsureFolder(Folder);

            WeaponDefinition toast = Weapon(ToastPath, "Toast", damage: 12f, interval: 1.6f, range: 8.5f, projectileSpeed: 8f);
            WeaponDefinition doubleToast = Weapon(DoubleToastPath, "Double Toast", damage: 15f, interval: 1.15f, range: 9.5f, projectileSpeed: 9f);

            DefenseDefinition toaster = Toastapult(toast, doubleToast);
            ToastapultMotion();

            AssetDatabase.SaveAssetIfDirty(toast);
            AssetDatabase.SaveAssetIfDirty(doubleToast);
            AssetDatabase.SaveAssetIfDirty(toaster);
            return toaster;
        }

        public static DefenseMotionProfile LoadToastapultMotion() =>
            AssetDatabase.LoadAssetAtPath<DefenseMotionProfile>(ToastapultMotionPath);

        static DefenseDefinition Toastapult(WeaponDefinition toast, WeaponDefinition doubleToast)
        {
            if (!CreateIfMissing(ToastapultPath, out DefenseDefinition asset)) return asset;

            asset.id = "toastapult";
            asset.displayName = "Toastapult";
            asset.description = ToastapultDescription;

            asset.requiresCrew = false;
            asset.crewRole = SurvivorRole.Guard;

            // Placeholder, like the turret's.
            asset.buildPrice = new DefensePrice(15, new ResourceAmount(ResourceType.TradeToken, 40));

            asset.levels = new[]
            {
                new DefenseLevel { weapon = toast, spotRangeMetres = 12f },
                new DefenseLevel
                {
                    weapon = doubleToast,
                    spotRangeMetres = 13.5f,
                    upgradePrice = new DefensePrice(25, new ResourceAmount(ResourceType.TradeToken, 65)),
                },
            };

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static DefenseMotionProfile ToastapultMotion()
        {
            if (!CreateIfMissing(ToastapultMotionPath, out DefenseMotionProfile asset)) return asset;

            // Turns to look, sizes up the approach with its head on one side, then the other.
            asset.turnSpeed = 200f;
            asset.targetHeight = 0.9f;
            asset.idleScanDegrees = 25f;
            asset.idleScanSeconds = 5f;
            asset.flashSeconds = 0.05f;

            // Nothing kicks back and nothing is ejected: the pop out of the slots is its launch effect.
            asset.recoilDistance = 0f;
            asset.casingsPerShot = 0;

            asset.lobHeight = 2f;
            asset.projectileSpin = 600f;

            asset.breatheAmount = 0.035f;
            asset.breatheSeconds = 1.3f;
            asset.shotSquash = 0.22f;
            asset.shotSettleSeconds = 0.5f;
            asset.blinkSeconds = 3.2f;
            asset.swayDegrees = 8f;
            asset.reloadShowsAt = 0.45f;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        /// <summary>One line on what the Corn Shooter is, for its popup.</summary>
        public const string CornShooterDescription =
            "An old pitching machine re-geared for corn. Its engine spins two tyres, and a cob dropped " +
            "between them leaves the tube like a fastball.";

        /// <summary>
        /// The Corn Shooter: the wall's own gun, mounted on a bastion wherever one is built.
        ///
        /// Placeholder numbers: whole cobs, heavy and steady - 9 a cob every 0.9 seconds (10 a second),
        /// so a Zombie (30) takes four, thrown nose first at 14 m/s on a shallow arc. It reaches 9 m from
        /// the top of the wall - the strip and a good way past it - and sees 12. No crew, no Ammo: it
        /// is fed from the garden's leftovers.
        /// </summary>
        public static DefenseDefinition GenerateCornShooter()
        {
            ContentGenerator.EnsureFolder(Folder);

            WeaponDefinition shot = Weapon(CornShotPath, "Cob Shot", damage: 9f, interval: 0.9f, range: 9f, projectileSpeed: 14f);
            WeaponDefinition blast = Weapon(CornBlastPath, "Cob Barrage", damage: 11f, interval: 0.7f, range: 10f, projectileSpeed: 16f);

            DefenseDefinition shooter = CornShooter(shot, blast);
            CornShooterMotion();

            AssetDatabase.SaveAssetIfDirty(shot);
            AssetDatabase.SaveAssetIfDirty(blast);
            AssetDatabase.SaveAssetIfDirty(shooter);
            return shooter;
        }

        public static DefenseMotionProfile LoadCornShooterMotion() =>
            AssetDatabase.LoadAssetAtPath<DefenseMotionProfile>(CornShooterMotionPath);

        static DefenseDefinition CornShooter(WeaponDefinition shot, WeaponDefinition blast)
        {
            if (!CreateIfMissing(CornShooterPath, out DefenseDefinition asset)) return asset;

            asset.id = "corn_shooter";
            asset.displayName = "Corn Shooter";
            asset.description = CornShooterDescription;

            asset.requiresCrew = false;
            asset.crewRole = SurvivorRole.Guard;

            // Placeholder: a little under the turret's, since it hits softer.
            asset.buildPrice = new DefensePrice(15, new ResourceAmount(ResourceType.TradeToken, 40));

            asset.levels = new[]
            {
                new DefenseLevel { weapon = shot, spotRangeMetres = 12f },
                new DefenseLevel
                {
                    weapon = blast,
                    spotRangeMetres = 13.5f,
                    upgradePrice = new DefensePrice(25, new ResourceAmount(ResourceType.TradeToken, 60)),
                },
            };

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static DefenseMotionProfile CornShooterMotion()
        {
            if (!CreateIfMissing(CornShooterMotionPath, out DefenseMotionProfile asset)) return asset;

            ApplyCornShooterMotion(asset);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        // --- The kitchen-garden shooters, sold in the shop -----------------------------------------------

        /// <summary>One line on what the Tomato Shooter is, for its popup.</summary>
        public const string TomatoShooterDescription =
            "A pressure cooker turned blunderbuss. Steam builds in the pot and spits tomatoes out of the " +
            "bell by the dozen; whatever they burst on is left wading through the pulp.";

        /// <summary>
        /// The Tomato Shooter: fast and light, and every hit slows.
        ///
        /// Placeholder numbers, pitched as the quick one of the three: 4 a tomato every 0.45 seconds
        /// (8.9 a second), so a Zombie (30) takes eight - but every one leaves it at 60% speed for
        /// 1.5 seconds, which keeps it in reach of everything else for longer. 7.5 m, a flat quick lob.
        /// </summary>
        public static DefenseDefinition GenerateTomatoShooter()
        {
            ContentGenerator.EnsureFolder(Folder);

            WeaponDefinition shot = Weapon(TomatoShotPath, "Tomato Shot", damage: 4f, interval: 0.45f, range: 7.5f, projectileSpeed: 15f);
            WeaponDefinition volley = Weapon(TomatoVolleyPath, "Tomato Volley", damage: 5f, interval: 0.35f, range: 8.5f, projectileSpeed: 17f);
            Slowing(shot, 0.6f, 1.5f);
            Slowing(volley, 0.5f, 1.8f);

            DefenseDefinition shooter = Shooter(TomatoShooterPath, "tomato_shooter", "Tomato Shooter", TomatoShooterDescription,
                                                shot, volley, spot: 11f, spotUpgraded: 12.5f, build: 35, upgrade: 55);
            Motion(TomatoShooterMotionPath, ApplyTomatoShooterMotion);

            AssetDatabase.SaveAssetIfDirty(shot);
            AssetDatabase.SaveAssetIfDirty(volley);
            AssetDatabase.SaveAssetIfDirty(shooter);
            return shooter;
        }

        public static DefenseMotionProfile LoadTomatoShooterMotion() =>
            AssetDatabase.LoadAssetAtPath<DefenseMotionProfile>(TomatoShooterMotionPath);

        /// <summary>How the pressure cooker moves: it swings round quickly and kicks its bell back on every spit.</summary>
        public static void ApplyTomatoShooterMotion(DefenseMotionProfile asset)
        {
            asset.turnSpeed = 280f;
            asset.targetHeight = 0.8f;
            asset.idleScanDegrees = 35f;
            asset.idleScanSeconds = 6f;
            asset.flashSeconds = 0.05f;

            // A short sharp jolt, home well inside the volley's 0.35 s.
            asset.recoilDistance = 0.06f;
            asset.recoilReturnSeconds = 0.1f;
            asset.casingsPerShot = 0;

            // A low quick lob, the tomato tumbling.
            asset.lobHeight = 0.9f;
            asset.projectileSpin = 720f;
            asset.projectileSpinAxis = Vector3.right;
            asset.projectileFacesFlight = false;

            asset.reloadShowsAt = 0.5f;

            // Toon life: the pot swells and settles with the pressure, and squashes down on every spit.
            asset.breatheAmount = 0.03f;
            asset.breatheSeconds = 1.1f;
            asset.shotSquash = 0.16f;
            asset.shotSettleSeconds = 0.32f;
            asset.blinkSeconds = 0f;
            asset.swayDegrees = 0f;
        }

        /// <summary>One line on what the Cabbage Shooter is, for its popup.</summary>
        public const string CabbageShooterDescription =
            "A spring catapult on a garden cart, its bucket a kitchen colander. It winds itself down " +
            "and flings a whole cabbage high over the wall, and it bursts on everything round where it lands.";

        /// <summary>
        /// The Cabbage Shooter: slow, heavy, and it splashes.
        ///
        /// Placeholder numbers, pitched as the heavy one: 18 a cabbage every 2.2 seconds (8.2 a second
        /// to one target), but everything within 1.8 m of where it lands takes 60% of that too - a
        /// crowd at a gate takes far more than any single-target gun gives it. It reaches 9.5 m on a
        /// high slow lob.
        /// </summary>
        public static DefenseDefinition GenerateCabbageShooter()
        {
            ContentGenerator.EnsureFolder(Folder);

            WeaponDefinition lob = Weapon(CabbageLobPath, "Cabbage Lob", damage: 18f, interval: 2.2f, range: 9.5f, projectileSpeed: 7f);
            WeaponDefinition heavy = Weapon(CabbageHeavyPath, "Heavy Head", damage: 24f, interval: 1.9f, range: 10.5f, projectileSpeed: 8f);
            Splashing(lob, 1.8f, 0.6f);
            Splashing(heavy, 2.2f, 0.6f);

            DefenseDefinition shooter = Shooter(CabbageShooterPath, "cabbage_shooter", "Cabbage Shooter", CabbageShooterDescription,
                                                lob, heavy, spot: 13f, spotUpgraded: 14.5f, build: 50, upgrade: 75);
            Motion(CabbageShooterMotionPath, ApplyCabbageShooterMotion);

            AssetDatabase.SaveAssetIfDirty(lob);
            AssetDatabase.SaveAssetIfDirty(heavy);
            AssetDatabase.SaveAssetIfDirty(shooter);
            return shooter;
        }

        public static DefenseMotionProfile LoadCabbageShooterMotion() =>
            AssetDatabase.LoadAssetAtPath<DefenseMotionProfile>(CabbageShooterMotionPath);

        /// <summary>How the catapult moves: a heavy cart that turns slowly, and a high tumbling lob.</summary>
        public static void ApplyCabbageShooterMotion(DefenseMotionProfile asset)
        {
            asset.turnSpeed = 140f;
            asset.targetHeight = 0.7f;
            asset.idleScanDegrees = 25f;
            asset.idleScanSeconds = 9f;
            asset.flashSeconds = 0.05f;

            // The arm swings; nothing kicks back.
            asset.recoilDistance = 0f;
            asset.casingsPerShot = 0;

            asset.lobHeight = 3.2f;
            asset.projectileSpin = 260f;
            asset.projectileSpinAxis = Vector3.right;
            asset.projectileFacesFlight = false;

            // The arm is wound back down and the next cabbage dropped in its bucket three quarters of the way to the next throw.
            asset.reloadShowsAt = 0.75f;

            // The frame bucks down as the arm lets go, and settles.
            asset.breatheAmount = 0f;
            asset.shotSquash = 0.07f;
            asset.shotSettleSeconds = 0.55f;
            asset.blinkSeconds = 0f;
            asset.swayDegrees = 0f;
        }

        /// <summary>One line on what the Plate Shooter is, for its popup.</summary>
        public const string PlateShooterDescription =
            "The diner's plate dispenser on a bus cart, with a clay-pigeon arm bolted on top. It skims " +
            "plates flat and fast, and they shatter into everything standing next to what they hit.";

        /// <summary>
        /// The Plate Shooter: quick flat throws that shatter.
        ///
        /// Placeholder numbers, pitched between the other two: 7 a plate every 0.75 seconds (9.3 a
        /// second), shards hitting anything within 1.2 m for 40% of that. 8 m on an almost flat throw
        /// at 18 m/s, so it lands nearly as it is thrown.
        /// </summary>
        public static DefenseDefinition GeneratePlateShooter()
        {
            ContentGenerator.EnsureFolder(Folder);

            WeaponDefinition toss = Weapon(PlateTossPath, "Plate Toss", damage: 7f, interval: 0.75f, range: 8f, projectileSpeed: 18f);
            WeaponDefinition stack = Weapon(PlateStackPath, "Stack Toss", damage: 9f, interval: 0.6f, range: 9f, projectileSpeed: 20f);
            Splashing(toss, 1.2f, 0.4f);
            Splashing(stack, 1.4f, 0.4f);

            DefenseDefinition shooter = Shooter(PlateShooterPath, "plate_shooter", "Plate Shooter", PlateShooterDescription,
                                                toss, stack, spot: 11.5f, spotUpgraded: 13f, build: 45, upgrade: 70);
            Motion(PlateShooterMotionPath, ApplyPlateShooterMotion);

            AssetDatabase.SaveAssetIfDirty(toss);
            AssetDatabase.SaveAssetIfDirty(stack);
            AssetDatabase.SaveAssetIfDirty(shooter);
            return shooter;
        }

        public static DefenseMotionProfile LoadPlateShooterMotion() =>
            AssetDatabase.LoadAssetAtPath<DefenseMotionProfile>(PlateShooterMotionPath);

        /// <summary>How the plate flinger moves: a cart that turns briskly, plates skimmed flat and spinning.</summary>
        public static void ApplyPlateShooterMotion(DefenseMotionProfile asset)
        {
            asset.turnSpeed = 240f;
            asset.targetHeight = 0.9f;
            asset.idleScanDegrees = 40f;
            asset.idleScanSeconds = 7f;
            asset.flashSeconds = 0.05f;

            // The flinger arm kicks back on its spindle as it lets go.
            asset.recoilDistance = 0.04f;
            asset.recoilReturnSeconds = 0.12f;
            asset.casingsPerShot = 0;

            // Almost flat, spinning like a thrown frisbee.
            asset.lobHeight = 0.35f;
            asset.projectileSpin = 1440f;
            asset.projectileSpinAxis = Vector3.up;
            asset.projectileFacesFlight = false;

            asset.reloadShowsAt = 0.45f;

            // The motor hums - a quick shiver - and the top jumps as the arm lets go.
            asset.breatheAmount = 0.008f;
            asset.breatheSeconds = 0.22f;
            asset.shotSquash = 0.1f;
            asset.shotSettleSeconds = 0.3f;
            asset.blinkSeconds = 0f;
            asset.swayDegrees = 0f;
        }

        /// <summary>A crewless shooter with two levels, priced in Trade Tokens (and premium), filled in only when it is created.</summary>
        static DefenseDefinition Shooter(string path, string id, string name, string description, WeaponDefinition level1,
                                         WeaponDefinition level2, float spot, float spotUpgraded, int build, int upgrade)
        {
            if (!CreateIfMissing(path, out DefenseDefinition asset)) return asset;

            asset.id = id;
            asset.displayName = name;
            asset.description = description;
            asset.requiresCrew = false;
            asset.crewRole = SurvivorRole.Guard;

            // What a wall site would charge for it. Bought from the shop, it is paid in Coins instead.
            asset.buildPrice = new DefensePrice(15, new ResourceAmount(ResourceType.TradeToken, build));

            asset.levels = new[]
            {
                new DefenseLevel { weapon = level1, spotRangeMetres = spot },
                new DefenseLevel
                {
                    weapon = level2,
                    spotRangeMetres = spotUpgraded,
                    upgradePrice = new DefensePrice(25, new ResourceAmount(ResourceType.TradeToken, upgrade)),
                },
            };

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void Motion(string path, System.Action<DefenseMotionProfile> apply)
        {
            if (!CreateIfMissing(path, out DefenseMotionProfile asset)) return;

            apply(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        /// <summary>Gives a weapon its slow, only while it has none - the assets are tuned by hand afterwards.</summary>
        static void Slowing(WeaponDefinition weapon, float multiplier, float seconds)
        {
            if (weapon.Slows) return;

            weapon.slowMultiplier = multiplier;
            weapon.slowSeconds = seconds;
            EditorUtility.SetDirty(weapon);
        }

        /// <summary>Gives a weapon its splash, only while it has none.</summary>
        static void Splashing(WeaponDefinition weapon, float radius, float fraction)
        {
            if (weapon.splashRadius > 0f) return;

            weapon.splashRadius = radius;
            weapon.splashFraction = fraction;
            EditorUtility.SetDirty(weapon);
        }

        /// <summary>
        /// The three things the strip outside the walls can hold. Placeholder numbers, filled in only
        /// when an asset is created, pitched against a Zombie (30 health, 1 m/s, 6 a second on a wall):
        ///
        /// - Barricade: 120 health - twenty seconds of one Zombie hitting it, five of a Brute's.
        /// - Spike Plate: 8 to everything on it every 1.2 s and pins it for a moment; jams after 30 strikes.
        /// - Razor Wire: holds whatever wades in to 40% speed and cuts it for 2 a second; torn after 45
        ///   zombie-seconds in it.
        ///
        /// Priced in the stores' Wood and Scrap, which clearing land pays out. The models are set on
        /// them by <see cref="GroundDefenseModels"/>.
        /// </summary>
        public static GroundDefenseDefinition[] GenerateGroundDefenses()
        {
            ContentGenerator.EnsureFolder(StripFolder);

            GroundDefenseDefinition barricade = Ground(BarricadePath, out bool made);
            if (made)
            {
                barricade.id = "barricade";
                barricade.displayName = "Barricade";
                barricade.description = "Tin sheets nailed to posts and braced from behind. Anything that walks into it stops to batter it down.";
                barricade.kind = GroundDefenseKind.Barricade;
                barricade.buildCost = new[] { new ResourceAmount(ResourceType.Wood, 10), new ResourceAmount(ResourceType.Scrap, 4) };
                barricade.footprint = new Vector2(2.4f, 0.55f);
                barricade.health = 120f;
                barricade.damagedBelow = 0.5f;
            }

            GroundDefenseDefinition spikes = Ground(SpikePlatePath, out made);
            if (made)
            {
                spikes.id = "spike_plate";
                spikes.displayName = "Spike Plate";
                spikes.description = "A sprung bed of sharpened rebar under a steel plate. It punches up through anything that steps on it.";
                spikes.kind = GroundDefenseKind.Trap;
                spikes.buildCost = new[] { new ResourceAmount(ResourceType.Scrap, 12) };
                spikes.footprint = new Vector2(1.5f, 1.2f);
                spikes.health = 30f;
                spikes.strikeDamage = 8f;
                spikes.strikeSeconds = 1.2f;
                spikes.wearPerStrike = 1f;
                spikes.slowMultiplier = 0.25f;
                spikes.slowSeconds = 0.6f;
            }

            GroundDefenseDefinition wire = Ground(RazorWirePath, out made);
            if (made)
            {
                wire.id = "razor_wire";
                wire.displayName = "Razor Wire";
                wire.description = "A coil of razor wire staked out low across the strip. Whatever wades into it is held back and cut.";
                wire.kind = GroundDefenseKind.Snare;
                wire.buildCost = new[] { new ResourceAmount(ResourceType.Scrap, 6), new ResourceAmount(ResourceType.Wood, 4) };
                wire.footprint = new Vector2(2.4f, 1f);
                wire.health = 45f;
                wire.slowMultiplier = 0.4f;
                wire.slowSeconds = 0.6f;
                wire.damagePerSecond = 2f;
                wire.wearPerSecond = 1f;
            }

            foreach (GroundDefenseDefinition asset in new[] { barricade, spikes, wire })
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }

            return new[] { barricade, spikes, wire };
        }

        static GroundDefenseDefinition Ground(string path, out bool made)
        {
            made = CreateIfMissing(path, out GroundDefenseDefinition asset);
            return asset;
        }

        /// <summary>How the cob pitcher moves. Public so a profile made for the kernel-spraying sheller it replaced can be brought up to date.</summary>
        public static void ApplyCornShooterMotion(DefenseMotionProfile asset)
        {
            // Swings round steadily and sweeps the strip below it while it waits.
            asset.turnSpeed = 220f;
            asset.targetHeight = 0.9f;
            asset.idleScanDegrees = 45f;
            asset.idleScanSeconds = 7f;
            asset.flashSeconds = 0.05f;

            // A jolt of the tube on each throw; nothing thrown out of the side.
            asset.recoilDistance = 0.05f;
            asset.recoilReturnSeconds = 0.14f;
            asset.casingsPerShot = 0;

            // A cob flies nose first on a shallow arc, spiralling like a thrown ball.
            asset.lobHeight = 0.6f;
            asset.projectileSpin = 900f;
            asset.projectileSpinAxis = Vector3.forward;
            asset.projectileFacesFlight = true;

            // The next cob drops into the breech a little after half way to the next throw.
            asset.reloadShowsAt = 0.55f;
        }

        static WeaponDefinition Weapon(string path, string name, float damage, float interval, float range, float projectileSpeed = 0f)
        {
            if (!CreateIfMissing(path, out WeaponDefinition asset)) return asset;

            asset.displayName = name;
            asset.damagePerShot = damage;
            asset.secondsBetweenShots = interval;
            asset.rangeMetres = range;
            asset.ammoPerShot = 0;
            asset.projectileSpeed = projectileSpeed;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static DefenseDefinition GuardTower(WeaponDefinition pistol, WeaponDefinition rifle)
        {
            if (!CreateIfMissing(GuardTowerPath, out DefenseDefinition asset)) return asset;

            asset.id = "guard_tower";
            asset.displayName = "Guard Tower";
            asset.description = "A raised post inside the perimeter wall where one survivor watches " +
                                "the road and protects the restaurant at night.";

            asset.requiresCrew = true;
            asset.crewRole = SurvivorRole.Guard;

            // Placeholder premium price: the design says "or optional premium currency" and names no figure.
            asset.buildPrice = new DefensePrice(20, new ResourceAmount(ResourceType.TradeToken, 60));

            asset.levels = new[]
            {
                // Placeholder spot range. Far enough to see the road beyond the gate - the
                // early warning - and about twice what the pistol reaches.
                new DefenseLevel { weapon = pistol, spotRangeMetres = 14f },

                // Placeholder throughout: the rifle, how far it sees, and what the upgrade costs.
                new DefenseLevel
                {
                    weapon = rifle,
                    spotRangeMetres = 18f,
                    upgradePrice = new DefensePrice(30, new ResourceAmount(ResourceType.TradeToken, 90)),
                },
            };

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>True only when the asset was created on this call.</summary>
        static bool CreateIfMissing<T>(string path, out T asset) where T : ScriptableObject
        {
            asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return false;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return true;
        }
    }
}
