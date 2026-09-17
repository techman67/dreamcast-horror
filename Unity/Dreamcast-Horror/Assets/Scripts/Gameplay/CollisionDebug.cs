using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Always-on visualization of the C++ collision setup.
//
// Add this component to any GameObject in the scene.
// It draws every CppCollision in the scene (not just the
// selected one) plus the live C++ player position.
//
// Toggle with the "Enabled" checkbox, or from code via
// CollisionDebug.Enabled = false;
[ExecuteAlways]
public sealed class CollisionDebug : MonoBehaviour
{
    public static bool Enabled = true;

    [Header("Colors")]

    public Color boxColor =
        new Color(0.25f, 0.95f, 0.25f, 1.0f);

    public Color sphereColor =
        new Color(0.95f, 0.95f, 0.25f, 1.0f);

    public Color capsuleColor =
        new Color(0.95f, 0.45f, 0.95f, 1.0f);

    public Color playerCircleColor =
        new Color(1.00f, 0.35f, 0.35f, 1.0f);

    [Header("Player radius (must match native)")]

    public float playerCollisionRadius =
        0.35f;

    [Header("Also show fill, not just wireframe")]

    public bool drawFilled =
        true;

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern float
        unity_game_get_player_x();

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern float
        unity_game_get_player_y();

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern float
        unity_game_get_player_z();

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_radius();

    private void OnDrawGizmos()
    {
        if (!Enabled)
            return;

        if (Application.isPlaying)
        {
            if (!NativeGameBridge.IsInitialized || NativeGameBridge.LoadedShapes == null) return;
            for (int i = 0; i < NativeGameBridge.LoadedShapes.Length; ++i)
            {
                if (NativeGameBridge.SliceView.DoorOpen && i == NativeGameBridge.SliceBindings.doorShapeIndex) continue;
                DrawShape(NativeGameBridge.LoadedShapes[i]);
            }
        }
        else
        {
            // No scene searches, vertex copies or native calls during edit-mode repaint.
            for (int i = 0; i < CppCollision.ActiveShapes.Count; ++i)
            {
                CppCollision c = CppCollision.ActiveShapes[i];
                if (c == null) continue;
                DrawShape(new NativeRoomDiagnostics.Shape {
                    type = (uint)c.ResolvedMode - 1,
                    center = c.WorldCenter, halfExtents = c.WorldSize * 0.5f,
                    radius = c.Radius, height = c.Height
                });
            }
        }

        DrawPlayer();
    }

    private void DrawShape(NativeRoomDiagnostics.Shape c)
    {
        switch (c.type)
        {
            case 0:

                Gizmos.color = boxColor;

                Gizmos.DrawWireCube(
                    c.center,
                    (c.halfExtents * 2.0f));

                if (drawFilled)
                {
                    Color fill = boxColor;
                    fill.a = 0.10f;
                    Gizmos.color = fill;

                    Gizmos.DrawCube(
                        c.center,
                        (c.halfExtents * 2.0f));
                }

                break;

            case 1:

                Gizmos.color = sphereColor;

                Gizmos.DrawWireSphere(
                    c.center,
                    c.radius);

                if (drawFilled)
                {
                    Color fill = sphereColor;
                    fill.a = 0.10f;
                    Gizmos.color = fill;

                    Gizmos.DrawSphere(
                        c.center,
                        c.radius);
                }

                break;

            case 2:

                Gizmos.color = capsuleColor;

                // The C++ solver treats a capsule as a circle
                // in X/Z of radius "Radius". Draw both the
                // full 3D capsule and the flat horizontal
                // circle the solver actually uses.

                float cylinderHeight =
                    Mathf.Max(
                        0.0f,
                        c.height -
                        c.radius * 2.0f);

                Vector3 top =
                    c.center +
                    Vector3.up *
                    (cylinderHeight * 0.5f);

                Vector3 bottom =
                    c.center -
                    Vector3.up *
                    (cylinderHeight * 0.5f);

                Gizmos.DrawWireSphere(
                    top,
                    c.radius);

                Gizmos.DrawWireSphere(
                    bottom,
                    c.radius);

                Gizmos.DrawLine(
                    top + Vector3.right * c.radius,
                    bottom + Vector3.right * c.radius);

                Gizmos.DrawLine(
                    top - Vector3.right * c.radius,
                    bottom - Vector3.right * c.radius);

                Gizmos.DrawLine(
                    top + Vector3.forward * c.radius,
                    bottom + Vector3.forward * c.radius);

                Gizmos.DrawLine(
                    top - Vector3.forward * c.radius,
                    bottom - Vector3.forward * c.radius);

                // The horizontal circle the solver actually uses.
                DrawCircle(
                    new Vector3(
                        c.center.x,
                        0.0f,
                        c.center.z),
                    c.radius);

                break;
        }
    }

    private void DrawPlayer()
    {
        if (!Application.isPlaying || !NativeGameBridge.IsInitialized) return;

        Vector3 p;
        try
        {
            p = new Vector3(unity_game_get_player_x(), unity_game_get_player_y(), unity_game_get_player_z());
            playerCollisionRadius = unity_game_get_player_radius();
        }
        catch (DllNotFoundException) { return; }
        catch (EntryPointNotFoundException) { return; }
        catch (BadImageFormatException) { return; }

        Gizmos.color = playerCircleColor;

        // Small marker at the C++ position.
        Gizmos.DrawWireSphere(p, 0.05f);

        // The actual collision footprint used by the solver.
        DrawCircle(
            new Vector3(p.x, 0.0f, p.z),
            playerCollisionRadius);

        // Vertical line so you can see the player through
        // other geometry.
        Gizmos.DrawLine(
            p,
            p + Vector3.up * 2.0f);
    }

    private static void DrawCircle(
        Vector3 center,
        float radius)
    {
        const int segments = 48;

        Vector3 prev =
            center +
            new Vector3(radius, 0.0f, 0.0f);

        for (int i = 1; i <= segments; ++i)
        {
            float t =
                (float)i /
                (float)segments *
                Mathf.PI * 2.0f;

            Vector3 next =
                center +
                new Vector3(
                    Mathf.Cos(t) * radius,
                    0.0f,
                    Mathf.Sin(t) * radius);

            Gizmos.DrawLine(prev, next);

            prev = next;
        }
    }
}
