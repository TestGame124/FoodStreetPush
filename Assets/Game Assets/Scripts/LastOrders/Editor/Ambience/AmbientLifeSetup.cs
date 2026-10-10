using System.Collections.Generic;
using LastOrders.Ambience;
using LastOrders.Perimeter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Brings the level to life: one root, <c>AmbientLife</c>, with the wind, the cloud shadows, the
    /// birds, the butterflies and fireflies, and what the wind carries - pollen, gust-thrown leaves, the
    /// smoke of fires still burning out in the suburbs. The trees' materials are told to sway with it.
    ///
    /// Re-runnable: the root is rebuilt from scratch each time, and the models, prefabs and effects with
    /// it. Nothing else in the scene is touched apart from the perches being measured - wall tops and
    /// roof ridges, found by dropping rays onto their meshes through colliders added for the moment and
    /// taken away again. 'Remove Ambient Life' takes the root out and stills the trees.
    /// </summary>
    internal static class AmbientLifeSetup
    {
        /// <summary>Where the birds may land: the clear ground round the compound, inside the suburbs and forest.</summary>
        static readonly Rect LandingArea = new Rect(-44f, -40f, 128f, 116f);

        /// <summary>Where they fly off to at dusk: deep in the forest to the north.</summary>
        static readonly Vector3 Roost = new Vector3(18f, 22f, 118f);

        const float TreeBend = 0.32f;
        const float LeafFlutter = 0.02f;

        /// <summary>Vegetation-only materials (FoliageKit gives the pack's plants their own), in every look's folder.</summary>
        static readonly string[] Foliage =
        {
            "Assets/Game Assets/Content/LastOrders/Environment/Materials/LowPoly/PolygonFarm_01_A_Toon.mat",
            "Assets/Game Assets/Content/LastOrders/Environment/Materials/LowPoly/PolygonFarm_01_A_ToonDusk.mat",
            "Assets/Game Assets/Content/LastOrders/Environment/Materials/Painted/PolygonFarm_01_A_Toon.mat",
            "Assets/Game Assets/Content/LastOrders/Environment/Materials/Painted/PolygonFarm_01_A_ToonDusk.mat",
            "Assets/Game Assets/Content/LastOrders/Environment/Materials/PolygonFarm_01_A_Toon.mat",
            "Assets/Game Assets/Content/LastOrders/Environment/Materials/PolygonFarm_01_A_ToonDusk.mat",
        };

        /// <summary>The fires: what is burning, found by name, and roughly where, in case it has moved.</summary>
        static readonly (string name, Vector3 near)[] Fires =
        {
            ("Obstacle_CarWreck_C", new Vector3(-22.2f, 0f, 7.6f)),
            ("SM_Prop_Truck_Rusted_01", new Vector3(48.4f, 0f, -62.7f)),
            ("Lot_033", new Vector3(114.2f, 0f, 40.7f)),
            ("Lot_042", new Vector3(90.8f, 0f, 71.7f)),
        };

        [MenuItem("Last Orders/Ambience/Set Up Ambient Life (Current Scene)", priority = 100)]
        static void Run() => Debug.Log("[Last Orders] " + Apply());

        [MenuItem("Last Orders/Ambience/Remove Ambient Life (Current Scene)", priority = 101)]
        static void RunRemove() => Debug.Log("[Last Orders] " + Remove());

        internal static string Apply()
        {
            var report = new List<string> { BirdModel.Apply(), ButterflyModel.Apply() };
            Texture2D clouds = AmbienceEffects.Clouds();
            report.Add(Sway(TreeBend, LeafFlutter));

            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject existing in scene.GetRootGameObjects())
                if (existing.name == AmbienceKit.RootName) Object.DestroyImmediate(existing);

            var root = new GameObject(AmbienceKit.RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            root.AddComponent<AmbientWind>();
            root.AddComponent<CloudShadows>().Configure(Sun(), clouds);

            AmbientBirds.Perch[] perches = FindPerches(out string perchReport);
            report.Add(perchReport);
            root.AddComponent<AmbientBirds>().Configure(
                Birds(BirdModel.Pigeons), Birds(BirdModel.Breed.Sparrow), Birds(BirdModel.Breed.Crow), Birds(BirdModel.Breed.Bat),
                LandingArea, perches, Roost);

            var butterflies = new List<Butterfly>();
            foreach (ButterflyModel.Kind kind in System.Enum.GetValues(typeof(ButterflyModel.Kind)))
                butterflies.Add(AssetDatabase.LoadAssetAtPath<GameObject>(ButterflyModel.PrefabPath(kind)).GetComponent<Butterfly>());
            root.AddComponent<AmbientInsects>().Configure(butterflies.ToArray(), AmbienceEffects.Fireflies(root.transform));

            // The plane the gusts' leaves land on: the ground.
            var ground = new GameObject("GroundPlane").transform;
            ground.SetParent(root.transform, false);

            var plumes = new List<ParticleSystem>();
            var fuel = new List<Renderer>();
            foreach ((string name, Vector3 near) in Fires)
            {
                Renderer burning = Burning(name, near);
                // Out of the top of the wreck, not inside it, where the body would hide the glow and the smoke's foot.
                Vector3 at = burning != null
                    ? new Vector3(burning.bounds.center.x, burning.bounds.max.y * 0.85f, burning.bounds.center.z)
                    : near + Vector3.up * 0.5f;
                plumes.Add(AmbienceEffects.Smoke(root.transform, "Smoke_" + name, at));
                fuel.Add(burning);
            }

            root.AddComponent<AmbientDrift>().Configure(AmbienceEffects.Pollen(root.transform), AmbienceEffects.Leaves(root.transform, ground),
                                                       plumes.ToArray(), fuel.ToArray());

            EditorSceneManager.MarkSceneDirty(scene);
            report.Add($"Ambient life: wind, cloud shadows, birds, butterflies, fireflies, pollen, gust leaves, {plumes.Count} smoke plumes. Scene not saved.");
            return string.Join("\n", report);
        }

        internal static string Remove()
        {
            Scene scene = SceneManager.GetActiveScene();
            int removed = 0;
            foreach (GameObject existing in scene.GetRootGameObjects())
            {
                if (existing.name != AmbienceKit.RootName) continue;
                Object.DestroyImmediate(existing);
                removed++;
            }

            string trees = Sway(0f, 0f);
            if (removed > 0) EditorSceneManager.MarkSceneDirty(scene);
            return $"Ambient life removed ({removed} root). {trees}";
        }

        /// <summary>Sets how far the plants lean and shiver in the wind. Zero stills them.</summary>
        static string Sway(float bend, float flutter)
        {
            int set = 0;
            foreach (string path in Foliage)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty("_WindBend")) continue;

                material.SetFloat("_WindBend", bend);
                material.SetFloat("_WindFlutter", flutter);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                set++;
            }

            return bend > 0f ? $"Trees sway: {set} foliage materials." : $"Trees stilled: {set} foliage materials.";
        }

        static AmbientBird[] Birds(params BirdModel.Breed[] breeds)
        {
            var birds = new List<AmbientBird>();
            foreach (BirdModel.Breed breed in breeds)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BirdModel.PrefabPath(breed));
                if (prefab != null) birds.Add(prefab.GetComponent<AmbientBird>());
            }

            return birds.ToArray();
        }

        static Light Sun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;

            Light best = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && (best == null || light.intensity > best.intensity)) best = light;
            return best;
        }

        /// <summary>The renderer of the thing burning: by name, nearest to where it was planned.</summary>
        static Renderer Burning(string name, Vector3 near)
        {
            Renderer best = null;
            float bestDistance = 12f;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name != name) continue;

                // A lot burns where its house stood, not in its yard's grass.
                Transform house = t.Find("House");
                Renderer renderer = (house != null ? house : t).GetComponentInChildren<Renderer>();
                if (renderer == null) continue;

                Vector3 c = renderer.bounds.center;
                float distance = new Vector2(c.x - near.x, c.z - near.z).magnitude;
                if (distance < bestDistance)
                {
                    best = renderer;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // --- Perches -----------------------------------------------------------------------------------

        /// <summary>
        /// Wall tops, roof ridges, the diner's wall rim: rays dropped onto each one's meshes. Each perch is
        /// pinned to the mesh it is on, so a crow's perch goes with its bit of wall when that is knocked down.
        /// </summary>
        static AmbientBirds.Perch[] FindPerches(out string report)
        {
            var perches = new List<AmbientBirds.Perch>();
            int walls = 0, roofs = 0;
            var rng = new System.Random(1010);

            foreach (CompoundWall wall in Object.FindObjectsByType<CompoundWall>(FindObjectsSortMode.None))
            {
                var intact = new SerializedObject(wall).FindProperty("intactVisuals").objectReferenceValue as GameObject;
                if (intact == null || !intact.activeInHierarchy) continue;

                Transform t = wall.transform;
                foreach (float share in new[] { -0.28f, 0.28f })
                {
                    Vector3 point = t.position + t.right * (wall.Length * share);
                    if (Drop(intact, point, out AmbientBirds.Perch perch, 1.2f))
                    {
                        perch.yaw = t.eulerAngles.y + (rng.Next(3) - 1) * 90f;
                        perches.Add(perch);
                        walls++;
                    }
                }
            }

            // The diner's walls, along their length.
            GameObject diner = GameObject.Find("Restaurant");
            if (diner != null)
                foreach (MeshRenderer renderer in diner.GetComponentsInChildren<MeshRenderer>())
                {
                    Bounds b = renderer.bounds;
                    if (b.size.y < 1.2f || b.max.y > 6f || Mathf.Min(b.size.x, b.size.z) > 0.8f || Mathf.Max(b.size.x, b.size.z) < 2f) continue;

                    bool alongX = b.size.x > b.size.z;
                    foreach (float share in new[] { 0.25f, 0.75f })
                    {
                        Vector3 point = alongX ? new Vector3(Mathf.Lerp(b.min.x, b.max.x, share), 0f, b.center.z)
                                               : new Vector3(b.center.x, 0f, Mathf.Lerp(b.min.z, b.max.z, share));
                        if (Drop(renderer.gameObject, point, out AmbientBirds.Perch perch, 1.2f))
                        {
                            perch.yaw = (alongX ? 0f : 90f) + (rng.Next(2) == 0 ? 0f : 180f);
                            perches.Add(perch);
                            walls++;
                        }
                    }
                }

            // Roof ridges: the suburb houses and the farmyard's coop and cow shelter.
            var roofed = new List<GameObject>();
            GameObject world = GameObject.Find("WorldEnvironment");
            if (world != null)
                foreach (Transform t in world.GetComponentsInChildren<Transform>())
                    if (t.name == "House" && t.parent != null && t.parent.name.StartsWith("Lot_")) roofed.Add(t.gameObject);

            GameObject farmyard = GameObject.Find("Farmyard");
            if (farmyard != null)
                foreach (Transform t in farmyard.transform)
                    roofed.Add(t.gameObject);

            foreach (GameObject building in roofed)
            {
                if (!building.activeInHierarchy) continue;
                if (Ridge(building, out AmbientBirds.Perch perch))
                {
                    perch.yaw = rng.Next(360);
                    perches.Add(perch);
                    roofs++;
                }
            }

            report = $"Perches: {walls} on walls, {roofs} on roofs.";
            return perches.ToArray();
        }

        /// <summary>A ray dropped onto <paramref name="target"/>'s meshes at <paramref name="point"/>: its top, if level enough and high enough.</summary>
        static bool Drop(GameObject target, Vector3 point, out AmbientBirds.Perch perch, float minimumHeight)
        {
            perch = default;
            var added = Collide(target);
            try
            {
                if (!Highest(added, point, out RaycastHit hit)) return false;
                if (hit.normal.y < 0.7f || hit.point.y < minimumHeight) return false;

                perch = Pin(hit);
                return true;
            }
            finally
            {
                foreach (MeshCollider c in added.Keys) Object.DestroyImmediate(c);
            }
        }

        /// <summary>The highest point of a building's roof: a grid of rays over it, the topmost hit.</summary>
        static bool Ridge(GameObject building, out AmbientBirds.Perch perch)
        {
            perch = default;
            var added = Collide(building);
            try
            {
                if (added.Count == 0) return false;

                Bounds bounds = default;
                bool first = true;
                foreach (Renderer r in added.Values)
                {
                    if (first) bounds = r.bounds;
                    else bounds.Encapsulate(r.bounds);
                    first = false;
                }

                if (bounds.size.y < 2.5f) return false;

                bool found = false;
                RaycastHit best = default;
                for (int i = 1; i < 8; i++)
                    for (int j = 1; j < 8; j++)
                    {
                        var point = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, i / 8f), 0f, Mathf.Lerp(bounds.min.z, bounds.max.z, j / 8f));
                        if (!Highest(added, point, out RaycastHit hit)) continue;
                        if (!found || hit.point.y > best.point.y)
                        {
                            best = hit;
                            found = true;
                        }
                    }

                if (!found) return false;

                perch = Pin(best);
                return true;
            }
            finally
            {
                foreach (MeshCollider c in added.Keys) Object.DestroyImmediate(c);
            }
        }

        static Dictionary<MeshCollider, Renderer> Collide(GameObject target)
        {
            var added = new Dictionary<MeshCollider, Renderer>();
            foreach (MeshRenderer renderer in target.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                added[collider] = renderer;
            }

            Physics.SyncTransforms();
            return added;
        }

        static bool Highest(Dictionary<MeshCollider, Renderer> mine, Vector3 point, out RaycastHit best)
        {
            best = default;
            bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(point.x, 60f, point.z), Vector3.down, 80f))
            {
                if (!(hit.collider is MeshCollider mesh) || !mine.ContainsKey(mesh)) continue;
                if (!found || hit.point.y > best.point.y)
                {
                    best = hit;
                    found = true;
                }
            }

            return found;
        }

        static AmbientBirds.Perch Pin(RaycastHit hit)
        {
            Renderer surface = hit.collider.GetComponent<Renderer>();
            return new AmbientBirds.Perch { surface = surface, local = surface.transform.InverseTransformPoint(hit.point) };
        }
    }
}
