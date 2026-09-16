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
    std::ifstream file("Unity/Dreamcast-Horror/Assets/StreamingAssets/sample.room");
    assert(file.is_open());
    const std::string text((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    RoomData room{};
    assert(parseRoomData(text.c_str(), room) == nullptr);
    assert(room.shapeCount == 10);
    assert(room.bindings.cameraTransition.cameraA.id == 1);
    assert(room.bindings.cameraTransition.cameraB.id == 2);
    assert(std::fabs(room.bindings.cameraTransition.poseA.position.y - 6.33f) < 0.001f);
    assert(std::fabs(room.bindings.cameraTransition.poseB.position.z - 4.74f) < 0.001f);
    assert(std::fabs(room.bindings.playerCollisionRadius - 0.35f) < 0.0001f);

    // The real exported room must allow passage beneath its decorative lintel.
    StaticCollisionBackend collision;
    collision.configure(room.shapes, room.shapeCount, room.bindings.playerCollisionRadius);
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
        assert(room.shapeCount == 10);
        assert(std::fabs(room.bindings.cameraTransition.poseA.position.y - 6.33f) < 0.001f);
    };
    reject(replace(text, "dreamcast_room 1", "dreamcast_room 2"));
    reject(replace(text, "shapes 10", "shapes 129"));
    reject(replace(text, "shapes 10", "shapes -1"));
    reject(replace(text, "camera 2", "camera 1"));
    reject(replace(text, "box", "unknown"));
    reject(replace(text, "player 1", "player 0"));
    reject(text.substr(0, text.find("shapes")));
    reject(text + "unexpected");
    assert(parseRoomData(nullptr, room) != nullptr);

    const auto prefix = text.substr(0, text.find("shapes"));
    std::string maximum = prefix + "shapes 128\n";
    for (int i = 0; i < 128; ++i) maximum += "box 10 0 10 1 1 1\n";
    maximum += "end\n";
    assert(parseRoomData(maximum.c_str(), room) == nullptr && room.shapeCount == 128);
    assert(parseRoomData((prefix + "shapes 1\nsphere 0 0 0 -1\nend").c_str(), room) != nullptr);
    assert(parseRoomData((prefix + "shapes 1\nbox 0 0 0 1 0 1\nend").c_str(), room) != nullptr);
    assert(parseRoomData((prefix + "shapes 1\ncapsule 0 0 0 1 1\nend").c_str(), room) != nullptr);
    assert(parseRoomData((prefix + "shapes 1\nsphere nan 0 0 1\nend").c_str(), room) != nullptr);
    assert(parseRoomData((prefix + "shapes 0\nend").c_str(), room) == nullptr);
    std::puts("Shared room parsing, budget rejection and real room integration tests passed.");
}
