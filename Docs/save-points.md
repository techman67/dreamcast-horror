# Save points: authoring and testing

## Add your own save object

1. Place a stationary book, typewriter or another model in your Unity room.
2. Select it and choose **Dreamcast > Gameplay > Add save point to selected object**.
3. In **Dreamcast Save Point**, set **Prompt** (for example `Use typewriter`) and
   **Interaction Range**. The menu gives the component an unused positive ID.
   If you duplicate an object, give its component a different ID.
4. Optionally create a child at the interaction position and drag it into
   **Interaction Point**. Otherwise the object's origin is used. The cyan selected
   gizmo shows the range from the player's centre. Keep it on the reachable side
   of the object; this slice does not perform line-of-sight tracing through walls.
5. Save the scene and export/build normally. Save-point data is included in the CDI.

No trigger collider, script or Unity Canvas is required. The component does not
add physical collision; use the existing collision authoring for furniture. Up
to 16 stationary points per room are supported, with ranges 0.25-3m and prompts
of 1-47 printable ASCII characters. Invalid settings report their reason on export.
Key/door interactions have priority if their ranges overlap a save point.

The lighting test room includes a small red **Save journal** on the left box.
Move near it and press **E / controller A**. The message confirms whether storage
succeeded. Saved progress contains player position, camera, key possession and
door state. In the 3D controller the player must be grounded. Transient sounds,
movement velocity, lighting phase and completion screens are not saved.

Until the main menu exists, **L / controller Y** loads the newest valid save.
The shipped Flycast keyboard mapping also maps **L** to controller Y; its existing
V mapping still works. Reset (R / Start in Flycast) starts fresh without erasing
the save. There is no automatic save or automatic load on startup.

## Test the complete loop

- Collect the key, visit the journal and save.
- Unlock the door, visit the journal and save again.
- Reset/restart the game, then load. The key remains collected and the door stays
  open; the player returns to the saved position and camera.
- On a test VMU, check missing/full-card messages. Do not remove a card while saving.

Unity stores two files under `Application.persistentDataPath/DreamcastSaves`;
the Console prints the actual folder after a storage operation. Desktop Simulant
uses `.dreamcast-saves` beneath its working directory (tests may override it with
`DREAMCAST_SAVE_DIRECTORY`). Unity and Flycast have separate storage.

Dreamcast uses the first detected memory-card device. It writes `DCHORROR0` and
`DCHORROR1`, each two 512-byte VMU blocks including the save icon/header. Allow
four blocks for both records. These are one progress slot and its backup, not two
player-selectable slots. VMUs without LCDs are still valid memory-card devices.
The CDI uses fixed serial `IND-DCH001` so Flycast's per-game VMU stays consistent
across rebuilds. Older prototype disc IDs are left untouched.

## Recovery and current limits

Each record has a version, room fingerprint, sequence number and checksum. Saving
writes the older/inactive record and reads it back before reporting success.
Loading chooses the newest valid record, falling back if the other is damaged.
This improves recovery; it cannot guarantee survival of interrupted VMU filesystem
metadata writes or damaged hardware. Unrelated VMU files are never deleted.

Single-room exports save their room state. Connected-world exports also save the
current room and every room's prototype key/door flags; loading changes rooms when
necessary. Changes to gameplay layout/cameras/bindings or the world catalogue
invalidate incompatible saves with an explanatory message. Pure lighting or texture
changes do not change the gameplay fingerprint.
Saving at a point intentionally replaces the one current progress slot, including
an older-layout save. There is no save-slot picker, main menu, pause menu or custom
save sound yet. The temporary Load control will move into the menu system.

## Implementation boundary

`Game/Core/SaveData.*` owns the bounded binary formats, validation and alternating
record algorithm. `Game/Compatibility/Game.*` owns proximity, interaction requests,
progress capture and restore. `SharedHost/FileSaveStorage.h` and
`Simulant/sources/RoomSaveStorage.h` are the desktop and KOS storage adapters.
Unity components export `sample.saves`; the shared game never reads Unity objects.

KOS supplies VMU packaging and file I/O. Writes are padded to full 512-byte blocks
before calling `vmufs_write`; storage errors are surfaced instead of reporting
success. No compression, extra framework or runtime gameplay allocation is needed.
Storage itself may allocate and block briefly, so it is only called on an explicit
save/load action, outside the frame simulation.

## Verification

- `Tests/run-tests.ps1 -AddressSanitizer`: shared format/recovery/state tests and
  existing gameplay tests.
- `RoomSaveChecks.Run`: Unity export validation and native PC-file round trip.
- `RoomSavePlayChecks.Run`: actual Play-mode input walks to the sample journal,
  presses E, moves away and presses L to restore; uses an isolated temporary folder.
- `Tests/test_save_bundle.py`: malformed/stale sidecars rejected before staging.
- `Tests/build-vmu-probe.sh`: builds a standalone KOS probe CDI. Use only an isolated
  Flycast installation/VMU: first boot writes records and checks corrupted-record
  recovery; second boot loads persisted progress and writes a receipt. Check that
  test image with `Tests/check_vmu_probe.py <test-vmu-image>`.

The isolated Flycast VMU probe, native tests, Unity checks and desktop Simulant
playback passed on 2026-09-17. Real VMU hardware, unplug interruption and a physically
full VMU have not been tested; full/unavailable/I/O results are handled by the
adapter and failed/full writes are covered by injected storage failures.

## Connected-room extension

See [connected rooms](connected-rooms.md) for authoring destinations and arrivals.
Connected-world saves now include the current room and every room's prototype key/door
flags. Loading can change rooms through the same unload-before-load flow. DSV2 payloads
are 72 bytes; VMU storage remains four blocks total.
