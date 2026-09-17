using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomLightingDemo
{
    const string ScenePath = "Assets/Scenes/LightingTestScene.unity";
    [MenuItem("Dreamcast/Lighting/Open lighting test room")]
    public static void Open()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Leave Play mode before opening the lighting test room.");
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }
    // Explicit batch fixture generation. Original SampleScene is not saved/changed.
    public static void BuildAndExport()
    {
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Material Make(string name, Color baseColor, Color emission)
            {
                string path = "Assets/Materials/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material,path);
                }
                material.SetColor("_BaseColor", baseColor);
                material.SetColor("_EmissionColor", emission);
                // URP's material validator clears the keyword when its default
                // EmissiveIsBlack flag remains set, even for a nonblack color.
                material.globalIlluminationFlags = emission.maxColorComponent > 0
                    ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                if (emission.maxColorComponent > 0) material.EnableKeyword("_EMISSION");
                else material.DisableKeyword("_EMISSION");
                EditorUtility.SetDirty(material);
                return material;
            }
            var frame = Make("M_LightingTest_TVFrame", new Color(.08f,.08f,.1f), Color.black);
            var screen = Make("M_LightingTest_TVScreen", Color.black, new Color(.3f,.5f,.8f));
            var fixture = new GameObject("Lighting test television");
            fixture.transform.position = new Vector3(1.5f,1.7f,4.65f);
            void Box(string name, Vector3 offset, Vector3 size, Material material)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
                obj.transform.SetParent(fixture.transform,false); obj.transform.localPosition = offset; obj.transform.localScale = size;
                UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
                obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            Box("TV frame",Vector3.zero,new Vector3(1.4f,.9f,.14f),frame);
            Box("Emissive screen",new Vector3(0,0,-.09f),new Vector3(1.2f,.7f,.04f),screen);
            var light = DreamcastLightTools.Create(LightType.Rectangle,fixture.transform);
            light.name = "TV area spill"; light.transform.localPosition = new Vector3(0,0,-.15f);
            light.transform.localRotation = Quaternion.Euler(0,180,0);
            light.areaSize = new Vector2(1.2f,.7f); light.color = new Color(.45f,.65f,1);
            light.intensity = 2; light.range = 4;
            ConfigureEffect(light);
            RoomExporter.Export();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("LIGHTING DEMO EXPORTED: original sample retained; separate TV area/emission scene saved.");
            EditorApplication.Exit(0);
        } catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void ConfigureEffect(Light light)
    {
        // The sample's directional source is indoor fill, not outdoor sun.
        // Keep actual fixture shadows without baking wall shadows across the floor.
        var fill=GameObject.Find("Directional Light")?.GetComponent<Light>();
        if(fill!=null && fill.type==LightType.Directional) { fill.shadows=LightShadows.None; EditorUtility.SetDirty(fill); }
        var effect = light.GetComponent<DreamcastLightEffect>() ?? light.gameObject.AddComponent<DreamcastLightEffect>();
        effect.mode=DreamcastLightEffect.EffectMode.Flicker; effect.frequency=4; effect.minimumBrightness=.15f; effect.seed=7;
        effect.glowingSurfaces=new[] { light.transform.parent.Find("Emissive screen").GetComponent<MeshRenderer>() };
        EditorUtility.SetDirty(effect);
        DreamcastLightTools.SofterCorners();
    }
    public static void UpgradeEffectsDemo()
    {
        try {
            EditorSceneManager.OpenScene(ScenePath);
            var light=GameObject.Find("TV area spill").GetComponent<Light>();
            ConfigureEffect(light);
            RoomExporter.Export();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("LIGHT EFFECT DEMO EXPORTED: flickering TV and softer AO corners."); EditorApplication.Exit(0);
        } catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
