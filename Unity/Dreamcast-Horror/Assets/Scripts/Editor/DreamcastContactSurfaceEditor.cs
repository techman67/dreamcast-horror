using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DreamcastContactSurface))]
public sealed class DreamcastContactSurfaceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("Bakes contact shading from nearby static meshes onto a flat floor. No additional runtime shadow calculations. Export again after moving props. This has its own strength, separate from room vertex AO.",MessageType.Info);
        var resolution=serializedObject.FindProperty("resolution");
        resolution.intValue=EditorGUILayout.IntPopup("Resolution",resolution.intValue,new[]{"64 (8 KiB)","128 (32 KiB)","256 (128 KiB)"},new[]{64,128,256});
        EditorGUILayout.PropertyField(serializedObject.FindProperty("strength"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("distance"),new GUIContent("Contact distance (m)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("samples"));
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Shares the room's 512 KiB texture budget. Only upward flat faces receive the map; side faces keep their original mapping. Use opaque Lit materials without emission.",MessageType.None);
        if(GUILayout.Button("Open exported room preview")) EditorApplication.ExecuteMenuItem("Dreamcast/Lighting/Preview exported room");
    }
}
