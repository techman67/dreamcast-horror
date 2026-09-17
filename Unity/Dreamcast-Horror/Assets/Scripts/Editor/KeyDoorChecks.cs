using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class KeyDoorChecks
{
    private const string Pending = "Dreamcast.KeyDoorChecks";

    [InitializeOnLoadMethod]
    private static void ResumeAfterDomainReload()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += RunWhenPlaying;
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Pending, true);
        SessionState.SetFloat(Pending + ".deadline", (float)EditorApplication.timeSinceStartup + 60);
        EditorApplication.update += RunWhenPlaying;
        EditorApplication.EnterPlaymode();
    }

    private static void RunWhenPlaying()
    {
        if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Pending + ".deadline", 0))
        {
            SessionState.SetBool(Pending, false);
            Debug.LogError("Timed out waiting for the native room to initialize in Play mode.");
            EditorApplication.Exit(1);
            return;
        }
        if (!Application.isPlaying || !NativeGameBridge.IsInitialized) return;
        EditorApplication.update -= RunWhenPlaying;
        SessionState.SetBool(Pending, false);
        RunChecks();
    }
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_game_step(float x, float z, float seconds, int interactPressed);
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_x();
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_z();

    private static NativeGameBridge bridge;
    private static RoomAudio Audio => (RoomAudio)typeof(NativeGameBridge)
        .GetField("audioPresentation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bridge);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Call(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
    private static void Tick(float x = 0, float z = 0, bool interact = false, float seconds = 1f / 60)
    {
        unity_game_step(x, z, seconds, interact ? 1 : 0);
        Audio.Consume();
        Call(bridge, "RefreshSlicePresentation");
    }
    private static void Walk(float target, bool xAxis)
    {
        for (int i = 0; i < 1800; ++i)
        {
            float delta = target - (xAxis ? unity_game_get_player_x() : unity_game_get_player_z());
            if (Mathf.Abs(delta) < 0.001f) return;
            float direction = Mathf.Sign(delta);
            Tick(xAxis ? direction : 0, xAxis ? 0 : direction, false, Mathf.Min(Mathf.Abs(delta), 1f / 60));
        }
        throw new InvalidOperationException("The sample route is blocked.");
    }

    private static void InputChecks()
    {
        // Button edges are player-update state; editor-only device buffers do not
        // implement the same wasUpdatedThisFrame contract. Test in real Play mode.
        Check(Application.isPlaying, "Input checks require Play mode.");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Gamepad controller = InputSystem.AddDevice<Gamepad>();
        try
        {
            keyboard.MakeCurrent(); controller.MakeCurrent();
            // Prime edge tracking on a neutral frame, as the runtime Update loop does.
            UnityPlayerInput.Read();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.E));
            InputSystem.Update();
            UnityPlayerInput.Sample sample = UnityPlayerInput.Read();
            Check(sample.move.y == 1 && sample.interactPressed,
                $"Keyboard edge failed: move={sample.move}, edge={sample.interactPressed}, W={keyboard.wKey.isPressed}, E={keyboard.eKey.isPressed}, updated={keyboard.wasUpdatedThisFrame}.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.E));
            InputSystem.Update();
            Check(!UnityPlayerInput.Read().interactPressed, "Holding E repeated the interaction edge.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            InputSystem.Update();
            Check(UnityPlayerInput.Read().interactPressed, "A second E press was lost.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(controller, new GamepadState(GamepadButton.South) { leftStick = new Vector2(0.7f, 0) });
            InputSystem.Update();
            sample = UnityPlayerInput.Read();
            Check(sample.move.x > 0 && sample.move.x < 1 && sample.interactPressed, "Analog movement or controller south-button edge failed.");
            InputSystem.QueueStateEvent(controller, new GamepadState(GamepadButton.South) { leftStick = new Vector2(0.7f, 0) });
            InputSystem.Update();
            Check(!UnityPlayerInput.Read().interactPressed, "Holding the controller button repeated interaction.");
            InputSystem.QueueStateEvent(controller, new GamepadState(GamepadButton.DpadDown));
            InputSystem.Update();
            Check(UnityPlayerInput.Read().move.y == -1, "Controller D-pad mapping failed.");
            InputSystem.QueueStateEvent(controller, new GamepadState());
            InputSystem.Update();
            Check(UnityPlayerInput.Read().move == Vector2.zero, "Released controls continued moving.");
        }
        finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(controller); }
    }

    private static void RunChecks()
    {
        try
        {
            InputChecks();
            bridge = Object.FindAnyObjectByType<NativeGameBridge>();
            KeyDoorPresentation presentation = Object.FindAnyObjectByType<KeyDoorPresentation>();
            Check(presentation != null, "Missing key-door presentation.");
            Call(bridge, "Start");
            Check(NativeGameBridge.IsInitialized && NativeGameBridge.SliceView.Enabled, "Native objective failed to initialize.");
            Check(presentation.KeyVisual.gameObject.activeSelf && presentation.DoorVisual.gameObject.activeSelf, "Initial visuals are missing.");
            Walk(1.1f, true); Walk(-4, false); Walk(0, true);
            Tick(0, 0, true);
            Check(NativeGameBridge.SliceView.feedback == NativeKeyDoor.Feedback.DoorLocked, "Locked-door feedback missing.");
            for (int i = 0; i < 180; ++i) Tick(0, -1);
            Check(unity_game_get_player_z() > -5 && presentation.DoorVisual.gameObject.activeSelf, "Closed door did not obstruct movement.");
            Walk(1.1f, true); Walk(2.8f, false); Walk(0, true);
            Check(NativeGameBridge.SliceView.prompt == NativeKeyDoor.Prompt.TakeKey, "Key prompt missing.");
            Tick(0, 0, true);
            Check(NativeGameBridge.SliceView.HasKey && !presentation.KeyVisual.gameObject.activeSelf, "Key pickup did not update its visual.");
            Check(presentation.DoorVisual.gameObject.activeSelf, "Picking up the key opened the door automatically.");
            Walk(1.1f, true); Walk(-4, false); Walk(0, true);
            Check(NativeGameBridge.SliceView.prompt == NativeKeyDoor.Prompt.UnlockDoor, "Unlock prompt missing.");
            Tick(0, 0, true);
            Check(NativeGameBridge.SliceView.DoorOpen && !presentation.DoorVisual.gameObject.activeSelf, "Opening the door did not update its visual.");
            for (int i = 0; i < 180; ++i) Tick(0, -1);
            Check(NativeGameBridge.SliceView.Complete && unity_game_get_player_z() <= -5.9f, "Walking through the door did not complete the slice.");
            var bank = NativeAudioData.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"sample.audio")));
            Check(Audio.SampleBytes == bank.clips.Sum(x => (long)x.pcmBytes) && Audio.SampleBytes <= 524288 && Audio.ActiveCount <= 8, "Audio budget or voice cap mismatch.");
            Check(Audio.Played[0] > 0 && Audio.Played[1] == 1 && Audio.Played[2] == 1 && Audio.Played[3] == 1,
                "Unity did not play each interaction exactly once.");
            Audio.Consume();
            Check(Audio.Played[1] == 1, "Drained audio events replayed.");
            Audio.Stop();
            Check(Audio.ActiveCount == 0, "Audio stop left a playing voice.");
            Audio.Reset();
            Check(Audio.ActiveCount == bank.sources.Length, "Audio reset must restore exactly the authored placed sounds.");
            Call(bridge, "Start");
            Check(!NativeGameBridge.SliceView.HasKey && !NativeGameBridge.SliceView.Complete &&
                presentation.KeyVisual.gameObject.activeSelf && presentation.DoorVisual.gameObject.activeSelf, "Restart did not restore the objective.");
            Walk(1.1f, true); Walk(-4, false); Walk(0, true);
            Audio.Stop(); // Isolate saturation from the route's footsteps.
            for (int i = 0; i < 20; ++i) Tick(0, 0, true);
            Check(Audio.Played[2] == 5 && Audio.ActiveCount <= 8,
                "Interaction burst exceeded five reserved voices.");
            Audio.Reset();
            Check(Audio.ActiveCount == bank.sources.Length, "Burst reset left effects playing.");
            Debug.Log("KEY-DOOR CHECKS PASSED: keyboard/controller edges, analog/D-pad input, locked door, pickup, unlock, visuals, escape and reset.");
            Debug.Log("UNITY AUDIO CHECKS PASSED: bank budget, native event ABI, interaction playback, bounded slots, stop/reset.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
