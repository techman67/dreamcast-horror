#pragma once
#include "AuthoredBindings.h"

// Versioned startup data, with no engine objects or resource identifiers.
// Fixed storage survives initialization; parsing is never a per-frame operation.
struct RoomData {
    SliceBindings bindings{};
    CollisionShape shapes[MaxStaticCollisionShapes]{};
    unsigned int shapeCount = 0;
};

// Returns nullptr on success, otherwise a static diagnostic string.
// On failure output is unchanged. The host owns reading the text from storage.
const char* parseRoomData(const char* text, RoomData& output);
