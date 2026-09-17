using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomLightingChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var settings = new GameObject("Bake settings").AddComponent<DreamcastBakedLighting>();
            settings.ambient = Color.black;
            var light = new GameObject("Test light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = .5f; light.color = Color.white;
            var bake = RoomLightingBake.FromScene();
            Check(Mathf.Abs(bake.Sample(Vector3.zero,Vector3.back,0).r-.5f)<.001f,"Directional light direction/intensity incorrect.");
            Check(bake.Sample(Vector3.zero,Vector3.forward,0).r==0,"Back face received direct light.");
            light.intensity=4; light.color=new Color(1,.78f,.48f);
            Color bright=bake.Sample(Vector3.zero,Vector3.back,0);
            Check(Mathf.Abs(bright.r-1)<.001f && Mathf.Abs(bright.g-.78f)<.001f && Mathf.Abs(bright.b-.48f)<.001f,
                "Overbright warm light lost its hue through channel clipping.");
            light.intensity=.5f; light.color=Color.white;
            light.cullingMask=2;
            Check(bake.Sample(Vector3.zero,Vector3.back,0).r==0,"Light culling mask ignored.");
            light.cullingMask=-1; light.type=LightType.Point; light.range=4; light.transform.position=new Vector3(0,0,-2);
            Check(Mathf.Abs(bake.Sample(Vector3.zero,Vector3.back,0).r-.125f)<.001f,"Point falloff incorrect.");
            Check(bake.Sample(new Vector3(0,0,2),Vector3.back,0).r==0,"Point light escaped its range.");
            light.type=LightType.Spot; light.spotAngle=60; light.innerSpotAngle=30;
            Check(bake.Sample(Vector3.zero,Vector3.back,0).r>.12f,"Spot cone center missing.");
            Check(bake.Sample(new Vector3(2,0,-2),Vector3.left,0).r==0,"Spot cone leaked outside.");
            light.enabled=false; settings.ambient=new Color(.2f,.3f,.4f,1);
            Check(Mathf.Abs(RoomLightingBake.FromScene().Sample(Vector3.zero,Vector3.up,0).r-.2f)<.001f,"Disabled light exported.");
            light.enabled=true; light.type=LightType.Rectangle;
            bool rejected=false; try { RoomLightingBake.FromScene(); } catch(InvalidDataException e) { rejected=e.Message.Contains("area lights"); }
            Check(rejected,"Unsupported light lacks diagnostic.");
            light.type=LightType.Directional; light.transform.rotation=Quaternion.Euler(35,25,0);
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor",Color.white); cube.GetComponent<Renderer>().sharedMaterial=material;
            cube.transform.localScale=new Vector3(-2,3,4);
            settings.maxTriangleEdge=8;
            var coarse=RoomPropExporter.Build();
            Check(BitConverter.ToUInt32(coarse.manifest,0)==0x32504344 && coarse.triangles==12,"Lit primitives not exported as DCP2.");
            // Known flat face normal under mirrored, nonuniform transform must
            // agree with its exported diffuse color and corrected winding.
            var bytes=coarse.manifest;
            for(int at=24;at<bytes.Length;at+=72) {
                Vector3 Position(int p) => new Vector3(BitConverter.ToSingle(bytes,p),BitConverter.ToSingle(bytes,p+4),BitConverter.ToSingle(bytes,p+8));
                var a=Position(at); var b=Position(at+24); var c=Position(at+48);
                var normal=Vector3.Cross(b-a,c-a).normalized;
                Color32 expected=RoomLightingBake.FromScene().Sample(a,normal,0);
                Check(Math.Abs(bytes[at+20]-expected.r)<=1,"Mirrored/nonuniform normal or vertex color incorrect.");
            }
            settings.maxTriangleEdge=2;
            var fine=RoomPropExporter.Build();
            Check(fine.triangles>coarse.triangles && fine.manifest.SequenceEqual(RoomPropExporter.Build().manifest),"Subdivision/determinism failed.");
            var unlit=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            unlit.SetColor("_BaseColor",Color.white); cube.GetComponent<Renderer>().sharedMaterial=unlit;
            var bulb=RoomPropExporter.Build();
            Check(bulb.triangles==12 && bulb.manifest[44]==255 && bulb.manifest[45]==255 && bulb.manifest[46]==255,"Unlit bulb was shaded or subdivided.");
            cube.GetComponent<Renderer>().sharedMaterial=material; UnityEngine.Object.DestroyImmediate(unlit);
            var player=cube.AddComponent<CppPlayerVisual>();
            Check(RoomPropExporter.Build().triangles==0,"Moving player was frozen into static geometry.");
            UnityEngine.Object.DestroyImmediate(player);
            cube.transform.localScale=Vector3.one*100;
            rejected=false; try { RoomPropExporter.Build(); } catch(InvalidDataException e) { rejected=e.Message.Contains("4,096"); }
            Check(rejected,"Subdivision exceeded its budget without diagnostic.");
            UnityEngine.Object.DestroyImmediate(material);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            RoomExporter.Export();
            Debug.Log("LIGHTING CHECKS PASSED: directional/front-back, point range, spot cone, masks, disabled lights, unsupported lights, mirrored/nonuniform normals, subdivision, deterministic bytes and budget rejection.");
            EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
