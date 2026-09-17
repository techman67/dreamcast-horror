using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

// Export-only horizontal surface rasterizer. Runtime uses its ordinary base map.
public sealed class RoomContactBake
{
    struct Face { public Vector3 a,b,c; public Vector2 ua,ub,uc; }
    readonly bool[] top;
    readonly float minX,minZ,width,depth;
    readonly int resolution;
    public byte[] Texture { get; private set; }
    public bool Contains(int triangle) => top[triangle];
    public Vector2 UV(Vector3 p) => new Vector2((1.5f+(p.x-minX)/width*(resolution-3))/resolution,
        (1.5f+(p.z-minZ)/depth*(resolution-3))/resolution);
    public static RoomContactBake Create(MeshRenderer renderer, int submesh, Material material, byte[] sourceTexture, RoomLightingBake lighting)
    {
        var settings=renderer.GetComponent<DreamcastContactSurface>();
        if(settings==null || !settings.isActiveAndEnabled) return null;
        if(!float.IsFinite(settings.strength) || settings.strength<0 || settings.strength>1 ||
           !float.IsFinite(settings.distance) || settings.distance<.05f || settings.distance>2 || settings.samples<8 || settings.samples>64 ||
           (settings.resolution!=64 && settings.resolution!=128 && settings.resolution!=256))
            throw new InvalidDataException("Contact surface '"+renderer.name+"': resolution must be 64, 128 or 256; strength 0-1, distance 0.05-2m, samples 8-64.");
        if(settings.strength==0) return null;
        if(lighting==null || !renderer.receiveShadows || RoomPropExporter.IsDynamic(renderer))
            throw new InvalidDataException("Contact surface '"+renderer.name+"' needs room lighting, Receive Shadows enabled, and a stationary mesh.");
        if(material.shader.name.Contains("Unlit") || material.IsKeywordEnabled("_EMISSION"))
            throw new InvalidDataException("Contact surface '"+renderer.name+"' needs an opaque Lit material without emission. Use a separate mesh for glowing parts.");
        var result=new RoomContactBake(renderer,submesh,material,sourceTexture,lighting,settings);
        return result.Texture==null ? null : result;
    }
    RoomContactBake(MeshRenderer renderer,int submesh,Material material,byte[] source,RoomLightingBake lighting,DreamcastContactSurface settings)
    {
        resolution=settings.resolution;
        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
        var positions=mesh.vertices; var normals=mesh.normals; var uv=mesh.uv; var indices=mesh.GetTriangles(submesh);
        var matrix=renderer.localToWorldMatrix; var normalMatrix=matrix.inverse.transpose;
        if(normals.Length!=positions.Length) throw new InvalidDataException("Contact surface needs mesh normals: "+renderer.name);
        top=new bool[indices.Length/3]; var faces=new List<Face>();
        string map=material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
        Vector2 scale=source!=null ? material.GetTextureScale(map) : Vector2.one, offset=source!=null ? material.GetTextureOffset(map) : Vector2.zero;
        Vector2 Tex(int i) => source!=null ? Vector2.Scale(uv[i],scale)+offset : Vector2.zero;
        var bounds=new Bounds(); bool first=true; float y=0;
        for(int i=0;i<indices.Length;i+=3) {
            int a=indices[i],b=indices[i+1],c=indices[i+2];
            if(normalMatrix.MultiplyVector(normals[a]+normals[b]+normals[c]).normalized.y<.999f) continue;
            var face=new Face { a=matrix.MultiplyPoint3x4(positions[a]),b=matrix.MultiplyPoint3x4(positions[b]),c=matrix.MultiplyPoint3x4(positions[c]),ua=Tex(a),ub=Tex(b),uc=Tex(c) };
            if(first) { y=face.a.y; bounds=new Bounds(face.a,Vector3.zero); first=false; }
            if(Mathf.Abs(face.a.y-y)>.001f || Mathf.Abs(face.b.y-y)>.001f || Mathf.Abs(face.c.y-y)>.001f)
                throw new InvalidDataException("Contact surface '"+renderer.name+"' has sloped or stacked upward faces. Use a separate flat horizontal receiver for each floor height.");
            if(Mathf.Abs(Cross(face.b-face.a,face.c-face.a))<1e-8f) continue;
            bounds.Encapsulate(face.a); bounds.Encapsulate(face.b); bounds.Encapsulate(face.c);
            faces.Add(face); top[i/3]=true;
        }
        if(faces.Count==0) return;
        if(faces.Count>1024) throw new InvalidDataException("Contact surface '"+renderer.name+"' exceeds 1,024 upward source triangles. Simplify this receiver.");
        minX=bounds.min.x; minZ=bounds.min.z; width=bounds.size.x; depth=bounds.size.z;
        if(width<.01f || depth<.01f) throw new InvalidDataException("Contact receiver is narrower than 1cm: "+renderer.name);
        int sourceW=source==null ? 0 : BitConverter.ToInt32(source,4), sourceH=source==null ? 0 : BitConverter.ToInt32(source,8);
        Color Read(int x,int z) {
            x=(x%sourceW+sourceW)%sourceW; z=(z%sourceH+sourceH)%sourceH;
            ushort packed=BitConverter.ToUInt16(source,12+(z*sourceW+x)*2);
            return new Color(((packed>>11)&31)/31f,((packed>>5)&63)/63f,(packed&31)/31f,1);
        }
        Color Base(Vector2 tex) {
            if(source==null) return Color.white;
            float x=(tex.x-Mathf.Floor(tex.x))*sourceW-.5f,z=(tex.y-Mathf.Floor(tex.y))*sourceH-.5f;
            int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);
            return Color.Lerp(Color.Lerp(Read(ix,iz),Read(ix+1,iz),x-ix),Color.Lerp(Read(ix,iz+1),Read(ix+1,iz+1),x-ix),z-iz);
        }
        using var bytes=new MemoryStream(); using var writer=new BinaryWriter(bytes);
        writer.Write(0x31544344u); writer.Write(resolution); writer.Write(resolution);
        try {
            for(int row=0;row<resolution;++row) {
                if(!Application.isBatchMode && row%16==0 && EditorUtility.DisplayCancelableProgressBar("Bake floor contact shadows",renderer.name,(float)row/resolution))
                    throw new OperationCanceledException("Contact bake cancelled; no new room export was written.");
                for(int col=0;col<resolution;++col) {
                    var p=new Vector3(minX+Mathf.Clamp01((col-1f)/(resolution-3))*width,y,minZ+Mathf.Clamp01((row-1f)/(resolution-3))*depth);
                    Color color=Color.white;
                    foreach(var face in faces) {
                        float det=Cross(face.b-face.a,face.c-face.a);
                        float u=Cross(p-face.a,face.c-face.a)/det, v=Cross(face.b-face.a,p-face.a)/det;
                        if(u<-.00001f || v<-.00001f || u+v>1.00001f) continue;
                        var tex=face.ua*(1-u-v)+face.ub*u+face.uc*v;
                        color=Base(tex)*lighting.SurfaceOcclusion(p,Vector3.up,settings.strength,settings.distance,settings.samples);
                        break;
                    }
                    writer.Write((ushort)((Mathf.RoundToInt(Mathf.Clamp01(color.r)*31)<<11) | (Mathf.RoundToInt(Mathf.Clamp01(color.g)*63)<<5) | Mathf.RoundToInt(Mathf.Clamp01(color.b)*31)));
                }
            }
        } finally { if(!Application.isBatchMode) EditorUtility.ClearProgressBar(); }
        Texture=bytes.ToArray();
    }
    static float Cross(Vector3 a,Vector3 b) => a.x*b.z-a.z*b.x;
}
