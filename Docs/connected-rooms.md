# Connected rooms

Unity authors scenes; the shared C++ game chooses links, arrivals and persistent
room progress. Simulant loads the exported room bundles from the disc. The first
version supports eight rooms, eight arrivals and eight links per room. These are
deliberate slice limits, not claims about the Dreamcast's maximum world size.

## Try the example

Open `Assets/Scenes/ConnectedRoom01.unity` in Unity and press Play. Walk right toward
the blue marker and press **E** (controller **A**). Room 2 has an amber marker that
returns to room 1. Each arrival starts at the room's centre. Input pauses during
the short fade and loading screen.

The existing journal still saves with E/A. **L/Y** loads the latest save, including
its room. Room changes retain each room's key/door state in memory but do not
automatically write to the VMU. Keys belong to their room's prototype objective;
this is not yet a general inventory shared between unrelated puzzles.

The example copies the lighting scene; it does not replace the original. The menu
**Dreamcast > Rooms > Create two-room example** refuses to overwrite an existing
example. On Simulant, **R/Start** starts a fresh session in the world's starting room;
it does not erase the saved game.

## Make your own connections

1. Create a world asset with **Assets > Create > Dreamcast > Rooms > World**.
   Add your saved scenes to its list using the scene pickers. Assign unique room
   numbers 1–8 and choose the starting room.
2. In each scene, use **Dreamcast > Rooms > Add room settings**. Assign the world
   asset and that scene's room number. Keep the existing exported-player/camera
   setup required by the room exporter.
3. Use **Dreamcast > Rooms > Add arrival marker**. Give it a name such as
   `FromHallway`, choose camera 1 or 2, and position it at the player's feet, clear
   of walls. With 3D character physics, place it on top of a collision box.
4. Use **Dreamcast > Rooms > Add room link marker** beside your doorway. Set the
   destination room number and the exact arrival name in that room. Its yellow
   sphere measures distance from the player's feet. Players press E/A within it.
   Enable **Requires Open Door** only for a link gated by this room's existing
   prototype key/door objective. Use static markers beside moving doors.
5. Save the scenes. **Dreamcast > Rooms > Export connected rooms** exports the
   complete catalogue. **Dreamcast > Export room and build CDI** also exports the
   connected catalogue when the active scene contains room settings.

Names/references, marker positions, arrival collision and per-room geometry,
texture and audio budgets are checked before publishing the world export.
Export failures identify the problem; a failed build-stage validation preserves
the previously staged bundles. Scene names/paths should use ASCII characters and
fit within the manifest's 95-character path limit.

Unity Play mode can start in either configured scene. Simulant starts at the
world asset's starting room. Unity's editor loads scene paths through its editor
scene API; a standalone Unity player also needs these scenes in its build settings.

## Dreamcast loading and storage

- Bundles live at `assets/world/<room number>/` on disc. The catalogue is a bounded
  binary `sample.world`; there is no Unity runtime dependency in the game code.
- Simulant fades out, activates a small loading scene, releases room audio and
  scene references, waits across the deferred scene-destruction boundary, and only
  then activates the destination. A runtime check rejects overlapping RoomScenes.
- Each room retains the existing 4096-triangle, 32-part, 512 KiB exported-texture
  and 512 KiB PCM-bank ceilings. Those limits are not a complete RAM/VRAM guarantee;
  engine overhead and temporary loader allocations still count.
- Only a compact fixed-capacity progress table survives a room change. No second
  room's mesh, texture or audio bank is preloaded behind the fade.
- DSV2 save payloads are 72 bytes. Both rotating VMU files still occupy two blocks
  each (four total). The reader accepts previous DSV1 single-room records; those
  do not become connected-world saves. Catalogue/gameplay changes can invalidate
  a save rather than loading it into incompatible rooms.

RAM/AICA/VRAM snapshots are logged around loading on Dreamcast builds. Desktop
Simulant tests verify scene lifetime, not actual GD-ROM seek times or console memory
behaviour. Physical Dreamcast testing remains necessary before raising budgets or
claiming final loading performance.

## Verification checkpoint

Core tests, Unity live transitions and cross-room save/load, twelve desktop Simulant
transitions, Dreamcast compilation and exact disc-file checks passed. The complete connected-room CDI still needs interactive Flycast and physical
Dreamcast memory/loading checks. An isolated KOS VMU probe passed write/read,
backup recovery and reboot persistence with the DSV2 payload.
