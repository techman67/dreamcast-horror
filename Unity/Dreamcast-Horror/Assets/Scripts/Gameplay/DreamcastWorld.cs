using System;
using UnityEngine;

[CreateAssetMenu(menuName="Dreamcast/Rooms/World",fileName="DreamcastWorld")]
public sealed class DreamcastWorld : ScriptableObject
{
    [Serializable] public sealed class Room { [Range(1,8)] public int id=1; public string scenePath=""; }
    [Range(1,8)] public int startingRoom=1;
    public Room[] rooms=Array.Empty<Room>();
}
