#pragma once
#include "SaveData.h"
#include <cstring>
#include <cstdlib>
#ifdef __DREAMCAST__
#include <dc/maple.h>
#include <dc/vmufs.h>
#include <dc/vmu_pkg.h>
class RoomSaveStorage final : public ISaveStorage {
    maple_device_t* device_=nullptr;
    int port_=0,unit_=0;
    unsigned existing_[2]{};
    bool available() const { return device_ && maple_enum_dev(port_,unit_)==device_ && device_->valid; }
    const char* name(unsigned i) const { return i ? "DCHORROR1" : "DCHORROR0"; }
public:
    RoomSaveStorage() { device_=maple_enum_type(0,MAPLE_FUNC_MEMCARD); if(device_) { port_=device_->port; unit_=device_->unit; } }
    SaveResult read(unsigned slot,unsigned char* data,unsigned& size) override {
        if(!available()) return SaveResult::Unavailable;
        void* bytes=nullptr; int length=0; int result=vmufs_read(device_,name(slot),&bytes,&length);
        if(result<0) return result==-2 ? SaveResult::Missing : SaveResult::IoError;
        const char appId[16]="DCHORROR";
        // Refuse to overwrite a different application's file using our filename.
        if(length<128 || std::memcmp(static_cast<unsigned char*>(bytes)+48,appId,16)) { std::free(bytes); return SaveResult::IoError; }
        existing_[slot]=static_cast<unsigned>(length)/512;
        vmu_pkg_t pkg{};
        bool valid=length<=2048 && vmu_pkg_parse(static_cast<uint8_t*>(bytes),static_cast<std::size_t>(length),&pkg)==0 && (pkg.data_len==SaveRecordBytes || pkg.data_len==36) && size>=static_cast<unsigned>(pkg.data_len);
        if(valid) { size=static_cast<unsigned>(pkg.data_len); std::memcpy(data,pkg.data,size); }
        std::free(bytes); return valid ? SaveResult::Ok : SaveResult::Invalid;
    }
    SaveResult write(unsigned slot,const unsigned char* data,unsigned size) override {
        if(!available()) return SaveResult::Unavailable;
        if(size!=SaveRecordBytes) return SaveResult::Invalid;
        int freeBlocks=vmufs_free_blocks(device_); if(freeBlocks<0) return SaveResult::IoError;
        if(static_cast<unsigned>(freeBlocks)+existing_[slot]<2) return SaveResult::Full;
        vmu_pkg_t pkg{}; std::strcpy(pkg.desc_short,"Horror progress"); std::strcpy(pkg.desc_long,"Dreamcast Horror save / backup"); std::strcpy(pkg.app_id,"DCHORROR");
        unsigned char icon[512]{};
        for(unsigned y=4;y<28;++y) for(unsigned x=6;x<26;++x) if(x<9 || x>22 || (y>13 && y<18)) icon[y*16+x/2]|=static_cast<unsigned char>(x%2 ? 1 : 16);
        pkg.icon_cnt=1; pkg.icon_pal[1]=0xffff; pkg.icon_data=icon; pkg.data=data; pkg.data_len=static_cast<int>(size);
        uint8_t* built=nullptr; int length=0;
        if(vmu_pkg_build(&pkg,&built,&length)<0) return SaveResult::IoError;
        // KOS writes whole blocks: provide padded storage, not an undersized package buffer.
        unsigned char padded[1024]{};
        if(length<0 || length>1024) { std::free(built); return SaveResult::Invalid; }
        std::memcpy(padded,built,static_cast<std::size_t>(length)); std::free(built);
        int result=vmufs_write(device_,name(slot),padded,sizeof(padded),VMUFS_OVERWRITE);
        return result==0 ? SaveResult::Ok : result==-7 ? SaveResult::Full : SaveResult::IoError;
    }
};
#else
#include "../SharedHost/FileSaveStorage.h"
#include <sys/stat.h>
class RoomSaveStorage final : public ISaveStorage {
    FileSaveStorage files_;
    static const char* directory() { const char* overridePath=std::getenv("DREAMCAST_SAVE_DIRECTORY"); return overridePath ? overridePath : ".dreamcast-saves"; }
public:
    RoomSaveStorage():files_(directory()) { mkdir(directory(),0700); }
    SaveResult read(unsigned slot,unsigned char* data,unsigned& size) override { return files_.read(slot,data,size); }
    SaveResult write(unsigned slot,const unsigned char* data,unsigned size) override { return files_.write(slot,data,size); }
};
#endif
