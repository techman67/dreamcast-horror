#include "Player.h"

namespace {
    constexpr float playerSpeed = 1.0f;
}

void playerUpdate(GameState& state,
                  IDisplacementBackend& displacement,
                  const InputFrame& input,
                  float deltaSeconds) {
    const Vec3 desired {
        input.move.x * playerSpeed * deltaSeconds,
        0.0f,
        input.move.y * playerSpeed * deltaSeconds
    };

    if (desired.x == 0.0f && desired.z == 0.0f) {
        return;
    }

    const DisplacementResult result =
        displacement.resolveMove(
            state.playerActor,
            state.playerPos,
            desired);

    // Apply the portion the backend actually allowed, even when blocked.
    state.playerPos.x += result.resolvedDelta.x;
    state.playerPos.y += result.resolvedDelta.y;
    state.playerPos.z += result.resolvedDelta.z;
}
