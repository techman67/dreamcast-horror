#include "SaveData.h"
#include <cmath>
#include <cstring>
#include <limits>
namespace {
unsigned word(const unsigned char* p) { return unsigned(p[0])|unsigned(p[1])<<8|unsigned(p[2])<<16|unsigned(p[3])<<24; }
void put(unsigned char* p,unsigned value) { for(unsigned i=0;i<4;++i) p[i]=static_cast<unsigned char>(value>>(8*i)); }
float number(const unsigned char* p) { unsigned bits=word(p); float v; std::memcpy(&v,&bits,4); return v; }
void putFloat(unsigned char* p,float v) { unsigned bits; std::memcpy(&bits,&v,4); put(p,bits); }
bool coordinate(float v) { return std::isfinite(v) && std::fabs(v)<=10000; }
bool valid(const SaveProgress& p) { if(p.worldRoom>8) return false; for(auto f:p.roomFlags) if((f!=0 && f!=1 && f!=3) || (!p.worldRoom && f)) return false; return coordinate(p.position.x)&&coordinate(p.position.y)&&coordinate(p.position.z)&&p.camera>0 && (p.flags&~3u)==0 && ((p.flags&2)==0 || (p.flags&1)!=0); }
struct Slots { SaveRecord records[2]{}; SaveResult results[2]{}; int newest=-1; };
Slots scan(ISaveStorage& storage) {
    Slots slots;
    for(unsigned i=0;i<2;++i) {
        unsigned char data[SaveRecordBytes]{}; unsigned size=sizeof(data);
        slots.results[i]=storage.read(i,data,size);
        if(slots.results[i]==SaveResult::Ok && !decodeSaveRecord(data,size,slots.records[i])) slots.results[i]=SaveResult::Invalid;
        if(slots.results[i]==SaveResult::Ok && (slots.newest<0 || slots.records[i].sequence>slots.records[slots.newest].sequence)) slots.newest=static_cast<int>(i);
    }
    return slots;
}
SaveResult failure(const Slots& slots) {
    for(auto r:slots.results) if(r==SaveResult::Unavailable || r==SaveResult::IoError) return r;
    for(auto r:slots.results) if(r==SaveResult::Invalid) return r;
    return SaveResult::Missing;
}
}
unsigned saveHash(const void* data,std::size_t size) {
    unsigned hash=2166136261u; auto p=static_cast<const unsigned char*>(data);
    for(std::size_t i=0;i<size;++i) hash=(hash^p[i])*16777619u;
    return hash;
}
const char* parseSavePoints(const unsigned char* data,std::size_t size,SavePoints& output) {
    if(!data || size<12 || word(data)!=0x31505344u) return "Missing or invalid save-point manifest. Export the room again.";
    SavePoints candidate; candidate.room=word(data+4); candidate.count=word(data+8);
    if(candidate.count>MaxSavePoints || size!=12+candidate.count*68) return "Save-point count/size is invalid (maximum 16).";
    for(unsigned i=0;i<candidate.count;++i) {
        const auto p=data+12+i*68; auto& point=candidate.points[i]; point.id=word(p);
        point.position={number(p+4),number(p+8),number(p+12)}; point.range=number(p+16);
        if(!point.id || !coordinate(point.position.x)||!coordinate(point.position.y)||!coordinate(point.position.z)||!std::isfinite(point.range)||point.range<.25f||point.range>3) return "Save point needs a unique positive ID, finite position and range 0.25-3 metres.";
        for(unsigned j=0;j<i;++j) if(candidate.points[j].id==point.id) return "Duplicate save-point ID.";
        bool ended=false;
        for(unsigned j=0;j<48;++j) { auto c=p[20+j]; if(c==0) ended=true; else if(ended || c<32 || c>126) return "Save prompt must be 1-47 printable ASCII characters."; point.prompt[j]=static_cast<char>(c); }
        if(!ended || !point.prompt[0]) return "Save prompt must be 1-47 printable ASCII characters.";
    }
    output=candidate; return nullptr;
}
void encodeSaveRecord(const SaveRecord& r,unsigned char* p) {
    put(p,0x32565344u); put(p+4,r.room); put(p+8,r.sequence);
    putFloat(p+12,r.progress.position.x); putFloat(p+16,r.progress.position.y); putFloat(p+20,r.progress.position.z);
    put(p+24,r.progress.camera); put(p+28,r.progress.flags); put(p+32,r.progress.worldRoom); for(unsigned i=0;i<8;++i) put(p+36+i*4,r.progress.roomFlags[i]); put(p+68,saveHash(p,68));
}
bool decodeSaveRecord(const unsigned char* p,std::size_t size,SaveRecord& output) {
    if(!p) return false;
    const bool legacy=size==36 && word(p)==0x31565344u;
    if(!legacy && (size!=SaveRecordBytes || word(p)!=0x32565344u)) return false;
    if(word(p+size-4)!=saveHash(p,size-4)) return false;
    SaveRecord r; r.room=word(p+4); r.sequence=word(p+8); r.progress.position={number(p+12),number(p+16),number(p+20)};
    r.progress.camera=word(p+24); r.progress.flags=word(p+28);
    if(!legacy) { r.progress.worldRoom=word(p+32); for(unsigned i=0;i<8;++i) r.progress.roomFlags[i]=word(p+36+i*4); }
    if(!r.sequence || !valid(r.progress)) return false;
    output=r; return true;
}
SaveResult readProgress(ISaveStorage& storage,unsigned room,SaveProgress& output) {
    auto slots=scan(storage); if(slots.newest<0) return failure(slots);
    auto& r=slots.records[slots.newest]; if(r.room!=room) return SaveResult::WrongRoom;
    output=r.progress; return SaveResult::Ok;
}
SaveResult writeProgress(ISaveStorage& storage,unsigned room,const SaveProgress& progress) {
    if(!valid(progress)) return SaveResult::NotReady;
    auto slots=scan(storage);
    for(auto r:slots.results) if(r==SaveResult::Unavailable || r==SaveResult::IoError) return r;
    unsigned sequence=slots.newest<0 ? 1 : slots.records[slots.newest].sequence+1;
    if(sequence==0) return SaveResult::NotReady;
    unsigned target=slots.newest==0 ? 1u : 0u;
    unsigned char bytes[SaveRecordBytes]; encodeSaveRecord({room,sequence,progress},bytes);
    auto result=storage.write(target,bytes,sizeof(bytes)); if(result!=SaveResult::Ok) return result;
    unsigned char verify[SaveRecordBytes]{}; unsigned size=sizeof(verify);
    result=storage.read(target,verify,size);
    if(result!=SaveResult::Ok || size!=sizeof(bytes) || std::memcmp(bytes,verify,sizeof(bytes))) return SaveResult::IoError;
    return SaveResult::Ok;
}
const char* saveResultText(SaveResult r) {
    switch(r) {
    case SaveResult::Ok: return "Progress saved.";
    case SaveResult::Missing: return "No saved game found.";
    case SaveResult::Unavailable: return "No memory card available. Insert a VMU.";
    case SaveResult::Full: return "Not enough free VMU blocks.";
    case SaveResult::Invalid: return "Saved data is invalid; no valid backup found.";
    case SaveResult::WrongRoom: return "Save belongs to another room/world or an older export.";
    case SaveResult::IoError: return "Storage operation failed. Try loading the previous save.";
    default: return "Cannot save/load now. Stand on the floor in an active room.";
    }
}
