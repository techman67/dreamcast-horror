using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class DreamcastAudioCheckWindow : EditorWindow
{
    private readonly List<DreamcastAudioValidator.Report> reports = new List<DreamcastAudioValidator.Report>();
    private Vector2 scroll;
    private string roomStatus;
    [MenuItem("Dreamcast/Audio/Check selected audio")]
    public static void CheckSelected()
    {
        var paths = Selection.objects.Select(AssetDatabase.GetAssetPath).Where(x => !string.IsNullOrEmpty(x)).ToList();
        var folders = paths.Where(AssetDatabase.IsValidFolder).ToArray();
        if (folders.Length > 0) paths.AddRange(AssetDatabase.FindAssets("", folders).Select(AssetDatabase.GUIDToAssetPath).Where(DreamcastAudioValidator.IsAudioPath));
        Open(paths.Where(File.Exists).Distinct());
    }
    public static void Open(IEnumerable<string> paths)
    {
        var window = GetWindow<DreamcastAudioCheckWindow>("Dreamcast Audio Check");
        window.reports.Clear(); window.roomStatus = null;
        foreach (string path in paths) window.reports.Add(DreamcastAudioValidator.Analyze(path));
        window.Show();
    }
    [MenuItem("Dreamcast/Audio/Check room audio")]
    public static void CheckRoom()
    {
        var window = GetWindow<DreamcastAudioCheckWindow>("Dreamcast Audio Check");
        window.reports.Clear();
        try {
            var bundle = RoomAudioExporter.Build();
            window.roomStatus = $"ROOM AUDIO READY: {bundle.files.Count}/8 unique clips; {bundle.files.Values.Sum(x => x.Length - 44):N0}/524,288 PCM bytes. All active assignments and placed-source settings passed. No files were exported.";
        } catch (Exception error) { window.roomStatus = "ROOM AUDIO BLOCKED\n" + error.Message; }
        var clips = UnityEngine.Object.FindObjectsByType<DreamcastRoomSound>().Where(x => x.isActiveAndEnabled).Select(x => x.clip)
            .Concat(UnityEngine.Object.FindObjectsByType<DreamcastSoundCue>().Where(x => x.isActiveAndEnabled).Select(x => x.clip));
        foreach (string path in clips.Where(x => x != null).Select(AssetDatabase.GetAssetPath).Distinct()) {
            var report = DreamcastAudioValidator.Analyze(path);
            if (!report.Usable) window.reports.Add(report);
        }
        window.Show();
    }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Dreamcast audio compatibility", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select a source audio asset (or folder) in Project, then check it. These are limits of the current resident-audio pipeline, not every capability of the Dreamcast hardware.", MessageType.Info);
        if (GUILayout.Button("Check current selection")) CheckSelected();
        if (GUILayout.Button("Check all assigned room audio")) CheckRoom();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (roomStatus != null) EditorGUILayout.HelpBox(roomStatus, roomStatus.StartsWith("ROOM AUDIO READY") ? MessageType.Info : MessageType.Error);
        foreach (var report in reports) {
            EditorGUILayout.HelpBox(report.Describe(), report.Usable ? MessageType.Info : MessageType.Error);
            DreamcastAudioInspectorReport.DrawTrim(report);
            if (GUILayout.Button("Select " + Path.GetFileName(report.path))) Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(report.path);
        }
        if (reports.Count == 0 && roomStatus == null) EditorGUILayout.LabelField("No source audio selected.");
        EditorGUILayout.EndScrollView();
    }
}

public sealed class DreamcastAudioImportCheck : AssetPostprocessor
{
    private const string Preference = "Dreamcast.Audio.AutomaticChecks";
    private const string Menu = "Dreamcast/Audio/Automatic import checks";
    [MenuItem(Menu)] private static void Toggle() => EditorPrefs.SetBool(Preference, !EditorPrefs.GetBool(Preference, true));
    [MenuItem(Menu, true)] private static bool Checked() { UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(Preference, true)); return true; }
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
    {
        if (!EditorPrefs.GetBool(Preference, true)) return;
        var paths = imported.Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
            !p.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal) &&
            DreamcastAudioValidator.IsAudioPath(p)).ToArray();
        if (paths.Length == 0) return;
        EditorApplication.delayCall += () => {
            foreach (string path in paths) {
                var report = DreamcastAudioValidator.Analyze(path);
                var context = AssetDatabase.LoadMainAssetAtPath(path);
                if (report.Usable) Debug.Log("Dreamcast audio import check\n" + report.Describe(), context);
                else Debug.LogWarning("Dreamcast audio import check\n" + report.Describe(), context);
            }
        };
    }
}

public static class DreamcastAudioInspectorReport
{
    public static void DrawTrim(DreamcastAudioValidator.Report report)
    {
        if (!report.CanTrimForTesting || !report.path.StartsWith("Assets/", StringComparison.Ordinal) ||
            report.path.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal)) return;
        EditorGUILayout.HelpBox("Creates a separate WAV containing the first approximately 5.94 seconds. The original and current assignments stay unchanged. Cutting a hum may cause a click at its loop seam; this is a test excerpt, not a seamless-loop repair.", MessageType.Info);
        if (!GUILayout.Button("Make shortened test copy (~5.94 seconds)")) return;
        try {
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.GetDirectoryName(report.path).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(report.path) + "-dc-test.wav");
            DreamcastAudioTrimmer.WriteTestCopy(report.path, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var checkedCopy = DreamcastAudioValidator.Analyze(path);
            if (!checkedCopy.Usable) throw new InvalidDataException(checkedCopy.Describe());
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("Shortened test copy created. Drag this new clip into your sound component's Clip field. Original preserved.\n" + checkedCopy.Describe(), Selection.activeObject);
        } catch (Exception error) { Debug.LogError("Could not create shortened test copy: " + error.Message); }
    }
    private static readonly Dictionary<string, (long stamp, long length, DreamcastAudioValidator.Report report)> cache =
        new Dictionary<string, (long, long, DreamcastAudioValidator.Report)>();
    public static void Draw(AudioClip clip)
    {
        if (clip == null) { EditorGUILayout.HelpBox("Assign a source audio clip. An active component without a clip blocks export.", MessageType.Warning); return; }
        string path = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path)) { EditorGUILayout.HelpBox("This clip has no source asset file. Assign an imported PCM WAV from the Project window.", MessageType.Error); return; }
        var file = new FileInfo(path);
        long stamp = file.Exists ? file.LastWriteTimeUtc.Ticks : 0, length = file.Exists ? file.Length : 0;
        if (!cache.TryGetValue(path, out var entry) || entry.stamp != stamp || entry.length != length) {
            entry = (stamp, length, DreamcastAudioValidator.Analyze(path)); cache[path] = entry;
        }
        EditorGUILayout.HelpBox(entry.report.Describe(), entry.report.Usable ? MessageType.Info : MessageType.Error);
        DrawTrim(entry.report);
        if (GUILayout.Button("Open audio compatibility report")) DreamcastAudioCheckWindow.Open(new[] { path });
    }
}
[CustomEditor(typeof(DreamcastSoundCue))]
public sealed class DreamcastSoundCueInspector : Editor
{
    public override void OnInspectorGUI() { DrawDefaultInspector(); DreamcastAudioInspectorReport.Draw(((DreamcastSoundCue)target).clip); }
}
