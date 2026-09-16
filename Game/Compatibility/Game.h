#pragma once
#include "AuthoredBindings.h"
#include "IDisplacementBackend.h"
#include "Types.h"

extern "C" {

void game_init(
    SliceBindings bindings,
    IDisplacementBackend* displacement);

void game_step(
    const InputFrame* input,
    float deltaSeconds);

Vec3 game_get_player_pos();

// Host/test inspection hook.
// NOT part of the eventual gameplay compatibility API unless
// Unity/Simulant demonstrates a need for it.
Pose game_get_camera_pose();

}
