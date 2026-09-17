using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomPropExportChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static Vector3[] Positions(byte[] data) {
        using var input = new BinaryReader(new MemoryStream(data));
        input.ReadUInt32(); int textures=input.ReadInt32(), parts=input.ReadInt32();
        input.BaseStream.Position += textures*44;
        var positions = new System.Collections.Generic.List<Vector3>();
        for (int p=0;p<parts;++p) {
            int count=input.ReadInt32(); input.ReadInt32();
            for(int i=0;i<count;++i) {
                positions.Add(new Vector3(input.ReadSingle(),input.ReadSingle(),input.ReadSingle()));
                input.BaseStream.Position += 12;
            }
        }
        return positions.ToArray();
    }
    public static void Run()
    {
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Check(RoomPropExporter.Build().manifest.Length==12,"Empty room must have an explicit empty prop bundle.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/AirConditioner/AirConditioner.prefab");
            var first=UnityEngine.Object.Instantiate(prefab);
            var baseline=RoomPropExporter.Build();
            Check(baseline.triangles==528 && baseline.parts==6 && baseline.textures.Count==2 && baseline.textureBytes==262144,"AC geometry/material budget mismatch.");
            Check(baseline.manifest.SequenceEqual(RoomPropExporter.Build().manifest),"Export is not deterministic.");
            var before=Positions(baseline.manifest);
            first.transform.position=new Vector3(2,3,4);
            var after=Positions(RoomPropExporter.Build().manifest);
            for(int i=0;i<before.Length;++i) Check(Vector3.Distance(after[i],before[i]+first.transform.position)<.0001f,"Placement was not baked.");
            first.transform.rotation=Quaternion.Euler(0,90,0); first.transform.localScale=new Vector3(2,3,4);
            after=Positions(RoomPropExporter.Build().manifest);
            for(int i=0;i<before.Length;++i) Check(Vector3.Distance(after[i],first.transform.TransformPoint(before[i]))<.0001f,"Rotation/nonuniform scale was not baked.");
            first.transform.localScale=new Vector3(-2,3,4);
            after=Positions(RoomPropExporter.Build().manifest);
            for(int i=0;i<before.Length;i+=3) {
                Check(Vector3.Distance(after[i],first.transform.TransformPoint(before[i]))<.0001f,"Mirrored position mismatch.");
                Check(Vector3.Distance(after[i+1],first.transform.TransformPoint(before[i+2]))<.0001f,"Mirrored winding was not corrected.");
            }
            var second=UnityEngine.Object.Instantiate(prefab);
            var duplicated=RoomPropExporter.Build();
            Check(duplicated.triangles==1056 && duplicated.textures.Count==2 && duplicated.textureBytes==262144,"Shared textures were duplicated.");
            second.SetActive(false); Check(RoomPropExporter.Build().triangles==528,"Inactive prop exported.");
            var renderer=first.GetComponentInChildren<MeshRenderer>();
            var original=renderer.sharedMaterials; var invalid=new Material(original[0]); invalid.SetColor("_BaseColor",new Color(1,1,1,.5f));
            var replaced=(Material[])original.Clone(); replaced[0]=invalid; renderer.sharedMaterials=replaced;
            bool rejected=false; try { RoomPropExporter.Build(); } catch(InvalidDataException) { rejected=true; }
            Check(rejected,"Transparent material accepted."); renderer.sharedMaterials=original; UnityEngine.Object.DestroyImmediate(invalid);
            var filter=renderer.GetComponent<MeshFilter>(); Mesh originalMesh=filter.sharedMesh;
            string badMeshPath=AssetDatabase.GenerateUniqueAssetPath("Assets/AC_invalid_uv_test.asset");
            try {
                var badMesh=UnityEngine.Object.Instantiate(originalMesh); badMesh.uv=new Vector2[badMesh.vertexCount];
                AssetDatabase.CreateAsset(badMesh,badMeshPath); filter.sharedMesh=badMesh;
                rejected=false; try { RoomPropExporter.Build(); } catch(InvalidDataException e) { rejected=e.Message.Contains("Collapsed texture UVs"); }
                Check(rejected,"Collapsed UVs did not receive a specific diagnostic.");
            } finally { filter.sharedMesh=originalMesh; AssetDatabase.DeleteAsset(badMeshPath); }
            for(int i=0;i<6;++i) UnityEngine.Object.Instantiate(prefab);
            rejected=false; try { RoomPropExporter.Build(); } catch(InvalidDataException) { rejected=true; }
            Check(rejected,"Part budget overflow accepted.");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            RoomExporter.Export();
            Debug.Log("PROP EXPORT CHECKS PASSED: real AC, texture deduplication, placement/rotation/scale/mirroring, inactive props, deterministic output, collapsed UV, material and budget rejection.");
            EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
