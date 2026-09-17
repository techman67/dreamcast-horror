# Portable character collision: stairs first

The v3 controller is shared C++ in `Game/Gameplay/Player.cpp` and
`Game/Compatibility/CharacterCollision.cpp`. Unity and Simulant call the same
code. It does not use Unity Physics or Simulant rigid bodies. This is a static-world
character controller, not a general rigid-body simulator.

## Testing and authoring

Open **Dreamcast > Open stairs test room** in Unity. This opens the separate
`Assets/Scenes/PhysicsTestScene.unity`; the original SampleScene is unchanged.
Use **Dreamcast > Export room and build CDI** to export the active scene and
replace `FlyCast/dreamcast-horror.cdi`. Both scenes share that export destination:
export SampleScene again to return to the original key/door/AC room.

The test starts facing six 25 cm steps. W climbs them; S walks down. From the
landing, A walks off the clear left edge and gravity lands the player on the floor.
At the top, approach the right edge and hold D while pressing Space to cross
the gap onto the higher slab. The slab also serves as a low ceiling underneath.
R / Start resets in Simulant/Flycast. The right side contains a 40 cm obstacle
that is too high to step onto and a low-ceiling obstruction. The tall obstacle
on the left exercises gentle corner steering. Controls remain world-relative.

For a new scene, add **Dreamcast Character Physics** to its CppPlayerVisual.
Default standing height is 1.8 m and maximum step height is 0.3 m. Select the
player to see its cyan collision volume. Its existing visual pivot offset still
controls the authored feet position; match visual scale/offset to your chosen
height. Add CppCollision boxes to floors, individual stair treads, landings,
walls, and ceilings. Floors in the original flat sample were decorative and
must gain collision when converting that scene. Use separate axis-aligned box
proxies for irregular or rotated art. Export rejects rotated collision objects,
spheres/capsules, invalid dimensions, and a spawn overlapping a solid, with an
explanation. No Unity Collider is required.

## Behavior and limits

- Upright box: width/depth twice the authored radius, height above feet. Swept
  XYZ collision prevents thin-wall tunneling and permits walking below platforms.
- Gravity is 9.8 m/s², capped at 12 m/s falling speed. Rendering updates consume
  at most 0.1 seconds in up to six 1/60-second substeps; a hitch slows simulation
  rather than producing unbounded work or a teleport.
- Step-up requires ground contact, sufficient clearance for the full authored
  step height, and a valid landing. Taller obstacles block movement. Descending
  stairs snap down by at most one step; larger drops fall. Space / controller B (east face button) jumps from the ground.
  Holding the button does not repeat jumps; there are no midair jumps. Upward
  speed is 5 m/s (about 1.23 m height and 1 m travel at the current walking speed).
  Head contact cancels ascent. Jumping is enabled only for v3 rooms.
- Wall sliding and gentle corner steering are swept against the full standing
  volume. Steering considers one nearby tall corner after stair resolution;
  low stair treads are excluded. Airborne movement does not emit footsteps.
- Up to 128 static boxes, four contact passes per sweep, fixed storage and no
  per-movement allocation. Shape iteration is linear: there is no spatial index
  yet. Physical Dreamcast frame cost still needs hardware profiling.
- Bounds are limited to 10,000 m, height 0.5–4 m, radius >0–1 m, and step height
  0–0.5 m below standing height. These are validation limits, not suggested
  room sizes. Small rooms and conservative box counts remain the target.
- No slopes/triangle meshes, moving platforms, dynamic rigid bodies, pushing,
  ladder climbing yet. Ladder teleport is deferred. Production
  meshes continue to originate in Blender; test primitives are only fixtures.
- V1/v2 rooms retain their existing flat collision and corner behavior. V3
  key/door prompts include vertical distance; exit crossing is height-gated
  relative to the door interaction range, not a general volume trigger.

## Verification

`Tests/run-tests.ps1 -AddressSanitizer` checks shared collision/gravity, ceilings,
stairs, ledges, excessive steps, corner steering, frame timing, reset, airborne
audio suppression, height-aware interactions, and malformed v3 input. Legacy
room tests use `Tests/Fixtures/sample-v2.room` so selecting a different exported
scene does not invalidate their fixture.

`Tests/run-unity-checks.ps1` temporarily exports the original room for its existing
regressions, checks the stairs scene through the native DLL, and restores the
previous three bundle files. `RoomPhysicsSetup.BuildAndCheck` creates the test
scene if absent, checks it, and exports it. Simulant's `check` playback selects
the stair route for v3 and the original objective route for v2.

The stairs scene has a Unity-editor-only movement status strip during Play.
It reports whether the game is initialized/paused/focused, the movement received
by NativeGameBridge, and the visual player position. It is excluded from builds.
The Unity regression runner also feeds WASD through real MonoBehaviour Update
and LateUpdate in Play mode (`RoomPhysicsInputChecks`), not only the native DLL.
