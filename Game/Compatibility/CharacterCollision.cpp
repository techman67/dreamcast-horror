#include "StaticCollisionBackend.h"
#include <algorithm>
#include <cmath>

namespace {
constexpr float Skin = 0.0001f;
Vec3 add(Vec3 a, Vec3 b) { return {a.x+b.x,a.y+b.y,a.z+b.z}; }
Vec3 subtract(Vec3 a, Vec3 b) { return {a.x-b.x,a.y-b.y,a.z-b.z}; }
float horizontal(Vec3 a) { return a.x*a.x+a.z*a.z; }

// Sweep feet against the Minkowski-expanded obstacle. Touching a face is
// permitted when travelling parallel to it or away from it.
bool sweep(Vec3 p, Vec3 d, const CollisionShape& box, float radius, float height,
           float& time, unsigned& axis, float& sign) {
    const float origin[]{p.x,p.y,p.z}, delta[]{d.x,d.y,d.z};
    const float minimum[]{box.center.x-box.halfExtents.x-radius,
        box.center.y-box.halfExtents.y-height,box.center.z-box.halfExtents.z-radius};
    const float maximum[]{box.center.x+box.halfExtents.x+radius,
        box.center.y+box.halfExtents.y,box.center.z+box.halfExtents.z+radius};
    float enter = -1e30f, leave = 1e30f;
    for (unsigned i=0;i<3;++i) {
        if (std::fabs(delta[i]) < 1e-9f) {
            if (origin[i] <= minimum[i]+Skin*.5f || origin[i] >= maximum[i]-Skin*.5f) return false;
        } else {
            float near = (minimum[i]-origin[i])/delta[i], far = (maximum[i]-origin[i])/delta[i];
            const float normal = delta[i] > 0 ? -1.0f : 1.0f;
            if (near > far) std::swap(near,far);
            // An allowed skin-depth overlap must still stop inward motion.
            if (near < 0 && std::fabs(near*delta[i]) <= Skin*1.1f) near=0;
            if (near > enter) { enter=near; axis=i; sign=normal; }
            leave=std::min(leave,far);
            if (enter > leave) return false;
        }
    }
    if (enter < -1e-6f || enter > 1 || leave < 0) return false;
    time=std::max(0.0f,enter);
    return true;
}
}

bool StaticCollisionBackend::clear3D(const Vec3& p) const {
    for (unsigned i=0;i<m_shapeCount;++i) {
        if (!isShapeEnabled(i)) continue;
        const auto& s=m_shapes[i];
        if (p.x > s.center.x-s.halfExtents.x-m_playerRadius+Skin &&
            p.x < s.center.x+s.halfExtents.x+m_playerRadius-Skin &&
            p.z > s.center.z-s.halfExtents.z-m_playerRadius+Skin &&
            p.z < s.center.z+s.halfExtents.z+m_playerRadius-Skin &&
            p.y > s.center.y-s.halfExtents.y-m_playerHeight+Skin &&
            p.y < s.center.y+s.halfExtents.y-Skin) return false;
    }
    return true;
}

DisplacementResult StaticCollisionBackend::slide3D(const Vec3& start, const Vec3& desired) const {
    Vec3 p=start, remaining=desired;
    DisplacementResult result{};
    for (unsigned contact=0;contact<4;++contact) {
        float earliest=1, normal=0; unsigned axis=0; bool hit=false;
        for (unsigned i=0;i<m_shapeCount;++i) {
            if (!isShapeEnabled(i)) continue;
            float t=0, n=0; unsigned a=0;
            if (sweep(p,remaining,m_shapes[i],m_playerRadius,m_playerHeight,t,a,n) && (!hit || t<earliest)) {
                earliest=t; axis=a; normal=n; hit=true;
            }
        }
        if (!hit) { p=add(p,remaining); break; }
        result.blocked=true;
        const float length=std::max({std::fabs(remaining.x),std::fabs(remaining.y),std::fabs(remaining.z),Skin});
        const float safe=std::max(0.0f,earliest-Skin/length);
        p=add(p,{remaining.x*safe,remaining.y*safe,remaining.z*safe});
        remaining={remaining.x*(1-safe),remaining.y*(1-safe),remaining.z*(1-safe)};
        if (axis==0) remaining.x=0;
        else if (axis==2) remaining.z=0;
        else { remaining.y=0; result.grounded |= normal>0; result.hitCeiling |= normal<0; }
        if (horizontal(remaining)+remaining.y*remaining.y < 1e-16f) break;
    }
    result.resolvedDelta=subtract(p,start);
    return result;
}

DisplacementResult StaticCollisionBackend::resolveMove3D(const Vec3& start, const Vec3& desired) const {
    for (float value : {start.x,start.y,start.z,desired.x,desired.y,desired.z})
        if (!std::isfinite(value) || std::fabs(value)>10000) return {{},true};
    // Invalid initial overlap cannot tunnel through a solid. Export rejects
    // overlapping spawns; runtime callers receive a blocked move.
    if (!clear3D(start)) return {{},true};
    const bool wasGrounded=slide3D(start,{0,-Skin*4,0}).grounded;
    auto vertical=slide3D(start,{0,desired.y,0});
    Vec3 p=add(start,vertical.resolvedDelta);
    const Vec3 sideways{desired.x,0,desired.z};
    auto walk=slide3D(p,sideways);
    Vec3 finish=add(p,walk.resolvedDelta);
    const bool canStep=(wasGrounded || vertical.grounded) && desired.y<=0;
    if (canStep && m_stepHeight>0 && walk.blocked && horizontal(sideways)>0) {
        auto up=slide3D(p,{0,m_stepHeight,0});
        if (!up.hitCeiling) {
            Vec3 raised=add(p,up.resolvedDelta);
            auto across=slide3D(raised,sideways);
            Vec3 over=add(raised,across.resolvedDelta);
            auto down=slide3D(over,{0,-m_stepHeight-Skin*4,0});
            Vec3 landing=add(over,down.resolvedDelta);
            if (down.grounded && clear3D(landing) &&
                horizontal(across.resolvedDelta)>horizontal(walk.resolvedDelta)+1e-10f &&
                landing.y-start.y <= m_stepHeight+Skin*2) {
                finish=landing; walk=across;
            }
        }
    }
    // Preserve the flat controller's gentle corner steering. Try only the
    // nearest tall obstacle corner, after stairs, with another full XYZ sweep.
    // Low treads are excluded so assistance cannot steer the player off stairs.
    if (walk.blocked && horizontal(sideways)>0) {
        float nearest=m_playerRadius*m_playerRadius*2.56f;
        Vec3 tangent{};
        for (unsigned i=0;i<m_shapeCount;++i) {
            if (!isShapeEnabled(i)) continue;
            const auto& s=m_shapes[i];
            if (s.center.y+s.halfExtents.y<=p.y+m_stepHeight+Skin ||
                s.center.y-s.halfExtents.y>=p.y+m_playerHeight-Skin) continue;
            const float dx=p.x-s.center.x, dz=p.z-s.center.z;
            if (std::fabs(dx)<=s.halfExtents.x || std::fabs(dz)<=s.halfExtents.z) continue;
            const float nx=dx-std::copysign(s.halfExtents.x,dx);
            const float nz=dz-std::copysign(s.halfExtents.z,dz);
            const float distance=nx*nx+nz*nz;
            const float dot=sideways.x*nx+sideways.z*nz;
            if (distance>1e-10f && distance<nearest && dot<0) {
                nearest=distance;
                tangent={sideways.x-dot*nx/distance,0,sideways.z-dot*nz/distance};
            }
        }
        if (horizontal(tangent)>0) {
            auto assisted=slide3D(p,tangent);
            if (horizontal(assisted.resolvedDelta)>horizontal(subtract(finish,p))+1e-10f)
                finish=add(p,assisted.resolvedDelta);
        }
    }
    // Follow descending steps, but never snap down a fall larger than one step.
    if (canStep) {
        auto down=slide3D(finish,{0,-m_stepHeight-Skin*4,0});
        if (down.grounded) finish=add(finish,down.resolvedDelta);
    }
    const bool grounded=slide3D(finish,{0,-Skin*4,0}).grounded;
    return {subtract(finish,start),vertical.blocked || walk.blocked,grounded,vertical.hitCeiling};
}
