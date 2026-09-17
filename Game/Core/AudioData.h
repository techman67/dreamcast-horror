#pragma once
#include "Types.h"

constexpr unsigned MaxAudioClips = 8, MaxRoomSounds = 2;
struct AudioClipData { unsigned pcmBytes = 0; char file[72]{}; };
struct AudioCueData { int clip = -1; float volume = 1; };
struct RoomSoundData {
    unsigned clip = 0, loop = 1;
    float volume = 1;
    Vec3 position;
    float range = 0; // Zero means room-wide; otherwise silent at/beyond this radius.
    float fullVolumeRange = 0; // Linear fade starts here; must be smaller than range.
};
struct AudioData {
    unsigned clipCount = 0, sourceCount = 0;
    AudioClipData clips[MaxAudioClips]{};
    AudioCueData cues[4]{};
    RoomSoundData sources[MaxRoomSounds]{};
};
const char* parseAudioData(const char* text, AudioData& output);
float roomSoundGain(const RoomSoundData& sound, Vec3 listener);
