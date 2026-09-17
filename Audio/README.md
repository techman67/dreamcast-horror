# Author and export room audio

Unity is the editing interface; exported audio data and WAVs are the runtime source
for both Unity Play mode and Simulant. No Unity audio component or asset GUID is
required by shared C++ or the Dreamcast build.

## In Unity

Open `Assets/Scenes/SampleScene.unity`. The sample is already configured:

- The player has a **Dreamcast Sound Cue** for footsteps.
- The key has the key-pickup cue.
- The door has locked and unlocked cues.
- **Room Ambience** has a **Dreamcast Room Sound** component.
- Source clips are in `Assets/Audio`.

To assign an effect, select its object and drag a source WAV/AudioClip onto its
Dreamcast Sound Cue. Choose the event and volume. The current slice supports one
assignment for each of its four events; putting a cue on a different object does
not create another door or change the shared gameplay rules. Remove/disable the
component to leave that event silent.

To place a room sound, use **GameObject > Dreamcast > Placed Room Sound**, move the
object, and assign its clip. Set:

- **Volume:** 0–1.
- **Loop:** continuous playback, or a single play when entering/resetting the room.
- **Sound coverage:** Nearby only for an AC or machine; Whole room for ambience.
- **Full volume distance:** the green inner radius. The chosen volume stays constant inside it.
- **Silent beyond distance:** the cyan outer radius. Sound fades between the radii and is silent outside.

Select the sound object and drag the colored radius handles in Scene view, or
enter the distances in the Inspector. Handle edits support Undo and prefab
instance overrides. Distances use world units, regardless of object scale.
New sounds default to a 0.5-unit full-volume radius and a 2-unit outer radius.
Existing sources keep their previous center-to-edge fade until an inner distance
is set. Whole-room sounds have no distance handles and are clearly labeled.
Full volume distance must be nonnegative and smaller than the outer distance.

Range is distance attenuation, not stereo panning, occlusion or reverb. Rotation
and scale do not affect it. Leaving range does not restart the sound; loops keep
running silently so re-entering range preserves their phase. A one-shot plays on
entry even if the player starts out of range.

Use **Dreamcast > Export sample room** before Play mode to preview the same data
that Simulant will receive. Runtime deliberately does not read live authoring
components. Ordinary Unity AudioSources are rejected at export; their settings
are not silently translated.

Use **Dreamcast > Export room and build CDI** to export, collect the audio and build
`FlyCast/dreamcast-horror.cdi`. Progress/completion appears in the Console;
the full build log is `Simulant/build/unity-cdi-build.log`. Close Flycast before
replacing a disc image it is using. Existing WSL/Simulant installation is required.
The shell `Simulant/run.ps1 package` builds the last export without needing Unity.

## Supported source files and limits

Audio compatibility checks run automatically when source audio is imported (the
generated StreamingAssets bundle is excluded). The Console report names the asset,
detected codec/bit depth/channels/rate, duration, target sample count and PCM memory.
It distinguishes automatic conversions from errors and gives a suggested fix.
Toggle this through **Dreamcast > Audio > Automatic import checks**.

For an on-demand report, select a clip or audio folder in Project and choose
**Dreamcast > Audio > Check selected audio**. **Check room audio** validates all
active assignments and the aggregate memory budget without exporting. Both
Dreamcast sound components also show a cached compatibility report in the Inspector.

When duration is the only compatibility problem, the report and sound Inspector
show **Make shortened test copy (~5.94 seconds)**. This writes a uniquely named
`-dc-test.wav` beside the original and selects it in Project. Drag the new clip
into the sound component, then export. The original and existing assignments are
preserved. It retains the first complete PCM frames, preserving source rate,
channels and bit depth; normal export performs the Dreamcast conversion. It does
not repair loop seams, apply fades, or fix unsupported formats/codecs. Room checks
also show per-file reports for invalid assigned sounds, including this option.

Diagnostics distinguish a too-long clip, unsupported container, unsupported codec
inside a WAV (such as floating-point or ADPCM), bit depth, channel count, sample
rate, malformed/truncated data and oversized source files. Reports include actual
values and limits. For example, a six-second clip reports 66,150 target samples
against the 65,534-sample limit, explains that this backend uses resident playback,
and recommends a shorter effect/loop. This is not a claim that Dreamcast cannot
play longer audio using a future streaming implementation. A `.wav` extension does
not guarantee that its codec is PCM; renaming an MP3 will not make it usable.

Use standard RIFF PCM WAVs: 8/16-bit, mono/stereo, 8–48 kHz, under `Assets/`.
Export reads original file bytes independently of Unity import settings. It
averages stereo to mono and resamples with a windowed-sinc low-pass filter to
11025 Hz PCM16. Canonical 11025 Hz mono PCM16 input preserves samples exactly.
Compressed WAV/MP3/OGG and unsupported formats fail with a diagnostic; they are
not silently omitted. Source files above 16 MiB are rejected before reading.

- Up to **8 unique clips**, deduplicated by exported content.
- At most **65,534 samples per clip** (about 5.94 seconds at 11025 Hz).
- **512 KiB total unique PCM payload** for the room.
- Up to **2 placed room sounds**.
- **8 reserved playback voices:** two placed sounds, one footstep, five interaction
  effects. Extra effects are dropped while those slots are occupied.
- Volume is quantized to AICA's 256 levels on both hosts.

These limits deliberately support the short resident-sound slice, not streamed
music or long ambience. Source recordings still need an appropriate loop seam;
the exporter cannot make an arbitrary recording loop seamlessly.

## Bundle and backend ownership

Export writes `StreamingAssets/sample.room`, `sample.audio`, and SHA256-prefix-named WAVs
under `StreamingAssets/room-audio`. The audio manifest holds clip references,
volumes, source positions, ranges and loop flags. Invalid authoring is rejected
before replacing the export. Content-addressed clips are written before metadata;
older unreferenced exported clips may remain in Unity, but are not packaged.

The CDI staging tool verifies each referenced file's format, size and SHA256 prefix before
updating its staging area. Removed references remove stale staged audio. It never
collects arbitrary files from `Assets/Audio` or the repository's `Audio` folder.
The portable native parser checks indices, finite values, limits and safe names.
See `Docs/audio-format.md` for the versioned schema.

Shared C++ emits a fixed-capacity batch of sound events. Footsteps follow actual
resolved travel: the first movement after reset starts with a step, then steps
repeat every 0.65 units accumulated across stops and starts. Releasing a movement
key does not restart the stride.
A 0.25-second minimum interval prevents rapid tapping from spamming sounds;
blocked movement stays silent. Key/door rules emit interactions exactly once. Playback,
asset loading and distance attenuation are host presentation responsibilities.

Unity uses eight AudioSources and decoded float clips. Desktop Simulant uses
static OpenAL buffers in its existing audio context. Dreamcast uses KOS resident
AICA samples, hardware looping and eight channels reserved through the same
allocator used by ALdc. This avoids the queued-buffer transitions that produced
the earlier tick. No new library, modified SDK or game-core dependency is needed.

Dreamcast memory remains 16 MiB main RAM, 8 MiB VRAM and 2 MiB sound RAM. Each unique
sample is uploaded once to AICA and shared by voices; the 512 KiB payload ceiling
excludes small alignment/driver overhead. PCM loading uses bounded temporary main
RAM. Startup logs heap allocation and free AICA memory, but full peak memory/CPU
profiling on actual hardware is still pending. Unity's float clip memory is not
a Dreamcast memory measurement.

## Placeholder generation and verification

`generate.py` creates the five original procedural sounds in this folder using
Python's standard library. `stage.py` copies them to **Unity's authoring folder**;
export in Unity afterward. These five generated sounds total 127,668 PCM bytes.
The current scene also references the user's downloaded AC recording, bringing
the exported bank to 258,736 bytes. Its original and shortened test copy are under
`Assets/Audio/window-ac-unit-hum-c1fed12a*.wav`; the source/license was not supplied.
The five generated effects remain placeholders, especially the
locked-door cue, which is distinct from footsteps but not a realistic recording.

Native tests cover game events, the audio manifest ABI, bounds and distance gains.
Unity checks cover assignment changes, deduplication, placement, loop/volume/range,
PCM conversion, specific diagnostic reasons and runtime playback/reset/voice limits. Packaging tests
cover missing/corrupt files and removal of stale dependencies. Simulant tests run
the complete room route and audio burst/reset checks. Audible playback is checked
separately in Flycast; emulator success does not certify real hardware performance.

The package command also checks exact audio filenames and WAV lengths in the
finished CDI Joliet directory records before publishing the disc. Audio uses
36-character filenames (32 SHA256 prefix digits plus `.wav`) to prevent the
silent filename truncation seen with full 64-digit hashes.

After the disc-safe filename fix, the user confirmed in Flycast that the rebuilt
room loads and sounds work (2026-09-16). Real Dreamcast hardware remains untested.
