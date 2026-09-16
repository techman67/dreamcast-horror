#include <cassert>
#include <cmath>
#include <cstdio>
#include "Game.h"
#include "MockBackend.h"

static bool nearlyEqual(float a, float b, float eps = 0.001f) {
    return std::fabs(a - b) < eps;
}

int main() {
    MockBackend backend;
    SliceBindings bindings{};
bindings.initialPlayerPose.position = {0.0f, 0.0f, 0.0f};
game_init(bindings, &backend);

    const InputFrame forward{ {0.0f, 1.0f}, false };

    // Case 1: full movement.
    // Requested 1.0 -> resolved 1.0, blocked=false -> position advances 1.0.
    backend.useScriptedResponse = true;
    backend.nextResolvedDelta = {0.0f, 0.0f, 1.0f};
    backend.nextBlocked = false;
    game_step(&forward, 1.0f);
    Vec3 pos = game_get_player_pos();
    assert(nearlyEqual(pos.z, 1.0f));
    std::printf("Case 1 passed: z = %.3f\n", pos.z);

    // Case 2: partial movement.
    // Requested 1.0 -> resolved 0.4, blocked=true -> position advances 0.4.
    // This is the key partial-resolution proof.
    backend.useScriptedResponse = true;
    backend.nextResolvedDelta = {0.0f, 0.0f, 0.4f};
    backend.nextBlocked = true;
    game_step(&forward, 1.0f);
    pos = game_get_player_pos();
    assert(nearlyEqual(pos.z, 1.4f));
    std::printf("Case 2 passed: z = %.3f\n", pos.z);

    // Case 3: full movement again.
    // Requested 1.0 -> resolved 1.0, blocked=false -> position advances 1.0.
    backend.useScriptedResponse = true;
    backend.nextResolvedDelta = {0.0f, 0.0f, 1.0f};
    backend.nextBlocked = false;
    game_step(&forward, 1.0f);
    pos = game_get_player_pos();
    assert(nearlyEqual(pos.z, 2.4f));
    std::printf("Case 3 passed: z = %.3f\n", pos.z);

    std::printf("All tests passed.\n");
    return 0;
}