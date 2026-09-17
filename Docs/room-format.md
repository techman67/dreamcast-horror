# Prototype room format, versions 1, 2 and 3

The shared artifact is `Unity/Dreamcast-Horror/Assets/StreamingAssets/sample.room`.
All hosts consume the same text. Whitespace separates tokens; decimal numbers
use a period regardless of locale. Comments and unknown/trailing tokens are
rejected. No engine GUIDs, object names or platform-specific paths occur inside.

The current key-door sample uses version 2. Version 1 remains supported for
movement-only rooms and does not have an objective. Records occur in this order:

```text
dreamcast_room 2
player <actor-id> <x> <y> <z> <yaw> <pitch> <roll> <collision-radius>
camera 1 <x> <y> <z> <yaw> <pitch> <roll>
camera 2 <x> <y> <z> <yaw> <pitch> <roll>
transition <boundary-z> <half-width-x> <initial-camera-id>
shapes <count>
<zero or more shape records>
key <actor-id> <x> <y> <z> <interaction-range>
door <actor-id> <x> <y> <z> <interaction-range> <zero-based-shape-index>
exit <boundary-z> <center-x> <half-width-x>
end
```

Version 1 omits the `key`, `door` and `exit` records. Version 2 requires all three.
Key and door IDs must be distinct and must differ from the player ID. The door
must reference a box centered on its authored position, and its interaction
range must reach outside the closed slab. The exit must lie south of the door,
with its width contained in the opening after accounting for player radius.
The door counts toward the same 128-shape budget.

Shape records:

```text
box <center-x> <center-y> <center-z> <half-x> <half-y> <half-z>
sphere <center-x> <center-y> <center-z> <radius>
capsule <center-x> <center-y> <center-z> <radius> <height>
```

Coordinates use +X right, +Y up and +Z forward. Angles are radians; rotate around
Z (roll), then X (pitch), then Y (yaw). Zero rotation faces +Z. Host adapters must
translate these conventions explicitly, rather than assume native engine parity.
Box footprints are axis aligned. Sphere and upright capsule footprints are
circles. Shape Y/height are descriptive and do not enable vertical collision.

The prototype fixes camera IDs to 1 and 2 and requires a nonzero player ID.
The transition spans [-half-width-x, +half-width-x] at boundary-z. At startup the
explicit initial camera wins, including when the spawn is exactly on the boundary.
Maximum collision count is 128; exceeding it fails instead of dropping obstacles.
All sizes/radii must be positive; capsule height must cover its diameter.

## Authoring the current prototype

1. Open SampleScene outside Play mode. Keep exactly one NativeGameBridge.
2. Adjust the primitive geometry and CppCollision components. Use explicit
   Box/Sphere/Capsule or Manual mode when automatic inference is unsuitable.
   Floors, ceilings and overhead lintels are decorative, without CppCollision.
3. Adjust GameplayCamera_01, GameplayCamera_02 and CameraZone_01_To_02.
   The marker must be centered on X=0 and unrotated. Its X scale gives the span.
4. Set the player's visual transform; CppPlayerVisual subtracts its pivot offset
   to export the native spawn. Set radius and initial camera in the bridge's
   room export settings. These inspector values affect export, not live gameplay.
5. For the objective, keep a KeyDoorPresentation component with references to
   BrassKey, SliceDoor, its CppCollision and SliceExit. Configure the actor IDs,
   interaction ranges and exit width there. Export with the door and key active
   outside Play mode; the door uses an explicit manual box around its slab.
6. Select **Dreamcast > Export sample room** and review the text diff. Export
   validates the data through the same native parser before replacing the file.
7. Run native tests, ArchitectureChecks.Run and KeyDoorChecks.Run. The Unity check reports scene /
   export drift. Do not silently generate content during a verification run.

Edit-mode gizmos show cached authoring values. Export or an inspector component
edit refreshes them. During Play, debug drawing instead shows the loaded native
snapshot, so later editor changes cannot masquerade as live collision changes.

## Art source and scope

The checked-in scene is a graybox made from built-in primitives, not final art.
Keep production .blend files as canonical sources when replacing them. A future
Blender exporter can emit these same room values; do not make Unity's mesh
classification algorithm a requirement for another backend. Static mesh/base-color
export uses a separate portable bundle, documented in [static props](static-props.md).
Animation remains future work; this collision format does not depend on a
Simulant mesh importer.

## Audio bundle

The room exporter also writes `sample.audio` and the referenced PCM WAVs under
`StreamingAssets/room-audio/`. Sound assignments are authored with Dreamcast Sound
Cue and Dreamcast Room Sound components. Room format versions 1/2 remain unchanged;
the audio sidecar has its own version and parser. The current host/build workflow
requires that sidecar, which can explicitly describe a silent room.

Use **Dreamcast > Export room and build CDI** to collect the complete bundle and
build the disc. **Dreamcast > Audio > Check selected audio** explains source-file
compatibility; **Check room audio** checks the aggregate configuration. See
[audio authoring](../Audio/README.md) and [manifest schema](audio-format.md).

Static visuals are carried in the companion `sample.props` file and referenced
`prop-textures/` files; they do not alter the collision format. See [static props](static-props.md).

## Version 3: 3D character collision

Version 3 adds `character <standing-height> <step-height>` immediately after
`player`. Player XYZ is the feet position; radius defines the horizontal box
half-width and half-depth. Height must be 0.5–4 m, radius >0–1 m, step height
0–0.5 m and less than height. Only axis-aligned `box` shapes are accepted.
All box centers/extents and spawn coordinates are limited to ±10,000 m;
extents remain strictly positive. Spawns overlapping any closed solid fail.

After shapes, `objective 0` goes directly to `end`; `objective 1` requires the
same key/door/exit records as v2. Camera records are unchanged. Versions 1/2
still use the flat controller and their original format. See
[character physics](character-physics.md) for behavior and authoring limits.
