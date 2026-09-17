#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$repo/Simulant/build"
docker run --rm -v "$repo:/project" -w /project \
  kazade/dreamcast-sdk@sha256:7202a5d5d007bcb7802c48f1bf30b22d03e337927543b5396130f8420c6f37ec \
  bash -lc 'source /opt/toolchains/dc/kos/environ.sh &&
    /opt/toolchains/dc/kos/utils/build_wrappers/kos-c++ -std=c++17 -D__DREAMCAST__ -fno-fast-math \
      -I Game/Core -I Simulant/sources Tests/vmu_save_probe.cpp Game/Core/SaveData.cpp \
      -o Simulant/build/vmu-probe.elf &&
    mkdcdisc -e Simulant/build/vmu-probe.elf -n SaveProbe -s IND-SAVT01 -N \
      --allow-overwrite -o Simulant/build/vmu-probe.cdi'
