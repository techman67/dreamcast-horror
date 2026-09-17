using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

// Fixed native data schema, shared validation with the Dreamcast host.
public sealed class NativeAudioData
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct Clip {
        public uint pcmBytes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 72)] public string file;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Cue { public int clip; public float volume; }
    [StructLayout(LayoutKind.Sequential)]
    public struct Source {
        public uint clip, loop;
        public float volume;
        public Vector3 position;
        public float range, fullVolumeRange;
        public float Gain(Vector3 listener) => volume * (range == 0 ? 1 : Mathf.Clamp01((range - Vector3.Distance(listener, position)) / (range - fullVolumeRange)));
    }
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr unity_audio_parse([MarshalAs(UnmanagedType.LPStr)] string text);
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint unity_audio_clip_count();
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint unity_audio_source_count();
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_audio_clip(uint index, out Clip value);
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_audio_cue(uint index, out Cue value);
    [DllImport("dreamcast_horror", CallingConvention = CallingConvention.Cdecl)]
    private static extern void unity_audio_source_v2(uint index, out Source value);
    public Clip[] clips;
    public Cue[] cues;
    public Source[] sources;
    public static NativeAudioData Parse(string text)
    {
        string error = Marshal.PtrToStringAnsi(unity_audio_parse(text));
        if (error != null) throw new InvalidDataException(error);
        var result = new NativeAudioData {
            clips = new Clip[unity_audio_clip_count()], cues = new Cue[4], sources = new Source[unity_audio_source_count()]
        };
        for (uint i = 0; i < result.clips.Length; ++i) unity_audio_clip(i, out result.clips[i]);
        for (uint i = 0; i < 4; ++i) unity_audio_cue(i, out result.cues[i]);
        for (uint i = 0; i < result.sources.Length; ++i) unity_audio_source_v2(i, out result.sources[i]);
        return result;
    }
}
