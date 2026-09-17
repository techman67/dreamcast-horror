#include "PropData.h"
#include <cassert>
#include <cstdio>
#include <cstring>
#include <limits>

static void put(std::vector<unsigned char>& b, std::uint32_t v) {
    for(unsigned i=0;i<4;++i) b.push_back(static_cast<unsigned char>(v>>(i*8)));
}
static void scalar(std::vector<unsigned char>& b,float f) { std::uint32_t v; std::memcpy(&v,&f,4); put(b,v); }
int main() {
    LightEffectSettings s;
    s.mode=1; s.minimum=.2f;
    assert(validLightEffect(s));
    assert(std::fabs(sampleLightEffect(s,0,{})-.2f)<.00001f);
    assert(sampleLightEffect(s,.5f,{})==1);
    assert(std::fabs(sampleLightEffect(s,1,{})-.2f)<.00001f);
    s.mode=0; s.activationRange=3;
    assert(sampleLightEffect(s,0,{})==1);
    assert(sampleLightEffect(s,0,{3,0,0})==0);
    assert(std::fabs(sampleLightEffect(s,0,{2.75f,0,0})-.5f)<.00001f);
    assert(sampleLightEffect(s,0,{0,4,0})==0); // XYZ proximity, not XZ.
    s.mode=2; s.activationRange=0;
    float first=sampleLightEffect(s,0,{}); bool varied=false;
    for(unsigned i=0;i<256;++i) {
        float a=sampleLightEffect(s,static_cast<float>(i),{});
        assert(a>=s.minimum && a<=1 && a==sampleLightEffect(s,static_cast<float>(i),{}));
        varied |= a!=first;
    }
    assert(varied && sampleLightEffect(s,0,{})==first);
    assert(advanceLightPhase(255.9f,.1f,2)<.11f);
    assert(advanceLightPhase(0,10,2)==.2f); // Pause/hitch cap.
    assert(advanceLightPhase(5,0,2)==5);
    assert(advanceLightPhase(5,-1,2)==5);
    assert(advanceLightPhase(0,std::numeric_limits<float>::quiet_NaN(),2)==0);
    assert(blendLightColor(0xff001122,0xffaabbcc,0)==0xff001122);
    assert(blendLightColor(0xff001122,0xffaabbcc,255)==0xffaabbcc);
    assert(blendLightColor(0xff000000,0xffffffff,128)==0xff808080);
    s.frequency=std::numeric_limits<float>::infinity(); assert(!validLightEffect(s));
    std::vector<unsigned char> b;
    put(b,0x33504344); put(b,0); put(b,1); put(b,1); put(b,3); put(b,0xffffffff);
    for(unsigned i=0;i<3;++i) { for(unsigned k=0;k<5;++k) scalar(b,0); put(b,0xff000000); }
    const auto at=b.size();
    put(b,1); scalar(b,1); scalar(b,.2f); scalar(b,0);
    for(unsigned i=0;i<3;++i) scalar(b,0);
    put(b,1); put(b,2); put(b,0); put(b,0xffffffff); put(b,2); put(b,0xffabcdef);
    PropData data;
    assert(!parsePropData(b.data(),b.size(),data) && data.hasLightEffect && data.lightChanges.size()==2);
    for(std::size_t size=0;size<b.size();++size) {
        assert(parsePropData(b.data(),size,data)); assert(data.lightChanges.size()==2);
    }
    auto reject=[&](std::size_t offset,std::uint32_t value) {
        auto bad=b; for(unsigned i=0;i<4;++i) bad[offset+i]=static_cast<unsigned char>(value>>(8*i));
        assert(parsePropData(bad.data(),bad.size(),data));
    };
    reject(at,3); reject(at+4,0x7fc00000); reject(at+8,0xbf800000); reject(at+28,65536);
    reject(at+32,2049); reject(at+32,0); reject(at+36,3); reject(at+40,0x00ffffff); reject(at+44,0);
    b.push_back(0); assert(parsePropData(b.data(),b.size(),data));
    std::puts("Light effects passed: waves, XYZ activation, deterministic reset, phase bounds, byte blending, DCP3 malformed/truncated data and failure isolation.");
}
