# Dreamcast platform facilities review — 2026-09-17

Research only; no runtime changes. Verified against installed Simulant revision
`4a5f5799d0232c1a509b186262a214da0f138894` and headers inside the pinned SDK image
`kazade/dreamcast-sdk@sha256:7202a5d5d007bcb7802c48f1bf30b22d03e337927543b5396130f8420c6f37ec`.

## VMU storage

KallistiOS already supplies the platform operations:

- `vmufs_read` / `vmufs_write`: read/write a selected memory card's file.
- `vmufs_free_blocks`: check free space (confirmed in installed SDK header).
- `vmu_pkg_build` / `vmu_pkg_parse`: construct/parse the Dreamcast save header,
  including metadata, icon data and CRC. Installed parse signature includes
  the input buffer length; do not copy older two-argument examples.
- Discover storage devices using the Maple memory-card capability. Do not use
  Simulant's LCD screen discovery as the memory-card inventory.

The installed Simulant source has VMU LCD support in
`simulant/platforms/dreamcast/kos_window.cpp`, but no references to `vmufs_*`,
`vmu_pkg_*` or `MAPLE_FUNC_MEMCARD`. There is no automatic game-state saving
facility established by this review. `Path::system_temp_dir()` returns `/ram`
on Dreamcast; that is temporary storage, not a persistent save location.

Proposed integration: shared, versioned game-state bytes, passed to a small host
storage adapter. The Dreamcast adapter packages/writes them through KOS; the
Unity/desktop adapter writes the same payload to a local file. Game code remains
free of KOS and Unity types. Save timing, fields, validation, missing/full-card
messages and recovery behavior still belong to the application.

KOS documents overwrite as deleting the previous file before writing the new
one. Do not equate its internal atomic operation with power-loss-safe saving.
Evaluate two alternating small save records with sequence numbers and validation,
retaining the previous valid record until the new record is read back successfully.
No save capacity or resilience promise is made before implementation/testing.

The user's Ocarina port source was not identified in this review. Its apparent
automatic saving may come from a port-specific storage adapter; that remains an
inference, not a verified claim about that project.

## Existing facilities worth reusing

| Facility | Verified installed implementation | Remaining game work |
| --- | --- | --- |
| Room lifecycle | SceneManager defaults to `ACTIVATE_BEHAVIOUR_UNLOAD_FIRST`; scenes default to unloading on deactivation. Scene unload calls shared asset GC. | Multi-room export/packaging, destinations/spawns, fade, retained progress, releasing our audio/mesh references and measuring repeated transitions. |
| VMU LCD | KOSWindow detects LCD devices and queues screen updates; texture-to-VMU-image helper exists. | Optional logo/status presentation. This is separate from save storage. |
| Rumble | GameController start/stop API is connected to KOS Puru Puru discovery and calls. | Game feedback events, absent-accessory handling and real accessory validation. |
| Disc assets | Our room/prop host reads `/cd/assets`; current CDI packaging includes external assets. | Package and select more than the hard-coded sample room. |

Source locations inspected in installed engine: `simulant/scenes/scene_manager.h`,
`simulant/scenes/scene.h`, `simulant/scenes/scene.cpp`,
`simulant/platforms/dreamcast/kos_window.cpp`, `simulant/input/input_state.h`,
`simulant/utils/dreamcast.h`, `simulant/path.cpp`.

## Documentation qualification and recommended sequence

The Simulant Dreamcast guide claims all assets are embedded into the executable.
That does not describe this project's verified disc-loading path. Use the actual
pinned implementation and our packaged build when a guide disagrees with them.
Do not preload two full rooms merely because background preloading is available.

A small save/load proof is now a reasonable first step: save the current key/door
progress, restart the game, restore it, then test missing/full/invalid saves.
Follow with two linked rooms using unload-first loading and the same persistent
state format. Rumble and VMU LCD feedback are smaller optional additions.

Sources:
- [KOS VMU filesystem](https://kos-docs.dreamcast.wiki/group__vfs__vmu.html)
- [KOS VMU package header](https://kos-docs.dreamcast.wiki/vmu__pkg_8h_source.html)
- [Simulant Dreamcast guide](https://simulant.dev/docs/guides/dreamcast.md)
- [Pinned Simulant source](https://gitlab.com/simulant/simulant/-/tree/4a5f5799d0232c1a509b186262a214da0f138894)
