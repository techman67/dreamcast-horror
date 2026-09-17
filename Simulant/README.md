# Simulant development setup

This host loads the exported room, audio and static visuals, and runs the same C++ movement, collision, cameras and key-door rules
as Unity. Unity remains the authoring environment; shared gameplay stays in `Game/`.

From the repository root in Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\Simulant\run.ps1
powershell -ExecutionPolicy Bypass -File .\Simulant\run.ps1 check
powershell -ExecutionPolicy Bypass -File .\Simulant\run.ps1 dreamcast
```

The default command builds and opens the room in a 640x480 window through WSLg.
Move with WASD/arrows,
the left stick or D-pad; use E / controller A to interact. Find the gold key marker,
unlock the south door and walk through. R / Start resets; Escape or closing the
window quits. Physical controller forwarding from Windows into WSL is not configured
by this project; keyboard input works without it.

The room now includes original placeholder footsteps, key/door effects and a quiet
ambient hum. Shared C++ emits the events; host audio uses eight bounded slots and
the current sample uses 258,736 bytes of PCM, including its placed AC hum. Assign and place sounds with the Dreamcast
components in Unity, then use **Dreamcast > Export room and build CDI**. The build
collects `sample.audio` and only its referenced exported WAVs; it no longer uses
the hard-coded source bank. `package` builds the last export without Unity.
See [audio authoring, validation and memory accounting](../Audio/README.md).

`check` exercises synthetic keyboard/controller mappings, then plays the current
sample's full route through normal updates and checks reset. It writes three PPM
frame captures under `Simulant/build/`. This fixed route is a test fixture for the
checked-in sample; after redesigning its layout, update the test route too.

`dreamcast` builds an ELF containing the same shared core, and copies the room to
`Simulant/build/assets/sample.room`. It is not a hardware/emulator test or bootable
disc image. Use `package` to build a bootable CDI containing the executable,
exported room and staged engine assets under `/cd/assets`.

## Flycast

With Flycast installed at `FlyCast/flycast.exe`, build, package and launch with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Simulant\flycast.ps1
```

Alternatively, run `Simulant/run.ps1 package` and open
`FlyCast/dreamcast-horror.cdi` in Flycast. The packaging command uses the
pinned Dreamcast SDK's `mkdcdisc`, including its boot-prerequisite checks. It disables
disc padding for a smaller emulator test image. Engine assets are on disc, not all
loaded into RAM at startup. Emulator files, settings and saves stay outside Git.

Use Flycast's controller mappings for Dreamcast D-pad/stick movement, A to interact
and Start to reset. Desktop E/R bindings only apply when a keyboard is exposed to
the game; a Windows keyboard mapped to an emulated controller uses Flycast's mappings.
This computer has `FlyCast/mappings/SDL_Keyboard.cfg` installed from
`Simulant/config/SDL_Keyboard.cfg`: WASD maps to the stick, arrows to the D-pad,
E to A, Space to B (jump), R to Start, and Tab opens the emulator menu. To install it on another
machine, close Flycast and copy the supplied mapping to its `mappings` directory;
back up any existing keyboard mapping first. The launcher preserves user mappings.

Keep standard Dreamcast memory settings when testing. A successful package/build
does not establish correct emulated rendering or hardware performance; test the
locked door, key pickup, camera changes, unlocked exit and reset in the emulator.

## Installed on this computer

- Ubuntu WSL2, account `administrator`, with WSLg.
- CMake, C++ build tools, SDL2, OpenAL, OpenGL, Python virtual environment and Docker.
- Simulant CLI: `~/.local/bin/simulant` in Ubuntu.
- Tools, downloads and builds: `~/.local/share/dreamcast-horror/` in Ubuntu.
- Staged project: `~/.local/share/dreamcast-horror/setup-check/`.
- Dreamcast ELF: copied to `Simulant/build/setup-check.elf` in this Windows repository.

The wrapper copies Simulant sources, shared `Game/` sources, and the existing Unity
StreamingAssets `sample.room` into the staged project before building. Export changes
using Unity's **Dreamcast > Export sample room**, then rerun the command. It does not
export or change the Unity scene itself. Edit repository files, not staged copies.
SDK downloads and build products are deliberately outside Git.

## Presentation and portability

- Simulant receives input intent and presents snapshots; it does not decide inventory,
  collision, interaction distance, camera transitions or victory.
- The host reflects Z for rendering, including camera forward/up vectors. The core
  continues to use the exported coordinate convention without conversion.
- Camera poses come from the file. The host uses a 65-degree lens and 640x480 HUD;
  version 2 does not export lens settings. Camera roll is supported by the conversion.
- Fixed-function `gl1x` rendering and vertex colors avoid shader-specific dependencies.
  Sphere/capsule proxies use bounded low-resolution meshes. The blue player
  and gold key marker are presentation proxies. Static room surfaces and props use the
  separate [mesh/texture export pipeline](../Docs/static-props.md).
- Legacy prop-only exports generate a floor spanning collision extents and exit;
  DCP2 uses the authored static floor and baked vertex lighting. No Unity physics or
  Simulant physics service is used: `StaticCollisionBackend` remains authoritative.
- Startup rejects missing, invalid or over-64-KiB room files. Set `DREAMCAST_ROOM_FILE`
  inside Ubuntu for an explicit alternative file. Version 1 remains exploration-only.
- Shared core compilation disables fast-math even though the Dreamcast rendering
  toolchain enables it, preserving finite-value validation and collision semantics.

## If the taskbar entry exists but the window is invisible

On this computer WSLg once failed its shared-memory display connection. Its Windows
window title contained `[WARN:COPY MODE]`, and `/mnt/wslg/weston.log` reported
`rdp_allocate_shared_memory` with `Input/output error`. The game still rendered
frames internally, so passing screenshot/playthrough checks did not prove Windows
was displaying the window. A WSL restart restored `use_gfxredir = 1` and removed
the warning; changing the game's fullscreen setting did not fix it.

After saving work in any Linux sessions, run `wsl --shutdown` from PowerShell,
then launch the room again. This stops all WSL processes, including Docker;
the configured Docker service starts again with Ubuntu. Do not reset WSL
automatically inside the game launcher. WSL was already at its latest stable
version (2.7.14) when this occurred. Related upstream report:
[Microsoft WSL #40618](https://github.com/microsoft/WSL/issues/40618).

## Versions and upstream fixes

Simulant tools revision: `3c8067dbdb1f71f1bf39ef3469e2d3f06fd1c324`.
Linux libraries/assets were downloaded from upstream `main` on September 16, 2026.
That endpoint is mutable; the installed local copies are the verified baseline.
Do not run `simulant update` casually: it replaces upstream assets and libraries.

The upstream main template still specifies C++14 and the old templated Scene API.
This starter uses C++17 and the current Scene lifecycle. It starts directly in its
own scene; the supplied splash transition crashed in the initial local check.

The downloaded Dreamcast release archive used incompatible GCC LTO bytecode, and
the debug archive did not match its headers. The replacement SDK is built from
engine revision `4a5f5799d0232c1a509b186262a214da0f138894` and its pinned submodules.
Rebuild it inside Ubuntu with `bash Simulant/scripts/build-dreamcast-sdk.sh` from
this repository. The script uses the CLI's working Dreamcast toolchain file.
It also updates the external engine profiler's old `fs_dcload_detected` call to
the installed KOS API `syscall_dcload_detected`. The host links KOS pthread support.

Verified Docker image digests:

- Linux: `kazade/linux-sdk@sha256:701c123edadab82bb98ab232add4144fb55d0e958b7b297cab1b1943eec6b8b2`
- Dreamcast: `kazade/dreamcast-sdk@sha256:7202a5d5d007bcb7802c48f1bf30b22d03e337927543b5396130f8420c6f37ec`

The CLI uses the locally installed `latest` tags; do not replace those images
without revalidating the library/compiler pairing. The SDK rebuild script pins
the Dreamcast image by digest. Upstream compiler warnings remain in engine code.

Sources: [official installation guide](https://simulant.dev/docs/getting-started/installation.md),
[Simulant tools](https://gitlab.com/simulant/simulant-tools),
[engine source](https://gitlab.com/simulant/simulant).

Verified: Linux input mapping tests and complete room playthrough, visual captures
from both cameras, and Dreamcast cross-compilation. Hardware execution, controller
forwarding and actual RAM/VRAM/performance budgets remain unverified. CDI packaging
passes the disc generator's boot-prerequisite checks. On September 16, 2026, the
user confirmed the room boots and renders in Windows Flycast with a reported steady
60 FPS. The complete emulated gameplay route and real hardware remain unverified.

Static props now load from `sample.props` and the referenced RGB565 textures.
The AC export was visually checked in the Linux Simulant host and cross-compiled
into the CDI. The package check verifies the finished disc contains exact geometry
and texture filenames/sizes alongside its audio.

Stairs rooms support **Space / controller B** to jump. The supplied Flycast
keyboard mapping maps Space to Dreamcast B. Approach the landing edge before
jumping across the right-hand gap; hold D during flight.
