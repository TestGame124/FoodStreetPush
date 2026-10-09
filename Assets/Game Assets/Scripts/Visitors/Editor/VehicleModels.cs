using System;
using System.Collections.Generic;
using System.Text;
using LastOrders.EditorTools;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using Object = UnityEngine.Object;

namespace FoodStreet.Visitors.EditorTools
{
    /// <summary>
    /// The visitors' vehicles, modelled in code in the game's smooth low-poly style with toon
    /// proportions, and their prefabs.
    ///
    /// Each is one soft rounded shape rather than a stack of boxes: a fat tub of bodywork bulging over
    /// big wheels, a bubble of a cabin on top with its glass laid on in crisp panes, round lamps for
    /// eyes and a chrome grille for a mouth, chunky bumpers, and the owner's things tied on the roof.
    /// Muted paint from the compound's palette - diner red, teal, cream, slate blue, mustard - worn
    /// at the edges and dusty low down.
    ///
    /// What moves (<see cref="VehicleAnimation"/>): the body rides on springs over the wheels, the
    /// wheels roll and the front pair steer, the door on the side the driver gets out swings open at
    /// a stop and is slammed shut. Particles (<see cref="VehicleEffects"/>): exhaust puffing on each
    /// chug of the engine, dust off the rear wheels, a cough of smoke as it starts and a puff of dust
    /// as it pulls up.
    ///
    /// Written as new assets beside the greybox ones, which are left exactly as they were: the
    /// vehicle definitions are pointed at the new prefabs, and 'Restore Greybox Vehicles' points
    /// them back. Re-running rebuilds the meshes and the prefabs in place.
    /// </summary>
    public static partial class VehicleModels
    {
        internal const string Root = "Assets/Game Assets/Content/Visitors";
        internal const string Folder = Root + "/" + EnvKit.Style + "/Vehicles";
        internal const string PrefabFolder = Root + "/Prefabs/" + EnvKit.Style;
        const string GreyboxFolder = Root + "/Prefabs";

        // --- The designs ---------------------------------------------------------------------------

        sealed class Design
        {
            public string Paint, Trim;
            public float WheelRadius;
            public Func<Color, Color, Rig> Make;
        }

        /// <summary>Which design builds which vehicle, by the vehicle's id (its asset name after "Vehicle_"), and its paint.</summary>
        static readonly Dictionary<string, Design> Designs = new Dictionary<string, Design>
        {
            ["Hatchback"] = new Design { Paint = "5F8A84", Trim = "B5AE9A", WheelRadius = 0.37f, Make = Hatchback },
            ["Pickup"] = new Design { Paint = "86423A", Trim = "B5AE9A", WheelRadius = 0.43f, Make = Pickup },
            ["Van"] = new Design { Paint = "B6AD97", Trim = "4E7D79", WheelRadius = 0.42f, Make = Van },
            ["Saloon"] = new Design { Paint = "55697D", Trim = "3C4855", WheelRadius = 0.37f, Make = Saloon },
            ["Shuttle"] = new Design { Paint = "A88C55", Trim = "B5AE9A", WheelRadius = 0.5f, Make = Shuttle },
        };

        internal static string IdOf(VehicleDefinition definition)
        {
            string name = definition != null ? definition.name : "";
            return name.StartsWith("Vehicle_") ? name.Substring("Vehicle_".Length) : name;
        }

        internal static bool HasDesign(string id) => Designs.ContainsKey(id);

        /// <summary>Writes the design's paint and wheel size onto the definition, so the asset says what the model shows.</summary>
        internal static void ApplyLook(VehicleDefinition definition, string id)
        {
            if (!Designs.TryGetValue(id, out Design design)) return;

            definition.bodyColour = B.Hex(design.Paint);
            definition.cabinColour = B.Hex(design.Trim);
            definition.wheelRadius = design.WheelRadius;
            EditorUtility.SetDirty(definition);
        }

        // --- Menus ---------------------------------------------------------------------------------

        [MenuItem("Food Street/Visitors/Rebuild Vehicle Models (Toon)", priority = 20)]
        static void RebuildAll()
        {
            var report = new StringBuilder("[Visitors] Toon vehicles:\n");
            foreach (VehicleDefinition definition in AllDefinitions())
            {
                if (!HasDesign(IdOf(definition)))
                {
                    report.AppendLine($"  {definition.name}: no design, left as it is.");
                    continue;
                }

                report.AppendLine("  " + Build(definition));
            }

            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        [MenuItem("Food Street/Visitors/Restore Greybox Vehicles", priority = 21)]
        static void RestoreGreybox()
        {
            var report = new StringBuilder("[Visitors] Greybox vehicles restored:\n");
            foreach (VehicleDefinition definition in AllDefinitions())
            {
                string path = $"{GreyboxFolder}/{definition.name}.prefab";
                var greybox = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (greybox == null)
                {
                    report.AppendLine($"  {definition.name}: no greybox prefab at {path}.");
                    continue;
                }

                definition.prefab = greybox;
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
                report.AppendLine($"  {definition.name} -> {path}");
            }

            Debug.Log(report.ToString());
        }

        static IEnumerable<VehicleDefinition> AllDefinitions()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:VehicleDefinition", new[] { Root + "/Vehicles" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition != null) yield return definition;
            }
        }

        // --- Building one --------------------------------------------------------------------------

        /// <summary>
        /// Builds the vehicle's meshes, effects and prefab - in place if it exists - and points the
        /// definition at it. Returns a line for the log.
        /// </summary>
        internal static string Build(VehicleDefinition definition)
        {
            string id = IdOf(definition);
            if (!Designs.TryGetValue(id, out Design design))
                throw new ArgumentException($"No toon design for vehicle '{definition.name}'.");

            ApplyLook(definition, id);

            Rig rig = design.Make(definition.bodyColour, definition.cabinColour);

            string meshFolder = $"{Folder}/{id}";
            ContentGenerator.EnsureFolder(meshFolder);
            ContentGenerator.EnsureFolder(PrefabFolder);

            Mesh body = rig.Body.Save($"{meshFolder}/Vehicle_{id}_Body.asset", $"Vehicle_{id}_Body");
            Mesh wheel = rig.Wheel.Save($"{meshFolder}/Vehicle_{id}_Wheel.asset", $"Vehicle_{id}_Wheel");
            var doors = new Mesh[rig.Doors.Count];
            for (int i = 0; i < doors.Length; i++)
                doors[i] = rig.Doors[i].Model.Save($"{meshFolder}/Vehicle_{id}_Door{i}.asset", $"Vehicle_{id}_Door{i}");

            string path = $"{PrefabFolder}/Vehicle_{id}.prefab";
            bool created = AssetDatabase.LoadAssetAtPath<GameObject>(path) == null;
            GameObject root = created ? new GameObject($"Vehicle_{id}") : PrefabUtility.LoadPrefabContents(path);

            try
            {
                Dress(root, definition, rig, body, wheel, doors);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (created) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }

            definition.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);

            int vertices = body.vertexCount + wheel.vertexCount * rig.Wheels.Count;
            foreach (Mesh door in doors) vertices += door.vertexCount;
            return $"{definition.name}: {(created ? "built" : "rebuilt")}, {vertices} vertices, prefab {path}";
        }

        static void Dress(GameObject root, VehicleDefinition definition, Rig rig, Mesh bodyMesh, Mesh wheelMesh, Mesh[] doorMeshes)
        {
            // Rebuilt from scratch below the root; the root's own components are kept and re-configured.
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            var painted = new[] { B.PaintedMaterial() };
            Vector3 pivot = new Vector3(0f, rig.PivotY, 0f);

            Transform body = Child(root.transform, "Body", pivot);
            B.Show(body.gameObject, bodyMesh, painted);

            var doors = new Transform[rig.Doors.Count];
            var angles = new float[rig.Doors.Count];
            for (int i = 0; i < doors.Length; i++)
            {
                doors[i] = Child(body, $"Door_{i}", rig.Doors[i].Hinge - pivot);
                B.Show(doors[i].gameObject, doorMeshes[i], painted);
                angles[i] = rig.Doors[i].OpenAngle;
            }

            var wheels = new Transform[rig.Wheels.Count];
            var steers = new bool[rig.Wheels.Count];
            for (int i = 0; i < wheels.Length; i++)
            {
                Rig.WheelPlace place = rig.Wheels[i];
                wheels[i] = Child(root.transform, place.Name, place.Axle);
                steers[i] = place.Steers;

                // The mesh's hub faces +X: the left-hand wheels are turned round to face out.
                Transform tyre = Child(wheels[i], "Tyre", Vector3.zero);
                tyre.localRotation = place.Side < 0f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                B.Show(tyre.gameObject, wheelMesh, painted);
            }

            // Effects: the tailpipe's smoke and cough ride on the body; the dust stays down by the wheels.
            // Out of the back and a little up: aimed down, the smoke hugs the ground under the bumper
            // where the camera never sees it.
            Transform pipe = Child(body, "Exhaust", rig.Exhaust - pivot + Vector3.back * 0.08f);
            pipe.localRotation = Quaternion.LookRotation(Vector3.back + Vector3.up * 0.35f, Vector3.up);
            ParticleSystem smoke = VehicleEffects.Exhaust(pipe);
            ParticleSystem cough = VehicleEffects.Backfire(pipe);

            var dust = new List<ParticleSystem>();
            foreach (Rig.WheelPlace place in rig.Wheels)
            {
                if (place.Steers) continue;
                Transform at = Child(root.transform, "Dust_" + place.Name, new Vector3(place.Axle.x, 0.08f, place.Axle.z - rig.WheelRadius * 0.55f));
                dust.Add(VehicleEffects.WheelDust(at));
            }

            Transform pullUp = Child(root.transform, "StopDust", new Vector3(0f, 0.05f, rig.FrontAxleZ));
            ParticleSystem stop = VehicleEffects.StopDust(pullUp, rig.Track + rig.WheelRadius * 0.4f);

            // A tap target over the whole car.
            Bounds hull = bodyMesh.bounds;
            hull.center += pivot;
            var hit = root.GetComponent<BoxCollider>();
            if (hit == null) hit = root.AddComponent<BoxCollider>();
            hit.center = hull.center;
            hit.size = hull.size;

            var vehicle = root.GetComponent<VisitorVehicle>();
            if (vehicle == null) vehicle = root.AddComponent<VisitorVehicle>();
            vehicle.Configure(definition);

            var animation = root.GetComponent<VehicleAnimation>();
            if (animation == null) animation = root.AddComponent<VehicleAnimation>();
            animation.Configure(vehicle, body, wheels, steers, rig.WheelRadius, doors, angles, rig.DoorSide, rig.DoorsOpenWhileParked,
                                rig.IdleWhileParked, smoke, cough, dust.ToArray(), stop);
        }

        static Transform Child(Transform parent, string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            return t;
        }

        // --- The rig --------------------------------------------------------------------------------

        /// <summary>
        /// One vehicle as built: the sprung body (modelled in vehicle space, stored about its pivot at
        /// axle height), each door about its hinge, one wheel, and where everything goes.
        /// </summary>
        internal sealed class Rig
        {
            public struct WheelPlace
            {
                public string Name;
                public Vector3 Axle;
                public bool Steers;
                public float Side;
            }

            public sealed class DoorPart
            {
                public B.Model Model;
                public Vector3 Hinge;
                public float OpenAngle;
            }

            public readonly float PivotY;
            public readonly B.Model Body;
            public readonly B.Model Wheel = new B.Model();
            public readonly List<DoorPart> Doors = new List<DoorPart>();
            public readonly List<WheelPlace> Wheels = new List<WheelPlace>();

            public float WheelRadius, Track, FrontAxleZ;
            public Vector3 Exhaust;
            public float DoorSide = 1f;
            public bool DoorsOpenWhileParked, IdleWhileParked;

            public Rig(float pivotY)
            {
                PivotY = pivotY;
                Body = new B.Model { Transform = Matrix4x4.Translate(Vector3.down * pivotY) };
            }

            /// <summary>A door hinged at <paramref name="hinge"/> (vehicle space), opening <paramref name="angle"/> degrees about Y. Model it in vehicle space.</summary>
            public B.Model AddDoor(Vector3 hinge, float angle)
            {
                var model = new B.Model { Transform = Matrix4x4.Translate(-hinge) };
                Doors.Add(new DoorPart { Model = model, Hinge = hinge, OpenAngle = angle });
                return model;
            }

            /// <summary>Four wheels at <paramref name="track"/> either side of the middle, the front pair steering, and the wheel itself.</summary>
            public void FourWheels(float track, float front, float rear, float radius, float width, Color rim, Color hubcap, bool whitewall)
            {
                WheelRadius = radius;
                Track = track;
                FrontAxleZ = front;

                Wheels.Add(new WheelPlace { Name = "Wheel_FL", Axle = new Vector3(-track, radius, front), Steers = true, Side = -1f });
                Wheels.Add(new WheelPlace { Name = "Wheel_FR", Axle = new Vector3(track, radius, front), Steers = true, Side = 1f });
                Wheels.Add(new WheelPlace { Name = "Wheel_RL", Axle = new Vector3(-track, radius, rear), Steers = false, Side = -1f });
                Wheels.Add(new WheelPlace { Name = "Wheel_RR", Axle = new Vector3(track, radius, rear), Steers = false, Side = 1f });

                Wheel.Transform = Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, Vector3.right));
                VehicleKit.Wheel(Wheel, radius, width, rim, hubcap, whitewall);
                Wheel.Transform = Matrix4x4.identity;
            }
        }
    }
}
