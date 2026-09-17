using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Reads the actual export bytes. Nothing is installed on or hidden in the scene.
public sealed class DreamcastExportPreview : EditorWindow
{
    sealed class Part { public Mesh mesh; public Material material; public Color32[] off,on; }
    readonly List<Part> parts=new List<Part>();
    readonly List<Texture2D> textures=new List<Texture2D>();
    PreviewRenderUtility preview;
    float brightness=1;
    string status="Click Bake preview to inspect the exported static room.";
    int cameraIndex;
    [MenuItem("Dreamcast/Lighting/Preview exported room")]
    static void Open() { GetWindow<DreamcastExportPreview>("Dreamcast preview"); }
    void Clear()
    {
        foreach(var p in parts) { DestroyImmediate(p.mesh); DestroyImmediate(p.material); } parts.Clear();
        foreach(var t in textures) DestroyImmediate(t); textures.Clear();
        preview?.Cleanup(); preview=null;
    }
    void OnDisable() { Clear(); }
    public void Rebuild()
    {
        Clear();
        try {
            var bundle=RoomPropExporter.Build();
            using var reader=new BinaryReader(new MemoryStream(bundle.manifest));
            uint magic=reader.ReadUInt32(); int nt=reader.ReadInt32(), np=reader.ReadInt32();
            if(magic!=0x31504344) reader.ReadUInt32();
            for(int i=0;i<nt;++i) {
                int w=reader.ReadInt32(),h=reader.ReadInt32(); string name=Encoding.ASCII.GetString(reader.ReadBytes(36));
                byte[] bytes=bundle.textures[name]; var pixels=new Color32[w*h];
                for(int j=0;j<pixels.Length;++j) { ushort v=BitConverter.ToUInt16(bytes,12+j*2); pixels[j]=new Color32((byte)(((v>>11)&31)*255/31),(byte)(((v>>5)&63)*255/63),(byte)((v&31)*255/31),255); }
                var t=new Texture2D(w,h,TextureFormat.RGBA32,false,true) { hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear };
                textures.Add(t); t.SetPixels32(pixels); t.Apply();
            }
            var shader=Shader.Find("Hidden/DreamcastExportPreview");
            if(shader==null) throw new InvalidDataException("Export preview shader is missing.");
            for(int i=0;i<np;++i) {
                int count=reader.ReadInt32(),tex=reader.ReadInt32(); var pos=new Vector3[count]; var uv=new Vector2[count]; var colors=new Color32[count]; var indices=new int[count];
                for(int j=0;j<count;++j) { pos[j]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()); uv[j]=new Vector2(reader.ReadSingle(),reader.ReadSingle()); colors[j]=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte()); indices[j]=j; }
                var part=new Part { mesh=new Mesh { hideFlags=HideFlags.HideAndDontSave },material=new Material(shader) { hideFlags=HideFlags.HideAndDontSave },off=colors,on=(Color32[])colors.Clone() };
                parts.Add(part); part.mesh.vertices=pos; part.mesh.uv=uv; part.mesh.colors32=colors; part.mesh.triangles=indices; part.mesh.RecalculateBounds();
                if(tex>=0) part.material.mainTexture=textures[tex];
            }
            if(magic==0x33504344) {
                reader.ReadBytes(32); int changes=reader.ReadInt32();
                for(int i=0;i<changes;++i) { int index=reader.ReadInt32(); var color=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte()); foreach(var p in parts) { if(index<p.on.Length) { p.on[index]=color; break; } index-=p.on.Length; } }
            }
            if(reader.BaseStream.Position!=reader.BaseStream.Length) throw new InvalidDataException("Preview did not consume the complete room export.");
            preview=new PreviewRenderUtility(); preview.camera.clearFlags=CameraClearFlags.SolidColor; preview.camera.backgroundColor=new Color(.09f,.09f,.1f); preview.camera.nearClipPlane=.05f; preview.camera.farClipPlane=150; preview.camera.fieldOfView=65;
            ApplyBrightness();
            status=$"{bundle.triangles:N0} triangles • {bundle.parts} parts • {bundle.textureBytes/1024} / 512 KiB textures";
        } catch(Exception e) { Clear(); status=e.Message; throw; }
    }
    void ApplyBrightness() { foreach(var p in parts) { var colors=new Color32[p.off.Length]; for(int i=0;i<colors.Length;++i) colors[i]=Color32.Lerp(p.off[i],p.on[i],brightness); p.mesh.colors32=colors; } }
    void PositionCamera()
    {
        var source=GameObject.Find(cameraIndex==0 ? "GameplayCamera_01" : "GameplayCamera_02");
        if(source!=null) preview.camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
        else { preview.camera.transform.position=new Vector3(0,9,-10); preview.camera.transform.LookAt(Vector3.zero); }
    }
    void OnGUI()
    {
        GUILayout.Label("Exported geometry, RGB565 textures and baked colors",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Static room preview. Moving player/key/door are omitted. Re-bake after editing lights or objects. Light effect slider shows off/on endpoints; display filtering may differ from Flycast.",MessageType.Info);
        if(GUILayout.Button("Bake preview")) { try { Rebuild(); } catch(Exception e) { Debug.LogException(e); } }
        cameraIndex=GUILayout.Toolbar(cameraIndex,new[]{"Camera 1","Camera 2"});
        EditorGUI.BeginChangeCheck(); brightness=EditorGUILayout.Slider("Light effect brightness",brightness,0,1); if(EditorGUI.EndChangeCheck()) ApplyBrightness();
        GUILayout.Label(status);
        Rect rect=GUILayoutUtility.GetRect(100,100,10000,10000);
        if(preview==null || Event.current.type!=EventType.Repaint || rect.width<1 || rect.height<1) return;
        PositionCamera(); preview.BeginPreview(rect,GUIStyle.none);
        foreach(var p in parts) preview.DrawMesh(p.mesh,Matrix4x4.identity,p.material,0);
        preview.Render(); GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.ScaleToFit,false);
    }
    public static void CheckDemo()
    {
        DreamcastExportPreview window=null;
        try {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/LightingTestScene.unity");
            bool dirty=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty;
            window=CreateInstance<DreamcastExportPreview>(); window.Rebuild(); window.brightness=0; window.ApplyBrightness(); window.Rebuild();
            if(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty!=dirty) throw new Exception("Preview dirtied the source scene.");
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null) {
                window.PositionCamera(); window.preview.BeginPreview(new Rect(0,0,640,480),GUIStyle.none);
                foreach(var part in window.parts) window.preview.DrawMesh(part.mesh,Matrix4x4.identity,part.material,0);
                window.preview.Render(); var rendered=window.preview.EndPreview();
                var target=RenderTexture.GetTemporary(640,480,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                var previous=RenderTexture.active; var image=new Texture2D(640,480,TextureFormat.RGB24,false);
                try { Graphics.Blit(rendered,target); RenderTexture.active=target; image.ReadPixels(new Rect(0,0,640,480),0,0); image.Apply(); File.WriteAllBytes(Path.GetFullPath("../../Simulant/build/contact-unity-preview.png"),image.EncodeToPNG()); }
                finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); DestroyImmediate(image); }
            }
            Debug.Log("EXPORT PREVIEW CHECK PASSED: complete manifest decoded, rebuilt and disposed without scene edits. "+window.status);
            DestroyImmediate(window); EditorApplication.Exit(0);
        } catch(Exception e) { if(window!=null) DestroyImmediate(window); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
