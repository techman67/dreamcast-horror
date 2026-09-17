using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DreamcastRoomSound))]
public sealed class DreamcastRoomSoundInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("clip"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("volume"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("loop"));
        var outer = serializedObject.FindProperty("audibleRange");
        var inner = serializedObject.FindProperty("fullVolumeRange");
        int mode = outer.floatValue == 0 ? 1 : 0;
        int selected = EditorGUILayout.Popup("Sound coverage", mode, new[] { "Nearby only", "Whole room" });
        if (selected != mode) {
            outer.floatValue = selected == 0 ? 2 : 0;
            inner.floatValue = selected == 0 ? .5f : 0;
        }
        if (selected == 0) {
            EditorGUILayout.PropertyField(inner, new GUIContent("Full volume distance"));
            EditorGUILayout.PropertyField(outer, new GUIContent("Silent beyond distance"));
            if (inner.floatValue < 0 || outer.floatValue <= inner.floatValue || outer.floatValue > 10000 ||
                float.IsNaN(inner.floatValue) || float.IsNaN(outer.floatValue))
                EditorGUILayout.HelpBox("Use 0 or more for full volume, and a larger outer distance (up to 10,000).", MessageType.Error);
            EditorGUILayout.HelpBox("Green: full volume. Cyan: silence. Sound fades between them. Drag the colored radius handles in Scene view; distances use world units and do not grow with object scale. Export before testing in Play mode.", MessageType.Info);
        } else EditorGUILayout.HelpBox("This sound is heard everywhere in the room. Choose Nearby only for an AC, machine or other local sound.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();
        DreamcastAudioInspectorReport.Draw(((DreamcastRoomSound)target).clip);
    }

    private void OnSceneGUI()
    {
        var sound = (DreamcastRoomSound)target;
        Vector3 position = sound.transform.position;
        if (sound.audibleRange == 0) { Handles.Label(position, "Room-wide sound (no distance fade)"); return; }
        float outer = sound.audibleRange, inner = sound.fullVolumeRange;
        if (!float.IsFinite(outer) || !float.IsFinite(inner) || outer <= 0 || inner < 0 || inner >= outer) return;
        using (new Handles.DrawingScope(Matrix4x4.identity)) {
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.cyan;
            outer = Handles.RadiusHandle(Quaternion.identity, position, outer);
            outer = Mathf.Clamp(outer, Mathf.Max(.01f, inner + .01f), 10000);
            Handles.Label(position + Vector3.right * outer, $"Silent beyond {outer:0.##} units");
            Handles.color = Color.green;
            inner = Handles.RadiusHandle(Quaternion.identity, position, inner);
            inner = Mathf.Clamp(inner, 0, Mathf.Max(0, outer - .01f));
            Handles.Label(position + Vector3.left * inner, $"Full volume to {inner:0.##} units");
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(sound, "Adjust sound distances");
                sound.audibleRange = outer; sound.fullVolumeRange = inner;
                PrefabUtility.RecordPrefabInstancePropertyModifications(sound);
                EditorUtility.SetDirty(sound);
            }
        }
    }
}
