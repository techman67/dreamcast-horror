using UnityEngine;

[AddComponentMenu("Dreamcast/Placed Room Sound")]
[DisallowMultipleComponent]
public sealed class DreamcastRoomSound : MonoBehaviour
{
    public AudioClip clip;
    [Range(0, 1)] public float volume = 1;
    [Tooltip("Loop continuously, or play once when entering/resetting the room.")]
    public bool loop = true;
    [Tooltip("0 = room-wide. Otherwise sound becomes silent at this distance from the player. No panning/reverb.")]
    [Min(0)] public float audibleRange = 2;
    [Tooltip("Keep the chosen volume up to this distance, then fade to silence at Audible Range.")]
    [Min(0)] public float fullVolumeRange;

    private void Reset() { audibleRange = 2; fullVolumeRange = .5f; }
}
