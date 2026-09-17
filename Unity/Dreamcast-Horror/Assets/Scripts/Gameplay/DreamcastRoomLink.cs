using UnityEngine;

[DisallowMultipleComponent]
public sealed class DreamcastRoomLink : MonoBehaviour
{
    [Range(1,8)] public int destinationRoom=1;
    public string destinationArrival="Entrance";
    [Tooltip("Distance from player feet to this marker. Press E / A to enter.")]
    [Range(.25f,3)] public float interactionRange=1.25f;
    [Tooltip("Require this room's prototype key door to be unlocked first.")]
    public bool requiresOpenDoor;
    void OnDrawGizmosSelected() { Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(transform.position,interactionRange); }
}
