#pragma once
#include "Types.h"
#include "LightEffect.h"
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

constexpr unsigned MaxPropParts = 32, MaxPropVertices = 12288, MaxPropTextures = 8;
constexpr unsigned MaxPropTextureBytes = 524288, MaxPropFileBytes = 300000;
constexpr unsigned MaxLightEffectVertices = 2048;
struct PropTexture { unsigned width, height; std::string file; };
struct PropVertex { Vec3 position; float u, v; std::uint32_t color; };
struct PropPart { int texture; std::vector<PropVertex> vertices; };
struct PropLightChange { unsigned vertex; std::uint32_t onColor; };
struct PropData {
    bool includesStaticRoom = false, hasLightEffect = false;
    std::vector<PropTexture> textures; std::vector<PropPart> parts;
    LightEffectSettings lightEffect{};
    std::vector<PropLightChange> lightChanges;
};
const char* parsePropData(const unsigned char* bytes, std::size_t size, PropData& output);
