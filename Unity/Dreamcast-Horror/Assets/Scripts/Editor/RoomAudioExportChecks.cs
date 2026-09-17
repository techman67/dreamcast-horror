using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoomAudioExportChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action) {
        bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Invalid audio export was accepted.");
    }
    public static void Run()
    {
        GameObject temporary = null;
        try {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            string exported = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "sample.audio"));
            var baseline = RoomAudioExporter.Build();
            Check(baseline.text == exported, "Saved scene and exported manifest disagree.");
            // Validate the real scene first, then leave one source active for
            // the two-source placement fixture. Never save these test edits.
            var placedSounds = UnityEngine.Object.FindObjectsByType<DreamcastRoomSound>().Where(x => x.isActiveAndEnabled).ToArray();
            Check(placedSounds.Length > 0, "The sample needs a placed sound for this fixture.");
            var placed = placedSounds[0];
            foreach (var sound in placedSounds.Skip(1)) sound.enabled=false;
            baseline = RoomAudioExporter.Build();
            int clipCount=baseline.files.Count;
            var cues = UnityEngine.Object.FindObjectsByType<DreamcastSoundCue>();
            var foot = cues.Single(x => x.cue == DreamcastCue.Footstep);
            var key = cues.Single(x => x.cue == DreamcastCue.KeyTaken);
            AudioClip original = foot.clip;
            foot.clip = key.clip;
            var reassigned = RoomAudioExporter.Build(); var remapped = NativeAudioData.Parse(reassigned.text);
            Check(remapped.cues[0].clip == remapped.cues[1].clip && reassigned.files.Count == clipCount-1,
                  "Changing a scene assignment did not update/deduplicate the export.");
            foot.clip = original;
            temporary = new GameObject("Audio export test source");
            var source = temporary.AddComponent<DreamcastRoomSound>();
            source.clip = placed.clip; source.loop = false; source.volume = .25f; source.audibleRange = 8; source.fullVolumeRange = 0;
            source.transform.position = new Vector3(3, 2, -1);
            var bundle = RoomAudioExporter.Build(); var parsed = NativeAudioData.Parse(bundle.text);
            Check(bundle.files.Count == clipCount && parsed.sources.Length == 2, "Placed source duplicated the audio bank.");
            var second = parsed.sources.Single(x => x.loop == 0);
            Check(second.position == source.transform.position && second.range == 8 && second.volume == .25f,
                  "Placed sound settings were not exported.");
            Check(Mathf.Abs(second.Gain(second.position + new Vector3(4,0,0)) - .125f) < .0001f,
                  "Distance fade differs from the portable definition.");
            source.clip = null; Reject(() => RoomAudioExporter.Build()); source.clip = placed.clip;
            source.volume = float.NaN; Reject(() => RoomAudioExporter.Build()); source.volume = .25f;
            var duplicate = temporary.AddComponent<DreamcastSoundCue>(); duplicate.cue = DreamcastCue.Footstep; duplicate.clip = original;
            Reject(() => RoomAudioExporter.Build()); UnityEngine.Object.DestroyImmediate(duplicate);
            source.enabled = false;
            Check(RoomAudioExporter.Build().text == baseline.text, "Disabled sounds should not export.");
            Check(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "sample.audio")) == exported,
                  "Validation modified the previous export.");
            UnityEngine.Object.DestroyImmediate(temporary); temporary = null;

            string wavPath = Path.GetTempFileName();
            try {
                using (var stream = File.Create(wavPath)) using (var w = new BinaryWriter(stream)) {
                    const int frames = 4800;
                    w.Write(0x46464952u); w.Write(36 + frames * 4); w.Write(0x45564157u);
                    w.Write(0x20746d66u); w.Write(16); w.Write((ushort)1); w.Write((ushort)2);
                    w.Write(48000); w.Write(192000); w.Write((ushort)4); w.Write((ushort)16);
                    w.Write(0x61746164u); w.Write(frames * 4);
                    for (int i = 0; i < frames; ++i) { w.Write((short)1000); w.Write((short)3000); }
                }
                byte[] pcm = RoomAudioExporter.ConvertWav(wavPath);
                Check(pcm.Length == 44 + 1102 * 2, "Resampling length mismatch.");
                Check(BitConverter.ToInt16(pcm, 44 + 500 * 2) == 2000, "Stereo downmix is not averaged.");
                var validSource = File.ReadAllBytes(wavPath);
                Check(DreamcastAudioValidator.Analyze(wavPath).Usable, "Convertible PCM source was rejected.");
                var trailing=new byte[validSource.Length+3]; Array.Copy(validSource,trailing,validSource.Length);
                Array.Copy(BitConverter.GetBytes(trailing.Length-8),0,trailing,4,4);
                File.WriteAllBytes(wavPath,trailing);
                Check(DreamcastAudioValidator.Analyze(wavPath).Describe().Contains("Incomplete trailing WAV"), "Incomplete trailing chunk was accepted.");
                void Diagnostic(int offset, byte[] value, string expected) {
                    var altered = (byte[])validSource.Clone(); Array.Copy(value, 0, altered, offset, value.Length);
                    File.WriteAllBytes(wavPath, altered);
                    var report = DreamcastAudioValidator.Analyze(wavPath);
                    Check(!report.Usable && report.Describe().Contains(expected), "Missing specific diagnostic: " + expected);
                }
                Diagnostic(20, BitConverter.GetBytes((ushort)3), "Wrong codec: detected IEEE floating-point");
                Diagnostic(34, BitConverter.GetBytes((ushort)24), "Unsupported bit depth: 24-bit");
                Diagnostic(22, BitConverter.GetBytes((ushort)6), "Unsupported channel count: 6");
                Diagnostic(24, BitConverter.GetBytes(96000), "Unsupported source sample rate: 96000");
                var longSource = new byte[44 + 48000 * 6 * 4]; Array.Copy(validSource, longSource, 44);
                Array.Copy(BitConverter.GetBytes(longSource.Length - 8), 0, longSource, 4, 4);
                Array.Copy(BitConverter.GetBytes(longSource.Length - 44), 0, longSource, 40, 4);
                File.WriteAllBytes(wavPath, longSource);
                Check(DreamcastAudioValidator.Analyze(wavPath).Describe().Contains("Too long: 6.000 s"), "Duration diagnostic missing.");
                File.WriteAllBytes(wavPath, new byte[12]); Reject(() => RoomAudioExporter.ConvertWav(wavPath));
                Check(DreamcastAudioValidator.Analyze(wavPath).Describe().Contains("Wrong container"), "Container diagnostic missing.");
            } finally { File.Delete(wavPath); }
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Check(RoomAudioExporter.Build().text == exported, "Fixture edits leaked into the saved scene.");
            Debug.Log("AUDIO EXPORT CHECKS PASSED: assignments, deduplication, placement, loop, gain, range, PCM conversion and specific duration/codec/format/channel/rate diagnostics.");
            EditorApplication.Exit(0);
        } catch (Exception error) {
            if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
