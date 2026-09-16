#Requires -Version 5.1
<#
    Setup-CollisionSystem.ps1

    Creates / replaces every source file for the StaticCollisionBackend
    refactor, patches build_unity_plugin.bat, and optionally runs the build.
    File contents are byte-for-byte identical to the authored versions.
    Nothing is rewritten, reformatted, or "improved".
#>

[CmdletBinding()]
param(
    # %REPO% - folder that contains UnityBridge.cpp and the Game\ tree.
    [string]$RepoRoot = $PSScriptRoot,

    # Unity project root, relative to %REPO% by default.
    [string]$UnityRoot = (Join-Path $PSScriptRoot 'Unity\Dreamcast-Horror'),

    # Set to skip the final compile step.
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

$GameCoreDir         = Join-Path $RepoRoot  'Game\Core'
$GameGameplayDir     = Join-Path $RepoRoot  'Game\Gameplay'
$GameCompatDir       = Join-Path $RepoRoot  'Game\Compatibility'
$UnityBridgeCpp      = Join-Path $RepoRoot  'UnityBridge.cpp'

$UnityScriptsDir     = Join-Path $UnityRoot 'Assets\Scripts\Gameplay'
$BuildScript         = Join-Path $UnityRoot 'build_unity_plugin.bat'

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-TextFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Content
    )

    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    # Write UTF-8 without BOM so C++ compilers and Unity are both happy.
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)

    Write-Host ("  wrote  " + $Path) -ForegroundColor DarkGray
}

function Write-Section {
    param([string]$Text)
    Write-Host ''
    Write-Host $Text -ForegroundColor Cyan
}

# ---------------------------------------------------------------------------
# 1. Game/Core/AuthoredBindings.h
# ---------------------------------------------------------------------------

Write-Section '1/12  Game/Core/AuthoredBindings.h'

Write-TextFile -Path (Join-Path $GameCoreDir 'AuthoredBindings.h') -Content @'
#pragma once
#include "Types.h"

// Stable opaque identifier for an authored actor.
struct ActorRef {
    unsigned int id = 0;
};

// Stable opaque identifier for an authored camera.
struct CameraRef {
    unsigned int id = 0;
};

// Minimal authored pose.
struct Pose {
    Vec3 position;
    float yaw = 0.0f;
};

// Supported Dreamcast-friendly collision primitives.
enum class CollisionShapeType : unsigned int {
    Box = 0,
    Sphere = 1,
    Capsule = 2
};

// Static collision primitive.
//
// Box:
//     center + halfExtents are used.
//
// Sphere:
//     center + radius are used.
//
// Capsule:
//     center + radius + height are stored.
//     For the current flat X/Z movement solver, the horizontal
//     footprint is equivalent to a circle of radius "radius".
struct CollisionShape {
    CollisionShapeType type =
        CollisionShapeType::Box;

    Vec3 center;

    Vec3 halfExtents;

    float radius = 0.0f;

    float height = 0.0f;
};

// Fixed storage keeps the gameplay layer deterministic and
// avoids dynamic allocation.
constexpr unsigned int MaxStaticCollisionShapes = 128;

// Authored two-way fixed-camera transition.
struct CameraTransition {
    CameraRef cameraA;
    CameraRef cameraB;

    float boundaryZ = 0.0f;
    float halfWidthX = 0.0f;
};

// Initial game bindings.
//
// Collision shapes are intentionally NOT part of GameState.
// They are supplied to the displacement backend during initialization.
struct SliceBindings {
    ActorRef playerActor;

    CameraRef gameplayCamera;

    Pose initialPlayerPose;
    Pose initialCameraPose;

    CameraTransition cameraTransition;

    float playerCollisionRadius = 0.35f;
};
'@

# ---------------------------------------------------------------------------
# 2. Game/Gameplay/GameState.h
# ---------------------------------------------------------------------------

Write-Section '2/12  Game/Gameplay/GameState.h'

Write-TextFile -Path (Join-Path $GameGameplayDir 'GameState.h') -Content @'
#pragma once
#include "AuthoredBindings.h"
#include "Types.h"

struct GameState {
    ActorRef playerActor;

    Vec3 playerPos;
    Vec3 previousPlayerPos;

    CameraRef gameplayCamera;
    Pose cameraPose;

    CameraTransition cameraTransition;
};
'@

# ---------------------------------------------------------------------------
# 3. Game/Compatibility/IDisplacementBackend.h
# ---------------------------------------------------------------------------

Write-Section '3/12  Game/Compatibility/IDisplacementBackend.h'

Write-TextFile -Path (Join-Path $GameCompatDir 'IDisplacementBackend.h') -Content @'
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
};

class IDisplacementBackend {
public:
    virtual ~IDisplacementBackend() = default;

    virtual DisplacementResult resolveMove(
        const ActorRef& actor,
        const Vec3& position,
        const Vec3& desiredDelta) = 0;
};
'@

# ---------------------------------------------------------------------------
# 4. Game/Compatibility/StaticCollisionBackend.h
# ---------------------------------------------------------------------------

Write-Section '4/12  Game/Compatibility/StaticCollisionBackend.h'

Write-TextFile -Path (Join-Path $GameCompatDir 'StaticCollisionBackend.h') -Content @'
#pragma once

#include "IDisplacementBackend.h"

class StaticCollisionBackend : public IDisplacementBackend {
public:
    StaticCollisionBackend();

    void configure(
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
'@

# ---------------------------------------------------------------------------
# 5. Game/Compatibility/StaticCollisionBackend.cpp
# ---------------------------------------------------------------------------

Write-Section '5/12  Game/Compatibility/StaticCollisionBackend.cpp'

Write-TextFile -Path (Join-Path $GameCompatDir 'StaticCollisionBackend.cpp') -Content @'
#include "StaticCollisionBackend.h"

#include <cmath>

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

void StaticCollisionBackend::configure(
    const CollisionShape* shapes,
    unsigned int shapeCount,
    float playerRadius) {

    m_shapes = shapes;

    m_shapeCount =
        shapeCount;

    if (m_shapeCount >
        MaxStaticCollisionShapes) {

        m_shapeCount =
            MaxStaticCollisionShapes;
    }

    m_playerRadius =
        playerRadius;

    if (m_playerRadius < 0.0f) {
        m_playerRadius = 0.0f;
    }
}

DisplacementResult
StaticCollisionBackend::resolveMove(
    const ActorRef&,
    const Vec3& position,
    const Vec3& desiredDelta) {

    if (m_shapes == nullptr ||
        m_shapeCount == 0) {

        return {
            desiredDelta,
            false
        };
    }

    bool blocked = false;

    // Resolve X first.
    //
    // This intentionally produces wall sliding:
    // if X is blocked, the Z portion can still proceed.
    const float resolvedX =
        resolveX(
            position,
            position.x + desiredDelta.x,
            blocked);

    Vec3 afterX = position;

    afterX.x =
        resolvedX;

    // Resolve Z using the X position already resolved above.
    const float resolvedZ =
        resolveZ(
            afterX,
            position.z + desiredDelta.z,
            blocked);

    return {
        Vec3{
            resolvedX - position.x,
            desiredDelta.y,
            resolvedZ - position.z
        },
        blocked
    };
}

float StaticCollisionBackend::resolveX(
    const Vec3& position,
    float desiredX,
    bool& blocked) const {

    float resolvedX =
        desiredX;

    for (unsigned int i = 0;
         i < m_shapeCount;
         ++i) {

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

    float resolvedZ =
        desiredZ;

    for (unsigned int i = 0;
         i < m_shapeCount;
         ++i) {

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
    if (otherAxis <= boxMinOther ||
        otherAxis >= boxMaxOther) {

        return desiredAxis;
    }

    // Moving toward the positive side.
    if (desiredAxis > currentAxis) {

        // Already penetrating.
        if (currentAxis > boxMinAxis &&
            currentAxis < boxMaxAxis) {

            blocked = true;

            return boxMaxAxis;
        }

        // Swept crossing.
        if (currentAxis <= boxMinAxis &&
            desiredAxis > boxMinAxis) {

            blocked = true;

            return boxMinAxis;
        }
    }

    // Moving toward the negative side.
    if (desiredAxis < currentAxis) {

        // Already penetrating.
        if (currentAxis > boxMinAxis &&
            currentAxis < boxMaxAxis) {

            blocked = true;

            return boxMinAxis;
        }

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

    // Moving positive.
    if (desiredAxis > currentAxis) {

        // Already inside the circle.
        if (currentAxis > minAxis &&
            currentAxis < maxAxis) {

            blocked = true;

            return maxAxis;
        }

        // Sweep crossed the circle boundary.
        if (currentAxis <= minAxis &&
            desiredAxis > minAxis) {

            blocked = true;

            return minAxis;
        }
    }

    // Moving negative.
    if (desiredAxis < currentAxis) {

        // Already inside the circle.
        if (currentAxis > minAxis &&
            currentAxis < maxAxis) {

            blocked = true;

            return minAxis;
        }

        // Sweep crossed the circle boundary.
        if (currentAxis >= maxAxis &&
            desiredAxis < maxAxis) {

            blocked = true;

            return maxAxis;
        }
    }

    return desiredAxis;
}
'@

# ---------------------------------------------------------------------------
# 6. Game/Compatibility/Game.cpp
# ---------------------------------------------------------------------------

Write-Section '6/12  Game/Compatibility/Game.cpp'

Write-TextFile -Path (Join-Path $GameCompatDir 'Game.cpp') -Content @'
#include "Game.h"
#include "GameState.h"
#include "Player.h"

namespace {

    GameState g_state{};

    IDisplacementBackend* g_displacement =
        nullptr;

    bool isWithinTransitionWidth(float x) {

        return x >=
                   -g_state.cameraTransition.halfWidthX &&
               x <=
                    g_state.cameraTransition.halfWidthX;
    }

    void updateCameraTransition() {

        const float boundary =
            g_state.cameraTransition.boundaryZ;

        const float previousZ =
            g_state.previousPlayerPos.z;

        const float currentZ =
            g_state.playerPos.z;

        const bool crossedForward =
            previousZ < boundary &&
            currentZ >= boundary;

        const bool crossedBackward =
            previousZ > boundary &&
            currentZ <= boundary;

        if (!crossedForward &&
            !crossedBackward) {

            return;
        }

        const float dz =
            currentZ -
            previousZ;

        float crossingT =
            0.0f;

        if (dz != 0.0f) {

            crossingT =
                (boundary - previousZ) /
                dz;
        }

        crossingT =
            crossingT < 0.0f
                ? 0.0f
                : crossingT > 1.0f
                    ? 1.0f
                    : crossingT;

        const float crossingX =
            g_state.previousPlayerPos.x +
            (g_state.playerPos.x -
             g_state.previousPlayerPos.x) *
            crossingT;

        if (!isWithinTransitionWidth(
                crossingX)) {

            return;
        }

        if (crossedForward) {

            g_state.gameplayCamera =
                g_state.cameraTransition.cameraB;
        }
        else {

            g_state.gameplayCamera =
                g_state.cameraTransition.cameraA;
        }
    }
}

extern "C" void game_init(
    SliceBindings bindings,
    IDisplacementBackend* displacement) {

    g_displacement =
        displacement;

    g_state =
        GameState{};

    g_state.playerActor =
        bindings.playerActor;

    g_state.playerPos =
        bindings.initialPlayerPose.position;

    g_state.previousPlayerPos =
        g_state.playerPos;

    g_state.gameplayCamera =
        bindings.gameplayCamera;

    g_state.cameraPose =
        bindings.initialCameraPose;

    g_state.cameraTransition =
        bindings.cameraTransition;
}

extern "C" void game_step(
    const InputFrame* input,
    float deltaSeconds) {

    if (input == nullptr ||
        g_displacement == nullptr) {

        return;
    }

    g_state.previousPlayerPos =
        g_state.playerPos;

    playerUpdate(
        g_state,
        *g_displacement,
        *input,
        deltaSeconds);

    updateCameraTransition();
}

extern "C" Vec3 game_get_player_pos() {

    return g_state.playerPos;
}

extern "C" void game_set_camera(
    CameraRef camera,
    Pose pose) {

    g_state.gameplayCamera =
        camera;

    g_state.cameraPose =
        pose;
}

extern "C" Pose game_get_camera_pose() {

    return g_state.cameraPose;
}

extern "C" CameraRef game_get_camera_ref() {

    return g_state.gameplayCamera;
}
'@

# ---------------------------------------------------------------------------
# 7. Game/Gameplay/Player.cpp
# ---------------------------------------------------------------------------

Write-Section '7/12  Game/Gameplay/Player.cpp'

Write-TextFile -Path (Join-Path $GameGameplayDir 'Player.cpp') -Content @'
#include "Player.h"

namespace {

    constexpr float playerSpeed =
        1.0f;
}

void playerUpdate(
    GameState& state,
    IDisplacementBackend& displacement,
    const InputFrame& input,
    float deltaSeconds) {

    const Vec3 desired{
        input.move.x *
            playerSpeed *
            deltaSeconds,

        0.0f,

        input.move.y *
            playerSpeed *
            deltaSeconds
    };

    if (desired.x == 0.0f &&
        desired.z == 0.0f) {

        return;
    }

    const DisplacementResult result =
        displacement.resolveMove(
            state.playerActor,
            state.playerPos,
            desired);

    // Always apply the resolved portion,
    // including when blocked == true.
    state.playerPos.x +=
        result.resolvedDelta.x;

    state.playerPos.y +=
        result.resolvedDelta.y;

    state.playerPos.z +=
        result.resolvedDelta.z;
}
'@

# ---------------------------------------------------------------------------
# 8. UnityBridge.cpp
# ---------------------------------------------------------------------------

Write-Section '8/12  UnityBridge.cpp'

Write-TextFile -Path $UnityBridgeCpp -Content @'
#include <cmath>

#include "Game.h"
#include "StaticCollisionBackend.h"

namespace {

    CollisionShape g_collisionShapes[
        MaxStaticCollisionShapes
    ]{};

    unsigned int g_collisionShapeCount =
        0;

    StaticCollisionBackend g_collisionBackend{};

    constexpr float playerCollisionRadius =
        0.35f;
}

extern "C" __declspec(dllexport)
void unity_game_collision_clear() {

    g_collisionShapeCount =
        0;
}

extern "C" __declspec(dllexport)
void unity_game_collision_add_box(
    float centerX,
    float centerY,
    float centerZ,
    float halfX,
    float halfY,
    float halfZ) {

    if (g_collisionShapeCount >=
        MaxStaticCollisionShapes) {

        return;
    }

    CollisionShape& shape =
        g_collisionShapes[
            g_collisionShapeCount++
        ];

    shape =
        CollisionShape{};

    shape.type =
        CollisionShapeType::Box;

    shape.center =
        Vec3{
            centerX,
            centerY,
            centerZ
        };

    shape.halfExtents =
        Vec3{
            std::fabs(halfX),
            std::fabs(halfY),
            std::fabs(halfZ)
        };
}

extern "C" __declspec(dllexport)
void unity_game_collision_add_sphere(
    float centerX,
    float centerY,
    float centerZ,
    float radius) {

    if (g_collisionShapeCount >=
        MaxStaticCollisionShapes) {

        return;
    }

    CollisionShape& shape =
        g_collisionShapes[
            g_collisionShapeCount++
        ];

    shape =
        CollisionShape{};

    shape.type =
        CollisionShapeType::Sphere;

    shape.center =
        Vec3{
            centerX,
            centerY,
            centerZ
        };

    shape.radius =
        std::fabs(radius);
}

extern "C" __declspec(dllexport)
void unity_game_collision_add_capsule(
    float centerX,
    float centerY,
    float centerZ,
    float radius,
    float height) {

    if (g_collisionShapeCount >=
        MaxStaticCollisionShapes) {

        return;
    }

    CollisionShape& shape =
        g_collisionShapes[
            g_collisionShapeCount++
        ];

    shape =
        CollisionShape{};

    shape.type =
        CollisionShapeType::Capsule;

    shape.center =
        Vec3{
            centerX,
            centerY,
            centerZ
        };

    shape.radius =
        std::fabs(radius);

    shape.height =
        std::fabs(height);
}

extern "C" __declspec(dllexport)
void unity_game_init() {

    g_collisionBackend.configure(
        g_collisionShapes,
        g_collisionShapeCount,
        playerCollisionRadius);

    SliceBindings bindings{};

    bindings.playerActor =
        ActorRef{1};

    bindings.gameplayCamera =
        CameraRef{1};

    bindings.initialPlayerPose.position =
        Vec3{
            0.0f,
            0.0f,
            0.0f
        };

    bindings.initialPlayerPose.yaw =
        0.0f;

    bindings.initialCameraPose.position =
        Vec3{
            0.0f,
            2.4f,
            -3.8f
        };

    bindings.initialCameraPose.yaw =
        0.0f;

    bindings.cameraTransition.cameraA =
        CameraRef{1};

    bindings.cameraTransition.cameraB =
        CameraRef{2};

    bindings.cameraTransition.boundaryZ =
        0.8f;

    bindings.cameraTransition.halfWidthX =
        4.0f;

    bindings.playerCollisionRadius =
        playerCollisionRadius;

    game_init(
        bindings,
        &g_collisionBackend);
}

extern "C" __declspec(dllexport)
void unity_game_step(
    float moveX,
    float moveY,
    float deltaSeconds) {

    InputFrame input{};

    input.move.x =
        moveX;

    input.move.y =
        moveY;

    input.interactPressed =
        false;

    game_step(
        &input,
        deltaSeconds);
}

extern "C" __declspec(dllexport)
float unity_game_get_player_x() {

    return game_get_player_pos().x;
}

extern "C" __declspec(dllexport)
float unity_game_get_player_y() {

    return game_get_player_pos().y;
}

extern "C" __declspec(dllexport)
float unity_game_get_player_z() {

    return game_get_player_pos().z;
}

extern "C" __declspec(dllexport)
unsigned int unity_game_get_camera_id() {

    return game_get_camera_ref().id;
}
'@

# ---------------------------------------------------------------------------
# 9. CppCollision.cs
# ---------------------------------------------------------------------------

Write-Section '9/12  CppCollision.cs'

Write-TextFile -Path (Join-Path $UnityScriptsDir 'CppCollision.cs') -Content @'
using UnityEngine;

[ExecuteAlways]
public sealed class CppCollision : MonoBehaviour
{
    public enum CollisionMode
    {
        Auto,
        Box,
        Sphere,
        Capsule,
        Manual
    }

    public enum ManualShape
    {
        Box,
        Sphere,
        Capsule
    }

    [Header("Collision")]

    [SerializeField]
    private CollisionMode mode =
        CollisionMode.Auto;

    [SerializeField]
    private ManualShape manualShape =
        ManualShape.Box;

    [Header("Manual Box")]

    [SerializeField]
    private Vector3 manualCenter =
        Vector3.zero;

    [SerializeField]
    private Vector3 manualSize =
        Vector3.one;

    [Header("Manual Sphere")]

    [SerializeField]
    private float manualRadius =
        0.5f;

    [Header("Manual Capsule")]

    [SerializeField]
    private float manualHeight =
        1.0f;

    [Header("Debug")]

    [SerializeField]
    private bool showGizmo =
        true;

    public CollisionMode Mode =>
        mode;

    public CollisionMode ResolvedMode {
        get;
        private set;
    }

    public Vector3 WorldCenter {
        get;
        private set;
    }

    public Vector3 WorldSize {
        get;
        private set;
    }

    public float Radius {
        get;
        private set;
    }

    public float Height {
        get;
        private set;
    }

    private void Awake()
    {
        Recalculate();
    }

    private void OnEnable()
    {
        Recalculate();
    }

    private void OnValidate()
    {
        Recalculate();
    }

    public void Recalculate()
    {
        Bounds bounds;

        if (!TryGetRendererBounds(
                out bounds)) {

            bounds =
                new Bounds(
                    transform.position,
                    Vector3.one);
        }

        if (mode ==
            CollisionMode.Manual) {

            ApplyManual();

            return;
        }

        WorldCenter =
            bounds.center;

        WorldSize =
            bounds.size;

        ResolvedMode =
            ResolveAutoMode(
                bounds);

        switch (ResolvedMode)
        {
            case CollisionMode.Box:

                Radius = 0.0f;

                Height =
                    bounds.size.y;

                break;

            case CollisionMode.Sphere:

                // Bounding-sphere approximation.
                //
                // Half the diagonal guarantees that the sphere
                // contains the measured bounds rather than
                // underestimating the object's footprint.
                Radius =
                    bounds.extents.magnitude;

                Height =
                    bounds.size.y;

                break;

            case CollisionMode.Capsule:

                Radius =
                    Mathf.Max(
                        bounds.extents.x,
                        bounds.extents.z);

                Height =
                    Mathf.Max(
                        bounds.size.y,
                        Radius * 2.0f);

                break;
        }
    }

    private CollisionMode ResolveAutoMode(
        Bounds bounds)
    {
        float width =
            Mathf.Max(
                bounds.size.x,
                0.0001f);

        float depth =
            Mathf.Max(
                bounds.size.z,
                0.0001f);

        float height =
            Mathf.Max(
                bounds.size.y,
                0.0001f);

        float horizontalMax =
            Mathf.Max(
                width,
                depth);

        float horizontalMin =
            Mathf.Min(
                width,
                depth);

        float horizontalRatio =
            horizontalMin /
            horizontalMax;

        // Tall and narrow objects:
        // people, dolls, poles, trees, etc.
        if (height >
            horizontalMax * 1.6f) {

            return CollisionMode.Capsule;
        }

        // Compact objects whose X/Z dimensions are
        // reasonably similar are good sphere candidates.
        //
        // We also require that height is reasonably close
        // to the horizontal size so a long box doesn't
        // accidentally become a sphere.
        if (horizontalRatio >= 0.80f &&
            height <=
                horizontalMax * 1.35f) {

            return CollisionMode.Sphere;
        }

        return CollisionMode.Box;
    }

    private void ApplyManual()
    {
        ResolvedMode =
            mode;

        switch (manualShape)
        {
            case ManualShape.Box:

                WorldCenter =
                    transform.TransformPoint(
                        manualCenter);

                WorldSize =
                    Vector3.Scale(
                        manualSize,
                        AbsVector(
                            transform.lossyScale));

                Radius = 0.0f;

                Height =
                    WorldSize.y;

                ResolvedMode =
                    CollisionMode.Box;

                break;

            case ManualShape.Sphere:

                WorldCenter =
                    transform.TransformPoint(
                        manualCenter);

                Radius =
                    Mathf.Abs(
                        manualRadius) *
                    MaxAbsScale(
                        transform.lossyScale);

                WorldSize =
                    Vector3.one *
                    Radius *
                    2.0f;

                Height =
                    Radius * 2.0f;

                ResolvedMode =
                    CollisionMode.Sphere;

                break;

            case ManualShape.Capsule:

                WorldCenter =
                    transform.TransformPoint(
                        manualCenter);

                Radius =
                    Mathf.Abs(
                        manualRadius) *
                    Mathf.Max(
                        Mathf.Abs(
                            transform.lossyScale.x),
                        Mathf.Abs(
                            transform.lossyScale.z));

                Height =
                    Mathf.Max(
                        Mathf.Abs(
                            manualHeight *
                            transform.lossyScale.y),
                        Radius * 2.0f);

                WorldSize =
                    new Vector3(
                        Radius * 2.0f,
                        Height,
                        Radius * 2.0f);

                ResolvedMode =
                    CollisionMode.Capsule;

                break;
        }
    }

    private static Vector3 AbsVector(
        Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }

    private static float MaxAbsScale(
        Vector3 value)
    {
        return Mathf.Max(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }

    private bool TryGetRendererBounds(
        out Bounds combined)
    {
        Renderer[] renderers =
            GetComponentsInChildren<
                Renderer>();

        bool found =
            false;

        combined =
            new Bounds();

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer == null ||
                !renderer.enabled) {

                continue;
            }

            if (!found)
            {
                combined =
                    renderer.bounds;

                found = true;
            }
            else
            {
                combined.Encapsulate(
                    renderer.bounds);
            }
        }

        return found;
    }

    public void SendToNative()
    {
        Recalculate();

        switch (ResolvedMode)
        {
            case CollisionMode.Box:

                NativeCollision.AddBox(
                    WorldCenter,
                    WorldSize * 0.5f);

                break;

            case CollisionMode.Sphere:

                NativeCollision.AddSphere(
                    WorldCenter,
                    Radius);

                break;

            case CollisionMode.Capsule:

                NativeCollision.AddCapsule(
                    WorldCenter,
                    Radius,
                    Height);

                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo)
            return;

        Recalculate();

        switch (ResolvedMode)
        {
            case CollisionMode.Box:

                Gizmos.DrawWireCube(
                    WorldCenter,
                    WorldSize);

                break;

            case CollisionMode.Sphere:

                Gizmos.DrawWireSphere(
                    WorldCenter,
                    Radius);

                break;

            case CollisionMode.Capsule:

                DrawCapsuleGizmo();

                break;
        }
    }

    private void DrawCapsuleGizmo()
    {
        float cylinderHeight =
            Mathf.Max(
                0.0f,
                Height -
                Radius * 2.0f);

        Vector3 top =
            WorldCenter +
            Vector3.up *
            (cylinderHeight * 0.5f);

        Vector3 bottom =
            WorldCenter -
            Vector3.up *
            (cylinderHeight * 0.5f);

        Gizmos.DrawWireSphere(
            top,
            Radius);

        Gizmos.DrawWireSphere(
            bottom,
            Radius);

        Vector3 right =
            Vector3.right *
            Radius;

        Vector3 forward =
            Vector3.forward *
            Radius;

        Gizmos.DrawLine(
            top + right,
            bottom + right);

        Gizmos.DrawLine(
            top - right,
            bottom - right);

        Gizmos.DrawLine(
            top + forward,
            bottom + forward);

        Gizmos.DrawLine(
            top - forward,
            bottom - forward);
    }
}
'@

# ---------------------------------------------------------------------------
# 10. NativeCollision.cs
# ---------------------------------------------------------------------------

Write-Section '10/12  NativeCollision.cs'

Write-TextFile -Path (Join-Path $UnityScriptsDir 'NativeCollision.cs') -Content @'
using System.Runtime.InteropServices;
using UnityEngine;

public static class NativeCollision
{
    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_collision_clear();

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_collision_add_box(
            float centerX,
            float centerY,
            float centerZ,
            float halfX,
            float halfY,
            float halfZ);

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_collision_add_sphere(
            float centerX,
            float centerY,
            float centerZ,
            float radius);

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_collision_add_capsule(
            float centerX,
            float centerY,
            float centerZ,
            float radius,
            float height);

    public static void Clear()
    {
        unity_game_collision_clear();
    }

    public static void AddBox(
        Vector3 center,
        Vector3 halfExtents)
    {
        unity_game_collision_add_box(
            center.x,
            center.y,
            center.z,
            halfExtents.x,
            halfExtents.y,
            halfExtents.z);
    }

    public static void AddSphere(
        Vector3 center,
        float radius)
    {
        unity_game_collision_add_sphere(
            center.x,
            center.y,
            center.z,
            radius);
    }

    public static void AddCapsule(
        Vector3 center,
        float radius,
        float height)
    {
        unity_game_collision_add_capsule(
            center.x,
            center.y,
            center.z,
            radius,
            height);
    }
}
'@

# ---------------------------------------------------------------------------
# 11. NativeGameBridge.cs
# ---------------------------------------------------------------------------

Write-Section '11/12  NativeGameBridge.cs'

Write-TextFile -Path (Join-Path $UnityScriptsDir 'NativeGameBridge.cs') -Content @'
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class NativeGameBridge :
    MonoBehaviour
{
    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_init();

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern void
        unity_game_step(
            float moveX,
            float moveY,
            float deltaSeconds);

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern float
        unity_game_get_player_x();

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern float
        unity_game_get_player_y();

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern float
        unity_game_get_player_z();

    [DllImport(
        "dreamcast_horror",
        CallingConvention =
            CallingConvention.Cdecl)]
    private static extern uint
        unity_game_get_camera_id();

    [Header("Authored Gameplay Cameras")]

    [SerializeField]
    private Camera gameplayCamera01;

    [SerializeField]
    private Camera gameplayCamera02;

    private uint lastCameraId = 0;

    private void Start()
    {
        ResolveAuthoredCameras();

        BuildNativeCollision();

        unity_game_init();

        Debug.Log(
            $"C++ game initialized. " +
            $"Player position: " +
            $"({unity_game_get_player_x():F3}, " +
            $"{unity_game_get_player_y():F3}, " +
            $"{unity_game_get_player_z():F3})");

        ApplyActiveCamera(true);
    }

    private void Update()
    {
        float moveX = 0.0f;
        float moveY = 0.0f;

        Keyboard keyboard =
            Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed)
                moveX -= 1.0f;

            if (keyboard.dKey.isPressed)
                moveX += 1.0f;

            if (keyboard.sKey.isPressed)
                moveY -= 1.0f;

            if (keyboard.wKey.isPressed)
                moveY += 1.0f;
        }

        unity_game_step(
            moveX,
            moveY,
            Time.deltaTime);

        ApplyActiveCamera(false);
    }

    private void BuildNativeCollision()
    {
        NativeCollision.Clear();

        CppCollision[] collisions =
            FindObjectsByType<CppCollision>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (CppCollision collision
                 in collisions)
        {
            collision.SendToNative();
        }

        Debug.Log(
            $"Native collision built from " +
            $"{collisions.Length} " +
            $"CppCollision components.");
    }

    private void ResolveAuthoredCameras()
    {
        if (gameplayCamera01 == null)
        {
            GameObject cameraObject =
                GameObject.Find(
                    "GameplayCamera_01");

            if (cameraObject != null)
            {
                gameplayCamera01 =
                    cameraObject.GetComponent<
                        Camera>();
            }
        }

        if (gameplayCamera02 == null)
        {
            GameObject cameraObject =
                GameObject.Find(
                    "GameplayCamera_02");

            if (cameraObject != null)
            {
                gameplayCamera02 =
                    cameraObject.GetComponent<
                        Camera>();
            }
        }

        if (gameplayCamera01 == null)
        {
            Debug.LogError(
                "GameplayCamera_01 was not found.");
        }

        if (gameplayCamera02 == null)
        {
            Debug.LogError(
                "GameplayCamera_02 was not found.");
        }
    }

    private void ApplyActiveCamera(
        bool force)
    {
        uint cameraId =
            unity_game_get_camera_id();

        if (!force &&
            cameraId == lastCameraId)
            return;

        lastCameraId =
            cameraId;

        switch (cameraId)
        {
            case 1:

                SetActiveCamera(
                    gameplayCamera01);

                Debug.Log(
                    "C++ selected " +
                    "GameplayCamera_01");

                break;

            case 2:

                SetActiveCamera(
                    gameplayCamera02);

                Debug.Log(
                    "C++ selected " +
                    "GameplayCamera_02");

                break;

            default:

                Debug.LogError(
                    $"C++ selected unknown " +
                    $"camera ID: {cameraId}");

                break;
        }
    }

    private void SetActiveCamera(
        Camera activeCamera)
    {
        if (activeCamera == null)
        {
            Debug.LogError(
                "Requested gameplay camera " +
                "is missing.");

            return;
        }

        if (gameplayCamera01 != null)
        {
            gameplayCamera01.enabled =
                activeCamera ==
                gameplayCamera01;
        }

        if (gameplayCamera02 != null)
        {
            gameplayCamera02.enabled =
                activeCamera ==
                gameplayCamera02;
        }
    }
}
'@

# ---------------------------------------------------------------------------
# 12. Patch build_unity_plugin.bat
# ---------------------------------------------------------------------------

Write-Section '12/12  Patch build_unity_plugin.bat'

if (-not (Test-Path -LiteralPath $BuildScript)) {
    Write-Warning "Build script not found at $BuildScript - skipping patch."
}
else {
    $raw = [System.IO.File]::ReadAllText($BuildScript)

    if ($raw -match 'StaticCollisionBackend\.cpp') {
        Write-Host '  already patched (StaticCollisionBackend.cpp present)' -ForegroundColor DarkGray
    }
    else {
        # Insert "%REPO%Game\Compatibility\StaticCollisionBackend.cpp" ^
        # right after the "%REPO%Game\Compatibility\Game.cpp" ^ line.
        $pattern = '("%REPO%Game\\Compatibility\\Game\.cpp"[ \t]*\^[ \t]*\r?\n)'
        $replacement = '$1"%REPO%Game\Compatibility\StaticCollisionBackend.cpp" ^' + "`r`n"

        $patched = [regex]::Replace($raw, $pattern, $replacement, 1)

        if ($patched -eq $raw) {
            Write-Warning 'Could not find the Game.cpp build line; patch not applied.'
        }
        else {
            # Preserve original newline style.
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllText($BuildScript, $patched, $utf8NoBom)
            Write-Host ("  patched " + $BuildScript) -ForegroundColor DarkGray
        }
    }
}

# ---------------------------------------------------------------------------
# Optional build
# ---------------------------------------------------------------------------

if (-not $SkipBuild) {
    Write-Section 'Building...'

    if (-not (Test-Path -LiteralPath $BuildScript)) {
        Write-Warning "Build script missing; skipping build."
    }
    else {
        Push-Location (Split-Path -Parent $BuildScript)
        try {
            & cmd.exe /c "`"$BuildScript`""
            if ($LASTEXITCODE -ne 0) {
                throw "Build failed with exit code $LASTEXITCODE"
            }
        }
        finally {
            Pop-Location
        }
    }
}

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green