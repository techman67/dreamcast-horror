# Connected-room checkpoint

The lighting, save-point and connected-room changes are ready for source control.
The reproducible source snapshot from before the review is kept locally under
`Simulant/build/checkpoints/connected-rooms-20260917.zip` (ignored by Git).

## What is implemented

- Unity room catalogue, named arrivals and press-to-enter links, with scene pickers
  and menus under Dreamcast > Rooms.
- Shared engine-neutral room progress, door gating, save-point interaction priority,
  cross-room save/load and session restart.
- Simulant loading scene waits for old-room destruction before allocating the next.
- Bounded per-room geometry, texture, lighting-effect and audio budgets.
- Versioned 72-byte saves, with DSV1 compatibility and rotating KOS VMU records.
- Exact full-path/content checks for connected-room files in the packaged CDI.

## Review fixes

The shared catalogue treats scene references as optional host metadata rather than
requiring Unity paths. Invalid world-save cameras are rejected before changing rooms.
Unity shows destination-load errors. Export cancellation aborts a build. Git preserves
room bytes so cross-platform checkout does not invalidate manifest/save fingerprints.
The Unity save Play test now exports its own fixture after the stairs tests.

## Reproduce checks

- `Tests/run-tests.ps1 -AddressSanitizer`
- `Tests/run-unity-checks.ps1`
- Unity batch methods `RoomWorldDemo.BuildCheck` and `RoomWorldPlayChecks.Run`
- `Tests/test_world_bundle.py`, `test_save_bundle.py`, `test_audio_bundle.py`,
  `test_prop_bundle.py`
- `Simulant/run.ps1 build` and `package`
- Desktop Simulant with `SIMULANT_WORLD_CHECK=1` exercises twelve scene transitions.
- `Tests/build-vmu-probe.sh` and `check_vmu_probe.py` verify isolated VMU write/read,
  damaged-record recovery and reboot persistence. Never use a user's VMU for this.

## Remaining hardware validation

The complete connected-room CDI still needs interactive Flycast testing and measured
Dreamcast-backend memory stability over repeated transitions. The isolated VMU probe
passes in Flycast, but does not validate the entire game's room-loading integration.
Real Dreamcast RAM/VRAM/AICA limits, GD-ROM seek times and physical VMU interruption
behaviour remain unverified. Desktop rendering heap includes caches and is not a
proxy for console memory usage.

Start with `Assets/Scenes/ConnectedRoom01.unity`; walk right to the blue marker and
press E/A. The amber marker in room 2 returns. See [authoring](connected-rooms.md).
Use the second monitor for visible testing; the user may be using the main monitor.

## Review validation on 2026-09-17

All listed native and Python checks passed. The full Unity regression run passed
except its save-play fixture dependency; after fixing that setup, the save-play
test passed on rerun. World export and live world save/load also passed with the
rebuilt plugin. Desktop Simulant passed both legacy gameplay and twelve world
transitions. Dreamcast compilation and exact CDI content checks passed. The isolated
VMU probe passed reboot persistence. Staged Git blobs preserve every room hash.
