using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Host storage/presentation. Save format, interaction and restore rules live in C++.
public sealed class RoomSavePresentation
{
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr unity_save_configure(byte[] data,uint length,uint room);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_save_requested();
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_save_near();
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr unity_save_prompt(int index);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr unity_save_result(int result);
    [DllImport("dreamcast_horror",CallingConvention=CallingConvention.Cdecl)] static extern int unity_save_action([MarshalAs(UnmanagedType.LPUTF8Str)] string directory,int load);
    string message=""; float remaining;
    public string DirectoryPath => Environment.GetEnvironmentVariable("DREAMCAST_SAVE_DIRECTORY") ?? Path.Combine(Application.persistentDataPath,"DreamcastSaves");
    public RoomSavePresentation(string room,string directory=null)
    {
        var path=Path.Combine(directory??Application.streamingAssetsPath,"sample.saves");
        var bytes=File.Exists(path) ? File.ReadAllBytes(path) : new byte[12];
        uint hash=2166136261; foreach(byte b in Encoding.UTF8.GetBytes(room)) hash=unchecked((hash^b)*16777619);
        if(!File.Exists(path)) { Array.Copy(BitConverter.GetBytes(0x31505344u),bytes,4); Array.Copy(BitConverter.GetBytes(hash),0,bytes,4,4); }
        var error=unity_save_configure(bytes,(uint)bytes.Length,hash); if(error!=IntPtr.Zero) throw new InvalidDataException(Marshal.PtrToStringAnsi(error));
    }
    public bool Update()
    {
        remaining=Mathf.Max(0,remaining-Time.unscaledDeltaTime);
        bool load=(Keyboard.current?.lKey.wasPressedThisFrame ?? false)||(Gamepad.current?.buttonNorth.wasPressedThisFrame ?? false);
        bool save=unity_save_requested()>=0;
        if(!load && !save) return false;
        try {
            Directory.CreateDirectory(DirectoryPath);
            int result=unity_save_action(DirectoryPath,load ? 1 : 0);
            message=load && result==0 ? "Saved progress loaded." : Marshal.PtrToStringAnsi(unity_save_result(result)); remaining=5;
            Debug.Log(message+" Save folder: "+DirectoryPath);
            return load && result==0;
        } catch(Exception e) { message="Save storage failed: "+e.Message; remaining=5; return false; }
    }
    public void Draw()
    {
        int near=unity_save_near(); string text=remaining>0 ? message : near>=0 ? "E / A: "+Marshal.PtrToStringAnsi(unity_save_prompt(near)) : "";
        GUI.Box(new Rect(12,Screen.height-82,Mathf.Min(620,Screen.width-24),64),text+"\nL / Y: load latest save (temporary test control)");
    }
}
