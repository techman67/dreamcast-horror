#pragma once
#include "AuthoredBindings.h"
#include "IDisplacementBackend.h"
#include "Types.h"
#include "SaveData.h"

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
Pose game_get_camera_pose();

// Host/test inspection hook for the currently active camera.
CameraRef game_get_camera_ref();

KeyDoorView game_get_key_door_view();
// Fixed-capacity, allocation-free batch. Reading drains it; reset clears it.
AudioEvents game_take_audio_events();
void game_set_linked_room(bool linked);
void game_set_save_points(const SavePoints* points);
int game_near_save_point();
int game_take_save_request();
bool game_capture_progress(SaveProgress* progress);
bool game_restore_progress(const SaveProgress* progress);

}
