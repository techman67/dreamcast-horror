#include "Game.h"
#include "GameState.h"
#include "Player.h"
#include "KeyDoor.h"
#include <cmath>

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

        // Zero IDs mean no transition was authored (e.g. single-camera tests).
        if (g_state.cameraTransition.cameraA.id == 0 ||
            g_state.cameraTransition.cameraB.id == 0) {
            return;
        }

        const float boundary =
            g_state.cameraTransition.boundaryZ;

        const float previousZ =
            g_state.previousPlayerPos.z;

        const float currentZ =
            g_state.playerPos.z;

        const bool crossedForward =
            previousZ <= boundary &&
            currentZ >= boundary && currentZ > previousZ;

        const bool crossedBackward =
            previousZ >= boundary &&
            currentZ <= boundary && currentZ < previousZ;

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
            g_state.cameraPose = g_state.cameraTransition.poseB;
        }
        else {

            g_state.gameplayCamera =
                g_state.cameraTransition.cameraA;
            g_state.cameraPose = g_state.cameraTransition.poseA;
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
    g_state.keyDoor = bindings.keyDoor;
    if (g_displacement != nullptr) initializeKeyDoor(g_state, *g_displacement);
}

extern "C" void game_step(
    const InputFrame* input,
    float deltaSeconds) {

    g_state.audio = {}; // Invalid steps must not replay a previous event batch.

    if (input == nullptr ||
        g_displacement == nullptr || !std::isfinite(deltaSeconds) || deltaSeconds < 0 ||
        !std::isfinite(input->move.x) || !std::isfinite(input->move.y)) {

        return;
    }

    g_state.previousPlayerPos =
        g_state.playerPos;

    if (!g_state.completed) playerUpdate(
        g_state,
        *g_displacement,
        *input,
        deltaSeconds);

    updateCameraTransition();
    const float dx = g_state.playerPos.x - g_state.previousPlayerPos.x;
    const float dz = g_state.playerPos.z - g_state.previousPlayerPos.z;
    const float distance = std::sqrt(dx * dx + dz * dz);
    if (deltaSeconds > 0) {
        constexpr float stride = 0.65f;
        constexpr float minimumInterval = 0.25f;
        g_state.footstepCooldown = std::fmax(0.0f, g_state.footstepCooldown - deltaSeconds);
        const bool walking = distance > 0.0001f && std::isfinite(distance);
        if (walking) {
            // Only the first movement after reset gets an immediate step.
            // Preserve travel across stops so tapping cannot restart the stride.
            if (g_state.firstFootstep) g_state.footstepDistance = stride;
            else g_state.footstepDistance += distance;
            if (g_state.footstepDistance >= stride && g_state.footstepCooldown <= 0) {
                g_state.emit(AudioCue::Footstep);
                g_state.firstFootstep = false;
                g_state.footstepCooldown = minimumInterval;
                // Never emit a burst after a long frame or displacement jump.
                g_state.footstepDistance = std::fmod(g_state.footstepDistance, stride);
            }
        }
    }
    updateKeyDoor(g_state, *g_displacement, *input, deltaSeconds);
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

extern "C" KeyDoorView game_get_key_door_view() {
    return keyDoorView(g_state);
}

extern "C" AudioEvents game_take_audio_events() {
    const AudioEvents result = g_state.audio;
    g_state.audio = {};
    return result;
}
