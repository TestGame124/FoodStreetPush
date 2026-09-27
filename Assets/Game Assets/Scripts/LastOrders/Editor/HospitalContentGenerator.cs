using FoodStreet.Visitors;
using FoodStreet.Visitors.EditorTools;
using LastOrders.Data;
using LastOrders.Medical;
using UnityEditor;
using UnityEngine;
using K = LastOrders.EditorTools.PrimitiveKit;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Builds the hospital tent: its definition, its prefab, and the shuttle that takes
    /// recovered visitors home.
    ///
    /// The tent is grey-box like the guard tower - canvas and poles from the primitive kit, the
    /// project's own characters in the cots - and expected to be replaced by a modelled one. The
    /// replacement only has to keep the parts <see cref="HospitalTent"/> is wired to.
    ///
    /// Local +Z is the way in. The cots run from the entrance to the back wall, heads at the
    /// back, so the patients look out through the way in.
    ///
    /// The definition's numbers are filled in only when it is created, so a tuned recovery time
    /// survives a re-run.
    /// </summary>
    public static class HospitalContentGenerator
    {
        public const string DefinitionFolder = "Assets/Game Assets/Content/LastOrders/Medical";
        public const string DefinitionPath = DefinitionFolder + "/Hospital_Tent.asset";

        public const string PrefabFolder = "Assets/Game Assets/Content/LastOrders/Prefabs/Medical";
        public const string TentPath = PrefabFolder + "/HospitalTent.prefab";

        public const string ShuttlePath = "Assets/Game Assets/Content/Visitors/Vehicles/Vehicle_Shuttle.asset";

        /// <summary>The patient drawn in a cot for a guard: the guard tower's own model.</summary>
        const int PatientModel = 3;

        const int MedicModel = 1;

        const float Width = 4f;
        const float Depth = 3.6f;
        const float Ridge = 2.5f;
        const float Eave = 1.2f;

        static readonly Color Canvas = K.Hex("7D8160");
        static readonly Color CanvasDark = K.Hex("5E6149");
        static readonly Color Sheet = K.Hex("EDEBE4");
        static readonly Color Blanket = K.Hex("7C9BB5");
        static readonly Color Cross = K.Hex("D2453C");

        [MenuItem("Last Orders/Hospital & Zombies/Rebuild Hospital Tent Prefab", priority = 62)]
        static void Rebuild() => EnsureTent(rebuild: true);

        /// <summary>The definition, created with the design's numbers if it does not exist, and
        /// hung off the balance asset if nothing is there yet.</summary>
        public static HospitalDefinition EnsureDefinition(GameBalanceConfig balance)
        {
            ContentGenerator.EnsureFolder(DefinitionFolder);

            var definition = AssetDatabase.LoadAssetAtPath<HospitalDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<HospitalDefinition>();
                definition.displayName = "Hospital Tent";
                definition.description = "A small tent beside the guarded entrance with two cots. Injured " +
                                         "visitors and guards recover here on their own; a medic makes it faster.";
                definition.cots = 2;
                definition.visitorRecoverySeconds = 90f;
                definition.survivorRecoverySeconds = 150f;
                definition.medicRole = SurvivorRole.Medic;

                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            if (balance != null && balance.hospital == null)
            {
                balance.hospital = definition;
                EditorUtility.SetDirty(balance);
            }

            return definition;
        }

        /// <summary>The shuttle bus: longer and taller than any customer's car, and yellow.</summary>
        public static VehicleDefinition EnsureShuttle()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(ShuttlePath);
            if (existing != null && existing.prefab != null) return existing;

            VisitorContentGenerator.EnsureFolders();
            K.Reset();

            return VisitorContentGenerator.Vehicle("Shuttle", "Shuttle", new Vector3(2.2f, 1.35f, 6f),
                                                   body: "E8B93C", cabin: "34302B", drive: 5.5f);
        }

        public static GameObject EnsureTent(bool rebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TentPath);
            if (existing != null && !rebuild) return existing;

            ContentGenerator.EnsureFolder(PrefabFolder);
            K.Reset();

            GameObject root = BuildTent();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TentPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[Last Orders] {(existing != null ? "Rebuilt" : "Created")} the hospital tent prefab at {TentPath}.");
            return prefab;
        }

        // --- The tent ------------------------------------------------------------------------

        static GameObject BuildTent()
        {
            var root = new GameObject("HospitalTent");
            Transform shell = K.Group("Canvas", root.transform);

            K.Cube("Groundsheet", shell, new Vector3(0f, 0.02f, 0f), new Vector3(Width, 0.04f, Depth), CanvasDark);

            BuildRoof(shell);
            BuildWalls(shell);
            BuildPoles(shell);

            var cots = new HospitalTent.Cot[2];
            cots[0] = BuildCot(root.transform, "Cot_0", -0.95f);
            cots[1] = BuildCot(root.transform, "Cot_1", 0.95f);

            GameObject medic = BuildMedic(root.transform);

            // Supplies at the back corner: a crate and a first-aid box on it.
            K.Box("Crate", root.transform, new Vector3(1.55f, 0f, -1.35f), new Vector3(0.6f, 0.5f, 0.5f), K.Wood);
            K.Box("FirstAid", root.transform, new Vector3(1.55f, 0.5f, -1.35f), new Vector3(0.36f, 0.2f, 0.26f), Sheet);
            K.Cube("FirstAidCross", root.transform, new Vector3(1.55f, 0.71f, -1.35f), new Vector3(0.22f, 0.02f, 0.07f), Cross);

            Transform entrance = K.Group("Entrance", root.transform);
            entrance.localPosition = new Vector3(0f, 0f, Depth * 0.5f + 0.5f);

            // Two upturned crates outside, where the recovered wait for the shuttle.
            var benches = new Transform[2];
            for (int i = 0; i < benches.Length; i++)
            {
                float x = i == 0 ? -1.3f : 1.3f;
                K.Box($"Seat_{i}", root.transform, new Vector3(x, 0f, Depth * 0.5f + 0.9f), new Vector3(0.5f, 0.42f, 0.5f), K.WoodLight);

                benches[i] = K.Group($"Wait_{i}", root.transform);
                benches[i].localPosition = new Vector3(x, 0f, Depth * 0.5f + 1.5f);
            }

            var tap = root.AddComponent<BoxCollider>();
            tap.center = new Vector3(0f, Ridge * 0.5f, 0f);
            tap.size = new Vector3(Width, Ridge, Depth);

            root.AddComponent<HospitalTent>().Configure(cots, medic, entrance, benches);
            return root;
        }

        /// <summary>Two sloped panels meeting at the ridge, each with a red cross that can be
        /// read from the camera whichever side of the tent it is on.</summary>
        static void BuildRoof(Transform parent)
        {
            float half = Width * 0.5f;
            float rise = Ridge - Eave;
            float slope = Mathf.Sqrt(half * half + rise * rise) + 0.15f;
            float angle = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject panel = K.Cube(side < 0 ? "Roof_L" : "Roof_R", parent,
                                          new Vector3(side * half * 0.5f, (Ridge + Eave) * 0.5f, 0f),
                                          new Vector3(slope, 0.05f, Depth + 0.3f), Canvas);

                // Tilted down toward its own eave.
                panel.transform.localRotation = Quaternion.Euler(0f, 0f, -side * angle);

                Transform badge = K.Group("Badge", panel.transform);
                badge.localPosition = new Vector3(0f, 0.6f, 0f);

                // Children of a scaled panel inherit its squash, so the badge is sized in the
                // panel's own units: 1.2 m across, however long the slope is.
                float sx = 1.2f / slope;
                float sz = 1.2f / (Depth + 0.3f);
                K.Cube("Patch", badge, Vector3.zero, new Vector3(sx, 0.4f, sz), Sheet);
                K.Cube("CrossA", badge, new Vector3(0f, 0.3f, 0f), new Vector3(sx * 0.75f, 0.4f, sz * 0.22f), Cross);
                K.Cube("CrossB", badge, new Vector3(0f, 0.3f, 0f), new Vector3(sx * 0.22f, 0.4f, sz * 0.75f), Cross);
            }
        }

        /// <summary>Side and back walls to the eave, and the front flaps tied back.</summary>
        static void BuildWalls(Transform parent)
        {
            float half = Width * 0.5f;

            K.Box("Wall_L", parent, new Vector3(-half, 0f, 0f), new Vector3(0.05f, Eave, Depth), Canvas);
            K.Box("Wall_R", parent, new Vector3(half, 0f, 0f), new Vector3(0.05f, Eave, Depth), Canvas);
            K.Box("Wall_Back", parent, new Vector3(0f, 0f, -Depth * 0.5f), new Vector3(Width, Eave, 0.05f), Canvas);

            // The gable: stacked strips narrowing to the ridge.
            const int strips = 4;
            float rise = Ridge - Eave;
            for (int i = 0; i < strips; i++)
            {
                float t = (i + 0.5f) / strips;
                float width = Width * (1f - t);
                K.Cube($"Gable_{i}", parent, new Vector3(0f, Eave + rise * t, -Depth * 0.5f),
                       new Vector3(width, rise / strips + 0.02f, 0.05f), Canvas);
            }

            foreach (float x in new[] { -half + 0.35f, half - 0.35f })
                K.Box(x < 0f ? "Flap_L" : "Flap_R", parent, new Vector3(x, 0f, Depth * 0.5f),
                      new Vector3(0.5f, Ridge * 0.8f, 0.08f), CanvasDark);
        }

        static void BuildPoles(Transform parent)
        {
            foreach (float z in new[] { Depth * 0.5f, -Depth * 0.5f })
                K.Cylinder("Pole", parent, new Vector3(0f, 0f, z), 0.08f, Ridge, K.WoodDark);

            K.Log("RidgePole", parent, new Vector3(0f, Ridge, 0f), 0.08f, Depth + 0.2f, K.WoodDark, yaw: 0f);
        }

        // --- Inside --------------------------------------------------------------------------

        /// <summary>
        /// A camp bed: frame, mattress, pillow at the back, and what is shown when somebody is in
        /// it - the blanket, and for a guard, the guard.
        /// </summary>
        static HospitalTent.Cot BuildCot(Transform parent, string name, float x)
        {
            Transform cot = K.Group(name, parent);
            cot.localPosition = new Vector3(x, 0f, 0f);

            const float length = 2f;
            const float width = 0.8f;
            const float height = 0.42f;

            for (int i = 0; i < 4; i++)
            {
                float lx = (i & 1) == 0 ? -width * 0.45f : width * 0.45f;
                float lz = (i & 2) == 0 ? -length * 0.45f : length * 0.45f;
                K.Box($"Leg_{i}", cot, new Vector3(lx, 0f, lz), new Vector3(0.05f, height, 0.05f), K.MetalDark);
            }

            K.Cube("Frame", cot, new Vector3(0f, height, 0f), new Vector3(width, 0.05f, length), K.Metal);
            K.Cube("Mattress", cot, new Vector3(0f, height + 0.07f, 0f), new Vector3(width * 0.92f, 0.1f, length * 0.96f), Sheet);
            K.Cube("Pillow", cot, new Vector3(0f, height + 0.16f, -length * 0.4f), new Vector3(width * 0.7f, 0.1f, 0.3f), Sheet);

            // The foot of the mattress, facing up the cot toward the pillow. A body laid here
            // with its feet on this point and its head toward forward is lying in the cot. Set
            // above the mattress by half a body's depth, or the back sinks into it.
            Transform bed = K.Group("Bed", cot);
            bed.localPosition = new Vector3(0f, height + 0.2f, length * 0.45f);
            bed.localRotation = Quaternion.LookRotation(Vector3.back);

            GameObject blanket = K.Cube("Blanket", cot, new Vector3(0f, height + 0.28f, length * 0.12f),
                                        new Vector3(width * 0.95f, 0.2f, length * 0.68f), Blanket);
            blanket.SetActive(false);

            GameObject patient = VisitorCharacterGenerator.CreateCharacter(PatientModel, "Patient", Vector3.zero, cot);
            if (patient == null) patient = K.Person("Patient", cot, Vector3.zero, K.Denim, chefHat: false, yaw: 0f).gameObject;

            patient.transform.localPosition = bed.localPosition;
            patient.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
            RemoveShadow(patient.transform);
            patient.SetActive(false);

            // Above the ridge, so the bar is never under the canvas.
            Transform bar = K.Group("BarAnchor", cot);
            bar.localPosition = new Vector3(0f, Ridge + 0.25f, 0f);

            return new HospitalTent.Cot { bed = bed, blanket = blanket, patientFigure = patient, barAnchor = bar };
        }

        /// <summary>Stands at the back between the cots, shown only while a medic is posted.</summary>
        static GameObject BuildMedic(Transform parent)
        {
            GameObject medic = VisitorCharacterGenerator.CreateCharacter(MedicModel, "Medic", Vector3.zero, parent);
            if (medic == null) medic = K.Person("Medic", parent, Vector3.zero, K.ChefWhite, chefHat: false, yaw: 0f).gameObject;

            medic.transform.localPosition = new Vector3(0f, 0f, -Depth * 0.5f + 0.5f);
            medic.transform.localRotation = Quaternion.identity;

            // An armband, so the medic is not just somebody standing in the tent.
            K.Cube("Armband", medic.transform, new Vector3(-0.22f, 1.18f, 0f), new Vector3(0.1f, 0.08f, 0.14f), Cross);

            medic.SetActive(false);
            return medic;
        }

        /// <summary>A lying figure's blob shadow would stand up off the mattress.</summary>
        static void RemoveShadow(Transform figure)
        {
            Transform shadow = figure.Find("Shadow");
            if (shadow != null) Object.DestroyImmediate(shadow.gameObject);
        }
    }
}
