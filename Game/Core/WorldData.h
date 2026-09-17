#pragma once
#include "SaveData.h"

constexpr unsigned MaxWorldRooms=8, MaxRoomArrivals=8, MaxRoomLinks=8;
struct RoomArrival { unsigned id=0; Vec3 position{}; unsigned camera=1; };
struct RoomLink { Vec3 position{}; float range=1; unsigned room=0,arrival=0,requiresDoor=0; };
struct WorldRoom {
    unsigned id=0,hash=0; char scene[96]{};
    unsigned arrivalCount=0,linkCount=0;
    RoomArrival arrivals[MaxRoomArrivals]{};
    RoomLink links[MaxRoomLinks]{};
};
struct WorldData { unsigned count=0,start=0; WorldRoom rooms[MaxWorldRooms]{}; };
const char* parseWorldData(const unsigned char*,std::size_t,WorldData&);
const WorldRoom* findWorldRoom(const WorldData&,unsigned id);
const RoomArrival* findRoomArrival(const WorldRoom&,unsigned id);
