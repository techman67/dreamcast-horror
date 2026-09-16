#pragma once
#include "AuthoredBindings.h"
#include "Types.h"

struct GameState {
    ActorRef playerActor;
    Vec3 playerPos;

    CameraRef gameplayCamera;
    Pose cameraPose;
};
