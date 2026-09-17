using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class RoomSavePlayChecks
{
    const string Pending="Dreamcast.SavePlayChecks";
    static Keyboard keyboard;
    static int stage,frames;
    static Vector3 saved;
    [InitializeOnLoadMethod] static void Resume() { if(SessionState.GetBool(Pending,false)) EditorApplication.update+=Tick; }
    public static void Run()
    {
        string directory=Path.Combine(Path.GetTempPath(),"dreamcast-save-play-"+Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("DREAMCAST_SAVE_DIRECTORY",directory);
        SessionState.SetString(Pending+".folder",directory);
        EditorSceneManager.OpenScene("Assets/Scenes/LightingTestScene.unity");
        // This test owns its fixture. Earlier stairs tests export a different room.
        RoomExporter.Export();
        SessionState.SetBool(Pending,true); SessionState.SetFloat(Pending+".deadline",(float)EditorApplication.timeSinceStartup+60);
        EditorApplication.update+=Tick; EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        try {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Pending+".deadline",0)) throw new Exception("Save/load Play-mode check timed out at stage "+stage);
            if(!Application.isPlaying || !NativeGameBridge.IsInitialized) return;
            var player=UnityEngine.Object.FindAnyObjectByType<CppPlayerVisual>(); var pos=player.transform.position;
            if(keyboard==null) { InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); }
            var file=Path.Combine(SessionState.GetString(Pending+".folder",""),"progress0.sav");
            if(stage==0) { if(pos.x> -2) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A)); return; } stage=1; }
            if(stage==1) { if(pos.z> -2) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S)); return; } stage=2; frames=0; }
            if(stage==2) { InputSystem.QueueStateEvent(keyboard,new KeyboardState()); if(++frames<3) return; saved=pos; stage=3; InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E)); return; }
            if(stage==3) { InputSystem.QueueStateEvent(keyboard,new KeyboardState()); if(!File.Exists(file)) return; stage=4; }
            if(stage==4) { if(pos.x<saved.x+.4f) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D)); return; } stage=5; InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.L)); return; }
            if(stage==5) { InputSystem.QueueStateEvent(keyboard,new KeyboardState()); if(Vector3.Distance(pos,saved)>.06f) return; Debug.Log("SAVE LIVE PLAY CHECK PASSED: walk to journal, E saves, walk away, L restores through actual host Update."); Finish(0); }
        } catch(Exception e) { Debug.LogException(e); Finish(1); }
    }
    static void Finish(int result)
    {
        SessionState.SetBool(Pending,false); EditorApplication.update-=Tick;
        if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
        string folder=SessionState.GetString(Pending+".folder","");
        if(folder.StartsWith(Path.Combine(Path.GetTempPath(),"dreamcast-save-play-"),StringComparison.Ordinal) && Directory.Exists(folder)) { foreach(string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
        Environment.SetEnvironmentVariable("DREAMCAST_SAVE_DIRECTORY",null);
        EditorApplication.Exit(result);
    }
}
