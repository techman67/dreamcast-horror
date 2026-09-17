using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DreamcastAudioTrimChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "dreamcast-trim-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            foreach (int rate in new[] { 8000, 11025, 44100, 48000 })
            foreach (int bits in new[] { 8, 16 })
            foreach (int channels in new[] { 1, 2 }) {
                int alignment = bits / 8 * channels, length = rate * 6 * alignment;
                string source = Path.Combine(folder, "source.wav"), copy = Path.Combine(folder, "copy.wav");
                using (var w = new BinaryWriter(File.Create(source))) {
                    w.Write(0x46464952u); w.Write(46 + length); w.Write(0x45564157u);
                    w.Write(0x20746d66u); w.Write(16); w.Write((ushort)1); w.Write((ushort)channels);
                    w.Write(rate); w.Write(rate * alignment); w.Write((ushort)alignment); w.Write((ushort)bits);
                    w.Write(0x4b4e554au); w.Write(1); w.Write((byte)9); w.Write((byte)0); // Padded metadata before samples.
                    w.Write(0x61746164u); w.Write(length);
                    for (int i = 0; i < length; ++i) w.Write((byte)(i % 251));
                }
                byte[] original = File.ReadAllBytes(source);
                Check(DreamcastAudioValidator.Analyze(source).CanTrimForTesting, "Valid overlong PCM not offered trimming.");
                DreamcastAudioTrimmer.WriteTestCopy(source, copy);
                var result = DreamcastAudioValidator.Analyze(copy);
                Check(result.Usable && result.targetSamples <= 65534 && result.targetSamples >= 65532, "Trimmed sample budget incorrect.");
                Check(result.bits == bits && result.channels == channels && result.rate == rate, "Source PCM format changed.");
                Check(File.ReadAllBytes(source).SequenceEqual(original), "Original was modified.");
                byte[] trimmed = File.ReadAllBytes(copy);
                Check(trimmed.Skip(44).Take(result.dataBytes).SequenceEqual(original.Skip(54).Take(result.dataBytes)), "Excerpt did not preserve the first complete PCM frames.");
                bool rejected = false;
                try { DreamcastAudioTrimmer.WriteTestCopy(source, copy); } catch (IOException) { rejected = true; }
                Check(rejected && trimmed.SequenceEqual(File.ReadAllBytes(copy)), "Existing copy was overwritten.");
                rejected = false;
                try { DreamcastAudioTrimmer.WriteTestCopy(source, source); } catch (IOException) { rejected = true; }
                Check(rejected && original.SequenceEqual(File.ReadAllBytes(source)), "Source overwrite was accepted.");
                File.Delete(copy);
                original[20] = 3; File.WriteAllBytes(source, original);
                Check(!DreamcastAudioValidator.Analyze(source).CanTrimForTesting, "Unsupported codec offered trimming.");
                rejected = false;
                try { DreamcastAudioTrimmer.WriteTestCopy(source, copy); } catch (InvalidDataException) { rejected = true; }
                Check(rejected && !File.Exists(copy), "Invalid source created a copy.");
            }
            Debug.Log("AUDIO TRIM CHECKS PASSED: 16 PCM formats, metadata/padding, duration, preserved samples/original, overwrite and codec rejection.");
            EditorApplication.Exit(0);
        } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        finally {
            foreach (string path in Directory.GetFiles(folder)) File.Delete(path);
            Directory.Delete(folder);
        }
    }
}
