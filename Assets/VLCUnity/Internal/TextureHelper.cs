using UnityEngine;
using UnityEditor;
using System;
using System.Runtime.InteropServices;
using LibVLCSharp;

namespace LibVLCSharp
{
    public static class TextureHelper
    {
#if !UNITY_EDITOR_WIN && (UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
        const string UnityPlugin = "libVLCUnityPlugin";
#elif UNITY_IOS
        const string UnityPlugin = "@rpath/VLCUnityPlugin.framework/VLCUnityPlugin";
#else
        const string UnityPlugin = "VLCUnityPlugin";
#endif

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_set_bit_depth_format")]
        static extern void SetBitDepthFormat(IntPtr mediaplayer, int bitDepth);

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_set_unity_texture_vulkan")]
        static extern bool SetUnityTextureVulkan(IntPtr mediaplayer, IntPtr texturePtr);

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetRenderEventFunc")]
        static extern IntPtr GetRenderEventFunc();

#if UNITY_ANDROID && !UNITY_EDITOR
        // Track if we're using the Vulkan approach (Unity-owned texture)
        private static bool isVulkanMode = false;
#endif
        /// <summary>
        /// Update texture with new frame data
        /// </summary>
        /// <param name="texture">The texture to update</param>
        /// <param name="player">The media player</param>
        /// <returns>true if frame was updated</returns>
        public static bool UpdateTexture(Texture2D texture, MediaPlayer player)
        {
            if (texture == null)
                return false;

#if UNITY_ANDROID && !UNITY_EDITOR
            // Vulkan on Android uses AccessTexture approach - plugin updates the texture directly via render thread
            if (isVulkanMode)
            {
                // Check if there's an update
                var texptr = player.GetTexture((uint)texture.width, (uint)texture.height, out bool updated);

                if (updated && texptr != System.IntPtr.Zero)
                {
                    // Issue a plugin event to trigger the texture copy on the render thread
                    // This ensures AccessTexture is called at the right time
                    IntPtr renderEventFunc = GetRenderEventFunc();
                    GL.IssuePluginEvent(renderEventFunc, 0);
                    return true;
                }
                return false;
            }
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // Trigger render-thread work (DMA-BUF texture import on Linux/Wayland)
            GL.IssuePluginEvent(GetRenderEventFunc(), 1);
#endif

            // Standard approach for non-Vulkan
            var ptr = player.GetTexture((uint)texture.width, (uint)texture.height, out bool updatedd);
            if (updatedd && ptr != System.IntPtr.Zero)
            {
                texture.UpdateExternalTexture(ptr);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Helper for native texture creation
        /// </summary>
        /// <param name="player">mediaplayer instance</param>
        /// <param name="bitDepth">8 or 16 bits (16 bits is Windows-only for now)</param>
        /// <param name="linear">true for linear color space</param>
        /// <param name="mipmap">default to false</param>
        /// <returns>texture or null / throw if fails</returns>
        public static Texture2D CreateNativeTexture(MediaPlayer player, bool linear, BitDepth bitDepth = BitDepth.Bit8, bool mipmap = false)
        {
            uint width = 0;
            uint height = 0;

            player.Size(0, ref width, ref height);

            if (bitDepth == BitDepth.Bit16)
            {
                if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf))
                {
                    throw new VLCException("16 bits was requested, but TextureFormat.RGBAHalf is not supported by your GPU");
                }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
                SetBitDepthFormat(player.NativeReference, (int)BitDepth.Bit16);
#endif
            }
            else
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
                SetBitDepthFormat(player.NativeReference, (int)BitDepth.Bit8);
#endif
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // Vulkan on Android requires a different approach
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
            {
                if (width == 0 || height == 0)
                    return default;

                // Create Unity-owned texture
                var texture = new Texture2D((int)width, (int)height,
                    bitDepth == BitDepth.Bit16 ? TextureFormat.RGBAHalf : TextureFormat.RGBA32,
                    mipmap, linear);

                // Force Unity to allocate GPU resources for the texture before we pass it to the plugin
                // This ensures the VkImage is fully created and initialized
                texture.Apply(false, false);

                // Pass texture to plugin so it can update it via AccessTexture
                if (!SetUnityTextureVulkan(player.NativeReference, texture.GetNativeTexturePtr()))
                {
                    UnityEngine.Debug.LogError("[VLC-Unity] Failed to set Unity texture for Vulkan");
                    UnityEngine.Object.Destroy(texture);
                    return default;
                }

                isVulkanMode = true;
                return texture;
            }
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // Trigger render-thread work (DMA-BUF texture import on Linux/Wayland)
            GL.IssuePluginEvent(GetRenderEventFunc(), 1);
#endif

            // Standard approach for non-Vulkan or non-Android
            var texptr = player.GetTexture(width, height, out bool updated);

            if (width != 0 && height != 0 && updated && texptr != IntPtr.Zero)
            {
                return Texture2D.CreateExternalTexture((int)width,
                        (int)height,
                        bitDepth == BitDepth.Bit16 ? TextureFormat.RGBAHalf : TextureFormat.RGBA32,
                        mipmap,
                        linear,
                        texptr);
            }
            else
            {
                return default;
            }
        }
    }

    public enum BitDepth
    {
        Bit8 = 8,
        Bit16 = 16
    }
}