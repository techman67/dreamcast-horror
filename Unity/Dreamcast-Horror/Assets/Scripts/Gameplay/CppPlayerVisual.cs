using System.Runtime.InteropServices;
using UnityEngine;

public sealed class CppPlayerVisual : MonoBehaviour
{
    [Tooltip("Visual vertical offset to account for mesh pivot (e.g. 0.9m for a 1.8m capsule with center pivot).")]
    [SerializeField] private float verticalOffset = 0.9f;
    public Vector3 AuthoredSpawnPosition => transform.position - Vector3.up * verticalOffset;

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_x();

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_y();

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_z();

    private void LateUpdate()
    {
        if (!NativeGameBridge.IsInitialized) return;
        transform.position = new Vector3(
            unity_game_get_player_x(),
            unity_game_get_player_y() + verticalOffset,
            unity_game_get_player_z());
    }
}

