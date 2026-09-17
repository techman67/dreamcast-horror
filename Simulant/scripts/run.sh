#!/usr/bin/env bash
set -euo pipefail
source_dir="$(cd "$(dirname "$0")/.." && pwd)"
install_dir="$HOME/.local/share/dreamcast-horror"
project_dir="$install_dir/setup-check"
cli="$install_dir/venv/bin/simulant"
if [[ ! -x "$cli" || ! -d "$project_dir/libraries" ]]; then
    echo "Simulant is not installed in this Ubuntu account. See Simulant/README.md." >&2
    exit 1
fi
# Stage only this setup project's tracked sources, keeping SDK downloads on Linux.
mkdir -p "$project_dir/sources" "$project_dir/assets"
python3 "$source_dir/scripts/stage_room.py" \
    "$source_dir/../Unity/Dreamcast-Horror/Assets/StreamingAssets" "$project_dir/assets"
for file in "$source_dir"/sources/*.cpp "$source_dir"/sources/*.h; do
    dest="$project_dir/sources/$(basename "$file")"
    cmp -s "$file" "$dest" || cp "$file" "$dest"
done
for dir in Core Compatibility Gameplay; do
    mkdir -p "$project_dir/Game/$dir"
    for file in "$source_dir/../Game/$dir"/*.cpp "$source_dir/../Game/$dir"/*.h; do
        [[ -f "$file" ]] || continue
        dest="$project_dir/Game/$dir/$(basename "$file")"
        cmp -s "$file" "$dest" || cp "$file" "$dest"
    done
done
cmp -s "$source_dir/CMakeLists.txt" "$project_dir/CMakeLists.txt" || cp "$source_dir/CMakeLists.txt" "$project_dir/CMakeLists.txt"
cmp -s "$source_dir/simulant.json" "$project_dir/simulant.json" || cp "$source_dir/simulant.json" "$project_dir/simulant.json"
cd "$project_dir"
target=linux
case "${1:-run}" in dreamcast|package) target=dreamcast ;; esac
"$cli" build "$target" --release
binary="$project_dir/build/linux-x64-gcc11/release/setup-check"
case "${1:-run}" in
    check)
        SIMULANT_SETUP_CHECK=1 timeout 90s "$binary"
        mkdir -p "$source_dir/build"
        cp "$project_dir"/room-check-*.ppm "$source_dir/build/" ;;
    run) exec "$binary" ;;
    build) echo "Linux build ready: $binary" ;;
    dreamcast|package)
        mkdir -p "$source_dir/build"
        cp "$project_dir/build/dreamcast-sh4-gcc/release/setup-check.elf" "$source_dir/build/setup-check.elf"
        mkdir -p "$source_dir/build/assets"
        python3 "$source_dir/scripts/stage_room.py" "$project_dir/assets" "$source_dir/build/assets"
        echo "Dreamcast build ready: $source_dir/build/setup-check.elf"
        if [[ "${1:-run}" == package ]]; then
            # -d preserves the assets directory itself, giving /cd/assets at runtime.
            mkdir -p "$project_dir/packages"
            docker run --rm --user "$(id -u):$(id -g)" \
                -v "$project_dir:/project" \
                kazade/dreamcast-sdk@sha256:7202a5d5d007bcb7802c48f1bf30b22d03e337927543b5396130f8420c6f37ec \
                mkdcdisc -e /project/build/dreamcast-sh4-gcc/release/setup-check.elf \
                -d /project/assets -n "Dreamcast Horror Sample" \
                -N --allow-overwrite -o /project/packages/dreamcast-horror.cdi
            python3 "$source_dir/scripts/check_disc_audio.py" "$project_dir/packages/dreamcast-horror.cdi" "$project_dir/assets/sample.audio"
            disc_dir="$source_dir/../FlyCast"
            mkdir -p "$disc_dir"
            cp "$project_dir/packages/dreamcast-horror.cdi" "$disc_dir/dreamcast-horror.cdi"
            echo "Disc image ready: $disc_dir/dreamcast-horror.cdi"
        fi ;;
    *) echo "Unknown action" >&2; exit 2 ;;
esac
