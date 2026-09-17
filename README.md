# Dreamcast Horror

Engine-neutral C++ gameplay, developed through a Unity Windows host. Sega
Dreamcast is the target. The Simulant backend runs on desktop and in Flycast;
real-hardware performance and peak memory remain to be validated. See [the architecture](Docs/compatibility-api.md) and
[the room authoring workflow](Docs/room-format.md).

The [Simulant room host](Simulant/README.md) runs the same exported room and C++
gameplay, with Windows launch and Dreamcast build commands.

From the repository root in PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/run-tests.ps1
.\Unity\Dreamcast-Horror\build_unity_plugin.bat --non-interactive
```

Rebuild the DLL after every C++ change; Unity does not compile native sources.
The build requires Visual Studio C++ x64 tools. Native tests compile all current
Game sources without Unity and leave their binaries in a unique temporary folder.
Pass `-AddressSanitizer` to the test script for instrumented memory-safety checks.

For Unity integration checks (Unity 6000.6.0f1, project closed in other editors):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -projectPath "$PWD\Unity\Dreamcast-Horror" -executeMethod ArchitectureChecks.Run -logFile "$env:TEMP\dreamcast-checks.log"
```

The check exits with code 0 on success and 1 on failure. Its log contains
`ARCHITECTURE CHECKS PASSED` when scene, export and native integration checks pass.
It does not substitute for a visual playthrough or Dreamcast hardware testing.

With Unity closed, run `Tests/run-unity-checks.ps1` for all seven authoring and
integration check groups. It writes per-check logs in a unique temporary folder
and stops on failure. It re-exports the saved sample without saving test scene edits.
Run `python Tests/test_audio_bundle.py` and `python Tests/test_prop_bundle.py`
for the staging regressions. See [the repository review](Docs/review-2026-09-16.md)
for the checked scope, fixes and remaining limitations.

## Play the key-door slice

Open SampleScene and press Play. Move with WASD/arrows, the controller left stick
or D-pad. Interact with E or the controller south face button (Xbox A / PlayStation
Cross). Try the south door, find the brass key in the north half of the room,
return to unlock the door, and walk through to escape. Stop and start Play to reset.

The door opens immediately; models are temporary primitive proxies. C++ owns the
objective and collision changes. Export the room after editing the key, door,
exit, collision, spawn or cameras. Data-only edits do not require a DLL rebuild.

Run `KeyDoorChecks.Run` using the same Unity batch command as above for Play-mode input and
key-door integration coverage. Run `KeyDoorPlaythrough.Run` without `-nographics`
for an automated keyboard playthrough and camera images in the temporary directory
reported in its log. These images exclude the on-screen text overlay.
Version 1 room files still load without an objective;
the original key-door sample is version 2; the active stairs/jumping test export is version 3. See the room format document before editing by hand.

## Author audio and build a disc

The sample has editable Dreamcast Sound Cue components on its player, key and
door, plus a Room Ambience object. Add placed sounds through
**GameObject > Dreamcast > Placed Room Sound**. Source audio lives under `Assets/`;
export handles supported PCM WAV conversion and collection.

Use **Dreamcast > Audio > Check selected audio** for a compatibility report, or
**Check room audio** for the complete budget. Import checks run automatically and
the sound inspectors explain unsupported files with actual values and fixes.

**Dreamcast > Export room and build CDI** exports gameplay/audio together and
builds `FlyCast/dreamcast-horror.cdi`. No manual sound-copy step is required.
See [the audio authoring guide](Audio/README.md) for supported settings and limits.

## Static model props

Active imported static models (including the AC prefab) now export with their
placement, opaque base-color textures and material tint. The same CDI command
collects them automatically. See [static prop export and budgets](Docs/static-props.md).

Static room surfaces and props can now receive [baked vertex lighting](Docs/baked-lighting.md)
from Unity Directional, Point and Spot lights. The sample includes a warm wall
lamp beside the AC. Export/Rebuild CDI bakes the colors with no real-time lighting
cost or additional lighting textures. Cast shadows and moving-object lighting
are not implemented in this pass.
The static pass supports opaque props; animated characters and dynamic
key/door model bindings remain future work.

For stairs, gravity, jumping, collision limits and test controls, see
[character physics](Docs/character-physics.md). Open **Dreamcast > Open stairs test room**
in Unity; Space / controller B jumps. Export the scene you want before playing
or rebuilding the CDI, since the scenes share one export destination.
