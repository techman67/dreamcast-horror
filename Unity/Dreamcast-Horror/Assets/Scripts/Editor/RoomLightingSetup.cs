using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomLightingSetup
{
    [MenuItem("Dreamcast/Lighting/Add baked lighting and sample wall lamp")]
    public static void AddToScene()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Set up lighting outside Play mode.");
        if (UnityEngine.Object.FindAnyObjectByType<DreamcastBakedLighting>() == null) {
            var settings=new GameObject("Dreamcast Baked Lighting");
            Undo.RegisterCreatedObjectUndo(settings,"Add baked room lighting");
            settings.AddComponent<DreamcastBakedLighting>();
        }
        if (GameObject.Find("Sample Wall Lamp") != null) return;
        const string folder="Assets/Materials";
        Material Material(string name, string shader, Color color) {
            string path=folder+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) {
                mat=new Material(Shader.Find(shader));
                mat.SetColor(mat.HasProperty("_BaseColor")?"_BaseColor":"_Color",color);
                AssetDatabase.CreateAsset(mat,path);
            }
            return mat;
        }
        var metal=Material("M_Lamp_Metal","Universal Render Pipeline/Lit",new Color(.22f,.20f,.16f,1));
        var bulb=Material("M_Lamp_Bulb","Universal Render Pipeline/Unlit",new Color(1,.84f,.5f,1));
        var root=new GameObject("Sample Wall Lamp");
        Undo.RegisterCreatedObjectUndo(root,"Add sample wall lamp");
        root.transform.position=new Vector3(-1.4f,2.1f,4.7f);
        void Box(string name, Vector3 position, Vector3 size, Material mat) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
            go.transform.SetParent(root.transform,false); go.transform.localPosition=position; go.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        Box("Wall bracket",Vector3.zero,new Vector3(.26f,.4f,.12f),metal);
        Box("Frosted bulb",new Vector3(0,0,-.16f),new Vector3(.14f,.28f,.16f),bulb);
        var source=new GameObject("Warm point light"); source.transform.SetParent(root.transform,false);
        source.transform.localPosition=new Vector3(0,0,-.35f);
        var light=source.AddComponent<Light>(); light.type=LightType.Point;
        light.color=new Color(1,.78f,.48f); light.intensity=2; light.range=5; light.shadows=LightShadows.Hard;
        Selection.activeGameObject=root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Added wall lamp and baked lighting settings. Export/Rebuild CDI to bake. Save the scene to retain these settings.");
    }
    // Explicit update to the existing sample; arbitrary scene lights stay user-controlled.
    public static void ExportShadowSample()
    {
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var lamp=GameObject.Find("Sample Wall Lamp");
            if(lamp==null) throw new InvalidOperationException("Sample wall lamp is missing.");
            var light=lamp.GetComponentInChildren<Light>();
            light.shadows=LightShadows.Hard;
            EditorUtility.SetDirty(light);
            RoomExporter.Export();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("SHADOW SAMPLE EXPORTED"); EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void Run()
    {
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            AddToScene();
            RoomExporter.Export(); // Validate before saving the setup.
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
