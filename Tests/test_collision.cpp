#include <cassert>
#include <cmath>
#include <cstdio>
#include <initializer_list>
#include "StaticCollisionBackend.h"

static bool near(float a, float b) { return std::fabs(a - b) < 0.001f; }

int main() {
    StaticCollisionBackend backend;
    auto move = [&](Vec3 from, Vec3 delta) { return backend.resolveMove({1}, from, delta); };
    auto result = move({0, 0, 0}, {1, 0, 2});
    assert(!result.blocked && near(result.resolvedDelta.x, 1) && near(result.resolvedDelta.z, 2));

    CollisionShape box{};
    box.halfExtents = {1, 1, 1};
    backend.configure(&box, 1, 0.35f);
    result = move({-2, 0, 0}, {4, 0, 0});
    assert(result.blocked && near(result.resolvedDelta.x, 0.65f));
    result = move({-1.35f, 0, 0}, {0.2f, 0, 0.2f});
    assert(result.blocked && near(result.resolvedDelta.x, 0) && near(result.resolvedDelta.z, 0.2f));
    result = move({-1.35f, 0, 0}, {-0.2f, 0, 0});
    assert(near(result.resolvedDelta.x, -0.2f));
    // Tiny penetration must escape by the near edge, never cross the obstacle.
    result = move({-1.349f, 0, 0}, {0.01f, 0, 0});
    assert(result.blocked && near(result.resolvedDelta.x, -0.001f));
    assert(std::fabs(result.resolvedDelta.z) < 0.01f);

    for (auto type : {CollisionShapeType::Sphere, CollisionShapeType::Capsule}) {
        CollisionShape round{};
        round.type = type;
        round.radius = 1;
        round.height = 3;
        backend.configure(&round, 1, 0.35f);
        result = move({-2, 0, 0}, {4, 0, 0});
        assert(result.blocked && near(result.resolvedDelta.x, 0.65f));
        result = move({-1.35f, 0, 0}, {0.2f, 0, 0.2f});
        assert(result.resolvedDelta.z > 0.1f);
        float x = -1.35f + result.resolvedDelta.x, z = result.resolvedDelta.z;
        assert(x * x + z * z >= 1.35f * 1.35f - 0.001f);
        result = move({-1.349f, 0, 0}, {0.01f, 0, 0});
        assert(near(result.resolvedDelta.x, -0.001f));
    }
    // A corner and two touching walls: repeated steps stay outside solid interiors.
    CollisionShape walls[2]{};
    walls[0].center = {2, 0, 0}; walls[0].halfExtents = {0.1f, 1, 4};
    walls[1].center = {0, 0, 2}; walls[1].halfExtents = {4, 1, 0.1f};
    backend.configure(walls, 2, 0.35f);
    Vec3 position{};
    for (int i = 0; i < 300; ++i) {
        result = move(position, {0.02f, 0, 0.02f});
        position.x += result.resolvedDelta.x; position.z += result.resolvedDelta.z;
        assert(position.x <= 1.56f && position.z <= 1.56f);
        assert(near(result.resolvedDelta.y, 0));
    }
    assert(!backend.configure(walls, MaxStaticCollisionShapes + 1, 0.35f));
    assert(!backend.configure(nullptr, 1, 0.35f));
    assert(!backend.configure(walls, 2, -1));
    assert(backend.configure(nullptr, 0, 0.35f));

    // Multiple faces of very small nearby boxes can fill the normal array in
    // a single iteration. Each append must be bounded, not just the outer loop.
    CollisionShape crowded[MaxStaticCollisionShapes]{};
    crowded[0].type = CollisionShapeType::Sphere;
    crowded[0].center = {-0.01f, 0, 0};
    crowded[0].radius = 0.01f;
    for (unsigned int i = 1; i < MaxStaticCollisionShapes; ++i)
        crowded[i].halfExtents = {0.001f, 1, 0.001f};
    assert(backend.configure(crowded, MaxStaticCollisionShapes, 0.001f));
    result = move({0, 0, 0}, {0.001f, 0, 0.001f});
    assert(std::isfinite(result.resolvedDelta.x) && std::isfinite(result.resolvedDelta.z));
    std::puts("Real collision wall, circle, capsule, penetration and corner tests passed.");
}
