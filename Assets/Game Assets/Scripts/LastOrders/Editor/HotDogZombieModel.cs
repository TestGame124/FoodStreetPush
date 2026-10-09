using System.Collections.Generic;
using FoodStreet.Navigation;
using LastOrders.Data;
using LastOrders.Night;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using S = LastOrders.EditorTools.SoupKitchenModel;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// The Hot Dog Zombie: the mascot from the burger place up the road, still in the costume. A
    /// sausage in a bun from the knees to well over its head, a mustard zigzag down the front, its
    /// green face poking out of the hole in the sausage - one big googly eye, one droopy one, tongue
    /// out - a paper cap tipped on top, bubblegum-pink mascot gloves reaching out in front and
    /// pink clown shoes flapping underneath. It waddles, rolls onto each foot, flaps, and goes
    /// boing when it is hit (<see cref="MascotZombieBody"/>).
    ///
    /// Its own archetype, with its own model rather than one of the project's people under a tint:
    /// a little tougher than a Zombie for the padding, a little slower for the shoes, and it drops a
    /// sausage. Modelled in code in the game's smooth low-poly style, painted in its vertices on
    /// LastOrders/Stylized, in five meshes for the five things that move apart: the bun and
    /// everything that rolls with it, the sausage on top that wobbles, each arm, a leg (used twice).
    ///
    /// This builds the meshes, the prefab, the enemy and gait assets, and adds the archetype to the
    /// roster and the wave manifests so the night actually sends it. Content values are only filled
    /// in when an asset is created; re-running rebuilds the model and never resets tuning.
    /// </summary>
    public static class HotDogZombieModel
    {
        public const string PrefabPath = ZombieContentGenerator.Folder + "/Enemy_HotDogZombie.prefab";
        public const string EnemyPath = ContentFolder + "/Enemy_HotDog.asset";
        public const string GaitPath = ContentFolder + "/Gait_HotDog.asset";

        const string ContentFolder = "Assets/Game Assets/Content/LastOrders/Enemies";
        const string MeshFolder = ContentFolder + "/" + EnvKit.Style + "/HotDogZombie";
        const string BalancePath = "Assets/Game Assets/Content/LastOrders/GameBalance.asset";
        const string WavesFolder = "Assets/Game Assets/Content/LastOrders/Waves";

        /// <summary>Wave manifest file name, how often it picks a Hot Dog against its others, and how many at most (0: no cap).</summary>
        static readonly (string wave, float weight, int maximum)[] Waves =
        {
            ("Wave_Tier0_FirstNights", 3f, 2),
            ("Wave_Tier1_Shamble", 3f, 0),
            ("Wave_Tier2_Pressure", 3f, 0),
            ("Wave_Tier3_Mixed", 2f, 0),
            ("Wave_Tier4_LateCampaign", 2f, 0),
        };

        // --- Measurements (metres; it faces +Z, its right is +X) -----------------------------------

        const float HipY = 0.42f, HipX = 0.13f;
        const float ShoulderY = 1.18f, ShoulderX = 0.36f, ShoulderZ = 0.02f;

        /// <summary>Where the sausage pivots as it wobbles: down inside the bun.</summary>
        static readonly Vector3 TopPivot = new Vector3(0f, 0.95f, 0f);

        const float SausageRadius = 0.19f, SausageBottom = 0.5f, SausageTop = 1.95f;
        static readonly Vector3 FaceCentre = new Vector3(0f, 1.5f, 0.11f);
        static readonly Vector3 GooglyEye = new Vector3(0.065f, 1.565f, 0.2f);
        const float GooglyRadius = 0.068f;

        // --- The palette -------------------------------------------------------------------------

        static readonly Color Bun = B.Hex("B98A52");
        static readonly Color BunTop = B.Hex("A0733F");
        static readonly Color Crumb = B.Hex("D2B98A");
        static readonly Color Sausage = B.Hex("A9473A");
        static readonly Color SausageDark = B.Hex("7A3226");
        static readonly Color Mustard = B.Hex("C9A033");
        static readonly Color HoleRim = B.Hex("6E2A22");
        static readonly Color ZombieGreen = B.Hex("8E9F6A");
        static readonly Color ZombieShade = B.Hex("788A58");
        static readonly Color EyeWhite = B.Hex("D9D6CC");
        static readonly Color Pupil = B.Hex("1A1A1C");
        static readonly Color MouthDark = B.Hex("3A1F1C");
        static readonly Color Tongue = B.Hex("C46A76");
        static readonly Color Tooth = B.Hex("D3CCBA");
        static readonly Color Paper = B.Hex("D3CCBA");
        static readonly Color PaperRed = B.Hex("A33A32");

        /// <summary>Bubblegum pink, the archetype's colour code: no other archetype is in this hue family.</summary>
        static readonly Color Pink = B.Hex("C9637E");
        static readonly Color PinkLight = B.Hex("D97C94");
        static readonly Color Sole = B.Hex("D3CCBA");
        static readonly Color SockRed = B.Hex("A33A32");

        [MenuItem("Last Orders/Hospital & Zombies/Build Hot Dog Zombie (Model, Prefab, Waves)", priority = 62)]
        static void Run() => Debug.Log(Apply());

        /// <summary>Builds the model and prefab, makes the assets if missing, and puts the archetype in the roster and the waves. No dialogs.</summary>
        public static string Apply()
        {
            ContentGenerator.EnsureFolder(MeshFolder);
            ContentGenerator.EnsureFolder(ZombieContentGenerator.Folder);

            ZombieGaitProfile gait = Gait();

            Mesh bun = Save(Costume(), "HotDog_Bun");
            Mesh top = Save(Top(), "HotDog_Sausage");
            Mesh pupil = Save(GooglyPupil(), "HotDog_Pupil");
            Mesh armL = Save(Arm(-1f), "HotDog_Arm_L");
            Mesh armR = Save(Arm(1f), "HotDog_Arm_R");
            Mesh leg = Save(Leg(), "HotDog_Leg");

            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = created ? new GameObject("Enemy_HotDogZombie") : PrefabUtility.LoadPrefabContents(PrefabPath);
            GameObject prefab;

            try
            {
                Dress(root, gait, bun, top, pupil, armL, armR, leg);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (created) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }

            EnemyDefinition enemy = Enemy(prefab);
            string roster = AddToRoster(enemy);
            string waves = AddToWaves(enemy);

            int vertices = bun.vertexCount + top.vertexCount + pupil.vertexCount + armL.vertexCount + armR.vertexCount + leg.vertexCount * 2;
            return $"[Last Orders] Hot Dog Zombie {(created ? "built" : "rebuilt")}: {vertices} vertices, prefab at {PrefabPath}. {roster} {waves}";
        }

        // --- The prefab --------------------------------------------------------------------------

        static void Dress(GameObject root, ZombieGaitProfile gait, Mesh bun, Mesh top, Mesh pupil, Mesh armL, Mesh armR, Mesh leg)
        {
            // Steering first: the other two require it.
            if (root.GetComponent<SteeringAgent>() == null) root.AddComponent<SteeringAgent>();
            if (root.GetComponent<FlowFieldFollower>() == null) root.AddComponent<FlowFieldFollower>();
            if (root.GetComponent<EnemyAgent>() == null) root.AddComponent<EnemyAgent>();

            Transform model = Child(root.transform, "Model", Vector3.zero);
            B.Show(model.gameObject, bun);

            Transform sausage = Child(model, "Sausage", TopPivot);
            B.Show(sausage.gameObject, top);

            Transform googly = Child(sausage, "Googly", GooglyEye + Vector3.forward * (GooglyRadius * 0.86f) - TopPivot);
            B.Show(googly.gameObject, pupil);

            Transform armLeft = Child(model, "Arm_L", new Vector3(-ShoulderX, ShoulderY, ShoulderZ));
            B.Show(armLeft.gameObject, armL);
            Transform armRight = Child(model, "Arm_R", new Vector3(ShoulderX, ShoulderY, ShoulderZ));
            B.Show(armRight.gameObject, armR);

            Transform legLeft = Child(model, "Leg_L", new Vector3(-HipX, HipY, 0f));
            B.Show(legLeft.gameObject, leg);
            Transform legRight = Child(model, "Leg_R", new Vector3(HipX, HipY, 0f));
            B.Show(legRight.gameObject, leg);

            var body = root.GetComponent<MascotZombieBody>();
            if (body == null) body = root.AddComponent<MascotZombieBody>();
            body.Configure(gait, model, sausage, armLeft, armRight, legLeft, legRight, new[] { googly });
        }

        static Transform Child(Transform parent, string name, Vector3 at)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }

            t.localPosition = at;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        // --- The costume -----------------------------------------------------------------------------

        /// <summary>The bun: two halves either side, toasted on top, the soft crumb showing on the faces that hold the sausage.</summary>
        static B.Model Costume()
        {
            var m = new B.Model { Shade = (p, n) => Mathf.Lerp(0.82f, 1f, S.Smooth((p.y - 0.36f) / 0.4f)) };

            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                var centre = new Vector3(s * 0.21f, 1.02f, 0f);
                S.Ball(m, (p, n) =>
                {
                    // The cut face toward the sausage is crumb; the outside is crust, darker up top where it browned.
                    if (n.x * s < -0.55f) return Crumb;
                    return Color.Lerp(Bun, BunTop, S.Smooth((p.y - 1.25f) / 0.4f));
                }, centre, new Vector3(0.17f, 0.66f, 0.31f), Quaternion.identity, 22, 18);
            }

            return m;
        }

        /// <summary>The sausage and everything on it - the face in its hole, the eyes, the tongue, the cap - about the wobble pivot.</summary>
        static B.Model Top()
        {
            var m = new B.Model { Transform = Matrix4x4.Translate(-TopPivot) };

            SausageShape(m);
            Face(m);
            Cap(m);
            return m;
        }

        static void SausageShape(B.Model m)
        {
            // A capsule turned up the Y axis: round at both ends.
            var profile = new List<Vector2> { new Vector2(0f, SausageBottom) };
            const int cap = 6;
            for (int i = 1; i <= cap; i++)
            {
                float a = i / (float)cap * Mathf.PI * 0.5f;
                profile.Add(new Vector2(Mathf.Sin(a) * SausageRadius, SausageBottom + SausageRadius * (1f - Mathf.Cos(a))));
            }

            for (int i = 0; i <= cap; i++)
            {
                float a = i / (float)cap * Mathf.PI * 0.5f;
                profile.Add(new Vector2(Mathf.Cos(a) * SausageRadius, SausageTop - SausageRadius * (1f - Mathf.Sin(a))));
            }

            profile.Add(new Vector2(0f, SausageTop));

            // More rings up the sides, so the grill marks and the mustard come out crisp.
            var dense = new List<Vector2>();
            for (int i = 0; i < profile.Count - 1; i++)
            {
                dense.Add(profile[i]);
                float gap = profile[i + 1].y - profile[i].y;
                int extra = Mathf.FloorToInt(gap / 0.025f);
                for (int k = 1; k < extra; k++) dense.Add(Vector2.Lerp(profile[i], profile[i + 1], k / (float)extra));
            }

            dense.Add(profile[profile.Count - 1]);

            m.Lathe((p, n) =>
            {
                float a = Mathf.Atan2(p.z, p.x);

                // A zigzag of mustard down the front, below the face.
                if (p.z > 0.06f && p.y > 0.62f && p.y < 1.3f)
                {
                    float wave = Mathf.PingPong(p.y * 7f, 1f) * 2f - 1f;
                    if (Mathf.Abs(p.x - wave * 0.07f) < 0.026f) return Mustard;
                }

                // Diagonal grill marks round it.
                if (Mathf.Sin((p.y + a * 0.12f) * 26f) > 0.86f) return SausageDark;

                // A little darker at the ends, where it browned most.
                float end = Mathf.Min(p.y - SausageBottom, SausageTop - p.y);
                return Color.Lerp(SausageDark, Sausage, S.Smooth(end / 0.25f));
            }, Vector3.zero, dense, 28);
        }

        /// <summary>The face poking out of a hole in the sausage: one big googly eye (its pupil is its own, to rattle), one droopy one, tongue out.</summary>
        static void Face(B.Model m)
        {
            S.Ball(m, (p, n) => Color.Lerp(ZombieShade, ZombieGreen, S.Smooth((p.y - 1.36f) / 0.2f)), FaceCentre,
                   new Vector3(0.15f, 0.17f, 0.11f), Quaternion.identity, 18, 14);
            FaceHoleRim(m, FaceCentre.y, 0.15f, 0.172f, 0.026f);

            // The googly eye's white; the pupil rattles in front of it.
            S.Ball(m, (p, n) => EyeWhite, GooglyEye, Vector3.one * GooglyRadius, Quaternion.identity, 16, 12);
            S.Ball(m, (p, n) => B.Hex("5E5C55"), GooglyEye, new Vector3(GooglyRadius + 0.006f, GooglyRadius + 0.006f, 0.02f),
                   Quaternion.identity, 16, 6);

            // The droopy eye: small, looking down, its lid half shut.
            var droopy = new Vector3(-0.07f, 1.55f, 0.205f);
            S.Ball(m, (p, n) => EyeWhite, droopy, Vector3.one * 0.042f, Quaternion.identity, 12, 9);
            S.Ball(m, (p, n) => Pupil, droopy + new Vector3(0.004f, -0.012f, 0.034f), new Vector3(0.016f, 0.016f, 0.012f), Quaternion.identity, 8, 6);
            S.Ball(m, (p, n) => ZombieShade, droopy + new Vector3(0f, 0.017f, 0.004f), new Vector3(0.048f, 0.028f, 0.046f),
                   Quaternion.Euler(0f, 0f, -12f), 12, 8);

            // The mouth hanging open, one tooth, the tongue lolling out of the corner.
            var mouth = new Vector3(0.0f, 1.43f, 0.205f);
            S.Ball(m, (p, n) => MouthDark, mouth, new Vector3(0.058f, 0.032f, 0.022f), Quaternion.Euler(0f, 0f, -6f), 14, 8);
            m.Box(Tooth, mouth + new Vector3(-0.022f, 0.02f, 0.016f), new Vector3(0.022f, 0.026f, 0.012f), 0.005f);
            m.Box(Tongue, mouth + new Vector3(0.022f, -0.028f, 0.02f), new Vector3(0.042f, 0.06f, 0.02f), 0.012f, Quaternion.Euler(-20f, 0f, 14f), 2);
        }

        /// <summary>A paper soda-jerk cap, tipped over to one side on top of the sausage.</summary>
        static void Cap(B.Model m)
        {
            Quaternion tip = Quaternion.Euler(4f, 0f, 14f);
            var at = new Vector3(0.03f, SausageTop - 0.02f, 0f);

            var cap = new SmoothMesh(1);
            cap.RoundedBox(0, at + tip * new Vector3(0f, 0.045f, 0f), new Vector3(0.3f, 0.09f, 0.15f), 0.035f, tip, 3);
            m.Add(cap, (p, n) =>
            {
                float y = Vector3.Dot(p - at, tip * Vector3.up);
                return y > 0.03f && y < 0.055f ? PaperRed : Paper;
            });
        }

        /// <summary>
        /// The rim of the hole the face looks out of: a roll of round section, elliptical by its two
        /// radii, laid on the curve of the sausage so it sits on it all the way round rather than
        /// standing off it at the sides.
        /// </summary>
        static void FaceHoleRim(B.Model m, float centreY, float radiusX, float radiusY, float thickness)
        {
            m.Surface((p, n) => HoleRim, 32, 8, (u, v) =>
            {
                float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * radiusX;
                float y = centreY + Mathf.Sin(a) * radiusY;
                float z = Mathf.Sqrt(Mathf.Max(0f, SausageRadius * SausageRadius - x * x));

                // Out across the sausage's surface, and out of it.
                Vector3 normal = new Vector3(x, 0f, z) / SausageRadius;
                Vector3 outward = new Vector3(Mathf.Cos(a) * radiusY, Mathf.Sin(a) * radiusX, 0f).normalized;
                outward = (outward - normal * Vector3.Dot(outward, normal)).normalized;

                return new Vector3(x, y, z) + (outward * Mathf.Cos(b) + normal * Mathf.Sin(b)) * thickness;
            }, true);
        }

        /// <summary>The googly eye's pupil: a black disc a little proud of the white, about its middle.</summary>
        static B.Model GooglyPupil()
        {
            var m = new B.Model();
            S.Ball(m, (p, n) => Pupil, Vector3.zero, new Vector3(0.032f, 0.032f, 0.016f), Quaternion.identity, 14, 8);
            S.Ball(m, (p, n) => B.Hex("ECEAE2"), new Vector3(0.011f, 0.012f, 0.013f), Vector3.one * 0.008f, Quaternion.identity, 6, 4);
            return m;
        }

        /// <summary>
        /// A costume sleeve reaching forward from the shoulder, a band of green wrist, and a big pink
        /// mascot glove - four puffy fingers. About the shoulder; <paramref name="side"/> is -1 or 1.
        /// </summary>
        static B.Model Arm(float side)
        {
            var m = new B.Model();
            var elbow = new Vector3(side * 0.03f, -0.08f, 0.36f);
            var wrist = new Vector3(side * 0.03f, -0.09f, 0.43f);
            var hand = new Vector3(side * 0.03f, -0.1f, 0.51f);

            m.Tube((p, n) => Bun, Vector3.zero, elbow, 0.068f, 0.058f, 14);
            m.Tube(BunTop, elbow - Vector3.forward * 0.015f, elbow + Vector3.forward * 0.02f, 0.064f, 14);
            m.Tube(ZombieGreen, elbow, wrist, 0.04f, 12);

            S.Ball(m, (p, n) => Pink, hand, new Vector3(0.088f, 0.07f, 0.085f), Quaternion.identity, 16, 12);
            for (int f = 0; f < 4; f++)
            {
                float x = (f - 1.5f) * 0.038f;
                S.Ball(m, (p, n) => PinkLight, hand + new Vector3(x, -0.01f, 0.07f), new Vector3(0.024f, 0.028f, 0.03f), Quaternion.identity, 10, 7);
            }

            // The thumb, on the inside.
            S.Ball(m, (p, n) => PinkLight, hand + new Vector3(-side * 0.075f, 0.02f, 0.01f), new Vector3(0.026f, 0.03f, 0.03f),
                   Quaternion.identity, 10, 7);
            return m;
        }

        /// <summary>A green leg out of the bottom of the costume, a stripy sock, a big pink clown shoe. About the hip.</summary>
        static B.Model Leg()
        {
            var m = new B.Model();
            m.Tube((p, n) => ZombieGreen, Vector3.zero, new Vector3(0f, -0.3f, 0f), 0.065f, 0.055f, 12);

            for (int i = 0; i < 3; i++)
            {
                float y = -0.24f - i * 0.03f;
                m.Tube(i == 1 ? SockRed : Sole, new Vector3(0f, y, 0f), new Vector3(0f, y - 0.03f, 0f), 0.06f, 12);
            }

            // A clown shoe: long, round-toed, on a pale sole. The toe sticks well out in front.
            m.Box(Pink, new Vector3(0f, -0.355f, 0.07f), new Vector3(0.17f, 0.12f, 0.34f), 0.055f, 3);
            S.Ball(m, (p, n) => PinkLight, new Vector3(0f, -0.35f, 0.2f), new Vector3(0.09f, 0.07f, 0.09f), Quaternion.identity, 12, 8);
            m.Box(Sole, new Vector3(0f, -HipY + 0.017f, 0.075f), new Vector3(0.18f, 0.034f, 0.36f), 0.014f, 2);
            return m;
        }

        // --- Assets -------------------------------------------------------------------------------------

        static Mesh Save(B.Model model, string name) => model.Save($"{MeshFolder}/{name}.asset", name);

        static ZombieGaitProfile Gait()
        {
            var gait = AssetDatabase.LoadAssetAtPath<ZombieGaitProfile>(GaitPath);
            if (gait != null) return gait;

            gait = ScriptableObject.CreateInstance<ZombieGaitProfile>();

            // Short busy steps for the big shoes, a lot of roll, arms flapping like it is still waving people in.
            gait.stepMetres = 0.34f;
            gait.legSwingDegrees = 32f;
            gait.waddleDegrees = 11f;
            gait.hopHeight = 0.07f;
            gait.armFlapDegrees = 18f;
            gait.idleSwayDegrees = 4f;
            gait.idleSwaySeconds = 2.2f;
            gait.topWobble = 0.8f;
            gait.hurtSquash = 0.22f;
            gait.hurtSeconds = 0.55f;
            gait.hurtWobbleDegrees = 16f;
            gait.poundSeconds = 0.5f;
            gait.poundLiftDegrees = 75f;
            gait.googlyRange = 0.022f;
            gait.googlyStiffness = 70f;
            gait.googlyDamping = 5f;
            gait.googlyKick = 0.4f;

            AssetDatabase.CreateAsset(gait, GaitPath);
            AssetDatabase.SaveAssetIfDirty(gait);
            return gait;
        }

        /// <summary>The archetype. Filled in when created; afterwards only its prefab is kept pointing at this one.</summary>
        static EnemyDefinition Enemy(GameObject prefab)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyPath);
            bool created = enemy == null;

            if (created)
            {
                enemy = ScriptableObject.CreateInstance<EnemyDefinition>();

                enemy.displayName = "Hot Dog Zombie";
                enemy.description = "The mascot from the burger place up the road. Still in the costume. Still waving people in.";
                enemy.silhouetteColour = new Color(0.85f, 0.42f, 0.55f);
                enemy.tintBody = false;

                enemy.navigation = ThreatNavigation.Doors;

                // A little tougher than a Zombie (30) for the padding, a little slower (1.0) in the shoes.
                enemy.maxHealth = 45f;
                enemy.moveSpeed = 0.8f;
                enemy.structureDps = 5f;
                enemy.agentRadius = 0.4f;
                enemy.attackRange = 0.65f;

                enemy.poolPrewarm = 4;
                enemy.threatCost = 1.5f;
                enemy.earliestDay = 1;

                // It drops a sausage.
                enemy.drops = new[] { new ResourceAmount(ResourceType.Meat, 1) };

                AssetDatabase.CreateAsset(enemy, EnemyPath);
            }

            if (enemy.prefab != prefab)
            {
                enemy.prefab = prefab;
                EditorUtility.SetDirty(enemy);
            }

            AssetDatabase.SaveAssetIfDirty(enemy);
            return enemy;
        }

        static string AddToRoster(EnemyDefinition enemy)
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalancePath);
            if (balance == null) return "No GameBalance asset; not added to the roster.";

            var roster = new List<EnemyDefinition>(balance.enemies ?? new EnemyDefinition[0]);
            if (roster.Contains(enemy)) return "Already in the roster.";

            roster.Add(enemy);
            balance.enemies = roster.ToArray();
            EditorUtility.SetDirty(balance);
            AssetDatabase.SaveAssetIfDirty(balance);
            return "Added to the roster.";
        }

        static string AddToWaves(EnemyDefinition enemy)
        {
            int added = 0, already = 0, missing = 0;

            foreach (var (wave, weight, maximum) in Waves)
            {
                var manifest = AssetDatabase.LoadAssetAtPath<WaveManifest>($"{WavesFolder}/{wave}.asset");
                if (manifest == null)
                {
                    missing++;
                    continue;
                }

                var entries = new List<WaveManifest.Entry>(manifest.entries ?? new WaveManifest.Entry[0]);
                if (entries.Exists(e => e.enemy == enemy))
                {
                    already++;
                    continue;
                }

                entries.Add(new WaveManifest.Entry { enemy = enemy, weight = weight, guaranteedMinimum = 0, maximum = maximum });
                manifest.entries = entries.ToArray();
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssetIfDirty(manifest);
                added++;
            }

            return $"Waves: added to {added}, already in {already}{(missing > 0 ? $", {missing} manifests not found" : "")}.";
        }
    }
}
