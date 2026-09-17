using UnityEngine;

// Authoring only: a planar RGB565 contact map is baked into the base texture.
[DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
[AddComponentMenu("Dreamcast/Contact Shadow Surface")]
public sealed class DreamcastContactSurface : MonoBehaviour
{
    [Tooltip("Texture width/height. Use 64, 128 or 256. A 256 map costs 128 KiB of the shared 512 KiB texture budget.")]
    public int resolution = 256;
    [Range(0,1), Tooltip("Local contact darkening. Zero disables the contact map.")]
    public float strength = .65f;
    [Range(.05f,2), Tooltip("How far nearby static geometry can darken this floor, in metres.")]
    public float distance = .55f;
    [Range(8,64), Tooltip("Hemisphere rays per texel at export only.")]
    public int samples = 32;
}
