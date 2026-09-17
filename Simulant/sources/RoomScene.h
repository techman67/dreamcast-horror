#pragma once
#include <simulant/simulant.h>
#include "RoomData.h"
#include "StaticCollisionBackend.h"
#include "RoomInput.h"
#include "RoomAudio.h"

// Reject bad room data before constructing the rendering runtime.
void validateExportedRoom();

class RoomScene : public smlt::Scene {
public:
    explicit RoomScene(smlt::Window* window) : smlt::Scene(window) {}
    void on_load() override;
    void on_update(float dt) override;
    void on_unload() override;
private:
    void reset();
    void present();
    smlt::Actor* shape(const CollisionShape& data, const smlt::MaterialPtr& material);
    void checkStep();
    RoomData room_{};
    StaticCollisionBackend collision_{};
    RoomInput controls_{};
    RoomAudio audio_{};
    smlt::Stage* world_ = nullptr;
    smlt::Camera3D* camera_ = nullptr;
    smlt::Actor* player_ = nullptr;
    smlt::Actor* key_ = nullptr;
    smlt::Actor* door_ = nullptr;
    smlt::ui::Label* status_ = nullptr;
    KeyDoorView lastView_{~0u, InteractionPrompt::None, SliceFeedback::None};
    unsigned cameraId_ = 0;
    unsigned checkStage_ = 0, checkFrames_ = 0, totalFrames_ = 0;
    bool checking_ = false;
    smlt::sig::connection captureConnection_;
};
