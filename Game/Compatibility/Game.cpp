#include "Game.h"
#include "GameState.h"
#include "Player.h"

namespace {

    GameState g_state{};

    IDisplacementBackend* g_displacement =
        nullptr;

    bool isWithinTransitionWidth(float x) {

        return x >=
                   -g_state.cameraTransition.halfWidthX &&
               x <=
                    g_state.cameraTransition.halfWidthX;
    }

    void updateCameraTransition() {

        const float boundary =
            g_state.cameraTransition.boundaryZ;

        const float previousZ =
            g_state.previousPlayerPos.z;

        const float currentZ =
            g_state.playerPos.z;

        const bool crossedForward =
            previousZ < boundary &&
            currentZ >= boundary;

        const bool crossedBackward =
            previousZ > boundary &&
            currentZ <= boundary;

        if (!crossedForward &&
            !crossedBackward) {

            return;
        }

        const float dz =
            currentZ -
            previousZ;

        float crossingT =
            0.0f;

        if (dz != 0.0f) {

            crossingT =
                (boundary - previousZ) /
                dz;
        }

        crossingT =
            crossingT < 0.0f
                ? 0.0f
                : crossingT > 1.0f
                    ? 1.0f
                    : crossingT;

        const float crossingX =
            g_state.previousPlayerPos.x +
            (g_state.playerPos.x -
             g_state.previousPlayerPos.x) *
            crossingT;

        if (!isWithinTransitionWidth(
                crossingX)) {

            return;
        }

        if (crossedForward) {

            g_state.gameplayCamera =
                g_state.cameraTransition.cameraB;
        }
        else {

            g_state.gameplayCamera =
                g_state.cameraTransition.cameraA;
        }
    }
}

extern "C" void game_init(
    SliceBindings bindings,
    IDisplacementBackend* displacement) {

    g_displacement =
        displacement;

    g_state =
        GameState{};

    g_state.playerActor =
        bindings.playerActor;

    g_state.playerPos =
        bindings.initialPlayerPose.position;

    g_state.previousPlayerPos =
        g_state.playerPos;

    g_state.gameplayCamera =
        bindings.gameplayCamera;

    g_state.cameraPose =
        bindings.initialCameraPose;

    g_state.cameraTransition =
        bindings.cameraTransition;
}

extern "C" void game_step(
    const InputFrame* input,
    float deltaSeconds) {

    if (input == nullptr ||
        g_displacement == nullptr) {

        return;
    }

    g_state.previousPlayerPos =
        g_state.playerPos;

    playerUpdate(
        g_state,
        *g_displacement,
        *input,
        deltaSeconds);

    updateCameraTransition();
}

extern "C" Vec3 game_get_player_pos() {

    return g_state.playerPos;
}

extern "C" void game_set_camera(
    CameraRef camera,
    Pose pose) {

    g_state.gameplayCamera =
        camera;

    g_state.cameraPose =
        pose;
}

extern "C" Pose game_get_camera_pose() {

    return g_state.cameraPose;
}

extern "C" CameraRef game_get_camera_ref() {

    return g_state.gameplayCamera;
}