using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(DreamcastWorld))]
public sealed class DreamcastWorldEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var world=(DreamcastWorld)target;
        EditorGUILayout.HelpBox("Up to 8 rooms. Only one room is loaded at a time on Dreamcast. Add a Dreamcast Room component to each scene and assign this world and its room ID.",MessageType.Info);
        serializedObject.Update(); EditorGUILayout.PropertyField(serializedObject.FindProperty("startingRoom"));
        var rooms=serializedObject.FindProperty("rooms");
        for(int i=0;i<rooms.arraySize;i++) {
            var room=rooms.GetArrayElementAtIndex(i); EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(room.FindPropertyRelative("id"),GUIContent.none,GUILayout.Width(55));
            var path=room.FindPropertyRelative("scenePath");
            var scene=(SceneAsset)EditorGUILayout.ObjectField(AssetDatabase.LoadAssetAtPath<SceneAsset>(path.stringValue),typeof(SceneAsset),false);
            path.stringValue=scene==null ? "" : AssetDatabase.GetAssetPath(scene);
            if(GUILayout.Button("Remove",GUILayout.Width(65))) { rooms.DeleteArrayElementAtIndex(i); EditorGUILayout.EndHorizontal(); break; }
            EditorGUILayout.EndHorizontal();
        }
        if(rooms.arraySize<8 && GUILayout.Button("Add room")) { rooms.InsertArrayElementAtIndex(rooms.arraySize); var r=rooms.GetArrayElementAtIndex(rooms.arraySize-1); r.FindPropertyRelative("id").intValue=rooms.arraySize; r.FindPropertyRelative("scenePath").stringValue=""; }
        serializedObject.ApplyModifiedProperties();
        if(GUILayout.Button("Export connected rooms")) RoomWorldExporter.Export(world);
    }
}

public static class RoomWorldExporter
{
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr unity_world_configure(byte[] bytes,uint size,uint start);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern void unity_world_clear();
    public static uint Hash(byte[] bytes) { uint h=2166136261; foreach(byte b in bytes) h=unchecked((h^b)*16777619); return h; }
    static uint Name(string text) { if(string.IsNullOrWhiteSpace(text) || text.Any(c=>c<32||c>126)) throw new InvalidDataException("Arrival names need printable ASCII text."); return Hash(Encoding.ASCII.GetBytes(text)); }
    static void Position(BinaryWriter w,Vector3 p) { if(!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z)||p.sqrMagnitude>10000f*10000f) throw new InvalidDataException("Room marker position is invalid."); w.Write(p.x); w.Write(p.y); w.Write(p.z); }
    static void StaticMarker(Component marker)
    {
        var objective=UnityEngine.Object.FindAnyObjectByType<KeyDoorPresentation>(); var t=marker.transform;
        if(marker.GetComponentInParent<Animator>()!=null || marker.GetComponentInParent<CppPlayerVisual>()!=null || (objective!=null && ((objective.KeyVisual!=null && t.IsChildOf(objective.KeyVisual)) || (objective.DoorVisual!=null && t.IsChildOf(objective.DoorVisual)))))
            throw new InvalidDataException(marker.name+": room markers must be static. Put a separate marker beside the moving object.");
    }
    static void ClearArrival(DreamcastArrival arrival)
    {
        StaticMarker(arrival);
        var bridge=UnityEngine.Object.FindAnyObjectByType<NativeGameBridge>(); var physics=UnityEngine.Object.FindObjectsByType<DreamcastCharacterPhysics>().FirstOrDefault(x=>x.isActiveAndEnabled);
        float radius=bridge.AuthoredPlayerRadius,height=physics==null ? 0 : physics.height;
        Vector3 p=arrival.transform.position; bool supported=height==0;
        foreach(var shape in UnityEngine.Object.FindObjectsByType<CppCollision>().Where(c=>c.isActiveAndEnabled)) {
            shape.Recalculate(); Vector3 delta=p-shape.WorldCenter,half=shape.WorldSize*.5f;
            if(height>0 && Mathf.Abs(p.y-(shape.WorldCenter.y+half.y))<.02f && Mathf.Abs(delta.x)<=half.x && Mathf.Abs(delta.z)<=half.z) supported=true;
            bool vertical=height==0 || (p.y>shape.WorldCenter.y-half.y-height+.0001f && p.y<shape.WorldCenter.y+half.y-.0001f);
            bool overlap=shape.ResolvedMode==CppCollision.CollisionMode.Box ? Mathf.Abs(delta.x)<half.x+radius-.0001f && Mathf.Abs(delta.z)<half.z+radius-.0001f : delta.x*delta.x+delta.z*delta.z<(shape.Radius+radius)*(shape.Radius+radius);
            if(vertical && overlap) throw new InvalidDataException(arrival.name+": the arriving player overlaps collision on '"+shape.name+"'. Move the marker clear of it.");
        }
        if(!supported) throw new InvalidDataException(arrival.name+": place the arrival's feet on top of a Dreamcast collision box, rather than above a gap.");
    }
    [MenuItem("Dreamcast/Rooms/Add room settings")]
    static void AddRoom() { var go=new GameObject("Dreamcast Room"); Undo.RegisterCreatedObjectUndo(go,"Add room settings"); go.AddComponent<DreamcastRoom>(); Selection.activeGameObject=go; }
    [MenuItem("Dreamcast/Rooms/Add arrival marker")]
    static void AddArrival() { var go=new GameObject("Room Arrival"); Undo.RegisterCreatedObjectUndo(go,"Add arrival"); go.AddComponent<DreamcastArrival>(); Selection.activeGameObject=go; }
    [MenuItem("Dreamcast/Rooms/Add room link marker")]
    static void AddLink() { var go=new GameObject("Room Link"); Undo.RegisterCreatedObjectUndo(go,"Add room link"); go.AddComponent<DreamcastRoomLink>(); Selection.activeGameObject=go; }
    [MenuItem("Dreamcast/Rooms/Export connected rooms")]
    static void ExportCurrent() { var room=UnityEngine.Object.FindAnyObjectByType<DreamcastRoom>(); if(room==null || room.world==null) throw new InvalidDataException("Add room settings and assign a Dreamcast World first."); Export(room.world); }
    public static void Export(DreamcastWorld world)
    {
        if(Application.isPlaying) throw new InvalidOperationException("Export outside Play mode.");
        if(world==null || world.rooms.Length<1 || world.rooms.Length>8 || world.rooms.Select(r=>r.id).Distinct().Count()!=world.rooms.Length || world.rooms.Select(r=>r.scenePath).Distinct().Count()!=world.rooms.Length) throw new InvalidDataException("World needs 1-8 unique room IDs and scene paths.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) throw new OperationCanceledException("Connected-room export cancelled.");
        // Never discard edits if the user selected Don't Save.
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the scene edits before exporting connected rooms.");
        string worldPath=AssetDatabase.GetAssetPath(world); var entries=world.rooms; int startingRoom=world.startingRoom;
        var setup=EditorSceneManager.GetSceneManagerSetup();
        string stage=Path.GetFullPath("Library/DreamcastWorldExport/"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        try {
            using(var stream=new MemoryStream()) using(var w=new BinaryWriter(stream)) {
                w.Write(0x31525744u); w.Write(entries.Length); w.Write(startingRoom);
                foreach(var entry in entries.OrderBy(r=>r.id)) {
                    if(entry.id<1 || entry.id>8 || AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.scenePath)==null) throw new InvalidDataException("Each room needs ID 1-8 and a saved scene.");
                    EditorSceneManager.OpenScene(entry.scenePath,OpenSceneMode.Single);
                    world=AssetDatabase.LoadAssetAtPath<DreamcastWorld>(worldPath);
                    var settings=UnityEngine.Object.FindObjectsByType<DreamcastRoom>();
                    if(settings.Length!=1 || !settings[0].isActiveAndEnabled || settings[0].world!=world || settings[0].roomId!=entry.id) throw new InvalidDataException(entry.scenePath+": room settings do not match this world entry.");
                    string folder=Path.Combine(stage,entry.id.ToString()); RoomExporter.ExportTo(folder);
                    var arrivals=UnityEngine.Object.FindObjectsByType<DreamcastArrival>().Where(a=>a.isActiveAndEnabled).OrderBy(a=>a.arrivalName,StringComparer.Ordinal).ToArray();
                    var links=UnityEngine.Object.FindObjectsByType<DreamcastRoomLink>().Where(a=>a.isActiveAndEnabled).OrderBy(a=>a.name,StringComparer.Ordinal).ToArray();
                    w.Write(entry.id); w.Write(Hash(File.ReadAllBytes(Path.Combine(folder,"sample.room"))));
                    byte[] path=Encoding.ASCII.GetBytes(entry.scenePath); if(path.Length>=96 || entry.scenePath.Any(c=>c>126)) throw new InvalidDataException("Scene path must be under 96 ASCII characters."); w.Write(path); w.Write(new byte[96-path.Length]);
                    w.Write(arrivals.Length); w.Write(links.Length);
                    foreach(var a in arrivals) { ClearArrival(a); w.Write(Name(a.arrivalName)); Position(w,a.transform.position); w.Write(a.cameraId); }
                    foreach(var l in links) { StaticMarker(l); if(l.requiresOpenDoor && UnityEngine.Object.FindAnyObjectByType<KeyDoorPresentation>()==null) throw new InvalidDataException(l.name+": door requirement needs a key-door objective."); Position(w,l.transform.position); w.Write(l.interactionRange); w.Write(l.destinationRoom); w.Write(Name(l.destinationArrival)); w.Write(l.requiresOpenDoor ? 1 : 0); }
                }
                byte[] bytes=stream.ToArray(); var error=unity_world_configure(bytes,(uint)bytes.Length,0); unity_world_clear(); if(error!=IntPtr.Zero) throw new InvalidDataException(Marshal.PtrToStringAnsi(error));
                string target=Path.Combine(Application.streamingAssetsPath,"world"); Directory.CreateDirectory(target);
                foreach(string file in Directory.GetFiles(stage,"*",SearchOption.AllDirectories)) { string dest=Path.Combine(target,Path.GetRelativePath(stage,file)); Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(file,dest,true); }
                File.WriteAllBytes(Path.Combine(Application.streamingAssetsPath,"sample.world"),bytes);
            }
        } finally { EditorSceneManager.RestoreSceneManagerSetup(setup); AssetDatabase.Refresh(); }
        Debug.Log("Exported connected rooms. Each room keeps its own Dreamcast geometry, texture and audio budgets.");
    }
}
