using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;

// Playback consumes only the exported manifest, never live authoring components.
public sealed class RoomAudio : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Events { public uint count, a, b, c, d; }
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_game_take_audio_events(out Events events);
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_x();
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_y();
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern float unity_game_get_player_z();
    private static Vector3 Listener => new Vector3(unity_game_get_player_x(), unity_game_get_player_y(), unity_game_get_player_z());
    private AudioClip[] clips = Array.Empty<AudioClip>();
    private readonly AudioSource[] slots = new AudioSource[8];
    private NativeAudioData data;
    private GameObject root;
    public int SampleBytes { get; private set; }
    public int[] Played { get; } = new int[4];
    public int ActiveCount {
        get { int n = 0; foreach (var slot in slots) if (slot != null && slot.isPlaying) ++n; return n; }
    }
    public RoomAudio(Transform parent,string directory=null)
    {
        try {
            string manifest = Path.Combine(directory??Application.streamingAssetsPath, "sample.audio");
            if (new FileInfo(manifest).Length > 8192) throw new InvalidDataException("Oversized audio manifest.");
            data = NativeAudioData.Parse(File.ReadAllText(manifest));
            clips = new AudioClip[data.clips.Length];
            for (int i = 0; i < clips.Length; ++i) {
                string path = Path.Combine(directory??Application.streamingAssetsPath, "room-audio", data.clips[i].file);
                int size = Validate(path);
                if (size != data.clips[i].pcmBytes) throw new InvalidDataException("Audio size differs from export: " + path);
                byte[] bytes = File.ReadAllBytes(path);
                using var sha = SHA256.Create();
                string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant().Substring(0, 32) + ".wav";
                if (hash != data.clips[i].file) throw new InvalidDataException("Audio content differs from export: " + path);
                SampleBytes += size;
                var pcm = new float[size / 2];
                for (int j = 0; j < pcm.Length; ++j)
                    pcm[j] = (short)(bytes[44 + j * 2] | bytes[45 + j * 2] << 8) / 32768f;
                clips[i] = AudioClip.Create(data.clips[i].file, pcm.Length, 1, 11025, false);
                if (!clips[i].SetData(pcm, 0)) throw new InvalidDataException("Unable to load exported PCM.");
            }
            root = new GameObject("Exported room audio (8 slots)"); root.transform.SetParent(parent, false);
            for (int i = 0; i < slots.Length; ++i) {
                slots[i] = root.AddComponent<AudioSource>(); slots[i].playOnAwake = false; slots[i].spatialBlend = 0;
            }
            Debug.Log($"AUDIO: {SampleBytes} PCM bytes; {clips.Length} clips; {data.sources.Length} placed sources.");
            Reset();
        } catch { Dispose(); throw; }
    }
    private static int Validate(string path)
    {
        using var stream = File.OpenRead(path); using var r = new BinaryReader(stream);
        if (stream.Length < 60 || stream.Length > 65534 * 2 + 44 ||
            r.ReadUInt32() != 0x46464952 || r.ReadUInt32() != stream.Length - 8 ||
            r.ReadUInt32() != 0x45564157 || r.ReadUInt32() != 0x20746d66 ||
            r.ReadUInt32() != 16 || r.ReadUInt16() != 1 || r.ReadUInt16() != 1 ||
            r.ReadUInt32() != 11025 || r.ReadUInt32() != 22050 ||
            r.ReadUInt16() != 2 || r.ReadUInt16() != 16 || r.ReadUInt32() != 0x61746164 ||
            r.ReadUInt32() != stream.Length - 44 || (stream.Length - 44) % 2 != 0)
            throw new InvalidDataException("Invalid canonical PCM WAV: " + path);
        return (int)stream.Length - 44;
    }
    private void Play(int slot, int clip, float volume, bool loop)
    {
        slots[slot].Stop(); slots[slot].clip = clips[clip]; slots[slot].loop = loop;
        slots[slot].volume = (int)(volume * 255) / 255f; slots[slot].Play();
    }
    public void Stop() { foreach (var slot in slots) if (slot != null) slot.Stop(); }
    public void Reset()
    {
        Stop();
        for (int i = 0; i < data.sources.Length; ++i) {
            var s = data.sources[i]; Play(i, (int)s.clip, s.Gain(Listener), s.loop != 0);
        }
    }
    public void Consume()
    {
        for (int i = 0; i < data.sources.Length; ++i)
            slots[i].volume = (int)(data.sources[i].Gain(Listener) * 255) / 255f;
        unity_game_take_audio_events(out var events);
        for (uint i = 0; i < events.count && i < 4; ++i) {
            uint cue = i == 0 ? events.a : i == 1 ? events.b : i == 2 ? events.c : events.d;
            if (cue > 3 || data.cues[cue].clip < 0) continue;
            var binding = data.cues[cue];
            int first = cue == 0 ? 2 : 3, end = cue == 0 ? 3 : 8;
            for (int slot = first; slot < end; ++slot) if (!slots[slot].isPlaying) {
                Play(slot, binding.clip, binding.volume, false); ++Played[cue]; break;
            }
        }
    }
    public void Dispose()
    {
        Stop(); foreach (var clip in clips) if (clip != null) UnityEngine.Object.Destroy(clip);
        if (root != null) UnityEngine.Object.Destroy(root);
    }
}
