using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomAudioSetup
{
    [MenuItem("Dreamcast/Set up sample audio authoring")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Set up audio outside Play mode.");
        string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        Directory.CreateDirectory("Assets/Audio");
        foreach (string name in new[] { "footstep", "key", "locked", "unlock", "ambient" }) {
            string path = "Assets/Audio/" + name + ".wav";
            if (!File.Exists(path)) File.Copy(Path.Combine(repo, "Audio", name + ".wav"), path);
        }
        AssetDatabase.Refresh();
        var objective = UnityEngine.Object.FindAnyObjectByType<KeyDoorPresentation>();
        var player = UnityEngine.Object.FindAnyObjectByType<CppPlayerVisual>();
        if (objective == null || player == null) throw new InvalidOperationException("Open the sample room first.");
        void Cue(GameObject target, DreamcastCue cue, string name) {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<DreamcastSoundCue>())
                if (existing.cue == cue) return;
            var sound = Undo.AddComponent<DreamcastSoundCue>(target);
            sound.cue = cue; sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + ".wav");
            EditorUtility.SetDirty(sound);
        }
        Cue(player.gameObject, DreamcastCue.Footstep, "footstep");
        Cue(objective.KeyVisual.gameObject, DreamcastCue.KeyTaken, "key");
        Cue(objective.DoorVisual.gameObject, DreamcastCue.DoorLocked, "locked");
        Cue(objective.DoorVisual.gameObject, DreamcastCue.DoorUnlocked, "unlock");
        if (UnityEngine.Object.FindObjectsByType<DreamcastRoomSound>().Length == 0) {
            var obj = new GameObject("Room Ambience"); Undo.RegisterCreatedObjectUndo(obj, "Add room ambience");
            var sound = obj.AddComponent<DreamcastRoomSound>();
            sound.audibleRange = 0; sound.fullVolumeRange = 0;
            sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ambient.wav");
            EditorUtility.SetDirty(sound);
        }
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
    }
    public static void SetupAndExport()
    {
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Setup(); EditorSceneManager.SaveOpenScenes(); RoomExporter.Export();
            Debug.Log("AUDIO AUTHORING SETUP AND EXPORT PASSED."); EditorApplication.Exit(0);
        } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    [MenuItem("GameObject/Dreamcast/Placed Room Sound", false, 10)]
    private static void CreateSource()
    {
        var obj = new GameObject("Room Sound"); Undo.RegisterCreatedObjectUndo(obj, "Add room sound");
        GameObjectUtility.SetParentAndAlign(obj, Selection.activeGameObject);
        obj.AddComponent<DreamcastRoomSound>(); Selection.activeGameObject = obj;
    }
}
