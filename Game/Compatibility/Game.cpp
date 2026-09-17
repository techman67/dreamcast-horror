#include "Game.h"
#include "GameState.h"
#include "Player.h"
#include "KeyDoor.h"
#include <cmath>

namespace {

    GameState g_state{};
    SliceBindings g_bindings{};
    SavePoints g_savePoints{};
    int g_saveRequest=-1;

    IDisplacementBackend* g_displacement =
        nullptr;

    bool isWithinTransitionWidth(float x) {

        return x >=
                   -g_state.cameraTransition.halfWidthX &&
               x <=
                    g_state.cameraTransition.halfWidthX;
    }

    void updateCameraTransition() {

        // Zero IDs mean no transition was authored (e.g. single-camera tests).
        if (g_state.cameraTransition.cameraA.id == 0 ||
            g_state.cameraTransition.cameraB.id == 0) {
            return;
        }

        const float boundary =
            g_state.cameraTransition.boundaryZ;

        const float previousZ =
            g_state.previousPlayerPos.z;

        const float currentZ =
            g_state.playerPos.z;

        const bool crossedForward =
            previousZ <= boundary &&
            currentZ >= boundary && currentZ > previousZ;

        const bool crossedBackward =
            previousZ >= boundary &&
            currentZ <= boundary && currentZ < previousZ;

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
            g_state.cameraPose = g_state.cameraTransition.poseB;
        }
        else {

            g_state.gameplayCamera =
                g_state.cameraTransition.cameraA;
            g_state.cameraPose = g_state.cameraTransition.poseA;
        }
    }
}

extern "C" void game_init(
    SliceBindings bindings,
    IDisplacementBackend* displacement) {

    g_displacement = displacement;
    g_bindings=bindings; g_savePoints.count=0; g_savePoints.room=0; g_saveRequest=-1;

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
    g_state.keyDoor = bindings.keyDoor;
    g_state.playerHeight = bindings.playerHeight;
    if (g_displacement != nullptr) initializeKeyDoor(g_state, *g_displacement);
}

extern "C" void game_step(
    const InputFrame* input,
    float deltaSeconds) {

    g_saveRequest=-1;
    g_state.audio = {}; // Invalid steps must not replay a previous event batch.

    if (input == nullptr ||
        g_displacement == nullptr || !std::isfinite(deltaSeconds) || deltaSeconds < 0 ||
        !std::isfinite(input->move.x) || !std::isfinite(input->move.y)) {

        return;
    }

    if (g_state.playerHeight > 0) deltaSeconds = std::fmin(deltaSeconds, 0.1f);
    g_state.previousPlayerPos =
        g_state.playerPos;

    if (!g_state.completed) playerUpdate(
        g_state,
        *g_displacement,
        *input,
        deltaSeconds);

    updateCameraTransition();
    const float dx = g_state.playerPos.x - g_state.previousPlayerPos.x;
    const float dz = g_state.playerPos.z - g_state.previousPlayerPos.z;
    const float distance = std::sqrt(dx * dx + dz * dz);
    if (deltaSeconds > 0) {
        constexpr float stride = 0.65f;
        constexpr float minimumInterval = 0.25f;
        g_state.footstepCooldown = std::fmax(0.0f, g_state.footstepCooldown - deltaSeconds);
        const bool walking = distance > 0.0001f && std::isfinite(distance) &&
            (g_state.playerHeight == 0 || g_state.grounded);
        if (walking) {
            // Only the first movement after reset gets an immediate step.
            // Preserve travel across stops so tapping cannot restart the stride.
            if (g_state.firstFootstep) g_state.footstepDistance = stride;
            else g_state.footstepDistance += distance;
            if (g_state.footstepDistance >= stride && g_state.footstepCooldown <= 0) {
                g_state.emit(AudioCue::Footstep);
                g_state.firstFootstep = false;
                g_state.footstepCooldown = minimumInterval;
                // Never emit a burst after a long frame or displacement jump.
                g_state.footstepDistance = std::fmod(g_state.footstepDistance, stride);
            }
        }
    }
    InputFrame idle; updateKeyDoor(g_state,*g_displacement,idle,deltaSeconds);
    InputFrame interaction=*input;
    const int savePoint=game_near_save_point();
    if(deltaSeconds>0 && savePoint>=0 && interaction.interactPressed) { g_saveRequest=savePoint; interaction.interactPressed=false; }
    updateKeyDoor(g_state, *g_displacement, interaction, 0);
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

extern "C" KeyDoorView game_get_key_door_view() {
    return keyDoorView(g_state);
}

extern "C" AudioEvents game_take_audio_events() {
    const AudioEvents result = g_state.audio;
    g_state.audio = {};
    return result;
}

extern "C" void game_set_save_points(const SavePoints* points) { if(points) g_savePoints=*points; else { g_savePoints.count=0; g_savePoints.room=0; } g_saveRequest=-1; }
extern "C" int game_near_save_point() {
    if(!g_displacement || g_state.completed || (g_state.playerHeight>0 && !g_state.grounded)) return -1;
    // Existing key/door interactions retain priority when ranges overlap.
    if(g_state.prompt!=InteractionPrompt::None) return -1;
    int nearest=-1; float best=1000000;
    for(unsigned i=0;i<g_savePoints.count && i<MaxSavePoints;++i) {
        const auto& p=g_savePoints.points[i]; const auto pos=g_state.playerPos;
        const float dx=pos.x-p.position.x,dy=pos.y+(g_state.playerHeight>0 ? g_state.playerHeight*.5f : .9f)-p.position.y,dz=pos.z-p.position.z;
        const float d=dx*dx+dy*dy+dz*dz;
        if(d<=p.range*p.range && d<best) { best=d; nearest=static_cast<int>(i); }
    }
    return nearest;
}
extern "C" int game_take_save_request() { int result=g_saveRequest; g_saveRequest=-1; return result; }
extern "C" bool game_capture_progress(SaveProgress* p) {
    if(!p || !g_displacement || g_state.completed || (g_state.playerHeight>0 && !g_state.grounded)) return false;
    *p={g_state.playerPos,g_state.gameplayCamera.id,(g_state.hasKey ? 1u : 0u)|(g_state.doorOpen ? 2u : 0u)}; return true;
}
extern "C" bool game_restore_progress(const SaveProgress* p) {
    if(!p || !g_displacement || (p->flags&~3u) || ((p->flags&2u) && !(p->flags&1u)) ||
       !std::isfinite(p->position.x)||!std::isfinite(p->position.y)||!std::isfinite(p->position.z)||
       std::fabs(p->position.x)>10000||std::fabs(p->position.y)>10000||std::fabs(p->position.z)>10000) return false;
    Pose camera;
    if(p->camera==g_bindings.gameplayCamera.id) camera=g_bindings.initialCameraPose;
    else if(p->camera && p->camera==g_bindings.cameraTransition.cameraA.id) camera=g_bindings.cameraTransition.poseA;
    else if(p->camera && p->camera==g_bindings.cameraTransition.cameraB.id) camera=g_bindings.cameraTransition.poseB;
    else return false;
    if((p->flags!=0) && g_bindings.keyDoor.keyActor.id==0) return false;
    auto points=g_savePoints; auto backend=g_displacement; game_init(g_bindings,backend); game_set_save_points(&points);
    g_state.playerPos=g_state.previousPlayerPos=p->position;
    g_state.hasKey=(p->flags&1u)!=0; g_state.doorOpen=(p->flags&2u)!=0;
    if(g_state.keyDoor.doorActor.id) backend->setDoorObstruction(g_state.keyDoor.doorActor,!g_state.doorOpen);
    g_state.gameplayCamera.id=p->camera; g_state.cameraPose=camera;
    InputFrame idle; updateKeyDoor(g_state,*backend,idle,0); return true;
}

extern "C" void game_set_linked_room(bool linked) { g_state.linkedRoom=linked; }
