#include "RoomData.h"
#include <cmath>
#include <locale>
#include <sstream>
#include <string>

namespace {
bool keyword(std::istream& input, const char* expected) {
    std::string token;
    return (input >> token) && token == expected;
}

bool scalar(std::istream& input, float& value) {
    return (input >> value) && std::isfinite(value);
}

bool vector(std::istream& input, Vec3& value) {
    return scalar(input, value.x) && scalar(input, value.y) && scalar(input, value.z);
}

bool pose(std::istream& input, Pose& value) {
    return vector(input, value.position) && scalar(input, value.yaw) &&
           scalar(input, value.pitch) && scalar(input, value.roll);
}
}

const char* parseRoomData(const char* text, RoomData& output) {
    if (text == nullptr) return "Room text is missing.";
    // Enforce the same startup budget in every host, including Unity validation.
    std::size_t length = 0;
    while (length <= 65536 && text[length] != '\0') ++length;
    if (length > 65536) return "Room text exceeds the 64 KiB startup limit.";
    std::istringstream input(text);
    input.imbue(std::locale::classic());
    RoomData room{};
    auto& bindings = room.bindings;
    auto& transition = bindings.cameraTransition;
    int version = 0;
    if (!keyword(input, "dreamcast_room") || !(input >> version) || (version != 1 && version != 2))
        return "Unsupported room format; expected dreamcast_room 1 or 2.";
    if (!keyword(input, "player") || !(input >> bindings.playerActor.id) ||
        bindings.playerActor.id == 0 || !pose(input, bindings.initialPlayerPose) ||
        !scalar(input, bindings.playerCollisionRadius) || bindings.playerCollisionRadius <= 0)
        return "Invalid player binding, pose or collision radius.";
    if (!keyword(input, "camera") || !(input >> transition.cameraA.id) ||
        transition.cameraA.id != 1 || !pose(input, transition.poseA) ||
        !keyword(input, "camera") || !(input >> transition.cameraB.id) ||
        transition.cameraB.id != 2 || !pose(input, transition.poseB))
        return "Expected camera 1 and camera 2 with finite poses.";
    unsigned int initialCamera = 0;
    if (!keyword(input, "transition") || !scalar(input, transition.boundaryZ) ||
        !scalar(input, transition.halfWidthX) || transition.halfWidthX < 0 ||
        !(input >> initialCamera) || (initialCamera != 1 && initialCamera != 2))
        return "Invalid camera transition or initial camera ID.";
    bindings.gameplayCamera = CameraRef{initialCamera};
    bindings.initialCameraPose = initialCamera == 1 ? transition.poseA : transition.poseB;
    if (!keyword(input, "shapes") || !(input >> room.shapeCount))
        return "Missing collision shape count.";
    if (room.shapeCount > MaxStaticCollisionShapes)
        return "Room exceeds the 128 static collision shape budget.";
    for (unsigned int i = 0; i < room.shapeCount; ++i) {
        std::string type;
        CollisionShape& shape = room.shapes[i];
        if (!(input >> type) || !vector(input, shape.center))
            return "Invalid collision shape center.";
        if (type == "box") {
            shape.type = CollisionShapeType::Box;
            if (!vector(input, shape.halfExtents) || shape.halfExtents.x <= 0 ||
                shape.halfExtents.y <= 0 || shape.halfExtents.z <= 0)
                return "Box half extents must be positive and finite.";
        } else if (type == "sphere" || type == "capsule") {
            shape.type = type == "sphere" ? CollisionShapeType::Sphere : CollisionShapeType::Capsule;
            if (!scalar(input, shape.radius) || shape.radius <= 0)
                return "Collision radius must be positive and finite.";
            if (type == "capsule" && (!scalar(input, shape.height) || shape.height < 2 * shape.radius))
                return "Capsule height must be at least its diameter.";
        } else {
            return "Unknown collision shape type.";
        }
    }
    if (version == 2) {
        auto& objective = bindings.keyDoor;
        if (!keyword(input, "key") || !(input >> objective.keyActor.id) ||
            objective.keyActor.id == 0 || objective.keyActor.id == bindings.playerActor.id ||
            !vector(input, objective.keyPosition) || !scalar(input, objective.keyRange) || objective.keyRange <= 0)
            return "Invalid key actor, position or interaction range.";
        if (!keyword(input, "door") || !(input >> objective.doorActor.id) ||
            objective.doorActor.id == 0 || objective.doorActor.id == bindings.playerActor.id ||
            objective.doorActor.id == objective.keyActor.id || !vector(input, objective.doorPosition) ||
            !scalar(input, objective.doorRange) || !(input >> objective.doorShapeIndex) ||
            objective.doorShapeIndex >= room.shapeCount)
            return "Invalid door actor, position, range or collision shape index.";
        const auto& door = room.shapes[objective.doorShapeIndex];
        if (door.type != CollisionShapeType::Box ||
            std::fabs(door.center.x - objective.doorPosition.x) > 0.001f ||
            std::fabs(door.center.y - objective.doorPosition.y) > 0.001f ||
            std::fabs(door.center.z - objective.doorPosition.z) > 0.001f ||
            objective.doorRange <= door.halfExtents.z + bindings.playerCollisionRadius)
            return "Door must bind its own box and be interactable from outside the closed obstacle.";
        if (!keyword(input, "exit") || !scalar(input, objective.exitBoundaryZ) ||
            !scalar(input, objective.exitCenterX) || !scalar(input, objective.exitHalfWidthX) ||
            objective.exitHalfWidthX <= 0 ||
            objective.exitBoundaryZ >= door.center.z - door.halfExtents.z - bindings.playerCollisionRadius ||
            std::fabs(objective.exitCenterX - door.center.x) + objective.exitHalfWidthX >
                door.halfExtents.x - bindings.playerCollisionRadius)
            return "Exit must be south of the door and within its walkable opening.";
    }
    if (!keyword(input, "end")) return "Expected end of room.";
    input >> std::ws;
    if (!input.eof()) return "Unexpected trailing room data.";
    output = room;
    return nullptr;
}
