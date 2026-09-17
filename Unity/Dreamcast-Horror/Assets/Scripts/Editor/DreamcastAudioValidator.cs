using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// One source of compatibility diagnostics for import, inspectors, checks and export.
public static class DreamcastAudioValidator
{
    public static bool IsAudioPath(string path) {
        switch (Path.GetExtension(path).ToLowerInvariant()) {
            case ".wav": case ".mp3": case ".ogg": case ".flac": case ".aif": case ".aiff":
            case ".aac": case ".m4a": case ".wma": case ".opus": return true;
            default: return false;
        }
    }
    public sealed class Report {
        public string path;
        public int channels, rate, bits, codec, dataOffset, dataBytes, frames, targetSamples;
        public long fileBytes;
        public bool tooLong;
        public readonly List<string> problems = new List<string>();
        public bool Usable => problems.Count == 0;
        public bool CanTrimForTesting => tooLong && problems.Count == 1;
        public string CodecName => codec == 1 ? "uncompressed PCM" : codec == 3 ? "IEEE floating-point" :
            codec == 6 ? "A-law" : codec == 7 ? "mu-law" : codec == 17 ? "IMA ADPCM" :
            codec == 2 ? "Microsoft ADPCM" : codec == 65534 ? "WAVE_FORMAT_EXTENSIBLE" : $"WAV codec tag {codec}";
        public string Describe()
        {
            var s = new StringBuilder();
            s.AppendLine(Usable ? "USABLE for this Dreamcast room audio pipeline." : "NOT USABLE by the current exporter.");
            s.AppendLine(path);
            if (rate > 0 && channels > 0) s.AppendLine($"Source: {CodecName}, {bits}-bit, {channels} channel(s), {rate:N0} Hz.");
            if (frames > 0 && rate > 0) s.AppendLine($"Duration: {(double)frames / rate:F3} s. Export estimate: {targetSamples:N0} samples / {targetSamples * 2:N0} PCM bytes.");
            if (Usable) {
                s.AppendLine("Export: mono 16-bit PCM WAV at 11,025 Hz.");
                s.AppendLine(channels == 1 && bits == 16 && rate == 11025 ? "No sample conversion needed." :
                    "Export automatically averages stereo to mono, resamples with a low-pass filter, and writes PCM16 as needed.");
                s.AppendLine("Fits the per-clip limit. The full room is checked separately against 512 KiB and eight unique clips.");
                s.AppendLine("Loop smoothness and sound quality still need a listening check; compatibility does not guarantee a seamless loop.");
            }
            foreach (var problem in problems) s.AppendLine("• " + problem);
            return s.ToString();
        }
    }

    public static Report Analyze(string path)
    {
        var r = new Report { path = path };
        try {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { r.problems.Add("Missing file: assign an existing source audio asset."); return r; }
            using var stream = File.OpenRead(path); using var input = new BinaryReader(stream);
            r.fileBytes = stream.Length;
            if (r.fileBytes > 16 * 1024 * 1024)
                r.problems.Add($"Source file is {r.fileBytes / 1048576.0:F2} MiB; the import-check/export limit is 16 MiB. Trim the source or export a smaller WAV.");
            if (stream.Length < 12 || input.ReadUInt32() != 0x46464952) {
                r.problems.Add($"Wrong container ({Path.GetExtension(path)}): this exporter reads RIFF WAV files, not MP3, OGG, FLAC or AIFF. Convert/export the source to 16-bit PCM WAV; renaming its extension is not conversion."); return r;
            }
            if (input.ReadUInt32() != stream.Length - 8 || input.ReadUInt32() != 0x45564157) {
                r.problems.Add("Damaged or unsupported RIFF header: declared file length/WAVE signature does not match. Re-export the original recording as PCM WAV."); return r;
            }
            bool fmt = false, data = false;
            int align = 0; uint byteRate = 0;
            while (stream.Position + 8 <= stream.Length) {
                uint tag = input.ReadUInt32(), length = input.ReadUInt32();
                long start = stream.Position, next = start + length + (length & 1);
                if (next > stream.Length) { r.problems.Add("Truncated WAV chunk: declared audio data extends beyond the file. Re-export or recopy the original."); return r; }
                if (tag == 0x20746d66) {
                    if (fmt || length < 16) { r.problems.Add("Invalid/duplicate WAV format chunk. Re-export a standard PCM WAV."); return r; }
                    fmt = true; r.codec = input.ReadUInt16(); r.channels = input.ReadUInt16(); r.rate = input.ReadInt32();
                    byteRate = input.ReadUInt32(); align = input.ReadUInt16(); r.bits = input.ReadUInt16();
                } else if (tag == 0x61746164) {
                    if (data || length > int.MaxValue || start > int.MaxValue) { r.problems.Add("Multiple/oversized WAV data chunks are unsupported. Flatten and re-export the sound."); return r; }
                    data = true; r.dataOffset = (int)start; r.dataBytes = (int)length;
                }
                stream.Position = next;
            }
            if (stream.Position != stream.Length)
                r.problems.Add("Incomplete trailing WAV chunk header. Re-export a standard PCM WAV.");
            if (!fmt) r.problems.Add("Missing WAV format chunk. Re-export a standard PCM WAV.");
            if (!data || r.dataBytes == 0) r.problems.Add("No audio samples: the WAV data chunk is missing or empty. Export a non-empty selection.");
            if (fmt) {
                if (r.codec != 1) r.problems.Add($"Wrong codec: detected {r.CodecName} (tag {r.codec}), but the exporter supports uncompressed PCM (tag 1). Re-export as 16-bit PCM WAV; a .wav extension alone does not specify the codec.");
                if (r.bits != 8 && r.bits != 16) r.problems.Add($"Unsupported bit depth: {r.bits}-bit; supported source depths are 8 and 16. Export 16-bit PCM WAV.");
                if (r.channels != 1 && r.channels != 2) r.problems.Add($"Unsupported channel count: {r.channels}; only mono or stereo sources are supported. Mix down to mono/stereo first.");
                if (r.rate < 8000 || r.rate > 48000) r.problems.Add($"Unsupported source sample rate: {r.rate} Hz; accepted range is 8,000–48,000 Hz. Resample to 44,100 or 48,000 Hz PCM WAV (or the target 11,025 Hz).");
                if (r.codec == 1 && r.channels > 0 && r.bits > 0 && r.bits % 8 == 0 && r.rate > 0) {
                    int frameSize = (int)((long)r.channels * r.bits / 8);
                    if (align != frameSize || (long)byteRate != (long)r.rate * frameSize || r.dataBytes % frameSize != 0)
                        r.problems.Add("Malformed PCM layout: block alignment, byte rate or sample count is inconsistent. Re-export the source WAV.");
                    r.frames = r.dataBytes / frameSize;
                    long samples = (long)r.frames * 11025 / r.rate;
                    r.targetSamples = (int)Math.Min(samples, int.MaxValue / 2);
                    r.tooLong = samples > 65534;
                    if (r.tooLong) r.problems.Add($"Too long: {(double)r.frames / r.rate:F3} s becomes {samples:N0} samples. This resident-sound backend allows at most 65,534 samples ({65534.0 / 11025:F3} s). Use Make shortened test copy in the audio report for an otherwise supported WAV, or trim it in an audio editor. Long streamed music is not implemented; reducing volume or compressing the source will not fix this limit.");
                    if (samples < 8) r.problems.Add($"Too short/empty: conversion produces {samples} samples; at least 8 are required. Select a longer sound.");
                }
            }
        } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) {
            r.problems.Add("Cannot read the source: " + e.Message + " Check the file's location and permissions, then retry.");
        }
        return r;
    }
}
