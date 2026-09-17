#pragma once
#include "Types.h"
#include <cstddef>
#include <cstdint>

constexpr unsigned MaxSavePoints=16;
struct SavePoint { unsigned id=0; Vec3 position{}; float range=1; char prompt[48]{}; };
struct SavePoints { unsigned room=0,count=0; SavePoint points[MaxSavePoints]{}; };
struct SaveProgress { Vec3 position{}; unsigned camera=0,flags=0; unsigned worldRoom=0,roomFlags[8]{}; };
constexpr unsigned SaveRecordBytes=72;
struct SaveRecord { unsigned room=0,sequence=0; SaveProgress progress{}; };
unsigned saveHash(const void* data,std::size_t size);
const char* parseSavePoints(const unsigned char* data,std::size_t size,SavePoints& output);
void encodeSaveRecord(const SaveRecord& record,unsigned char* output);
bool decodeSaveRecord(const unsigned char* data,std::size_t size,SaveRecord& output);

enum class SaveResult : int { Ok, Missing, Unavailable, Full, Invalid, WrongRoom, IoError, NotReady };
const char* saveResultText(SaveResult result);
// Host implements durable byte storage only. Game format and recovery are shared.
class ISaveStorage {
public:
    virtual ~ISaveStorage()=default;
    virtual SaveResult read(unsigned slot,unsigned char* data,unsigned& size)=0;
    virtual SaveResult write(unsigned slot,const unsigned char* data,unsigned size)=0;
};
SaveResult readProgress(ISaveStorage& storage,unsigned room,SaveProgress& output);
SaveResult writeProgress(ISaveStorage& storage,unsigned room,const SaveProgress& progress);
