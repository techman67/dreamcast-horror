#include "RoomScene.h"
#include "Game.h"
#include "RoomProps.h"
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
    std::ifstream stream(overridePath ? overridePath : "assets/sample.room", std::ios::binary);
    if (!stream && !overridePath) stream = std::ifstream("/cd/assets/sample.room", std::ios::binary);
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

void RoomScene::on_load() {
    const std::string text = roomText();
    if (const char* error = parseRoomData(text.c_str(), room_)) throw std::runtime_error(error);
    require(collision_.configure(room_.shapes, room_.shapeCount, room_.bindings.playerCollisionRadius, room_.bindings.playerHeight, room_.bindings.playerStepHeight),
            "Invalid room collision configuration.");
    if (room_.bindings.keyDoor.doorActor.id)
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
    const bool staticRoomExported = loadRoomProps(*assets, *world_);
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
    audio_.load(this, world_);
    reset();
    std::printf("SIMULANT ROOM: loaded %u exported shapes\n", room_.shapeCount);
    std::fflush(stdout);
}

void RoomScene::reset() {
    game_init(room_.bindings, &collision_);
    audio_.update(game_get_player_pos());
    audio_.reset();
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
    if (checking_) checkStep();
#endif
    const InputFrame frame = controls_.read(*input->state);
    if (controls_.quitPressed) app->stop_running();
    if (controls_.restartPressed) reset();
    else game_step(&frame, checking_ ? 1.0f / 60.0f : std::min(dt, 0.1f));
    audio_.update(game_get_player_pos());
    audio_.consume(game_take_audio_events());
    present();
}

void RoomScene::on_unload() {
    audio_.unload();
    captureConnection_.disconnect();
    game_init(SliceBindings{}, nullptr);
}
