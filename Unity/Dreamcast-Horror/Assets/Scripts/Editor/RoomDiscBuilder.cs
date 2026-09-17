using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RoomDiscBuilder
{
    private static Process process;
    private static readonly StringBuilder log = new StringBuilder();
    private static string repository;
    private static bool exitAfterBuild;

    // Batch verification follows the exact same menu-command path.
    public static void CheckBuild()
    {
        try {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            exitAfterBuild = true; Build();
        } catch (Exception e) { UnityEngine.Debug.LogException(e); EditorApplication.Exit(1); }
    }

    [MenuItem("Dreamcast/Export room and build CDI")]
    public static void Build()
    {
        if (process != null) throw new InvalidOperationException("A CDI build is already running.");
        RoomExporter.Export();
        repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        var info = new ProcessStartInfo("powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(repository, "Simulant/run.ps1") + "\" package") {
            WorkingDirectory = repository, UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true
        };
        log.Clear(); process = new Process { StartInfo = info };
        DataReceivedEventHandler append = (_, args) => { if (args.Data != null) lock (log) log.AppendLine(args.Data); };
        process.OutputDataReceived += append; process.ErrorDataReceived += append;
        try {
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            EditorApplication.LockReloadAssemblies(); EditorApplication.update += Poll;
            UnityEngine.Debug.Log("Building Dreamcast CDI from the exported room and referenced audio...");
        } catch { process.Dispose(); process = null; throw; }
    }
    private static void Poll()
    {
        if (!process.HasExited) return;
        EditorApplication.update -= Poll;
        int exitCode = process.ExitCode;
        try {
            process.WaitForExit();
            string folder = Path.Combine(repository, "Simulant/build"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "unity-cdi-build.log");
            lock (log) File.WriteAllText(path, log.ToString());
            if (process.ExitCode != 0) UnityEngine.Debug.LogError("CDI build failed. Close Flycast if it has the old disc open, then inspect: " + path);
            else UnityEngine.Debug.Log("CDI ready: " + Path.Combine(repository, "FlyCast/dreamcast-horror.cdi"));
        } finally {
            process.Dispose(); process = null; EditorApplication.UnlockReloadAssemblies();
            if (exitAfterBuild) EditorApplication.Exit(exitCode);
        }
    }
}
