using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomShadowPlacementChecks
{
    public static void ApplyIndoorFill()
    {
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/LightingTestScene.unity");
            var settings=UnityEngine.Object.FindAnyObjectByType<DreamcastBakedLighting>();
            var ambient=settings.ambient; float ao=settings.occlusionStrength;
            Selection.activeGameObject=GameObject.Find("Directional Light");
            DreamcastLightTools.UseDirectionalFill();
            if (Selection.activeGameObject.GetComponent<Light>().shadows!=LightShadows.None || settings.ambient!=ambient || settings.occlusionStrength!=ao)
                throw new Exception("Indoor fill changed unrelated lighting settings.");
            RoomExporter.Export();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("INDOOR FILL EXPORTED: user ambient/AO retained; only directional shadow casting changed.");
            EditorApplication.Exit(0);
        } catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    public static void Diagnose()
    {
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/LightingTestScene.unity");
            string path=Path.GetFullPath("../../Simulant/build");
            var settings=UnityEngine.Object.FindAnyObjectByType<DreamcastBakedLighting>();
            Debug.Log($"SHADOW DIAGNOSTIC: ambient={settings.ambient}, AO={settings.occlusionStrength}, edge={settings.maxTriangleEdge}");
            var baseline=RoomPropExporter.Build();
            File.WriteAllBytes(Path.Combine(path,"shadow-baseline.props"),baseline.manifest);
            var sun=GameObject.Find("Directional Light").GetComponent<Light>();
            sun.shadows=LightShadows.None;
            var noSunShadow=RoomPropExporter.Build();
            File.WriteAllBytes(Path.Combine(path,"shadow-no-sun-shadow.props"),noSunShadow.manifest);
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>()) light.shadows=LightShadows.None;
            File.WriteAllBytes(Path.Combine(path,"shadow-no-shadows.props"),RoomPropExporter.Build().manifest);
            Debug.Log("SHADOW DIAGNOSTIC EXPORTED: comparisons only; scene and active export unchanged.");
            EditorApplication.Exit(0);
        } catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
