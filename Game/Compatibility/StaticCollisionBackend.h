#pragma once

#include "IDisplacementBackend.h"

class StaticCollisionBackend : public IDisplacementBackend {
public:
    StaticCollisionBackend();

    // Reject invalid/oversized input; never silently truncate the obstacle set.
    bool configure(
        const CollisionShape* shapes,
        unsigned int shapeCount,
        float playerRadius);

    DisplacementResult resolveMove(
        const ActorRef& actor,
        const Vec3& position,
        const Vec3& desiredDelta) override;

private:
    float resolveX(
        const Vec3& position,
        float desiredX,
        bool& blocked) const;

    float resolveZ(
        const Vec3& position,
        float desiredZ,
        bool& blocked) const;

    float resolveBoxAxis(
        float currentAxis,
        float desiredAxis,
        float otherAxis,
        float boxMinAxis,
        float boxMaxAxis,
        float boxMinOther,
        float boxMaxOther,
        bool& blocked) const;

    float resolveCircleAxis(
        float currentAxis,
        float desiredAxis,
        float otherAxis,
        float centerAxis,
        float centerOther,
        float radius,
        bool& blocked) const;

private:
    const CollisionShape* m_shapes;
    unsigned int m_shapeCount;
    float m_playerRadius;
};
