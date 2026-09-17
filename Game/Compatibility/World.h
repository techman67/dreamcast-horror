#pragma once
#include "WorldData.h"
// Hosts own scenes/resources; this fixed-capacity session owns only game progress.
const char* world_configure(const unsigned char* bytes,std::size_t size,unsigned start=0);
void world_clear();
bool world_active();
const WorldRoom* world_room();
bool world_enter();
int world_near_link();
bool world_depart(bool interact);
bool world_pending();
void world_restart();
SaveResult world_save(ISaveStorage&);
SaveResult world_load(ISaveStorage&);
