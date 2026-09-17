using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class RoomSaveChecks
{
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_game_init(string text);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr unity_save_configure(byte[] data,uint size,uint room);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_save_action([MarshalAs(UnmanagedType.LPUTF8Str)] string directory,int load);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_save_requested();
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern void unity_game_step_v2(float x,float y,float dt,int interact,int jump);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern float unity_game_get_player_x();
    static void Check(bool condition,string text) { if(!condition) throw new Exception(text); }
    public static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"dreamcast-save-check-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/LightingTestScene.unity");
            foreach(var old in UnityEngine.Object.FindObjectsByType<DreamcastSavePoint>()) UnityEngine.Object.DestroyImmediate(old);
            var point=new GameObject("Test journal").AddComponent<DreamcastSavePoint>(); point.transform.position=new Vector3(0,.9f,0);
            string room=RoomExporter.BuildText(); var data=RoomSaveExporter.Build(room); uint hash=RoomSaveExporter.Hash(System.Text.Encoding.UTF8.GetBytes(room));
            Check(unity_game_init(room)==1,"Native initialization failed."); Check(unity_save_configure(data,(uint)data.Length,hash)==IntPtr.Zero,"Save export rejected.");
            unity_game_step_v2(0,0,.016f,1,0); Check(unity_save_requested()==0,"Authored point did not request save.");
            Check(unity_save_action(folder,1)==1,"Empty storage not reported."); Check(unity_save_action(folder,0)==0,"PC save failed.");
            unity_game_step_v2(1,0,.2f,0,0); Check(unity_game_get_player_x()>0,"Test movement failed.");
            Check(unity_save_action(folder,1)==0 && Mathf.Abs(unity_game_get_player_x())<.001f,"Saved position was not restored.");
            Check(unity_save_action(folder,0)==0 && File.Exists(Path.Combine(folder,"progress1.sav")),"Backup record missing.");
            File.WriteAllBytes(Path.Combine(folder,"progress1.sav"),new byte[]{1,2,3}); Check(unity_save_action(folder,1)==0,"Corrupt latest record did not recover backup.");
            Check(unity_save_configure(data,(uint)data.Length,hash+1)!=IntPtr.Zero,"Mismatched room accepted.");
            var duplicate=new GameObject("Duplicate").AddComponent<DreamcastSavePoint>();
            bool rejected=false; try { RoomSaveExporter.Build(room); } catch(InvalidDataException e) { rejected=e.Message.Contains("unique"); } Check(rejected,"Duplicate IDs not explained.");
            UnityEngine.Object.DestroyImmediate(duplicate.gameObject); point.prompt=new string('x',48); rejected=false;
            try { RoomSaveExporter.Build(room); } catch(InvalidDataException e) { rejected=e.Message.Contains("47"); } Check(rejected,"Oversized prompt not explained.");
            Debug.Log("SAVE AUTHORING CHECKS PASSED: native round trip, PC save/load, backup recovery, room mismatch and authoring validation."); EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        finally { foreach(string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
    public static void SetupDemo()
    {
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/LightingTestScene.unity");
            var book=GameObject.Find("Save journal");
            if(book==null) {
                book=GameObject.CreatePrimitive(PrimitiveType.Cube); book.name="Save journal";
                book.transform.position=new Vector3(-3.2f,.83f,-2.2f); book.transform.localScale=new Vector3(.34f,.06f,.44f);
                UnityEngine.Object.DestroyImmediate(book.GetComponent<Collider>());
                const string path="Assets/Materials/M_SaveJournal.mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.SetColor("_BaseColor",new Color(.5f,.12f,.08f)); AssetDatabase.CreateAsset(mat,path); }
                book.GetComponent<MeshRenderer>().sharedMaterial=mat;
                var point=book.AddComponent<DreamcastSavePoint>(); point.prompt="Save at journal"; point.interactionRange=1.5f;
            }
            RoomExporter.Export(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("SAVE DEMO EXPORTED: journal on left box; E/A save and L/Y load."); EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
