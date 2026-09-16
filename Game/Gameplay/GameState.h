#pragma once
#include "AuthoredBindings.h"
#include "Types.h"

struct GameState {
    ActorRef playerActor;

    Vec3 playerPos;
    Vec3 previousPlayerPos;

    CameraRef gameplayCamera;
    Pose cameraPose;

    CameraTransition cameraTransition;
};