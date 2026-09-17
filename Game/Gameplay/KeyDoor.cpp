#include "KeyDoor.h"
#include <cmath>

namespace {
bool enabled(const GameState& state) {
    return state.keyDoor.keyActor.id != 0 && state.keyDoor.doorActor.id != 0;
}

float distanceSquared(const Vec3& a, const Vec3& b) {
    const float dx = a.x - b.x, dz = a.z - b.z;
    return dx * dx + dz * dz;
}

InteractionPrompt promptFor(const GameState& state) {
    if (!enabled(state)) return InteractionPrompt::None;
    if (state.completed) return InteractionPrompt::Complete;
    const auto& data = state.keyDoor;
    const float keyDistance = distanceSquared(state.playerPos, data.keyPosition);
    const float doorDistance = distanceSquared(state.playerPos, data.doorPosition);
    const bool nearDoor = doorDistance <= data.doorRange * data.doorRange;
    if (!state.hasKey && keyDistance <= data.keyRange * data.keyRange &&
        (!nearDoor || keyDistance <= doorDistance)) return InteractionPrompt::TakeKey;
    if (nearDoor) {
        if (state.doorOpen) return InteractionPrompt::OpenDoor;
        return state.hasKey ? InteractionPrompt::UnlockDoor : InteractionPrompt::LockedDoor;
    }
    return InteractionPrompt::None;
}

void feedback(GameState& state, SliceFeedback value) {
    state.feedback = value;
    state.feedbackSeconds = 2.5f;
}

bool crossedExit(const GameState& state) {
    const float boundary = state.keyDoor.exitBoundaryZ;
    if (state.previousPlayerPos.z <= boundary || state.playerPos.z > boundary) return false;
    const float t = (boundary - state.previousPlayerPos.z) /
                    (state.playerPos.z - state.previousPlayerPos.z);
    const float x = state.previousPlayerPos.x + t * (state.playerPos.x - state.previousPlayerPos.x);
    return std::fabs(x - state.keyDoor.exitCenterX) <= state.keyDoor.exitHalfWidthX;
}
}

void initializeKeyDoor(GameState& state, IDisplacementBackend& displacement) {
    if (enabled(state)) displacement.setDoorObstruction(state.keyDoor.doorActor, true);
    state.prompt = promptFor(state);
}

void updateKeyDoor(GameState& state, IDisplacementBackend& displacement,
                   const InputFrame& input, float deltaSeconds) {
    if (!enabled(state) || state.completed) return;
    if (state.feedbackSeconds > 0) {
        state.feedbackSeconds -= deltaSeconds;
        if (state.feedbackSeconds <= 0) state.feedback = SliceFeedback::None;
    }
    state.prompt = promptFor(state);
    if (input.interactPressed) {
        switch (state.prompt) {
        case InteractionPrompt::TakeKey:
            state.hasKey = true;
            feedback(state, SliceFeedback::KeyTaken);
            state.emit(AudioCue::KeyTaken);
            break;
        case InteractionPrompt::LockedDoor:
            feedback(state, SliceFeedback::DoorLocked);
            state.emit(AudioCue::DoorLocked);
            break;
        case InteractionPrompt::UnlockDoor:
            state.doorOpen = true;
            displacement.setDoorObstruction(state.keyDoor.doorActor, false);
            feedback(state, SliceFeedback::DoorUnlocked);
            state.emit(AudioCue::DoorUnlocked);
            break;
        default:
            break;
        }
    }
    if (state.doorOpen && crossedExit(state)) {
        state.completed = true;
        feedback(state, SliceFeedback::Completed);
    }
    state.prompt = promptFor(state);
}

KeyDoorView keyDoorView(const GameState& state) {
    KeyDoorView result{};
    if (enabled(state)) result.flags |= ObjectiveEnabled;
    if (state.hasKey) result.flags |= HasKey;
    if (state.doorOpen) result.flags |= DoorOpen;
    if (state.completed) result.flags |= SliceComplete;
    result.prompt = state.prompt;
    result.feedback = state.feedback;
    return result;
}
