#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
#define VLC_LOADER_SUPPORTED
#endif

#if VLC_LOADER_SUPPORTED
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using LibVLCSharp;
#endif
using UnityEngine;

/// <summary>
/// Creates the process-wide <c>VLCMediaPlayer.LibVLC</c> from YARG code, so the vendored vlc-unity
/// files stay unmodified. It must run before the first <c>VLCMediaPlayer.Awake</c>, which only
/// builds its own LibVLC when none exists; <see cref="YargVideoPlayer"/> calls it from its Awake.
/// </summary>
public static class VlcLibraryLoader
{
    /// <summary>
    /// A user-selected VLC install or libVLC folder (the VlcLibraryPath setting), used in place of
    /// <see cref="Application.dataPath"/>. Null or empty means the default.
    /// </summary>
    public static string PathOverride { get; set; }

    /// <summary>
    /// Makes sure LibVLC exists and was built from the current path, rebuilding it after a path
    /// change. Returns false when no usable libVLC was found; the caller must then keep that
    /// VLCMediaPlayer from waking, or its own init poisons VLC for the rest of the process.
    /// </summary>
    public static bool EnsureLoaded(Component vlcMediaPlayer)
    {
#if VLC_LOADER_SUPPORTED
        var player = (VLCMediaPlayer) vlcMediaPlayer;
        string basePath = EffectiveBasePath;
        if (VLCMediaPlayer.LibVLC != null && _loadedFrom == basePath)
            return true;

        try
        {
            ReleaseLibVLC();

            if (!InitializeCore(basePath))
                return false;

            var args = new List<string>();
            if (player.Configuration != null)
                args.AddRange(player.Configuration.GetOptions());
            args.AddRange(player.libVLCArguments?.Where(arg => !string.IsNullOrWhiteSpace(arg)) ??
                Array.Empty<string>());

            // As VLCMediaPlayer.CreateLibVLC does.
            var libVLC = new LibVLC(enableDebugLogs: false, args.ToArray());
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

            LibVLCProperty.SetValue(null, libVLC);
            HookLibVLCMethod.Invoke(null, new object[] { libVLC });
            _loadedFrom = basePath;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcLibraryLoader] Failed to initialize libVLC, using Unity's video player: {ex}");
            return false;
        }
#else
        // Linux loads its bundled libVLC in VLCMediaPlayer's own Awake.
        return true;
#endif
    }

#if VLC_LOADER_SUPPORTED
    // The path LibVLC was last built from, to detect a PathOverride change: LibVLC is static and
    // process-lifetime, so otherwise the old install would silently keep being used.
    private static string _loadedFrom;

    private static string EffectiveBasePath =>
        string.IsNullOrEmpty(PathOverride) ? Application.dataPath : PathOverride;

    // VLCMediaPlayer.LibVLC has a private setter and VLCUnityLogger.HookLibVLC is internal; both
    // are kept from stripping by Assets/Script/Gameplay/link.xml.
    private static readonly PropertyInfo LibVLCProperty =
        typeof(VLCMediaPlayer).GetProperty(nameof(VLCMediaPlayer.LibVLC), BindingFlags.Public | BindingFlags.Static);

    private static readonly MethodInfo HookLibVLCMethod =
        typeof(VLCUnityLogger).GetMethod("HookLibVLC", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly MethodInfo UnhookLibVLCMethod =
        typeof(VLCUnityLogger).GetMethod("UnhookLibVLC", BindingFlags.NonPublic | BindingFlags.Static);

    private static void ReleaseLibVLC()
    {
        var old = VLCMediaPlayer.LibVLC;
        if (old == null)
            return;

        Debug.Log($"[VlcLibraryLoader] VLC path changed from '{_loadedFrom}' to '{EffectiveBasePath}', re-initializing. " +
            "If playback still reflects the old install, restart the Editor or app.");
        UnhookLibVLCMethod.Invoke(null, new object[] { old });
        old.Dispose();
        LibVLCProperty.SetValue(null, null);
        _loadedFrom = null;
    }

    // Returns false only when calling Core.Initialize would be unsafe (see TryPreloadMac).
    private static bool InitializeCore(string basePath)
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        // Core.Initialize's own Mac path math uses backslashes, which aren't separators on macOS,
        // so its lookup never finds anything. Preload the real libraries ourselves instead.
        bool loaded = TryPreloadMac(basePath, out string pluginsDir);
        if (!loaded && basePath != Application.dataPath)
        {
            Debug.LogWarning($"[VlcLibraryLoader] No usable libvlc under configured path '{basePath}'. Trying default location.");
            basePath = Application.dataPath;
            loaded = TryPreloadMac(basePath, out pluginsDir);
        }

        if (!loaded)
        {
            Debug.LogWarning("[VlcLibraryLoader] No usable libvlc found -- skipping native init so later attempts this session aren't poisoned.");
            return false;
        }
#endif

        Debug.Log($"[VlcLibraryLoader] Initializing libvlc with base path '{basePath}' (override: {!string.IsNullOrEmpty(PathOverride)}).");
        try
        {
            Core.Initialize(basePath);
        }
        catch (Exception ex) when (basePath != Application.dataPath)
        {
            Debug.LogWarning($"[VlcLibraryLoader] Failed to initialize libvlc from configured path '{basePath}': {ex.Message}. Falling back to default.");
            Core.Initialize(Application.dataPath);
        }

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        // Core.Initialize overwrites VLC_PLUGIN_PATH with its own broken guess; put ours back
        // before LibVLC is constructed and reads it.
        if (pluginsDir != null)
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginsDir);
#endif
        return true;
    }
#endif

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    // RTLD_GLOBAL makes LibVLCSharp's later bare-name DllImports resolve to the preloaded image.
    // NativeLibrary isn't available at this project's API compatibility level, hence raw dlopen.
    [DllImport("libSystem.dylib", EntryPoint = "dlopen")]
    private static extern IntPtr Dlopen(string path, int mode);

    private const int RTLD_NOW_GLOBAL = 0x2 | 0x8;

    // Native libraries are never unloaded within one Editor process, so a later dlopen of a
    // different libvlc doesn't replace the first; tracked only to warn about it.
    private static string _macPreloadedFrom;

    // Finds and dlopen-preloads libvlc/libvlccore/plugins under basePath. Returns false if none
    // are found or loading fails, and the caller must then not call Core.Initialize: a failed
    // DllImport into LibVLCSharp is cached for the rest of the process, so trying a path known
    // not to work breaks every later attempt this session, including a corrected path. On
    // success pluginsDir is the plugins folder, or null if none was found nearby.
    private static bool TryPreloadMac(string basePath, out string pluginsDir)
    {
        pluginsDir = null;

        const string libvlcName = "libvlc.dylib";
        const string libvlccoreName = "libvlccore.dylib";

        // Known install layouts: the folder itself, a VLC 3.x app bundle's Contents/MacOS/lib, a
        // VLC 4.x app bundle's Contents/Frameworks. Add new layouts rather than replacing these --
        // real installs use all of them.
        string[] candidates =
        {
            basePath,
            Path.Combine(basePath, "Contents", "MacOS", "lib"),
            Path.Combine(basePath, "Contents", "Frameworks"),
        };

        string libDir = candidates.FirstOrDefault(candidate =>
            File.Exists(Path.Combine(candidate, libvlcName)) &&
            File.Exists(Path.Combine(candidate, libvlccoreName)));

        if (libDir == null)
        {
            Debug.LogWarning($"[VlcLibraryLoader] Could not find {libvlcName}/{libvlccoreName} under '{basePath}' (checked: {string.Join(", ", candidates)}).");
            return false;
        }

        if (_macPreloadedFrom != null && _macPreloadedFrom != libDir)
        {
            Debug.LogWarning($"[VlcLibraryLoader] A different libvlc was already loaded earlier in this Editor session, from '{_macPreloadedFrom}'. " +
                "Native libraries aren't unloaded between Play sessions -- restart the Editor to test the newly-configured path.");
        }
        _macPreloadedFrom = libDir;

        // libvlc depends on libvlccore, so load that first.
        if (Dlopen(Path.Combine(libDir, libvlccoreName), RTLD_NOW_GLOBAL) == IntPtr.Zero ||
            Dlopen(Path.Combine(libDir, libvlcName), RTLD_NOW_GLOBAL) == IntPtr.Zero)
        {
            Debug.LogWarning($"[VlcLibraryLoader] Failed to pre-load libvlc from '{libDir}'.");
            return false;
        }

        // Plugins: beside the libraries (also a VLC 4.x bundle's Contents/Frameworks/plugins), a
        // sibling of libDir (a VLC 3.x bundle's Contents/MacOS/plugins), or under "vlc" (a
        // standalone SDK build's lib/vlc/plugins).
        string[] pluginCandidates =
        {
            Path.Combine(libDir, "plugins"),
            Path.Combine(Path.GetDirectoryName(libDir) ?? string.Empty, "plugins"),
            Path.Combine(libDir, "vlc", "plugins"),
        };

        pluginsDir = pluginCandidates.FirstOrDefault(Directory.Exists);

        if (pluginsDir != null)
        {
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginsDir);
            Debug.Log($"[VlcLibraryLoader] Pre-loaded libvlc from '{libDir}', plugins from '{pluginsDir}'.");
        }
        else
        {
            Debug.LogWarning($"[VlcLibraryLoader] Pre-loaded libvlc from '{libDir}' but couldn't find a plugins folder (checked: {string.Join(", ", pluginCandidates)}).");
        }

        return true;
    }
#endif
}
