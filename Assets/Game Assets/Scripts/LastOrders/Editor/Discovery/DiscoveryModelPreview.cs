using System;
using System.Collections.Generic;
using FoodStreet.Visitors.EditorTools;
using UnityEditor;
using UnityEngine;
using B = LastOrders.EditorTools.BuildingKit;
using Object = UnityEngine.Object;

namespace LastOrders.EditorTools
{
    /// <summary>
    /// Puts the discovery sites' models side by side in the open scene, unsaved, to look at - every
    /// part built fresh in memory, nothing written to the project - and takes them away again.
    /// For checking a model from the game's angle while working on it.
    /// </summary>
    public static class DiscoveryModelPreview
    {
        const string RootName = "__DiscoveryPreview";
        const string CameraName = "__DiscoveryPreviewCamera";

        [MenuItem("Last Orders/Discovery/Preview Models (Unsaved)", priority = 62)]
        static void PreviewFromMenu() => Debug.Log(Show(new Vector3(60f, 0f, -24f), true));

        [MenuItem("Last Orders/Discovery/Clear Model Preview", priority = 63)]
        static void ClearFromMenu() => Clear();

        public static void Clear()
        {
            foreach (string name in new[] { RootName, CameraName })
            {
                GameObject go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Lays every model out in a row from <paramref name="at"/> eastward, found and restored
        /// overlays shown as <paramref name="restored"/> says. Returns a line for the log.
        /// </summary>
        public static string Show(Vector3 at, bool restored)
        {
            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            root.position = at;

            int parts = 0;
            float x = 0f;

            foreach ((string name, Func<List<(string, B.Model, Vector3, Quaternion, bool)>> build, float width) in Models())
            {
                Transform group = new GameObject(name).transform;
                group.SetParent(root, false);
                group.localPosition = new Vector3(x + width * 0.5f, 0f, 0f);
                x += width + 2f;

                foreach ((string part, B.Model model, Vector3 position, Quaternion turn, bool overlayFound) in build())
                {
                    // Overlays: the found dressing shows only when looking at it derelict, the restored only restored.
                    if (part.StartsWith("Derelict") && restored) continue;
                    if (part.StartsWith("Restored") && !restored) continue;

                    Mesh mesh = model.Build(part);
                    var go = new GameObject(part);
                    go.transform.SetParent(group, false);
                    go.transform.localPosition = position;
                    go.transform.localRotation = turn;
                    B.Show(go, mesh);
                    parts++;
                }
            }

            return $"[Discovery] Preview: {parts} parts at {at}, {(restored ? "restored" : "derelict")}.";
        }

        /// <summary>Every site's models, each as named parts placed about its own middle.</summary>
        static IEnumerable<(string, Func<List<(string, B.Model, Vector3, Quaternion, bool)>>, float)> Models()
        {
            yield return ("FoodTruck", FoodTruck, 3f);
            yield return ("DriveThrough", DriveThrough, 9f);
            yield return ("SecurityHut", SecurityHut, 7f);
            yield return ("ShootingRange", ShootingRange, 12f);
        }

        static List<(string, B.Model, Vector3, Quaternion, bool)> SecurityHut()
        {
            Quaternion none = Quaternion.identity;
            var device = new Vector3(0f, 0f, -3.6f);
            return new List<(string, B.Model, Vector3, Quaternion, bool)>
            {
                ("Hut", SecurityHutModel.Hut(), Vector3.zero, none, false),
                ("Derelict", SecurityHutModel.Derelict(), Vector3.zero, none, true),
                ("Restored", SecurityHutModel.Restored(), Vector3.zero, none, false),
                ("Device", SecurityHutModel.Device(), device, none, false),
                ("Beacon", SecurityHutModel.DeviceBeacon(), device + Vector3.up * (SecurityHutModel.DeviceHead + 0.23f), none, false),
                ("Ring", SecurityHutModel.SoundRing(), device + Vector3.up * (SecurityHutModel.DeviceHead + 0.11f), Quaternion.Euler(90f, 0f, 0f), false),
                ("Earshot", SecurityHutModel.Earshot(), device, none, false),
            };
        }

        static List<(string, B.Model, Vector3, Quaternion, bool)> ShootingRange()
        {
            Quaternion none = Quaternion.identity;
            var list = new List<(string, B.Model, Vector3, Quaternion, bool)>
            {
                ("Range", ShootingRangeModel.Range(), Vector3.zero, none, false),
                ("Derelict", ShootingRangeModel.Derelict(), Vector3.zero, none, true),
                ("Restored", ShootingRangeModel.Restored(), Vector3.zero, none, false),
            };

            for (int i = 0; i < ShootingRangeModel.Rails.Length; i++)
            {
                Vector3 rail = ShootingRangeModel.Rails[i] + Vector3.right * (i - 1) * 0.6f;
                list.Add(("Trolley", ShootingRangeModel.Trolley(), rail, none, false));
                list.Add(("Target", ShootingRangeModel.CardboardZombie(), rail + Vector3.up * ShootingRangeModel.HingeHeight + Vector3.back * 0.02f,
                          Quaternion.Euler(i == 2 ? 70f : 0f, 0f, 0f), false));
            }

            return list;
        }

        static List<(string, B.Model, Vector3, Quaternion, bool)> DriveThrough()
        {
            Quaternion none = Quaternion.identity;
            var lane = new List<Vector3> { new Vector3(9f, 0f, -4.4f), new Vector3(6f, 0f, -3.2f), new Vector3(0f, 0f, -3.2f), new Vector3(-4f, 0f, -3.2f),
                                           new Vector3(-7f, 0f, -4.4f) };
            return new List<(string, B.Model, Vector3, Quaternion, bool)>
            {
                ("Booth", DriveThroughModel.Booth(), Vector3.zero, none, false),
                ("Derelict", DriveThroughModel.Derelict(), Vector3.zero, none, true),
                ("Restored", DriveThroughModel.Restored(), Vector3.zero, none, false),
                ("Hatch", DriveThroughModel.Hatch(), DriveThroughModel.HatchClosed + Vector3.right * DriveThroughModel.HatchSlide, none, false),
                ("MenuBoard", DriveThroughModel.MenuBoard(), new Vector3(4.2f, 0f, -1.4f), none, false),
                ("DerelictMenu", DriveThroughModel.MenuBoardDerelict(), new Vector3(4.2f, 0f, -1.4f), none, true),
                ("RestoredMenu", DriveThroughModel.MenuBoardRestored(), new Vector3(4.2f, 0f, -1.4f), none, false),
                ("Bag", DriveThroughModel.Bag(), DriveThroughModel.ServePoint, none, false),
                ("Lane", DriveThroughModel.Lane(lane, 3.2f, 1f, 16f, 9f), Vector3.zero, none, false),
            };
        }

        static List<(string, B.Model, Vector3, Quaternion, bool)> FoodTruck()
        {
            var list = new List<(string, B.Model, Vector3, Quaternion, bool)>();
            VehicleModels.FoodTruckParts truck = VehicleModels.FoodTruck();
            Vector3 pivot = new Vector3(0f, truck.Rig.PivotY, 0f);
            Quaternion facing = Quaternion.Euler(0f, 90f, 0f);

            list.Add(("Body", truck.Rig.Body, pivot, facing, false));
            list.Add(("Derelict", truck.Derelict, pivot, facing, true));
            list.Add(("Restored", truck.Restored, pivot, facing, false));
            list.Add(("Awning", truck.Awning, facing * truck.Hinge, facing * truck.HingeTurn * Quaternion.Euler(78f, 0f, 0f), false));

            foreach (VehicleModels.Rig.WheelPlace wheel in truck.Rig.Wheels)
                list.Add(("Wheel", truck.Rig.Wheel, facing * wheel.Axle, facing * (wheel.Side < 0f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity), false));

            return list;
        }

        /// <summary>Points the scene's preview camera at <paramref name="target"/> from the game's south-west angle.</summary>
        public static Camera Aim(Vector3 target, float distance, float fieldOfView = 30f)
        {
            GameObject go = GameObject.Find(CameraName);
            if (go == null) go = new GameObject(CameraName);
            var camera = go.GetComponent<Camera>();
            if (camera == null) camera = go.AddComponent<Camera>();

            camera.enabled = false;
            camera.orthographic = false;
            camera.fieldOfView = fieldOfView;
            camera.farClipPlane = 400f;
            go.transform.position = target + new Vector3(-0.55f, 0.75f, -1f).normalized * distance;
            go.transform.LookAt(target);
            return camera;
        }
    }
}
