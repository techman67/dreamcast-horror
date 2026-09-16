#pragma once
#include "IDisplacementBackend.h"

// Deterministic, scriptable mock of IDisplacementBackend.
// Records the last request so tests can assert what Game asked for,
// and returns a scripted response so tests can control the result.
class MockBackend : public IDisplacementBackend {
public:
    // Scripted response for the next resolveMove call.
    Vec3 nextResolvedDelta{};
    bool nextBlocked = false;
    bool useScriptedResponse = false;

    // Last observed request (for assertions).
    Vec3 lastPosition{};
    Vec3 lastDesiredDelta{};

    DisplacementResult resolveMove(const Vec3& position,
                                   const Vec3& desiredDelta) override {
        lastPosition = position;
        lastDesiredDelta = desiredDelta;

        if (useScriptedResponse) {
            return { nextResolvedDelta, nextBlocked };
        }

        // Default: allow the full requested displacement.
        return { desiredDelta, false };
    }
};