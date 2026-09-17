#include "RoomScene.h"
#include "Game.h"
#include <cmath>
#include <cstdio>
#include <stdexcept>

namespace {
void inputChecks(smlt::Window* window) {
    auto require = [](bool value) {
        if (!value) throw std::runtime_error("Simulant device mapping check failed.");
    };
    smlt::InputState state(window);
    RoomInput adapter;
    require(!adapter.read(state).interactPressed);
    state._update_keyboard_devices({{0, smlt::KEYBOARD_TYPE_PHYSICAL}, {1, smlt::KEYBOARD_TYPE_PHYSICAL}});
    state._handle_key_down(smlt::KeyboardID(1), smlt::KEYBOARD_CODE_E);
    require(adapter.read(state).interactPressed);
    require(!adapter.read(state).interactPressed);
    state._handle_key_up(smlt::KeyboardID(1), smlt::KEYBOARD_CODE_E);
    require(!adapter.read(state).interactPressed);
    state._handle_key_down(smlt::KeyboardID(0), smlt::KEYBOARD_CODE_UP);
    require(adapter.read(state).move.y == 1);
    state._handle_key_up(smlt::KeyboardID(0), smlt::KEYBOARD_CODE_UP);
    state._handle_key_down(smlt::KeyboardID(0), smlt::KEYBOARD_CODE_SPACE);
    require(adapter.read(state).jumpPressed);
    require(!adapter.read(state).jumpPressed);
    state._handle_key_up(smlt::KeyboardID(0), smlt::KEYBOARD_CODE_SPACE);
    require(!adapter.read(state).jumpPressed);
    smlt::GameControllerInfo info{};
    info.id = smlt::GameControllerID(7); // ID is deliberately different from index.
    info.button_count = smlt::JOYSTICK_BUTTON_MAX;
    info.axis_count = 2;
    info.hat_count = 1;
    state._update_game_controllers({info});
    state._handle_joystick_axis_motion(info.id, smlt::JOYSTICK_AXIS_XL, 0.5f);
    require(adapter.read(state).move.x == 0.5f);
    state._handle_joystick_axis_motion(info.id, smlt::JOYSTICK_AXIS_XL, 0.1f);
    require(adapter.read(state).move.x == 0);
    state._handle_joystick_button_down(info.id, smlt::JOYSTICK_BUTTON_DPAD_UP);
    require(adapter.read(state).move.y == 1);
    state._handle_joystick_button_up(info.id, smlt::JOYSTICK_BUTTON_DPAD_UP);
    state._handle_joystick_hat_motion(info.id, 0, smlt::HAT_POSITION_LEFT);
    require(adapter.read(state).move.x == -1);
    state._handle_joystick_hat_motion(info.id, 0, smlt::HAT_POSITION_CENTERED);
    state._handle_joystick_button_down(info.id, smlt::JOYSTICK_BUTTON_A);
    require(adapter.read(state).interactPressed);
    require(!adapter.read(state).interactPressed);
    state._handle_joystick_button_up(info.id, smlt::JOYSTICK_BUTTON_A);
    require(!adapter.read(state).interactPressed);
    state._handle_joystick_button_down(info.id, smlt::JOYSTICK_BUTTON_B);
    require(adapter.read(state).jumpPressed);
    require(!adapter.read(state).jumpPressed);
    state._handle_joystick_button_up(info.id, smlt::JOYSTICK_BUTTON_B);
    require(!adapter.read(state).jumpPressed);
    state._handle_joystick_button_down(info.id, smlt::JOYSTICK_BUTTON_START);
    adapter.read(state); require(adapter.restartPressed);
    adapter.read(state); require(!adapter.restartPressed);
    state._update_game_controllers({});
    require(adapter.read(state).move.x == 0);
    std::puts("SIMULANT INPUT CHECKS PASSED: keyboard edges, analog, D-pad, hat, controller edges and reset.");
}
}

// Integration playback feeds the same InputState read by the normal adapter.
// Targets describe the current sample test route; they are not gameplay logic.
void RoomScene::checkStep() {
    auto require = [](bool value, const char* message) {
        if (!value) throw std::runtime_error(message);
    };
    require(++totalFrames_ < 4000, "Room playthrough timed out.");
    if (totalFrames_ == 1) {
        require(room_.bindings.playerHeight > 0 || (key_ && door_), "Playback check requires the objective or stairs test room.");
        inputChecks(window);
    }
    auto& state = *input->state;
    const auto keyboard = smlt::KeyboardID(0);
    for (auto code : {smlt::KEYBOARD_CODE_W, smlt::KEYBOARD_CODE_A, smlt::KEYBOARD_CODE_S,
                      smlt::KEYBOARD_CODE_D, smlt::KEYBOARD_CODE_E, smlt::KEYBOARD_CODE_R, smlt::KEYBOARD_CODE_SPACE})
        state._handle_key_up(keyboard, code);
    auto press = [&](smlt::KeyboardCode code) { state._handle_key_down(keyboard, code); };
    auto next = [&]() {
        ++checkStage_; checkFrames_ = 0;
        std::printf("Room check stage %u\n", checkStage_);
        std::fflush(stdout);
    };
    auto walk = [&](float target, bool xAxis) {
        const Vec3 p = game_get_player_pos();
        const float delta = target - (xAxis ? p.x : p.z);
        if (std::fabs(delta) < 0.025f) next();
        else press(xAxis ? (delta > 0 ? smlt::KEYBOARD_CODE_D : smlt::KEYBOARD_CODE_A)
                         : (delta > 0 ? smlt::KEYBOARD_CODE_W : smlt::KEYBOARD_CODE_S));
    };
    if (room_.bindings.playerHeight > 0) {
        const Vec3 p=game_get_player_pos();
        switch(checkStage_) {
        case 0: if(++checkFrames_>30) next(); break;
        case 1: walk(4,false); break;
        case 2: require(std::fabs(p.y-1.5f)<.002f,"Stair ascent failed in Simulant."); next(); break;
        case 3: walk(-1,false); break;
        case 4: require(std::fabs(p.y)<.002f,"Stair descent failed in Simulant."); next(); break;
        case 5: walk(4,false); break;
        case 6: walk(-2,true); break;
        case 7: if(++checkFrames_>60) next(); break;
        case 8: require(std::fabs(p.y)<.002f,"Landing fall failed in Simulant.");
            press(smlt::KEYBOARD_CODE_R); next(); break;
        case 9:
            require(std::fabs(p.y)<.002f && std::fabs(p.z+1)<.002f && std::fabs(p.x)<.002f,"Physics reset failed.");
            require(audio_.played[0]>0,"Stairs did not emit footsteps.");
            std::puts("SIMULANT STAIRS CHECKS PASSED: ascent, descent, landing fall, footsteps and reset.");
            next(); break;
        case 10: walk(3.5f,false); break;
        case 11: walk(.85f,true); break;
        case 12: press(smlt::KEYBOARD_CODE_D); press(smlt::KEYBOARD_CODE_SPACE); next(); break;
        case 13: walk(1.85f,true); break;
        case 14:
            require(std::fabs(p.y-1.8f)<.01f,"Gap jump did not land on raised platform.");
            std::puts("SIMULANT GAP JUMP CHECKS PASSED");
            std::fflush(stdout); app->stop_running(); break;
        }
        return;
    }
    const KeyDoorView view = game_get_key_door_view();
    switch (checkStage_) {
    case 0: if (++checkFrames_ > 30) next(); break;
    case 1: walk(1.1f, true); break;
    case 2: walk(-4, false); break;
    case 3: walk(0, true); break;
    case 4: press(smlt::KEYBOARD_CODE_E); next(); break;
    case 5:
        require(view.feedback == SliceFeedback::DoorLocked, "Door did not report locked.");
        next(); break;
    case 6:
        press(smlt::KEYBOARD_CODE_S);
        if (++checkFrames_ > 100) {
            require(game_get_player_pos().z > -5, "Closed door did not block movement.");
            next();
        } break;
    case 7: walk(1.1f, true); break;
    case 8: walk(2.8f, false); break;
    case 9: walk(0, true); break;
    case 10:
        require(cameraId_ == 2, "North camera did not activate.");
        press(smlt::KEYBOARD_CODE_E); next(); break;
    case 11:
        require((view.flags & HasKey) && !key_->is_visible(), "Key was not collected/hidden.");
        press(smlt::KEYBOARD_CODE_E);
        if (++checkFrames_ > 10) next();
        break;
    case 12: walk(1.1f, true); break;
    case 13: walk(-4, false); break;
    case 14: walk(0, true); break;
    case 15:
        require(cameraId_ == 1, "South camera did not activate.");
        press(smlt::KEYBOARD_CODE_E); next(); break;
    case 16:
        require((view.flags & DoorOpen) && !door_->is_visible(), "Door did not open/hide.");
        press(smlt::KEYBOARD_CODE_S);
        if (view.flags & SliceComplete) next();
        break;
    case 17:
        require(game_get_player_pos().z <= room_.bindings.keyDoor.exitBoundaryZ, "Exit did not complete.");
        press(smlt::KEYBOARD_CODE_R); next(); break;
    case 18:
        require(audio_.sampleBytes() <= 512 * 1024, "Audio sample budget exceeded.");
        require(audio_.played[0] > 0 && audio_.played[1] == 1 && audio_.played[2] == 1 && audio_.played[3] == 1,
                "Audio playback did not receive exactly one of each interaction.");
        audio_.reset();
        const auto before = audio_.played[2];
        AudioEvents burst{4, {AudioCue::DoorLocked, AudioCue::DoorLocked, AudioCue::DoorLocked, AudioCue::DoorLocked}};
        audio_.consume(burst); audio_.consume(burst);
        require(audio_.played[2] == before + 5 && audio_.activeCount() <= 8,
                "Interaction audio exceeded its five reserved slots.");
        audio_.stop();
        require(audio_.activeCount() == 0, "Stopped audio still playing.");
        audio_.reset();
        require(audio_.activeCount() == audio_.placedCount(), "Reset did not restore exactly the authored placed sounds.");
        std::puts("SIMULANT AUDIO CHECKS PASSED: bank loaded, footsteps and all interactions played, reset restarted ambience.");
        require(view.flags == ObjectiveEnabled && key_->is_visible() && door_->is_visible(), "Reset did not restore the room.");
        require(cameraId_ == room_.bindings.gameplayCamera.id, "Reset camera mismatch.");
        std::puts("SIMULANT ROOM CHECKS PASSED: collision, key, door, escape, cameras and reset.");
        std::fflush(stdout);
        app->stop_running(); break;
    }
}
