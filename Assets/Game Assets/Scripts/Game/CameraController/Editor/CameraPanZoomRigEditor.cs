using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

/// <summary>
/// Scene view authoring for the pan rig: drag the pan rectangle by its edges and move the
/// starting pivot with a position handle, instead of typing coordinates.
/// </summary>
[CustomEditor(typeof(CameraPanZoomRig))]
public class CameraPanZoomRigEditor : Editor
{
    static readonly Color HandleColor = new Color(0.2f, 0.85f, 1f, 0.9f);

    readonly BoxBoundsHandle boundsHandle = new BoxBoundsHandle
    {
        axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z
    };

    SerializedProperty boundsCenter;
    SerializedProperty boundsSize;
    SerializedProperty startPivot;
    SerializedProperty groundHeight;

    void OnEnable()
    {
        boundsCenter = serializedObject.FindProperty("boundsCenter");
        boundsSize = serializedObject.FindProperty("boundsSize");
        startPivot = serializedObject.FindProperty("startPivot");
        groundHeight = serializedObject.FindProperty("groundHeight");
    }

    void OnSceneGUI()
    {
        if (boundsCenter == null || boundsSize == null || startPivot == null)
            return;

        serializedObject.Update();

        float y = groundHeight.floatValue;

        // Starting pivot.
        EditorGUI.BeginChangeCheck();
        Vector3 movedPivot = Handles.PositionHandle(startPivot.vector3Value, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            startPivot.vector3Value = new Vector3(movedPivot.x, y, movedPivot.z);
            serializedObject.ApplyModifiedProperties();
        }

        // Pan rectangle.
        Vector2 center = boundsCenter.vector2Value;
        Vector2 size = boundsSize.vector2Value;
        if (size.x <= 0f || size.y <= 0f)
            return;

        boundsHandle.center = new Vector3(center.x, y, center.y);
        boundsHandle.size = new Vector3(size.x, 0f, size.y);

        using (new Handles.DrawingScope(HandleColor))
        {
            EditorGUI.BeginChangeCheck();
            boundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                boundsCenter.vector2Value = new Vector2(boundsHandle.center.x, boundsHandle.center.z);
                boundsSize.vector2Value = new Vector2(Mathf.Abs(boundsHandle.size.x), Mathf.Abs(boundsHandle.size.z));
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
