#pragma once
#include "Types.h"

// Stable opaque identifier for an authored actor.
// The value is meaningful only to the backend that resolves it.
struct ActorRef {
    unsigned int id = 0;
};

// Stable opaque identifier for an authored camera.
struct CameraRef {
    unsigned int id = 0;
};

// Minimal authored pose: position plus yaw.
// Yaw is rotation around world Y, in radians.
struct Pose {
    Vec3 position;
    float yaw = 0.0f;
};

// Authored bindings for the first vertical slice.
// The backend resolves these opaque references to its own representation.
struct SliceBindings {
    ActorRef playerActor;
    CameraRef gameplayCamera;
    Pose initialPlayerPose;
};