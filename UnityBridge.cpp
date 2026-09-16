#include <exception>
#include "Game.h"
#include "RoomData.h"
#include "StaticCollisionBackend.h"

namespace {
    RoomData g_room{};
    StaticCollisionBackend g_collisionBackend{};
    const char* g_error = "Game has not been initialized.";
}

extern "C" __declspec(dllexport)
int unity_game_init(const char* roomText) {
    // A failed reload cannot continue running the previous room.
    game_init(SliceBindings{}, nullptr);
    try {
        g_error = parseRoomData(roomText, g_room);
        if (g_error != nullptr) return 0;
        if (!g_collisionBackend.configure(g_room.shapes, g_room.shapeCount,
                                          g_room.bindings.playerCollisionRadius)) {
            g_error = "Invalid static collision configuration.";
            return 0;
        }
        game_init(g_room.bindings, &g_collisionBackend);
        return 1;
    } catch (const std::exception&) {
        // Never propagate a C++ exception across P/Invoke.
        g_error = "Unable to allocate or parse room startup data.";
        return 0;
    }
}

extern "C" __declspec(dllexport)
const char* unity_game_get_error() { return g_error; }

extern "C" __declspec(dllexport)
const char* unity_game_validate_room(const char* text) {
    try {
        RoomData candidate{};
        return parseRoomData(text, candidate);
    } catch (const std::exception&) {
        return "Unable to allocate or parse room startup data.";
    }
}

extern "C" __declspec(dllexport)
unsigned int unity_game_get_shape_count() { return g_error == nullptr ? g_room.shapeCount : 0; }

extern "C" __declspec(dllexport)
int unity_game_get_shape(unsigned int index, CollisionShape* shape) {
    if (g_error != nullptr || shape == nullptr || index >= g_room.shapeCount) return 0;
    *shape = g_room.shapes[index];
    return 1;
}

extern "C" __declspec(dllexport)
void unity_game_step(float moveX, float moveY, float deltaSeconds) {
    const InputFrame input{{moveX, moveY}, false};
    game_step(&input, deltaSeconds);
}

extern "C" __declspec(dllexport)
float unity_game_get_player_x() { return game_get_player_pos().x; }

extern "C" __declspec(dllexport)
float unity_game_get_player_y() { return game_get_player_pos().y; }

extern "C" __declspec(dllexport)
float unity_game_get_player_z() { return game_get_player_pos().z; }

extern "C" __declspec(dllexport)
float unity_game_get_player_radius() { return g_room.bindings.playerCollisionRadius; }

extern "C" __declspec(dllexport)
unsigned int unity_game_get_camera_id() { return game_get_camera_ref().id; }

extern "C" __declspec(dllexport)
void unity_game_get_camera_pose(Pose* pose) {
    if (pose != nullptr) *pose = game_get_camera_pose();
}
