#include <cassert>
#include <cmath>
#include <cstdio>

#include "Game.h"
#include "MockBackend.h"

namespace {

InputFrame moveForward() {
    InputFrame input{};
    input.move.y = 1.0f;
    return input;
}

InputFrame moveBackward() {
    InputFrame input{};
    input.move.y = -1.0f;
    return input;
}

InputFrame moveRight() {
    InputFrame input{};
    input.move.x = 1.0f;
    return input;
}

SliceBindings makeBindings() {
    SliceBindings bindings{};

    bindings.playerActor = ActorRef{1};

    bindings.gameplayCamera = CameraRef{1};

    bindings.initialPlayerPose.position =
        Vec3{0.0f, 0.0f, 0.0f};

    bindings.initialPlayerPose.yaw = 0.0f;

    bindings.initialCameraPose.position =
        Vec3{0.0f, 2.4f, -3.8f};

    bindings.initialCameraPose.yaw = 0.0f;

    bindings.cameraTransition.cameraA =
        CameraRef{1};

    bindings.cameraTransition.cameraB =
        CameraRef{2};

    // Unity-authored boundary.
    bindings.cameraTransition.boundaryZ = 0.8f;

    // Unity-authored X span: [-4, +4].
    bindings.cameraTransition.halfWidthX = 4.0f;

    return bindings;
}

void assertCamera(unsigned int expected) {
    const CameraRef camera =
        game_get_camera_ref();

    assert(camera.id == expected);
}

}

int main() {

    MockBackend backend;
    SliceBindings bindings = makeBindings();

    game_init(bindings, &backend);

    // --------------------------------------------------------
    // Initial state.
    // --------------------------------------------------------

    assertCamera(1);

    // --------------------------------------------------------
    // Forward crossing.
    // --------------------------------------------------------

    InputFrame forward = moveForward();

    // Z = 0.0 -> 0.7
    game_step(&forward, 0.7f);

    assert(
        std::fabs(
            game_get_player_pos().z - 0.7f) < 0.0001f);

    assertCamera(1);

    // Z = 0.7 -> 0.9
    // Crosses boundary Z = 0.8.
    game_step(&forward, 0.2f);

    assert(
        std::fabs(
            game_get_player_pos().z - 0.9f) < 0.0001f);

    assertCamera(2);

    // Continue forward.
    // Camera 02 remains active.
    game_step(&forward, 0.5f);

    assert(
        std::fabs(
            game_get_player_pos().z - 1.4f) < 0.0001f);

    assertCamera(2);

    // --------------------------------------------------------
    // Backward crossing.
    // --------------------------------------------------------

    InputFrame backward = moveBackward();

    // Z = 1.4 -> 0.9
    // Still north of boundary.
    game_step(&backward, 0.5f);

    assert(
        std::fabs(
            game_get_player_pos().z - 0.9f) < 0.0001f);

    assertCamera(2);

    // Z = 0.9 -> 0.7
    // Crosses boundary toward -Z.
    game_step(&backward, 0.2f);

    assert(
        std::fabs(
            game_get_player_pos().z - 0.7f) < 0.0001f);

    assertCamera(1);

    // --------------------------------------------------------
    // Outside transition width.
    // --------------------------------------------------------

    // Move the player to X = 5 while staying at Z = 0.7.
    // This is outside the authored [-4,+4] transition span.
    backend.useScriptedResponse = true;
    backend.nextResolvedDelta =
        Vec3{5.0f, 0.0f, 0.0f};
    backend.nextBlocked = false;

    InputFrame right = moveRight();

    // playerSpeed = 1, so 5 seconds requests 5 units.
    game_step(&right, 5.0f);

    assert(
        std::fabs(
            game_get_player_pos().x - 5.0f) < 0.0001f);

    assertCamera(1);

    // Restore normal backend behavior.
    backend.useScriptedResponse = false;

    // Now cross Z = 0.8 while X = 5.
    // The crossing point is outside the authored width,
    // so the camera must NOT switch.
    game_step(&forward, 0.2f);

    assert(
        std::fabs(
            game_get_player_pos().z - 0.9f) < 0.0001f);

    assertCamera(1);

    std::printf(
        "Camera transition tests passed.\n"
        "Forward crossing: Camera 01 -> Camera 02\n"
        "Backward crossing: Camera 02 -> Camera 01\n"
        "Outside transition width: no switch\n");

    return 0;
}
