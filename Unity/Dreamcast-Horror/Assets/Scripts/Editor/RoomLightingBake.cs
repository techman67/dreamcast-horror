using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// A deliberately small diffuse baker, not Unity's lightmapper. Output is ordinary
// vertex color, multiplied by the texture by both fixed-function renderers.
public sealed class RoomLightingBake
{
    readonly DreamcastBakedLighting settings;
    readonly Light[] lights;
    public float MaxEdge => settings.maxTriangleEdge;
    public static RoomLightingBake FromScene()
    {
        var settings = UnityEngine.Object.FindObjectsByType<DreamcastBakedLighting>().Where(x => x.isActiveAndEnabled).ToArray();
        if (settings.Length > 1) throw new InvalidDataException("Use only one active Dreamcast Baked Room Lighting component per room.");
        return settings.Length == 0 ? null : new RoomLightingBake(settings[0]);
    }
    static bool ValidColor(Color c) => float.IsFinite(c.r) && float.IsFinite(c.g) && float.IsFinite(c.b) &&
        c.r >= 0 && c.r <= 1 && c.g >= 0 && c.g <= 1 && c.b >= 0 && c.b <= 1;
    RoomLightingBake(DreamcastBakedLighting value)
    {
        settings = value;
        if (!ValidColor(settings.ambient) || !float.IsFinite(MaxEdge) || MaxEdge < .5f || MaxEdge > 8)
            throw new InvalidDataException("Baked lighting needs ambient RGB in 0..1 and maximum triangle edge in 0.5..8 metres.");
        lights = UnityEngine.Object.FindObjectsByType<Light>().Where(x => x.isActiveAndEnabled)
            .OrderBy(x => GlobalObjectId.GetGlobalObjectIdSlow(x).ToString(), StringComparer.Ordinal).ToArray();
        if (lights.Length > 16) throw new InvalidDataException("Room lighting exceeds 16 baked lights. Disable or remove unused lights.");
        foreach (var light in lights) {
            if (light.type != LightType.Directional && light.type != LightType.Point && light.type != LightType.Spot)
                throw new InvalidDataException("Light '" + light.name + "': only Directional, Point and Spot lights bake; area lights are unsupported.");
            if (light.cookie != null) throw new InvalidDataException("Light '" + light.name + "' uses a cookie. Remove it; projected light textures are not supported.");
            if (!ValidColor(light.color) || !float.IsFinite(light.intensity) || light.intensity < 0 || light.intensity > 8 ||
                !float.IsFinite(light.range) || light.range <= 0 || light.range > 100 ||
                !float.IsFinite(light.spotAngle) || !float.IsFinite(light.innerSpotAngle) ||
                light.innerSpotAngle < 0 || light.innerSpotAngle > light.spotAngle || light.spotAngle <= 0 || light.spotAngle >= 180)
                throw new InvalidDataException("Light '" + light.name + "': use RGB 0..1, intensity 0..8, range >0..100m and valid spot angles below 180 degrees.");
            if (light.shadows != LightShadows.None)
                Debug.LogWarning("Light '" + light.name + "': cast shadows are not baked yet. Only direct diffuse light and ambient are exported.", light);
        }
    }
    public Color Sample(Vector3 position, Vector3 normal, int layer)
    {
        if (!float.IsFinite(normal.sqrMagnitude) || normal.sqrMagnitude < 1e-12f)
            throw new InvalidDataException("Baked lighting encountered a missing/zero normal. Recalculate mesh normals before export.");
        normal.Normalize();
        Color value = settings.ambient;
        foreach (var light in lights) {
            if ((light.cullingMask & (1 << layer)) == 0) continue;
            Vector3 direction = -light.transform.forward;
            float attenuation = 1;
            if (light.type != LightType.Directional) {
                Vector3 delta = light.transform.position - position;
                float distance = delta.magnitude;
                if (distance >= light.range) continue;
                direction = distance > .00001f ? delta / distance : normal;
                float falloff = 1 - distance / light.range;
                attenuation = falloff * falloff;
                if (light.type == LightType.Spot) {
                    float outer = Mathf.Cos(light.spotAngle * .5f * Mathf.Deg2Rad);
                    float inner = Mathf.Cos(light.innerSpotAngle * .5f * Mathf.Deg2Rad);
                    float cone = Vector3.Dot(-direction, light.transform.forward);
                    attenuation *= inner - outer < .00001f ? (cone >= outer ? 1 : 0) : Mathf.SmoothStep(0, 1, Mathf.Clamp01((cone - outer) / (inner - outer)));
                }
            }
            Color color = light.color;
            if (light.useColorTemperature) color *= Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);
            value += color * (light.intensity * attenuation * Mathf.Max(0, Vector3.Dot(normal, direction)));
        }
        // The vertex format cannot store HDR. Scale all channels together when
        // overbright instead of clipping each channel and whitening warm lights.
        float peak = Mathf.Max(1, value.r, value.g, value.b);
        return new Color(value.r / peak, value.g / peak, value.b / peak, 1);
    }
}

[CustomEditor(typeof(DreamcastBakedLighting))]
public sealed class DreamcastBakedLightingInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Export / Rebuild CDI bakes active Directional, Point and Spot lights into static room vertices. Moving objects keep their current appearance. No cast shadows, bounce light or Unity skybox lighting yet. Unlit materials stay unlit. Smaller triangle edges use more of the shared 4,096-triangle budget.", MessageType.Info);
        DrawDefaultInspector();
    }
}
