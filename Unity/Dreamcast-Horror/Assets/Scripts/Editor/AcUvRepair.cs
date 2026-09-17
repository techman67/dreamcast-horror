using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// One-prop authoring repair. The supplied FBX stays intact; the prefab uses derived meshes.
public static class AcUvRepair
{
    public static void Run()
    {
        const string folder = "Assets/Art/AirConditioner/";
        GameObject root = null;
        try {
            root = PrefabUtility.LoadPrefabContents(folder + "AirConditioner.prefab");
            var obj = new StringBuilder("# AC UV repair; Unity model coordinates, Y up, units unchanged.\nmtllib air-conditioner-uv-fixed.mtl\n");
            string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
            int meshIndex = 0, vertexOffset = 1;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>()) {
                Mesh source = filter.sharedMesh;
                var vertices = source.vertices; var normals = source.normals;
                var positions = new List<Vector3>(); var repairedNormals = new List<Vector3>(); var uvs = new List<Vector2>();
                var indices = new List<int[]>();
                float density = 1 / Mathf.Max(source.bounds.size.x, source.bounds.size.y, source.bounds.size.z);
                for (int sub = 0; sub < source.subMeshCount; ++sub) {
                    int[] triangles = source.GetTriangles(sub); var output = new List<int>();
                    for (int i = 0; i < triangles.Length; i += 3) {
                        Vector3 a = vertices[triangles[i]], b = vertices[triangles[i+1]], c = vertices[triangles[i+2]];
                        Vector3 normal = Vector3.Cross(b-a,c-a); // Keep tiny valid face normals for axis selection.
                        int axis = Mathf.Abs(normal.x) >= Mathf.Abs(normal.y) && Mathf.Abs(normal.x) >= Mathf.Abs(normal.z) ? 0 : Mathf.Abs(normal.y) >= Mathf.Abs(normal.z) ? 1 : 2;
                        for (int j = 0; j < 3; ++j) {
                            int index = triangles[i+j]; Vector3 v = vertices[index];
                            Vector2 uv = axis == 0 ? new Vector2(v.z,v.y) : axis == 1 ? new Vector2(v.x,v.z) : new Vector2(v.x,v.y);
                            output.Add(positions.Count); positions.Add(v); repairedNormals.Add(normals.Length == vertices.Length ? normals[index] : normal); uvs.Add(uv*density);
                        }
                    }
                    indices.Add(output.ToArray());
                }
                var mesh = new Mesh { name = "AC_UV_" + meshIndex.ToString("00") };
                mesh.SetVertices(positions); mesh.SetNormals(repairedNormals); mesh.SetUVs(0,uvs); mesh.subMeshCount = indices.Count;
                for(int sub=0;sub<indices.Count;++sub) mesh.SetTriangles(indices[sub],sub);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                string path = folder + mesh.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing == null) AssetDatabase.CreateAsset(mesh,path);
                else { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; }
                filter.sharedMesh = mesh;
                obj.AppendLine("o AC_part_" + meshIndex++);
                Matrix4x4 matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach(var v in positions) { Vector3 p=matrix.MultiplyPoint3x4(v); obj.AppendLine($"v {F(p.x)} {F(p.y)} {F(p.z)}"); }
                foreach(var uv in uvs) obj.AppendLine($"vt {F(uv.x)} {F(uv.y)}");
                foreach(var n in repairedNormals) { Vector3 normal=matrix.inverse.transpose.MultiplyVector(n).normalized; obj.AppendLine($"vn {F(normal.x)} {F(normal.y)} {F(normal.z)}"); }
                var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                for(int sub=0;sub<indices.Count;++sub) {
                    obj.AppendLine("usemtl " + materials[sub].name);
                    for(int i=0;i<indices[sub].Length;i+=3) {
                        obj.Append("f");
                        for(int j=0;j<3;++j) { int n=vertexOffset+indices[sub][i+j]; obj.Append($" {n}/{n}/{n}"); }
                        obj.AppendLine();
                    }
                }
                vertexOffset += positions.Count;
            }
            PrefabUtility.SaveAsPrefabAsset(root,folder+"AirConditioner.prefab"); AssetDatabase.SaveAssets();
            string art=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Art/AirConditioner"));
            File.WriteAllText(Path.Combine(art,"air-conditioner-uv-fixed.obj"),obj.ToString());
            File.WriteAllText(Path.Combine(art,"air-conditioner-uv-fixed.mtl"),"newmtl AC_PaintedMetal\nKd 1 1 1\nmap_Kd ac-painted-metal-source.png\n\nnewmtl AC_DarkMetal\nKd 1 1 1\nmap_Kd ac-dark-metal-source.png\n");
            Debug.Log("AC UV REPAIR COMPLETE: shape/normals/materials preserved; face-projected UVs and portable OBJ saved.");
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
        finally { if(root != null) PrefabUtility.UnloadPrefabContents(root); }
        RoomPropExportChecks.Run();
    }
}
