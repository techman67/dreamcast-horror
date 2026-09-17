using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Exercise actual MonoBehaviour Update/LateUpdate, rather than calling the DLL directly.
public static class RoomPhysicsInputChecks
{
    const string Pending = "Dreamcast.PhysicsInputChecks";
    static Keyboard keyboard;
    static Vector3 origin;
    static int direction;
    static double deadline;
    static bool jumped, landed;
    static float peak;
    [InitializeOnLoadMethod]
    static void Resume() { if(SessionState.GetBool(Pending,false)) EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene(RoomPhysicsSetup.ScenePath);
        SessionState.SetBool(Pending,true);
        SessionState.SetFloat(Pending+".deadline",(float)EditorApplication.timeSinceStartup+60);
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        try {
            if(EditorApplication.timeSinceStartup > SessionState.GetFloat(Pending+".deadline",0)) throw new Exception("Play-mode input check timed out.");
            if(!Application.isPlaying || !NativeGameBridge.IsInitialized) return;
            var player=UnityEngine.Object.FindAnyObjectByType<CppPlayerVisual>();
            if(keyboard==null) {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                origin=player.transform.position; deadline=EditorApplication.timeSinceStartup+5;
            }
            if(direction==4) {
                float rise=player.transform.position.y-origin.y;
                peak=Mathf.Max(peak,rise);
                // Hold Space through landing: an edge must not repeat as a bunny hop.
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
                if(rise>.5f) jumped=true;
                if(jumped && rise<.02f) landed=true;
                if(landed && rise>.05f) throw new Exception("Holding Space repeated the jump.");
                if(EditorApplication.timeSinceStartup>deadline) {
                    if(!jumped || !landed || peak>1.4f) throw new Exception("Space did not jump and land correctly.");
                    Debug.Log("UNITY LIVE WASD AND SPACE JUMP CHECKS PASSED"); Finish(0);
                }
                return;
            }
            Key[] keys={Key.W,Key.S,Key.A,Key.D};
            Vector3[] axes={Vector3.forward,Vector3.back,Vector3.left,Vector3.right};
            float distance=Vector3.Dot(player.transform.position-origin,axes[direction]);
            if(distance>=.15f) {
                Debug.Log("UNITY LIVE INPUT: "+keys[direction]+" moved player "+distance+" m");
                ++direction;
                if(direction==4) { origin=player.transform.position; deadline=EditorApplication.timeSinceStartup+2.5; return; }
                origin=player.transform.position; deadline=EditorApplication.timeSinceStartup+5;
            }
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("No player movement from "+keys[direction]+" through Unity Update.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys[direction]));
        } catch(Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int code)
    {
        EditorApplication.update-=Tick; SessionState.SetBool(Pending,false);
        if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
        EditorApplication.Exit(code);
    }
}
