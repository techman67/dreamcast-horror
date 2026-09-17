#pragma once
#include "AuthoredBindings.h"
#include "Types.h"

// Result of resolving a desired kinematic displacement.
//
// resolvedDelta:
//     The movement the backend actually allowed.
//
// blocked:
//     True when the complete requested movement could not be satisfied.
//
// IMPORTANT:
// blocked does NOT mean "don't move".
// Game logic must always apply resolvedDelta.
struct DisplacementResult {
    Vec3 resolvedDelta;
    bool blocked = false;
    bool grounded = false;
    bool hitCeiling = false;
};

class IDisplacementBackend {
public:
    virtual ~IDisplacementBackend() = default;

    virtual DisplacementResult resolveMove(
        const ActorRef& actor,
        const Vec3& position,
        const Vec3& desiredDelta) = 0;

    // Only the single authored door is mutable. This is not general physics.
    virtual void setDoorObstruction(const ActorRef& door, bool enabled) = 0;
};
