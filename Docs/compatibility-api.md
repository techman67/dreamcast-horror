# Current compatibility boundary

## Project direction

Dreamcast is the final platform and Simulant is the intended runtime. Unity is
our Windows development host and renderer. There is no Spiral dependency.
The Simulant graybox host runs the exported room and shared core on Linux/WSLg and
cross-compiles for Dreamcast. Hardware execution and budgets remain unverified.

## Implemented ownership

- `Game/Core/`: engine-neutral values, authored bindings and the versioned room parser.
- `Game/Gameplay/`: authoritative player state and movement rules.
- `Game/Compatibility/`: the callable game loop, fixed-camera transition rules,
  and `IDisplacementBackend` / `StaticCollisionBackend`.
- `UnityBridge.cpp`: Windows DLL exports, startup wiring and read-only inspection.
- `Simulant/sources/`: room loading, device mapping, graybox rendering and snapshot
  presentation. It links `Game/` directly without the Unity DLL.
- `Unity/Dreamcast-Horror/Assets/Scripts/`: all Unity C# integration, editor tools,
  debug presentation and template scripts.

The host supplies `InputFrame` and elapsed seconds to `game_step`. Keyboard
and controller polling are host adapter details; game code receives no device
or Unity types. WASD/arrows, left stick and D-pad map to movement; E and the
controller south button map to one interaction edge. C++ normalizes movement
above unit length, so diagonals are not faster and partial stick input is retained.
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

## Key and locked door slice

`Game/Gameplay/KeyDoor.cpp` owns interaction proximity, key possession, door state,
feedback duration and completion. Standing near an object only offers a prompt;
an interaction edge is required to pick up the key or unlock the door. The key
is retained after use. Opening is immediate and one-way for this prototype.
Crossing the authored southbound exit within its width completes the slice and
freezes player movement until the host reinitializes the room.

The only new backend operation is `setDoorObstruction(ActorRef, bool)`. The static
backend binds one authored shape index at startup and skips that shape in all
movement and sliding queries once the game opens it. Other obstacles remain
unchanged. All backends must implement this operation; there is no silent no-op
fallback. Game code never calls Unity colliders, triggers or animation.

The Unity presentation applies C++ snapshots: hide the collected key, hide the
open door slab, and map prompt/feedback identifiers to text. Runtime key/door
positions come from the exported data. Collision debug omits the open door too.
No inventory UI framework, general interaction registry, animation graph or
resource system was added. Interaction uses X/Z distance for the single flat
room; line-of-sight tracing is deferred and must be added before authoring
interactables across separating walls.

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

The gameplay slice still uses disposable Unity primitive proxies. An imported AC
prop now exercises static mesh and base-color texture export. Blender files must be the canonical source when production
3D assets are introduced; a Blender export/import path has not yet been proven.
Static prop mesh/base-color export is now implemented; see [static props](static-props.md).
Animation and richer material/lighting conversion remain unimplemented.

## Future capabilities (not implemented contracts)

Enemies, multi-item inventory, traces, animation, streamed/spatial audio and save/load remain
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
boundary behavior, key/door rules, input magnitude and a complete real-room
playthrough, plus the existing movement/binding contracts.

`ArchitectureChecks.Run` in Unity batch mode checks scene script references,
collision authoring overrides, export consistency and overflow, C# startup,
P/Invoke layout, native movement and camera/player presentation. It exercises
integration methods in the editor, not on Dreamcast hardware. `KeyDoorChecks.Run`
adds input-edge and end-to-end key/door presentation checks.

`Simulant/run.ps1 check` tests Simulant keyboard/controller mapping and a full
sample-room playthrough, including visual visibility, both cameras and reset.
It captures rendered frames for visual review. The same core builds into the
Dreamcast ELF; the host reflects Z only at its rendering boundary. See
`Simulant/README.md` for proxy geometry and asset limitations.

Still required before committing to substantial content:

1. Package and run the Simulant/Dreamcast build on hardware.
2. Verify physical Dreamcast controls and rendering, beyond the Linux host checks.
3. Measure CPU time, load-time and steady-state RAM, VRAM, polygons, draw calls,
   textures, animation and audio on the actual target.
4. Prove a Blender-source asset workflow with one prop and one character.

The design budgets remain 16 MB system RAM and 8 MB VRAM, with conservative
content and fixed-function-compatible presentation. Passing PC tests does not
certify these hardware budgets or Simulant compatibility.

## Room audio events

`game_take_audio_events()` drains the current step's fixed-capacity `AudioEvents`
batch. The shared rules emit footsteps from resolved movement and key/door cues
from actual interactions. Each host consumes once after `game_step`; reset and
invalid steps clear pending events. Unity's bridge exports the same 20-byte batch.
Engine-specific playback stays in the host, with eight bounded slots and a 512 KiB
PCM bank limit. See [audio design and memory accounting](../Audio/README.md).

`AudioData` is a separate versioned presentation manifest paired with the exported
room. It defines clip IDs/files, cue assignments, volume and up to two placed
sounds with loop and inner/outer distance settings. Unity authoring components are editor input only;
both hosts consume this manifest. Shared C++ parses/validates it, while host code
owns file loading, playback and distance attenuation. Static AICA samples/channels
are a Dreamcast backend detail, not a requirement on gameplay code. See
[audio format](audio-format.md).
