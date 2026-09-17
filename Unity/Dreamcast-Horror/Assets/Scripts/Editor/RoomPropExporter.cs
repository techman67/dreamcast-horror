using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// Static visual data only. Gameplay/collision never reads Unity meshes or materials.
public static class RoomPropExporter
{
    public sealed class Bundle {
        public byte[] manifest;
        public readonly Dictionary<string, byte[]> textures = new Dictionary<string, byte[]>();
        public int triangles, parts, textureBytes;
    }
    sealed class Part { public int texture; public byte[] vertices; }
    struct Vertex {
        public Vector3 position, normal;
        public Vector2 uv;
        public Color color;
        public static Vertex Mid(Vertex a, Vertex b) => new Vertex {
            position = (a.position+b.position)*.5f, normal = (a.normal+b.normal)*.5f,
            uv = (a.uv+b.uv)*.5f, color = (a.color+b.color)*.5f
        };
    }
    public static Bundle Build()
    {
        var lighting = RoomLightingBake.FromScene();
        var result = new Bundle(); var parts = new List<Part>();
        var textureIds = new Dictionary<Texture2D, int>();
        var names = new List<string>(); var dimensions = new List<Vector2Int>();
        int Texture(Texture2D source) {
            if (textureIds.TryGetValue(source, out int existing)) return existing;
            string path = AssetDatabase.GetAssetPath(source);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Prop texture '" + source.name + "': use an opaque PNG under Assets.");
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Prop source PNG exceeds 16 MiB: " + path);
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try {
                if (!image.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException("Cannot decode prop PNG: " + path);
                int w = image.width, h = image.height;
                bool Dimension(int n) => n >= 8 && n <= 256 && (n & (n - 1)) == 0;
                if (!Dimension(w) || !Dimension(h)) throw new InvalidDataException($"Prop texture '{source.name}' is {w}x{h}; resize the source PNG to power-of-two dimensions from 8 to 256 pixels.");
                if (source.wrapModeU != TextureWrapMode.Repeat || source.wrapModeV != TextureWrapMode.Repeat)
                    throw new InvalidDataException("Prop texture '" + source.name + "': only Repeat wrapping is currently supported.");
                using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                writer.Write(0x31544344u); writer.Write(w); writer.Write(h);
                foreach (Color32 c in image.GetPixels32()) { // Bottom row first, matching Unity UVs.
                    if (c.a != 255) throw new InvalidDataException("Prop texture '" + source.name + "' contains transparency; this static-prop pass supports opaque textures only.");
                    writer.Write((ushort)(((c.r >> 3) << 11) | ((c.g >> 2) << 5) | (c.b >> 3)));
                }
                byte[] bytes = stream.ToArray();
                using var sha = SHA256.Create();
                string name = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant().Substring(0,32) + ".rgb";
                int id = names.IndexOf(name);
                if (id < 0) {
                    if (names.Count >= 8 || result.textureBytes + w * h * 2 > 524288)
                        throw new InvalidDataException("Static prop textures exceed eight unique textures or 512 KiB RGB565. Reuse or reduce textures.");
                    id = names.Count; names.Add(name); dimensions.Add(new Vector2Int(w,h));
                    result.textures.Add(name, bytes); result.textureBytes += w * h * 2;
                } else if (!result.textures[name].SequenceEqual(bytes)) throw new InvalidDataException("Prop texture hash collision.");
                textureIds.Add(source,id); return id;
            } finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        var animated = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>().FirstOrDefault(x => x.enabled);
        if (animated != null) throw new InvalidDataException("Skinned model '" + animated.name + "' cannot export yet; only static props are supported.");
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>().Where(x => x.enabled)
                     .OrderBy(x => GlobalObjectId.GetGlobalObjectIdSlow(x).ToString(), StringComparer.Ordinal)) {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            bool primitive = path == "Resources/unity_builtin_extra" || path == "Library/unity default resources";
            // Legacy exports keep primitive proxies. Lit rooms export actual static surfaces.
            if (primitive && lighting == null) continue;
            if (string.IsNullOrEmpty(path)) throw new InvalidDataException("Prop mesh has no source asset: " + renderer.name);
            var objective = UnityEngine.Object.FindAnyObjectByType<KeyDoorPresentation>();
            bool dynamic = renderer.GetComponentInParent<CppPlayerVisual>() != null ||
                (objective != null && ((objective.KeyVisual != null && renderer.transform.IsChildOf(objective.KeyVisual)) ||
                (objective.DoorVisual != null && renderer.transform.IsChildOf(objective.DoorVisual))));
            if (dynamic && primitive) continue;
            if (dynamic)
                throw new InvalidDataException("Custom key/door models require dynamic visual binding, not static prop export: " + renderer.name);
            var animator = renderer.GetComponentInParent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                throw new InvalidDataException("Animated prop '" + renderer.name + "' is unsupported in static export.");
            Mesh mesh = filter.sharedMesh;
            var vertices = mesh.vertices; var uv = mesh.uv; var colors = mesh.colors32; var normals = mesh.normals;
            if (vertices.Length > 12288 || renderer.sharedMaterials.Length != mesh.subMeshCount)
                throw new InvalidDataException("Prop '" + renderer.name + "' has too many vertices or mismatched material slots.");
            Matrix4x4 transform = renderer.localToWorldMatrix;
            if (!float.IsFinite(transform.determinant) || Mathf.Abs(transform.determinant) < 1e-10f)
                throw new InvalidDataException("Prop has a zero or invalid transform scale: " + renderer.name);
            Matrix4x4 normalTransform = transform.inverse.transpose;
            for (int sub = 0; sub < mesh.subMeshCount; ++sub) {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidDataException("Only triangle meshes export: " + renderer.name);
                int[] indices = mesh.GetTriangles(sub); if (indices.Length == 0) continue;
                if (parts.Count >= 32 || result.triangles + indices.Length / 3 > 4096)
                    throw new InvalidDataException("Static room visuals exceed 32 material parts or 4,096 triangles. Simplify meshes, increase lighting's triangle edge limit or remove placements.");
                Material mat = renderer.sharedMaterials[sub];
                if (mat == null) throw new InvalidDataException("Missing prop material: " + renderer.name);
                string shader = mat.shader.name;
                bool bake = lighting != null && !shader.Contains("Unlit");
                if (bake && normals.Length != vertices.Length)
                    throw new InvalidDataException("Missing normals for lighting on '" + renderer.name + "'. Recalculate mesh normals before export.");
                if (shader != "Universal Render Pipeline/Lit" && shader != "Universal Render Pipeline/Unlit" && shader != "Standard" && shader != "Unlit/Texture" && shader != "Unlit/Color")
                    throw new InvalidDataException("Unsupported prop shader '" + shader + "' on " + renderer.name + ". Use a basic opaque base-color material.");
                string map = mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                Color tint = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.HasProperty("_Color") ? mat.color : Color.white;
                if (mat.renderQueue > 2500 || tint.a < 1 || (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") != 0) || mat.IsKeywordEnabled("_ALPHATEST_ON"))
                    throw new InvalidDataException("Transparent/cutout prop material is unsupported: " + mat.name);
                foreach (string feature in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailAlbedoMap" })
                    if (mat.HasProperty(feature) && mat.GetTexture(feature) != null) throw new InvalidDataException("Prop material '" + mat.name + "' uses " + feature + "; bake details into its base-color PNG.");
                if (mat.IsKeywordEnabled("_EMISSION")) throw new InvalidDataException("Emissive prop material is unsupported: " + mat.name);
                var baseMap = mat.HasProperty(map) ? mat.GetTexture(map) : null;
                if (baseMap != null && !(baseMap is Texture2D)) throw new InvalidDataException("Prop base map must be a 2D PNG: " + mat.name);
                var texture = baseMap as Texture2D;
                if (texture != null && uv.Length != vertices.Length) throw new InvalidDataException("Missing UVs on textured prop: " + renderer.name);
                int textureId = texture != null ? Texture(texture) : -1;
                Vector2 scale = texture != null ? mat.GetTextureScale(map) : Vector2.one, offset = texture != null ? mat.GetTextureOffset(map) : Vector2.zero;
                if (texture != null) for (int i = 0; i < indices.Length; i += 3) {
                    int a=indices[i], b=indices[i+1], c=indices[i+2];
                    Vector2 u=Vector2.Scale(uv[b]-uv[a],scale), v=Vector2.Scale(uv[c]-uv[a],scale);
                    if (Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude > 1e-14f && Mathf.Abs(u.x*v.y-u.y*v.x) < 1e-10f)
                        throw new InvalidDataException("Collapsed texture UVs on '" + renderer.name + "' (triangle " + i/3 + "). Re-unwrap the mesh or use an untextured material; a line of texture would otherwise stretch across this face.");
                }
                using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                Vertex ReadVertex(int index) {
                    Vector3 p = transform.MultiplyPoint3x4(vertices[index]);
                    Vector2 tex = uv.Length == vertices.Length ? Vector2.Scale(uv[index], scale) + offset : Vector2.zero;
                    foreach (float value in new[] { p.x, p.y, p.z, tex.x, tex.y })
                        if (!float.IsFinite(value) || Mathf.Abs(value) > 10000) throw new InvalidDataException("Prop position/UV is invalid or outside limits: " + renderer.name);
                    Color c = tint * (colors.Length == vertices.Length ? (Color)colors[index] : Color.white);
                    if (!float.IsFinite(c.r) || !float.IsFinite(c.g) || !float.IsFinite(c.b) || c.a != 1 || c.r < 0 || c.r > 1 || c.g < 0 || c.g > 1 || c.b < 0 || c.b > 1)
                        throw new InvalidDataException("Prop color must be opaque with RGB values between 0 and 1: " + renderer.name);
                    return new Vertex { position=p, uv=tex, color=c,
                        normal=bake ? normalTransform.MultiplyVector(normals[index]).normalized : Vector3.up };
                }
                void WriteVertex(Vertex v) {
                    Color32 packed = bake ? v.color * lighting.Sample(v.position, v.normal, renderer.gameObject.layer) : v.color;
                    writer.Write(v.position.x); writer.Write(v.position.y); writer.Write(v.position.z); writer.Write(v.uv.x); writer.Write(v.uv.y);
                    writer.Write(packed.r); writer.Write(packed.g); writer.Write(packed.b); writer.Write(packed.a);
                }
                void Triangle(Vertex a, Vertex b, Vertex c, int depth) {
                    float ab=(a.position-b.position).sqrMagnitude, bc=(b.position-c.position).sqrMagnitude, ca=(c.position-a.position).sqrMagnitude;
                    if (bake && Mathf.Max(ab,bc,ca) > lighting.MaxEdge*lighting.MaxEdge) {
                        if (depth >= 24) throw new InvalidDataException("Lighting subdivision is excessive on '"+renderer.name+"'. Reduce its scale or increase maximum triangle edge.");
                        // Bisect only the longest edge: thin walls should not pay
                        // for repeatedly splitting their already short edges.
                        if (ab >= bc && ab >= ca) { var mid=Vertex.Mid(a,b); Triangle(a,mid,c,depth+1); Triangle(mid,b,c,depth+1); }
                        else if (bc >= ca) { var mid=Vertex.Mid(b,c); Triangle(a,b,mid,depth+1); Triangle(a,mid,c,depth+1); }
                        else { var mid=Vertex.Mid(c,a); Triangle(a,b,mid,depth+1); Triangle(mid,b,c,depth+1); }
                        return;
                    }
                    if (++result.triangles > 4096) throw new InvalidDataException("Static room exceeds 4,096 triangles while exporting '"+renderer.name+"'. Increase lighting's maximum triangle edge, simplify meshes or remove placements.");
                    WriteVertex(a); WriteVertex(b); WriteVertex(c);
                }
                for(int i=0;i<indices.Length;i+=3) Triangle(ReadVertex(indices[i]),ReadVertex(indices[i+1]),ReadVertex(indices[i+2]),0);
                // Bake mirrored Unity object transforms by correcting winding in Unity space.
                byte[] data = stream.ToArray();
                if (transform.determinant < 0)
                    for (int i = 0; i < data.Length; i += 72)
                        for (int k = 0; k < 24; ++k) { byte temp = data[i+24+k]; data[i+24+k] = data[i+48+k]; data[i+48+k] = temp; }
                parts.Add(new Part { texture = textureId, vertices = data });
            }
        }
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) {
            writer.Write(lighting == null ? 0x31504344u : 0x32504344u); writer.Write(names.Count); writer.Write(parts.Count);
            if (lighting != null) writer.Write(1u); // DCP2: static room visuals included; suppress collision proxies.
            for (int i = 0; i < names.Count; ++i) { writer.Write(dimensions[i].x); writer.Write(dimensions[i].y); writer.Write(Encoding.ASCII.GetBytes(names[i])); }
            foreach (var part in parts) { writer.Write(part.vertices.Length / 24); writer.Write(part.texture); writer.Write(part.vertices); }
            result.manifest = stream.ToArray(); result.parts = parts.Count;
        }
        return result;
    }
}
