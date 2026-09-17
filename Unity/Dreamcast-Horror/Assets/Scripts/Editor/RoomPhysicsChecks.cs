using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomPhysicsChecks
{
    [DllImport("dreamcast_horror", CallingConvention=CallingConvention.Cdecl)]
    static extern int unity_game_init([MarshalAs(UnmanagedType.LPStr)] string text);
    [DllImport("dreamcast_horror", CallingConvention=CallingConvention.Cdecl)]
    static extern void unity_game_step(float x,float z,float dt,int interact);
    [DllImport("dreamcast_horror", CallingConvention=CallingConvention.Cdecl)]
    static extern float unity_game_get_player_y();
    [DllImport("dreamcast_horror", CallingConvention=CallingConvention.Cdecl)]
    static extern float unity_game_get_player_z();
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    public static void CheckScene()
    {
        string text=RoomExporter.BuildText();
        Check(text.StartsWith("dreamcast_room 3"),"Physics scene did not export v3.");
        Check(UnityEngine.Object.FindObjectsByType<Collider>().Length==0,"Fixture accidentally uses Unity Physics.");
        Check(unity_game_init(text)==1,"Native room init failed.");
        for(int i=0;i<300;++i) unity_game_step(0,1,1f/60,0);
        Check(Mathf.Abs(unity_game_get_player_y()-1.5f)<.002f && Mathf.Abs(unity_game_get_player_z()-4)<.002f,"Native DLL failed stair ascent.");
        for(int i=0;i<300;++i) unity_game_step(0,-1,1f/60,0);
        Check(Mathf.Abs(unity_game_get_player_y())<.002f && Mathf.Abs(unity_game_get_player_z()+1)<.002f,"Native DLL failed stair descent.");
        Check(unity_game_init(text)==1 && Mathf.Abs(unity_game_get_player_y())<.002f,"Reset failed.");
        Check(NativeRoomDiagnostics.Validate(text.Replace("character 1.8 0.3","character 1.8 0.8"))!=null,"Excessive step accepted.");
        var player=UnityEngine.Object.FindAnyObjectByType<CppPlayerVisual>();
        Vector3 initial=player.transform.position;
        player.transform.position=new Vector3(0,.8f,1);
        bool rejected=false;
        try { RoomExporter.BuildText(); } catch(InvalidDataException e) { rejected=e.Message.Contains("overlaps"); }
        finally { player.transform.position=initial; }
        Check(rejected,"Spawn inside floor lacks actionable rejection.");
        RoomPropExporter.Build(); RoomAudioExporter.Build();
        Debug.Log("UNITY 3D PHYSICS CHECKS PASSED");
    }
    public static void Run()
    {
        try { EditorSceneManager.OpenScene(RoomPhysicsSetup.ScenePath); CheckScene(); EditorApplication.Exit(0); }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
