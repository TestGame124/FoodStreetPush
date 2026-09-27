using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Deliberately no `using Unity.Cinemachine`: Cinemachine 3 has its own CameraTarget type and
// this file also has to talk about the project's legacy CameraTarget component.

/// <summary>
/// One-shot conversion from the old follow-target setup to <see cref="CameraPanZoomRig"/>.
///
/// For each scene it moves the tuning off the legacy CameraTarget object onto the Main
/// camera, clears the tracking targets that pointed at it, removes the procedural stages the
/// rig replaces, and deletes the proxy GameObject. Delete this file and CameraTarget.cs once
/// every scene has been through it.
/// </summary>
static class CameraRigMigration
{
    const string LegacyComponent = "CameraTarget";

    [MenuItem("Tools/Camera/Convert Open Scene To Pan-Zoom Rig")]
    static void ConvertOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log(Convert(scene, out bool changed));

        if (changed)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Camera/Convert All Scenes To Pan-Zoom Rig")]
    static void ConvertAllScenes()
    {
        if (!EditorUtility.DisplayDialog(
                "Convert all scenes to the pan-zoom rig",
                "Every scene under Assets will be opened, converted and saved. Commit first: "
                + "this rewrites scene files and cannot be undone once saved.\n\nContinue?",
                "Convert", "Cancel"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string originalScene = SceneManager.GetActiveScene().path;
        var report = new List<string>();

        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        for (int i = 0; i < guids.Length; ++i)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EditorUtility.DisplayProgressBar("Converting scenes", path, (float)i / guids.Length);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string line = Convert(scene, out bool changed);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add(line);
            }
        }

        EditorUtility.ClearProgressBar();

        if (!string.IsNullOrEmpty(originalScene))
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

        Debug.Log(report.Count == 0
            ? "Camera rig migration: nothing left to convert."
            : "Camera rig migration:\n" + string.Join("\n", report));
    }

    static string Convert(Scene scene, out bool changed)
    {
        changed = false;

        Component legacy = FindLegacyTarget(scene);
        if (legacy == null)
            return $"{scene.name}: no legacy {LegacyComponent}, skipped.";

        var controller = Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
        if (controller == null)
            return $"{scene.name}: has a {LegacyComponent} but no CameraController, skipped.";

        Unity.Cinemachine.CinemachineVirtualCameraBase registered = null;
        foreach (CameraController.CameraCase entry in controller.cameras)
        {
            if (entry.type == CameraController.CameraType.Main)
                registered = entry.VirtualCamera;
        }

        if (registered is not Unity.Cinemachine.CinemachineCamera main)
            return $"{scene.name}: the Main camera is missing or is not a CinemachineCamera, skipped.";

        Transform proxy = legacy.transform;
        Vector3 pivot = proxy.position;

        Vector3 offset = main.TryGetComponent(out Unity.Cinemachine.CinemachineFollow follow)
            ? follow.FollowOffset
            : main.transform.position - pivot;

        CameraPanZoomRig rig = main.GetComponent<CameraPanZoomRig>();
        if (rig == null)
            rig = Undo.AddComponent<CameraPanZoomRig>(main.gameObject);

        CopyTuning(new SerializedObject(legacy), new SerializedObject(rig), offset, pivot);

        int cleared = ClearTargets(proxy);

        // The rig writes the transform itself, so these stages would only fight it.
        if (main.TryGetComponent(out Unity.Cinemachine.CinemachineFollow staleFollow))
            Undo.DestroyObjectImmediate(staleFollow);
        if (main.TryGetComponent(out Unity.Cinemachine.CinemachineRotationComposer staleComposer))
            Undo.DestroyObjectImmediate(staleComposer);

        Undo.DestroyObjectImmediate(proxy.gameObject);

        changed = true;
        return $"{scene.name}: moved the rig onto {main.name}, offset {offset}, pivot {pivot}, "
             + $"cleared {cleared} tracking target(s), deleted the proxy.";
    }

    static void CopyTuning(SerializedObject src, SerializedObject dst, Vector3 offset, Vector3 pivot)
    {
        dst.FindProperty("followOffset").vector3Value = offset;
        dst.FindProperty("startPivot").vector3Value = pivot;
        dst.FindProperty("groundHeight").floatValue = pivot.y;

        Copy(src, dst, "minZoom", "minZoom");
        Copy(src, dst, "maxZoom", "maxZoom");
        Copy(src, dst, "DefaultZoom", "defaultZoom");
        Copy(src, dst, "zoomSpeed", "scrollZoomSpeed");
        Copy(src, dst, "movementTime", "panSharpness");

        // Prefer the rectangle already on the legacy component; fall back to the confiner
        // collider it was migrated from, which is live and measurable while this runs.
        Vector2 center = src.FindProperty("boundsCenter")?.vector2Value ?? Vector2.zero;
        Vector2 size = src.FindProperty("boundsSize")?.vector2Value ?? Vector2.zero;

        if (size.x <= 0f || size.y <= 0f)
        {
            if (src.FindProperty("legacyBoundsCollider")?.objectReferenceValue is BoxCollider confiner)
            {
                Bounds world = confiner.bounds;
                center = new Vector2(world.center.x, world.center.z);
                size = new Vector2(world.size.x, world.size.z);
            }
        }

        dst.FindProperty("boundsCenter").vector2Value = center;
        dst.FindProperty("boundsSize").vector2Value = size;
        dst.ApplyModifiedProperties();
    }

    static void Copy(SerializedObject src, SerializedObject dst, string from, string to)
    {
        SerializedProperty a = src.FindProperty(from);
        SerializedProperty b = dst.FindProperty(to);
        if (a != null && b != null)
            b.floatValue = a.floatValue;
    }

    static int ClearTargets(Transform proxy)
    {
        int cleared = 0;

        var cameras = Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Unity.Cinemachine.CinemachineCamera cam in cameras)
        {
            bool tracks = cam.Target.TrackingTarget == proxy;
            bool looks = cam.Target.LookAtTarget == proxy;
            if (!tracks && !looks)
                continue;

            Undo.RecordObject(cam, "Clear camera tracking target");
            if (tracks) cam.Target.TrackingTarget = null;
            if (looks) cam.Target.LookAtTarget = null;
            EditorUtility.SetDirty(cam);
            ++cleared;
        }

        return cleared;
    }

    static Component FindLegacyTarget(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                // By name, so this keeps compiling after CameraTarget.cs is deleted.
                Component legacy = t.GetComponent(LegacyComponent);
                if (legacy != null)
                    return legacy;
            }
        }

        return null;
    }
}
