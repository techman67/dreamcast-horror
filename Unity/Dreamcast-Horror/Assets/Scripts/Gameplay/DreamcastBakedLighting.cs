using UnityEngine;

// Authoring settings only. Exported vertices contain the finished light; no Unity
// light objects or lighting APIs are used by the portable game or Dreamcast host.
[DisallowMultipleComponent]
[AddComponentMenu("Dreamcast/Baked Room Lighting")]
public sealed class DreamcastBakedLighting : MonoBehaviour
{
    [Tooltip("Minimum surface illumination. This is independent of Unity's skybox/environment lighting.")]
    public Color ambient = new Color(.16f, .18f, .22f, 1);
    [Range(.5f, 8f), Tooltip("Split large triangles at export so point/spot lights have enough vertices. Smaller values cost more triangles.")]
    public float maxTriangleEdge = 2f;
    [Tooltip("Bake light blocking from static mesh surfaces. Each light must also enable Shadows. No Colliders are needed.")]
    public bool bakeShadows = true;
    [Range(.0001f, .05f), Tooltip("World-space ray offset to prevent self-shadow speckles. Too large can leak light through thin geometry.")]
    public float shadowBias = .002f;
    [Range(0, 1), Tooltip("Darken ambient light near static geometry. Zero disables ambient occlusion.")]
    public float occlusionStrength = .35f;
    [Range(.05f, 4), Tooltip("Distance in metres searched for ambient occlusion.")]
    public float occlusionDistance = .75f;
    [Range(4, 32), Tooltip("Rays per vertex at export only. More rays reduce directional sampling artifacts.")]
    public int occlusionSamples = 12;
    [Range(4, 32), Tooltip("Samples per rectangular/disc light at export. More samples smooth partial shadows; no additional runtime lights or vertices.")]
    public int areaSamples = 16;
}
