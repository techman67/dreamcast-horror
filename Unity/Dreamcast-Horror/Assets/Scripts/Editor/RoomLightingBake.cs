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
    readonly RoomShadowGeometry geometry;
    readonly Light effectLight;
    public DreamcastLightEffect Effect { get; private set; }
    public float MaxEdge => settings.maxTriangleEdge;
    public static bool IsArea(Light light) => light.type == LightType.Rectangle || light.type == LightType.Disc;
    public static RoomLightingBake FromScene()
    {
        var settings = UnityEngine.Object.FindObjectsByType<DreamcastBakedLighting>().Where(x => x.isActiveAndEnabled).ToArray();
        if (settings.Length > 1) throw new InvalidDataException("Use only one active Dreamcast Baked Room Lighting component per room.");
        if (settings.Length == 0 && UnityEngine.Object.FindObjectsByType<DreamcastLightEffect>().Any(x => x.isActiveAndEnabled))
            throw new InvalidDataException("Light effects need an active Dreamcast Baked Room Lighting component. Add one from Dreamcast > Lighting.");
        return settings.Length == 0 ? null : new RoomLightingBake(settings[0]);
    }
    static bool ValidColor(Color c) => float.IsFinite(c.r) && float.IsFinite(c.g) && float.IsFinite(c.b) &&
        c.r >= 0 && c.r <= 1 && c.g >= 0 && c.g <= 1 && c.b >= 0 && c.b <= 1;
    RoomLightingBake(DreamcastBakedLighting value)
    {
        settings = value;
        if (!ValidColor(settings.ambient) || !float.IsFinite(MaxEdge) || MaxEdge < .5f || MaxEdge > 8)
            throw new InvalidDataException("Baked lighting needs ambient RGB in 0..1 and maximum triangle edge in 0.5..8 metres.");
        if (!float.IsFinite(settings.shadowBias) || settings.shadowBias < .0001f || settings.shadowBias > .05f ||
            !float.IsFinite(settings.occlusionStrength) || settings.occlusionStrength < 0 || settings.occlusionStrength > 1 ||
            !float.IsFinite(settings.occlusionDistance) || settings.occlusionDistance < .05f || settings.occlusionDistance > 4 ||
            settings.occlusionSamples < 4 || settings.occlusionSamples > 32 || settings.areaSamples < 4 || settings.areaSamples > 32)
            throw new InvalidDataException("Shadow bias must be 0.0001-0.05m; occlusion strength 0-1, distance 0.05-4m; AO and area samples must be 4-32.");
        lights = UnityEngine.Object.FindObjectsByType<Light>().Where(x => x.isActiveAndEnabled)
            .OrderBy(x => GlobalObjectId.GetGlobalObjectIdSlow(x).ToString(), StringComparer.Ordinal).ToArray();
        if (lights.Length > 16) throw new InvalidDataException("Room lighting exceeds 16 baked lights. Disable or remove unused lights.");
        var effects = UnityEngine.Object.FindObjectsByType<DreamcastLightEffect>().Where(x => x.isActiveAndEnabled).ToArray();
        if (effects.Length > 1) throw new InvalidDataException("This Dreamcast pass supports one effect source per room. Disable extra Light Effect components; ordinary static lights may remain.");
        Effect = effects.FirstOrDefault();
        if (Effect != null) {
            if (Application.isPlaying) throw new InvalidDataException("Exit Play mode before exporting light effects so authored brightness is preserved.");
            effectLight = Effect.GetComponent<Light>();
            if (!lights.Contains(effectLight)) throw new InvalidDataException("Light Effect '" + Effect.name + "' needs its Light enabled. Use Activation Range to switch it by proximity.");
            if ((uint)Effect.mode > 2 || !float.IsFinite(Effect.frequency) || Effect.frequency < .1f || Effect.frequency > 10 ||
                !float.IsFinite(Effect.minimumBrightness) || Effect.minimumBrightness < 0 || Effect.minimumBrightness > 1 ||
                !float.IsFinite(Effect.activationRange) || Effect.activationRange < 0 || Effect.activationRange > 100 || Effect.seed < 0 || Effect.seed > 65535)
                throw new InvalidDataException("Light Effect '" + Effect.name + "': frequency must be 0.1-10 Hz, minimum brightness 0-1, activation range 0-100m and seed 0-65535.");
            var objective = UnityEngine.Object.FindAnyObjectByType<KeyDoorPresentation>();
            if (Effect.GetComponentInParent<CppPlayerVisual>() != null ||
                (Effect.GetComponentInParent<Animator>() is Animator animator && animator.runtimeAnimatorController != null) ||
                (objective != null && ((objective.KeyVisual != null && Effect.transform.IsChildOf(objective.KeyVisual)) ||
                (objective.DoorVisual != null && Effect.transform.IsChildOf(objective.DoorVisual)))))
                throw new InvalidDataException("Light Effect '" + Effect.name + "' must stay stationary. Moving/attached player lights need a separate runtime feature.");
            if (Effect.glowingSurfaces == null || Effect.glowingSurfaces.Length > 8 || Effect.glowingSurfaces.Distinct().Count() != Effect.glowingSurfaces.Length)
                throw new InvalidDataException("Light Effect glowing surfaces must contain up to eight unique static mesh renderers.");
            foreach (var glow in Effect.glowingSurfaces)
                if (glow == null || !glow.enabled || !glow.gameObject.activeInHierarchy || RoomPropExporter.IsDynamic(glow) ||
                    glow.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || glow.GetComponent<MeshFilter>()?.sharedMesh == null)
                    throw new InvalidDataException("Light Effect has a missing, hidden or moving glowing surface. Assign an enabled static mesh renderer.");
        }
        foreach (var light in lights) {
            if (light.type != LightType.Directional && light.type != LightType.Point && light.type != LightType.Spot && !IsArea(light))
                throw new InvalidDataException("Light '" + light.name + "': use Directional, Point, Spot, Rectangle or Disc.");
            if (light.cookie != null) throw new InvalidDataException("Light '" + light.name + "' uses a cookie. Remove it; projected light textures are not supported.");
            if (!ValidColor(light.color) || !float.IsFinite(light.intensity) || light.intensity < 0 || light.intensity > 8 ||
                !float.IsFinite(light.range) || light.range <= 0 || light.range > 100 ||
                !float.IsFinite(light.spotAngle) || !float.IsFinite(light.innerSpotAngle) ||
                light.innerSpotAngle < 0 || light.innerSpotAngle > light.spotAngle || light.spotAngle <= 0 || light.spotAngle >= 180)
                throw new InvalidDataException("Light '" + light.name + "': use RGB 0..1, intensity 0..8, range >0..100m and valid spot angles below 180 degrees.");
            var position = light.transform.position;
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z) ||
                Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.y), Mathf.Abs(position.z)) > 10000 ||
                !float.IsFinite(light.shadowStrength) || light.shadowStrength < 0 || light.shadowStrength > 1 ||
                (light.useColorTemperature && (!float.IsFinite(light.colorTemperature) || light.colorTemperature < 1000 || light.colorTemperature > 20000)))
                throw new InvalidDataException("Light '" + light.name + "': position must be within 10,000m, shadow strength 0-1, and temperature 1,000-20,000K.");
            if (IsArea(light) && (!float.IsFinite(light.areaSize.x) || !float.IsFinite(light.areaSize.y) ||
                light.areaSize.x < .01f || light.areaSize.x > 20 ||
                (light.type == LightType.Rectangle && (light.areaSize.y < .01f || light.areaSize.y > 20))))
                throw new InvalidDataException("Area light '" + light.name + "': rectangle width/height or disc radius must be 0.01-20 metres. Set Area Size; transform scale is not used.");
            if (settings.bakeShadows && light.shadows == LightShadows.Soft && !IsArea(light))
                Debug.LogWarning("Light '" + light.name + "': this vertex bake uses hard visibility, blended between vertices; Unity soft-shadow filtering is not exported.", light);
        }
        if (settings.bakeShadows || settings.occlusionStrength > 0 || UnityEngine.Object.FindObjectsByType<DreamcastContactSurface>().Any(x=>x.isActiveAndEnabled && x.strength>0)) geometry = new RoomShadowGeometry();
    }
    bool Blocked(Light light, Vector3 position, Vector3 normal, Vector3 source, bool receiveShadows)
    {
        if (!receiveShadows || !settings.bakeShadows || geometry == null || light.shadows == LightShadows.None) return false;
        var origin = position + normal * settings.shadowBias;
        var delta = source - origin;
        float distance = delta.magnitude;
        return distance > settings.shadowBias && geometry.Blocked(origin, delta / distance, distance - settings.shadowBias, light.cullingMask);
    }
    float Area(Light light, Vector3 position, Vector3 normal, bool receiveShadows)
    {
        float sum = 0;
        for (int i = 0; i < settings.areaSamples; ++i) {
            // Fixed low-discrepancy samples, normalized so sample count/size do
            // not multiply authored power. Rotation matters; scale does not.
            float u = (i + .5f) / settings.areaSamples;
            uint bits = (uint)i; float v = 0, weight = .5f;
            while (bits != 0) { v += (bits & 1) * weight; weight *= .5f; bits >>= 1; }
            float x, y;
            if (light.type == LightType.Disc) {
                float radius = Mathf.Sqrt(u) * light.areaSize.x, angle = 2 * Mathf.PI * v;
                x = radius * Mathf.Cos(angle); y = radius * Mathf.Sin(angle);
            } else { x = (u - .5f) * light.areaSize.x; y = (v - .5f) * light.areaSize.y; }
            var source = light.transform.position + light.transform.right * x + light.transform.up * y;
            var delta = source - position;
            float distance = delta.magnitude;
            if (distance >= light.range || distance < .00001f) continue;
            var direction = delta / distance;
            float diffuse = Mathf.Max(0, Vector3.Dot(normal, direction));
            float facing = Mathf.Max(0, Vector3.Dot(light.transform.forward, -direction));
            float falloff = 1 - distance / light.range;
            float contribution = diffuse * facing * falloff * falloff;
            if (contribution > 0 && Blocked(light, position, normal, source, receiveShadows)) contribution *= 1 - light.shadowStrength;
            sum += contribution;
        }
        return sum / settings.areaSamples;
    }
    float AmbientVisibility(Vector3 position, Vector3 normal)
        => SurfaceOcclusion(position, normal, settings.occlusionStrength, settings.occlusionDistance, settings.occlusionSamples);
    public float SurfaceOcclusion(Vector3 position, Vector3 normal, float strength, float distance, int samples)
    {
        if (geometry == null || strength == 0) return 1;
        var tangent = Vector3.Cross(Mathf.Abs(normal.y)<.9f ? Vector3.up : Vector3.right,normal).normalized;
        var bitangent = Vector3.Cross(normal,tangent);
        int blocked=0;
        for (int i=0;i<samples;++i) {
            // Fixed cosine-weighted hemisphere sequence: deterministic exports.
            float r=Mathf.Sqrt((i+.5f)/samples), angle=i*2.39996323f;
            var direction=tangent*(r*Mathf.Cos(angle))+bitangent*(r*Mathf.Sin(angle))+normal*Mathf.Sqrt(1-r*r);
            if(geometry.Blocked(position+normal*settings.shadowBias,direction,distance,-1)) ++blocked;
        }
        return 1-strength*blocked/samples;
    }
    public Color Sample(Vector3 position, Vector3 normal, int layer, bool receiveShadows = true, bool includeEffect = true, bool includeAmbientOcclusion = true)
    {
        if (!float.IsFinite(normal.sqrMagnitude) || normal.sqrMagnitude < 1e-12f)
            throw new InvalidDataException("Baked lighting encountered a missing/zero normal. Recalculate mesh normals before export.");
        normal.Normalize();
        Color value = settings.ambient * (receiveShadows && includeAmbientOcclusion ? AmbientVisibility(position,normal) : 1);
        foreach (var light in lights) {
            if (!includeEffect && light == effectLight) continue;
            if ((light.cullingMask & (1 << layer)) == 0) continue;
            Color color = light.color;
            if (light.useColorTemperature) color *= Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);
            if (IsArea(light)) {
                value += color * (light.intensity * Area(light, position, normal, receiveShadows));
                continue;
            }
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
            float diffuse = Mathf.Max(0, Vector3.Dot(normal, direction));
            if (diffuse <= 0 || attenuation <= 0) continue;
            if (receiveShadows && settings.bakeShadows && geometry != null && light.shadows != LightShadows.None) {
                var origin = position + normal * settings.shadowBias;
                var rayDirection = direction;
                float distance = 40000;
                if (light.type != LightType.Directional) {
                    var delta = light.transform.position-origin;
                    distance = delta.magnitude;
                    rayDirection = distance>.000001f ? delta/distance : direction;
                }
                if (geometry.Blocked(origin,rayDirection,distance-settings.shadowBias,light.cullingMask))
                    attenuation *= 1-light.shadowStrength;
            }
            value += color * (light.intensity * attenuation * diffuse);
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
        EditorGUILayout.HelpBox("Export / Rebuild CDI bakes Directional, Point, Spot, Rectangle and Disc lights into static room vertices. Area lights emit along their blue +Z axis; size is in metres, independent of transform scale. Enable Shadows on lights. Mesh Renderer controls blocking; no Colliders needed. Emission Color brightens its own material; use an area light for light spilling onto nearby surfaces. Moving lights and bounce light are not baked. Smaller triangle edges use more of the 4,096-triangle budget.", MessageType.Info);
        DrawDefaultInspector();
        if (GUILayout.Button("Export room with these lighting settings")) {
            try { RoomExporter.Export(); }
            catch (Exception error) { Debug.LogException(error); EditorUtility.DisplayDialog("Lighting export failed",error.Message,"OK"); }
        }
    }
}
