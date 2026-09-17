using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Temporary scene-authoring adapter for the primitive test room.
// Runtime consumes the resulting text, never Unity renderer bounds.
public static class RoomExporter
{
    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Vector(Vector3 value) => $"{Number(value.x)} {Number(value.y)} {Number(value.z)}";
    private static string Pose(Transform value, Vector3 position)
    {
        Vector3 angles = value.eulerAngles * Mathf.Deg2Rad;
        return $"{Vector(position)} {Number(angles.y)} {Number(angles.x)} {Number(angles.z)}";
    }

    public static string BuildText()
    {
        CppPlayerVisual player = UnityEngine.Object.FindAnyObjectByType<CppPlayerVisual>();
        NativeGameBridge bridge = UnityEngine.Object.FindAnyObjectByType<NativeGameBridge>();
        GameObject cameraA = GameObject.Find("GameplayCamera_01");
        GameObject cameraB = GameObject.Find("GameplayCamera_02");
        GameObject zone = GameObject.Find("CameraZone_01_To_02");
        if (player == null || bridge == null || cameraA == null || cameraB == null || zone == null)
            throw new InvalidDataException("The sample player, bridge, two cameras and transition marker are required.");
        if (Mathf.Abs(zone.transform.position.x) > 0.0001f ||
            Quaternion.Angle(zone.transform.rotation, Quaternion.identity) > 0.001f)
            throw new InvalidDataException("The transition marker must be centered on X=0 and axis aligned.");

        CppCollision[] shapes = UnityEngine.Object.FindObjectsByType<CppCollision>()
            .Where(shape => shape.isActiveAndEnabled).OrderBy(shape => shape.name, StringComparer.Ordinal).ToArray();
        if (shapes.Length > 128)
            throw new InvalidDataException("Room exceeds the 128 static collision shape budget; export was not written.");

        KeyDoorPresentation objective = UnityEngine.Object.FindAnyObjectByType<KeyDoorPresentation>();
        int doorIndex = -1;
        if (objective != null)
        {
            if (!objective.HasVisualBindings || objective.DoorCollision == null || objective.ExitMarker == null)
                throw new InvalidDataException("Key, door collision and exit marker bindings are required.");
            doorIndex = Array.IndexOf(shapes, objective.DoorCollision);
            if (doorIndex < 0) throw new InvalidDataException("The authored door collision must be active at export.");
        }
        var text = new StringBuilder();
        text.AppendLine(objective == null ? "dreamcast_room 1" : "dreamcast_room 2");
        text.AppendLine($"player 1 {Pose(player.transform, player.AuthoredSpawnPosition)} {Number(bridge.AuthoredPlayerRadius)}");
        text.AppendLine($"camera 1 {Pose(cameraA.transform, cameraA.transform.position)}");
        text.AppendLine($"camera 2 {Pose(cameraB.transform, cameraB.transform.position)}");
        text.AppendLine($"transition {Number(zone.transform.position.z)} {Number(Mathf.Abs(zone.transform.lossyScale.x) * 0.5f)} {bridge.AuthoredInitialCamera}");
        text.AppendLine($"shapes {shapes.Length}");
        foreach (CppCollision shape in shapes)
        {
            shape.Recalculate();
            switch (shape.ResolvedMode)
            {
                case CppCollision.CollisionMode.Box:
                    text.AppendLine($"box {Vector(shape.WorldCenter)} {Vector(shape.WorldSize * 0.5f)}");
                    break;
                case CppCollision.CollisionMode.Sphere:
                    text.AppendLine($"sphere {Vector(shape.WorldCenter)} {Number(shape.Radius)}");
                    break;
                case CppCollision.CollisionMode.Capsule:
                    text.AppendLine($"capsule {Vector(shape.WorldCenter)} {Number(shape.Radius)} {Number(shape.Height)}");
                    break;
                default:
                    throw new InvalidDataException($"Unsupported collision shape on {shape.name}.");
            }
        }
        if (objective != null)
        {
            text.AppendLine($"key {objective.KeyActorId} {Vector(objective.KeyVisual.position)} {Number(objective.KeyRange)}");
            text.AppendLine($"door {objective.DoorActorId} {Vector(objective.DoorVisual.position)} {Number(objective.DoorRange)} {doorIndex}");
            text.AppendLine($"exit {Number(objective.ExitMarker.position.z)} {Number(objective.ExitMarker.position.x)} {Number(objective.ExitHalfWidth)}");
        }
        text.AppendLine("end");
        string result = text.ToString();
        string error = NativeRoomDiagnostics.Validate(result);
        if (error != null) throw new InvalidDataException(error);
        return result;
    }

    [MenuItem("Dreamcast/Export sample room")]
    public static void Export()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Export the authored room outside Play mode.");
        string text = BuildText(); // Validate everything before replacing the artifact.
        var audio = RoomAudioExporter.Build();
        var props = RoomPropExporter.Build();
        Directory.CreateDirectory(Application.streamingAssetsPath);
        string audioDirectory = Path.Combine(Application.streamingAssetsPath, "room-audio");
        Directory.CreateDirectory(audioDirectory);
        string propDirectory = Path.Combine(Application.streamingAssetsPath, "prop-textures");
        Directory.CreateDirectory(propDirectory);
        foreach (var pair in props.textures) {
            string asset = Path.Combine(propDirectory, pair.Key);
            if (!File.Exists(asset) || !File.ReadAllBytes(asset).SequenceEqual(pair.Value)) File.WriteAllBytes(asset, pair.Value);
        }
        File.WriteAllBytes(Path.Combine(Application.streamingAssetsPath, "sample.props"), props.manifest);
        // Content-addressed clips are immutable. Old unreferenced exports are not packaged.
        foreach (var pair in audio.files) {
            string asset = Path.Combine(audioDirectory, pair.Key);
            if (!File.Exists(asset) || !File.ReadAllBytes(asset).SequenceEqual(pair.Value)) File.WriteAllBytes(asset, pair.Value);
        }
        string path = Path.Combine(Application.streamingAssetsPath, NativeGameBridge.RoomFileName);
        string manifest = Path.Combine(Application.streamingAssetsPath, "sample.audio");
        File.WriteAllText(manifest, audio.text, new UTF8Encoding(false));
        File.WriteAllText(path, text, new UTF8Encoding(false));
        if (File.ReadAllText(path) != text) throw new IOException("Room export verification failed.");
        AssetDatabase.Refresh();
        Debug.Log($"Exported room: {path}; {audio.files.Count} sound assets; {props.triangles}/4096 static prop triangles, {props.parts}/32 parts, {props.textureBytes}/524288 prop texture bytes.");
    }
}
