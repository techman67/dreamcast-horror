using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomAudioRangeChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run()
    {
        try {
            Check(Marshal.SizeOf<NativeAudioData.Source>() == 32, "Source ABI mismatch.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var obj = new GameObject("Range test");
            var source = obj.AddComponent<DreamcastRoomSound>();
            Check(source.audibleRange == 2 && source.fullVolumeRange == .5f, "New sound defaults are not nearby.");
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/ambient.wav");
            source.transform.position = new Vector3(1,2,3); source.transform.localScale = new Vector3(4,2,7);
            source.audibleRange = 4; source.fullVolumeRange = 2; source.volume = .8f;
            var bundle = RoomAudioExporter.Build();
            Check(bundle.text.StartsWith("dreamcast_audio 2\n"), "Exporter did not write v2.");
            var decoded = NativeAudioData.Parse(bundle.text).sources[0];
            Check(decoded.range == 4 && decoded.fullVolumeRange == 2 && decoded.position == source.transform.position,
                "World-unit distances or position changed during export.");
            Check(Mathf.Abs(decoded.Gain(new Vector3(3,2,3)) - .8f) < .0001f, "Inner edge not full volume.");
            Check(Mathf.Abs(decoded.Gain(new Vector3(4,2,3)) - .4f) < .0001f, "Fade midpoint incorrect.");
            Check(decoded.Gain(new Vector3(5,2,3)) == 0 && decoded.Gain(new Vector3(8,2,3)) == 0, "Outer edge not silent.");
            Check(Mathf.Abs(decoded.Gain(new Vector3(1,5,3)) - .4f) < .0001f, "Vertical distance ignored.");
            foreach (float invalid in new[] { -1f, 4f, 5f, float.NaN, float.PositiveInfinity }) {
                source.fullVolumeRange = invalid;
                bool rejected = false;
                try { RoomAudioExporter.Build(); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Invalid inner range accepted.");
            }
            source.audibleRange = 0; source.fullVolumeRange = 0;
            decoded = NativeAudioData.Parse(RoomAudioExporter.Build().text).sources[0];
            Check(decoded.Gain(Vector3.one * 1000) == .8f, "Room-wide behavior changed.");
            // Verify that older exports retain their original center-to-edge fade.
            source.audibleRange = 4;
            string legacy = RoomAudioExporter.Build().text.Replace("dreamcast_audio 2", "dreamcast_audio 1").Replace("1 2 3 4 0", "1 2 3 4");
            decoded = NativeAudioData.Parse(legacy).sources[0];
            Check(decoded.fullVolumeRange == 0 && Mathf.Abs(decoded.Gain(new Vector3(3,2,3)) - .4f) < .0001f, "Legacy fade changed.");
            UnityEngine.Object.DestroyImmediate(obj);
            // Export the user's saved scene without changing/saving any authored values.
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            RoomExporter.Export();
            Debug.Log("AUDIO RANGE CHECKS PASSED: defaults, ABI, v1/v2, scaled objects, fade edges, height, room-wide and invalid ranges; saved scene exported.");
            EditorApplication.Exit(0);
        } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
