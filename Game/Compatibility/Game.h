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

// Select the active authored fixed/hybrid gameplay camera.
void game_set_camera(
    CameraRef camera,
    Pose pose);

// Host/test inspection hook.
// Not necessarily part of the eventual backend-facing API.
Pose game_get_camera_pose();

}
