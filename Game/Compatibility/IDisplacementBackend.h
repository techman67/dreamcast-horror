#pragma once
#include "Types.h"

// Result of resolving a desired kinematic displacement against the world.
//
// resolvedDelta : the displacement the backend actually allowed.
// blocked       : true if the requested displacement could not be fully
//                 satisfied. This is a partial-resolution signal, NOT a
//                 "movement failed" signal. Game logic must apply
//                 resolvedDelta even when blocked == true.
struct DisplacementResult {
    Vec3 resolvedDelta;
    bool blocked = false;
};

class IDisplacementBackend {
public:
    virtual ~IDisplacementBackend() = default;

    // Resolve a desired displacement from a given position.
    // The backend must never mutate Game state; it only reports what
    // displacement it is willing to allow.
    virtual DisplacementResult resolveMove(const Vec3& position,
                                           const Vec3& desiredDelta) = 0;
};