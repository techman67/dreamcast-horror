using System;
using UnityEditor;
using UnityEngine;

// Editor-only helpers: all authored lighting becomes portable vertex colors.
public sealed class DreamcastLightTools : EditorWindow
{
    string report;
    MessageType reportType;
    Vector2 scroll;
    [MenuItem("Dreamcast/Lighting/Light tools")]
    public static void Open() => GetWindow<DreamcastLightTools>("Dreamcast lights");
    [MenuItem("Dreamcast/Lighting/Use selected directional light as fill")]
    public static void UseDirectionalFill()
    {
        var light=Selection.activeGameObject!=null ? Selection.activeGameObject.GetComponent<Light>() : null;
        if (Application.isPlaying || light==null || light.type!=LightType.Directional || EditorUtility.IsPersistent(light)) {
            EditorUtility.DisplayDialog("Select a directional scene light", "Outside Play mode, select a Directional Light in the scene hierarchy. This command keeps its illumination but disables its shadows for indoor fill.", "OK"); return;
        }
        Undo.RecordObject(light,"Use directional fill light");
        light.shadows=LightShadows.None;
        EditorUtility.SetDirty(light); PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(light.gameObject.scene);
        Debug.Log("Directional fill: this light no longer casts wall/pillar shadows. Color, intensity, other lights and ambient settings are unchanged. Save and rebuild CDI.");
    }
    [MenuItem("Dreamcast/Lighting/Add effect to selected light")]
    public static void AddEffect()
    {
        var light = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Light>() : null;
        if (Application.isPlaying || light == null || EditorUtility.IsPersistent(light)) {
            EditorUtility.DisplayDialog("Select a scene light", "Outside Play mode, select a Light in the scene hierarchy first.", "OK"); return;
        }
        if (light.GetComponent<DreamcastLightEffect>() == null) Undo.AddComponent<DreamcastLightEffect>(light.gameObject);
        Open();
    }
    [MenuItem("Dreamcast/Lighting/Softer corners preset")]
    public static void SofterCorners()
    {
        var settings = UnityEngine.Object.FindAnyObjectByType<DreamcastBakedLighting>();
        if (Application.isPlaying || settings == null) {
            EditorUtility.DisplayDialog("Room lighting settings", "Outside Play mode, add room lighting first using Dreamcast > Lighting > Light tools.", "OK"); return;
        }
        Undo.RecordObject(settings,"Soften baked corners");
        settings.occlusionStrength=.18f; settings.occlusionDistance=.4f; settings.occlusionSamples=24;
        EditorUtility.SetDirty(settings); PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
        Selection.activeGameObject=settings.gameObject;
        Debug.Log("Softer corners: reduced AO strength/radius and increased samples. Save and rebuild CDI to see the change. Triangle density is unchanged.");
    }
    void OnSelectionChange() => Repaint();
    public static Light Create(LightType type, Transform parent)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Create authored lights outside Play mode.");
        if (UnityEngine.Object.FindAnyObjectByType<DreamcastBakedLighting>() == null) {
            var settings = new GameObject("Dreamcast Baked Lighting");
            Undo.RegisterCreatedObjectUndo(settings, "Add room lighting");
            Undo.AddComponent<DreamcastBakedLighting>(settings);
        }
        var source = new GameObject("Dreamcast " + type + " light");
        Undo.RegisterCreatedObjectUndo(source, "Add light");
        if (parent != null) source.transform.SetParent(parent, false);
        var light = source.AddComponent<Light>();
        light.type = type; light.color = new Color(1, .78f, .48f);
        light.intensity = 1; light.range = 5; light.shadows = LightShadows.Hard;
        light.areaSize = new Vector2(1, .6f);
        if (RoomLightingBake.IsArea(light)) light.lightmapBakeType = LightmapBakeType.Baked;
        return light;
    }
    void Add(LightType type)
    {
        var light = Create(type, Selection.activeTransform);
        Selection.activeGameObject = light.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(light.gameObject.scene);
    }
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Select a fixture and add a child light. Place the source just outside opaque geometry. Export / rebuild to apply changes. Sources stay stationary; one optional Light Effect can animate brightness with baked shadows.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying)) {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Point / bulb")) Add(LightType.Point);
            if (GUILayout.Button("Spot / beam")) Add(LightType.Spot);
            if (GUILayout.Button("Directional")) Add(LightType.Directional);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rectangle / TV / window")) Add(LightType.Rectangle);
            if (GUILayout.Button("Disc / ceiling panel")) Add(LightType.Disc);
            EditorGUILayout.EndHorizontal();
            var light = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Light>() : null;
            if (light != null) {
                EditorGUILayout.Space(); EditorGUILayout.LabelField(light.name, EditorStyles.boldLabel);
                if (light.type==LightType.Directional && light.shadows!=LightShadows.None) {
                    EditorGUILayout.HelpBox("This directional light casts sun-like shadows from walls and pillars, even indoors. For general room fill, disable its shadows. Other lights can keep theirs.",MessageType.Info);
                    if (GUILayout.Button("Use as room fill (no shadows)")) UseDirectionalFill();
                }
                EditorGUI.BeginChangeCheck();
                bool enabled = EditorGUILayout.Toggle("Enabled", light.enabled);
                Color color = EditorGUILayout.ColorField("Color", light.color);
                float intensity = EditorGUILayout.Slider("Intensity", light.intensity, 0, 8);
                float range = light.range;
                if (light.type != LightType.Directional) range = EditorGUILayout.Slider("Range (metres)", range, .01f, 100);
                Vector2 size = light.areaSize;
                if (RoomLightingBake.IsArea(light)) {
                    EditorGUILayout.HelpBox("Emits along blue +Z. Dimensions ignore transform scale. Area Samples on room lighting controls export quality. Use a separate visible mesh for the glowing panel.", MessageType.Info);
                    size.x = EditorGUILayout.Slider(light.type == LightType.Disc ? "Radius (metres)" : "Width (metres)", size.x, .01f, 20);
                    if (light.type == LightType.Rectangle) size.y = EditorGUILayout.Slider("Height (metres)", size.y, .01f, 20);
                }
                float outer = light.spotAngle, inner = light.innerSpotAngle;
                if (light.type == LightType.Spot) {
                    outer = EditorGUILayout.Slider("Outer cone", outer, 1, 179);
                    inner = EditorGUILayout.Slider("Inner cone", inner, 0, outer);
                }
                bool shadows = EditorGUILayout.Toggle("Bake shadows", light.shadows != LightShadows.None);
                float strength = EditorGUILayout.Slider("Shadow strength", light.shadowStrength, 0, 1);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(light, "Edit Dreamcast light");
                    light.enabled = enabled; light.color = color; light.intensity = intensity; light.range = range;
                    light.areaSize = size; light.spotAngle = outer; light.innerSpotAngle = inner;
                    light.shadows = shadows ? LightShadows.Hard : LightShadows.None; light.shadowStrength = strength;
                    EditorUtility.SetDirty(light); PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(light.gameObject.scene);
                    SceneView.RepaintAll();
                }
                var effect = light.GetComponent<DreamcastLightEffect>();
                if (effect == null) { if (GUILayout.Button("Add flicker / pulse / proximity effect")) AddEffect(); }
                else {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("One effect source per room; at most 2,048 affected vertices. Activation Range 0 keeps it active; a positive range fades it on near the player. Glowing Surfaces can link the bulb or screen.", MessageType.Info);
                    var serialized = new SerializedObject(effect); serialized.Update();
                    foreach (string field in new[] { "m_Enabled", "mode", "frequency", "minimumBrightness", "activationRange", "seed", "glowingSurfaces" })
                        EditorGUILayout.PropertyField(serialized.FindProperty(field),true);
                    serialized.ApplyModifiedProperties();
                }
            } else EditorGUILayout.LabelField("Select a Light to edit its export settings here.");
            EditorGUILayout.Space();
            if (GUILayout.Button("Check room lighting and geometry budget")) {
                try {
                    var bundle = RoomPropExporter.Build();
                    report = $"Valid: {bundle.triangles:N0} / 4,096 triangles; {bundle.parts} / 32 material parts; {bundle.textureBytes / 1024} / 512 KiB textures; {bundle.manifest.Length:N0} / 300,000 geometry bytes; {bundle.effectVertices:N0} / 2,048 effect vertices. This check does not export or save the scene.";
                    if (bundle.manifest.Length > 300000) throw new InvalidOperationException("Geometry exceeds 300,000 bytes. Reduce triangles or materials.");
                    reportType = MessageType.Info;
                } catch (Exception error) { report = error.Message; reportType = MessageType.Error; }
            }
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, reportType);
        }
        EditorGUILayout.EndScrollView();
    }
    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    static void DrawArea(Light light, GizmoType unused)
    {
        if (!RoomLightingBake.IsArea(light)) return;
        var old = Gizmos.matrix; var color = Gizmos.color;
        Gizmos.matrix = Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one);
        Gizmos.color = light.color;
        if (light.type == LightType.Rectangle) Gizmos.DrawWireCube(Vector3.zero, new Vector3(light.areaSize.x, light.areaSize.y, 0));
        else {
            for (int i = 0; i < 32; ++i) {
                float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16;
                Gizmos.DrawLine(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * light.areaSize.x,
                    new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0) * light.areaSize.x);
            }
        }
        Gizmos.DrawRay(Vector3.zero, Vector3.forward * .75f);
        Gizmos.DrawLine(new Vector3(-.1f, 0, .55f), new Vector3(0, 0, .75f));
        Gizmos.DrawLine(new Vector3(.1f, 0, .55f), new Vector3(0, 0, .75f));
        Gizmos.matrix = old; Gizmos.color = color;
    }
}

[CustomEditor(typeof(DreamcastLightEffect))]
public sealed class DreamcastLightEffectInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Stationary light: Steady, Pulse or Flicker. Activation Range 0 = always active; otherwise the player's feet trigger a half-metre fade near the light. One source and 2,048 affected vertices per room. Link static Unlit/emissive renderers to dim the fixture too. Unity preview uses shared C++ timing but Unity rendering; area spill and baked shadows must be checked in the exported build.",MessageType.Info);
        DrawDefaultInspector();
        if (GUILayout.Button("Open Dreamcast light tools")) DreamcastLightTools.Open();
    }
}
