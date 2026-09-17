#pragma once
#include "GameState.h"
#include "IDisplacementBackend.h"

void initializeKeyDoor(GameState& state, IDisplacementBackend& displacement);
void updateKeyDoor(GameState& state, IDisplacementBackend& displacement,
                   const InputFrame& input, float deltaSeconds);
KeyDoorView keyDoorView(const GameState& state);
