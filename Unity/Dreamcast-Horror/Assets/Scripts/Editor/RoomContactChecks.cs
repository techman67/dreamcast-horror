using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomContactChecks
{
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    [MenuItem("Dreamcast/Lighting/Add contact shadows to selected floor")]
    public static void Add()
    {
        var obj=Selection.activeGameObject;
        if(Application.isPlaying || obj==null || !obj.scene.IsValid() || obj.GetComponent<MeshFilter>()==null || obj.GetComponent<MeshRenderer>()==null) {
            EditorUtility.DisplayDialog("Contact shadows","Select a stationary floor mesh in the scene outside Play mode.","OK"); return;
        }
        if(obj.GetComponent<DreamcastContactSurface>()==null) Undo.AddComponent<DreamcastContactSurface>(obj);
        Selection.activeGameObject=obj;
    }
    public static void Run()
    {
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var settings=new GameObject("Lighting").AddComponent<DreamcastBakedLighting>();
            settings.ambient=Color.white; settings.occlusionStrength=0; settings.bakeShadows=false;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.SetColor("_BaseColor",Color.white);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Floor";
            floor.transform.position=new Vector3(0,-.1f,0); floor.transform.localScale=new Vector3(4,.2f,4);
            floor.GetComponent<MeshRenderer>().sharedMaterial=mat;
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube); block.transform.position=new Vector3(0,.5f,0);
            block.GetComponent<MeshRenderer>().sharedMaterial=mat;
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>()); UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            var original=RoomPropExporter.Build();
            var contact=floor.AddComponent<DreamcastContactSurface>(); contact.resolution=64;
            var bake=RoomLightingBake.FromScene();
            var map=RoomContactBake.Create(floor.GetComponent<MeshRenderer>(),0,mat,null,bake);
            float Red(Vector3 p) { var uv=map.UV(p); int x=Mathf.Clamp(Mathf.FloorToInt(uv.x*64),0,63),y=Mathf.Clamp(Mathf.FloorToInt(uv.y*64),0,63); return (BitConverter.ToUInt16(map.Texture,12+(y*64+x)*2)>>11)/31f; }
            Check(Red(new Vector3(.55f,0,0))<Red(new Vector3(1.6f,0,0))-.1f,"Contact edge is not darker than open floor.");
            var bundle=RoomPropExporter.Build();
            Check(bundle.triangles==original.triangles && bundle.parts==original.parts+1,"Contact mapping changed triangles or failed to split top faces.");
            Check(bundle.textureBytes==8192 && bundle.textures.Count==1,"Contact texture budget incorrect.");
            Check(bundle.manifest.SequenceEqual(RoomPropExporter.Build().manifest),"Contact export is nondeterministic.");
            block.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var clear=RoomContactBake.Create(floor.GetComponent<MeshRenderer>(),0,mat,null,RoomLightingBake.FromScene());
            Check(clear.Texture.Skip(12).All(b=>b==255),"Disabled caster still darkens contact map.");
            // Existing base color survives contact baking, including source UVs.
            var source=new byte[12+8*8*2]; Array.Copy(BitConverter.GetBytes(0x31544344u),source,4);
            Array.Copy(BitConverter.GetBytes(8),0,source,4,4); Array.Copy(BitConverter.GetBytes(8),0,source,8,4);
            for(int i=12;i<source.Length;i+=2) { source[i]=0xe0; source[i+1]=7; }
            var green=RoomContactBake.Create(floor.GetComponent<MeshRenderer>(),0,mat,source,RoomLightingBake.FromScene());
            for(int i=12;i<green.Texture.Length;i+=2) Check(BitConverter.ToUInt16(green.Texture,i)==0x07e0,"Contact baking lost source texture color.");
            // A mirrored receiver with a pulse still exports aligned off/on endpoints.
            floor.transform.localScale=new Vector3(-4,.2f,4);
            var lamp=new GameObject("Pulse").AddComponent<Light>(); lamp.type=LightType.Point; lamp.range=4; lamp.intensity=1; lamp.transform.position=new Vector3(0,2,0);
            var effect=lamp.gameObject.AddComponent<DreamcastLightEffect>(); effect.mode=DreamcastLightEffect.EffectMode.Pulse;
            settings.ambient=new Color(.1f,.1f,.1f);
            var animated=RoomPropExporter.Build();
            Check(BitConverter.ToUInt32(animated.manifest,0)==0x33504344 && animated.effectVertices>0,"Contact split lost light effect colors.");
            UnityEngine.Object.DestroyImmediate(lamp.gameObject);
            contact.resolution=65; bool rejected=false;
            try { RoomPropExporter.Build(); } catch(InvalidDataException e) { rejected=e.Message.Contains("resolution"); }
            Check(rejected,"Invalid contact resolution lacked diagnostic.");
            contact.resolution=64; contact.strength=0;
            Check(RoomPropExporter.Build().textures.Count==0,"Zero strength retained contact texture.");
            UnityEngine.Object.DestroyImmediate(block);
            var plane=GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.GetComponent<MeshFilter>().sharedMesh=plane.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(plane);
            contact.strength=.6f;
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AirConditioner/ac-painted-metal.png"));
            var replaced=RoomPropExporter.Build();
            Check(replaced.textures.Count==1 && replaced.textureBytes==8192,"Unused original texture retained after planar contact replacement.");
            floor.transform.rotation=Quaternion.Euler(30,0,0); rejected=false;
            try { RoomPropExporter.Build(); } catch(InvalidDataException e) { rejected=e.Message.Contains("horizontal"); }
            Check(rejected,"Sloped receiver silently ignored.");
            Debug.Log("CONTACT CHECKS PASSED: mesh-only contact detail, topology, budgets, determinism, disabled casters, validation."); EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void UpgradeDemo()
    {
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/LightingTestScene.unity");
            var floor=GameObject.Find("Floor");
            if(floor==null) throw new Exception("Lighting test Floor not found.");
            var contact=floor.GetComponent<DreamcastContactSurface>() ?? floor.AddComponent<DreamcastContactSurface>();
            contact.resolution=256; contact.strength=.7f; contact.distance=.6f; contact.samples=32;
            var fill=GameObject.Find("Directional Light").GetComponent<Light>(); fill.intensity=.3f; fill.shadows=LightShadows.None;
            EditorUtility.SetDirty(contact); EditorUtility.SetDirty(fill);
            RoomExporter.Export(); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("CONTACT DEMO EXPORTED: floor contact map; directional fill 0.3; authored ambient retained."); EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
