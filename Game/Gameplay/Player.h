#pragma once
#include "GameState.h"
#include "IDisplacementBackend.h"
#include "Types.h"

// Advance player gameplay by one frame.
// Applies movement rules/speed, asks the backend to resolve the desired
// displacement against the world, and applies the resolved result.
void playerUpdate(GameState& state,
                  IDisplacementBackend& displacement,
                  const InputFrame& input,
                  float deltaSeconds);