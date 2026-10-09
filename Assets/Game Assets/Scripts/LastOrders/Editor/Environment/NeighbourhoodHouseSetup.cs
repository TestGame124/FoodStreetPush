using System.Collections.Generic;
using System.Text;
using LastOrders.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using E = LastOrders.EditorTools.EnvKit;
using K = LastOrders.EditorTools.PrimitiveKit;
using L = LastOrders.EditorTools.EnvironmentLayout;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the toon houses (<see cref="ToonHouseBuilder"/>) on the suburbs' lots of a level whose
    /// world edge is already built, without building it again - the streets, yards, cars and
    /// everything else stay exactly as they are.
    ///
    /// The suburbs are replayed off to one side from the same seed, which gives every lot's house
    /// plan as the planner made it; a lot whose replay does not match the level (by the names and
    /// places of everything on it) is left alone. The old house on each lot is switched off, not
    /// deleted, so 'Restore Previous Neighbourhood Houses' can bring it back.
    /// </summary>
    public static class NeighbourhoodHouseSetup
    {
        const string RootName = "WorldEnvironment";
        static readonly string[] Bands = { "Neighborhood_West", "Neighborhood_South", "Neighborhood_East" };

        /// <summary>The old house's parts.</summary>
        static readonly string[] Previous = { "Walls", "Roof", "Shed" };

        /// <summary>The parts either kind of house can have, left out when lots are compared.</summary>
        static readonly HashSet<string> HouseParts = new HashSet<string>
        {
            "Walls", "Roof", "Shed", ToonHouseBuilder.HouseChild, ToonHouseBuilder.ShedChild,
        };

        [MenuItem("Last Orders/Environment/Rebuild Neighbourhood Houses (Toon)", priority = 86)]
        static void RebuildMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Last Orders", "Stop Play mode first: the houses are built into the scene.", "OK");
                return;
            }

            Debug.Log(Rebuild());
        }

        [MenuItem("Last Orders/Environment/Restore Previous Neighbourhood Houses", priority = 87)]
        static void RestoreMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Last Orders", "Stop Play mode first.", "OK");
                return;
            }

            Debug.Log(Restore());
        }

        /// <summary>Builds a toon house on every lot of the open scene's suburbs. Does not save the scene.</summary>
        public static string Rebuild(int seed = L.DefaultSeed)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform world = FindRoot(scene);
            if (world == null) return $"[Last Orders] No {RootName} in '{scene.name}'; build the world edge first.";

            List<ToonHouseBuilder.Plan> plans = Replay(scene, world, seed, out List<string> mismatched);

            int houses = 0, sheds = 0, ruins = 0, vertices = 0;
            foreach (ToonHouseBuilder.Plan plan in plans)
            {
                Transform lot = FindLot(world, plan.Name);
                if (lot == null) continue;

                foreach (string part in Previous)
                {
                    Transform old = lot.Find(part);
                    if (old != null) old.gameObject.SetActive(false);
                }

                GameObject house = ToonHouseBuilder.Build(lot, plan);
                vertices += house.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                houses++;
                if (plan.Collapsed) ruins++;

                Transform oldShed = lot.Find(ToonHouseBuilder.ShedChild);
                if (plan.Shed != null)
                {
                    GameObject shed = ToonHouseBuilder.BuildShed(lot, plan);
                    vertices += shed.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                    sheds++;
                }
                else if (oldShed != null)
                {
                    Object.DestroyImmediate(oldShed.gameObject);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);

            var report = new StringBuilder($"[Last Orders] Toon houses on {houses} lots ({ruins} collapsed), {sheds} garden sheds, " +
                                           $"{vertices:N0} vertices, meshes in {ToonHouseBuilder.FolderFor(world)}. Old houses switched off. Scene not saved.");
            if (mismatched.Count > 0)
                report.Append($"\n  Left alone, their replay did not match the scene: {string.Join(", ", mismatched)}.");
            return report.ToString();
        }

        /// <summary>Takes the toon houses off and switches the old ones back on.</summary>
        public static string Restore()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform world = FindRoot(scene);
            if (world == null) return $"[Last Orders] No {RootName} in '{scene.name}'.";

            int restored = 0, missing = 0;
            foreach (Transform lot in Lots(world))
            {
                Transform house = lot.Find(ToonHouseBuilder.HouseChild);
                if (house == null) continue;

                bool hasOld = lot.Find("Walls") != null || lot.Find("Roof") != null;
                if (!hasOld)
                {
                    missing++;
                    continue;
                }

                Object.DestroyImmediate(house.gameObject);
                Transform shed = lot.Find(ToonHouseBuilder.ShedChild);
                if (shed != null) Object.DestroyImmediate(shed.gameObject);

                foreach (string part in Previous)
                {
                    Transform old = lot.Find(part);
                    if (old != null) old.gameObject.SetActive(true);
                }

                restored++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            string note = missing > 0
                ? $" {missing} lots had no old house to go back to (built toon from the start); rebuild the world edge with AbandonedHouseBuilder.Look = Classic for those."
                : "";
            return $"[Last Orders] Old houses back on {restored} lots; the toon houses' meshes stay in {ToonHouseBuilder.FolderFor(world)}.{note}";
        }

        // --- The replay -----------------------------------------------------------------------------

        /// <summary>
        /// The suburbs built again from <paramref name="seed"/> into a throwaway root, recording each
        /// house's plan; plans whose lot does not match the scene's are dropped and named in
        /// <paramref name="mismatched"/>.
        /// </summary>
        static List<ToonHouseBuilder.Plan> Replay(Scene scene, Transform world, int seed, out List<string> mismatched)
        {
            mismatched = new List<string>();
            var plans = new List<ToonHouseBuilder.Plan>();

            K.Reset();
            E.Reset();
            E.ResetPacks();

            List<Rect> keepOut = EnvironmentSceneSetup.SuburbKeepOut(scene);
            var temp = new GameObject("NeighbourhoodReplay (temporary)").transform;
            AbandonedHouseBuilder.HouseLook look = AbandonedHouseBuilder.Look;

            try
            {
                AbandonedHouseBuilder.Look = AbandonedHouseBuilder.HouseLook.Classic;
                AbandonedHouseBuilder.Recorder = plans;

                var rng = new DeterministicRandom(seed ^ DeterministicRandom.StableHash("suburbs"));
                NeighborhoodBuilder.Build(K.Group(Bands[0], temp), K.Group(Bands[1], temp), K.Group(Bands[2], temp), keepOut, ref rng);

                var good = new HashSet<string>();
                foreach (Transform band in temp)
                {
                    Transform sceneBand = world.Find(band.name);
                    foreach (Transform lot in band)
                    {
                        if (!lot.name.StartsWith("Lot_")) continue;
                        Transform match = sceneBand != null ? sceneBand.Find(lot.name) : null;
                        if (match != null && Signature(match) == Signature(lot)) good.Add(lot.name);
                        else mismatched.Add(lot.name);
                    }
                }

                plans.RemoveAll(p => !good.Contains(p.Name));
            }
            finally
            {
                AbandonedHouseBuilder.Look = look;
                AbandonedHouseBuilder.Recorder = null;
                Object.DestroyImmediate(temp.gameObject);
            }

            return plans;
        }

        /// <summary>The names and ground positions of everything on a lot but its house, in the lot's space.</summary>
        static string Signature(Transform lot)
        {
            var text = new StringBuilder();
            Walk(lot, lot, text);
            return text.ToString();
        }

        static void Walk(Transform lot, Transform t, StringBuilder text)
        {
            foreach (Transform child in t)
            {
                if (t == lot && HouseParts.Contains(child.name)) continue;
                Vector3 p = lot.InverseTransformPoint(child.position);
                text.Append(child.name).Append('@').Append(p.x.ToString("0.00")).Append(',').Append(p.z.ToString("0.00")).Append(';');
                Walk(lot, child, text);
            }
        }

        // --- Finding things ---------------------------------------------------------------------------

        static Transform FindRoot(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == RootName) return go.transform;
            return null;
        }

        static Transform FindLot(Transform world, string name)
        {
            foreach (string band in Bands)
            {
                Transform b = world.Find(band);
                Transform lot = b != null ? b.Find(name) : null;
                if (lot != null) return lot;
            }

            return null;
        }

        static IEnumerable<Transform> Lots(Transform world)
        {
            foreach (string band in Bands)
            {
                Transform b = world.Find(band);
                if (b == null) continue;
                foreach (Transform lot in b)
                    if (lot.name.StartsWith("Lot_")) yield return lot;
            }
        }
    }
}
