using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LibVLCSharp
{
    class OnLoad
    {
#if !UNITY_EDITOR_WIN && (UNITY_ANDROID || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
        internal const string UnityPlugin = "libVLCUnityPlugin";
#elif UNITY_IOS
        internal const string UnityPlugin = "@rpath/VLCUnityPlugin.framework/VLCUnityPlugin";
#else
        internal const string UnityPlugin = "VLCUnityPlugin";
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        internal static string LibVLCDirectory
        {
            get
            {
#if UNITY_EDITOR_LINUX
                // In the Editor, plugins live under the Assets tree
                return System.IO.Path.Combine(Application.dataPath, "VLCUnity", "Plugins", "Linux", "x86_64");
#else
                // In a standalone build, Unity flattens all native plugins into
                // <app>_Data/Plugins/ (no x86_64 subdirectory).
                return System.IO.Path.Combine(Application.dataPath, "Plugins");
#endif
            }
        }
#endif

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_set_color_space")]
        static extern void SetColorSpace(UnityColorSpace colorSpace);

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr GetRenderEventFunc();

        enum UnityColorSpace
        {
            Gamma = 0,
            Linear = 1,
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoadRuntimeMethod()
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            var libDir = LibVLCDirectory;
            var pluginPath = libDir + "/vlc/plugins";
            System.Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginPath);
            Debug.Log("[VLC] Set VLC_PLUGIN_PATH to " + pluginPath);
#endif
          //  Debug.Log("UnityEngine.QualitySettings.activeColorSpace: " + PlayerColorSpace);
            SetColorSpace(PlayerColorSpace);
#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            GL.IssuePluginEvent(GetRenderEventFunc(), 1);
#endif
        }
        static UnityColorSpace PlayerColorSpace => QualitySettings.activeColorSpace == 0 ? UnityColorSpace.Gamma : UnityColorSpace.Linear;
    }
}
