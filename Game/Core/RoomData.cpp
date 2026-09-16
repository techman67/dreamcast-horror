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
    std::istringstream input(text);
    input.imbue(std::locale::classic());
    RoomData room{};
    auto& bindings = room.bindings;
    auto& transition = bindings.cameraTransition;
    int version = 0;
    if (!keyword(input, "dreamcast_room") || !(input >> version) || version != 1)
        return "Unsupported room format; expected dreamcast_room 1.";
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
    if (!keyword(input, "end")) return "Expected end of room.";
    input >> std::ws;
    if (!input.eof()) return "Unexpected trailing room data.";
    output = room;
    return nullptr;
}
