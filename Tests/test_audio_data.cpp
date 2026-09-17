#include "AudioData.h"
#include <cassert>
#include <cmath>
#include <cstdio>
#include <string>

int main() {
    static_assert(sizeof(AudioClipData) == 76 && sizeof(AudioCueData) == 8 && sizeof(RoomSoundData) == 32, "Audio ABI changed");
    const std::string file(32, 'a');
    const std::string valid = "dreamcast_audio 1\nclips 1\nclip 0 88200 " + file +
        ".wav\ncue 0 0 0.5\ncue 1 -1 1\ncue 2 0 1\ncue 3 -1 1\nsources 1\nsource 0 1 0.8 1 0 0 4\nend\n";
    AudioData data{};
    assert(parseAudioData(valid.c_str(), data) == nullptr);
    assert(data.clipCount == 1 && data.sourceCount == 1 && data.cues[1].clip == -1);
    assert(std::fabs(roomSoundGain(data.sources[0], {1,0,0}) - .8f) < .0001f);
    assert(std::fabs(roomSoundGain(data.sources[0], {3,0,0}) - .4f) < .0001f);
    assert(roomSoundGain(data.sources[0], {6,0,0}) == 0);
    auto fail = [&](std::string from, std::string to) {
        auto text = valid; text.replace(text.find(from), from.size(), to);
        assert(parseAudioData(text.c_str(), data) != nullptr);
        assert(data.clipCount == 1 && data.clips[0].pcmBytes == 88200); // Failed parse is transactional.
    };
    fail("clips 1", "clips 9"); fail("88200", "131070"); fail("88200", "17");
    fail(file, "../escape"); fail(file, std::string(64, 'a')); // Old names were truncated on CDI.
    fail("cue 0 0", "cue 0 1"); fail("cue 0 0", "cue 0 -2");
    fail("sources 1", "sources 3"); fail("source 0 1", "source 0 2");
    fail("0.8", "nan"); fail("0.8", "1.01"); fail("0 0 4", "0 0 -1");
    fail("end", "end junk"); fail("audio 1", "audio 3");
    auto v2 = valid;
    v2.replace(v2.find("audio 1"), 7, "audio 2");
    v2.replace(v2.find("0 0 4\n"), 6, "0 0 4 2\n");
    assert(parseAudioData(v2.c_str(), data) == nullptr);
    const auto sound = data.sources[0];
    assert(sound.fullVolumeRange == 2);
    assert(std::fabs(roomSoundGain(sound, {1,0,0}) - .8f) < .0001f);
    assert(std::fabs(roomSoundGain(sound, {3,0,0}) - .8f) < .0001f); // Inner edge.
    assert(std::fabs(roomSoundGain(sound, {4,0,0}) - .4f) < .0001f); // Halfway through fade.
    assert(roomSoundGain(sound, {5,0,0}) == 0 && roomSoundGain(sound, {6,0,0}) == 0);
    assert(std::fabs(roomSoundGain(sound, {1,3,0}) - .4f) < .0001f); // Height counts too.
    for (const char* bad : {"4 4", "4 5", "4 -1", "4 nan", "0 2"}) {
        auto text = v2; text.replace(text.find("4 2\n"), 3, bad);
        assert(parseAudioData(text.c_str(), data) != nullptr);
        assert(data.sources[0].fullVolumeRange == 2);
    }
    assert(parseAudioData(valid.c_str(), data) == nullptr && data.sources[0].fullVolumeRange == 0);
    assert(parseAudioData(nullptr, data));
    std::string oversized = "dreamcast_audio 1\nclips 5\n";
    for (int i = 0; i < 5; ++i) oversized += "clip " + std::to_string(i) + " 131068 " + std::string(32, char('a'+i)) + ".wav\n";
    assert(parseAudioData(oversized.c_str(), data));
    const char* silent = "dreamcast_audio 1 clips 0 cue 0 -1 1 cue 1 -1 1 cue 2 -1 1 cue 3 -1 1 sources 0 end";
    assert(parseAudioData(silent, data) == nullptr && data.clipCount == 0);
    std::puts("Audio manifest passed: ABI, references, filenames, budgets, gains and failure isolation.");
}
