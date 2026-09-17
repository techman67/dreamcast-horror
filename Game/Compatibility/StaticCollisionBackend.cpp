#include "StaticCollisionBackend.h"

#include <cmath>
#include <initializer_list>

namespace {

constexpr float Epsilon = 0.000001f;

float clampFloat(
    float value,
    float minimum,
    float maximum) {

    if (value < minimum)
        return minimum;

    if (value > maximum)
        return maximum;

    return value;
}

}

StaticCollisionBackend::StaticCollisionBackend()
    : m_shapes(nullptr),
      m_shapeCount(0),
      m_playerRadius(0.35f) {
}

bool StaticCollisionBackend::configure(
    const CollisionShape* shapes,
    unsigned int shapeCount,
    float playerRadius, float playerHeight, float stepHeight) {

    if (shapeCount > MaxStaticCollisionShapes ||
        (shapes == nullptr && shapeCount != 0) ||
        !std::isfinite(playerRadius) || playerRadius < 0.0f ||
        !std::isfinite(playerHeight) || playerHeight < 0 || playerHeight > 4 ||
        !std::isfinite(stepHeight) || stepHeight < 0 || stepHeight > 0.5f ||
        (playerHeight > 0 && (playerHeight < 0.5f || playerRadius <= 0 || playerRadius > 1 || stepHeight >= playerHeight))) {
        return false;
    }
    if (playerHeight > 0) for (unsigned i = 0; i < shapeCount; ++i) {
        const auto& s = shapes[i];
        if (s.type != CollisionShapeType::Box) return false;
        for (float value : {s.center.x,s.center.y,s.center.z,s.halfExtents.x,s.halfExtents.y,s.halfExtents.z})
            if (!std::isfinite(value) || std::fabs(value) > 10000) return false;
        if (s.halfExtents.x <= 0 || s.halfExtents.y <= 0 || s.halfExtents.z <= 0) return false;
    }
    m_shapes = shapes;
    m_shapeCount = shapeCount;
    m_playerRadius = playerRadius;
    m_playerHeight = playerHeight;
    m_stepHeight = stepHeight;
    m_doorActor = ActorRef{};
    m_doorShapeIndex = MaxStaticCollisionShapes;
    m_doorBlocked = true;
    return true;
}

bool StaticCollisionBackend::configureDoor(const ActorRef& door, unsigned int shapeIndex) {
    if (door.id == 0 || shapeIndex >= m_shapeCount) return false;
    m_doorActor = door;
    m_doorShapeIndex = shapeIndex;
    m_doorBlocked = true;
    return true;
}

void StaticCollisionBackend::setDoorObstruction(const ActorRef& door, bool enabled) {
    if (door.id == m_doorActor.id) m_doorBlocked = enabled;
}

bool StaticCollisionBackend::isShapeEnabled(unsigned int index) const {
    return index != m_doorShapeIndex || m_doorBlocked;
}

DisplacementResult
StaticCollisionBackend::resolveMove(
    const ActorRef&,
    const Vec3& position,
    const Vec3& desiredDelta) {

    if (m_playerHeight > 0) return resolveMove3D(position, desiredDelta);

    if (m_shapes == nullptr || m_shapeCount == 0) {
        return { desiredDelta, false };
    }

    // --- Attempt 1: X-first axis-separated ---
    bool blockedX_1 = false;
    const float x_1 = resolveX(position, position.x + desiredDelta.x, blockedX_1);
    Vec3 mid_1 = position; mid_1.x = x_1;
    bool blockedZ_1 = blockedX_1;
    const float z_1 = resolveZ(mid_1, position.z + desiredDelta.z, blockedZ_1);
    const float dx_1 = x_1 - position.x;
    const float dz_1 = z_1 - position.z;
    const float len_1 = dx_1 * dx_1 + dz_1 * dz_1;

    // --- Attempt 2: Z-first axis-separated ---
    bool blockedZ_2 = false;
    const float z_2 = resolveZ(position, position.z + desiredDelta.z, blockedZ_2);
    Vec3 mid_2 = position; mid_2.z = z_2;
    bool blockedX_2 = blockedZ_2;
    const float x_2 = resolveX(mid_2, position.x + desiredDelta.x, blockedX_2);
    const float dx_2 = x_2 - position.x;
    const float dz_2 = z_2 - position.z;
    const float len_2 = dx_2 * dx_2 + dz_2 * dz_2;

    float bestLen = len_1;
    float bestDx = dx_1;
    float bestDz = dz_1;
    bool bestBlocked = blockedX_1 || blockedZ_1;

    if (len_2 > bestLen) {
        bestLen = len_2;
        bestDx = dx_2;
        bestDz = dz_2;
        bestBlocked = blockedX_2 || blockedZ_2;
    }

    // --- Collect candidate slide normals ---
    const int MaxNormals = 32;
    float nrmX[MaxNormals];
    float nrmZ[MaxNormals];
    int nrmCount = 0;

    float sumX = 0.0f;
    float sumZ = 0.0f;

    const float touch = 0.05f;

    for (unsigned int i = 0;
         i < m_shapeCount && nrmCount < MaxNormals - 2;
         ++i) {

        if (!isShapeEnabled(i)) continue;
        const CollisionShape& shape = m_shapes[i];

        if (shape.type == CollisionShapeType::Box) {

            const float ex = shape.halfExtents.x + m_playerRadius;
            const float ez = shape.halfExtents.z + m_playerRadius;
            const float minX = shape.center.x - ex;
            const float maxX = shape.center.x + ex;
            const float minZ = shape.center.z - ez;
            const float maxZ = shape.center.z + ez;

            if (nrmCount < MaxNormals && position.x <= minX + touch && position.x >= minX - touch &&
                position.z >= minZ - touch && position.z <= maxZ + touch) {
                nrmX[nrmCount] = -1.0f; nrmZ[nrmCount] = 0.0f;
                ++nrmCount; sumX -= 1.0f;
            }
            if (nrmCount < MaxNormals && position.x >= maxX - touch && position.x <= maxX + touch &&
                position.z >= minZ - touch && position.z <= maxZ + touch) {
                nrmX[nrmCount] = 1.0f; nrmZ[nrmCount] = 0.0f;
                ++nrmCount; sumX += 1.0f;
            }
            if (nrmCount < MaxNormals && position.z <= minZ + touch && position.z >= minZ - touch &&
                position.x >= minX - touch && position.x <= maxX + touch) {
                nrmX[nrmCount] = 0.0f; nrmZ[nrmCount] = -1.0f;
                ++nrmCount; sumZ -= 1.0f;
            }
            if (nrmCount < MaxNormals && position.z >= maxZ - touch && position.z <= maxZ + touch &&
                position.x >= minX - touch && position.x <= maxX + touch) {
                nrmX[nrmCount] = 0.0f; nrmZ[nrmCount] = 1.0f;
                ++nrmCount; sumZ += 1.0f;
            }

            // Corner normal: from the box's actual (unexpanded) corner
            // to the player, when the player is past that corner.
            const float actualMinX = shape.center.x - shape.halfExtents.x;
            const float actualMaxX = shape.center.x + shape.halfExtents.x;
            const float actualMinZ = shape.center.z - shape.halfExtents.z;
            const float actualMaxZ = shape.center.z + shape.halfExtents.z;

            const float cornerX = (position.x < shape.center.x) ? actualMinX : actualMaxX;
            const float cornerZ = (position.z < shape.center.z) ? actualMinZ : actualMaxZ;

            const bool pastX = (position.x < shape.center.x)
                ? (position.x < actualMinX)
                : (position.x > actualMaxX);
            const bool pastZ = (position.z < shape.center.z)
                ? (position.z < actualMinZ)
                : (position.z > actualMaxZ);

            if (pastX && pastZ) {
                const float cdx = position.x - cornerX;
                const float cdz = position.z - cornerZ;
                const float cdistSq = cdx * cdx + cdz * cdz;
                const float reach = m_playerRadius * 1.6f + touch;

                if (cdistSq > 1e-10f && cdistSq < reach * reach) {
                    const float cdist = std::sqrt(cdistSq);
                    const float cnx = cdx / cdist;
                    const float cnz = cdz / cdist;
                    if (nrmCount < MaxNormals) {
                        nrmX[nrmCount] = cnx; nrmZ[nrmCount] = cnz;
                        ++nrmCount;
                    }
                    sumX += cnx; sumZ += cnz;
                }
            }
        } else {
            const float r = shape.radius + m_playerRadius;
            const float dx = position.x - shape.center.x;
            const float dz = position.z - shape.center.z;
            const float distSq = dx * dx + dz * dz;

            if (distSq > 1e-10f) {
                const float dist = std::sqrt(distSq);
                if (dist < r + 0.1f) {
                    const float nx = dx / dist;
                    const float nz = dz / dist;
                    if (nrmCount < MaxNormals) {
                        nrmX[nrmCount] = nx; nrmZ[nrmCount] = nz;
                        ++nrmCount;
                    }
                    sumX += nx; sumZ += nz;
                }
            }
        }
    }

    // Add the summed-and-normalized normal, if any.
    const float sumLenSq = sumX * sumX + sumZ * sumZ;
    if (sumLenSq > 1e-10f && nrmCount < MaxNormals) {
        const float sumLen = std::sqrt(sumLenSq);
        nrmX[nrmCount] = sumX / sumLen;
        nrmZ[nrmCount] = sumZ / sumLen;
        ++nrmCount;
    }

    // --- Try each normal as a slide direction, both orders ---

    for (int i = 0; i < nrmCount; ++i) {

        const float nx = nrmX[i];
        const float nz = nrmZ[i];

        const float dot = desiredDelta.x * nx + desiredDelta.z * nz;
        const float tx = desiredDelta.x - dot * nx;
        const float tz = desiredDelta.z - dot * nz;

        if (tx * tx + tz * tz < 1e-10f) continue;

        // Order A: X-first
        {
            bool b = false;
            const float rx = resolveX(position, position.x + tx, b);
            Vec3 mid = position; mid.x = rx;
            const float rz = resolveZ(mid, position.z + tz, b);

            const float dx = rx - position.x;
            const float dz = rz - position.z;
            const float len = dx * dx + dz * dz;

            if (len > bestLen) {
                bestLen = len;
                bestDx = dx;
                bestDz = dz;
                bestBlocked = true;
            }
        }

        // Order B: Z-first
        {
            bool b = false;
            const float rz = resolveZ(position, position.z + tz, b);
            Vec3 mid = position; mid.z = rz;
            const float rx = resolveX(mid, position.x + tx, b);

            const float dx = rx - position.x;
            const float dz = rz - position.z;
            const float len = dx * dx + dz * dz;

            if (len > bestLen) {
                bestLen = len;
                bestDx = dx;
                bestDz = dz;
                bestBlocked = true;
            }
        }
    }

    return {
        Vec3{ bestDx, desiredDelta.y, bestDz },
        bestBlocked
    };
}

float StaticCollisionBackend::resolveX(
    const Vec3& position,
    float desiredX,
    bool& blocked) const {

    // Do not depenetrate an axis that this candidate does not request.
    // Otherwise the alternate axis order can win by making a large sideways
    // correction when the player is only barely inside a round obstacle.
    if (desiredX == position.x) return desiredX;

    float resolvedX =
        desiredX;

    for (unsigned int i = 0;
         i < m_shapeCount;
         ++i) {

        if (!isShapeEnabled(i)) continue;
        const CollisionShape& shape =
            m_shapes[i];

        if (shape.type ==
            CollisionShapeType::Box) {

            const float minX =
                shape.center.x -
                shape.halfExtents.x -
                m_playerRadius;

            const float maxX =
                shape.center.x +
                shape.halfExtents.x +
                m_playerRadius;

            const float minZ =
                shape.center.z -
                shape.halfExtents.z -
                m_playerRadius;

            const float maxZ =
                shape.center.z +
                shape.halfExtents.z +
                m_playerRadius;

            resolvedX =
                resolveBoxAxis(
                    position.x,
                    resolvedX,
                    position.z,
                    minX,
                    maxX,
                    minZ,
                    maxZ,
                    blocked);
        }
        else {
            resolvedX =
                resolveCircleAxis(
                    position.x,
                    resolvedX,
                    position.z,
                    shape.center.x,
                    shape.center.z,
                    shape.radius +
                        m_playerRadius,
                    blocked);
        }
    }

    return resolvedX;
}

float StaticCollisionBackend::resolveZ(
    const Vec3& position,
    float desiredZ,
    bool& blocked) const {

    if (desiredZ == position.z) return desiredZ;

    float resolvedZ =
        desiredZ;

    for (unsigned int i = 0;
         i < m_shapeCount;
         ++i) {

        if (!isShapeEnabled(i)) continue;
        const CollisionShape& shape =
            m_shapes[i];

        if (shape.type ==
            CollisionShapeType::Box) {

            const float minX =
                shape.center.x -
                shape.halfExtents.x -
                m_playerRadius;

            const float maxX =
                shape.center.x +
                shape.halfExtents.x +
                m_playerRadius;

            const float minZ =
                shape.center.z -
                shape.halfExtents.z -
                m_playerRadius;

            const float maxZ =
                shape.center.z +
                shape.halfExtents.z +
                m_playerRadius;

            resolvedZ =
                resolveBoxAxis(
                    position.z,
                    resolvedZ,
                    position.x,
                    minZ,
                    maxZ,
                    minX,
                    maxX,
                    blocked);
        }
        else {
            resolvedZ =
                resolveCircleAxis(
                    position.z,
                    resolvedZ,
                    position.x,
                    shape.center.z,
                    shape.center.x,
                    shape.radius +
                        m_playerRadius,
                    blocked);
        }
    }

    return resolvedZ;
}

float StaticCollisionBackend::resolveBoxAxis(
    float currentAxis,
    float desiredAxis,
    float otherAxis,
    float boxMinAxis,
    float boxMaxAxis,
    float boxMinOther,
    float boxMaxOther,
    bool& blocked) const {

    // If the player is exactly touching the boundary but not
    // penetrating it, movement parallel to the boundary remains valid.
if (otherAxis <= boxMinOther + 0.01f ||
        otherAxis >= boxMaxOther - 0.01f) {

        return desiredAxis;
    }

    // Already penetrating this box along this axis.
    //
    // Push out toward the NEAREST edge rather than toward the
    // direction of movement.
    //
    // Resolving toward the movement direction teleports the
    // player across the entire box when they are only barely
    // inside it, which is what the previous version did.
    if (currentAxis > boxMinAxis &&
        currentAxis < boxMaxAxis) {

        blocked = true;

        const float distanceToMin =
            currentAxis - boxMinAxis;

        const float distanceToMax =
            boxMaxAxis - currentAxis;

        if (distanceToMin < distanceToMax) {
            return boxMinAxis;
        }

        return boxMaxAxis;
    }

    // Moving toward the positive side.
    if (desiredAxis > currentAxis) {

        // Swept crossing.
        if (currentAxis <= boxMinAxis &&
            desiredAxis > boxMinAxis) {

            blocked = true;

            return boxMinAxis;
        }
    }

    // Moving toward the negative side.
    if (desiredAxis < currentAxis) {

        // Swept crossing.
        if (currentAxis >= boxMaxAxis &&
            desiredAxis < boxMaxAxis) {

            blocked = true;

            return boxMaxAxis;
        }
    }

    return desiredAxis;
}

float StaticCollisionBackend::resolveCircleAxis(
    float currentAxis,
    float desiredAxis,
    float otherAxis,
    float centerAxis,
    float centerOther,
    float radius,
    bool& blocked) const {

    const float otherDistance =
        otherAxis -
        centerOther;

    const float otherDistanceSquared =
        otherDistance *
        otherDistance;

    const float radiusSquared =
        radius *
        radius;

    // Completely outside the circle's horizontal span.
    if (otherDistanceSquared >=
        radiusSquared) {

        return desiredAxis;
    }

    // Calculate the allowed axis distance from the center.
    const float remainingSquared =
        radiusSquared -
        otherDistanceSquared;

    const float axisDistance =
        std::sqrt(
            remainingSquared);

    const float minAxis =
        centerAxis -
        axisDistance;

    const float maxAxis =
        centerAxis +
        axisDistance;

    // Already penetrating this circle along this axis.
    //
    // Push out toward the NEAREST edge. Same reasoning as the
    // box case above.
    if (currentAxis > minAxis &&
        currentAxis < maxAxis) {

        blocked = true;

        const float distanceToMin =
            currentAxis - minAxis;

        const float distanceToMax =
            maxAxis - currentAxis;

        if (distanceToMin < distanceToMax) {
            return minAxis;
        }

        return maxAxis;
    }

    // Moving positive.
    if (desiredAxis > currentAxis) {

        // Sweep crossed the circle boundary.
        if (currentAxis <= minAxis &&
            desiredAxis > minAxis) {

            blocked = true;

            return minAxis;
        }
    }

    // Moving negative.
    if (desiredAxis < currentAxis) {

        // Sweep crossed the circle boundary.
        if (currentAxis >= maxAxis &&
            desiredAxis < maxAxis) {

            blocked = true;

            return maxAxis;
        }
    }

    return desiredAxis;
}
