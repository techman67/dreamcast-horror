# Baked static lighting

Add **Dreamcast → Lighting → Add baked lighting and sample wall lamp** once, or
add a **Dreamcast / Baked Room Lighting** component to one room object. The menu
also places a small warm wall lamp near the sample AC. Save the scene, then use
the usual **Export sample room** or **Rebuild CDI** command.

Place ordinary Unity Directional, Point, Spot, Rectangle or Disc lights. Configure their color,
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

## Light tools and source choices

Open **Dreamcast > Lighting > Light tools**. Select a fixture (bulb, television,
window frame, fluorescent housing), then add a child light. The panel creates
room lighting settings if needed and lets you edit the selected light without
depending on which light controls the current Unity render pipeline exposes.
Creation and changes support Undo. Save your scene, then export/rebuild as usual.
**Dreamcast > Lighting > Open lighting test room** opens the separate
`LightingTestScene`: the existing room plus a cool emissive TV screen and a
rectangular spill light, now linked to a flicker effect. The original `SampleScene` is unchanged. As with the
stairs scene, export the scene you intend to build; these scenes share the
`sample.room` / audio / prop output names. The TV is a static lighting example,
not animated video or an interactable wall switch.
The budget-check button validates the current static geometry/material/light
configuration without writing an export or saving the scene; it is not a full
audio/gameplay check or a measured frame-rate guarantee.

| Source | Suggested use | Controls |
| --- | --- | --- |
| Point | Bulb, small lamp | Color, intensity, range |
| Spot | Directed fixture or fixed beam | Point controls plus inner/outer cone |
| Directional | Sun/moon direction | Rotation, color, intensity |
| Rectangle | TV spill, window, fluorescent panel | Width/height in metres, rotation, range |
| Disc | Round ceiling panel | Radius in metres, rotation, range |

Area lights emit on one side along local **blue +Z**. A selected area light has
an outline and arrow; rotate the arrow toward the surfaces to illuminate.
Size ignores transform scale, including parent scale; set dimensions in Light
tools. These dimensions are stored on Unity's ordinary Light component.
Place sources just outside opaque fixture geometry. A visible panel or bulb is
a separate mesh; a light alone does not create geometry.

Rectangle/disc light is approximated by 4–32 deterministic samples, configured
with **Area Samples** on the room settings (default 16). Samples include emitter
and receiver facing, the existing range falloff, layer masks, and individual
shadow rays. Partial visibility gives softer static shadow edges. More samples
increase export work only; they do not create runtime lights or multiply the
authored intensity. This is a normalized artistic approximation, not Unity's
physical area-light power model. Vertex density still limits the visible detail.
All five types together share the 16-source authoring limit.

## Emissive colors

On a supported Standard or URP Lit material, enable **Emission** and set its
**Emission Color**. Uniform RGB from 0–8 is accepted. Emission adds to the lit
material color even in darkness; overbright output is normalized to preserve
hue. No bloom or glow halo is implied. Disabled emission contributes nothing.

The existing single-texture renderer multiplies the finished color by the base
texture, including emission: dark texture pixels remain dark. This is deliberately
limited self-illumination, not a separate additive emission texture pass.
Emission maps remain rejected with a material/property diagnostic. Use a separate
mesh/material for a glowing part and a suitable base PNG, or an Unlit material.

Material emission does **not** light nearby geometry automatically. Pair a TV
screen with a Rectangle light or a bulb with a Point light for controllable spill.
This avoids an unbounded mesh-emitter/bounce solver. Area sources, self-emission
and their static shadows use the unchanged DCP2 format and existing vertex bytes;
they add no Dreamcast lighting calculations or extra textures.

## Flicker, pulse and proximity lights

Select a Light in the scene, then choose **Dreamcast > Lighting > Add effect to
selected light**, or the effect button in **Light tools**. The **Dreamcast Light
Effect** component and Light tools expose the same settings:

- **Mode:** Steady, Pulse (smooth triangular brightness cycle), or Flicker
  (repeatable stepped irregular brightness).
- **Frequency:** pulse cycles or flicker steps per second, 0.1–10 Hz.
- **Minimum Brightness:** fraction of full authored brightness, 0–1. The Light's
  ordinary color/intensity remain the fully lit appearance.
- **Activation Range:** 0 keeps the effect active. A positive distance switches
  it off when the player's feet are outside the sphere, with a half-metre fade
  inside its edge. XYZ distance includes height. Use Steady plus a range for a
  proximity light; no trigger collider or Unity Physics is needed.
- **Seed:** repeatable flicker variation. Reset restarts the same sequence.
- **Glowing Surfaces:** optional static MeshRenderers for the bulb or screen.
  Each material must be Unlit or have Emission Color enabled. Lit surfaces keep
  their other illumination when the emission dims. Up to eight unique renderers.

Keep the source and surrounding geometry stationary. Point, Spot, Directional,
Rectangle and Disc can use the effect, subject to the affected-vertex budget.
This does **not** implement moving lights/flashlights or animated shadow geometry.
The exporter rejects known player/key/door/Animator attachments instead of
silently freezing them. Only this component's behavior exports; arbitrary Unity
scripts, light animations and realtime settings do not.

This first runtime slice allows **one effect source per room**, alongside the
ordinary static lights. The exporter bakes its fully off and fully on appearances,
including its static shadows, then records only vertices whose packed colors
differ. Runtime blends those endpoints. This is color-state interpolation, not
recalculating a light or shadow map; HDR normalization is therefore evaluated at
the endpoints rather than continuously. Textures still multiply the result.

The cap is **2,048 changed vertices**, independent of the 4,096-triangle room
limit. Export explains the count and suggests reducing range, filtering layers,
or simplifying affected geometry. Light tools reports both budgets. Color refresh
is capped at 20 Hz and skips unchanged brightness. Per-vertex off/on/index records
use at most 24 KiB of retained CPU memory, plus mesh references/container overhead;
the exporter adds at most 16 KiB plus 36 bytes to the file. No extra textures or
runtime shadow rays are required. Marking an affected mesh updated also lets
Simulant refresh its mesh bookkeeping, so this is not a measured CPU guarantee.
Physical Dreamcast performance still needs validation.

Unity Play preview calls the same C++ waveform/proximity code through the native
bridge, adjusts light intensity and uses material property blocks for linked
glows. Disabling the preview restores authored intensity and previous property
blocks without editing material assets. Unity rendering remains approximate:
especially area-light spill and baked shadows need the exported Simulant/Flycast
view. Exit Play before exporting. Runtime R/Start resets the effect with the room.

## Rough corner shading

Check which light casts the shadows before changing ambient or AO. A Directional
Light behaves like outdoor sunlight: room walls and pillars can cast large floor
shadows even when there is no visible fixture at that location. Vertex interpolation
can spread those edges across large triangles. Turning AO off does not remove them.

For an indoor source intended only to provide fill, select that Directional Light
and choose **Dreamcast > Lighting > Use selected directional light as fill** (also
available as a button in Light tools). It disables only that light's shadows,
with Undo; color, intensity, ambient and other lights stay unchanged. Save and
rebuild. This is a lighting-design choice, not a fix that relocates physical shadows.
Keep directional shadows for intentional sun/moon lighting. The lighting test room
uses shadow-free directional fill while the wall lamp and TV still cast shadows.

**Dreamcast > Lighting > Softer corners preset** applies AO strength 0.18,
distance 0.4m, and 24 samples, with Undo. Save and rebuild to apply it. The lighting
test room uses this preset. It reduces broad dark ambient patches without adding
triangles. It does not remove actual light-blocking shadows or add light sources.
Low-density vertex interpolation can still produce visible wedges: reducing Max
Triangle Edge improves spatial detail but must fit the room triangle budget.

## Shadows and ambient occlusion in Unity

Select the **Dreamcast Baked Lighting** object:

- **Bake Shadows** enables static light blocking.
- **Shadow Bias** offsets rays from the surface to avoid self-shadow marks. Start
  with 0.002 m; excessive bias can leak through thin geometry.
- **Occlusion Strength** darkens ambient light in corners/under nearby objects;
  0 disables it. This is an approximation of ambient visibility, not bounce light.
- **Occlusion Distance** sets the search radius in metres (default 0.75).
- **Occlusion Samples** trades export time for sampling quality (4-32, default 12).
- **Area Samples** controls rectangular/disc source sampling (4-32, default 16).
- **Max Triangle Edge** controls spatial detail. Shadows blend between vertices;
  smaller edges improve their definition but consume the 4,096-triangle budget.

On a Unity **Light**, choose **Hard Shadows** and adjust **Shadow Strength**.
None disables that light's shadows. For point/spot/directional lights, Soft uses the same hard ray visibility with
vertex interpolation and issues a warning; Unity's soft-shadow filtering is not
exported. Light color, intensity, range, spot angles, temperature and culling mask
remain editable. The light's culling mask filters both receivers and casters.
Area lights always average visibility across their sampled surface when shadows
are enabled; their size controls the resulting partial-shadow transition.
Unity's own shadow bias/resolution settings are not used; use the room bias above.

On **Mesh Renderer**, **Cast Shadows: Off** excludes that object from shadow and
ambient-occlusion blocking. On/Two Sided use two-sided opaque mesh triangles, so
thin and mirrored walls block light. **Shadows Only** contributes an invisible
bake occluder, without exporting visible geometry. **Receive Shadows** controls
both direct shadows and ambient occlusion on that renderer. Unlit materials still
bypass lighting, but their geometry can cast a shadow unless casting is off.
Transparent/cutout casters are rejected: texture alpha is not traced.

No Colliders or Unity Physics are needed. A deterministic editor-only triangle
query tree traces the actual source meshes, with a 16,384-source-triangle limit
and 12,288 vertices per caster. The usual visible export limits still apply.
Disabled renderers and moving player/key/door proxies do not cast baked shadows.
In particular, a door opening will not leave a permanent baked door shadow.

Save and choose **Export room with these lighting settings** in the component's
inspector, or **Dreamcast > Export room and build CDI** to rebuild the disc.
The original SampleScene wall lamp now enables hard baked shadows. Re-export
whenever a light, caster, receiver or room setting changes.

The current pass has **no bounce light, specular highlights, skybox/environment
contribution, lightmaps or moving lights**. Unity's normal scene lighting is an
approximate authoring preview, not a pixel-identical preview of the vertex bake;
check the exported result in Simulant/Flycast. Moving player/key/door proxies
retain their previous unlit appearance. Their lighting and cast shadows remain
later work. The Light Effect component can fade a stationary light's baked shadow
contribution with its light. Shadows cannot respond to moving geometry.

With the component enabled, static primitive renderers and imported props export
together. Simulant suppresses static collision proxy visuals and its generated
floor, while keeping collision and moving-object presentation unchanged. The
authored floor and walls now supply their actual geometry/material colors. An
invisible collision shape stays invisible. Custom dynamic models remain unsupported.
Without the component, legacy prop-only export and room proxies still work.

## Dreamcast cost and format

Static lighting is stored in the four existing RGBA bytes per vertex. It needs no
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

Effect exports use `DCP3`: the same flags/texture/part layout as DCP2, with base
vertex colors representing the OFF endpoint. After all parts, a 36-byte record
stores little-endian uint32 mode; float32 frequency, minimum, activation range,
position XYZ; uint32 seed and change count. Each change is uint32 flattened vertex
index and packed RGBA ON color. Indices must be strictly increasing and in range;
alpha is 255. Count must be 1–2,048. The full file still fits 300,000 bytes.
Native parsing and Python staging validate the same bounds before use; DCP1/DCP2
remain valid without any effect records. Rules and blending live in portable
`Game/Core/LightEffect.h`; Unity and Simulant supply only authoring/presentation.

`RoomLightingChecks.Run` exercises front/back illumination, range, spot cones,
layer masks, disabled/unsupported lights, mirrored/nonuniform normals, subdivision,
deterministic bytes and triangle limits. Parser/staging checks cover DCP1/DCP2
compatibility and malformed flags. Rebuild the CDI after every lighting change.

`RoomShadowChecks.Run` additionally verifies point/spot/directional blocking,
strength, layer masks, renderer switches, mirrored/thin casters, dynamic exclusion,
AO distance, surface bias, deterministic export and receiving controls. All ray
queries run only during export; the Dreamcast format and runtime are unchanged.

`RoomExtendedLightingChecks.Run` covers rectangle/disc illumination, one-sided
emission, rotation, range, masks, dimensions independent of scale, normalized
sample count, partial area shadows, emission colors/HDR and rejection diagnostics.

`RoomLightEffectChecks.Run` checks native preview ABI, independent OFF/ON static
bakes against reconstructed DCP3 colors, mirrored/subdivided vertex indexing,
linked emission, determinism and diagnostics. `RunPreview` exercises actual Play
mode updates and restoration. Native tests cover waves/proximity, phase bounds,
color interpolation, malformed/truncated DCP3 data and parser failure isolation.

## Further lighting work

This pass does not implement interactable wall switches, moving lights,
flashlights, lighting on moving models, cookies, lightmaps or real-time shadow geometry.
Those need their own bounded runtime design and performance checks. Simulant's
documented Dreamcast limit is two native lights per renderable, not two lights in
the entire level; its runtime spotlight and shadow pipeline are incomplete.
See [Simulant lighting](https://simulant.dev/docs/rendering/lighting.md).
The baked effect uses vertex-color updates rather than Simulant native lights.
Do not treat Unity realtime light animation as exported functionality. Multiple
independent effect sources need a separate budget/performance pass before content
relies on them.

## Floor contact detail and export preview

Select a stationary horizontal floor mesh and choose **Dreamcast > Lighting >
Add contact shadows to selected floor**. Its Dreamcast Contact Surface component
controls strength, distance (metres), resolution and export-time samples. Start
with strength 0.65, distance 0.55m, 256 pixels, 32 samples. Zero strength disables
the map. This component is separate from the room's coarse vertex AO setting.

The exporter bakes nearby static mesh occlusion into the upward surface's base
RGB565 texture. This gives wall joins and props a narrow contact shadow without
subdividing the whole floor into more polygons. Original base-map UV scale and
offset are sampled into the map. Side/bottom faces keep their original material
mapping. Contact-mapped top faces skip vertex AO to avoid applying it twice;
ordinary lighting, direct shadows and light-effect colors continue to work.

This is artistic contact darkening of the final diffuse surface, not bounced
lighting or a physically correct shadow for each light. Moving players and doors
do not contribute. Receivers must have flat upward faces at one height per
material, opaque Lit materials without emission, and Receive Shadows enabled.
Use separate receivers for different floor heights. Contact textures can soften
fine base-map detail on large floors; use smaller receivers or disable the map
where that matters. Missing/bad setup is reported during export.

64/128/256 square maps cost 8/32/128 KiB each. They share the existing eight-texture,
512 KiB room texture budget, and top faces can add a material part (32 total max).
Replaced, unused original textures are removed before budget checks. No extra
runtime shadow calculations or new runtime format/API is required. The lighting
test room uses one 256 map, totals 384 KiB textures, and keeps its existing
triangle count. Its directional fill is reduced from 0.9 to 0.3; authored ambient
and global AO settings are retained.

**Dreamcast > Lighting > Preview exported room** opens a separate preview. Click
**Bake preview** after editing the scene, select Camera 1/2 and use the light-effect
brightness slider to inspect off/on endpoints. It reads the actual exported
geometry, RGB565 textures and vertex colors, with no Unity scene lights applied.
It neither changes the scene nor writes an export. Moving player/key/door visuals
are omitted. The preview is useful for checking the bake, while final filtering
and display color may differ in Flycast. Export/build the CDI normally afterward.
