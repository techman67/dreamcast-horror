#pragma once
#include "IDisplacementBackend.h"

class MockBackend : public IDisplacementBackend {
public:
    Vec3 nextResolvedDelta{};
    bool nextBlocked = false;
    bool useScriptedResponse = false;

    ActorRef lastActor{};
    Vec3 lastPosition{};
    Vec3 lastDesiredDelta{};

    DisplacementResult resolveMove(
        const ActorRef& actor,
        const Vec3& position,
        const Vec3& desiredDelta) override {

        lastActor = actor;
        lastPosition = position;
        lastDesiredDelta = desiredDelta;

        if (useScriptedResponse) {
            return { nextResolvedDelta, nextBlocked };
        }

        return { desiredDelta, false };
    }
};
