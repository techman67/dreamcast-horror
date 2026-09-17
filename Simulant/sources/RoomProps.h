#pragma once
#include <simulant/simulant.h>
#include "LightEffect.h"
#include <vector>

class RoomProps {
public:
    // True when exported geometry replaces collision proxies.
    bool load(smlt::AssetManager& assets, smlt::Stage& stage);
    void update(float dt, Vec3 listener);
    void reset(Vec3 listener);
    void clear();
    unsigned effectVertexCount() const { return effectVertices_; }
    unsigned effectUpdates() const { return updates_; }
    bool continuousEffect() const { return effect_.mode != 0 && effect_.minimum < 1 && effect_.activationRange == 0; }
private:
    struct Vertex { unsigned index; std::uint32_t off, on; };
    struct Part { smlt::MeshPtr mesh; std::vector<Vertex> vertices; };
    std::vector<Part> animated_;
    LightEffectSettings effect_{};
    float phase_ = 0, timer_ = 0;
    unsigned lastAmount_ = 256, effectVertices_ = 0, updates_ = 0;
    void apply(Vec3 listener);
};
