using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(DreamcastSavePoint))]
public sealed class DreamcastSavePointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Attach this to a stationary book, typewriter or other object. The player presses E / A within range to save. No Unity trigger collider is required.",MessageType.Info);
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Save the scene, then Export sample room or Rebuild CDI. This first version stores one current-room progress slot with an automatic backup. L / Y loads it until the main menu is built. Give duplicated components different IDs.",MessageType.None);
    }
}
