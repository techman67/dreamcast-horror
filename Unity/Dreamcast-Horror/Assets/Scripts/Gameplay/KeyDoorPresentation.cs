using UnityEngine;

// Prototype authoring references and read-only presentation. No interaction rules.
public sealed class KeyDoorPresentation : MonoBehaviour
{
    [SerializeField] private Transform keyVisual;
    [SerializeField] private Transform doorVisual;
    [SerializeField] private CppCollision doorCollision;
    [SerializeField] private Transform exitMarker;
    [SerializeField, Min(2)] private int keyActorId = 2;
    [SerializeField, Min(2)] private int doorActorId = 3;
    [SerializeField, Min(0.01f)] private float keyRange = 1.1f;
    [SerializeField, Min(0.01f)] private float doorRange = 1.4f;
    [SerializeField, Min(0.01f)] private float exitHalfWidth = 1.1f;

    public Transform KeyVisual => keyVisual;
    public Transform DoorVisual => doorVisual;
    public CppCollision DoorCollision => doorCollision;
    public Transform ExitMarker => exitMarker;
    public int KeyActorId => keyActorId;
    public int DoorActorId => doorActorId;
    public float KeyRange => keyRange;
    public float DoorRange => doorRange;
    public float ExitHalfWidth => exitHalfWidth;
    public bool HasVisualBindings => keyVisual != null && doorVisual != null;

    private NativeKeyDoor.View view;
    private GUIStyle titleStyle, textStyle;

    public void Initialize(NativeKeyDoor.Bindings bindings)
    {
        // Runtime positions come from the same exported data as interaction tests.
        keyVisual.position = bindings.keyPosition;
        doorVisual.position = bindings.doorPosition;
    }

    public void Apply(NativeKeyDoor.View current)
    {
        view = current;
        if (!current.Enabled) return;
        keyVisual.gameObject.SetActive(!current.HasKey);
        doorVisual.gameObject.SetActive(!current.DoorOpen);
    }

    private static string PromptText(NativeKeyDoor.Prompt prompt)
    {
        switch (prompt)
        {
            case NativeKeyDoor.Prompt.TakeKey: return "E / A  -  Take brass key";
            case NativeKeyDoor.Prompt.LockedDoor: return "E / A  -  Try locked door";
            case NativeKeyDoor.Prompt.UnlockDoor: return "E / A  -  Unlock and open door";
            case NativeKeyDoor.Prompt.OpenDoor: return "The doorway is open. Walk through.";
            case NativeKeyDoor.Prompt.Complete: return "You escaped the room.";
            default: return "";
        }
    }

    private static string FeedbackText(NativeKeyDoor.Feedback feedback)
    {
        switch (feedback)
        {
            case NativeKeyDoor.Feedback.DoorLocked: return "Locked. There must be a key nearby.";
            case NativeKeyDoor.Feedback.KeyTaken: return "Brass key collected.";
            case NativeKeyDoor.Feedback.DoorUnlocked: return "The key fits. The door is open.";
            case NativeKeyDoor.Feedback.Completed: return "Slice complete";
            default: return "";
        }
    }

    private void OnGUI()
    {
        if (!NativeGameBridge.IsInitialized || !view.Enabled) return;
        if (textStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        }
        GUI.Box(new Rect(16, 16, 430, 106), GUIContent.none);
        GUI.Label(new Rect(30, 25, 402, 30), view.Complete ? "ROOM ESCAPED" : "THE LOCKED ROOM", titleStyle);
        GUI.Label(new Rect(30, 57, 402, 28), view.HasKey ? "Inventory: brass key" : "Find a key and open the south door.", textStyle);
        GUI.Label(new Rect(30, 85, 402, 28), "Move: WASD / arrows / stick / D-pad", textStyle);
        GUI.Box(new Rect(16, Screen.height - 104, 500, 88), GUIContent.none);
        GUI.Label(new Rect(30, Screen.height - 97, 474, 32), PromptText(view.prompt), textStyle);
        GUI.Label(new Rect(30, Screen.height - 63, 474, 36), FeedbackText(view.feedback), textStyle);
    }
}
