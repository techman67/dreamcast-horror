#pragma once

struct Vec2 { float x = 0.0f; float y = 0.0f; };
struct Vec3 { float x = 0.0f; float y = 0.0f; float z = 0.0f; };

// Player intent for a single frame.
// move.x = right axis, move.y = forward axis, both in [-1, 1].
struct InputFrame {
    Vec2  move;
    bool  interactPressed = false;
};