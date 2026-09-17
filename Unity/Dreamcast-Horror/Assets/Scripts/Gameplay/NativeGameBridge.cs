using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class NativeGameBridge :
    MonoBehaviour
{
    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern int
        unity_game_init([MarshalAs(UnmanagedType.LPStr)] string roomText);

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_step_v2(
            float moveX,
            float moveY,
            float deltaSeconds,
            int interactPressed, int jumpPressed);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePose
    {
        public float x, y, z, yaw, pitch, roll;
    }

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_game_get_camera_pose(out NativePose pose);

    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_game_get_error();

    public static bool IsInitialized { get; private set; }
    public Vector2 LastMovementInput { get; private set; }
    public const string RoomFileName = "sample.room";

    [Header("Room export settings (runtime reads sample.room)")]
    [SerializeField, Min(0.001f)] private float authoredPlayerRadius = 0.35f;
    [SerializeField, Range(1, 2)] private int authoredInitialCamera = 1;
    public float AuthoredPlayerRadius => authoredPlayerRadius;
    public int AuthoredInitialCamera => authoredInitialCamera;

    public static NativeRoomDiagnostics.Shape[] LoadedShapes { get; private set; }

    [Header("Authored Gameplay Cameras")]

    [SerializeField]
    private Camera gameplayCamera01;

    [SerializeField]
    private Camera gameplayCamera02;

    private uint lastCameraId = 0;
    private bool initialized;
    private RoomAudio audioPresentation;
    private KeyDoorPresentation slicePresentation;
    public static NativeKeyDoor.Bindings SliceBindings { get; private set; }
    public static NativeKeyDoor.View SliceView { get; private set; }

    private void OnEnable()
    {
        // Re-enabling the host resumes the same native room and debug snapshot.
        if (initialized) { IsInitialized = true; audioPresentation?.Reset(); }
    }

    private void Start()
    {
        audioPresentation?.Dispose();
        audioPresentation = null;
        initialized = false;
        IsInitialized = false;
        LoadedShapes = null;
        LastMovementInput = Vector2.zero;
        ResolveAuthoredCameras();
        if (gameplayCamera01 == null || gameplayCamera02 == null)
        {
            enabled = false;
            return;
        }

        try
        {
            string roomText = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, RoomFileName));
            if (unity_game_init(roomText) == 0)
                throw new InvalidDataException(Marshal.PtrToStringAnsi(unity_game_get_error()));
            LoadedShapes = NativeRoomDiagnostics.Snapshot();
            SliceBindings = NativeKeyDoor.ReadBindings();
            SliceView = NativeKeyDoor.ReadView();
            slicePresentation = FindAnyObjectByType<KeyDoorPresentation>();
            if (SliceView.Enabled)
            {
                if (slicePresentation == null || !slicePresentation.HasVisualBindings ||
                    slicePresentation.KeyActorId != SliceBindings.keyActor ||
                    slicePresentation.DoorActorId != SliceBindings.doorActor)
                    throw new InvalidDataException("The exported key and door require matching presentation bindings.");
                slicePresentation.Initialize(SliceBindings);
            }
            RefreshSlicePresentation();
            audioPresentation = new RoomAudio(transform);
            initialized = true;
            IsInitialized = true;
        }
        catch (Exception error)
        {
            Debug.LogError($"Unable to initialize native game: {error.Message}", this);
            enabled = false;
            return;
        }

        Debug.Log(
            $"C++ game initialized. " +
            $"Player position: " +
            $"({unity_game_get_player_x():F3}, " +
            $"{unity_game_get_player_y():F3}, " +
            $"{unity_game_get_player_z():F3})");

        ApplyActiveCamera(true);
    }

    private void OnDisable()
    {
        audioPresentation?.Stop();
        IsInitialized = false;
    }

    private void OnDestroy()
    {
        audioPresentation?.Dispose();
        IsInitialized = false;
        LoadedShapes = null;
    }

    private void Update()
    {
        if (!IsInitialized) return;
        UnityPlayerInput.Sample input = UnityPlayerInput.Read();
        LastMovementInput = input.move;
        unity_game_step_v2(input.move.x, input.move.y, Time.deltaTime, input.interactPressed ? 1 : 0, input.jumpPressed ? 1 : 0);
        audioPresentation.Consume();
        RefreshSlicePresentation();

        ApplyActiveCamera(false);
    }

    private void RefreshSlicePresentation()
    {
        SliceView = NativeKeyDoor.ReadView();
        if (slicePresentation != null) slicePresentation.Apply(SliceView);
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

        unity_game_get_camera_pose(out NativePose pose);
        activeCamera.transform.SetPositionAndRotation(
            new Vector3(pose.x, pose.y, pose.z),
            Quaternion.Euler(pose.pitch * Mathf.Rad2Deg, pose.yaw * Mathf.Rad2Deg, pose.roll * Mathf.Rad2Deg));

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
