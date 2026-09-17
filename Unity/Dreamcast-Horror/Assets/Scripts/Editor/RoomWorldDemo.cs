using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomWorldDemo
{
    [MenuItem("Dreamcast/Rooms/Create two-room example")]
    public static void Create()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Create the example outside Play mode.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string first="Assets/Scenes/ConnectedRoom01.unity", second="Assets/Scenes/ConnectedRoom02.unity", asset="Assets/Scenes/ConnectedWorld.asset";
        if(File.Exists(first)||File.Exists(second)||File.Exists(asset)) throw new InvalidOperationException("The connected-room example already exists; it will not be overwritten.");
        string source=File.Exists("Assets/Scenes/LightingTestScene.unity") ? "Assets/Scenes/LightingTestScene.unity" : "Assets/Scenes/SampleScene.unity";
        AssetDatabase.CopyAsset(source,first); AssetDatabase.CopyAsset(source,second);
        var world=ScriptableObject.CreateInstance<DreamcastWorld>(); world.rooms=new[] {new DreamcastWorld.Room{id=1,scenePath=first},new DreamcastWorld.Room{id=2,scenePath=second}}; AssetDatabase.CreateAsset(world,asset);
        for(int id=1;id<=2;id++) {
            var scene=EditorSceneManager.OpenScene(id==1 ? first : second);
            world=AssetDatabase.LoadAssetAtPath<DreamcastWorld>(asset);
            var room=new GameObject("Dreamcast Room").AddComponent<DreamcastRoom>(); room.world=world; room.roomId=id;
            var arrival=new GameObject("Arrival - Entrance").AddComponent<DreamcastArrival>(); arrival.transform.position=new Vector3(0,0,0);
            var link=new GameObject("Link - other room").AddComponent<DreamcastRoomLink>(); link.destinationRoom=id==1 ? 2 : 1; link.transform.position=new Vector3(1.3f,0,0); link.interactionRange=1;
            var sign=GameObject.CreatePrimitive(PrimitiveType.Cube); sign.name=id==1 ? "Blue room link marker" : "Amber room link marker"; sign.transform.position=new Vector3(1.3f,.35f,0); sign.transform.localScale=new Vector3(.2f,.7f,.2f); UnityEngine.Object.DestroyImmediate(sign.GetComponent<Collider>());
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=id==1 ? new Color(.15f,.45f,.8f) : new Color(.8f,.4f,.1f); string materialPath=$"Assets/Materials/M_WorldLink_{id}.mat"; AssetDatabase.CreateAsset(material,materialPath); sign.GetComponent<Renderer>().sharedMaterial=material;
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets(); world=AssetDatabase.LoadAssetAtPath<DreamcastWorld>(asset); RoomWorldExporter.Export(world); EditorSceneManager.OpenScene(first);
        Selection.activeObject=world;
        Debug.Log("Two-room example ready. Walk toward the coloured marker and press E / A to change rooms. The original lighting scene was preserved.");
    }
    public static void BuildCheck() { try { if(File.Exists("Assets/Scenes/ConnectedWorld.asset")) { var world=AssetDatabase.LoadAssetAtPath<DreamcastWorld>("Assets/Scenes/ConnectedWorld.asset"); foreach(var entry in world.rooms) { var scene=EditorSceneManager.OpenScene(entry.scenePath); var settings=UnityEngine.Object.FindAnyObjectByType<DreamcastRoom>(); settings.world=AssetDatabase.LoadAssetAtPath<DreamcastWorld>("Assets/Scenes/ConnectedWorld.asset"); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); } RoomWorldExporter.Export(AssetDatabase.LoadAssetAtPath<DreamcastWorld>("Assets/Scenes/ConnectedWorld.asset")); } else Create(); EditorApplication.Exit(0); } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); } }
}
