# Static visual export, versions 1 and 2

Active imported MeshRenderer/MeshFilter assets in the open Unity room now export
alongside gameplay and audio. Without baked lighting, built-in sample primitives retain
their backend proxy rendering; with it, static primitives export as geometry. No extra component is required on an imported static
prop: the placed AC prefab is included automatically. Disabled/inactive props
are excluded. Custom player/key/door or skinned/animated models require a later
dynamic-model path and are rejected rather than frozen into the room.

Export bakes position, rotation, scale (including mirrored scale), base-map UV
scale/offset, vertex colors and material tint into engine-neutral files. Blender
remains the canonical source for new model editing; the supplied AC FBX has no
.blend source. Unity is only the import/placement adapter. Simulant never loads
FBX, Unity materials, GUIDs or prefabs. Mesh collision remains separately authored.

The runtime uses opaque, unlit base-color rendering; optional [baked lighting](baked-lighting.md)
is stored in vertex colors and includes static room primitives in DCP2. It supports the built-in
URP Lit/Unlit, Standard and Unlit Texture/Color materials with a base color/PNG;
normal, metallic, occlusion and emission maps, transparency, collapsed texture UVs and custom shaders
are rejected. Smoothness and shadow results are not baked; the optional diffuse
baker samples ordinary Unity light placement with its own documented attenuation.
Textures must be opaque PNGs with Repeat wrapping and power-of-two dimensions
from 8 to 256. Export converts pixels to RGB565 and deduplicates identical images.
The runtime uses bilinear sampling, no mipmaps, and releases CPU texture data after
upload through Simulant's default behavior.

Budgets for this static-prop pass (not whole-console memory certification):
- 4,096 triangles across all placed props, expanded to at most 12,288 vertices.
- 32 material parts/draws.
- 8 unique textures and 512 KiB total RGB565 pixels.
- Geometry file at most 300,000 bytes.

Current AC: 528 triangles, 6 parts, two 256x256 RGB565 textures (256 KiB total).
Repeated placements share texture uploads but currently duplicate geometry.
Budget errors include the offending object/material where applicable.

## Files

`sample.props` has little-endian integers/floats:
- magic `DCP1` or `DCP2`, uint32 texture count, uint32 part count.
- DCP2 only: uint32 flags = 1 (static room visuals included).
- Each texture: uint32 width, height, then 36 ASCII filename bytes.
- Each part: uint32 vertex count (multiple of 3), int32 texture index (-1 = none).
- Each expanded vertex: float32 world XYZ, float32 UV, four RGBA bytes (alpha 255).

Each `prop-textures/<32 lowercase SHA256 prefix digits>.rgb` contains magic `DCT1`,
uint32 width/height, and little-endian RGB565 pixels, bottom row first. The hash
covers the header and pixels. Short names fit the disc's Joliet filename rules.
Unity-space winding is retained, with mirrored transforms corrected on export.
The Simulant host reflects Z and reverses triangle winding at its render boundary.
The shared C++ parser validates bounds, filenames, references and finite values
before creating rendering resources. Staging also validates texture sizes/hashes
and removes unreferenced textures from the generated build folder. The completed
CDI is checked for exact prop and texture directory filenames/sizes.

## Verification

`RoomPropExportChecks.Run` checks the real AC, deterministic export, placement,
rotation/nonuniform/mirrored scale, texture deduplication, disabled objects,
material rejection and budgets. Native parser tests include malformed/truncated
records, nonfinite values, alpha and the actual exported bundle. Python staging
tests reject corrupted/missing dependencies and verify stale-file removal.
`SIMULANT_ROOM_SNAPSHOT=1` takes a desktop frame after 30 rendered frames and exits;
`DREAMCAST_ROOM_FILE` can point to a temporary review camera without editing the
saved Unity scene. Dreamcast hardware remains untested.

The AC source contained collapsed texture coordinates on 92 triangles, which
stretched texture rows across surfaces. Its prefab uses derived UV-repaired
meshes; the supplied FBX is unchanged and an OBJ repair snapshot is available in
`Art/AirConditioner`. The corrected mapping was visually checked in the Linux
Simulant host. Prop materials explicitly use opaque rendering and back-face
culling. A user-reported intermittent WASD issue is deferred for the next pass.
