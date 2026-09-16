using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Read-only inspection of the loaded room; no gameplay or authoring policy.
public static class NativeRoomDiagnostics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Shape
    {
        public uint type;
        public Vector3 center;
        public Vector3 halfExtents;
        public float radius;
        public float height;
    }

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_game_validate_room([MarshalAs(UnmanagedType.LPStr)] string text);

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint unity_game_get_shape_count();

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern int unity_game_get_shape(uint index, out Shape shape);

    public static string Validate(string text) => Marshal.PtrToStringAnsi(unity_game_validate_room(text));

    public static Shape[] Snapshot()
    {
        var result = new Shape[unity_game_get_shape_count()];
        for (uint i = 0; i < result.Length; ++i)
            if (unity_game_get_shape(i, out result[i]) == 0)
                throw new InvalidOperationException("Native room changed while reading collision shapes.");
        return result;
    }
}
