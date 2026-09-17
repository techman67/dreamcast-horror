using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class RoomWorldPlayChecks
{
    const string Pending="Dreamcast.WorldPlayChecks";
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_save_action([MarshalAs(UnmanagedType.LPUTF8Str)] string directory,int load);
    static Keyboard keyboard; static int stage,frames;
    [InitializeOnLoadMethod] static void Resume() { if(SessionState.GetBool(Pending,false)) EditorApplication.update+=Tick; }
    public static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"dreamcast-world-play-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        Environment.SetEnvironmentVariable("DREAMCAST_SAVE_DIRECTORY",folder); SessionState.SetString(Pending+".folder",folder);
        EditorSceneManager.OpenScene("Assets/Scenes/ConnectedRoom01.unity");
        SessionState.SetBool(Pending,true); SessionState.SetFloat(Pending+".deadline",(float)EditorApplication.timeSinceStartup+90);
        EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        try {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Pending+".deadline",0)) throw new Exception("World Play check timed out at stage "+stage);
            if(!Application.isPlaying || !NativeGameBridge.IsInitialized || RoomWorldPresentation.Busy) return;
            var rooms=UnityEngine.Object.FindObjectsByType<DreamcastRoom>(); if(rooms.Length!=1) throw new Exception("Expected exactly one loaded room.");
            int room=rooms[0].roomId; var player=UnityEngine.Object.FindAnyObjectByType<CppPlayerVisual>();
            if(keyboard==null) { InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); }
            string folder=SessionState.GetString(Pending+".folder","");
            if(stage==0 || stage==4) { if(player.transform.position.x<.55f) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D)); return; } stage++; frames=0; }
            if(stage==1 || stage==5) { InputSystem.QueueStateEvent(keyboard,new KeyboardState()); if(++frames<3) return; stage++; InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E)); return; }
            if(stage==2) { InputSystem.QueueStateEvent(keyboard,new KeyboardState()); if(room!=2) return; if(unity_save_action(folder,0)!=0) throw new Exception("World save failed."); stage=4; return; }
            if(stage==6) { InputSystem.QueueStateEvent(keyboard,new KeyboardState()); if(room!=1) return; if(unity_save_action(folder,1)!=0) throw new Exception("Cross-room load failed."); stage=7; return; }
            if(stage==7 && room==2) { if(Mathf.Abs(player.transform.position.x)>.05f) throw new Exception("Save did not restore arrival position."); Debug.Log("WORLD LIVE PLAY CHECK PASSED: E transitions 1->2->1, single loaded room, save in room 2 and load from room 1."); Finish(0); }
        } catch(Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int result) { SessionState.SetBool(Pending,false); EditorApplication.update-=Tick; if(keyboard!=null) InputSystem.RemoveDevice(keyboard); Environment.SetEnvironmentVariable("DREAMCAST_SAVE_DIRECTORY",null); EditorApplication.Exit(result); }
}
