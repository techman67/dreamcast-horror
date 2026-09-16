#include <cassert>
#include <cmath>
#include <cstdio>
#include "AuthoredBindings.h"

static bool nearlyEqual(float a, float b, float eps = 0.001f) {
    return std::fabs(a - b) < eps;
}

int main() {
    SliceBindings bindings{};

    bindings.playerActor.id = 42;
    bindings.gameplayCamera.id = 7;
    bindings.initialPlayerPose.position = {1.0f, 0.0f, 2.0f};
    bindings.initialPlayerPose.yaw = 1.57f;

    assert(bindings.playerActor.id == 42);
    assert(bindings.gameplayCamera.id == 7);

    assert(nearlyEqual(bindings.initialPlayerPose.position.x, 1.0f));
    assert(nearlyEqual(bindings.initialPlayerPose.position.y, 0.0f));
    assert(nearlyEqual(bindings.initialPlayerPose.position.z, 2.0f));
    assert(nearlyEqual(bindings.initialPlayerPose.yaw, 1.57f));

    std::printf("Bindings tests passed.\n");
    return 0;
}