#include "World.h"
#include "Game.h"
namespace {
WorldData data;
unsigned hash=0,current=0,arrival=0,flags[8]{};
bool pending=false,restoring=false;
SaveProgress restored;
}
void world_clear() { data.count=0; current=0; pending=false; restoring=false; for(auto& f:flags) f=0; }
bool world_active() { return data.count!=0; }
const WorldRoom* world_room() { return findWorldRoom(data,current); }
bool world_pending() { return pending; }
const char* world_configure(const unsigned char* bytes,std::size_t size,unsigned start) {
    WorldData candidate; if(const char* error=parseWorldData(bytes,size,candidate)) return error;
    if(start && !findWorldRoom(candidate,start)) return "Active scene is not in this world.";
    world_clear(); data=candidate; hash=saveHash(bytes,size); current=start ? start : data.start; arrival=0; return nullptr;
}
bool world_enter() {
    const auto* room=world_room(); if(!room) return false;
    SaveProgress p; p.position=game_get_player_pos(); p.camera=game_get_camera_ref().id;
    p.flags=flags[current-1];
    if(restoring) p=restored;
    else if(arrival) { const auto* a=findRoomArrival(*room,arrival); if(!a) return false; p.position=a->position; p.camera=a->camera; }
    if(!game_restore_progress(&p)) return false;
    game_set_linked_room(room->linkCount!=0);
    pending=false; restoring=false; arrival=0; return true;
}
int world_near_link() {
    const auto* room=world_room(); if(!room || pending) return -1;
    auto view=game_get_key_door_view();
    if(view.prompt!=InteractionPrompt::None && view.prompt!=InteractionPrompt::OpenDoor) return -1;
    if(game_near_save_point()>=0) return -1;
    SaveProgress progress; if(!game_capture_progress(&progress)) return -1;
    auto p=progress.position;
    for(unsigned i=0;i<room->linkCount;++i) {
        const auto& l=room->links[i]; float x=p.x-l.position.x,y=p.y-l.position.y,z=p.z-l.position.z;
        if((!l.requiresDoor || (view.flags&DoorOpen)) && x*x+y*y+z*z<=l.range*l.range) return static_cast<int>(i);
    }
    return -1;
}
bool world_depart(bool interact) {
    int near=world_near_link(); if(!interact || near<0) return false;
    const auto link=world_room()->links[near];
    flags[current-1]=game_get_key_door_view().flags&3;
    current=link.room; arrival=link.arrival; pending=true; return true;
}
void world_restart() { if(!world_active()) return; for(auto& f:flags) f=0; current=data.start; arrival=0; restoring=false; pending=true; }
SaveResult world_save(ISaveStorage& storage) {
    SaveProgress p; if(!world_active() || pending || !game_capture_progress(&p)) return SaveResult::NotReady;
    flags[current-1]=p.flags; p.worldRoom=current;
    for(unsigned i=0;i<8;++i) p.roomFlags[i]=flags[i];
    return writeProgress(storage,hash,p);
}
SaveResult world_load(ISaveStorage& storage) {
    if(!world_active() || pending) return SaveResult::NotReady;
    SaveProgress p; auto result=readProgress(storage,hash,p); if(result!=SaveResult::Ok) return result;
    if(p.camera<1 || p.camera>2 || !findWorldRoom(data,p.worldRoom) || p.flags!=p.roomFlags[p.worldRoom-1]) return SaveResult::Invalid;
    for(unsigned i=0;i<8;++i) if(!findWorldRoom(data,i+1) && p.roomFlags[i]) return SaveResult::Invalid;
    for(unsigned i=0;i<8;++i) flags[i]=p.roomFlags[i];
    current=p.worldRoom; restored=p; restoring=true; arrival=0; pending=true; return SaveResult::Ok;
}
