#include "WorldData.h"
#include <cmath>
#include <cstring>
namespace {
struct Reader {
    const unsigned char* p; std::size_t left; bool ok=true;
    unsigned word() { if(left<4) { ok=false; return 0; } unsigned v=unsigned(p[0])|unsigned(p[1])<<8|unsigned(p[2])<<16|unsigned(p[3])<<24; p+=4; left-=4; return v; }
    float number() { unsigned bits=word(); float v; std::memcpy(&v,&bits,4); return v; }
    Vec3 position() { return {number(),number(),number()}; }
};
bool position(Vec3 p) { return std::isfinite(p.x)&&std::isfinite(p.y)&&std::isfinite(p.z)&&std::fabs(p.x)<=10000&&std::fabs(p.y)<=10000&&std::fabs(p.z)<=10000; }
}
const WorldRoom* findWorldRoom(const WorldData& w,unsigned id) { for(unsigned i=0;i<w.count;++i) if(w.rooms[i].id==id) return &w.rooms[i]; return nullptr; }
const RoomArrival* findRoomArrival(const WorldRoom& r,unsigned id) { for(unsigned i=0;i<r.arrivalCount;++i) if(r.arrivals[i].id==id) return &r.arrivals[i]; return nullptr; }
const char* parseWorldData(const unsigned char* bytes,std::size_t size,WorldData& output) {
    if(!bytes || size<12 || size>4096) return "World manifest must be 12-4096 bytes.";
    Reader in{bytes,size}; if(in.word()!=0x31525744u) return "Unsupported world manifest; export connected rooms again.";
    WorldData w; w.count=in.word(); w.start=in.word();
    if(w.count==0 || w.count>MaxWorldRooms) return "World needs 1-8 rooms.";
    for(unsigned i=0;i<w.count;++i) {
        auto& r=w.rooms[i]; r.id=in.word(); r.hash=in.word();
        if(r.id<1 || r.id>8) return "Room IDs must be unique numbers 1-8.";
        for(unsigned j=0;j<i;++j) if(w.rooms[j].id==r.id) return "Duplicate room ID.";
        if(in.left<96) return "Truncated scene path.";
        bool ended=false;
        for(unsigned j=0;j<96;++j) { unsigned c=*in.p++; --in.left; if(!c) ended=true; else if(ended || c<32 || c>126 || c=='\\' || c==':') return "Invalid scene path."; r.scene[j]=static_cast<char>(c); }
        // Optional host metadata; the portable game never interprets an engine scene format.
        if(!ended || r.scene[0]=='/' || std::strstr(r.scene,"..")) return "Host scene reference must be empty or a relative path without parent traversal.";
        r.arrivalCount=in.word(); r.linkCount=in.word();
        if(r.arrivalCount>8 || r.linkCount>8) return "Maximum eight arrivals and eight links per room.";
        for(unsigned j=0;j<r.arrivalCount;++j) {
            auto& a=r.arrivals[j]; a.id=in.word(); a.position=in.position(); a.camera=in.word();
            if(!a.id || !position(a.position) || a.camera<1 || a.camera>2) return "Arrival needs an ID, finite position and camera 1 or 2.";
            for(unsigned k=0;k<j;++k) if(r.arrivals[k].id==a.id) return "Duplicate arrival ID.";
        }
        for(unsigned j=0;j<r.linkCount;++j) {
            auto& l=r.links[j]; l.position=in.position(); l.range=in.number(); l.room=in.word(); l.arrival=in.word(); l.requiresDoor=in.word();
            if(!position(l.position)||!std::isfinite(l.range)||l.range<.25f||l.range>3||l.requiresDoor>1) return "Link needs finite position, range 0.25-3 and a valid door requirement.";
        }
    }
    if(!in.ok || in.left) return "Truncated or trailing world data.";
    if(!findWorldRoom(w,w.start)) return "Starting room is missing.";
    for(unsigned i=0;i<w.count;++i) for(unsigned j=0;j<w.rooms[i].linkCount;++j) {
        const auto& l=w.rooms[i].links[j]; const auto* target=findWorldRoom(w,l.room);
        if(!target || !findRoomArrival(*target,l.arrival)) return "Room link has a missing destination or arrival.";
    }
    output=w; return nullptr;
}
