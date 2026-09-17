#pragma once

#include "IDisplacementBackend.h"

class StaticCollisionBackend : public IDisplacementBackend {
public:
    StaticCollisionBackend();

    // Reject invalid/oversized input; never silently truncate the obstacle set.
    bool configure(
        const CollisionShape* shapes,
        unsigned int shapeCount,
        float playerRadius, float playerHeight = 0, float stepHeight = 0.3f);

    bool configureDoor(const ActorRef& door, unsigned int shapeIndex);
    void setDoorObstruction(const ActorRef& door, bool enabled) override;

    DisplacementResult resolveMove(
        const ActorRef& actor,
        const Vec3& position,
        const Vec3& desiredDelta) override;

private:
    DisplacementResult resolveMove3D(const Vec3& position, const Vec3& delta) const;
    DisplacementResult slide3D(const Vec3& position, const Vec3& delta) const;
    bool clear3D(const Vec3& position) const;
    bool isShapeEnabled(unsigned int index) const;
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
    float m_playerHeight = 0;
    float m_stepHeight = 0.3f;
    ActorRef m_doorActor{};
    unsigned int m_doorShapeIndex = MaxStaticCollisionShapes;
    bool m_doorBlocked = true;
};
