# First Vertical Slice Compatibility Boundary

## Design goals

- Support one static room, a player, one third-person camera, a door/key/enemy, basic enemy behavior, animation, sound, and one state transition on Unity and Dreamcast.
- Keep rules, state machines, inventory, interaction consequences, patrol decisions, timers, and authored game data in `Game/`.
- Use small value types (`Vec2`, `Vec3`, `Yaw`, `Pose`, numeric seconds) and opaque references (`ActorRef`, `CameraRef`). No engine object, asset, physics, or controller type crosses the boundary.
- Keep the scene static and simple. The compatibility layer adapts authored bindings and backend facilities; it is not an entity, resource, rendering, or physics framework.

## Proposed capabilities

### 1. Frame input

**Purpose.** Drive player movement, interaction, timers, and the fixed third-person camera behavior.

**Minimal contract.** The host calls game update with `deltaSeconds` and an `InputFrame` containing only `move` (`Vec2`) and `interactPressed` (edge-triggered boolean). No look axis, device identity, button code, update phase, clock, or time-scale control is exposed.

**Gameplay may depend on.** The supplied values only. It owns movement speed, facing policy, interaction timing, and all game timers.

**Backend responsibility.** Unity maps its chosen input system to this snapshot. Simulant maps the standard Dreamcast controller to the same snapshot. The backend owns polling and frame/physics scheduling.

**Limitations.** The slice uses movement-derived facing and a fixed authored camera offset; it does not require free-look or a second analog input.

### 2. Static authored bindings

**Purpose.** Give gameplay stable references to the pre-authored player, camera, door, key, enemy, and patrol markers without scene loading or an entity system.

**Minimal contract.** At slice startup, a `SliceBindings` value resolves a fixed set of named binding keys to opaque `ActorRef`/`CameraRef` values and their initial `Pose` values, plus marker `Pose` values. Bindings are not created, searched, spawned, or destroyed at runtime.

**Gameplay may depend on.** The fixed bindings and supplied initial/marker poses. It must not retain or inspect backend objects.

**Backend responsibility.** Unity maps binding keys to scene objects in its integration layer. Simulant resolves named nodes once during scene setup and caches its internal IDs.

**Limitations.** This is deliberately not a general scene graph, prefab, ECS, or actor lookup API. Bindings are immutable for this slice.

### 3. Kinematic displacement

**Purpose.** Keep the player and enemy inside the room and blocked by walls and a closed door.

**Minimal contract.** `MoveKinematic(ActorRef, requestedDisplacement)` returns `resolvedDisplacement` and `blocked`. The backend owns the actor's collision representation and resolved world pose. Gameplay uses the resolved displacement to update its own position state.

**Gameplay may depend on.** Requested versus resolved displacement and a coarse blocked result; it may not depend on slopes, step height, contacts, grounding, sliding rules, rigidbodies, or physics callbacks.

**Backend responsibility.** Unity may adapt a controller or conservative sweep implementation internally. Simulant implements the same observable result with its kinematic/physics facilities or a thin custom adapter.

**Limitations.** Geometry must be static, mostly flat, and simple. There are no steps, moving platforms, dynamic-body pushing, root motion, or advanced slope behavior. This is the highest-risk capability and requires early hardware validation.

### 4. Fixed-purpose traces and door obstruction

**Purpose.** Support forward interaction, enemy line of sight, and door/wall blocking without exposing physics queries.

**Minimal contract.** `TraceFirst(origin, direction, maxDistance, purpose)` accepts only `Interaction` or `Visibility` and returns no hit or `{ actorRef?, distance }`. `SetDoorObstruction(doorRef, enabled)` controls the authored door's solid/visibility-blocking state.

**Gameplay may depend on.** A nearest interaction target or vision blocker, and whether the door obstruction is enabled. It cannot specify engine layers, collider shapes, trigger behavior, normals, arbitrary filters, or broad raycasts.

**Backend responsibility.** Unity converts each purpose to internal colliders/layers and maps hits back to bindings. Simulant maps each purpose to its physics filters and cached binding IDs.

**Limitations.** Door obstruction affects kinematic movement and `Visibility` traces together. It **does not** remove the door from `Interaction` traces, so an open door remains interactable. Keys and doors require explicit authored interaction participation; no collision inference is assumed. Query volume and distance stay small.

### 5. Minimal presentation commands

**Purpose.** Present pickup disappearance, door/enemy/player animation, and the required sound feedback.

**Minimal contract.** `SetVisible(ActorRef, bool)`, `PlayAnimation(ActorRef, AnimationKey, loop)`, and `PlayOneShot(SoundKey, worldPosition)`.

**Gameplay may depend on.** Stable, engine-neutral animation and sound keys stored in game data, plus the commands above. It owns gameplay timing and state transitions; it receives no animation completion event.

**Backend responsibility.** Unity maps keys to its rendering, animation, and audio facilities internally. Simulant maps keys to prepared render/animation/audio resources and owns voice allocation, formats, pooling, and playback policy.

**Limitations.** Visibility never changes collision or interaction participation. Animation means authored clip selection only: no graphs, blend trees, events, root motion, IK, or bone access. Audio is positional one-shot playback only: no mixer, streaming, DSP scheduling, or gameplay-visible voice state.

### 6. One gameplay camera pose

**Purpose.** Render the third-person view.

**Minimal contract.** `SetCameraPose(CameraRef, Pose)`. Gameplay derives the desired pose from player state and a fixed data-defined offset; the backend realizes it.

**Gameplay may depend on.** The bound camera reference and a world-space pose. It does not access a camera object, projection, render pipeline, collision avoidance, or post-processing.

**Backend responsibility.** Unity applies the pose to its bound camera. Simulant applies it to its bound camera node and uses one ordinary perspective view.

**Limitations.** The slice has one active camera and no camera selection, Cinemachine-style rig, camera collision, FOV control, or rendering effects.

## Data and ownership rules

`AnimationKey` and `SoundKey` are stable authored identifiers in engine-neutral game data, not Unity references/GUIDs, Simulant asset IDs, or a runtime resource API. Each backend resolves them during level preparation. No resource loading, scene loading, spawning, or destruction capability is introduced.

`Game/` owns all gameplay rules: pickup/door/enemy state, inventory, patrol/chase decisions, interaction consequences, event transition, movement/facing policy, and data mapping gameplay states to keys. `Engine/Compatibility/` eventually owns only the value types, opaque references, and capabilities described above. Backends own all engine-specific objects and configuration.

## Explicitly deferred capabilities

- General entity/component systems, spawning/destruction, scene loading, save/load, UI, and resource loading.
- NavMesh/pathfinding, rigidbodies, dynamic pushing, generalized physics, triggers, and arbitrary collision queries.
- Grounding, slopes, steps, moving platforms, root motion, animation blending/events/IK, and arbitrary transform editing.
- Camera selection, collision, FOV/projection controls, post-processing, particles, terrain, material/shader control, and arbitrary rendering control.
- Audio mixers, streaming, music control, voice state, and backend-specific pooling controls.
- Networking and any Unity-, Simulant-, or KallistiOS-specific type exposure.

## Dreamcast constraints

- Design to real limits: 16 MB system RAM, 8 MB VRAM, conservative texture and polygon budgets, PVR-friendly fixed-function presentation, compact assets, and bounded audio voices.
- Use one static room, static collision, one active camera, and conservative lighting. Do not make HDRP, Shader Graph, Terrain, Timeline, NavMesh, Unity Physics semantics, Animator Controllers, or Addressables a gameplay requirement.
- Pre-prepare only slice resources. The Simulant backend decides resource formats, lifetimes, voice policy, and rendering details; the Unity backend may improve presentation only without changing the contract.

## Open questions requiring hardware validation

1. Can Simulant's kinematic path produce the stated requested/resolved displacement semantics reliably for the flat room and a closed door?
2. Can Simulant apply one door obstruction state consistently to movement and visibility while leaving interaction participation intact?
3. What practical character/animation, texture, polygon, and simultaneous one-shot-audio budgets hold on real hardware for this room?
4. Does the selected positional-audio path remain stable on hardware under the slice's expected voice load?
5. Are the authored scene-binding, collision-filter, and animation-key conventions supported without per-frame name or resource lookup?

## Decisions differing from the original Gameplay proposal

- Frame timing is update input, not a compatibility service; resource identifiers are a data convention, not a resource API.
- Generic mutable transform access is removed. Kinematic actors move only through `MoveKinematic`; the camera has its own pose command.
- Generic raycasting becomes two fixed-purpose nearest traces. Hit points, normals, engine layers, and arbitrary filters are not exposed.
- Generic runtime collision toggling becomes `SetDoorObstruction`, with explicit movement/visibility/interact semantics.
- Camera activation/selection, optional look input, grounded/contact data, volume control, and arbitrary animation state are omitted because the slice does not demonstrate a need for them.
