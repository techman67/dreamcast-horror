#include <cassert>
#include <cmath>
#include <cstdio>
#include "AuthoredBindings.h"
#include "Game.h"
#include "MockBackend.h"

static bool nearlyEqual(float a, float b, float eps = 0.001f) {
    return std::fabs(a - b) < eps;
}

int main() {
    SliceBindings bindings{};

    bindings.playerActor.id = 42;
    bindings.gameplayCamera.id = 7;

    bindings.initialPlayerPose.position = {10.0f, 0.0f, 20.0f};
    bindings.initialPlayerPose.yaw = 0.0f;

    bindings.initialCameraPose.position = {10.0f, 1.6f, 18.0f};
    bindings.initialCameraPose.yaw = 3.14159265f;

    MockBackend backend;
    game_init(bindings, &backend);

    Vec3 pos = game_get_player_pos();

    assert(nearlyEqual(pos.x, 10.0f));
    assert(nearlyEqual(pos.y, 0.0f));
    assert(nearlyEqual(pos.z, 20.0f));

    std::printf(
        "Player init passed: (%.3f, %.3f, %.3f)\n",
        pos.x, pos.y, pos.z
    );

    Pose camera = game_get_camera_pose();

    assert(nearlyEqual(camera.position.x, 10.0f));
    assert(nearlyEqual(camera.position.y, 1.6f));
    assert(nearlyEqual(camera.position.z, 18.0f));
    assert(nearlyEqual(camera.yaw, 3.14159265f));

    std::printf(
        "Camera init passed: (%.3f, %.3f, %.3f), yaw=%.3f\n",
        camera.position.x,
        camera.position.y,
        camera.position.z,
        camera.yaw
    );

    const InputFrame forward{{0.0f, 1.0f}, false};

    game_step(&forward, 1.0f);

    assert(backend.lastActor.id == 42);
    assert(nearlyEqual(backend.lastPosition.x, 10.0f));
    assert(nearlyEqual(backend.lastPosition.y, 0.0f));
    assert(nearlyEqual(backend.lastPosition.z, 20.0f));

    assert(nearlyEqual(backend.lastDesiredDelta.x, 0.0f));
    assert(nearlyEqual(backend.lastDesiredDelta.y, 0.0f));
    assert(nearlyEqual(backend.lastDesiredDelta.z, 1.0f));

    pos = game_get_player_pos();

    assert(nearlyEqual(pos.x, 10.0f));
    assert(nearlyEqual(pos.y, 0.0f));
    assert(nearlyEqual(pos.z, 21.0f));

    std::printf(
        "Actor identity and movement request passed: actor=%u\n",
        backend.lastActor.id
    );

    camera = game_get_camera_pose();

    assert(nearlyEqual(camera.position.x, 10.0f));
    assert(nearlyEqual(camera.position.y, 1.6f));
    assert(nearlyEqual(camera.position.z, 18.0f));

    std::printf("Camera remained stable during movement.\n");

    backend.useScriptedResponse = true;
    backend.nextResolvedDelta = {0.0f, 0.0f, 0.4f};
    backend.nextBlocked = true;

    game_step(&forward, 1.0f);

    pos = game_get_player_pos();

    assert(nearlyEqual(pos.x, 10.0f));
    assert(nearlyEqual(pos.y, 0.0f));
    assert(nearlyEqual(pos.z, 21.4f));

    std::printf(
        "Partial movement passed: (%.3f, %.3f, %.3f)\n",
        pos.x, pos.y, pos.z
    );

    std::printf("Camera-binding tests passed.\n");
    return 0;
}
