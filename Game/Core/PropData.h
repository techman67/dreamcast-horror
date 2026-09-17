#pragma once
#include "Types.h"
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

constexpr unsigned MaxPropParts = 32, MaxPropVertices = 12288, MaxPropTextures = 8;
constexpr unsigned MaxPropTextureBytes = 524288, MaxPropFileBytes = 300000;
struct PropTexture { unsigned width, height; std::string file; };
struct PropVertex { Vec3 position; float u, v; std::uint32_t color; };
struct PropPart { int texture; std::vector<PropVertex> vertices; };
struct PropData { bool includesStaticRoom = false; std::vector<PropTexture> textures; std::vector<PropPart> parts; };
const char* parsePropData(const unsigned char* bytes, std::size_t size, PropData& output);
