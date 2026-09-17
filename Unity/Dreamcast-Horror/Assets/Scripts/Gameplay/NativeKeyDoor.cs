using System.Runtime.InteropServices;
using UnityEngine;

public static class NativeKeyDoor
{
    public enum Prompt : uint { None, TakeKey, LockedDoor, UnlockDoor, OpenDoor, Complete }
    public enum Feedback : uint { None, DoorLocked, KeyTaken, DoorUnlocked, Completed }

    [StructLayout(LayoutKind.Sequential)]
    public struct Bindings
    {
        public uint keyActor, doorActor;
        public Vector3 keyPosition, doorPosition;
        public uint doorShapeIndex;
        public float keyRange, doorRange, exitBoundaryZ, exitCenterX, exitHalfWidthX;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct View
    {
        public uint flags;
        public Prompt prompt;
        public Feedback feedback;
        public bool HasKey => (flags & 1) != 0;
        public bool DoorOpen => (flags & 2) != 0;
        public bool Complete => (flags & 4) != 0;
        public bool Enabled => (flags & 8) != 0;
    }

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_game_get_key_door_bindings(out Bindings bindings);

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_game_get_key_door_view(out View view);

    public static Bindings ReadBindings() { unity_game_get_key_door_bindings(out Bindings result); return result; }
    public static View ReadView() { unity_game_get_key_door_view(out View result); return result; }
}
