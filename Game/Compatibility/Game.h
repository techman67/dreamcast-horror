#pragma once
#include "IDisplacementBackend.h"
#include "Types.h"

extern "C" {

// Initialise the Game with a displacement backend.
void game_init(IDisplacementBackend* displacement);

// Advance the Game by one frame.
// input may be null, in which case the Game performs no work this frame.
void game_step(const InputFrame* input, float deltaSeconds);

// Host/test inspection hook. NOT part of the eventual gameplay
// compatibility API unless Unity/Simulant demonstrates a need for it.
Vec3 game_get_player_pos();

}