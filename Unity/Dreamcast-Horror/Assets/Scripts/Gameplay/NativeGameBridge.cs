using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class NativeGameBridge :
    MonoBehaviour
{
    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_init();

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_step(
            float moveX,
            float moveY,
            float deltaSeconds);

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

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern uint
        unity_game_get_camera_id();

    [Header("Authored Gameplay Cameras")]

    [SerializeField]
    private Camera gameplayCamera01;

    [SerializeField]
    private Camera gameplayCamera02;

    private uint lastCameraId = 0;

    private void Start()
    {
        ResolveAuthoredCameras();

        BuildNativeCollision();

        unity_game_init();

        Debug.Log(
            $"C++ game initialized. " +
            $"Player position: " +
            $"({unity_game_get_player_x():F3}, " +
            $"{unity_game_get_player_y():F3}, " +
            $"{unity_game_get_player_z():F3})");

        ApplyActiveCamera(true);
    }

    private void Update()
    {
        float moveX = 0.0f;
        float moveY = 0.0f;

        Keyboard keyboard =
            Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed)
                moveX -= 1.0f;

            if (keyboard.dKey.isPressed)
                moveX += 1.0f;

            if (keyboard.sKey.isPressed)
                moveY -= 1.0f;

            if (keyboard.wKey.isPressed)
                moveY += 1.0f;
        }

        unity_game_step(
            moveX,
            moveY,
            Time.deltaTime);

        ApplyActiveCamera(false);
    }

    private void BuildNativeCollision()
    {
        NativeCollision.Clear();

        CppCollision[] collisions =
            FindObjectsByType<CppCollision>(
                FindObjectsInactive.Exclude);

        foreach (CppCollision collision
                 in collisions)
        {
            collision.SendToNative();
        }

        Debug.Log(
            $"Native collision built from " +
            $"{collisions.Length} " +
            $"CppCollision components.");
    }

    private void ResolveAuthoredCameras()
    {
        if (gameplayCamera01 == null)
        {
            GameObject cameraObject =
                GameObject.Find(
                    "GameplayCamera_01");

            if (cameraObject != null)
            {
                gameplayCamera01 =
                    cameraObject.GetComponent<
                        Camera>();
            }
        }

        if (gameplayCamera02 == null)
        {
            GameObject cameraObject =
                GameObject.Find(
                    "GameplayCamera_02");

            if (cameraObject != null)
            {
                gameplayCamera02 =
                    cameraObject.GetComponent<
                        Camera>();
            }
        }

        if (gameplayCamera01 == null)
        {
            Debug.LogError(
                "GameplayCamera_01 was not found.");
        }

        if (gameplayCamera02 == null)
        {
            Debug.LogError(
                "GameplayCamera_02 was not found.");
        }
    }

    private void ApplyActiveCamera(
        bool force)
    {
        uint cameraId =
            unity_game_get_camera_id();

        if (!force &&
            cameraId == lastCameraId)
            return;

        lastCameraId =
            cameraId;

        switch (cameraId)
        {
            case 1:

                SetActiveCamera(
                    gameplayCamera01);

                Debug.Log(
                    "C++ selected " +
                    "GameplayCamera_01");

                break;

            case 2:

                SetActiveCamera(
                    gameplayCamera02);

                Debug.Log(
                    "C++ selected " +
                    "GameplayCamera_02");

                break;

            default:

                Debug.LogError(
                    $"C++ selected unknown " +
                    $"camera ID: {cameraId}");

                break;
        }
    }

    private void SetActiveCamera(
        Camera activeCamera)
    {
        if (activeCamera == null)
        {
            Debug.LogError(
                "Requested gameplay camera " +
                "is missing.");

            return;
        }

        if (gameplayCamera01 != null)
        {
            gameplayCamera01.enabled =
                activeCamera ==
                gameplayCamera01;
        }

        if (gameplayCamera02 != null)
        {
            gameplayCamera02.enabled =
                activeCamera ==
                gameplayCamera02;
        }
    }
}