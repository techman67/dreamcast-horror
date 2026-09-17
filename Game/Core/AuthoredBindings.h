#pragma once
#include "Types.h"

// Stable opaque identifier for an authored actor.
struct ActorRef {
    unsigned int id = 0;
};

// Stable opaque identifier for an authored camera.
struct CameraRef {
    unsigned int id = 0;
};

// Minimal authored pose.
struct Pose {
    Vec3 position;
    float yaw = 0.0f;
    // Radians; rotation order is roll (Z), pitch (X), then yaw (Y).
    float pitch = 0.0f;
    float roll = 0.0f;
};

// Supported Dreamcast-friendly collision primitives.
enum class CollisionShapeType : unsigned int {
    Box = 0,
    Sphere = 1,
    Capsule = 2
};

// Static collision primitive.
//
// Box:
//     center + halfExtents are used.
//
// Sphere:
//     center + radius are used.
//
// Capsule:
//     center + radius + height are stored.
//     For the current flat X/Z movement solver, the horizontal
//     footprint is equivalent to a circle of radius "radius".
struct CollisionShape {
    CollisionShapeType type =
        CollisionShapeType::Box;

    Vec3 center;

    Vec3 halfExtents;

    float radius = 0.0f;

    float height = 0.0f;
};

// Fixed storage keeps the gameplay layer deterministic and
// avoids dynamic allocation.
constexpr unsigned int MaxStaticCollisionShapes = 128;

// Authored two-way fixed-camera transition.
struct CameraTransition {
    CameraRef cameraA;
    CameraRef cameraB;
    Pose poseA;
    Pose poseB;

    float boundaryZ = 0.0f;
    float halfWidthX = 0.0f;
};

// One key, one door and a southbound exit for the first playable objective.
// Zero actor IDs disable the objective for older room files and test hosts.
struct KeyDoorBindings {
    ActorRef keyActor;
    ActorRef doorActor;
    Vec3 keyPosition;
    Vec3 doorPosition;
    unsigned int doorShapeIndex = MaxStaticCollisionShapes;
    float keyRange = 1.1f;
    float doorRange = 1.4f;
    float exitBoundaryZ = 0.0f;
    float exitCenterX = 0.0f;
    float exitHalfWidthX = 0.0f;
};

// Initial game bindings.
//
// Collision shapes are intentionally NOT part of GameState.
// They are supplied to the displacement backend during initialization.
struct SliceBindings {
    ActorRef playerActor;

    CameraRef gameplayCamera;

    Pose initialPlayerPose;
    Pose initialCameraPose;

    CameraTransition cameraTransition;

    float playerCollisionRadius = 0.35f;
    // Zero keeps the v1/v2 flat controller. V3 uses feet position and an
    // upright box of width/depth 2*radius with this standing height.
    float playerHeight = 0.0f;
    float playerStepHeight = 0.3f;
    KeyDoorBindings keyDoor;
};
