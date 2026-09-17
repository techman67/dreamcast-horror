#include <exception>
#include "Game.h"
#include "World.h"
#include "RoomData.h"
#include "AudioData.h"
#include "LightEffect.h"
#include "StaticCollisionBackend.h"

extern "C" __declspec(dllexport)
float unity_light_effect_advance(float phase, float dt, float frequency) { return advanceLightPhase(phase, dt, frequency); }
extern "C" __declspec(dllexport)
float unity_light_effect_sample(const LightEffectSettings* settings, float phase, float x, float y, float z) {
    return settings ? sampleLightEffect(*settings, phase, {x,y,z}) : 0;
}

namespace {
    SavePoints g_points{};
    RoomData g_room{};
    AudioData g_audio{};
    StaticCollisionBackend g_collisionBackend{};
    const char* g_error = "Game has not been initialized.";
}

extern "C" __declspec(dllexport)
const char* unity_audio_parse(const char* text) {
    try { return parseAudioData(text, g_audio); }
    catch (const std::exception&) { return "Unable to parse audio manifest."; }
}
extern "C" __declspec(dllexport)
unsigned unity_audio_clip_count() { return g_audio.clipCount; }
extern "C" __declspec(dllexport)
unsigned unity_audio_source_count() { return g_audio.sourceCount; }
extern "C" __declspec(dllexport)
void unity_audio_clip(unsigned i, AudioClipData* out) { if (out && i < g_audio.clipCount) *out = g_audio.clips[i]; }
extern "C" __declspec(dllexport)
void unity_audio_cue(unsigned i, AudioCueData* out) { if (out && i < 4) *out = g_audio.cues[i]; }
extern "C" __declspec(dllexport)
void unity_audio_source_v2(unsigned i, RoomSoundData* out) { if (out && i < g_audio.sourceCount) *out = g_audio.sources[i]; }

extern "C" __declspec(dllexport)
void unity_game_take_audio_events(AudioEvents* events) {
    if (events != nullptr) *events = game_take_audio_events();
}

extern "C" __declspec(dllexport)
int unity_game_init(const char* roomText) {
    // A failed reload cannot continue running the previous room.
    game_init(SliceBindings{}, nullptr);
    g_points=SavePoints{};
    try {
        g_error = parseRoomData(roomText, g_room);
        if (g_error != nullptr) return 0;
        if (!g_collisionBackend.configure(g_room.shapes, g_room.shapeCount,
                                          g_room.bindings.playerCollisionRadius, g_room.bindings.playerHeight, g_room.bindings.playerStepHeight)) {
            g_error = "Invalid static collision configuration.";
            return 0;
        }
        const auto& objective = g_room.bindings.keyDoor;
        if (objective.doorActor.id != 0 &&
            !g_collisionBackend.configureDoor(objective.doorActor, objective.doorShapeIndex)) {
            g_error = "Unable to bind the door collision shape.";
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
void unity_game_step(float moveX, float moveY, float deltaSeconds, int interactPressed) {
    const InputFrame input{{moveX, moveY}, interactPressed != 0};
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

extern "C" __declspec(dllexport)
void unity_game_get_key_door_bindings(KeyDoorBindings* bindings) {
    if (bindings != nullptr) *bindings = g_error == nullptr ? g_room.bindings.keyDoor : KeyDoorBindings{};
}

extern "C" __declspec(dllexport)
void unity_game_get_key_door_view(KeyDoorView* view) {
    if (view != nullptr) *view = game_get_key_door_view();
}

// Keep the original entry point for existing tools; gameplay uses the expanded intent.
extern "C" __declspec(dllexport)
void unity_game_step_v2(float moveX, float moveY, float deltaSeconds, int interactPressed, int jumpPressed) {
    const InputFrame input{{moveX, moveY}, interactPressed != 0, jumpPressed != 0};
    game_step(&input, deltaSeconds);
}

#include "SharedHost/FileSaveStorage.h"

extern "C" __declspec(dllexport)
const char* unity_save_configure(const unsigned char* bytes,unsigned size,unsigned roomHash) {
    try {
        SavePoints points; const char* error=parseSavePoints(bytes,size,points);
        if(error) return error;
        if(points.room!=roomHash) return "Save-point manifest does not match the room. Export again.";
        g_points=points; game_set_save_points(&g_points); return nullptr;
    } catch(...) { return "Unable to read save-point manifest."; }
}
extern "C" __declspec(dllexport)
int unity_save_near() { return game_near_save_point(); }
extern "C" __declspec(dllexport)
int unity_save_requested() { return game_take_save_request(); }
extern "C" __declspec(dllexport)
const char* unity_save_prompt(int index) { return index>=0 && unsigned(index)<g_points.count ? g_points.points[index].prompt : ""; }
extern "C" __declspec(dllexport)
int unity_save_action(const char* directory,int load) {
    try {
        if(!directory || !*directory || g_error) return int(SaveResult::NotReady);
        FileSaveStorage storage(directory); SaveProgress progress;
        if(world_active()) return int(load ? world_load(storage) : world_save(storage));
        if(load) { auto r=readProgress(storage,g_points.room,progress); if(r==SaveResult::Ok && !game_restore_progress(&progress)) r=SaveResult::Invalid; return int(r); }
        if(!game_capture_progress(&progress)) return int(SaveResult::NotReady);
        return int(writeProgress(storage,g_points.room,progress));
    } catch(...) { return int(SaveResult::IoError); }
}
extern "C" __declspec(dllexport)
const char* unity_save_result(int result) { return saveResultText(static_cast<SaveResult>(result)); }

extern "C" __declspec(dllexport) const char* unity_world_configure(const unsigned char* bytes,unsigned size,unsigned start) { return world_configure(bytes,size,start); }
extern "C" __declspec(dllexport) void unity_world_clear() { world_clear(); }
extern "C" __declspec(dllexport) unsigned unity_world_room() { auto r=world_room(); return r ? r->id : 0; }
extern "C" __declspec(dllexport) unsigned unity_world_hash() { auto r=world_room(); return r ? r->hash : 0; }
extern "C" __declspec(dllexport) const char* unity_world_scene() { auto r=world_room(); return r ? r->scene : ""; }
extern "C" __declspec(dllexport) int unity_world_enter() { return world_enter() ? 1 : 0; }
extern "C" __declspec(dllexport) int unity_world_near() { return world_near_link(); }
extern "C" __declspec(dllexport) int unity_world_depart(int interact) { return world_depart(interact!=0) ? 1 : 0; }
extern "C" __declspec(dllexport) int unity_world_pending() { return world_pending() ? 1 : 0; }
