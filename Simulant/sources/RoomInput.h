#pragma once
#include <simulant/simulant.h>
#include "Types.h"
#include <cmath>

// Device mapping only. Movement speed and interaction rules remain in Game/.
class RoomInput {
public:
    InputFrame read(smlt::InputState& state) {
        auto key = [&](smlt::KeyboardCode code) {
            for (std::size_t i = 0; i < state.keyboard_count(); ++i)
                if (state.keyboard_key_state(smlt::KeyboardID(static_cast<int8_t>(i)), code)) return true;
            return false;
        };
        InputFrame result{};
        result.move.x = float(key(smlt::KEYBOARD_CODE_D) || key(smlt::KEYBOARD_CODE_RIGHT)) -
                        float(key(smlt::KEYBOARD_CODE_A) || key(smlt::KEYBOARD_CODE_LEFT));
        result.move.y = float(key(smlt::KEYBOARD_CODE_W) || key(smlt::KEYBOARD_CODE_UP)) -
                        float(key(smlt::KEYBOARD_CODE_S) || key(smlt::KEYBOARD_CODE_DOWN));
        bool jump = key(smlt::KEYBOARD_CODE_SPACE);
        bool interact = key(smlt::KEYBOARD_CODE_E);
        bool restart = key(smlt::KEYBOARD_CODE_R);
        for (std::size_t i = 0; i < state.game_controller_count(); ++i) {
            auto controller = state.game_controller(smlt::GameControllerIndex(static_cast<int8_t>(i)));
            if (!controller) continue;
            Vec2 stick{controller->axis_state(smlt::JOYSTICK_AXIS_XL),
                       -controller->axis_state(smlt::JOYSTICK_AXIS_YL)};
            if (std::hypot(stick.x, stick.y) < 0.15f) stick = {};
            const auto hat = controller->hat_state(0);
            Vec2 pad{
                float(controller->button_state(smlt::JOYSTICK_BUTTON_DPAD_RIGHT) || (hat & smlt::HAT_POSITION_RIGHT)) -
                float(controller->button_state(smlt::JOYSTICK_BUTTON_DPAD_LEFT) || (hat & smlt::HAT_POSITION_LEFT)),
                float(controller->button_state(smlt::JOYSTICK_BUTTON_DPAD_UP) || (hat & smlt::HAT_POSITION_UP)) -
                float(controller->button_state(smlt::JOYSTICK_BUTTON_DPAD_DOWN) || (hat & smlt::HAT_POSITION_DOWN))};
            auto useStronger = [&](Vec2 candidate) {
                if (candidate.x * candidate.x + candidate.y * candidate.y >
                    result.move.x * result.move.x + result.move.y * result.move.y) result.move = candidate;
            };
            useStronger(stick); useStronger(pad);
            jump |= controller->button_state(smlt::JOYSTICK_BUTTON_B);
            interact |= controller->button_state(smlt::JOYSTICK_BUTTON_A);
            restart |= controller->button_state(smlt::JOYSTICK_BUTTON_START);
        }
        result.jumpPressed = jump && !wasJump_;
        wasJump_ = jump;
        result.interactPressed = interact && !wasInteract_;
        restartPressed = restart && !wasRestart_;
        quitPressed = key(smlt::KEYBOARD_CODE_ESCAPE);
        wasInteract_ = interact; wasRestart_ = restart;
        return result;
    }
    bool restartPressed = false, quitPressed = false;
private:
    bool wasInteract_ = false, wasRestart_ = false, wasJump_ = false;
};
