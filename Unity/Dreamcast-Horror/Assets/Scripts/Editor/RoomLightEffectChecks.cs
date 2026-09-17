using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RoomLightEffectChecks
{
    const string PreviewPending="Dreamcast.LightEffectPreviewChecks";
    static DreamcastLightEffect preview;
    static Light previewLight;
    static MeshRenderer previewGlow;
    static int firstFrame;
    static float dimmest=2;
    [InitializeOnLoadMethod]
    static void Resume() { if(SessionState.GetBool(PreviewPending,false)) EditorApplication.update+=PreviewTick; }
    public static void RunPreview()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/LightingTestScene.unity");
        SessionState.SetBool(PreviewPending,true);
        SessionState.SetFloat(PreviewPending+".deadline",(float)EditorApplication.timeSinceStartup+60);
        EditorApplication.update+=PreviewTick; EditorApplication.EnterPlaymode();
    }
    static void PreviewTick()
    {
        try {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(PreviewPending+".deadline",0)) throw new Exception("Light effect Play-mode preview timed out.");
            if(!Application.isPlaying || !NativeGameBridge.IsInitialized) return;
            if(preview==null) {
                previewLight=new GameObject("Preview check light").AddComponent<Light>(); previewLight.intensity=2;
                previewGlow=GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshRenderer>();
                var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor",new Color(.8f,.4f,.2f)); previewGlow.sharedMaterial=material;
                var original=new MaterialPropertyBlock(); original.SetColor("_BaseColor",new Color(.1f,.2f,.3f));
                previewGlow.SetPropertyBlock(original,0);
                preview=previewLight.gameObject.AddComponent<DreamcastLightEffect>(); preview.enabled=false;
                preview.mode=DreamcastLightEffect.EffectMode.Pulse; preview.minimumBrightness=0; preview.frequency=2;
                preview.glowingSurfaces=new[] {previewGlow}; preview.enabled=true; firstFrame=Time.frameCount;
            }
            dimmest=Mathf.Min(dimmest,previewLight.intensity);
            if(Time.frameCount-firstFrame<20) return;
            Check(dimmest<1.8f,"Actual Light Effect LateUpdate never changed intensity.");
            var block=new MaterialPropertyBlock(); previewGlow.GetPropertyBlock(block,0);
            Color value=block.GetColor("_BaseColor");
            Check(Mathf.Abs(value.r-.8f*previewLight.intensity/2)<.001f,"Linked glow did not follow light brightness.");
            preview.enabled=false;
            Check(Mathf.Abs(previewLight.intensity-2)<.00001f,"Disabling preview failed to restore authored intensity.");
            previewGlow.GetPropertyBlock(block,0);
            Check(block.GetColor("_BaseColor")==new Color(.1f,.2f,.3f),"Disabling preview lost the original material property block.");
            Debug.Log("LIGHT EFFECT PLAY PREVIEW PASSED: actual LateUpdate, linked glow, shared native timing and restoration.");
            FinishPreview(0);
        } catch(Exception error) { Debug.LogException(error); FinishPreview(1); }
    }
    static void FinishPreview(int code)
    {
        SessionState.SetBool(PreviewPending,false); EditorApplication.update-=PreviewTick; EditorApplication.Exit(code);
    }
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static void Reject(Action action,string message)
    {
        try { action(); } catch(InvalidDataException error) { Check(error.Message.Contains(message),"Wrong diagnostic: "+error.Message); return; }
        throw new Exception("Missing rejection: "+message);
    }
    public static void Run()
    {
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var settings=new GameObject("Lighting").AddComponent<DreamcastBakedLighting>();
            settings.ambient=new Color(.1f,.1f,.1f); settings.occlusionStrength=0;
            var light=DreamcastLightTools.Create(LightType.Point,null);
            light.transform.position=new Vector3(-2,2,-2); light.range=6;
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale=new Vector3(-1,2,1);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor",Color.white); cube.GetComponent<MeshRenderer>().sharedMaterial=material;
            var on=RoomPropExporter.Build();
            var effect=light.gameObject.AddComponent<DreamcastLightEffect>();
            effect.mode=DreamcastLightEffect.EffectMode.Pulse; effect.frequency=1; effect.minimumBrightness=.2f;
            Check(Mathf.Abs(effect.SampleAt(.5f,Vector3.zero)-1)<.00001f,"Unity/native effect ABI or pulse mismatch.");
            effect.activationRange=3; effect.mode=DreamcastLightEffect.EffectMode.Steady;
            Check(effect.SampleAt(0,light.transform.position)==1 && effect.SampleAt(0,light.transform.position+Vector3.up*4)==0,"Unity/native proximity mismatch.");
            effect.activationRange=0; effect.mode=DreamcastLightEffect.EffectMode.Flicker;
            var animated=RoomPropExporter.Build();
            Check(animated.effectVertices>0 && animated.manifest.SequenceEqual(RoomPropExporter.Build().manifest),"No affected vertices or nondeterministic effect export.");
            Check(BitConverter.ToUInt32(animated.manifest,0)==0x33504344,"Effect must export DCP3.");
            // Reconstruct ON endpoint from DCP3 and compare the entire mirrored,
            // subdivided geometry with an independent ordinary static bake.
            int tail=on.manifest.Length;
            byte[] restored=animated.manifest.Take(tail).ToArray(); restored[3]=(byte)'2';
            int count=BitConverter.ToInt32(animated.manifest,tail+32);
            Check(count==animated.effectVertices,"Effect count mismatch.");
            for(int i=0;i<count;++i) {
                int vertex=BitConverter.ToInt32(animated.manifest,tail+36+i*8);
                Array.Copy(animated.manifest,tail+40+i*8,restored,24+vertex*24+20,4);
            }
            Check(restored.SequenceEqual(on.manifest),"ON colors lost mirror/subdivision vertex correspondence.");
            effect.enabled=false; light.enabled=false;
            var off=RoomPropExporter.Build();
            var baseColors=animated.manifest.Take(tail).ToArray(); baseColors[3]=(byte)'2';
            Check(baseColors.SequenceEqual(off.manifest),"OFF endpoint differs from independent unlit-source bake.");
            effect.enabled=true; Reject(()=>RoomPropExporter.Build(),"Light enabled"); light.enabled=true;
            var extra=new GameObject("Extra").AddComponent<Light>(); extra.gameObject.AddComponent<DreamcastLightEffect>();
            Reject(()=>RoomPropExporter.Build(),"one effect source"); Object.DestroyImmediate(extra.gameObject);
            effect.frequency=float.NaN; Reject(()=>RoomPropExporter.Build(),"frequency"); effect.frequency=4;
            effect.glowingSurfaces=new[] { cube.GetComponent<MeshRenderer>() };
            Reject(()=>RoomPropExporter.Build(),"Emission Color");
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",new Color(.5f,.2f,.1f));
            Check(RoomPropExporter.Build().effectVertices>animated.effectVertices,"Linked emissive faces did not animate independently of light facing.");
            var player=light.gameObject.AddComponent<CppPlayerVisual>();
            Reject(()=>RoomPropExporter.Build(),"stationary"); Object.DestroyImmediate(player);
            cube.transform.localScale=Vector3.one*8; settings.maxTriangleEdge=1;
            Reject(()=>RoomPropExporter.Build(),"2,048");
            Object.DestroyImmediate(material);
            Debug.Log("LIGHT EFFECT EXPORT CHECKS PASSED: shared native preview, DCP3, independent off/on endpoints, mirrored subdivision, linked emission, determinism, invalid source/parameters and affected-vertex budget.");
            EditorApplication.Exit(0);
        } catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
