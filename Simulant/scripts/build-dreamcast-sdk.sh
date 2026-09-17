#!/usr/bin/env bash
set -euo pipefail
install_dir="$HOME/.local/share/dreamcast-horror"
engine="$install_dir/engine-source"
revision=4a5f5799d0232c1a509b186262a214da0f138894
image=kazade/dreamcast-sdk@sha256:7202a5d5d007bcb7802c48f1bf30b22d03e337927543b5396130f8420c6f37ec
if [[ ! -d "$engine/.git" ]]; then
    git clone https://gitlab.com/simulant/simulant.git "$engine"
    git -C "$engine" checkout --detach "$revision"
fi
if [[ "$(git -C "$engine" rev-parse HEAD)" != "$revision" ]]; then
    echo "Engine checkout differs from the verified revision; leaving it untouched." >&2
    exit 1
fi
git -C "$engine" submodule update --init --recursive
# Current KOS names this syscall_dcload_detected (declared in dc/fs_dcload.h).
# Keep the compatibility patch explicit and limited to the external SDK source.
python3 - "$engine/simulant/platforms/dreamcast/profiler.c" <<'PY'
from pathlib import Path
import sys
p = Path(sys.argv[1])
text = p.read_text()
p.write_text(text.replace('fs_dcload_detected', 'syscall_dcload_detected'))
PY
docker run --rm -v "$engine:/src" \
    -v "$install_dir/venv/share/simulant-tools/toolchains:/toolchains:ro" \
    -w /src "$image" bash -lc '
    source /etc/bash.bashrc
    cmake -S . -B build/dreamcast-cli -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_TOOLCHAIN_FILE=/toolchains/Dreamcast.cmake \
        -DSIMULANT_BUILD_SAMPLES=OFF -DSIMULANT_BUILD_TESTS=OFF &&
    cmake --build build/dreamcast-cli -j 8'
python3 - "$engine" "$install_dir/setup-check/dreamcast-sdk" <<'PY'
from pathlib import Path
import shutil, sys
engine, sdk = map(Path, sys.argv[1:])
for source in (engine / 'simulant').rglob('*', recurse_symlinks=True):
    if source.is_file() and source.suffix in {'.h', '.hpp', '.inc', '.inl'}:
        target = sdk / 'include' / source.relative_to(engine)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
(sdk / 'lib').mkdir(parents=True, exist_ok=True)
shutil.copy2(engine / 'build/dreamcast-cli/simulant/libsimulant.a', sdk / 'lib/libsimulant.a')
PY
echo "Matching Dreamcast SDK ready: $install_dir/setup-check/dreamcast-sdk"
