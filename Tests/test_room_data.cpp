#include <cassert>
#include <cmath>
#include <cstdio>
#include <fstream>
#include <iterator>
#include <string>
#include "RoomData.h"
#include "Game.h"
#include "StaticCollisionBackend.h"

static std::string replace(std::string text, const std::string& from, const std::string& to) {
    auto offset = text.find(from);
    assert(offset != std::string::npos);
    text.replace(offset, from.size(), to);
    return text;
}

int main() {
    std::ifstream file("Tests/Fixtures/sample-v2.room");
    assert(file.is_open());
    const std::string text((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    RoomData room{};
    assert(parseRoomData(text.c_str(), room) == nullptr);
    assert(room.shapeCount == 11);
    assert(room.bindings.cameraTransition.cameraA.id == 1);
    assert(room.bindings.cameraTransition.cameraB.id == 2);
    assert(std::fabs(room.bindings.cameraTransition.poseA.position.y - 6.33f) < 0.001f);
    assert(std::fabs(room.bindings.cameraTransition.poseB.position.z - 4.74f) < 0.001f);
    assert(std::fabs(room.bindings.playerCollisionRadius - 0.35f) < 0.0001f);

    // The real exported room must allow passage beneath its decorative lintel.
    StaticCollisionBackend collision;
    collision.configure(room.shapes, room.shapeCount, room.bindings.playerCollisionRadius);
    assert(collision.configureDoor(room.bindings.keyDoor.doorActor, room.bindings.keyDoor.doorShapeIndex));
    collision.setDoorObstruction(room.bindings.keyDoor.doorActor, false);
    auto result = collision.resolveMove({1}, {0, 0, -4}, {0, 0, -2});
    assert(std::fabs(result.resolvedDelta.z + 2.0f) < 0.0001f);
    assert(!result.blocked);
    result = collision.resolveMove({1}, {4, 0, 0}, {2, 0, 0});
    assert(result.blocked && std::fabs(result.resolvedDelta.x - 0.55f) < 0.0001f);

    // Shared initialization + real displacement + camera transition, without Unity.
    game_init(room.bindings, &collision);
    const InputFrame forward{{0, 1}, false};
    game_step(&forward, 0.8f);
    assert(game_get_camera_ref().id == 2);
    assert(std::fabs(game_get_camera_pose().position.z - 4.74f) < 0.001f);

    // Failed parsing must leave the existing output intact.
    auto reject = [&](const std::string& invalid) {
        assert(parseRoomData(invalid.c_str(), room) != nullptr);
        assert(room.shapeCount == 11);
        assert(std::fabs(room.bindings.cameraTransition.poseA.position.y - 6.33f) < 0.001f);
    };
    reject(replace(text, "dreamcast_room 2", "dreamcast_room 9"));
    reject(replace(text, "shapes 11", "shapes 129"));
    reject(replace(text, "shapes 11", "shapes -1"));
    reject(replace(text, "camera 2", "camera 1"));
    reject(replace(text, "box", "unknown"));
    reject(replace(text, "player 1", "player 0"));
    reject(text.substr(0, text.find("shapes")));
    reject(text + "unexpected");
    reject(text + std::string(65537, ' ')); // Whitespace still consumes startup RAM.
    reject(replace(text, "key 2", "key 1"));
    reject(replace(text, "door 3", "door 2"));
    reject(replace(text, "exit -5.9", "exit 0"));
    reject(text.substr(0, text.find("key 2")) + "end");
    assert(parseRoomData(nullptr, room) != nullptr);

    // Version 1 stays valid for movement-only rooms; version 2 requires its objective.
    const auto prefix = replace(text.substr(0, text.find("shapes")), "dreamcast_room 2", "dreamcast_room 1");
    std::string maximum = prefix + "shapes 128\n";
    for (int i = 0; i < 128; ++i) maximum += "box 10 0 10 1 1 1\n";
    maximum += "end\n";
    assert(parseRoomData(maximum.c_str(), room) == nullptr && room.shapeCount == 128);
    assert(parseRoomData((prefix + "shapes 1\nsphere 0 0 0 -1\nend").c_str(), room) != nullptr);
    assert(parseRoomData((prefix + "shapes 1\nbox 0 0 0 1 0 1\nend").c_str(), room) != nullptr);
    assert(parseRoomData((prefix + "shapes 1\ncapsule 0 0 0 1 1\nend").c_str(), room) != nullptr);
    assert(parseRoomData((prefix + "shapes 1\nsphere nan 0 0 1\nend").c_str(), room) != nullptr);
    assert(parseRoomData((prefix + "shapes 0\nend").c_str(), room) == nullptr);
    const std::string v3="dreamcast_room 3\nplayer 1 0 0 0 0 0 0 0.35\ncharacter 1.8 0.3\ncamera 1 0 6 -5 0 0 0\ncamera 2 0 6 5 0 0 0\ntransition 1 4 1\nshapes 1\nbox 0 -0.1 0 10 0.1 10\nobjective 0\nend\n";
    assert(parseRoomData(v3.c_str(),room)==nullptr && room.bindings.playerHeight==1.8f);
    for (const auto& invalid : {replace(v3,"character 1.8 0.3","character 0.2 0.3"),
        replace(v3,"character 1.8 0.3","character 1.8 0.6"),
        replace(v3,"box 0 -0.1 0 10 0.1 10","sphere 0 -1 0 1"),
        replace(v3,"box 0 -0.1 0","box 0 0 0"),
        replace(v3,"objective 0","objective 2"), replace(v3,"box 0 -0.1 0","box 10001 -0.1 0")}) {
        assert(parseRoomData(invalid.c_str(),room)!=nullptr && room.bindings.playerHeight==1.8f);
    }
    std::puts("Shared room parsing, budget rejection and real room integration tests passed.");
}
