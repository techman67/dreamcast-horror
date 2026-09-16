# Prototype room format, version 1

The shared artifact is `Unity/Dreamcast-Horror/Assets/StreamingAssets/sample.room`.
All hosts consume the same text. Whitespace separates tokens; decimal numbers
use a period regardless of locale. Comments and unknown/trailing tokens are
rejected. No engine GUIDs, object names or platform-specific paths occur inside.

Records occur in this exact order:

```text
dreamcast_room 1
player <actor-id> <x> <y> <z> <yaw> <pitch> <roll> <collision-radius>
camera 1 <x> <y> <z> <yaw> <pitch> <roll>
camera 2 <x> <y> <z> <yaw> <pitch> <roll>
transition <boundary-z> <half-width-x> <initial-camera-id>
shapes <count>
<zero or more shape records>
end
```

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
5. Select **Dreamcast > Export sample room** and review the text diff. Export
   validates the data through the same native parser before replacing the file.
6. Run native tests and ArchitectureChecks.Run. The Unity check reports scene /
   export drift. Do not silently generate content during a verification run.

Edit-mode gizmos show cached authoring values. Export or an inspector component
edit refreshes them. During Play, debug drawing instead shows the loaded native
snapshot, so later editor changes cannot masquerade as live collision changes.

## Art source and scope

The checked-in scene is a graybox made from built-in primitives, not final art.
Keep production .blend files as canonical sources when replacing them. A future
Blender exporter can emit these same room values; do not make Unity's mesh
classification algorithm a requirement for another backend. Mesh, material,
animation and audio conversion are separate unverified work. No Simulant asset
format or importer is assumed by this prototype format.
