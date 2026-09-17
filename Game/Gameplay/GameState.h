#pragma once
#include "AuthoredBindings.h"
#include "Types.h"

struct GameState {
    AudioEvents audio;
    float footstepDistance = 0;
    float footstepCooldown = 0;
    bool firstFootstep = true;
    void emit(AudioCue cue) {
        if (audio.count < 4) audio.cues[audio.count++] = cue;
    }
    ActorRef playerActor;

    Vec3 playerPos;
    Vec3 previousPlayerPos;

    CameraRef gameplayCamera;
    Pose cameraPose;

    CameraTransition cameraTransition;
    KeyDoorBindings keyDoor;
    bool hasKey = false;
    bool doorOpen = false;
    bool completed = false;
    InteractionPrompt prompt = InteractionPrompt::None;
    SliceFeedback feedback = SliceFeedback::None;
    float feedbackSeconds = 0.0f;
};
