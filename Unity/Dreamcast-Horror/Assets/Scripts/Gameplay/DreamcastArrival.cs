using UnityEngine;

[DisallowMultipleComponent]
public sealed class DreamcastArrival : MonoBehaviour
{
    [Tooltip("Unique name in this room. Place the marker at the player's feet, clear of walls.")]
    public string arrivalName="Entrance";
    [Range(1,2)] public int cameraId=1;
    void OnDrawGizmosSelected() { Gizmos.color=Color.cyan; Gizmos.DrawWireSphere(transform.position,.35f); Gizmos.DrawLine(transform.position,transform.position+Vector3.up*1.8f); }
}
