using UnityEngine;

[DisallowMultipleComponent, AddComponentMenu("Dreamcast/Save Point")]
public sealed class DreamcastSavePoint : MonoBehaviour
{
    [Min(1),Tooltip("Unique within this room. Duplicate IDs are rejected on export.")]
    public int pointId=1;
    [Tooltip("Short action text, 1-47 printable English/ASCII characters. Controls are added automatically.")]
    public string prompt="Save progress";
    [Range(.25f,3),Tooltip("Distance from the player's centre to this interaction point, in metres.")]
    public float interactionRange=1.5f;
    [Tooltip("Optional interaction position. Use a child near the book/typewriter; otherwise uses this object's origin.")]
    public Transform interactionPoint;
    public Vector3 Position => interactionPoint!=null ? interactionPoint.position : transform.position;
    void OnDrawGizmosSelected() { Gizmos.color=Color.cyan; Gizmos.DrawWireSphere(Position,interactionRange); }
}
