# Current compatibility boundary

## Project direction

Dreamcast is the final platform and Simulant is the intended runtime. Unity is
our Windows development host and renderer. There is no Spiral dependency.
The Simulant backend is not implemented or hardware-validated yet.

## Implemented ownership

- `Game/Core/`: engine-neutral values, authored bindings and the versioned room parser.
- `Game/Gameplay/`: authoritative player state and movement rules.
- `Game/Compatibility/`: the callable game loop, fixed-camera transition rules,
  and `IDisplacementBackend` / `StaticCollisionBackend`.
- `UnityBridge.cpp`: Windows DLL exports, startup wiring and read-only inspection.
- `Unity/Dreamcast-Horror/Assets/Scripts/`: all Unity C# integration, editor tools,
  debug presentation and template scripts.

The host supplies `InputFrame` and elapsed seconds to `game_step`. Keyboard
polling is a Unity adapter detail; game code receives no device or Unity types.
C++ updates the player by requesting displacement from the backend and applying
the resolved result, including partial movement. Unity reads the result. The
current static collision backend shares no code or behavior contract with Unity
Physics. It operates on X/Z footprints; Y is not used to test obstacles. Floors,
ceilings and overhead lintels must not be authored as movement obstacles.

The game has two authored fixed cameras and one active camera at a time. It owns
the crossing test and updates the active reference and pose together, including
when movement lands exactly on a boundary and reverses. A zero camera reference
in `CameraTransition` disables the transition for single-camera hosts/tests.
A host must provide both transition poses if it enables the transition.

## Shared authored room data

The prototype uses one versioned, engine-neutral text file:
`Unity/Dreamcast-Horror/Assets/StreamingAssets/sample.room`.
Its location packages it with Unity; its contents and C++ parser have no Unity
requirements. Another host can read the same bytes and call `parseRoomData`.
No additional JSON library or speculative asset framework is introduced.

`RoomData` contains startup bindings and a fixed array of at most 128 collision
shapes. The parser checks versions, counts, finite numbers, supported primitive
kinds and positive dimensions. Invalid input leaves the output unchanged.
The Unity bridge refuses startup on error and does not continue a previous room.
The old incremental native collision registration API was removed: it silently
truncated shapes and kept runtime collision dependent on Unity renderer queries.

Parsing uses standard C++ streams and may allocate at startup. The game step and
collision solver do not allocate. This distinction is intentional. Stream code
size, load-time memory and performance still need measurement with the Dreamcast
toolchain; the PC DLL size is not a Dreamcast RAM measurement.

`Dreamcast > Export sample room` is the temporary Unity authoring adapter. It
exports the existing primitive scene to the shared file and rejects overflow
before writing. It is an explicit editor action, not runtime gameplay. Changes
to the prototype scene's collision, player spawn, cameras or boundary require a
fresh export. Runtime camera poses and collision are read from the exported file.

See [room-format.md](room-format.md) for the exact data contract and authoring
workflow. This is startup data for one room, not a scene/resource framework.

## Presentation and assets

Unity URP, materials, camera components and gizmos remain presentation details.
They are not assumed to have Simulant equivalents. Debug drawing uses a snapshot
of loaded collision during Play mode, and cached authoring data in edit mode.
It performs no native player queries outside Play mode.

The current meshes are disposable Unity primitive proxies. No production models
have been authored. Blender files must be the canonical source when production
3D assets are introduced; a Blender export/import path has not yet been proven.
The room export solves gameplay-data portability, not mesh/material portability.

## Future capabilities (not implemented contracts)

Doors, keys, enemies, inventory, traces, animation, audio and save/load are still
future slice work. Add only the smallest interface required by a demonstrated
feature. In particular, do not implement a general entity, renderer, physics,
resource or animation framework based on the previous proposed API.

Validate any required Simulant animation, audio, collision or asset facility
before substantial content depends on it. Do not assume Unity NavMesh, Animator
Controllers, Timeline, Physics, Addressables or shader features transfer.

## Verification and remaining evidence

`Tests/run-tests.ps1` compiles every current `Game/**/*.cpp` outside Unity and
runs all native test executables with assertions and compiler warnings enabled.
It exercises real collision, the checked-in room, parser rejection, camera
boundary behavior and the existing movement/binding contracts.

`ArchitectureChecks.Run` in Unity batch mode checks scene script references,
collision authoring overrides, export consistency and overflow, C# startup,
P/Invoke layout, native movement and camera/player presentation. It exercises
integration methods in the editor, not a hardware or visual playthrough.

Still required before committing to substantial content:

1. Compile and run this core using the Simulant/Dreamcast toolchain.
2. Exercise movement and camera conventions in a minimal Simulant host.
3. Measure CPU time, load-time and steady-state RAM, VRAM, polygons, draw calls,
   textures, animation and audio on the actual target.
4. Prove a Blender-source asset workflow with one prop and one character.

The design budgets remain 16 MB system RAM and 8 MB VRAM, with conservative
content and fixed-function-compatible presentation. Passing PC tests does not
certify these hardware budgets or Simulant compatibility.
