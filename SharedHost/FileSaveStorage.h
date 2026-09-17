#pragma once
#include "SaveData.h"
#include <cstdio>
#include <cerrno>
#include <string>
#ifdef _WIN32
#include <windows.h>
#include <io.h>
#else
#include <unistd.h>
#endif
// Desktop adapter. Caller creates the directory; never used by Dreamcast.
class FileSaveStorage final : public ISaveStorage {
    std::string directory_;
    FILE* open(unsigned slot,const char* mode) {
        std::string path=directory_+"/progress"+std::to_string(slot)+".sav";
#ifdef _WIN32
        int count=MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,path.c_str(),-1,nullptr,0);
        if(count<=0) { errno=EINVAL; return nullptr; }
        std::wstring wide(static_cast<std::size_t>(count),L'\0');
        MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,path.c_str(),-1,&wide[0],count);
        return _wfopen(wide.c_str(),mode[0]=='r' ? L"rb" : L"wb");
#else
        return std::fopen(path.c_str(),mode);
#endif
    }
public:
    explicit FileSaveStorage(const char* directory):directory_(directory) {}
    SaveResult read(unsigned slot,unsigned char* data,unsigned& size) override {
        auto file=open(slot,"rb"); if(!file) return errno==ENOENT ? SaveResult::Missing : SaveResult::IoError;
        const auto count=std::fread(data,1,size,file); int extra=std::fgetc(file); bool error=std::ferror(file)!=0;
        if(std::fclose(file)!=0) error=true;
        if(error) return SaveResult::IoError;
        if(extra!=EOF) return SaveResult::Invalid;
        size=static_cast<unsigned>(count); return SaveResult::Ok;
    }
    SaveResult write(unsigned slot,const unsigned char* data,unsigned size) override {
        auto file=open(slot,"wb"); if(!file) return SaveResult::IoError;
        bool ok=std::fwrite(data,1,size,file)==size && std::fflush(file)==0;
#ifdef _WIN32
        if(ok) ok=_commit(_fileno(file))==0;
#else
        if(ok) ok=fsync(fileno(file))==0;
#endif
        if(std::fclose(file)!=0) ok=false;
        return ok ? SaveResult::Ok : SaveResult::IoError;
    }
};
