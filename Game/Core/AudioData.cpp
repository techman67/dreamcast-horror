#include "AudioData.h"
#include <cmath>
#include <cstring>
#include <locale>
#include <sstream>
#include <string>

namespace {
bool key(std::istream& in, const char* word) { std::string s; return (in >> s) && s == word; }
bool scalar(std::istream& in, float& x) { return (in >> x) && std::isfinite(x); }
bool gain(std::istream& in, float& x) { return scalar(in, x) && x >= 0 && x <= 1; }
}
const char* parseAudioData(const char* text, AudioData& output) {
    if (!text || std::strlen(text) > 8192) return "Missing or oversized audio manifest.";
    std::istringstream in(text); in.imbue(std::locale::classic());
    AudioData data{};
    unsigned version = 0, bytes = 0;
    if (!key(in, "dreamcast_audio") || !(in >> version) || (version != 1 && version != 2) ||
        !key(in, "clips") || !(in >> data.clipCount) || data.clipCount > MaxAudioClips)
        return "Expected dreamcast_audio 1 or 2 with at most eight clips.";
    for (unsigned i = 0; i < data.clipCount; ++i) {
        unsigned id = 0; std::string name;
        auto& clip = data.clips[i];
        if (!key(in, "clip") || !(in >> id >> clip.pcmBytes >> name) || id != i ||
            clip.pcmBytes < 16 || clip.pcmBytes > 65534 * 2 || clip.pcmBytes % 2 ||
            name.size() != 36 || name.substr(32) != ".wav")
            return "Invalid audio clip; expected a 32-hex SHA256-prefix-named PCM WAV, at most 65534 samples.";
        for (unsigned c = 0; c < 32; ++c)
            if (!((name[c] >= '0' && name[c] <= '9') || (name[c] >= 'a' && name[c] <= 'f')))
                return "Invalid audio asset filename.";
        for (unsigned j = 0; j < i; ++j)
            if (name == data.clips[j].file) return "Duplicate audio asset.";
        std::memcpy(clip.file, name.c_str(), name.size() + 1);
        bytes += clip.pcmBytes;
    }
    if (bytes > 512 * 1024) return "Audio bank exceeds 512 KiB of PCM samples.";
    bool referenced[MaxAudioClips]{};
    for (unsigned i = 0; i < 4; ++i) {
        unsigned id = 0; auto& cue = data.cues[i];
        if (!key(in, "cue") || !(in >> id >> cue.clip) || id != i || cue.clip < -1 ||
            cue.clip >= static_cast<int>(data.clipCount) || !gain(in, cue.volume))
            return "Invalid audio cue assignment or volume.";
        if (cue.clip >= 0) referenced[cue.clip] = true;
    }
    if (!key(in, "sources") || !(in >> data.sourceCount) || data.sourceCount > MaxRoomSounds)
        return "At most two placed room sounds are supported.";
    for (unsigned i = 0; i < data.sourceCount; ++i) {
        auto& s = data.sources[i];
        if (!key(in, "source") || !(in >> s.clip >> s.loop) || s.clip >= data.clipCount || s.loop > 1 ||
            !gain(in, s.volume) || !scalar(in, s.position.x) || !scalar(in, s.position.y) ||
            !scalar(in, s.position.z) || !scalar(in, s.range) || s.range < 0 || s.range > 10000)
            return "Invalid placed sound, volume, loop or range.";
        if (version == 2 && (!scalar(in, s.fullVolumeRange) || s.fullVolumeRange < 0 ||
            (s.range == 0 ? s.fullVolumeRange != 0 : s.fullVolumeRange >= s.range)))
            return "Full-volume distance must be nonnegative and smaller than audible range (zero for room-wide sound).";
        referenced[s.clip] = true;
    }
    for (unsigned i = 0; i < data.clipCount; ++i)
        if (!referenced[i]) return "Audio manifest contains an unreferenced clip.";
    if (!key(in, "end")) return "Expected end of audio manifest.";
    in >> std::ws;
    if (!in.eof()) return "Unexpected trailing audio data.";
    output = data; return nullptr;
}
float roomSoundGain(const RoomSoundData& s, Vec3 p) {
    if (s.range == 0) return s.volume;
    const float d = std::hypot(std::hypot(p.x - s.position.x, p.y - s.position.y), p.z - s.position.z);
    if (d <= s.fullVolumeRange) return s.volume;
    return s.volume * std::fmax(0.0f, (s.range - d) / (s.range - s.fullVolumeRange));
}
