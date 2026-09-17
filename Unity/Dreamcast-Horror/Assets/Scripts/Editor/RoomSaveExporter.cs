using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RoomSaveExporter
{
    public static uint Hash(byte[] bytes) { uint value=2166136261; foreach(byte b in bytes) value=unchecked((value^b)*16777619); return value; }
    public static byte[] Build(string room)
    {
        var points=UnityEngine.Object.FindObjectsByType<DreamcastSavePoint>().Where(p=>p.isActiveAndEnabled).OrderBy(p=>p.pointId).ToArray();
        if(points.Length>16) throw new InvalidDataException("Room exceeds 16 save points. Disable or remove unused points.");
        using var stream=new MemoryStream(); using var writer=new BinaryWriter(stream);
        writer.Write(0x31505344u); writer.Write(Hash(Encoding.UTF8.GetBytes(room))); writer.Write(points.Length);
        var objective=UnityEngine.Object.FindAnyObjectByType<KeyDoorPresentation>();
        bool Moving(Transform target) => target.GetComponentInParent<CppPlayerVisual>()!=null || target.GetComponentInParent<Animator>()?.runtimeAnimatorController!=null ||
            (objective!=null && ((objective.KeyVisual!=null && target.IsChildOf(objective.KeyVisual)) || (objective.DoorVisual!=null && target.IsChildOf(objective.DoorVisual))));
        int previous=0;
        foreach(var p in points) {
            if(p.pointId<1 || p.pointId==previous) throw new InvalidDataException("Save point '"+p.name+"' needs a unique positive ID.");
            previous=p.pointId; var pos=p.Position;
            if(!float.IsFinite(pos.x)||!float.IsFinite(pos.y)||!float.IsFinite(pos.z)||Mathf.Max(Mathf.Abs(pos.x),Mathf.Abs(pos.y),Mathf.Abs(pos.z))>10000 || !float.IsFinite(p.interactionRange)||p.interactionRange<.25f||p.interactionRange>3)
                throw new InvalidDataException("Save point '"+p.name+"': invalid position or interaction range (0.25-3 metres).");
            if(string.IsNullOrEmpty(p.prompt)||p.prompt.Length>47||p.prompt.Any(c=>c<32||c>126)) throw new InvalidDataException("Save point '"+p.name+"': prompt must contain 1-47 printable ASCII characters.");
            if(Moving(p.transform) || Moving(p.interactionPoint!=null ? p.interactionPoint : p.transform)) throw new InvalidDataException("Save point '"+p.name+"' must be stationary.");
            writer.Write(p.pointId); writer.Write(pos.x); writer.Write(pos.y); writer.Write(pos.z); writer.Write(p.interactionRange);
            byte[] text=new byte[48]; Encoding.ASCII.GetBytes(p.prompt).CopyTo(text,0); writer.Write(text);
        }
        return stream.ToArray();
    }
    [MenuItem("Dreamcast/Gameplay/Add save point to selected object")]
    public static void Add()
    {
        var obj=Selection.activeGameObject;
        if(Application.isPlaying || obj==null || !obj.scene.IsValid()) { EditorUtility.DisplayDialog("Save point","Select your book, typewriter or another scene object outside Play mode.","OK"); return; }
        if(obj.GetComponent<DreamcastSavePoint>()!=null) return;
        int id=1; var used=UnityEngine.Object.FindObjectsByType<DreamcastSavePoint>().Select(p=>p.pointId).ToArray(); while(used.Contains(id)) ++id;
        var point=Undo.AddComponent<DreamcastSavePoint>(obj); point.pointId=id; EditorUtility.SetDirty(point);
    }
}
