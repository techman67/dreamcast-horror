#include "PropData.h"
#include <cmath>
#include <cstring>
#include <utility>

const char* parsePropData(const unsigned char* bytes, std::size_t size, PropData& output) {
    if (!bytes || size < 12 || size > MaxPropFileBytes) return "Missing or oversized static prop data.";
    std::size_t at = 0;
    auto word = [&]() { unsigned v = 0; for (unsigned i = 0; i < 4; ++i) v |= unsigned(bytes[at++]) << (8*i); return v; };
    auto scalar = [&]() { unsigned bits = word(); float v; std::memcpy(&v, &bits, 4); return v; };
    const unsigned magic = word();
    if (magic != 0x31504344 && magic != 0x32504344) return "Expected DCP1 or DCP2 static visual data.";
    unsigned textures = word(), parts = word(), textureBytes = 0, vertices = 0;
    if (textures > MaxPropTextures || parts > MaxPropParts) return "Static prop texture/part budget exceeded.";
    PropData data;
    if (magic == 0x32504344) {
        if (size - at < 4) return "Truncated static visual flags.";
        const unsigned flags = word();
        if (flags != 1) return "Invalid static visual flags.";
        data.includesStaticRoom = true;
    }
    for (unsigned i = 0; i < textures; ++i) {
        if (size - at < 44) return "Truncated prop texture record.";
        PropTexture t; t.width = word(); t.height = word();
        auto dimension = [](unsigned n) { return n >= 8 && n <= 256 && (n & (n - 1)) == 0; };
        if (!dimension(t.width) || !dimension(t.height)) return "Prop textures must be power-of-two, 8 to 256 pixels.";
        t.file.assign(reinterpret_cast<const char*>(bytes + at), 36); at += 36;
        for (unsigned c = 0; c < 32; ++c)
            if (!((t.file[c] >= '0' && t.file[c] <= '9') || (t.file[c] >= 'a' && t.file[c] <= 'f'))) return "Invalid prop texture filename.";
        if (t.file.substr(32) != ".rgb") return "Invalid prop texture extension.";
        for (const auto& existing : data.textures) if (existing.file == t.file) return "Duplicate prop texture.";
        textureBytes += t.width * t.height * 2;
        if (textureBytes > MaxPropTextureBytes) return "Prop textures exceed 512 KiB.";
        data.textures.push_back(t);
    }
    std::vector<bool> used(textures, false);
    for (unsigned i = 0; i < parts; ++i) {
        if (size - at < 8) return "Truncated prop part header.";
        unsigned count = word(), texture = word();
        if (!count || count % 3 || count > MaxPropVertices - vertices ||
            (texture != 0xffffffffu && texture >= textures) || size - at < std::size_t(count) * 24)
            return "Invalid prop vertices, texture reference or triangle budget.";
        vertices += count;
        PropPart part; part.texture = texture == 0xffffffffu ? -1 : static_cast<int>(texture);
        if (part.texture >= 0) used[part.texture] = true;
        part.vertices.reserve(count);
        for (unsigned v = 0; v < count; ++v) {
            PropVertex vertex{{scalar(), scalar(), scalar()}, scalar(), scalar(), word()};
            for (float f : {vertex.position.x, vertex.position.y, vertex.position.z, vertex.u, vertex.v})
                if (!std::isfinite(f) || std::fabs(f) > 10000) return "Invalid prop position or UV coordinate.";
            if ((vertex.color >> 24) != 255) return "Only opaque prop colors are supported.";
            part.vertices.push_back(vertex);
        }
        data.parts.push_back(std::move(part));
    }
    for (bool referenced : used) if (!referenced) return "Unreferenced prop texture.";
    if (at != size) return "Unexpected trailing prop data.";
    output = std::move(data); return nullptr;
}
