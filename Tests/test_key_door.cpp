#include <cassert>
#include <cmath>
#include <cstdio>
#include <fstream>
#include <iterator>
#include <string>
#include "Game.h"
#include "MockBackend.h"
#include "RoomData.h"
#include "StaticCollisionBackend.h"

static void tick(float x = 0, float z = 0, bool interact = false, float seconds = 1.0f / 60) {
    InputFrame input{{x, z}, interact};
    game_step(&input, seconds);
}

static void walk(float target, bool xAxis) {
    for (int i = 0; i < 1800; ++i) {
        Vec3 p = game_get_player_pos();
        float delta = target - (xAxis ? p.x : p.z);
        if (std::fabs(delta) < 0.001f) return;
        const float direction = delta > 0 ? 1.0f : -1.0f;
        const float seconds = std::fmin(std::fabs(delta), 1.0f / 60);
        tick(xAxis ? direction : 0, xAxis ? 0 : direction, false, seconds);
    }
    assert(false && "The authored route is blocked.");
}

int main() {
    MockBackend mock;
    SliceBindings bindings{};
    auto& objective = bindings.keyDoor;
    objective.keyActor = {2}; objective.doorActor = {3};
    objective.keyPosition = {3, 0.6f, 0};
    objective.doorPosition = {0, 1.1f, -5};
    objective.exitBoundaryZ = -6;
    objective.exitHalfWidthX = 1;
    game_init(bindings, &mock);
    assert(mock.lastDoor.id == 3 && mock.doorBlocked);
    tick(0, 0, true);
    assert(game_get_key_door_view().flags == ObjectiveEnabled); // Out of range.
    assert(game_get_key_door_view().prompt == InteractionPrompt::None);

    // Input magnitude must not change the game's chosen movement speed.
    tick(1, 1, false, 1);
    Vec3 p = game_get_player_pos();
    assert(std::fabs(p.x * p.x + p.z * p.z - 1) < 0.001f);
    game_init(bindings, &mock);
    tick(0.5f, 0, false, 1);
    assert(std::fabs(game_get_player_pos().x - 0.5f) < 0.001f);

    bindings.initialPlayerPose.position = {0, 0, -4};
    game_init(bindings, &mock);
    assert(game_get_key_door_view().prompt == InteractionPrompt::LockedDoor);
    tick(0, 0, true);
    assert(game_get_key_door_view().feedback == SliceFeedback::DoorLocked);
    assert(mock.doorBlocked);
    tick(0, 0, false, 3);
    assert(game_get_key_door_view().feedback == SliceFeedback::None);
    tick(0, -1, false, 3); // Even a non-blocking test host cannot complete a locked objective.
    assert(!(game_get_key_door_view().flags & SliceComplete));

    bindings.initialPlayerPose.position = objective.keyPosition;
    game_init(bindings, &mock);
    assert(game_get_key_door_view().prompt == InteractionPrompt::TakeKey);
    tick(); // Merely standing near the key does not collect it.
    assert(!(game_get_key_door_view().flags & HasKey));
    tick(0, 0, true);
    assert(game_get_key_door_view().flags & HasKey);
    assert(game_get_key_door_view().feedback == SliceFeedback::KeyTaken);
    tick(0, 0, true);
    assert(game_get_key_door_view().prompt == InteractionPrompt::None);
    assert(mock.doorBlocked); // Picking up a key alone never opens the door.
    walk(0, true); walk(-4, false);
    assert(game_get_key_door_view().prompt == InteractionPrompt::UnlockDoor);
    tick(); assert(mock.doorBlocked);
    tick(0, 0, true);
    assert(!mock.doorBlocked && (game_get_key_door_view().flags & DoorOpen));
    assert(game_get_key_door_view().feedback == SliceFeedback::DoorUnlocked);
    unsigned int changes = mock.doorChanges;
    tick(0, 0, true);
    assert(mock.doorChanges == changes); // No repeated opening or accidental re-closing.
    assert(!(game_get_key_door_view().flags & SliceComplete));
    tick(0, -1, false, game_get_player_pos().z - objective.exitBoundaryZ); // Exact boundary landing.
    assert(game_get_key_door_view().flags & SliceComplete);
    assert(game_get_key_door_view().prompt == InteractionPrompt::Complete);
    p = game_get_player_pos(); tick(1, 1, true, 1);
    assert(game_get_player_pos().x == p.x && game_get_player_pos().z == p.z);

    // Reinitialization restores key, closed door and incomplete state.
    game_init(bindings, &mock);
    assert(game_get_key_door_view().flags == ObjectiveEnabled && mock.doorBlocked);
    tick(0, 0, true); walk(0, true); walk(-4, false); tick(0, 0, true);
    walk(2, true); tick(0, -1, false, 3);
    assert(!(game_get_key_door_view().flags & SliceComplete)); // Outside exit width.

    // Complete the actual authored room using real collision and small frame steps.
    std::ifstream file("Unity/Dreamcast-Horror/Assets/StreamingAssets/sample.room");
    assert(file.is_open());
    const std::string text((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    RoomData room{};
    assert(parseRoomData(text.c_str(), room) == nullptr && room.bindings.keyDoor.keyActor.id != 0);
    StaticCollisionBackend collision;
    assert(collision.configure(room.shapes, room.shapeCount, room.bindings.playerCollisionRadius));
    assert(collision.configureDoor(room.bindings.keyDoor.doorActor, room.bindings.keyDoor.doorShapeIndex));
    game_init(room.bindings, &collision);
    walk(1.1f, true); walk(-4, false); walk(0, true);
    tick(0, 0, true);
    assert(game_get_key_door_view().feedback == SliceFeedback::DoorLocked);
    for (int i = 0; i < 180; ++i) tick(0, -1);
    assert(game_get_player_pos().z > -5 && !(game_get_key_door_view().flags & SliceComplete));
    walk(1.1f, true); walk(2.8f, false); walk(0, true);
    assert(game_get_key_door_view().prompt == InteractionPrompt::TakeKey);
    tick(0, 0, true);
    assert(game_get_key_door_view().flags & HasKey);
    walk(1.1f, true); walk(-4, false); walk(0, true);
    tick(0, 0, true);
    assert(game_get_key_door_view().flags & DoorOpen);
    for (int i = 0; i < 180; ++i) tick(0, -1);
    assert(game_get_key_door_view().flags & SliceComplete);
    assert(game_get_player_pos().z <= room.bindings.keyDoor.exitBoundaryZ);
    assert(std::fabs(game_get_player_pos().y) < 0.001f);
    game_init(room.bindings, &collision);
    assert(game_get_key_door_view().flags == ObjectiveEnabled);
    const auto blocked = collision.resolveMove({1}, {0, 0, -4}, {0, 0, -2});
    assert(blocked.blocked && blocked.resolvedDelta.z > -1);
    std::puts("Key-door rules, controller magnitude, feedback, reset and full real-room playthrough passed.");
}
