using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Reproducible graybox fixture, not a production art pipeline.
public static class RoomPhysicsSetup
{
    public const string ScenePath = "Assets/Scenes/PhysicsTestScene.unity";
    static GameObject Box(string name, Vector3 center, Vector3 size, string material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name; obj.transform.position = center; obj.transform.localScale = size;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + material + ".mat");
        obj.AddComponent<CppCollision>().Recalculate();
        return obj;
    }
    [MenuItem("Dreamcast/Open stairs test room")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) Create();
        else EditorSceneManager.OpenScene(ScenePath);
    }
    static void Create()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Native Game Bridge").AddComponent<NativeGameBridge>();
        var settings = new GameObject("Baked lighting").AddComponent<DreamcastBakedLighting>();
        settings.ambient = new Color(.35f,.35f,.35f); settings.maxTriangleEdge = 3;
        var light = new GameObject("Test sunlight").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = .8f;
        light.transform.rotation = Quaternion.Euler(45,-30,0);
        Box("Floor",new Vector3(0,-.1f,1),new Vector3(12,.2f,14),"M_TestRoom_Floor");
        for (int i=0;i<6;++i)
            Box("Stair_"+i,new Vector3(0,.125f*(i+1),.5f+i*.5f),new Vector3(2,.25f*(i+1),.5f),"M_TestRoom_Crate");
        Box("Upper landing",new Vector3(0,1.4f,4),new Vector3(2,.2f,2),"M_TestRoom_Crate");
        Box("Tall corner obstacle",new Vector3(-3,1.5f,0),new Vector3(1.5f,3,1.5f),"M_TestRoom_Crate");
        Box("Too tall step - 40 cm",new Vector3(3,.2f,0),new Vector3(1.5f,.4f,1),"M_TestRoom_Crate");
        Box("Low ceiling",new Vector3(3,1.7f,3),new Vector3(2,.2f,2),"M_TestRoom_Crate");
        Box("Back wall",new Vector3(0,2,7.9f),new Vector3(12,4,.2f),"M_TestRoom_Floor");
        Box("Left wall",new Vector3(-5.9f,2,1),new Vector3(.2f,4,14),"M_TestRoom_Floor");
        Box("Right wall",new Vector3(5.9f,2,1),new Vector3(.2f,4,14),"M_TestRoom_Floor");
        Box("Front wall",new Vector3(0,.5f,-5.9f),new Vector3(12,1,.2f),"M_TestRoom_Floor");
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player"; player.transform.position = new Vector3(0,.9f,-1);
        player.transform.localScale = new Vector3(.7f,.9f,.7f);
        Object.DestroyImmediate(player.GetComponent<Collider>());
        player.AddComponent<CppPlayerVisual>(); player.AddComponent<DreamcastCharacterPhysics>();
        var cue=player.AddComponent<DreamcastSoundCue>(); cue.cue=DreamcastCue.Footstep;
        cue.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/footstep.wav");
        for(int i=1;i<=2;++i) {
            var camera=new GameObject("GameplayCamera_0"+i).AddComponent<Camera>();
            camera.transform.position=new Vector3(7,8,-8);
            camera.transform.LookAt(new Vector3(0,1,2)); camera.fieldOfView=65;
            camera.enabled=i==1;
            if(i==1) camera.gameObject.AddComponent<AudioListener>();
        }
        var zone=new GameObject("CameraZone_01_To_02");
        zone.transform.position=new Vector3(0,0,2); zone.transform.localScale=new Vector3(12,1,.1f);
        if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath))
            throw new IOException("Could not save the stairs test scene.");
        AssetDatabase.SaveAssets();
    }
    public static void BuildAndCheck()
    {
        try {
            if (!File.Exists(ScenePath)) Create();
            else EditorSceneManager.OpenScene(ScenePath);
            RoomPhysicsChecks.CheckScene();
            RoomExporter.Export();
            Debug.Log("STAIRS TEST SCENE EXPORTED AND CHECKED");
            EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
