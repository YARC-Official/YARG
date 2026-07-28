using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LibVLCSharp
{
    /// <summary>
    /// Stub for the GetTexture method that was available in custom builds of LibVLCSharp
    /// with Unity=true. In the standard NuGet package, this method does not exist.
    /// This stub returns IntPtr.Zero, meaning video frames won't be rendered via VLC.
    /// Audio will still work. When VLC native binaries are not present, the YargVideoPlayer
    /// wrapper falls back to Unity's VideoPlayer.
    /// </summary>
    public static class MediaPlayerExtensions
    {
        public static IntPtr GetTexture(this MediaPlayer player, uint width, uint height, out bool updated)
        {
            updated = false;
            return IntPtr.Zero;
        }
    }
}
