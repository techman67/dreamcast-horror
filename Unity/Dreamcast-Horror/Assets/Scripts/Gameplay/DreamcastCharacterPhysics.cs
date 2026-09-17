using UnityEngine;

// Authoring data only; the shared C++ controller performs every collision check.
[DisallowMultipleComponent]
[RequireComponent(typeof(CppPlayerVisual))]
public sealed class DreamcastCharacterPhysics : MonoBehaviour
{
    [Tooltip("Standing collision height above the player's feet, in metres. Static collision must use boxes.")]
    [Range(.5f, 4f)] public float height = 1.8f;
    [Tooltip("Largest automatic step up or down, in metres. Larger drops use gravity.")]
    [Range(0, .5f)] public float stepHeight = .3f;
#if UNITY_EDITOR
    void OnGUI()
    {
        if (!Application.isPlaying) return;
        var bridge = Object.FindAnyObjectByType<NativeGameBridge>();
        Vector2 input = bridge != null ? bridge.LastMovementInput : Vector2.zero;
        string state = UnityEditor.EditorApplication.isPaused ? "PAUSED - press Pause to resume" :
            !NativeGameBridge.IsInitialized ? "Movement not initialized - check Console" :
            !Application.isFocused ? "Unity does not have keyboard focus" : "Running";
        string movement = input == Vector2.zero ? "none" :
            (input.y > 0 ? "W / forward " : input.y < 0 ? "S / backward " : "") +
            (input.x > 0 ? "D / right" : input.x < 0 ? "A / left" : "");
        float scale = Mathf.Max(1, Screen.width / 640f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        GUI.Box(new Rect(8, Screen.height / scale - 78, 620, 70),
            $" {state} | Movement received: {movement}\n Player position: {transform.position:F2}", style);
        GUI.matrix = previous;
    }
#endif
    void OnDrawGizmosSelected()
    {
        var player = GetComponent<CppPlayerVisual>();
        var bridge = Object.FindAnyObjectByType<NativeGameBridge>();
        if (player == null || bridge == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(player.AuthoredSpawnPosition + Vector3.up * height * .5f,
            new Vector3(bridge.AuthoredPlayerRadius * 2, height, bridge.AuthoredPlayerRadius * 2));
    }
}
