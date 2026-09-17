# Portable room audio manifest v2

`sample.audio` accompanies `sample.room`; neither contains Unity asset identifiers.
The single-room starter uses these fixed names. Text is ASCII, whitespace-delimited,
at most 8192 bytes, with invariant-culture finite numbers. Order is significant:

```text
dreamcast_audio 2
clips N
clip 0 PCM_BYTES SHA256_PREFIX.wav
...one clip record per index, ascending from zero...
cue 0 CLIP_OR_MINUS_ONE VOLUME
cue 1 CLIP_OR_MINUS_ONE VOLUME
cue 2 CLIP_OR_MINUS_ONE VOLUME
cue 3 CLIP_OR_MINUS_ONE VOLUME
sources M
source CLIP LOOP VOLUME X Y Z RANGE FULL_VOLUME_RANGE
...one record per placed room sound...
end
```

Cue IDs are footstep (0), key taken (1), locked door (2), door unlocked (3).
`-1` leaves a cue silent. Source clips must exist; sources cannot use `-1`.
`LOOP` is 0 (once on room entry/reset) or 1 (hardware loop). Volume is 0–1.
Range is 0 (room-wide), or a finite positive distance up to 10000. Full-volume
range is finite and nonnegative; it must be smaller than range for nearby sounds
and zero for room-wide sounds. Distances are world units, independent of object
scale. Gain stays at volume inside the inner range, fades linearly between the
radii, and is zero at/beyond the outer range. Distance includes height and uses
the native player position, not the camera or Unity AudioListener.

For nearby sounds, gain is `volume * clamp((range - distance) / (range - fullVolumeRange), 0, 1)`.
It does not pan, occlude or restart at the range boundary. The runtime still accepts
version 1 records, which omit FULL_VOLUME_RANGE and default it to zero, preserving
the original fade. New exports write version 2. The Unity source getter is versioned
as `unity_audio_source_v2` because its struct grew from 28 to 32 bytes.

Clip IDs are unique consecutive indices, at most eight. Filenames are precisely
the first 32 lowercase hexadecimal SHA256 digits plus `.wav`; no paths, spaces or traversal.
The 36-character name fits the CDI Joliet filename limit. Export rejects a prefix
collision between different files; staging verifies the content hash prefix.
Assets live in `room-audio/`. Identical output bytes share one ID. Every clip must
be referenced. Each file is canonical 44-byte-header mono 11025 Hz PCM16, containing
8–65534 samples. Total PCM bytes must not exceed 524288. At most two placed sources
are supported. Four cue records are always present, even for a silent room.

Export and Unity runtime use the C++ `parseAudioData` validator. Simulant uses the
same parser. The build staging step additionally verifies file existence, format,
length and content hash before collecting the bundle. Neither runtime reads Unity
scene objects or AudioSource configuration to determine sound assignments.
