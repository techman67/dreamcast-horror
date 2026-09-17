#include "RoomAudio.h"
#include <algorithm>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>
#ifdef __DREAMCAST__
#include <malloc.h>
#include <dc/sound/sound.h>
#include <dc/sound/sfxmgr.h>
#include <dc/sound/aica_comm.h>
#else
#include <AL/al.h>
#endif

namespace {
std::uint64_t now() { return smlt::get_app()->time_keeper->now_in_us(); }
std::string path(const std::string& relative) {
    std::string p = "assets/" + relative;
    return std::ifstream(p) ? p : "/cd/" + p;
}
unsigned le(const unsigned char* p, unsigned n) {
    unsigned value = 0;
    for (unsigned i = 0; i < n; ++i) value |= unsigned(p[i]) << (8 * i);
    return value;
}
void validate(const std::string& p, unsigned expected) {
    std::ifstream file(p, std::ios::binary | std::ios::ate);
    const auto size = file.tellg(); unsigned char h[44]{};
    file.seekg(0); file.read(reinterpret_cast<char*>(h), sizeof h);
    if (!file || std::memcmp(h, "RIFF", 4) || std::memcmp(h + 8, "WAVEfmt ", 8) ||
        le(h+16,4) != 16 || le(h+20,2) != 1 || le(h+22,2) != 1 || le(h+24,4) != 11025 ||
        le(h+28,4) != 22050 || le(h+32,2) != 2 || le(h+34,2) != 16 ||
        std::memcmp(h+36,"data",4) || le(h+40,4) != expected ||
        size != std::streamoff(expected + 44) || le(h+4,4) != expected + 36)
        throw std::runtime_error("Invalid exported PCM or manifest mismatch: " + p);
}
void memory(const char* phase) {
#ifdef __DREAMCAST__
    std::printf("AUDIO MEMORY %s: heap=%u, AICA free=%u bytes\n", phase,
                unsigned(mallinfo().uordblks), unsigned(snd_mem_available()));
#else
    (void)phase;
#endif
}
}
RoomAudio::~RoomAudio() { unload(); }
void RoomAudio::load(smlt::Scene*, smlt::Stage*) try {
    std::ifstream manifest(path("sample.audio"), std::ios::binary | std::ios::ate);
    if (!manifest || manifest.tellg() > 8192) throw std::runtime_error("Export the room audio in Unity first (sample.audio missing/oversized).");
    manifest.seekg(0);
    std::string text((std::istreambuf_iterator<char>(manifest)), {});
    if (const char* error = parseAudioData(text.c_str(), data_)) throw std::runtime_error(error);
    memory("before bank");
    // Validate the whole bundle before allocating samples in the sound driver.
    for (unsigned i = 0; i < data_.clipCount; ++i) {
        validate(path(std::string("room-audio/") + data_.clips[i].file), data_.clips[i].pcmBytes);
        sampleBytes_ += data_.clips[i].pcmBytes;
    }
    for (unsigned i = 0; i < data_.clipCount; ++i) {
        const auto p = path(std::string("room-audio/") + data_.clips[i].file);
#ifdef __DREAMCAST__
        buffers_[i] = snd_sfx_load(p.c_str());
        if (!buffers_[i]) throw std::runtime_error("AICA sample upload failed.");
#else
        std::ifstream file(p, std::ios::binary); file.seekg(44);
        std::vector<char> pcm(data_.clips[i].pcmBytes); file.read(pcm.data(), pcm.size());
        alGenBuffers(1, &buffers_[i]);
        alBufferData(buffers_[i], AL_FORMAT_MONO16, pcm.data(), pcm.size(), 11025);
        if (alGetError() != AL_NO_ERROR) throw std::runtime_error("OpenAL sample upload failed.");
#endif
    }
    for (unsigned i = 0; i < 8; ++i) {
#ifdef __DREAMCAST__
        channels_[i] = snd_sfx_chn_alloc(); // Same allocator as ALdc: no channel collisions.
        if (channels_[i] < 0) throw std::runtime_error("Unable to reserve AICA voice.");
#else
        ALuint source = 0; alGenSources(1, &source); channels_[i] = static_cast<int>(source);
        alSourcei(source, AL_SOURCE_RELATIVE, AL_TRUE); alSource3f(source, AL_POSITION, 0, 0, 0);
        if (alGetError() != AL_NO_ERROR) throw std::runtime_error("Unable to reserve OpenAL voice.");
#endif
    }
    std::printf("AUDIO: %u resident PCM bytes; %u clips, %u placed sounds; 8 reserved voices\n",
                sampleBytes_, data_.clipCount, data_.sourceCount);
    memory("bank loaded");
} catch (const std::exception& error) {
    std::printf("AUDIO LOAD FAILED: %s\n", error.what());
    std::fflush(stdout);
    throw;
}
bool RoomAudio::busy(unsigned slot) const { return loops_[slot] || ends_[slot] > now(); }
unsigned RoomAudio::activeCount() const {
    unsigned result = 0; for (unsigned i = 0; i < 8; ++i) if (busy(i)) ++result; return result;
}
void RoomAudio::volume(unsigned slot, float gain) {
    const int value = static_cast<int>(std::max(0.0f, std::min(1.0f, gain)) * 255);
    if (channels_[slot] < 0 || gains_[slot] == value) return;
    gains_[slot] = value;
#ifdef __DREAMCAST__
    AICA_CMDSTR_CHANNEL(tmp, cmd, chan);
    cmd->cmd = AICA_CMD_CHAN; cmd->timestamp = 0; cmd->size = AICA_CMDSTR_CHANNEL_SIZE;
    cmd->cmd_id = channels_[slot]; chan->cmd = AICA_CH_CMD_UPDATE | AICA_CH_UPDATE_SET_VOL;
    chan->vol = value; snd_sh4_to_aica(tmp, cmd->size);
#else
    alSourcef(channels_[slot], AL_GAIN, value / 255.0f);
#endif
}
void RoomAudio::play(unsigned slot, unsigned clip, float gain, bool loop) {
    gains_[slot] = -1;
#ifdef __DREAMCAST__
    snd_sfx_stop(channels_[slot]);
    sfx_play_data_t data{}; data.chn = channels_[slot]; data.idx = buffers_[clip];
    data.vol = static_cast<int>(gain * 255); data.pan = 128; data.freq = 11025; data.loop = loop;
    if (snd_sfx_play_ex(&data) < 0) throw std::runtime_error("AICA playback failed.");
#else
    alSourceStop(channels_[slot]); alSourcei(channels_[slot], AL_BUFFER, buffers_[clip]);
    alSourcei(channels_[slot], AL_LOOPING, loop ? AL_TRUE : AL_FALSE);
    alSourcef(channels_[slot], AL_GAIN, gain); alSourcePlay(channels_[slot]);
    if (alGetError() != AL_NO_ERROR) throw std::runtime_error("OpenAL playback failed.");
#endif
    gains_[slot] = static_cast<int>(gain * 255);
    loops_[slot] = loop;
    ends_[slot] = now() + std::uint64_t(data_.clips[clip].pcmBytes / 2) * 1000000 / 11025;
}
void RoomAudio::stop() {
    for (unsigned i = 0; i < 8; ++i) {
        if (channels_[i] >= 0) {
#ifdef __DREAMCAST__
            snd_sfx_stop(channels_[i]);
#else
            alSourceStop(channels_[i]);
#endif
        }
        loops_[i] = false; ends_[i] = 0;
    }
}
void RoomAudio::unload() {
    stop();
    for (auto& channel : channels_) if (channel >= 0) {
#ifdef __DREAMCAST__
        snd_sfx_chn_free(channel);
#else
        ALuint id = channel; alDeleteSources(1, &id);
#endif
        channel = -1;
    }
    for (auto& buffer : buffers_) if (buffer) {
#ifdef __DREAMCAST__
        snd_sfx_unload(buffer);
#else
        alDeleteBuffers(1, &buffer);
#endif
        buffer = 0;
    }
}
void RoomAudio::reset() {
    stop();
    for (unsigned i = 0; i < data_.sourceCount; ++i) {
        const auto& s = data_.sources[i]; play(i, s.clip, roomSoundGain(s, listener_), s.loop != 0);
    }
    memory("reset");
}
void RoomAudio::update(Vec3 listener) {
    listener_ = listener;
    for (unsigned i = 0; i < data_.sourceCount; ++i)
        if (busy(i)) volume(i, roomSoundGain(data_.sources[i], listener));
}
void RoomAudio::consume(AudioEvents events) {
    for (unsigned i = 0; i < events.count && i < 4; ++i) {
        const unsigned cue = static_cast<unsigned>(events.cues[i]);
        if (cue > 3 || data_.cues[cue].clip < 0) continue;
        const auto& binding = data_.cues[cue];
        const unsigned first = cue == 0 ? 2 : 3, end = cue == 0 ? 3 : 8;
        for (unsigned slot = first; slot < end; ++slot) if (!busy(slot)) {
            play(slot, binding.clip, binding.volume, false); ++played[cue]; break;
        }
    }
}
