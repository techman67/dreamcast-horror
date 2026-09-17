#pragma once
#include <simulant/simulant.h>
#include "World.h"
#include <fstream>
#include <cstdio>
#include <stdexcept>
#include <cstdlib>
#ifdef __DREAMCAST__
#include <malloc.h>
#include <dc/sound/sound.h>
#include <dc/pvr.h>
#else
#include <malloc.h>
#endif

inline unsigned& liveRoomScenes() { static unsigned count=0; return count; }
inline std::string roomAsset(const std::string& name) {
    const auto* room=world_room();
    const std::string relative=room ? "world/"+std::to_string(room->id)+"/"+name : name;
    std::string path="assets/"+relative;
    return std::ifstream(path) ? path : "/cd/"+path;
}
inline void roomMemory(const char* phase) {
#ifdef __DREAMCAST__
    std::printf("ROOM MEMORY %s: heap=%u AICA=%u VRAM=%u\n",phase,unsigned(mallinfo().uordblks),unsigned(snd_mem_available()),unsigned(pvr_mem_available()));
#else
    std::printf("ROOM MEMORY %s: heap=%zu\n",phase,static_cast<std::size_t>(mallinfo2().uordblks));
#endif
    std::fflush(stdout);
}
inline void loadWorldManifest() {
    world_clear();
    if(std::getenv("SIMULANT_SETUP_CHECK") || std::getenv("DREAMCAST_ROOM_FILE")) return;
    std::ifstream f(roomAsset("sample.world"),std::ios::binary|std::ios::ate);
    if(!f) return; // Single-room exports remain supported.
    auto size=f.tellg(); if(size<12 || size>4096) throw std::runtime_error("Invalid world manifest size.");
    unsigned char bytes[4096]; f.seekg(0); f.read(reinterpret_cast<char*>(bytes),size);
    if(!f) throw std::runtime_error("Cannot read world manifest.");
    if(const char* error=world_configure(bytes,static_cast<std::size_t>(size))) throw std::runtime_error(error);
}

class RoomLoadingScene final : public smlt::Scene {
    unsigned frames_=0;
public:
    explicit RoomLoadingScene(smlt::Window* w):smlt::Scene(w) {}
    void on_load() override {
        auto stage=create_child<smlt::Stage>(); auto camera=create_child<smlt::Camera2D>();
        camera->set_orthographic_projection(0,640,0,480);
        auto layer=compositor->create_layer(stage,camera); layer->viewport->set_color(smlt::Color::black()); layer->set_clear_flags(smlt::BUFFER_CLEAR_ALL); layer->activate();
        auto text=stage->create_child<smlt::ui::Label>("Loading room..."); text->transform->set_position_2d(smlt::Vec2(100,40)); text->set_text_color(smlt::Color::white());
    }
    void on_update(float dt) override {
        smlt::Scene::on_update(dt);
        // SceneManager destroys the old scene after late_update. Wait through
        // that boundary, then load on a later frame; never overlap room assets.
        if(++frames_!=3) return;
        if(liveRoomScenes()!=0) throw std::runtime_error("Previous room is still alive; refusing overlapping room allocation.");
        app->shared_assets->run_garbage_collection();
        roomMemory("old room released");
        scenes->activate("main");
    }
};
