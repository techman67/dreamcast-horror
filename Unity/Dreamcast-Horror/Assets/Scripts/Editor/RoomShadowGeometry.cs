using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only triangle queries. No Unity Physics, Colliders, or runtime data.
// A small bounding-volume tree keeps repeated vertex rays inexpensive.
public sealed class RoomShadowGeometry
{
    struct Triangle { public Vector3 a, b, c, center; public Bounds bounds; public int layer; }
    struct Node { public Bounds bounds; public int start, count, left, right; }
    readonly Triangle[] triangles;
    readonly List<Node> nodes = new List<Node>();
    public RoomShadowGeometry()
    {
        var source = new List<Triangle>();
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>()
            .Where(r => r.enabled && r.shadowCastingMode != ShadowCastingMode.Off)
            .OrderBy(r => GlobalObjectId.GetGlobalObjectIdSlow(r).ToString(), StringComparer.Ordinal)) {
            if (RoomPropExporter.IsDynamic(renderer)) continue;
            var animator=renderer.GetComponentInParent<Animator>();
            if(animator!=null && animator.runtimeAnimatorController!=null)
                throw new InvalidDataException("Animated shadow caster cannot be frozen into a static bake: "+renderer.name);
            foreach(var material in renderer.sharedMaterials) {
                if(material==null) throw new InvalidDataException("Missing shadow caster material: "+renderer.name);
                float alpha=material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").a : material.HasProperty("_Color") ? material.color.a : 1;
                if(material.renderQueue>2500 || alpha!=1 || material.IsKeywordEnabled("_ALPHATEST_ON") ||
                    (material.HasProperty("_Surface") && material.GetFloat("_Surface")!=0))
                    throw new InvalidDataException("Shadow caster '"+renderer.name+"' is transparent/cutout. Use opaque geometry or disable Cast Shadows; texture alpha is not traced.");
            }
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) continue;
            if (mesh.vertexCount > 12288) throw new InvalidDataException("Shadow caster '"+renderer.name+"' exceeds 12,288 source vertices. Simplify it or disable Cast Shadows.");
            var transform = renderer.localToWorldMatrix;
            if (!float.IsFinite(transform.determinant) || Mathf.Abs(transform.determinant)<1e-10f)
                throw new InvalidDataException("Shadow caster has invalid/zero scale: "+renderer.name);
            var vertices = mesh.vertices;
            for (int i=0;i<vertices.Length;++i) {
                vertices[i]=transform.MultiplyPoint3x4(vertices[i]);
                var v=vertices[i];
                if (!float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z) ||
                    Mathf.Max(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z))>10000)
                    throw new InvalidDataException("Invalid shadow caster position: "+renderer.name);
            }
            for (int sub=0;sub<mesh.subMeshCount;++sub) {
                if (mesh.GetTopology(sub)!=MeshTopology.Triangles)
                    throw new InvalidDataException("Shadow caster requires triangle geometry: "+renderer.name);
                if (mesh.GetIndexCount(sub)/3 > 16384-source.Count)
                    throw new InvalidDataException("Shadow bake exceeds 16,384 source triangles. Simplify shadow casters or disable Cast Shadows on small details.");
                var indices=mesh.GetTriangles(sub);
                for(int i=0;i<indices.Length;i+=3) {
                    var a=vertices[indices[i]]; var b=vertices[indices[i+1]]; var c=vertices[indices[i+2]];
                    if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-16f) continue;
                    var bounds=new Bounds(a,Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
                    source.Add(new Triangle { a=a,b=b,c=c,center=(a+b+c)/3,bounds=bounds,layer=renderer.gameObject.layer });
                }
            }
        }
        triangles=source.ToArray();
        if(triangles.Length>0) Build(0,triangles.Length);
    }
    int Build(int start,int count)
    {
        var bounds=triangles[start].bounds;
        for(int i=start+1;i<start+count;++i) bounds.Encapsulate(triangles[i].bounds);
        int id=nodes.Count; nodes.Add(default);
        var node=new Node { bounds=bounds,start=start,count=count };
        if(count>8) {
            var size=bounds.size; int axis=size.x>=size.y && size.x>=size.z ? 0 : size.y>=size.z ? 1 : 2;
            Array.Sort(triangles,start,count,Comparer<Triangle>.Create((a,b)=>a.center[axis].CompareTo(b.center[axis])));
            node.count=0; node.left=Build(start,count/2); node.right=Build(start+count/2,count-count/2);
        }
        nodes[id]=node; return id;
    }
    public bool Blocked(Vector3 origin,Vector3 direction,float distance,int mask)
        => nodes.Count>0 && distance>0 && Hit(0,new Ray(origin,direction),distance,mask);
    bool Hit(int id,Ray ray,float distance,int mask)
    {
        var node=nodes[id];
        if(!node.bounds.IntersectRay(ray,out float entry) || entry>distance) return false;
        if(node.count==0) return Hit(node.left,ray,distance,mask) || Hit(node.right,ray,distance,mask);
        for(int i=node.start;i<node.start+node.count;++i) {
            var t=triangles[i]; if((mask & (1<<t.layer))==0) continue;
            var e1=t.b-t.a; var e2=t.c-t.a; var cross=Vector3.Cross(ray.direction,e2);
            float determinant=Vector3.Dot(e1,cross);
            if(Mathf.Abs(determinant)<1e-10f) continue;
            float inverse=1/determinant; var offset=ray.origin-t.a;
            float u=Vector3.Dot(offset,cross)*inverse;
            if(u<-.000001f || u>1.000001f) continue;
            var q=Vector3.Cross(offset,e1); float v=Vector3.Dot(ray.direction,q)*inverse;
            if(v<-.000001f || u+v>1.000001f) continue;
            float along=Vector3.Dot(e2,q)*inverse;
            // Both faces block light, including thin walls and mirrored meshes.
            if(along>.000001f && along<distance) return true;
        }
        return false;
    }
}
