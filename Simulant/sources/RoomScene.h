#pragma once
#include <simulant/simulant.h>
#include "RoomData.h"
#include "StaticCollisionBackend.h"
#include "RoomInput.h"
#include "RoomAudio.h"
#include "RoomProps.h"
#include "SaveData.h"

// Reject bad room data before constructing the rendering runtime.
void validateExportedRoom();

class RoomScene : public smlt::Scene {
public:
    explicit RoomScene(smlt::Window* window);
    ~RoomScene() override;
    void on_load() override;
    void on_update(float dt) override;
    void on_unload() override;
private:
    smlt::ui::Label* fade_=nullptr;
    float fadeAmount_=0;
    bool departing_=false;
    void reset();
    void saveAction(bool load);
    SavePoints savePoints_;
    smlt::ui::Label* saveStatus_=nullptr;
    std::string saveMessage_,lastSaveText_;
    float saveMessageSeconds_=0;
    void present();
    smlt::Actor* shape(const CollisionShape& data, const smlt::MaterialPtr& material);
    void checkStep();
    RoomData room_{};
    StaticCollisionBackend collision_{};
    RoomInput controls_{};
    RoomAudio audio_{};
    RoomProps props_{};
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
