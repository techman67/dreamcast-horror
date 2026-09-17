using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run with Unity -batchmode -executeMethod ArchitectureChecks.Run.
// No additional packages or gameplay assembly dependencies are required.
public static class ArchitectureChecks
{
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_game_step(float x, float z, float seconds, int interactPressed);
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_z();
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint unity_game_get_camera_id();
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern int unity_game_init([MarshalAs(UnmanagedType.LPStr)] string text);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Call(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }

    // Explicit authoring action, separate from the read-only regression run.
    public static void ExportSample()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            RoomExporter.Export();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Check(Object.FindObjectsByType<NativeGameBridge>().Length == 1,
                "Exactly one native update owner is required.");
            foreach (GameObject obj in Object.FindObjectsByType<GameObject>())
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj) == 0,
                    $"Missing script on {obj.name}.");
            Check(GameObject.Find("SouthWall_Lintel").GetComponent<CppCollision>() == null,
                "Overhead lintel must remain decorative.");
            Check(GameObject.Find("Floor").GetComponent<CppCollision>() == null,
                "Floor must remain decorative.");

            var debug = Object.FindAnyObjectByType<CollisionDebug>();
            Call(debug, "DrawPlayer"); // Must return before accessing the DLL in edit mode.

            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var shape = probe.AddComponent<CppCollision>();
            var properties = new SerializedObject(shape);
            foreach (CppCollision.CollisionMode mode in new[] { CppCollision.CollisionMode.Box,
                CppCollision.CollisionMode.Sphere, CppCollision.CollisionMode.Capsule })
            {
                properties.FindProperty("mode").enumValueIndex = (int)mode;
                properties.ApplyModifiedPropertiesWithoutUndo();
                shape.Recalculate();
                Check(shape.ResolvedMode == mode, $"Explicit {mode} choice was ignored.");
            }
            properties.FindProperty("mode").enumValueIndex = (int)CppCollision.CollisionMode.Auto;
            properties.ApplyModifiedPropertiesWithoutUndo();
            shape.Recalculate();
            Check(shape.ResolvedMode == CppCollision.CollisionMode.Box, "Auto cube classification regressed.");
            properties.FindProperty("mode").enumValueIndex = (int)CppCollision.CollisionMode.Manual;
            properties.FindProperty("manualShape").enumValueIndex = (int)CppCollision.ManualShape.Sphere;
            properties.FindProperty("manualRadius").floatValue = 0.7f;
            properties.ApplyModifiedPropertiesWithoutUndo();
            shape.Recalculate();
            Check(shape.ResolvedMode == CppCollision.CollisionMode.Sphere && Mathf.Abs(shape.Radius - 0.7f) < 0.001f,
                "Manual radius or shape regressed.");
            Object.DestroyImmediate(probe);
            Check(CppCollision.ActiveShapes.Count == 11, "Collision debug registry contains stale entries.");

            string text = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, NativeGameBridge.RoomFileName));
            Check(NativeRoomDiagnostics.Validate(text) == null, "Shared room failed native validation.");
            Check(text.Replace("\r\n", "\n") == RoomExporter.BuildText().Replace("\r\n", "\n"),
                "Authored scene and exported room differ; explicitly export the room after authoring changes.");
            Check(NativeRoomDiagnostics.Validate(text.Replace("shapes 11", "shapes 129")) != null,
                "Native room loader accepted overflow.");

            // Export must refuse overflow before touching the existing file.
            var extras = new GameObject[118];
            for (int i = 0; i < extras.Length; ++i) {
                extras[i] = new GameObject($"Overflow_{i}");
                extras[i].AddComponent<CppCollision>();
            }
            bool rejected = false;
            try { RoomExporter.BuildText(); } catch (InvalidDataException) { rejected = true; }
            foreach (GameObject extra in extras) Object.DestroyImmediate(extra);
            Check(rejected, "Exporter accepted more than 128 shapes.");
            Check(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, NativeGameBridge.RoomFileName)) == text,
                "Failed export modified the existing room.");

            // Exercise actual C# startup, P/Invoke and presentation against the rebuilt DLL.
            Camera cameraA = GameObject.Find("GameplayCamera_01").GetComponent<Camera>();
            Camera cameraB = GameObject.Find("GameplayCamera_02").GetComponent<Camera>();
            Quaternion expectedA = cameraA.transform.rotation, expectedB = cameraB.transform.rotation;
            var bridge = Object.FindAnyObjectByType<NativeGameBridge>();
            Call(bridge, "Start");
            Check(NativeGameBridge.IsInitialized, "C# native startup failed.");
            Check(cameraA.enabled && !cameraB.enabled, "Initial camera activation failed.");
            Check(Quaternion.Angle(cameraA.transform.rotation, expectedA) < 0.01f, "Camera A orientation changed on load.");
            Check(NativeGameBridge.LoadedShapes.Length == 11, "Debug snapshot has the wrong collision count.");
            Call(bridge, "OnDisable");
            Check(!NativeGameBridge.IsInitialized, "Disabled host remained active.");
            Call(bridge, "OnEnable");
            Check(NativeGameBridge.IsInitialized && NativeGameBridge.LoadedShapes.Length == 11,
                "Re-enabled host did not resume the room.");
            unity_game_step(0, 1, 0.8f, 0);
            Call(bridge, "ApplyActiveCamera", false);
            Check(unity_game_get_camera_id() == 2 && cameraB.enabled && !cameraA.enabled, "Camera B was not presented.");
            Check(Quaternion.Angle(cameraB.transform.rotation, expectedB) < 0.01f, "Camera B orientation changed on load.");
            var player = Object.FindAnyObjectByType<CppPlayerVisual>();
            Call(player, "LateUpdate");
            Check(Mathf.Abs(player.transform.position.z - 0.8f) < 0.001f, "C++ position did not reach the visual.");
            unity_game_step(0, -1, 0.1f, 0);
            Call(bridge, "ApplyActiveCamera", false);
            Check(cameraA.enabled && !cameraB.enabled, "Boundary reversal was not presented.");
            Check(Mathf.Abs(unity_game_get_player_z() - 0.7f) < 0.001f, "Native movement mismatch.");
            Check(unity_game_init("invalid") == 0, "Invalid reload was accepted.");
            unity_game_step(0, 1, 1, 0);
            Check(unity_game_get_player_z() == 0, "Failed reload continued old gameplay.");
            Debug.Log("ARCHITECTURE CHECKS PASSED: scene, export, collision modes, overflow, native startup, movement and camera presentation.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
