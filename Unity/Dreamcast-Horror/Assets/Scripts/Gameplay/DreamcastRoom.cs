using UnityEngine;

[DisallowMultipleComponent]
public sealed class DreamcastRoom : MonoBehaviour
{
    public DreamcastWorld world;
    [Range(1,8)] public int roomId=1;
}
