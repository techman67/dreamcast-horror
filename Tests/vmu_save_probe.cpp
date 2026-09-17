// Standalone KOS/Flycast probe; run only with isolated test VMUs.
#include "RoomSaveStorage.h"
#include <kos.h>
#include <cstdio>
int main() {
    thd_sleep(1500);
    RoomSaveStorage storage; SaveProgress progress;
    auto result=readProgress(storage,0x12345678,progress);
    if(result==SaveResult::Unavailable) { std::puts("VMU PROBE NO CARD: detected correctly"); std::fflush(stdout); for(;;) thd_sleep(1000); }
    if(result==SaveResult::Ok) {
        if(progress.flags!=3 || progress.position.x!=2) { std::puts("VMU PROBE FAILED: reboot state mismatch"); return 1; }
        progress.position.z=42;
        if(writeProgress(storage,0x12345678,progress)!=SaveResult::Ok) return 1;
        std::puts("VMU PROBE REBOOT LOAD PASSED");
    } else if(result==SaveResult::Missing) {
        progress={{1,0,0},1,1}; if(writeProgress(storage,0x12345678,progress)!=SaveResult::Ok) { std::puts("VMU PROBE FAILED: first write"); return 1; }
        progress={{2,0,0},1,3}; if(writeProgress(storage,0x12345678,progress)!=SaveResult::Ok) { std::puts("VMU PROBE FAILED: second write"); return 1; }
        if(readProgress(storage,0x12345678,progress)!=SaveResult::Ok || progress.flags!=3) { std::puts("VMU PROBE FAILED: readback"); return 1; }
        unsigned char bad[SaveRecordBytes]{};
        if(storage.write(1,bad,sizeof(bad))!=SaveResult::Ok || readProgress(storage,0x12345678,progress)!=SaveResult::Ok || progress.flags!=1) { std::puts("VMU PROBE FAILED: backup recovery"); return 1; }
        progress={{2,0,0},1,3}; if(writeProgress(storage,0x12345678,progress)!=SaveResult::Ok) { std::puts("VMU PROBE FAILED: recovery write"); return 1; }
        std::puts("VMU PROBE WRITE READ RECOVERY PASSED");
    } else { std::printf("VMU PROBE FAILED: initial read %d\n",int(result)); return 1; }
    std::fflush(stdout); for(;;) thd_sleep(1000);
}
