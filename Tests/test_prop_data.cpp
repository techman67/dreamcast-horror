#include "PropData.h"
#include <cassert>
#include <cstdio>
#include <fstream>
#include <iterator>

static void put(std::vector<unsigned char>& data, unsigned value) {
    for (unsigned i = 0; i < 4; ++i) data.push_back(static_cast<unsigned char>(value >> (8*i)));
}
int main() {
    std::vector<unsigned char> bytes;
    put(bytes,0x31504344); put(bytes,0); put(bytes,1); put(bytes,3); put(bytes,0xffffffffu);
    for (unsigned i=0;i<3;++i) { for (unsigned f=0;f<5;++f) put(bytes,0); put(bytes,0xffffffffu); }
    PropData data;
    assert(!parsePropData(bytes.data(),bytes.size(),data));
    assert(data.parts.size()==1 && data.parts[0].vertices.size()==3 && data.textures.empty());
    for (std::size_t size=0;size<bytes.size();++size) {
        assert(parsePropData(bytes.data(),size,data));
        assert(data.parts.size()==1); // Failure must not replace the prior data.
    }
    auto reject = [&](unsigned offset, unsigned value) {
        auto bad=bytes;
        for (unsigned i=0;i<4;++i) bad[offset+i]=static_cast<unsigned char>(value>>(i*8));
        assert(parsePropData(bad.data(),bad.size(),data));
    };
    reject(0,0); reject(4,9); reject(8,33); reject(12,12291); reject(16,0);
    reject(20,0x7fc00000); reject(40,0x00ffffff); // NaN position and alpha.
    auto version2=bytes;
    version2[3]='2'; version2.insert(version2.begin()+12,{1,0,0,0});
    assert(!parsePropData(version2.data(),version2.size(),data) && data.includesStaticRoom);
    for (std::size_t size=12;size<version2.size();++size)
        assert(parsePropData(version2.data(),size,data));
    version2[12]=2; assert(parsePropData(version2.data(),version2.size(),data));
    version2[12]=0; assert(parsePropData(version2.data(),version2.size(),data));
    assert(!parsePropData(bytes.data(),bytes.size(),data) && !data.includesStaticRoom);
    bytes.push_back(0); assert(parsePropData(bytes.data(),bytes.size(),data));
    std::ifstream exported("Unity/Dreamcast-Horror/Assets/StreamingAssets/sample.props",std::ios::binary);
    if (exported) {
        std::vector<unsigned char> real((std::istreambuf_iterator<char>(exported)),{});
        assert(!parsePropData(real.data(),real.size(),data));
        unsigned vertices=0; for (const auto& p:data.parts) vertices+=static_cast<unsigned>(p.vertices.size());
        assert(vertices<=MaxPropVertices);
        if (!data.textures.empty()) { real[20]='.'; assert(parsePropData(real.data(),real.size(),data)); }
    }
    std::puts("Static props passed: bounded parser, malformed/truncated records, finite values, failure isolation and real export.");
}
