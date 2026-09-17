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
}
