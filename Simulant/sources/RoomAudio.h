#pragma once
#include <simulant/simulant.h>
#include "AudioData.h"
#include <cstdint>

class RoomAudio {
public:
    ~RoomAudio();
    void load(smlt::Scene*, smlt::Stage*);
    void reset();
    void stop();
    void unload();
    void consume(AudioEvents events);
    void update(Vec3 listener);
    unsigned sampleBytes() const { return sampleBytes_; }
    unsigned activeCount() const;
    unsigned placedCount() const { return data_.sourceCount; }
    unsigned played[4]{};
private:
    void play(unsigned slot, unsigned clip, float gain, bool loop);
    void volume(unsigned slot, float gain);
    bool busy(unsigned slot) const;
    AudioData data_{};
    unsigned buffers_[MaxAudioClips]{};
    int channels_[8]{-1,-1,-1,-1,-1,-1,-1,-1};
    std::uint64_t ends_[8]{};
    bool loops_[8]{};
    int gains_[8]{-1,-1,-1,-1,-1,-1,-1,-1};
    Vec3 listener_{};
    unsigned sampleBytes_ = 0;
};
