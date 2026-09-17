using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class RoomShadowChecks
{
    static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
    public static void Run()
    {
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var settings=new GameObject("Lighting").AddComponent<DreamcastBakedLighting>();
            settings.ambient=Color.black; settings.occlusionStrength=0;
            var light=new GameObject("Point light").AddComponent<Light>();
            light.type=LightType.Point; light.transform.position=Vector3.up*4;
            light.range=8; light.intensity=1; light.shadows=LightShadows.Hard;
            float Sample(bool receive=true) => RoomLightingBake.FromScene().Sample(Vector3.zero,Vector3.up,0,receive).r;
            Check(Mathf.Abs(Sample()-.25f)<.001f,"Unblocked light changed.");
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(blocker.GetComponent<Collider>());
            blocker.transform.position=Vector3.up*2; blocker.transform.localScale=new Vector3(2,.05f,2);
            var renderer=blocker.GetComponent<MeshRenderer>();
            Check(Sample()<.001f,"Static mesh failed to block point light without a collider.");
            Check(Mathf.Abs(Sample(false)-.25f)<.001f,"Receive Shadows off ignored.");
            light.shadowStrength=.5f; Check(Mathf.Abs(Sample()-.125f)<.001f,"Shadow strength ignored.");
            light.shadowStrength=1; light.shadows=LightShadows.None;
            Check(Mathf.Abs(Sample()-.25f)<.001f,"Light Shadows None ignored.");
            light.shadows=LightShadows.Hard; settings.bakeShadows=false;
            Check(Mathf.Abs(Sample()-.25f)<.001f,"Room shadow switch ignored.");
            settings.bakeShadows=true; renderer.shadowCastingMode=ShadowCastingMode.Off;
            Check(Mathf.Abs(Sample()-.25f)<.001f,"Cast Shadows Off ignored.");
            renderer.shadowCastingMode=ShadowCastingMode.On; blocker.layer=7; light.cullingMask=1;
            Check(Mathf.Abs(Sample()-.25f)<.001f,"Caster layer mask ignored.");
            blocker.layer=0; renderer.enabled=false;
            Check(Mathf.Abs(Sample()-.25f)<.001f,"Disabled caster blocks light.");
            renderer.enabled=true; blocker.transform.position=Vector3.up*6;
            Check(Mathf.Abs(Sample()-.25f)<.001f,"Geometry beyond light blocks it.");
            blocker.transform.position=Vector3.up*2; blocker.transform.localScale=new Vector3(-2,.05f,2);
            Check(Sample()<.001f,"Mirrored caster leaked light.");
            light.type=LightType.Directional; light.transform.rotation=Quaternion.Euler(90,0,0);
            Check(Sample()<.001f,"Directional shadow failed.");
            light.type=LightType.Spot; light.spotAngle=60; light.innerSpotAngle=30;
            Check(Sample()<.001f,"Spot shadow failed.");
            var player=blocker.AddComponent<CppPlayerVisual>();
            Check(Sample()>.24f,"Moving player cast a permanent shadow.");
            Object.DestroyImmediate(player);
            // Ambient occlusion works independently of direct light shadow settings.
            light.enabled=false; settings.ambient=Color.white; settings.occlusionStrength=1; settings.occlusionDistance=1;
            blocker.transform.position=Vector3.up*.4f; blocker.transform.localScale=new Vector3(4,.05f,4);
            Check(Sample()<.5f,"Nearby overhead geometry did not occlude ambient light.");
            Check(Mathf.Abs(Sample(false)-1)<.001f,"Receiver opt-out failed for AO.");
            blocker.transform.position=Vector3.up*3;
            Check(Mathf.Abs(Sample()-1)<.001f,"AO exceeded its distance.");
            // Surface origin offset prevents self-shadowing on an exposed face.
            settings.occlusionStrength=0; light.enabled=true; light.type=LightType.Point;
            blocker.transform.position=Vector3.zero; blocker.transform.localScale=Vector3.one;
            var bake=RoomLightingBake.FromScene();
            Check(bake.Sample(Vector3.up*.5f,Vector3.up,0).r>.2f,"Exposed face self-shadowed.");
            renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
            Check(RoomPropExporter.Build().triangles==0,"Shadows Only caster appeared as visible geometry.");
            // Verify the renderer receiver switch affects exported vertex colors.
            settings.ambient=Color.black; blocker.transform.position=Vector3.up*2;
            blocker.transform.localScale=new Vector3(3,.1f,3);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.localScale=new Vector3(1,.1f,1);
            var floorRenderer=floor.GetComponent<MeshRenderer>();
            var shadowed=RoomPropExporter.Build().manifest;
            Check(shadowed.SequenceEqual(RoomPropExporter.Build().manifest),"Shadow export is nondeterministic.");
            floorRenderer.receiveShadows=false;
            Check(!shadowed.SequenceEqual(RoomPropExporter.Build().manifest),"Receive Shadows did not affect exported colors.");
            Object.DestroyImmediate(floor);
            settings.shadowBias=float.NaN;
            bool rejected=false; try { RoomLightingBake.FromScene(); } catch(InvalidDataException) { rejected=true; }
            Check(rejected,"Invalid shadow settings accepted.");
            Debug.Log("BAKED SHADOW CHECKS PASSED: point/spot/directional, strength, masks, switches, thin/mirrored casters, no colliders, dynamic exclusion, AO and self-shadow bias.");
            EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
