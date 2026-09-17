#include "RoomScene.h"
#include "RoomLoading.h"
#include "Game.h"
#include "RoomProps.h"
#include "RoomSaveStorage.h"
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <stdexcept>
#include <string>
#ifndef __DREAMCAST__
#include <SDL_opengl.h>
#include <vector>
#endif

namespace {
// Exported coordinates use +Z forward. Simulant cameras face local -Z.
smlt::Vec3 position(Vec3 p) { return smlt::Vec3(p.x, p.y, -p.z); }

std::string roomText() {
    const char* overridePath = std::getenv("DREAMCAST_ROOM_FILE");
    std::ifstream stream(overridePath ? overridePath : roomAsset("sample.room"), std::ios::binary);

    if (!stream) throw std::runtime_error("sample.room is missing; export the room in Unity first.");
    std::string text;
    char buffer[1024];
    while (stream.read(buffer, sizeof(buffer)) || stream.gcount()) {
        text.append(buffer, static_cast<std::size_t>(stream.gcount()));
        if (text.size() > 65536) throw std::runtime_error("Room file exceeds the 64 KiB startup limit.");
    }
    if (stream.bad()) throw std::runtime_error("Unable to read room file.");
    return text;
}
const char* prompt(InteractionPrompt value) {
    switch (value) {
    case InteractionPrompt::TakeKey: return "E / A: Take brass key";
    case InteractionPrompt::LockedDoor: return "E / A: Try locked door";
    case InteractionPrompt::UnlockDoor: return "E / A: Unlock door";
    case InteractionPrompt::OpenDoor: return "Door open. Walk through to escape.";
    case InteractionPrompt::Complete: return "ROOM ESCAPED - R / Start to replay";
    default: return "Find key. Unlock the south door.";
    }
}
const char* feedback(SliceFeedback value) {
    switch (value) {
    case SliceFeedback::DoorLocked: return "Locked. Find the brass key.";
    case SliceFeedback::KeyTaken: return "Brass key collected.";
    case SliceFeedback::DoorUnlocked: return "The key fits. Door opened.";
    case SliceFeedback::Completed: return "Slice complete.";
    default: return "";
    }
}
void require(bool value, const char* message) {
    if (!value) {
        std::fprintf(stderr, "%s\n", message);
        throw std::runtime_error(message);
    }
}
}

void validateExportedRoom() {
    const std::string text = roomText();
    RoomData candidate{};
    if (const char* error = parseRoomData(text.c_str(), candidate)) throw std::runtime_error(error);
}

smlt::Actor* RoomScene::shape(const CollisionShape& data, const smlt::MaterialPtr& material) {
    auto mesh = assets->create_mesh(smlt::VertexSpecification::DEFAULT);
    switch (data.type) {
    case CollisionShapeType::Box:
        mesh->create_submesh_as_box("box", material, data.halfExtents.x * 2,
                                   data.halfExtents.y * 2, data.halfExtents.z * 2);
        break;
    case CollisionShapeType::Sphere:
        mesh->create_submesh_as_sphere("sphere", material, data.radius * 2, 12, 8);
        break;
    case CollisionShapeType::Capsule:
        // Simulant takes the straight cylinder length; the export stores total height.
        if (data.height <= data.radius * 2)
            mesh->create_submesh_as_sphere("capsule", material, data.radius * 2, 12, 8);
        else
            mesh->create_submesh_as_capsule("capsule", material, data.radius * 2,
                                           data.height - data.radius * 2, 12, 1, 4);
        break;
    }
    // Fixed-function unlit rendering takes its tint from vertex colors.
    mesh->find_submesh_with_material(material)->set_base_color(material->base_color());
    auto actor = world_->create_child<smlt::Actor>(mesh);
    require(actor != nullptr, "Simulant could not create a room shape actor.");
    actor->transform->set_position(position(data.center));
    return actor;
}

RoomScene::RoomScene(smlt::Window* w):smlt::Scene(w) { ++liveRoomScenes(); }
RoomScene::~RoomScene() { --liveRoomScenes(); }

void RoomScene::on_load() {
    require(liveRoomScenes()==1,"Overlapping room scenes are forbidden.");
    roomMemory("before room load");
    const std::string text = roomText();
    if (const char* error = parseRoomData(text.c_str(), room_)) throw std::runtime_error(error);
    if(world_active()) require(saveHash(text.data(),text.size())==world_room()->hash,"Room content does not match world manifest.");
    require(collision_.configure(room_.shapes, room_.shapeCount, room_.bindings.playerCollisionRadius, room_.bindings.playerHeight, room_.bindings.playerStepHeight),
            "Invalid room collision configuration.");
    if (room_.bindings.keyDoor.doorActor.id)
        if(world_active()) require(saveHash(text.data(),text.size())==world_room()->hash,"Room content does not match world manifest.");
    require(collision_.configureDoor(room_.bindings.keyDoor.doorActor, room_.bindings.keyDoor.doorShapeIndex),
                "Invalid exported door collision binding.");
    world_ = create_child<smlt::Stage>();
    require(world_ != nullptr, "Simulant could not create the world stage.");
    camera_ = create_child<smlt::Camera3D>();
    require(camera_ != nullptr, "Simulant could not create the gameplay camera.");
    // FOV is host presentation: version 2 exports camera poses, not lens settings.
    camera_->set_perspective_projection(smlt::Degrees(65),
        float(window->width()) / float(window->height()), 0.1f, 100.0f);
    auto layer = compositor->create_layer(world_, camera_);
    layer->viewport->set_color(smlt::Color(0.07f, 0.08f, 0.10f, 1));
    layer->set_clear_flags(smlt::BUFFER_CLEAR_ALL);
    layer->activate();
    auto material = [&](smlt::Color color) {
        auto result = assets->clone_default_material();
        result->set_lighting_enabled(false);
        result->set_base_color(color);
        return result;
    };
    const auto gray = material(smlt::Color(0.48f, 0.48f, 0.46f, 1));
    const auto brown = material(smlt::Color(0.40f, 0.22f, 0.12f, 1));
    const auto gold = material(smlt::Color(1.0f, 0.72f, 0.10f, 1));
    const auto blue = material(smlt::Color(0.12f, 0.40f, 0.95f, 1));
    const auto floorMaterial = material(smlt::Color(0.22f, 0.25f, 0.28f, 1));
    float minX = room_.bindings.initialPlayerPose.position.x - 1, maxX = minX + 2;
    float minZ = room_.bindings.initialPlayerPose.position.z - 1, maxZ = minZ + 2;
    const bool staticRoomExported = props_.load(*assets, *world_);
    for (unsigned i = 0; i < room_.shapeCount; ++i) {
        const auto& data = room_.shapes[i];
        const bool door = room_.bindings.keyDoor.doorActor.id && i == room_.bindings.keyDoor.doorShapeIndex;
        if (door) door_ = shape(data, brown);
        else if (!staticRoomExported) shape(data, gray);
        const float x = data.type == CollisionShapeType::Box ? data.halfExtents.x : data.radius;
        const float z = data.type == CollisionShapeType::Box ? data.halfExtents.z : data.radius;
        minX = std::min(minX, data.center.x - x); maxX = std::max(maxX, data.center.x + x);
        minZ = std::min(minZ, data.center.z - z); maxZ = std::max(maxZ, data.center.z + z);
    }
    if (room_.bindings.keyDoor.keyActor.id) {
        CollisionShape key{};
        key.center = room_.bindings.keyDoor.keyPosition;
        key.halfExtents = {0.16f, 0.05f, 0.35f};
        key_ = shape(key, gold);
        minZ = std::min(minZ, room_.bindings.keyDoor.exitBoundaryZ - 1);
    }
    // Presentation-only ground plane: flat movement is already a core rule.
    CollisionShape floor{};
    floor.center = {(minX + maxX) * 0.5f, room_.bindings.initialPlayerPose.position.y - 0.1f, (minZ + maxZ) * 0.5f};
    floor.halfExtents = {(maxX - minX) * 0.5f, 0.1f, (maxZ - minZ) * 0.5f};
    if (!staticRoomExported && room_.bindings.playerHeight == 0) shape(floor, floorMaterial);
    CollisionShape player{};
    player.type = CollisionShapeType::Capsule;
    player.radius = room_.bindings.playerCollisionRadius;
    player.height = room_.bindings.playerHeight > 0 ? room_.bindings.playerHeight : 1.8f;
    player_ = shape(player, blue);
    auto ui = create_child<smlt::Stage>();
    require(ui != nullptr, "Simulant could not create the HUD stage.");
    auto uiCamera = create_child<smlt::Camera2D>();
    require(uiCamera != nullptr, "Simulant could not create the HUD camera.");
    uiCamera->set_orthographic_projection(0, 640, 0, 480);
    auto overlay = compositor->create_layer(ui, uiCamera, 1);
    overlay->set_clear_flags(0);
    overlay->activate();
    status_ = ui->create_child<smlt::ui::Label>("Loading exported room");
    require(status_ != nullptr, "Simulant could not create the HUD label.");
    status_->set_anchor_point(0, 1);
    status_->transform->set_position_2d(smlt::Vec2(12, 440));
    status_->set_text_color(smlt::Color::white());
    status_->set_background_color(smlt::Color(0.03f, 0.03f, 0.04f, 0.9f));
    status_->set_padding(8);
    auto help = ui->create_child<smlt::ui::Label>("Move: WASD / arrows / stick / D-pad | R / Start: reset");
    require(help != nullptr, "Simulant could not create the controls label.");
    help->set_anchor_point(0, 1);
    help->transform->set_position_2d(smlt::Vec2(12, 468));
    help->set_text_color(smlt::Color::white());
    help->set_background_color(smlt::Color(0.03f, 0.03f, 0.04f, 0.9f));
    help->set_padding(5);
#ifndef __DREAMCAST__
    checking_ = std::getenv("SIMULANT_SETUP_CHECK") != nullptr;
    const bool snapshot = std::getenv("SIMULANT_ROOM_SNAPSHOT") != nullptr;
    if (checking_ || snapshot) captureConnection_ = app->signal_pre_swap().connect([this, snapshot, frames = 0]() mutable {
        const char* filename = nullptr;
        if (snapshot && ++frames == 30) filename = "room-snapshot.ppm";
        if (checkStage_ == 0 && checkFrames_ == 10) filename = "room-check-start.ppm";
        if (props_.effectVertexCount() && checkStage_ == 0 && checkFrames_ == 25) filename = "room-effect-late.ppm";
        if (checkStage_ == 11 && checkFrames_ == 5) filename = "room-check-key.ppm";
        if (checkStage_ == 17) filename = "room-check-exit.ppm";
        if (!filename) return;
        const int w = window->width(), h = window->height();
        std::vector<unsigned char> pixels(static_cast<std::size_t>(w) * h * 3);
        glPixelStorei(GL_PACK_ALIGNMENT, 1);
        glReadPixels(0, 0, w, h, GL_RGB, GL_UNSIGNED_BYTE, pixels.data());
        std::ofstream file(filename, std::ios::binary);
        file << "P6\n" << w << ' ' << h << "\n255\n";
        for (int y = h - 1; y >= 0; --y)
            file.write(reinterpret_cast<const char*>(pixels.data() + y * w * 3), w * 3);
        if (snapshot) app->stop_running();
    });
#endif
    saveStatus_=ui->create_child<smlt::ui::Label>("L / Y: load latest save");
    require(saveStatus_!=nullptr,"Unable to create save status.");
    saveStatus_->set_anchor_point(0,0); saveStatus_->transform->set_position_2d(smlt::Vec2(12,12));
    saveStatus_->set_text_color(smlt::Color::white()); saveStatus_->set_background_color(smlt::Color(0.03f,0.03f,0.04f,.9f)); saveStatus_->set_padding(5);
    auto sourceRoom=roomText(); savePoints_.room=saveHash(sourceRoom.data(),sourceRoom.size());
    const char* overridePath=std::getenv("DREAMCAST_ROOM_FILE");
    std::ifstream saveFile(overridePath ? std::string(overridePath)+".saves" : roomAsset("sample.saves"),std::ios::binary|std::ios::ate);

    if(saveFile) {
        auto size=saveFile.tellg(); require(size>=12 && size<=1100,"Invalid save-point manifest size.");
        unsigned char bytes[1100]{}; saveFile.seekg(0); saveFile.read(reinterpret_cast<char*>(bytes),size);
        require(bool(saveFile),"Unable to read save points."); SavePoints points;
        if(const char* error=parseSavePoints(bytes,static_cast<std::size_t>(size),points)) throw std::runtime_error(error);
        require(points.room==savePoints_.room,"Save points do not match room; export again."); savePoints_=points;
    }
    if(world_active()) {
        auto fadeStage=create_child<smlt::Stage>();
        auto fadeLayer=compositor->create_layer(fadeStage,uiCamera,10); fadeLayer->set_clear_flags(0); fadeLayer->activate();
        fade_=fadeStage->create_child<smlt::ui::Label>(""); fade_->resize(smlt::ui::Px(640),smlt::ui::Px(480));
        fade_->set_anchor_point(0,0); fade_->transform->set_position_2d(smlt::Vec2(0,0));
        fadeAmount_=1; fade_->set_background_color(smlt::Color(0,0,0,1));
    }
    audio_.load(this, world_);
    reset();
    if(world_active()) { require(world_enter(),"Unable to enter room arrival or restore progress."); cameraId_=0; audio_.update(game_get_player_pos()); props_.reset(game_get_player_pos()); present(); }
    roomMemory("room loaded");
    std::printf("SIMULANT ROOM: loaded %u exported shapes\n", room_.shapeCount);
    std::fflush(stdout);
}

void RoomScene::reset() {
    game_init(room_.bindings, &collision_);
    game_set_save_points(&savePoints_);
    saveMessage_.clear(); saveMessageSeconds_=0;
    audio_.update(game_get_player_pos());
    audio_.reset();
    props_.reset(game_get_player_pos());
    cameraId_ = 0;
    present();
}

void RoomScene::present() {
    Vec3 p = game_get_player_pos(); p.y += room_.bindings.playerHeight > 0 ? room_.bindings.playerHeight * 0.5f : 0.9f;
    player_->transform->set_position(position(p));
    const unsigned id = game_get_camera_ref().id;
    if (id != cameraId_) {
        cameraId_ = id;
        const Pose pose = game_get_camera_pose();
        const float sy = std::sin(pose.yaw), cy = std::cos(pose.yaw);
        const float sp = std::sin(pose.pitch), cp = std::cos(pose.pitch);
        const float sr = std::sin(pose.roll), cr = std::cos(pose.roll);
        const Vec3 forward{sy * cp, -sp, cy * cp};
        const Vec3 up{-cy * sr + sy * sp * cr, cp * cr, sy * sr + cy * sp * cr};
        const auto origin = position(pose.position);
        camera_->transform->set_position(origin);
        camera_->transform->look_at(origin + position(forward), position(up));
        std::printf("SIMULANT ROOM: camera %u\n", id);
    }
    const KeyDoorView view = game_get_key_door_view();
    if (key_) key_->set_visible(!(view.flags & HasKey));
    if (door_) door_->set_visible(!(view.flags & DoorOpen));
    if (view.flags != lastView_.flags || view.prompt != lastView_.prompt || view.feedback != lastView_.feedback) {
        std::string text;
        if (view.flags & ObjectiveEnabled) {
            text += (view.flags & HasKey) ? "Inventory: brass key\n" : "Inventory: empty\n";
            text += prompt(view.prompt); text += "\n"; text += feedback(view.feedback);
        } else if (room_.bindings.playerHeight > 0) text += "3D stairs test: W up, S down. Walk off landing to fall.\nWASD: move | Space / B: jump | R / Start: reset";
        else text += "Exploring exported room (no key-door objective).";
        status_->set_text(text);
        lastView_ = view;
    }
}

void RoomScene::on_update(float dt) {
    smlt::Scene::on_update(dt);
#ifndef __DREAMCAST__
    const bool worldCheck=std::getenv("SIMULANT_WORLD_CHECK")!=nullptr;
    if(worldCheck) dt=1.0f/60.0f;
#endif
#ifndef __DREAMCAST__
    if (checking_) checkStep();
#endif
    const InputFrame frame = controls_.read(*input->state);
    if(fade_) {
        if(departing_) {
            fadeAmount_=std::min(1.0f,fadeAmount_+std::max(dt,0.0f)/.25f);
            fade_->set_background_color(smlt::Color(0,0,0,fadeAmount_));
            if(fadeAmount_>=1) scenes->activate("loading");
            return;
        }
        if(fadeAmount_>0) { fadeAmount_=std::max(0.0f,fadeAmount_-dt/.25f); fade_->set_background_color(smlt::Color(0,0,0,fadeAmount_)); return; }
#ifndef __DREAMCAST__
        if(worldCheck && ++totalFrames_==20) {
            static unsigned trips=0;
            if(trips==12) { std::puts("SIMULANT WORLD CHECK PASSED: 12 scene transitions; previous RoomScene destroyed before every destination allocation."); std::fflush(stdout); app->stop_running(); return; }
            require(world_room()->linkCount>0,"World check needs a linked room.");
            SaveProgress p; require(game_capture_progress(&p),"World check could not capture progress."); p.position=world_room()->links[0].position;
            require(game_restore_progress(&p),"World check could not position player at link.");
            require(world_depart(true),"World check could not activate link."); ++trips; departing_=true; return;
        }
#endif
        if(world_depart(frame.interactPressed)) { departing_=true; return; }
    }
    if (controls_.quitPressed) app->stop_running();
    if (controls_.restartPressed && world_active()) { world_restart(); departing_=true; return; }
    if (controls_.restartPressed) reset();
    else game_step(&frame, checking_ ? 1.0f / 60.0f : std::min(dt, 0.1f));
    if(controls_.loadPressed) saveAction(true);
    else if(game_take_save_request()>=0) saveAction(false);
    if(world_pending()) { departing_=true; return; }
    saveMessageSeconds_=std::max(0.0f,saveMessageSeconds_-dt);
    int near=game_near_save_point();
    std::string saveText=saveMessageSeconds_>0 ? saveMessage_ : near>=0 ? std::string("E / A: ")+savePoints_.points[near].prompt : "";
    if(world_near_link()>=0 && saveMessageSeconds_<=0) saveText="E / A: Enter room";
    saveText+="\nL / Y: load latest save";
    if(saveText!=lastSaveText_) { saveStatus_->set_text(saveText); lastSaveText_=saveText; }
    audio_.update(game_get_player_pos());
    audio_.consume(game_take_audio_events());
    props_.update(checking_ ? 1.0f / 60.0f : dt, game_get_player_pos());
    present();
}

void RoomScene::on_unload() {
    if (checking_ && props_.effectVertexCount()) {
        if (props_.continuousEffect()) require(props_.effectUpdates() > 1, "Light effect did not animate during room playback.");
        std::printf("SIMULANT LIGHT EFFECT CHECK PASSED: %u vertices, %u runtime color changes excluding resets.\n",props_.effectVertexCount(),props_.effectUpdates());
    }
    props_.clear();
    audio_.unload();
    captureConnection_.disconnect();
    game_init(SliceBindings{}, nullptr);
}

void RoomScene::saveAction(bool load) {
    RoomSaveStorage storage; SaveProgress progress;
    SaveResult result;
    if(world_active()) result=load ? world_load(storage) : world_save(storage);
    else if(load) {
        result=readProgress(storage,savePoints_.room,progress);
        if(result==SaveResult::Ok) {
            if(!game_restore_progress(&progress)) result=SaveResult::Invalid;
            else { cameraId_=0; audio_.reset(); props_.reset(game_get_player_pos()); }
        }
    } else result=game_capture_progress(&progress) ? writeProgress(storage,savePoints_.room,progress) : SaveResult::NotReady;
    saveMessage_=load && result==SaveResult::Ok ? "Saved progress loaded." : saveResultText(result); saveMessageSeconds_=5;
    std::printf("SAVE: %s\n",saveMessage_.c_str()); std::fflush(stdout);
}
