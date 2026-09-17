#include "Game.h"
#include "MockBackend.h"
#include <cassert>
#include <limits>
#include <cstdio>

static void step(float x, float z, float dt, bool interact = false) {
    InputFrame input{{x, z}, interact}; game_step(&input, dt);
}
static void expect(AudioCue cue) {
    auto events = game_take_audio_events();
    assert(events.count == 1 && events.cues[0] == cue);
    assert(game_take_audio_events().count == 0); // Exactly-once consumption.
}
static unsigned footsteps(float dt, float axis) {
    MockBackend backend;
    game_init({}, &backend);
    unsigned n = 0;
    for (int i = 0; i < int(4.0f / dt); ++i) {
        step(axis, 0, dt);
        n += game_take_audio_events().count;
    }
    return n;
}
int main() {
    static_assert(sizeof(AudioEvents) == 20, "Unity audio ABI changed");
    assert(footsteps(1.0f / 30, 1) == 7);
    assert(footsteps(1.0f / 120, 1) == 7);
    assert(footsteps(1.0f / 60, .5f) == 4); // Cadence follows resolved distance.
    MockBackend backend;
    game_init({}, &backend);
    // First movement is immediate; even taps spaced beyond the cooldown must
    // accumulate a stride rather than receiving a free sound on every press.
    step(1, 0, .01f); expect(AudioCue::Footstep);
    for (int i = 0; i < 60; ++i) {
        step(0, 0, .3f); assert(game_take_audio_events().count == 0);
        step(1, 0, .01f); assert(game_take_audio_events().count == 0);
    }
    step(0, 0, 10); assert(game_take_audio_events().count == 0);
    step(1, 0, .06f); expect(AudioCue::Footstep); // 0.66 cumulative travel.
    step(0, 0, 0); assert(game_take_audio_events().count == 0);
    step(1, 0, .3f); assert(game_take_audio_events().count == 0);
    game_init({}, &backend);
    step(1, 0, .66f); expect(AudioCue::Footstep);
    backend.useScriptedResponse = true;
    backend.nextResolvedDelta = {};
    for (int i = 0; i < 300; ++i) {
        step(1, 0, .1f); assert(game_take_audio_events().count == 0);
    }
    backend.nextBlocked = true;
    backend.nextResolvedDelta = {.7f, 0, 0}; // Wall sliding still counts movement.
    step(1, 0, 1); expect(AudioCue::Footstep);
    backend.useScriptedResponse = false;
    step(1, 0, 100); expect(AudioCue::Footstep); // No hitch burst.
    step(1, 0, 1);
    game_step(nullptr, 1); assert(game_take_audio_events().count == 0);
    step(1, 0, 1);
    step(1, 0, std::numeric_limits<float>::quiet_NaN());
    assert(game_take_audio_events().count == 0);
    step(1, 0, 1);
    game_init({}, &backend); assert(game_take_audio_events().count == 0);
    step(0, 0, 1); assert(game_take_audio_events().count == 0);

    SliceBindings bindings{};
    auto& k = bindings.keyDoor;
    k.keyActor = {2}; k.doorActor = {3};
    k.keyPosition = {0, 0, 0}; k.keyRange = .4f;
    k.doorPosition = {0, 0, -2}; k.doorRange = .5f;
    k.exitBoundaryZ = -3; k.exitHalfWidthX = 1;
    bindings.initialPlayerPose.position = k.doorPosition;
    game_init(bindings, &backend);
    step(0, 0, .01f, true); expect(AudioCue::DoorLocked);
    step(0, 0, .01f, true); expect(AudioCue::DoorLocked); // Distinct interaction intent.
    step(0, 0, .01f); assert(game_take_audio_events().count == 0);
    bindings.initialPlayerPose.position = {};
    game_init(bindings, &backend);
    step(0, 0, .01f, true); expect(AudioCue::KeyTaken);
    step(0, 0, .01f, true); assert(game_take_audio_events().count == 0);
    step(0, -1, 2, true);
    auto events = game_take_audio_events();
    assert(events.count == 2 && events.cues[0] == AudioCue::Footstep &&
           events.cues[1] == AudioCue::DoorUnlocked);
    step(0, 0, .01f, true); assert(game_take_audio_events().count == 0);
    step(0, -1, 1.1f); game_take_audio_events();
    assert(game_get_key_door_view().flags & SliceComplete);
    step(1, 0, 10, true); assert(game_take_audio_events().count == 0);
    game_init(bindings, &backend);
    assert(game_take_audio_events().count == 0);
    step(0, 0, .01f, true); expect(AudioCue::KeyTaken);
    std::puts("Audio events passed: cadence, collision, one-shot interactions, invalid input, reset.");
}
