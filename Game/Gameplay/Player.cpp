#include "Player.h"

namespace {
    // Gameplay constant: world units per second at full input.
    constexpr float playerSpeed = 1.0f;
}

void playerUpdate(GameState& state,
                  IDisplacementBackend& displacement,
                  const InputFrame& input,
                  float deltaSeconds) {
    // InputFrame is intent. Player converts intent to desired displacement.
    const Vec3 desired {
        input.move.x * playerSpeed * deltaSeconds,
        0.0f,
        input.move.y * playerSpeed * deltaSeconds
    };

    if (desired.x == 0.0f && desired.z == 0.0f) {
        return;
    }

    const DisplacementResult result =
        displacement.resolveMove(state.playerPos, desired);

    // Apply resolvedDelta regardless of blocked.
    // blocked only signals that the requested displacement was not fully
    // satisfiable; resolvedDelta is the portion the backend allowed.
    state.playerPos.x += result.resolvedDelta.x;
    state.playerPos.y += result.resolvedDelta.y;
    state.playerPos.z += result.resolvedDelta.z;
}