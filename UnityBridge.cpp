#include <cmath>

#include "Game.h"
#include "StaticCollisionBackend.h"

namespace {

    CollisionShape g_collisionShapes[
        MaxStaticCollisionShapes
    ]{};

    unsigned int g_collisionShapeCount =
        0;

    StaticCollisionBackend g_collisionBackend{};

    constexpr float playerCollisionRadius =
        0.35f;
}

extern "C" __declspec(dllexport)
void unity_game_collision_clear() {

    g_collisionShapeCount =
        0;
}

extern "C" __declspec(dllexport)
void unity_game_collision_add_box(
    float centerX,
    float centerY,
    float centerZ,
    float halfX,
    float halfY,
    float halfZ) {

    if (g_collisionShapeCount >=
        MaxStaticCollisionShapes) {

        return;
    }

    CollisionShape& shape =
        g_collisionShapes[
            g_collisionShapeCount++
        ];

    shape =
        CollisionShape{};

    shape.type =
        CollisionShapeType::Box;

    shape.center =
        Vec3{
            centerX,
            centerY,
            centerZ
        };

    shape.halfExtents =
        Vec3{
            std::fabs(halfX),
            std::fabs(halfY),
            std::fabs(halfZ)
        };
}

extern "C" __declspec(dllexport)
void unity_game_collision_add_sphere(
    float centerX,
    float centerY,
    float centerZ,
    float radius) {

    if (g_collisionShapeCount >=
        MaxStaticCollisionShapes) {

        return;
    }

    CollisionShape& shape =
        g_collisionShapes[
            g_collisionShapeCount++
        ];

    shape =
        CollisionShape{};

    shape.type =
        CollisionShapeType::Sphere;

    shape.center =
        Vec3{
            centerX,
            centerY,
            centerZ
        };

    shape.radius =
        std::fabs(radius);
}

extern "C" __declspec(dllexport)
void unity_game_collision_add_capsule(
    float centerX,
    float centerY,
    float centerZ,
    float radius,
    float height) {

    if (g_collisionShapeCount >=
        MaxStaticCollisionShapes) {

        return;
    }

    CollisionShape& shape =
        g_collisionShapes[
            g_collisionShapeCount++
        ];

    shape =
        CollisionShape{};

    shape.type =
        CollisionShapeType::Capsule;

    shape.center =
        Vec3{
            centerX,
            centerY,
            centerZ
        };

    shape.radius =
        std::fabs(radius);

    shape.height =
        std::fabs(height);
}

extern "C" __declspec(dllexport)
void unity_game_init() {

    g_collisionBackend.configure(
        g_collisionShapes,
        g_collisionShapeCount,
        playerCollisionRadius);

    SliceBindings bindings{};

    bindings.playerActor =
        ActorRef{1};

    bindings.gameplayCamera =
        CameraRef{1};

    bindings.initialPlayerPose.position =
        Vec3{
            0.0f,
            0.0f,
            0.0f
        };

    bindings.initialPlayerPose.yaw =
        0.0f;

    bindings.initialCameraPose.position =
        Vec3{
            0.0f,
            2.4f,
            -3.8f
        };

    bindings.initialCameraPose.yaw =
        0.0f;

    bindings.cameraTransition.cameraA =
        CameraRef{1};

    bindings.cameraTransition.cameraB =
        CameraRef{2};

    bindings.cameraTransition.boundaryZ =
        0.8f;

    bindings.cameraTransition.halfWidthX =
        4.0f;

    bindings.playerCollisionRadius =
        playerCollisionRadius;

    game_init(
        bindings,
        &g_collisionBackend);
}

extern "C" __declspec(dllexport)
void unity_game_step(
    float moveX,
    float moveY,
    float deltaSeconds) {

    InputFrame input{};

    input.move.x =
        moveX;

    input.move.y =
        moveY;

    input.interactPressed =
        false;

    game_step(
        &input,
        deltaSeconds);
}

extern "C" __declspec(dllexport)
float unity_game_get_player_x() {

    return game_get_player_pos().x;
}

extern "C" __declspec(dllexport)
float unity_game_get_player_y() {

    return game_get_player_pos().y;
}

extern "C" __declspec(dllexport)
float unity_game_get_player_z() {

    return game_get_player_pos().z;
}

extern "C" __declspec(dllexport)
unsigned int unity_game_get_camera_id() {

    return game_get_camera_ref().id;
}