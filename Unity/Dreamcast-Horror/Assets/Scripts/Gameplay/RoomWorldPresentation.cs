using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene/resource operations stay in the Unity host. C++ chooses links and owns progress.
public sealed class RoomWorldPresentation : MonoBehaviour
{
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr unity_world_configure(byte[] bytes,uint size,uint start);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern void unity_world_clear();
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern uint unity_world_room();
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern uint unity_world_hash();
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr unity_world_scene();
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_world_enter();
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_world_near();
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_world_depart(int interact);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_world_pending();
    static RoomWorldPresentation instance;
    bool busy; float opacity; string failure;
    public static bool Busy => instance!=null && instance.busy;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { instance=null; unity_world_clear(); }
    public static string Prepare()
    {
        var room=FindAnyObjectByType<DreamcastRoom>();
        if(room==null) { if(instance!=null) throw new InvalidDataException("Loaded scene has no Dreamcast Room settings."); unity_world_clear(); return Application.streamingAssetsPath; }
        if(instance==null) {
            byte[] bytes=File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"sample.world"));
            var error=unity_world_configure(bytes,(uint)bytes.Length,(uint)room.roomId); if(error!=IntPtr.Zero) throw new InvalidDataException(Marshal.PtrToStringAnsi(error));
            var go=new GameObject("Dreamcast room loading"); instance=go.AddComponent<RoomWorldPresentation>(); DontDestroyOnLoad(go);
        }
        if(unity_world_room()!=room.roomId || Marshal.PtrToStringAnsi(unity_world_scene())!=room.gameObject.scene.path) throw new InvalidDataException("Scene and exported world do not match. Export connected rooms again.");
        string folder=Path.Combine(Application.streamingAssetsPath,"world",room.roomId.ToString());
        uint hash=2166136261; foreach(byte b in File.ReadAllBytes(Path.Combine(folder,"sample.room"))) hash=unchecked((hash^b)*16777619);
        if(hash!=unity_world_hash()) throw new InvalidDataException("Room content does not match world manifest. Re-export connected rooms.");
        return folder;
    }
    public static void Enter() { if(instance!=null && unity_world_enter()==0) throw new InvalidDataException("Unable to restore room state or arrival."); }
    public static bool Transition(bool interact)
    {
        if(instance==null) return false;
        if(instance.busy) return true;
        if(unity_world_pending()!=0 || unity_world_depart(interact ? 1 : 0)!=0) { instance.busy=true; instance.StartCoroutine(instance.LoadRoom()); return true; }
        return false;
    }
    IEnumerator LoadRoom()
    {
        string destination=Marshal.PtrToStringAnsi(unity_world_scene());
        while(opacity<1) { opacity=Mathf.Min(1,opacity+Time.unscaledDeltaTime/.25f); yield return null; }
        var old=SceneManager.GetActiveScene(); var empty=SceneManager.CreateScene("Dreamcast loading"); SceneManager.SetActiveScene(empty);
        yield return SceneManager.UnloadSceneAsync(old);
        yield return Resources.UnloadUnusedAssets();
        GC.Collect(); yield return null;
        // The old scene and its audio/texture references are gone before this call.
        AsyncOperation load=null;
        try {
#if UNITY_EDITOR
            load=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(destination,new LoadSceneParameters(LoadSceneMode.Additive));
#else
            load=SceneManager.LoadSceneAsync(destination,LoadSceneMode.Additive);
#endif
        } catch(Exception error) { failure="Unable to load room: "+error.Message; Debug.LogException(error); }
        if(load==null) { failure=failure??"Destination scene is unavailable. Check the world export and scene build settings."; yield break; }
        yield return load;
        var next=SceneManager.GetSceneByPath(destination); if(next.IsValid()) SceneManager.SetActiveScene(next);
        yield return SceneManager.UnloadSceneAsync(empty);
        yield return null;
        if(!NativeGameBridge.IsInitialized) { failure="Room could not load. See the Console for the reason."; yield break; }
        while(opacity>0) { opacity=Mathf.Max(0,opacity-Time.unscaledDeltaTime/.25f); yield return null; }
        busy=false;
    }
    void OnGUI()
    {
        if(!busy && unity_world_near()>=0) GUI.Box(new Rect(12,Screen.height-120,300,30),"E / A: Enter room");
        if(opacity<=0) return;
        GUI.depth=-1000; var old=GUI.color; GUI.color=new Color(0,0,0,opacity); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture); GUI.color=old;
        if(opacity>=1) GUI.Label(new Rect(24,Screen.height-60,Screen.width-48,40),failure??"Loading room...");
    }
}
