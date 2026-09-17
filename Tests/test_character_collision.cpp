#include <cassert>
#include <cmath>
#include <cstdio>
#include <limits>
#include "StaticCollisionBackend.h"
#include "Game.h"
#include "Player.h"

static bool near(float a,float b) { return std::fabs(a-b)<0.002f; }
static CollisionShape box(Vec3 p,Vec3 half) { CollisionShape b{}; b.center=p; b.halfExtents=half; return b; }
static void apply(Vec3& p,DisplacementResult r) { p.x+=r.resolvedDelta.x;p.y+=r.resolvedDelta.y;p.z+=r.resolvedDelta.z; }
int main() {
    StaticCollisionBackend backend;
    CollisionShape shapes[10]{};
    shapes[0]=box({0,-.1f,0},{10,.1f,10});
    assert(backend.configure(shapes,1,.35f,1.8f,.3f));
    auto move=[&](Vec3 p,Vec3 d){return backend.resolveMove({1},p,d);};
    auto r=move({0,4,0},{0,-100,0});
    assert(r.grounded && near(r.resolvedDelta.y,-4));
    r=move({0,0,0},{1,-.01f,1});
    assert(r.grounded && near(r.resolvedDelta.x,1) && near(r.resolvedDelta.z,1));
    shapes[1]=box({0,3,0},{2,.1f,2});
    assert(backend.configure(shapes,2,.35f,1.8f,.3f));
    r=move({0,0,0},{0,100,0});
    assert(r.hitCeiling && near(r.resolvedDelta.y,1.1f));
    r=move({-4,0,0},{8,0,0}); // Walk beneath a raised platform.
    assert(near(r.resolvedDelta.x,8));
    r=move({0,4,0},{0,-100,0});
    assert(r.grounded && near(r.resolvedDelta.y,-.9f));
    shapes[1]=box({2,1.5f,0},{.1f,1.5f,4});
    assert(backend.configure(shapes,2,.35f,1.8f,.3f));
    r=move({0,0,0},{100,0,1});
    assert(r.blocked && near(r.resolvedDelta.x,1.55f) && near(r.resolvedDelta.z,1));
    r=move({1.55005f,0,0},{1,0,0});
    assert(r.blocked && near(r.resolvedDelta.x,0));
    r=move({2,0,0},{100,0,0}); // Initial overlap fails closed.
    assert(r.blocked && near(r.resolvedDelta.x,0));
    for(int i=0;i<6;++i) shapes[i+1]=box({0,.125f*(i+1),.5f+i*.5f},{1,.125f*(i+1),.25f});
    shapes[7]=box({0,1.4f,4},{1,.1f,1});
    assert(backend.configure(shapes,8,.35f,1.8f,.3f));
    Vec3 p{0,0,-1};
    for(int i=0;i<300;++i) { r=move(p,{0,-.003f,1.0f/60}); apply(p,r); assert(r.grounded); }
    assert(near(p.y,1.5f) && near(p.z,4));
    for(int i=0;i<300;++i) { r=move(p,{0,-.003f,-1.0f/60}); apply(p,r); assert(r.grounded); }
    assert(near(p.y,0) && near(p.z,-1));
    // A ledge drop must become airborne, not snap all the way to the floor.
    p={0,1.5001f,4}; r=move(p,{2,-.003f,0}); apply(p,r);
    assert(!r.grounded && p.y>1.49f);
    // Airborne characters cannot step onto the next tread.
    r=move({0,.1f,-.2f},{0,-.001f,.3f});
    assert(r.blocked && r.resolvedDelta.z<.11f && r.resolvedDelta.y<0);
    // Insufficient headroom stops step-up, even when the tread is low enough.
    shapes[2]=box({0,1.95f,.5f},{2,.1f,2});
    assert(backend.configure(shapes,3,.35f,1.8f,.3f));
    r=move({0,0,-.3f},{0,-.003f,.4f});
    assert(r.blocked && r.resolvedDelta.z<.21f && near(r.resolvedDelta.y,0));
    shapes[1]=box({0,.2f,.5f},{1,.2f,.25f});
    assert(backend.configure(shapes,2,.35f,1.8f,.3f));
    r=move({0,0,-.3f},{0,-.003f,.4f});
    assert(r.blocked && near(r.resolvedDelta.y,0)); // 40 cm exceeds 30 cm step.
    shapes[1]=box({0,.15f,.5f},{1,.15f,.25f});
    assert(backend.configure(shapes,2,.35f,1.8f,.3f));
    r=move({0,0,-.3f},{0,-.003f,.4f});
    assert(near(r.resolvedDelta.y,.3f) && near(r.resolvedDelta.z,.4f));
    // Repeated diagonal approach slides safely at an inside wall corner.
    shapes[1]=box({2,1.5f,0},{.1f,1.5f,4});
    shapes[2]=box({0,1.5f,2},{4,1.5f,.1f});
    assert(backend.configure(shapes,3,.35f,1.8f,.3f));
    p={0,0,0};
    for(int i=0;i<200;++i) { apply(p,move(p,{.02f,-.003f,.02f})); assert(p.x<1.551f && p.z<1.551f); }
    assert(near(p.x,1.55f) && near(p.z,1.55f));
    // Straight input near a tall outside corner still gently steers around it.
    shapes[1]=box({0,1.5f,0},{1,1.5f,1});
    assert(backend.configure(shapes,2,.35f,1.8f,.3f));
    r=move({-1.3501f,0,-1.2f},{.02f,-.003f,0});
    assert(r.resolvedDelta.z < -.001f && near(r.resolvedDelta.y,0));
    // The same corner overhead must not steer a player walking beneath it.
    shapes[1].center.y=4;
    assert(backend.configure(shapes,2,.35f,1.8f,.3f));
    r=move({-1.3501f,0,-1.2f},{.02f,-.003f,0});
    assert(near(r.resolvedDelta.x,.02f) && near(r.resolvedDelta.z,0));
    shapes[1].type=CollisionShapeType::Sphere;
    assert(!backend.configure(shapes,2,.35f,1.8f,.3f));
    assert(!backend.configure(shapes,1,.35f,.4f,.3f));
    assert(!backend.configure(shapes,1,.35f,1.8f,.6f));
    assert(!backend.configure(shapes,1,.35f,std::numeric_limits<float>::quiet_NaN(),.3f));
    assert(backend.configure(shapes,1,.35f,1.8f,.3f));
    // Frame-rate independent bounded gravity and reset, tested through gameplay.
    SliceBindings bindings{}; bindings.playerActor={1};bindings.playerHeight=1.8f;
    bindings.initialPlayerPose.position={0,3,0};
    InputFrame input{}; input.move={1,0};
    game_init(bindings,&backend);
    game_step(&input,1.0f/60);
    assert(game_get_player_pos().y<3 && game_take_audio_events().count==0);
    for(int i=1;i<120;++i) game_step(&input,1.0f/60);
    auto sixty=game_get_player_pos();
    game_init(bindings,&backend);
    for(int i=0;i<60;++i) game_step(&input,1.0f/30);
    auto thirty=game_get_player_pos();
    assert(near(sixty.x,thirty.x) && near(sixty.y,0) && near(thirty.y,0));
    game_init(bindings,&backend); game_step(&input,10);
    assert(game_get_player_pos().x<=.101f && game_get_player_pos().y>2.9f);
    game_init(bindings,&backend); game_step(&input,0);
    assert(near(game_get_player_pos().y,3));
    bindings.initialPlayerPose.position={0,0,0};
    bindings.keyDoor.keyActor={2}; bindings.keyDoor.doorActor={3};
    bindings.keyDoor.keyPosition={0,4,0}; bindings.keyDoor.keyRange=1;
    bindings.keyDoor.doorPosition={0,4,0}; bindings.keyDoor.doorRange=1;
    game_init(bindings,&backend);
    input.move={0,0}; input.interactPressed=true; game_step(&input,1.0f/60);
    assert(!(game_get_key_door_view().flags & HasKey));
    assert(game_get_key_door_view().prompt==InteractionPrompt::None);
    // Jump across the authored one-metre gap to a 30 cm higher platform.
    shapes[1]=box({0,1.4f,4},{1,.1f,1});
    shapes[2]=box({3,1.7f,3},{1,.1f,1});
    assert(backend.configure(shapes,3,.35f,1.8f,.3f));
    bindings.keyDoor={}; bindings.initialPlayerPose.position={.85f,1.5001f,3.5f};
    game_init(bindings,&backend);
    input={{1,0},false,true}; game_step(&input,1.0f/60);
    assert(game_get_player_pos().y>1.5f && game_take_audio_events().count==0);
    input.jumpPressed=false;
    for(int i=0;i<59;++i) game_step(&input,1.0f/60);
    assert(game_get_player_pos().x>1.8f && near(game_get_player_pos().y,1.8f));
    // Repeated airborne presses cannot gain altitude or reset falling speed.
    assert(backend.configure(shapes,1,.35f,1.8f,.3f));
    bindings.initialPlayerPose.position={0,0,0};
    game_init(bindings,&backend); input={{0,0},false,true}; game_step(&input,1.0f/60);
    for(int i=0;i<20;++i) game_step(&input,1.0f/60);
    float repeated=game_get_player_pos().y;
    game_init(bindings,&backend); game_step(&input,1.0f/60); input.jumpPressed=false;
    for(int i=0;i<20;++i) game_step(&input,1.0f/60);
    assert(near(repeated,game_get_player_pos().y));
    // Ceiling impact cancels upward velocity and returns to the floor.
    shapes[1]=box({0,2.1f,0},{2,.1f,2});
    assert(backend.configure(shapes,2,.35f,1.8f,.3f));
    game_init(bindings,&backend); input.jumpPressed=true; game_step(&input,1.0f/60); input.jumpPressed=false;
    for(int i=0;i<90;++i) { game_step(&input,1.0f/60); assert(game_get_player_pos().y<=.201f); }
    assert(near(game_get_player_pos().y,0));
    std::puts("3D CHARACTER COLLISION AND GRAVITY CHECKS PASSED");
}
