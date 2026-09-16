using System.Runtime.InteropServices;
using UnityEngine;

public static class NativeCollision
{
    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_collision_clear();

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_collision_add_box(
            float centerX,
            float centerY,
            float centerZ,
            float halfX,
            float halfY,
            float halfZ);

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_collision_add_sphere(
            float centerX,
            float centerY,
            float centerZ,
            float radius);

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_collision_add_capsule(
            float centerX,
            float centerY,
            float centerZ,
            float radius,
            float height);

    public static void Clear()
    {
        unity_game_collision_clear();
    }

    public static void AddBox(
        Vector3 center,
        Vector3 halfExtents)
    {
        unity_game_collision_add_box(
            center.x,
            center.y,
            center.z,
            halfExtents.x,
            halfExtents.y,
            halfExtents.z);
    }

    public static void AddSphere(
        Vector3 center,
        float radius)
    {
        unity_game_collision_add_sphere(
            center.x,
            center.y,
            center.z,
            radius);
    }

    public static void AddCapsule(
        Vector3 center,
        float radius,
        float height)
    {
        unity_game_collision_add_capsule(
            center.x,
            center.y,
            center.z,
            radius,
            height);
    }
}