#include "World.h"
#include "Game.h"
#include "MockBackend.h"
#include <cassert>
#include <cstdio>
#include <cstring>
#include <vector>
namespace {
std::vector<unsigned char> bytes;
void word(unsigned v) { for(unsigned i=0;i<4;++i) bytes.push_back(static_cast<unsigned char>(v>>(8*i))); }
void number(float f) { unsigned v; std::memcpy(&v,&f,4); word(v); }
void room(unsigned id,unsigned target) {
    word(id); word(100+id); const char path[]="rooms/test.scene";
    for(unsigned i=0;i<96;++i) bytes.push_back(i<sizeof(path) ? static_cast<unsigned char>(path[i]) : 0);
    word(1); word(1); // arrivals, links
    word(123); number(0); number(0); number(0); word(1);
    number(0); number(0); number(0); number(1); word(target); word(123); word(0);
}
struct Storage:ISaveStorage {
    std::vector<unsigned char> files[2];
    SaveResult read(unsigned i,unsigned char* p,unsigned& n) override { if(files[i].empty()) return SaveResult::Missing; if(n<files[i].size()) return SaveResult::Invalid; n=static_cast<unsigned>(files[i].size()); std::memcpy(p,files[i].data(),n); return SaveResult::Ok; }
    SaveResult write(unsigned i,const unsigned char* p,unsigned n) override { files[i].assign(p,p+n); return SaveResult::Ok; }
};
}
int main() {
    word(0x31525744); word(2); word(1); room(1,2); room(2,1);
    WorldData data; assert(!parseWorldData(bytes.data(),bytes.size(),data));
    for(unsigned i=0;i<bytes.size();++i) assert(parseWorldData(bytes.data(),i,data));
    auto bad=bytes; bad.push_back(0); assert(parseWorldData(bad.data(),bad.size(),data));
    bad=bytes; bad[4]=9; assert(parseWorldData(bad.data(),bad.size(),data));
    bad=bytes; bad[bytes.size()-12]=3; assert(parseWorldData(bad.data(),bad.size(),data));
    assert(!world_configure(bytes.data(),bytes.size())); assert(world_room()->id==1);
    MockBackend backend; SliceBindings bindings; bindings.playerActor={1}; bindings.gameplayCamera={1};
    bindings.keyDoor.keyActor={2}; bindings.keyDoor.doorActor={3}; bindings.keyDoor.keyPosition={10,0,10}; bindings.keyDoor.doorPosition={20,0,20};
    auto enter=[&]() { game_init(bindings,&backend); assert(world_enter()); };
    enter(); assert(!world_depart(false));
    SaveProgress collected{{0,0,0},1,3}; assert(game_restore_progress(&collected));
    assert(world_depart(true)); assert(world_pending() && world_room()->id==2); assert(!world_depart(true));
    game_init(SliceBindings{},nullptr); // Simulate the host releasing the old room.
    enter(); assert(!(game_get_key_door_view().flags&3));
    Storage storage; assert(world_save(storage)==SaveResult::Ok);
    assert(world_depart(true)); enter(); assert((game_get_key_door_view().flags&3)==3 && !backend.doorBlocked);
    assert(world_load(storage)==SaveResult::Ok && world_pending() && world_room()->id==2); enter();
    assert(!(game_get_key_door_view().flags&3));
    assert(world_depart(true)); enter(); assert((game_get_key_door_view().flags&3)==3);
    for(unsigned i=0;i<100;++i) { assert(world_depart(true)); enter(); assert((game_get_key_door_view().flags&3)==(world_room()->id==1 ? 3u : 0u)); }
    auto invalid=storage.files[0]; SaveRecord record; assert(decodeSaveRecord(invalid.data(),invalid.size(),record));
    record.progress.camera=99; encodeSaveRecord(record,invalid.data()); storage.files[0]=invalid;
    const unsigned before=world_room()->id; assert(world_load(storage)==SaveResult::Invalid && !world_pending() && world_room()->id==before);
    world_restart(); enter(); assert(world_room()->id==1 && !(game_get_key_door_view().flags&3));
    auto gated=bytes; gated[168]=1; // First link requires the local door to be open.
    assert(!world_configure(gated.data(),gated.size())); bindings.keyDoor.doorPosition={0,0,0}; bindings.keyDoor.doorRange=1;
    enter(); assert(!world_depart(true));
    SaveProgress withKey{{0,0,0},1,1}; assert(game_restore_progress(&withKey)); assert(!world_depart(true));
    InputFrame interact; interact.interactPressed=true; game_step(&interact,.016f);
    assert(game_get_key_door_view().flags&DoorOpen); assert(world_depart(true)); bindings.keyDoor.doorPosition={20,0,20}; enter();
    assert(world_depart(true)); enter(); assert(game_get_key_door_view().flags&DoorOpen);
    // Move the door prompt away so the nearby save point wins over the link.
    bindings.keyDoor.doorPosition={20,0,20}; game_init(bindings,&backend); assert(world_enter());
    SavePoints points; points.count=1; points.points[0]={1,{0,.9f,0},1,"Save"}; game_set_save_points(&points);
    assert(game_near_save_point()==0 && !world_depart(true));
    world_clear(); assert(!world_active() && !world_room() && !world_pending());
    std::puts("World tests passed: bounded manifest, links, 100 round trips, room progress, cross-room save/load, restart.");
}
