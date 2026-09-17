using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RoomAudioExporter
{
    public sealed class Bundle {
        public string text;
        public readonly SortedDictionary<string, byte[]> files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
    }
    private static string N(float x) => x.ToString("R", CultureInfo.InvariantCulture);

    // Read source WAV bytes directly: Unity import compression/settings never affect the export.
    public static byte[] ConvertWav(string path)
    {
        var report = DreamcastAudioValidator.Analyze(path);
        if (!report.Usable) throw new InvalidDataException(report.Describe());
        byte[] file = File.ReadAllBytes(path);
        int frames = report.frames, channels = report.channels, bits = report.bits;
        int offset = report.dataOffset, count = report.targetSamples, rate = report.rate;
        var mono = new double[frames];
        for (int i = 0; i < frames; ++i) {
            for (int ch = 0; ch < channels; ++ch) {
                int p = offset + (i * channels + ch) * bits / 8;
                mono[i] += bits == 8 ? (file[p] - 128) * 256 : (short)(file[p] | file[p + 1] << 8);
            }
            mono[i] /= channels;
        }
        using var result = new MemoryStream();
        using var output = new BinaryWriter(result);
        output.Write(0x46464952u); output.Write(36 + count * 2); output.Write(0x45564157u);
        output.Write(0x20746d66u); output.Write(16); output.Write((ushort)1); output.Write((ushort)1);
        output.Write(11025); output.Write(22050); output.Write((ushort)2); output.Write((ushort)16);
        output.Write(0x61746164u); output.Write(count * 2);
        double cutoff = Math.Min(1, 11025.0 / rate) * .9;
        int radius = (int)Math.Ceiling(16 / cutoff);
        for (int i = 0; i < count; ++i) {
            double value;
            if (rate == 11025) value = mono[i];
            else {
                double center = (double)i * rate / 11025, sum = 0, weight = 0;
                for (int j = (int)center - radius; j <= (int)center + radius; ++j) {
                    double distance = center - j, x = distance * cutoff;
                    if (Math.Abs(distance) >= radius) continue;
                    double w = (Math.Abs(x) < 1e-10 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x)) *
                               (.5 + .5 * Math.Cos(Math.PI * distance / radius));
                    sum += mono[Math.Max(0, Math.Min(frames - 1, j))] * w; weight += w;
                }
                value = sum / weight;
            }
            output.Write((short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(value))));
        }
        return result.ToArray();
    }

    public static Bundle Build()
    {
        var unsupported = UnityEngine.Object.FindObjectsByType<AudioSource>().FirstOrDefault(x => x.isActiveAndEnabled);
        if (unsupported != null) throw new InvalidDataException("Unity AudioSource on '" + unsupported.name +
            "' is not portable. Use Dreamcast/Placed Room Sound or Gameplay Sound Cue, or disable it before export.");
        var result = new Bundle();
        var cues = UnityEngine.Object.FindObjectsByType<DreamcastSoundCue>().Where(x => x.isActiveAndEnabled).ToArray();
        var sources = UnityEngine.Object.FindObjectsByType<DreamcastRoomSound>().Where(x => x.isActiveAndEnabled)
            .OrderBy(x => GlobalObjectId.GetGlobalObjectIdSlow(x).ToString(), StringComparer.Ordinal).ToArray();
        if (sources.Length > 2) throw new InvalidDataException($"Too many placed sounds: {sources.Length}/2. Disable or remove extra Dreamcast Room Sound components; two voices are reserved for placed audio.");
        var duplicate = cues.GroupBy(x => x.cue).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null) throw new InvalidDataException("Duplicate " + duplicate.Key + " cue on: " + string.Join(", ", duplicate.Select(x => x.name)) + ". Keep one assignment for this event.");
        foreach (var cue in cues) {
            if (cue.clip == null) throw new InvalidDataException("Missing audio clip on '" + cue.name + "' (" + cue.cue + "). Assign a source clip or disable this cue.");
            if (float.IsNaN(cue.volume) || cue.volume < 0 || cue.volume > 1) throw new InvalidDataException("Volume on '" + cue.name + "' must be finite and between 0 and 1.");
        }
        foreach (var source in sources) {
            if (source.clip == null) throw new InvalidDataException("Missing audio clip on placed source '" + source.name + "'. Assign a source clip or disable it.");
            if (float.IsNaN(source.volume) || source.volume < 0 || source.volume > 1) throw new InvalidDataException("Volume on '" + source.name + "' must be finite and between 0 and 1.");
            if (float.IsNaN(source.audibleRange) || source.audibleRange < 0 || source.audibleRange > 10000) throw new InvalidDataException("Audible range on '" + source.name + "' must be 0 (room-wide) or a finite positive distance up to 10000.");
        }
        foreach (var source in sources)
            if (float.IsNaN(source.fullVolumeRange) || float.IsInfinity(source.fullVolumeRange) || source.fullVolumeRange < 0 ||
                (source.audibleRange == 0 ? source.fullVolumeRange != 0 : source.fullVolumeRange >= source.audibleRange))
                throw new InvalidDataException("Full volume distance on '" + source.name + "' must be nonnegative and smaller than Audible Range; use 0 for room-wide sound.");
        var references = new Dictionary<AudioClip, string>();
        foreach (var clip in cues.Select(x => x.clip).Concat(sources.Select(x => x.clip)).Distinct()) {
            if (clip == null) throw new InvalidDataException("An active Dreamcast sound component has no clip.");
            string path = AssetDatabase.GetAssetPath(clip);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidDataException("Reference a source WAV under Assets: " + clip.name);
            byte[] bytes = ConvertWav(path);
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant().Substring(0, 32) + ".wav";
            if (result.files.TryGetValue(hash, out var existing) && !existing.SequenceEqual(bytes))
                throw new InvalidDataException("Audio filename hash collision; export cannot safely continue.");
            result.files[hash] = bytes; references.Add(clip, hash);
        }
        if (result.files.Count > 8) throw new InvalidDataException($"Too many unique sounds: {result.files.Count}/8 after deduplication. Reuse clips or remove assignments.");
        int totalBytes = result.files.Values.Sum(x => x.Length - 44);
        if (totalBytes > 512 * 1024) throw new InvalidDataException($"Room audio uses {totalBytes:N0} PCM bytes; limit is 524,288 (512 KiB), exceeded by {totalBytes - 524288:N0} bytes. Shorten or remove clips. Volume changes do not reduce memory.");
        var names = result.files.Keys.ToList();
        int Id(AudioClip clip) => names.IndexOf(references[clip]);
        var text = new StringBuilder("dreamcast_audio 2\n");
        text.AppendLine($"clips {names.Count}");
        for (int i = 0; i < names.Count; ++i) text.AppendLine($"clip {i} {result.files[names[i]].Length - 44} {names[i]}");
        for (int i = 0; i < 4; ++i) {
            var cue = cues.SingleOrDefault(x => (int)x.cue == i);
            text.AppendLine($"cue {i} {(cue == null ? -1 : Id(cue.clip))} {N(cue == null ? 1 : cue.volume)}");
        }
        if (cues.Any(x => (int)x.cue < 0 || (int)x.cue > 3)) throw new InvalidDataException("Unknown gameplay sound cue.");
        text.AppendLine($"sources {sources.Length}");
        foreach (var source in sources) {
            Vector3 p = source.transform.position;
            text.AppendLine($"source {Id(source.clip)} {(source.loop ? 1 : 0)} {N(source.volume)} {N(p.x)} {N(p.y)} {N(p.z)} {N(source.audibleRange)} {N(source.fullVolumeRange)}");
        }
        text.AppendLine("end"); result.text = text.ToString();
        NativeAudioData.Parse(result.text); // Same validation as the Dreamcast runtime.
        return result;
    }
}
