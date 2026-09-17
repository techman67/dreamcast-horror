#include "Player.h"
#include <cmath>

namespace {

    constexpr float playerSpeed =
        1.0f;
}

void playerUpdate(
    GameState& state,
    IDisplacementBackend& displacement,
    const InputFrame& input,
    float deltaSeconds) {

    // Movement speed is a game rule, independent of keyboard diagonals or sticks.
    const float length = std::hypot(input.move.x, input.move.y);
    const float scale = length > 1.0f ? 1.0f / length : 1.0f;
    const Vec3 desired{
        input.move.x * scale *
            playerSpeed *
            deltaSeconds,

        0.0f,

        input.move.y * scale *
            playerSpeed *
            deltaSeconds
    };

    if (desired.x == 0.0f &&
        desired.z == 0.0f) {

        return;
    }

    const DisplacementResult result =
        displacement.resolveMove(
            state.playerActor,
            state.playerPos,
            desired);

    // Always apply the resolved portion,
    // including when blocked == true.
    state.playerPos.x +=
        result.resolvedDelta.x;

    state.playerPos.y +=
        result.resolvedDelta.y;

    state.playerPos.z +=
        result.resolvedDelta.z;
}