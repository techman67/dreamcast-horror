# AC unit texture sample

The user supplied `airConditionner.fbx`; the original Desktop file is unchanged.
Its source/license was not provided. Keep that provenance with any eventual release.
No Blender source was supplied; future model edits should have a canonical .blend source.

## Assets

- `ac-painted-metal-source.png`: original generated painted gray-green metal.
- `ac-dark-metal-source.png`: original generated dusty charcoal metal.
- `ac-preview-0.png`, `ac-preview-1.png`: Unity renders of the supplied FBX with the materials applied.
- Unity prefab: `Assets/Art/AirConditioner/AirConditioner.prefab`.
- Unity textures: `Assets/Art/AirConditioner/ac-painted-metal.png` and `ac-dark-metal.png`.
- Both Unity PNGs are 256x256, opaque, diffuse color only. Bilinear filtering, repeat wrapping, no mipmaps.
- Two RGB565 textures at these dimensions would require 256 KiB total pixel storage, before overhead. The static prop exporter now converts/loads RGB565 for Simulant/Dreamcast; Unity's texture memory differs.

The original material assignments are preserved. The supplied FBX had 92 collapsed-UV triangles; the prefab now references derived `AC_UV_*.asset` meshes with face-projected UVs. Geometry, normals and transforms are unchanged. The original FBX is retained. The model is centered at its base inside a placement prefab. No collision or sound is attached. The materials use Unity URP Lit for preview, with no metallic response or smoothness; their base-color images are consumed by the portable static mesh exporter. The user has placed the AC in SampleScene; the static prop exporter now includes its mesh and textures in the CDI.

## Generation

Created with the built-in image generation tool. The two generation prompts are recorded in `texture-prompts.txt`. Unity's standard texture import pipeline produced the 256x256 copies. These are general material swatches, not custom baked ambient occlusion or edge wear mapped to each face.

## UV repair

`air-conditioner-uv-fixed.obj` and its `.mtl` are portable repair snapshots for
Blender import (Y-up Unity model coordinates, units unchanged). Save future model
edits as a canonical .blend source. `AcUvRepair.Run` regenerates the derived
Unity mesh assets and repair snapshot; the prefab keeps its original hierarchy
and materials. UVs use planar projection per face for the tileable material
swatches. This removes stretched lines without changing triangle count.
The earlier `ac-preview-*` images show the original mapping;
`ac-simulant-uv-fixed.png` shows the corrected Simulant close-up.
