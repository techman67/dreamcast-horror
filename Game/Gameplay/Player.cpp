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
    if (state.playerHeight > 0) {
        // At most six character sweeps per frame, independent of rendering.
        const float duration = std::fmin(deltaSeconds, 0.1f);
        const int steps = static_cast<int>(std::ceil(duration * 60.0f));
        if (steps == 0) return;
        const float dt = duration / static_cast<float>(steps);
        // Jump is an edge-triggered intent and requires support at the feet.
        if (input.jumpPressed && state.verticalSpeed <= 0 &&
            displacement.resolveMove(state.playerActor,state.playerPos,{}).grounded) {
            state.verticalSpeed = 5.0f;
            state.grounded = false;
        }
        for (int i = 0; i < steps; ++i) {
            state.verticalSpeed = std::fmax(-12.0f, state.verticalSpeed - 9.8f * dt);
            const auto result = displacement.resolveMove(state.playerActor, state.playerPos,
                {input.move.x * scale * playerSpeed * dt, state.verticalSpeed * dt,
                 input.move.y * scale * playerSpeed * dt});
            state.playerPos.x += result.resolvedDelta.x;
            state.playerPos.y += result.resolvedDelta.y;
            state.playerPos.z += result.resolvedDelta.z;
            state.grounded = result.grounded && state.verticalSpeed <= 0;
            if (state.grounded || (result.hitCeiling && state.verticalSpeed > 0)) state.verticalSpeed = 0;
        }
        return;
    }
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
