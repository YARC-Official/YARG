using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.Experimental.Rendering;

namespace LibVLCSharp
{
    public static class TextureHelper
    {
        static int _lastVulkanCopyEventFrame = -1;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
        static int _lastLinuxInteropEventFrame = -1;
#endif
        static IntPtr _renderEvent;
#if !UNITY_EDITOR_WIN && (UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX)
        const string UnityPlugin = "libVLCUnityPlugin";
#elif UNITY_IOS
        const string UnityPlugin = "@rpath/VLCUnityPlugin.framework/VLCUnityPlugin";
#else
        const string UnityPlugin = "VLCUnityPlugin";
#endif

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_set_bit_depth_format")]
        static extern void SetBitDepthFormat(IntPtr mediaplayer, int bitDepth);

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_set_unity_texture_vulkan")]
        [return: MarshalAs(UnmanagedType.I1)]
        static extern bool SetUnityTextureVulkan(IntPtr mediaplayer, IntPtr texturePtr);

#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Winapi,
            EntryPoint = "libvlc_unity_get_vulkan_interception_failure")]
        static extern IntPtr GetVulkanInterceptionFailure();
#endif

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetRenderEventFunc")]
        static extern IntPtr GetRenderEventFunc();

        static IntPtr RenderEvent
        {
            get
            {
                if (_renderEvent == IntPtr.Zero)
                    _renderEvent = GetRenderEventFunc();
                return _renderEvent;
            }
        }

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "libvlc_unity_has_retired_renderers")]
        [return: MarshalAs(UnmanagedType.I1)]
        static extern bool HasRetiredRenderersNative();

        internal static bool HasRetiredRenderers()
        {
            return HasRetiredRenderersNative();
        }

        internal static void QueueRendererCleanupEvent()
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            GL.IssuePluginEvent(RenderEvent, 2);
#endif
            GL.IssuePluginEvent(RenderEvent, 3);
        }

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
        static void QueueLinuxTextureInterop()
        {
            var frame = Time.frameCount;
            if (_lastLinuxInteropEventFrame == frame)
                return;
            _lastLinuxInteropEventFrame = frame;
            GL.IssuePluginEvent(RenderEvent, 1);
        }
#endif

        static void IssueVulkanCopyWorkOncePerFrame()
        {
            var frame = Time.frameCount;
            if (_lastVulkanCopyEventFrame == frame)
                return;
            _lastVulkanCopyEventFrame = frame;
#if UNITY_ANDROID && !UNITY_EDITOR
            GL.IssuePluginEvent(RenderEvent, 0);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            GL.IssuePluginEvent(RenderEvent, 1);
            GL.IssuePluginEvent(RenderEvent, 2);
#endif
        }

#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
        static bool IsVulkanTexturePath()
        {
            return SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan;
        }

        static string GetVulkanInterceptionFailureMessage()
        {
            try
            {
                var message = GetVulkanInterceptionFailure();
                return message == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(message);
            }
            catch (Exception exception) when (
                exception is DllNotFoundException ||
                exception is EntryPointNotFoundException)
            {
                return null;
            }
        }
#endif

        internal static bool IsVulkanTexturePathActive()
        {
#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            return IsVulkanTexturePath();
#else
            return false;
#endif
        }

        static bool RegisterVulkanTexture(MediaPlayer player, Texture texture)
        {
#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            if (!IsVulkanTexturePath() || player == null || texture == null)
                return false;
            if (SetUnityTextureVulkan(
                    player.NativeReference, texture.GetNativeTexturePtr()))
                return true;

            var failure = GetVulkanInterceptionFailureMessage();
            var detail = string.IsNullOrEmpty(failure) ? string.Empty : $": {failure}";
            UnityEngine.Debug.LogError(
                "[VLC-Unity] Failed to set Unity texture for Vulkan" + detail);
#endif
            return false;
        }

        internal static RenderTexture CreateDirectVulkanOutput(
            MediaPlayer player)
        {
#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            if (!IsVulkanTexturePath() || player == null)
                return null;

            uint width = 0;
            uint height = 0;
            player.Size(0, ref width, ref height);
            if (width == 0 || height == 0)
                return null;

            var descriptor = new RenderTextureDescriptor(
                (int)width, (int)height)
            {
                depthBufferBits = 0,
                msaaSamples = 1,
                graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                useMipMap = false,
                autoGenerateMips = false,
            };
            var texture = new RenderTexture(descriptor);
            if (!texture.Create() || !RegisterVulkanTexture(player, texture))
            {
                texture.Release();
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            return texture;
#else
            return null;
#endif
        }

        internal static bool UpdateVulkanTexture(
            Texture texture, MediaPlayer player)
        {
#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            if (!IsVulkanTexturePath() || texture == null || player == null)
                return false;

            var texturePointer = player.GetTexture(
                (uint)texture.width, (uint)texture.height, out bool updated);
            // Completion polling is part of the render event. Keep pumping it
            // even when the producer has no free slot to publish a new frame.
            IssueVulkanCopyWorkOncePerFrame();
            return updated && texturePointer != IntPtr.Zero;
#else
            return false;
#endif
        }
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

#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            // Vulkan uses AccessTexture; the plugin copies on Unity's render thread.
            if (IsVulkanTexturePath())
                return UpdateVulkanTexture(texture, player);
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            // Trigger render-thread work (DMA-BUF texture import on Linux/Wayland)
            QueueLinuxTextureInterop();
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

#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            if (IsVulkanTexturePath())
            {
                if (width == 0 || height == 0)
                    return default;
                if (bitDepth != BitDepth.Bit8)
                    throw new VLCException("The Vulkan video path currently requires an RGBA32 destination texture");

                // Create Unity-owned texture
                var texture = new Texture2D((int)width, (int)height,
                    TextureFormat.RGBA32,
                    mipmap, linear);

                // Force Unity to allocate GPU resources for the texture before we pass it to the plugin
                // This ensures the VkImage is fully created and initialized
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

                // Pass texture to plugin so it can update it via AccessTexture
                if (!RegisterVulkanTexture(player, texture))
                {
                    UnityEngine.Object.Destroy(texture);
                    return default;
                }

                return texture;
            }
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            // Trigger render-thread work (DMA-BUF texture import on Linux/Wayland)
            QueueLinuxTextureInterop();
#endif

            // Standard external-texture approach for non-Vulkan renderers.
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
