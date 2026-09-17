using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Actual Play-mode smoke test: feeds a keyboard and lets MonoBehaviour.Update,
// native gameplay, presentation and rendering run normally. No state teleporting.
public static class KeyDoorPlaythrough
{
    private const string Pending = "Dreamcast.KeyDoorPlaythrough";
    private static Keyboard keyboard;
    private static CppPlayerVisual player;
    private static int stage, frames, lastFrame = -1;

    [InitializeOnLoadMethod]
    private static void ResumeAfterDomainReload()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Update;
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        string directory = Path.Combine(Path.GetTempPath(), "dreamcast-playthrough-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(directory);
        SessionState.SetString(Pending + ".output", directory);
        SessionState.SetFloat(Pending + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
        SessionState.SetBool(Pending, true);
        EditorApplication.update += Update;
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static void Next() { ++stage; frames = 0; Keys(); }
    private static void Capture(string name)
    {
        // Batch editors do not have a presented backbuffer for ScreenCapture.
        // Render the active gameplay camera explicitly; these images exclude IMGUI.
        Camera active = null;
        foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>())
            if (camera.isActiveAndEnabled) { active = camera; break; }
        Check(active != null, "No active gameplay camera to capture.");
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        float aspect = active.aspect;
        try
        {
            active.aspect = 1280f / 720;
            RenderPipeline.SubmitRenderRequest(active, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(SessionState.GetString(Pending + ".output", ""), name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            active.aspect = aspect;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
    private static void Walk(float target, bool xAxis)
    {
        float delta = target - (xAxis ? player.transform.position.x : player.transform.position.z);
        if (Mathf.Abs(delta) < 0.025f) { Next(); return; }
        Keys(xAxis ? (delta > 0 ? Key.D : Key.A) : (delta > 0 ? Key.W : Key.S));
    }

    private static void Update()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Pending + ".deadline", 0))
                throw new TimeoutException($"Playthrough timed out at stage {stage}.");
            if (!Application.isPlaying || !NativeGameBridge.IsInitialized || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (keyboard == null)
            {
                keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                player = UnityEngine.Object.FindAnyObjectByType<CppPlayerVisual>();
                Application.runInBackground = true;
                Time.captureDeltaTime = 1f / 60;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            }
            ++frames;
            switch (stage)
            {
                case 0:
                    Keys();
                    if (frames >= 30) { Capture("01-start"); Next(); }
                    break;
                case 1: Walk(1.1f, true); break;
                case 2: Walk(-4, false); break;
                case 3: Walk(0, true); break;
                case 4: Next(); Keys(Key.E); break;
                case 5:
                    Keys(Key.S);
                    if (frames >= 120)
                    {
                        Check(!NativeGameBridge.SliceView.DoorOpen && player.transform.position.z > -5, "Closed door failed during Play mode.");
                        Capture("02-locked-door"); Next();
                    }
                    break;
                case 6: Walk(1.1f, true); break;
                case 7: Walk(2.8f, false); break;
                case 8: Walk(0, true); break;
                case 9: Next(); Keys(Key.E); break;
                case 10:
                    Keys();
                    if (frames >= 5)
                    {
                        Check(NativeGameBridge.SliceView.HasKey, "E did not pick up the key during Play mode.");
                        Capture("03-key-collected"); Next();
                    }
                    break;
                case 11: Walk(1.1f, true); break;
                case 12: Walk(-4, false); break;
                case 13: Walk(0, true); break;
                case 14: Next(); Keys(Key.E); break;
                case 15:
                    Keys();
                    if (frames >= 5)
                    {
                        Check(NativeGameBridge.SliceView.DoorOpen, "E did not unlock the door during Play mode.");
                        Capture("04-door-open"); Next();
                    }
                    break;
                case 16:
                    Keys(Key.S);
                    if (NativeGameBridge.SliceView.Complete) { Capture("05-escaped"); Next(); }
                    break;
                case 17:
                    Keys();
                    if (frames >= 15)
                    {
                        string directory = SessionState.GetString(Pending + ".output", "");
                        Check(File.Exists(Path.Combine(directory, "05-escaped.png")), "Playthrough screenshot was not written.");
                        Debug.Log($"PLAYTHROUGH CHECKS PASSED: real input, Update loop, native objective and rendered screenshots. Output: {directory}");
                        SessionState.SetBool(Pending, false);
                        EditorApplication.Exit(0);
                    }
                    break;
            }
        }
        catch (Exception error)
        {
            SessionState.SetBool(Pending, false);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
