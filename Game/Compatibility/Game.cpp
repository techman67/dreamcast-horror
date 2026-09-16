#include "Game.h"
#include "GameState.h"
#include "Player.h"

namespace {
    GameState g_state{};
    IDisplacementBackend* g_displacement = nullptr;
}

extern "C" void game_init(
    SliceBindings bindings,
    IDisplacementBackend* displacement) {

    g_displacement = displacement;

    g_state = GameState{};

    g_state.playerActor = bindings.playerActor;
    g_state.playerPos = bindings.initialPlayerPose.position;

    g_state.gameplayCamera = bindings.gameplayCamera;
    g_state.cameraPose = bindings.initialCameraPose;
}

extern "C" void game_step(
    const InputFrame* input,
    float deltaSeconds) {

    if (input == nullptr || g_displacement == nullptr) {
        return;
    }

    playerUpdate(g_state, *g_displacement, *input, deltaSeconds);
}

extern "C" Vec3 game_get_player_pos() {
    return g_state.playerPos;
}

extern "C" void game_set_camera(
    CameraRef camera,
    Pose pose) {

    g_state.gameplayCamera = camera;
    g_state.cameraPose = pose;
}

extern "C" Pose game_get_camera_pose() {
    return g_state.cameraPose;
}
