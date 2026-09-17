using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RoomExtendedLightingChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action, string fragment)
    {
        try { action(); } catch (InvalidDataException error) { Check(error.Message.Contains(fragment), "Wrong diagnostic: " + error.Message); return; }
        throw new Exception("Missing rejection: " + fragment);
    }
    public static void Run()
    {
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var fixture = new GameObject("Fixture");
            var light = DreamcastLightTools.Create(LightType.Rectangle, fixture.transform);
            Check(light.transform.parent == fixture.transform, "Light tools lost fixture parent.");
            var settings = Object.FindAnyObjectByType<DreamcastBakedLighting>();
            Check(settings != null, "Light tools omitted room settings.");
            settings.ambient = Color.black; settings.occlusionStrength = 0;
            light.transform.position = new Vector3(0, 0, -2);
            light.color = Color.white; light.intensity = 1; light.range = 4;
            light.areaSize = new Vector2(.01f, .01f);
            float Sample(Vector3 p, Vector3 n, int layer = 0, bool receive = true) => RoomLightingBake.FromScene().Sample(p,n,layer,receive).r;
            float Center() => Sample(Vector3.zero, Vector3.back);
            Check(Mathf.Abs(Center() - .25f) < .001f, "Tiny area did not converge on forward point illumination.");
            Check(Sample(new Vector3(0,0,-4),Vector3.forward) == 0, "Area emitted backwards.");
            Check(Sample(Vector3.zero,Vector3.forward) == 0, "Area illuminated receiver back face.");
            Check(Sample(Vector3.forward*3,Vector3.back) == 0, "Area exceeded range.");
            light.cullingMask = 2; Check(Center() == 0, "Area receiver layer mask ignored."); light.cullingMask = -1;
            light.areaSize = new Vector2(2,2);
            float large = Center(); Check(large > 0 && large < .25f, "Area dimensions ignored or multiplied power.");
            settings.areaSamples = 32; Check(Mathf.Abs(Center()-large)<.02f, "Sample count multiplied power.");
            light.transform.localScale = new Vector3(4,2,3);
            float scaled = Center(); light.transform.localScale = Vector3.one;
            Check(Mathf.Abs(Center()-scaled)<.00001f, "Area dimensions depend on transform scale.");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(blocker.GetComponent<Collider>());
            blocker.transform.position = new Vector3(0,0,-1); blocker.transform.localScale = new Vector3(4,4,.05f);
            Check(Center() == 0, "Opaque geometry did not block area.");
            Check(Sample(Vector3.zero,Vector3.back,0,false) > .1f, "Area Receive Shadows opt-out ignored.");
            blocker.transform.position = new Vector3(-2,0,-1);
            float partial = Center(); Check(partial > .02f && partial < large-.02f, "Partial area visibility failed.");
            blocker.SetActive(false);
            light.type = LightType.Disc; light.areaSize = new Vector2(1,1);
            float disc = Center(); Check(disc > .1f && disc < .25f, "Disc sample illumination invalid.");
            light.transform.rotation = Quaternion.Euler(0,180,0); Check(Center() == 0, "Area rotation ignored.");
            light.transform.rotation = Quaternion.identity;
            light.areaSize = new Vector2(0,1); Reject(() => RoomLightingBake.FromScene(), "radius");
            light.areaSize = Vector2.one; settings.areaSamples = 33;
            Reject(() => RoomLightingBake.FromScene(), "area samples"); settings.areaSamples = 16;
            light.enabled = false; Check(Center() == 0, "Disabled area exported.");
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor",Color.black); mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor",new Color(.8f,.4f,.1f));
            receiver.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var bundle = RoomPropExporter.Build();
            Check(bundle.manifest.SequenceEqual(RoomPropExporter.Build().manifest), "Extended bake nondeterministic.");
            // DCP2, zero textures, one part: vertex colors start at byte 44.
            Check(bundle.manifest[44] == 204 && bundle.manifest[45] == 102 && Math.Abs(bundle.manifest[46]-26) <= 1,
                "Emission failed on black material with zero ambient and no lights.");
            mat.SetColor("_EmissionColor",new Color(4,2,.5f)); bundle = RoomPropExporter.Build();
            Check(bundle.manifest[44] == 255 && Math.Abs(bundle.manifest[45]-128) <= 1 && Math.Abs(bundle.manifest[46]-32) <= 1,
                "HDR emission lost hue.");
            string persistedPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Emission_persistence_check.mat");
            try {
                var persisted = new Material(mat);
                persisted.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                AssetDatabase.CreateAsset(persisted, persistedPath); AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(persistedPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                receiver.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(persistedPath);
                Check(RoomPropExporter.Build().manifest[44] == 255, "Saved/imported URP material lost emission.");
            } finally {
                receiver.GetComponent<MeshRenderer>().sharedMaterial = mat; AssetDatabase.DeleteAsset(persistedPath);
            }
            mat.DisableKeyword("_EMISSION"); Check(RoomPropExporter.Build().manifest[44] == 0, "Disabled emission still exported.");
            mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",new Color(9,0,0));
            Reject(() => RoomPropExporter.Build(), "Emission Color");
            mat.SetColor("_EmissionColor",Color.white); mat.SetTexture("_EmissionMap",Texture2D.whiteTexture);
            Reject(() => RoomPropExporter.Build(), "_EmissionMap");
            Object.DestroyImmediate(mat);
            Debug.Log("EXTENDED LIGHTING CHECKS PASSED: rectangle/disc, direction, range, masks, dimensions, normalized sampling, partial shadows without colliders, receiver controls, disabled lights, emission/HDR, deterministic bytes and actionable validation.");
            EditorApplication.Exit(0);
        } catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
