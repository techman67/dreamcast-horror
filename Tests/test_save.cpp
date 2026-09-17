#include "SaveData.h"
#include "Game.h"
#include "MockBackend.h"
#include <cassert>
#include <cstdio>
#include <cstring>
#include <vector>
#include <limits>
struct Memory final:ISaveStorage {
    std::vector<unsigned char> files[2]; SaveResult failure=SaveResult::Ok; bool corrupt=false;
    SaveResult read(unsigned slot,unsigned char* data,unsigned& size) override { if(files[slot].empty()) return SaveResult::Missing; if(files[slot].size()>size) return SaveResult::Invalid; size=static_cast<unsigned>(files[slot].size()); std::memcpy(data,files[slot].data(),size); return SaveResult::Ok; }
    SaveResult write(unsigned slot,const unsigned char* data,unsigned size) override { if(failure!=SaveResult::Ok) return failure; files[slot].assign(data,data+size); if(corrupt) files[slot][4]^=1; return SaveResult::Ok; }
};
int main() {
    Memory storage; SaveProgress state{{1,2,3},1,1},out{{9,9,9},2,0};
    assert(readProgress(storage,4,out)==SaveResult::Missing && out.position.x==9);
    assert(writeProgress(storage,4,state)==SaveResult::Ok);
    state.flags=3; assert(writeProgress(storage,4,state)==SaveResult::Ok);
    assert(readProgress(storage,4,out)==SaveResult::Ok && out.flags==3);
    assert(readProgress(storage,5,out)==SaveResult::WrongRoom);
    storage.files[1][0]^=1;
    assert(readProgress(storage,4,out)==SaveResult::Ok && out.flags==1);
    storage.failure=SaveResult::Full;
    assert(writeProgress(storage,4,state)==SaveResult::Full);
    assert(readProgress(storage,4,out)==SaveResult::Ok && out.flags==1);
    storage.failure=SaveResult::Ok; storage.corrupt=true;
    assert(writeProgress(storage,4,state)==SaveResult::IoError);
    assert(readProgress(storage,4,out)==SaveResult::Ok && out.flags==1);
    state.position.x=std::numeric_limits<float>::quiet_NaN();
    assert(writeProgress(storage,4,state)==SaveResult::NotReady);
    SaveRecord record{4,1,{{1,2,3},1,1}},decoded; unsigned char bytes[SaveRecordBytes]; encodeSaveRecord(record,bytes);
    for(unsigned i=0;i<SaveRecordBytes;++i) { bytes[i]^=1; assert(!decodeSaveRecord(bytes,sizeof(bytes),decoded)); bytes[i]^=1; }
    for(unsigned i=0;i<SaveRecordBytes;++i) assert(!decodeSaveRecord(bytes,i,decoded));
    MockBackend backend; SliceBindings bindings; bindings.playerActor={1}; bindings.gameplayCamera={1};
    bindings.keyDoor.keyActor={2}; bindings.keyDoor.doorActor={3}; bindings.keyDoor.keyPosition={10,0,10}; bindings.keyDoor.doorPosition={20,0,20};
    game_init(bindings,&backend); SavePoints points; points.count=1; points.points[0]={1,{0,.9f,0},1,"Use journal"}; game_set_save_points(&points);
    assert(game_near_save_point()==0); InputFrame input; input.interactPressed=true; game_step(&input,.016f);
    assert(game_take_save_request()==0 && game_take_save_request()==-1);
    SaveProgress restored{{3,0,4},1,3}; assert(game_restore_progress(&restored)); assert(!backend.doorBlocked); assert(game_get_player_pos().x==3);
    assert((game_get_key_door_view().flags&3)==3); SaveProgress capture; assert(game_capture_progress(&capture) && capture.flags==3);
    restored.camera=99; assert(!game_restore_progress(&restored) && game_get_player_pos().x==3);
    game_init(bindings,&backend); assert(backend.doorBlocked && game_near_save_point()==-1);
    std::puts("Save tests passed: format validation, recovery, failed/full writes, restore and interaction.");
}
