#pragma once
#include "Types.h"
#include <algorithm>
#include <cmath>
#include <cstdint>

// Portable presentation data. One stationary source blends baked off/on colors.
struct LightEffectSettings {
    std::uint32_t mode = 0; // Steady, pulse, deterministic stepped flicker.
    float frequency = 2, minimum = .25f, activationRange = 0;
    Vec3 position{};
    std::uint32_t seed = 1;
};
inline bool validLightEffect(const LightEffectSettings& s) {
    return s.mode <= 2 && std::isfinite(s.frequency) && s.frequency >= .1f && s.frequency <= 10 &&
        std::isfinite(s.minimum) && s.minimum >= 0 && s.minimum <= 1 &&
        std::isfinite(s.activationRange) && s.activationRange >= 0 && s.activationRange <= 100 &&
        s.seed <= 65535 && std::isfinite(s.position.x) && std::isfinite(s.position.y) && std::isfinite(s.position.z) &&
        std::fabs(s.position.x) <= 10000 && std::fabs(s.position.y) <= 10000 && std::fabs(s.position.z) <= 10000;
}
inline float advanceLightPhase(float phase, float dt, float frequency) {
    if (!std::isfinite(phase) || phase < 0 || phase >= 256) return 0;
    if (!std::isfinite(dt) || dt <= 0 || !std::isfinite(frequency) || frequency < .1f || frequency > 10) return phase;
    return std::fmod(phase + std::min(dt, .1f) * frequency, 256.0f);
}
inline float sampleLightEffect(const LightEffectSettings& s, float phase, Vec3 listener) {
    if (!validLightEffect(s) || !std::isfinite(phase) || phase < 0 || phase >= 256) return 0;
    float wave = 1;
    if (s.mode == 1) wave = 1 - std::fabs(2 * (phase - std::floor(phase)) - 1);
    if (s.mode == 2) {
        // Integer hash gives repeatable irregular values on PC and SH4.
        std::uint32_t bits = static_cast<std::uint32_t>(phase) + s.seed * 256u;
        bits ^= bits >> 16; bits *= 0x7feb352du; bits ^= bits >> 15; bits *= 0x846ca68bu; bits ^= bits >> 16;
        wave = static_cast<float>(bits & 255u) / 255.0f;
    }
    float amount = s.minimum + (1 - s.minimum) * wave;
    if (s.activationRange > 0) {
        const float x = listener.x - s.position.x, y = listener.y - s.position.y, z = listener.z - s.position.z;
        const float distance = std::sqrt(x*x + y*y + z*z);
        if (!std::isfinite(distance)) return 0;
        amount *= std::clamp((s.activationRange - distance) / .5f, 0.0f, 1.0f);
    }
    return amount;
}
inline std::uint32_t blendLightColor(std::uint32_t off, std::uint32_t on, unsigned amount) {
    amount = std::min(amount, 255u);
    std::uint32_t result = 0xff000000u;
    for (unsigned shift = 0; shift < 24; shift += 8) {
        unsigned a = (off >> shift) & 255, b = (on >> shift) & 255;
        result |= ((a * (255 - amount) + b * amount + 127) / 255) << shift;
    }
    return result;
}
