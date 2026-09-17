using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Authoring + approximate Unity preview. Wave/proximity rules run in shared C++.
[DisallowMultipleComponent, RequireComponent(typeof(Light))]
[AddComponentMenu("Dreamcast/Light Effect")]
public sealed class DreamcastLightEffect : MonoBehaviour
{
    public enum EffectMode { Steady, Pulse, Flicker }
    public EffectMode mode = EffectMode.Flicker;
    [Range(.1f,10), Tooltip("Pulse cycles / flicker changes per second. Dreamcast color updates are capped at 20 Hz.")]
    public float frequency = 4;
    [Range(0,1), Tooltip("Dim end of the effect. 0 allows complete darkness; 1 stays fully lit.")]
    public float minimumBrightness = .2f;
    [Range(0,100), Tooltip("0 = always active. Otherwise fades on as the player's feet approach this light, over the inner half metre.")]
    public float activationRange;
    [Range(0,65535), Tooltip("Repeatable variation for flicker. Reset restarts the same sequence.")]
    public int seed = 1;
    [Tooltip("Optional static bulb/screen renderers to dim with the light. Use an Unlit material or enable Emission Color on each material.")]
    public MeshRenderer[] glowingSurfaces = new MeshRenderer[0];
    [StructLayout(LayoutKind.Sequential)]
    public struct Settings {
        public uint mode; public float frequency, minimum, activationRange;
        public Vector3 position; public uint seed;
    }
    [DllImport("dreamcast_horror", CallingConvention=CallingConvention.Cdecl)]
    public static extern float unity_light_effect_advance(float phase, float dt, float frequency);
    [DllImport("dreamcast_horror", CallingConvention=CallingConvention.Cdecl)]
    static extern float unity_light_effect_sample(ref Settings settings, float phase, float x, float y, float z);
    public float SampleAt(float phase, Vector3 listener)
    {
        var settings = new Settings { mode=(uint)mode, frequency=frequency, minimum=minimumBrightness,
            activationRange=activationRange, position=transform.position, seed=(uint)seed };
        return unity_light_effect_sample(ref settings,phase,listener.x,listener.y,listener.z);
    }
    sealed class Glow {
        public MeshRenderer renderer; public int slot, property; public Color color;
        public MaterialPropertyBlock original, working;
    }
    readonly List<Glow> previewGlows = new List<Glow>();
    Light source; CppPlayerVisual player; float authoredIntensity, phase; bool captured;
    void OnEnable()
    {
        if (!Application.isPlaying) return;
        source=GetComponent<Light>(); authoredIntensity=source.intensity; captured=true; phase=0;
        player=FindAnyObjectByType<CppPlayerVisual>();
        foreach (var renderer in glowingSurfaces) {
            if (renderer==null) continue;
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;++i) {
                var material=materials[i]; if(material==null) continue;
                string property=material.shader.name.Contains("Unlit") ? (material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color") : "_EmissionColor";
                if (!material.HasProperty(property)) continue;
                var glow=new Glow { renderer=renderer,slot=i,property=Shader.PropertyToID(property),color=material.GetColor(property),
                    original=new MaterialPropertyBlock(),working=new MaterialPropertyBlock() };
                renderer.GetPropertyBlock(glow.original,i); renderer.GetPropertyBlock(glow.working,i);
                previewGlows.Add(glow);
            }
        }
    }
    void LateUpdate()
    {
        if(!captured) return;
        phase=unity_light_effect_advance(phase,Time.deltaTime,frequency);
        float amount=activationRange>0 && player==null ? 0 : SampleAt(phase,player!=null ? player.AuthoredSpawnPosition : Vector3.zero);
        source.intensity=authoredIntensity*amount;
        foreach(var glow in previewGlows) if(glow.renderer!=null) {
            var color=glow.color*amount; color.a=glow.color.a;
            glow.working.SetColor(glow.property,color); glow.renderer.SetPropertyBlock(glow.working,glow.slot);
        }
    }
    void OnDisable()
    {
        if(!captured) return;
        if(source!=null) source.intensity=authoredIntensity;
        foreach(var glow in previewGlows) if(glow.renderer!=null) glow.renderer.SetPropertyBlock(glow.original,glow.slot);
        previewGlows.Clear(); captured=false;
    }
    void OnDrawGizmosSelected()
    {
        if(activationRange<=0) return;
        Gizmos.color=Color.cyan; Gizmos.DrawWireSphere(transform.position,activationRange);
    }
}
