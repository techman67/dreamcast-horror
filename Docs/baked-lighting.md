# Baked static lighting

Add **Dreamcast → Lighting → Add baked lighting and sample wall lamp** once, or
add a **Dreamcast / Baked Room Lighting** component to one room object. The menu
also places a small warm wall lamp near the sample AC. Save the scene, then use
the usual **Export sample room** or **Rebuild CDI** command.

Place ordinary Unity Directional, Point or Spot lights. Configure their color,
intensity, range and spot angles. Active lights (up to 16) and their GameObject
layer culling masks are sampled during export. Color temperature is supported.
The lighting settings object provides ambient RGB and maximum triangle edge
length. Reduce the edge length for smoother pools of light; increase it if the
export reports too many triangles. No separate Unity lightmapper bake is needed.

This is a small diffuse vertex baker, not Unity's lightmapper. It uses ambient
plus Lambert diffuse. Point lights use `(1 - distance/range)^2` attenuation;
spots additionally fade smoothly between their inner and outer half angles.
Overbright RGB illumination is scaled by its largest channel into 0..1, preserving
its hue instead of clipping individual channels. It is multiplied into material tint and mesh
vertex color; the runtime multiplies this by the existing base texture. Normals
use the inverse transpose of the object transform, including mirrored and
nonuniform scales. Large lit triangles subdivide at export; source meshes remain
unchanged. Basic Unlit materials bypass lighting and subdivision, useful for the
sample bulb. The bulb does not illuminate other geometry by itself; its point
light does that.

The current pass has **no cast shadows, occlusion, bounce light, specular highlights,
skybox/environment contribution, lightmaps or moving lights**. Walls do not block
the baked light yet. Lights requesting shadows issue a warning. Cookies and area
lights fail export with a reason. Unity's normal scene lighting is an approximate
authoring preview, not a pixel-identical preview of this bake; check the exported
result in Simulant/Flycast. Moving player/key/door proxies retain their previous
unlit appearance. Their lighting and cast shadows are later work.

With the component enabled, static primitive renderers and imported props export
together. Simulant suppresses static collision proxy visuals and its generated
floor, while keeping collision and moving-object presentation unchanged. The
authored floor and walls now supply their actual geometry/material colors. An
invisible collision shape stays invisible. Custom dynamic models remain unsupported.
Without the component, legacy prop-only export and room proxies still work.

## Dreamcast cost and format

Lighting is stored in the four existing RGBA bytes per vertex. It needs no
additional textures, no exported light objects, and no per-frame lighting
calculations. Subdivision consumes geometry budget: the entire static room and
props share the existing limits of 4,096 triangles, 32 parts, 8 textures, 512 KiB
RGB565 pixels and a 300,000-byte geometry file. These are subsystem budgets, not
a claim that every possible combination has passed real-hardware testing.

Lit exports use `DCP2`: the DCP1 header followed by a uint32 flags field after
texture/part counts. The only accepted flag value is 1, meaning static room
visuals are included. Texture/part/vertex records are unchanged. Both native and
staging parsers continue to accept DCP1. No game logic depends on Unity lights,
Simulant lights, or these authoring settings.

`RoomLightingChecks.Run` exercises front/back illumination, range, spot cones,
layer masks, disabled/unsupported lights, mirrored/nonuniform normals, subdivision,
deterministic bytes and triangle limits. Parser/staging checks cover DCP1/DCP2
compatibility and malformed flags. Rebuild the CDI after every lighting change.
