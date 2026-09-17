using UnityEngine;

public enum DreamcastCue { Footstep, KeyTaken, DoorLocked, DoorUnlocked }

[AddComponentMenu("Dreamcast/Gameplay Sound Cue")]
public sealed class DreamcastSoundCue : MonoBehaviour
{
    [Tooltip("One assignment per event in the room. Gameplay decides when it fires.")]
    public DreamcastCue cue;
    public AudioClip clip;
    [Range(0, 1)] public float volume = 1;
}
