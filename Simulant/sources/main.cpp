#include <simulant/simulant.h>
#include "RoomScene.h"
#include <cstdio>
#include <cstdlib>
#include <exception>

class RoomApp : public smlt::Application {
public:
    explicit RoomApp(const smlt::AppConfig& config) : smlt::Application(config) {}
    bool init() override {
        scenes->register_scene<RoomScene>("main");
        return true;
    }
};

int main(int, char**) {
    try {
        validateExportedRoom();
        smlt::AppConfig config;
        config.title = "Dreamcast Horror - exported room";
        config.width = 640;
        config.height = 480;
        config.fullscreen = false;
        config.development.force_renderer = "gl1x";
        if (std::getenv("SIMULANT_SETUP_CHECK")) config.target_frame_rate = 0;
        config.search_paths.push_back("assets");
        config.search_paths.push_back("/cd/assets");
        RoomApp app(config);
        return app.run();
    } catch (const std::exception& error) {
        std::fprintf(stderr, "Unable to run exported room: %s\n", error.what());
        return 1;
    }
}
