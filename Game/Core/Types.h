#pragma once

struct Vec2 { float x = 0.0f; float y = 0.0f; };
struct Vec3 { float x = 0.0f; float y = 0.0f; float z = 0.0f; };

// Transient output of one game_step. Hosts consume once after each step.
enum class AudioCue : unsigned int { Footstep, KeyTaken, DoorLocked, DoorUnlocked };
struct AudioEvents {
    unsigned int count = 0;
    AudioCue cues[4]{};
};

// Player intent for a single frame.
// move.x = right axis, move.y = forward axis, both in [-1, 1].
struct InputFrame {
    Vec2  move;
    bool  interactPressed = false;
};

enum class InteractionPrompt : unsigned int {
    None, TakeKey, LockedDoor, UnlockDoor, OpenDoor, Complete
};

enum class SliceFeedback : unsigned int {
    None, DoorLocked, KeyTaken, DoorUnlocked, Completed
};

// Presentation snapshot. The host maps these values to text/visuals only.
enum KeyDoorFlags : unsigned int {
    HasKey = 1, DoorOpen = 2, SliceComplete = 4, ObjectiveEnabled = 8
};

struct KeyDoorView {
    unsigned int flags = 0;
    InteractionPrompt prompt = InteractionPrompt::None;
    SliceFeedback feedback = SliceFeedback::None;
};
