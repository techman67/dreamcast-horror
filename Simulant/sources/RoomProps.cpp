#include "RoomProps.h"
#include "PropData.h"
#include <cstdio>
#include <fstream>
#include <stdexcept>
#include <vector>

namespace {
std::vector<unsigned char> read(const std::string& name, unsigned limit) {
    std::ifstream stream("assets/" + name, std::ios::binary | std::ios::ate);
    if (!stream) stream = std::ifstream("/cd/assets/" + name, std::ios::binary | std::ios::ate);
    if (!stream || stream.tellg() < 0 || stream.tellg() > std::streamoff(limit))
        throw std::runtime_error("Missing/oversized prop asset: " + name + ". Re-export the room.");
    std::vector<unsigned char> bytes(static_cast<std::size_t>(stream.tellg()));
    stream.seekg(0); stream.read(reinterpret_cast<char*>(bytes.data()), bytes.size());
    if (!stream) throw std::runtime_error("Cannot read prop asset: " + name);
    return bytes;
}
unsigned word(const unsigned char* p) { return unsigned(p[0]) | unsigned(p[1]) << 8 | unsigned(p[2]) << 16 | unsigned(p[3]) << 24; }
}
bool loadRoomProps(smlt::AssetManager& assets, smlt::Stage& stage) try {
    auto file = read("sample.props", MaxPropFileBytes);
    PropData props;
    if (const char* error = parsePropData(file.data(), file.size(), props)) throw std::runtime_error(error);
    std::vector<smlt::TexturePtr> textures;
    unsigned textureBytes = 0, triangles = 0;
    for (const auto& t : props.textures) {
        auto bytes = read("prop-textures/" + t.file, 12 + t.width * t.height * 2);
        if (bytes.size() != 12 + t.width*t.height*2 || word(bytes.data()) != 0x31544344 ||
            word(bytes.data()+4) != t.width || word(bytes.data()+8) != t.height)
            throw std::runtime_error("Invalid RGB565 prop texture: " + t.file);
        auto texture = assets.create_texture(t.width,t.height,smlt::TEXTURE_FORMAT_RGB_1US_565);
        texture->set_mipmap_generation(smlt::MIPMAP_GENERATE_NONE);
        texture->set_texture_filter(smlt::TEXTURE_FILTER_BILINEAR);
        texture->set_data(bytes.data()+12, bytes.size()-12);
        textures.push_back(texture); textureBytes += t.width*t.height*2;
    }
    for (const auto& part : props.parts) {
        auto material = assets.clone_default_material();
        material->set_lighting_enabled(false);
        material->set_blend_func(smlt::BLEND_NONE);
        material->set_cull_mode(smlt::CULL_MODE_BACK_FACE);
        if (part.texture >= 0) material->set_base_color_map(textures[part.texture]);
        auto mesh = assets.create_mesh(smlt::VertexSpecification::DEFAULT);
        for (const auto& v : part.vertices) {
            mesh->vertex_data->position(v.position.x,v.position.y,-v.position.z);
            mesh->vertex_data->tex_coord0(v.u,v.v);
            mesh->vertex_data->color(smlt::Color((v.color & 255)/255.0f, ((v.color>>8) & 255)/255.0f,
                ((v.color>>16) & 255)/255.0f, 1.0f));
            mesh->vertex_data->move_next();
        }
        mesh->vertex_data->done();
        auto sub = mesh->create_submesh("prop",material,smlt::INDEX_TYPE_16_BIT);
        // Reflection into Simulant's -Z-forward space reverses triangle winding.
        for (unsigned i = 0; i < part.vertices.size(); i += 3) {
            sub->index_data->index(i); sub->index_data->index(i+2); sub->index_data->index(i+1);
        }
        sub->index_data->done();
        if (!stage.create_child<smlt::Actor>(mesh)) throw std::runtime_error("Unable to create static prop actor.");
        triangles += part.vertices.size()/3;
    }
    std::printf("PROPS: %u triangles, %u parts, %u RGB565 texture bytes\n",triangles,unsigned(props.parts.size()),textureBytes);
    return props.includesStaticRoom;
} catch (const std::exception& error) {
    std::printf("PROP LOAD FAILED: %s\n",error.what()); std::fflush(stdout); throw;
}
