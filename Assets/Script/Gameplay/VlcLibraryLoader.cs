#if (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX)
#define VLC_LOADER_SUPPORTED
#endif

#if VLC_LOADER_SUPPORTED
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using LibVLCSharp;
using UnityEngine;
using Debug = UnityEngine.Debug;
#endif

/// <summary>
/// Creates the process-wide <c>VLCMediaPlayer.LibVLC</c> from YARG code, so the vendored vlc-unity
/// files stay unmodified. Nothing in the repo ships a libvlc. On Mac and Windows it finds a libVLC 4
/// install (the VlcLibraryPath setting, then standard install locations) and preloads it, which is
/// what binds LibVLCSharp's imports. On Linux libvlc binds at launch from the library path, so the
/// player relaunches itself with the setting's folder there (RelaunchWithLibVlcPath), and this
/// uses whatever was bound. It must run before the first <c>VLCMediaPlayer.Awake</c>, which only builds its own LibVLC when none exists;
/// <see cref="YargVideoPlayer"/> calls it from its Awake.
/// </summary>
public static class VlcLibraryLoader
{
    /// <summary>
    /// A user-selected VLC install or libVLC folder (the VlcLibraryPath setting), searched before the
    /// standard install locations. Null or empty means only the standard locations.
    /// </summary>
    public static string PathOverride { get; set; }

#if VLC_LOADER_SUPPORTED
    /// <summary>
    /// Makes sure LibVLC exists. Runs the search once per process: a loaded native library can't be
    /// replaced, so a later PathOverride change needs a restart. Returns false when no usable
    /// libVLC 4 was found; the caller must then keep that VLCMediaPlayer from waking, or its own
    /// init poisons VLC for the rest of the process.
    /// </summary>
    public static bool EnsureLoaded(VLCMediaPlayer player)
    {
        if (_attempted)
        {
            if (PathOverride != _attemptedOverride && !_warnedPathChange)
            {
                _warnedPathChange = true;
                Debug.LogWarning($"[VlcLibraryLoader] VLC path changed to '{PathOverride}' after libVLC was loaded " +
                    $"(from '{_loadedFrom ?? "nowhere"}'); restart YARG to use it.");
            }
            return VLCMediaPlayer.LibVLC != null;
        }

        _attempted = true;
        _attemptedOverride = PathOverride;

        if (VLCMediaPlayer.LibVLC != null)
        {
            Debug.LogError("[VlcLibraryLoader] LibVLC already exists: a VLCMediaPlayer woke before YargVideoPlayer and built its own.");
            return true;
        }

        try
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            if (!TryFindBoundLibVlc(out string libDir, out string pluginsDir))
                return false;
#else
            if (!TryPreload(out string libDir, out string pluginsDir))
            {
                Debug.LogWarning("[VlcLibraryLoader] No usable libVLC 4 found; using Unity's video player.");
                return false;
            }

            // Not needed on Linux: the plugin loaded at startup (TryFindBoundLibVlc requires it), so
            // vlc-unity's OnLoad succeeded there.
            CompleteVlcUnityOnLoad();
#endif

            // Needs a non-empty path on Mac/Windows and ignores it on Linux. Its own lookup misses
            // (and logs it). LibVLCSharp's DllImports then bind to the libraries preloaded above on
            // Mac/Windows, and through the launch-time library path on Linux.
            Core.Initialize(libDir);

            // Must be the last write before new LibVLC, whose libvlc_new reads it. Two others
            // overwrite it before this point: Core.Initialize, through the plugin's SetPluginPath
            // (Mac/Windows; a backslash-joined path that never exists on Mac), and vlc-unity's
            // OnLoad (Linux, at startup). Any earlier set is lost.
            if (pluginsDir != null)
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginsDir);

            var args = new List<string>();
            if (player.Configuration != null)
                args.AddRange(player.Configuration.GetOptions());
            args.AddRange(player.libVLCArguments?.Where(arg => !string.IsNullOrWhiteSpace(arg)) ??
                Array.Empty<string>());

            var libVLC = new LibVLC(enableDebugLogs: false, args.ToArray());
            LibVLCProperty.SetValue(null, libVLC);
            HookLibVLCMethod.Invoke(null, new object[] { libVLC });
            _loadedFrom = libDir;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcLibraryLoader] Failed to initialize libVLC, using Unity's video player: {ex}");
            return false;
        }
    }

    private static bool _attempted;
    private static string _attemptedOverride;
    private static bool _warnedPathChange;
    private static string _loadedFrom;

    // VLCMediaPlayer.LibVLC has a private setter and VLCUnityLogger.HookLibVLC is internal; both
    // are kept from stripping by Assets/Script/Gameplay/link.xml.
    private static readonly PropertyInfo LibVLCProperty =
        typeof(VLCMediaPlayer).GetProperty(nameof(VLCMediaPlayer.LibVLC), BindingFlags.Public | BindingFlags.Static);

    private static readonly MethodInfo HookLibVLCMethod =
        typeof(VLCUnityLogger).GetMethod("HookLibVLC", BindingFlags.NonPublic | BindingFlags.Static);

    // Mac/Windows: vlc-unity's OnLoad runs before the first scene, when its plugin can't load yet
    // (the plugin links libvlc, which isn't loaded until TryPreload), so it throws before setting
    // the colour space (read by the D3D11 renderer). Its renderer-cleanup pump is replaced by
    // ScheduleRendererCleanup.
    private static void CompleteVlcUnityOnLoad()
    {
        try
        {
            SetColorSpace(QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear ? 1 : 0);
            Debug.Log("[VlcLibraryLoader] Set vlc-unity's colour space.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcLibraryLoader] Couldn't finish vlc-unity's OnLoad setup: {ex}");
        }
    }

    /// <summary>
    /// Frees the native renderers of VLC players destroyed around now. A released player's renderer
    /// is only retired; the plugin destroys it after two cleanup render events on separate frames,
    /// or at graphics shutdown. Call when a VLC player
    /// is torn down; the pump runs from a static hook, so it outlives the scene. A no-op on Linux,
    /// where vlc-unity's own OnLoad succeeded and hooked its pump.
    /// </summary>
    public static void ScheduleRendererCleanup()
    {
#if !(UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
        if (VLCMediaPlayer.LibVLC == null)
            return;

        // Covers the player's own OnDestroy running after the caller's (another GameObject).
        _cleanupMinFrames = CLEANUP_MIN_FRAMES;
        if (_cleanupHooked)
            return;
        _cleanupHooked = true;
        UnityEngine.Application.onBeforeRender += PumpRendererCleanup;
#endif
    }

    private const int CLEANUP_MIN_FRAMES = 10;
    private static int _cleanupMinFrames;
    private static bool _cleanupHooked;
    private static IntPtr _renderEvent;

    private static void PumpRendererCleanup()
    {
        try
        {
            if (HasRetiredRenderers())
            {
                if (_renderEvent == IntPtr.Zero)
                    _renderEvent = GetRenderEventFunc();
                GL.IssuePluginEvent(_renderEvent, 3);
                return;
            }

            if (--_cleanupMinFrames > 0)
                return;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcLibraryLoader] Renderer cleanup failed: {ex}");
        }

        UnityEngine.Application.onBeforeRender -= PumpRendererCleanup;
        _cleanupHooked = false;
    }

    // Not libVLCUnityPlugin, which vlc-unity's own imports use on Mac: that name's first load fails
    // at startup, before libvlc is loaded, and every import using it then stays failed for the
    // process. VLCUnityPlugin is the name LibVLCSharp uses, so on Mac it first loads (as the
    // re-export shim) after the preload.
    private const string VlcUnityPlugin = "VLCUnityPlugin";

    [DllImport(VlcUnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_set_color_space")]
    private static extern void SetColorSpace(int colorSpace);

    [DllImport(VlcUnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_has_retired_renderers")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool HasRetiredRenderers();

    [DllImport(VlcUnityPlugin, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetRenderEventFunc();

#if !(UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
    private static IEnumerable<string> SearchRoots()
    {
        if (!string.IsNullOrEmpty(PathOverride))
            yield return PathOverride;
        foreach (string root in StandardRoots())
            yield return root;
    }

    // Finds the first libVLC 4 under SearchRoots and preloads it. Returns false if none is found or
    // loading fails, and the caller must then not call Core.Initialize: a failed DllImport into
    // LibVLCSharp is cached for the rest of the process, so it would also break a corrected path.
    // Checks only files before loading anything, so a VLC 3 install is never loaded.
    private static bool TryPreload(out string libDir, out string pluginsDir)
    {
        libDir = null;
        pluginsDir = null;

        var checkedDirs = new List<string>();
        foreach (string root in SearchRoots())
        {
            foreach (string dir in LibDirCandidates(root))
            {
                checkedDirs.Add(dir);
                if (!HasLibVlc(dir))
                    continue;

                if (IsVlc3(dir))
                {
                    Debug.LogWarning($"[VlcLibraryLoader] '{dir}' is VLC 3.x, which isn't supported; a VLC 4 build is required.");
                    continue;
                }

                if (root == PathOverride || string.IsNullOrEmpty(PathOverride))
                    Debug.Log($"[VlcLibraryLoader] Found libVLC in '{dir}'.");
                else
                    Debug.LogWarning($"[VlcLibraryLoader] No libVLC 4 under configured path '{PathOverride}'; using '{dir}' instead.");

                if (!LoadLibraries(dir, out string error))
                {
                    Debug.LogWarning($"[VlcLibraryLoader] Failed to pre-load libVLC from '{dir}': {error}");
                    return false;
                }

                libDir = dir;
                pluginsDir = PluginDirCandidates(dir).FirstOrDefault(Directory.Exists);
                if (pluginsDir != null)
                    Debug.Log($"[VlcLibraryLoader] Pre-loaded libVLC from '{dir}', plugins from '{pluginsDir}'.");
                else
                    Debug.LogWarning($"[VlcLibraryLoader] Pre-loaded libVLC from '{dir}' but found no plugins folder " +
                        $"(checked: {string.Join(", ", PluginDirCandidates(dir))}).");
                return true;
            }
        }

        Debug.Log($"[VlcLibraryLoader] No libVLC 4 found (checked: {string.Join(", ", checkedDirs)}).");
        return false;
    }
#endif

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private static IEnumerable<string> StandardRoots()
    {
        yield return "/Applications/VLC.app";
        string home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
            yield return Path.Combine(home, "Applications", "VLC.app");
    }

    // The folder itself, a VLC 4 app bundle's Contents/Frameworks, a standalone libVLC SDK's lib.
    private static IEnumerable<string> LibDirCandidates(string root) => new[]
    {
        root,
        Path.Combine(root, "Contents", "Frameworks"),
        Path.Combine(root, "lib"),
    };

    // Beside the libraries (a VLC 4 bundle's Contents/Frameworks/plugins), or an SDK's lib/vlc/plugins.
    private static IEnumerable<string> PluginDirCandidates(string libDir) => new[]
    {
        Path.Combine(libDir, "plugins"),
        Path.Combine(libDir, "vlc", "plugins"),
    };

    private static bool HasLibVlc(string dir) =>
        File.Exists(Path.Combine(dir, "libvlc.dylib")) && File.Exists(Path.Combine(dir, "libvlccore.dylib"));

    // libVLC 3's library is libvlc.5.dylib; 4's is libvlc.12.dylib.
    private static bool IsVlc3(string dir) => File.Exists(Path.Combine(dir, "libvlc.5.dylib"));

    // libvlc depends on libvlccore, so load that first. RTLD_GLOBAL makes LibVLCSharp's later
    // bare-name DllImports resolve to these images.
    private static bool LoadLibraries(string dir, out string error) =>
        Dlopen(Path.Combine(dir, "libvlccore.dylib"), out error) &&
        Dlopen(Path.Combine(dir, "libvlc.dylib"), out error);

    // NativeLibrary isn't available at this project's API compatibility level, hence raw dlopen.
    [DllImport("libSystem.dylib", EntryPoint = "dlopen")]
    private static extern IntPtr DlopenNative(string path, int mode);

    [DllImport("libSystem.dylib", EntryPoint = "dlerror")]
    private static extern IntPtr Dlerror();

    private const int RTLD_NOW_GLOBAL = 0x2 | 0x8;

    private static bool Dlopen(string path, out string error)
    {
        // Binds the import (its first-call resolution runs dlopen/dlsym, clearing the error) and
        // clears any stale error, so the one read below is this dlopen's.
        Dlerror();
        if (DlopenNative(path, RTLD_NOW_GLOBAL) != IntPtr.Zero)
        {
            error = null;
            return true;
        }
        error = Marshal.PtrToStringAnsi(Dlerror()) ?? $"dlopen failed for '{path}'";
        return false;
    }
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    // libvlc binds at launch, through the library path: vlc-unity's plugin (DT_NEEDED libvlc.so.12,
    // loaded by Unity before graphics init, as its Vulkan renderer needs) and LibVLCSharp's bare
    // "libvlc" import both resolve there. So use the libvlc the plugin bound, never a second copy:
    // a VlcLibraryPath changed since launch would otherwise load another libvlc beside it.
    // Returns false when the plugin missed the startup load; then the "libvlc" import would fail
    // and poison itself for the process, and under Vulkan libVLC would open a window of its own.
    private static bool TryFindBoundLibVlc(out string libDir, out string pluginsDir)
    {
        libDir = null;
        pluginsDir = null;

        // Only the relaunch puts PathOverride on the library path, and it skips an unusable folder.
        string overrideProblem = string.IsNullOrEmpty(PathOverride) ? null : LibVlcFolderProblem(PathOverride);

        string libvlc = MappedFile("libvlc.so.");
        if (MappedFile("libVLCUnityPlugin.so") == null || libvlc == null)
        {
            Debug.LogWarning(overrideProblem != null
                ? $"[VlcLibraryLoader] {InvalidOverrideMessage(overrideProblem)} No libVLC 4 was found on the default " +
                    "library path either; using Unity's video player."
                : "[VlcLibraryLoader] vlc-unity's plugin didn't load at startup (libvlc wasn't on the library path at " +
                    "launch); using Unity's video player.");
            return false;
        }

        libDir = Path.GetDirectoryName(libvlc);
        if (!File.Exists(Path.Combine(libDir, "libvlc.so")))
        {
            Debug.LogWarning($"[VlcLibraryLoader] '{libDir}' has no unversioned libvlc.so, which LibVLCSharp imports; " +
                "using Unity's video player.");
            return false;
        }

        if (overrideProblem != null)
            Debug.LogWarning($"[VlcLibraryLoader] {InvalidOverrideMessage(overrideProblem)} Using '{libDir}' from the " +
                "default library path instead.");
        else if (!string.IsNullOrEmpty(PathOverride) && !LibDirCandidates(PathOverride).Select(RealPath).Contains(libDir))
            Debug.LogWarning($"[VlcLibraryLoader] VlcLibraryPath is '{PathOverride}', but libVLC was bound from " +
                $"'{libDir}' at launch; restart YARG to use it.");

        pluginsDir = PluginDirCandidates(libDir).FirstOrDefault(Directory.Exists);
        Debug.Log($"[VlcLibraryLoader] Using libVLC bound at launch from '{libDir}', plugins from '{pluginsDir ?? "nowhere"}'.");
        return true;
    }

    private static string InvalidOverrideMessage(string problem) =>
        $"VlcLibraryPath '{PathOverride}' isn't a usable libVLC 4 folder ({problem}), so YARG didn't launch with it. " +
        "To fix it, point VlcLibraryPath at the folder holding libvlc.so and libvlccore.so* (or its parent, with " +
        "them in lib/), from a libVLC 4 SDK or -dev/-devel install, then restart YARG.";

    // Why root holds no libVLC 4 the relaunch would use (see ConfiguredLibDir), or null if it does.
    private static string LibVlcFolderProblem(string root)
    {
        if (!Directory.Exists(root))
            return "the folder doesn't exist";

        var dirs = LibDirCandidates(root).ToList();
        if (dirs.Any(dir => HasLibVlc(dir) && !IsVlc3(dir)))
            return null;
        if (dirs.Any(IsVlc3))
            return "it's VLC 3.x";
        if (dirs.Any(dir => Directory.Exists(dir) && Directory.EnumerateFiles(dir, "libvlc.so.*").Any()))
            return "it has a versioned libvlc.so.N but no unversioned libvlc.so, or no libvlccore.so*";
        return "no libvlc.so in it or its lib/ folder";
    }

    // The first mapped file whose name starts with fileNamePrefix. Mapped paths are canonical.
    private static string MappedFile(string fileNamePrefix) =>
        File.ReadLines("/proc/self/maps")
            .Select(line => line.IndexOf('/') is var i && i >= 0 ? line.Substring(i) : null)
            .FirstOrDefault(path => path != null && Path.GetFileName(path).StartsWith(fileNamePrefix, StringComparison.Ordinal));

    // Canonical form, to compare with mapped paths (e.g. Fedora Atomic's /home -> /var/home).
    private static string RealPath(string path)
    {
        IntPtr resolved = RealPathNative(path, IntPtr.Zero);
        if (resolved == IntPtr.Zero)
            return null;
        try
        {
            return Marshal.PtrToStringAnsi(resolved);
        }
        finally
        {
            Free(resolved);
        }
    }

    [DllImport("libc.so.6", EntryPoint = "realpath")]
    private static extern IntPtr RealPathNative(string path, IntPtr resolved);

    [DllImport("libc.so.6", EntryPoint = "free")]
    private static extern void Free(IntPtr ptr);

    // The folder itself (pointed directly at the lib dir), or its lib subfolder (an SDK/install root).
    private static IEnumerable<string> LibDirCandidates(string root) => new[]
    {
        root,
        Path.Combine(root, "lib"),
    };

    // The standard libVLC install layout (<libdir>/vlc/plugins), or beside/a sibling of libDir.
    private static IEnumerable<string> PluginDirCandidates(string libDir) => new[]
    {
        Path.Combine(libDir, "vlc", "plugins"),
        Path.Combine(libDir, "plugins"),
        Path.Combine(Path.GetDirectoryName(libDir) ?? string.Empty, "plugins"),
    };

    // For the relaunch. LibVLCSharp imports bare "libvlc", which resolves to the unversioned
    // libvlc.so (what a -dev/-devel package provides); libvlccore may be versioned-only.
    private static bool HasLibVlc(string dir) =>
        File.Exists(Path.Combine(dir, "libvlc.so")) && LibVlcCorePath(dir) != null;

    // libVLC 3's SONAME is libvlc.so.5; 4's is libvlc.so.12.
    private static bool IsVlc3(string dir) => File.Exists(Path.Combine(dir, "libvlc.so.5"));

    private static string LibVlcCorePath(string dir)
    {
        string unversioned = Path.Combine(dir, "libvlccore.so");
        if (File.Exists(unversioned))
            return unversioned;
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "libvlccore.so.*").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault()
            : null;
    }

#if !UNITY_EDITOR
    // Holds the original LD_LIBRARY_PATH ("" if unset) across the relaunch; also stops a second one.
    private const string RELAUNCH_ENV = "YARG_VLC_RELAUNCH";

    // Unity loads vlc-unity's plugin before graphics init, as its Vulkan renderer needs, and that
    // load needs libvlc.so.12 on the library path, which glibc fixes at exec. So when the plugin
    // missed it and the VlcLibraryPath folder isn't on LD_LIBRARY_PATH, execve the player with it
    // added: same PID and arguments. The relaunched process puts the original value back, so
    // programs it starts don't inherit the folder; its own search list keeps it. BeforeSceneLoad
    // because PathHelper's settings path is set up at BeforeSplashScreen.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RelaunchWithLibVlcPath()
    {
        try
        {
            string original = Environment.GetEnvironmentVariable(RELAUNCH_ENV);
            if (original != null)
            {
                Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", original.Length > 0 ? original : null);
                Environment.SetEnvironmentVariable(RELAUNCH_ENV, null);
                return;
            }

            if (MappedFile("libVLCUnityPlugin.so") != null)
                return;

            string libDir = ConfiguredLibDir();
            string ldPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? string.Empty;
            if (libDir == null || ldPath.Split(':').Contains(libDir))
                return;

            var env = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                .Where(e => (string) e.Key != "LD_LIBRARY_PATH")
                .Select(e => $"{e.Key}={e.Value}")
                .Append($"LD_LIBRARY_PATH={(ldPath.Length > 0 ? libDir + ":" + ldPath : libDir)}")
                .Append($"{RELAUNCH_ENV}={ldPath}");

            Debug.Log($"[VlcLibraryLoader] Relaunching with '{libDir}' on LD_LIBRARY_PATH so vlc-unity's plugin loads at startup.");
            // Arrays end in null: execve takes NULL-terminated char* arrays.
            Execve("/proc/self/exe", Environment.GetCommandLineArgs().Append(null).ToArray(), env.Append(null).ToArray());
            Debug.LogWarning($"[VlcLibraryLoader] Relaunch failed (errno {Marshal.GetLastWin32Error()}).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcLibraryLoader] Relaunch failed: {ex}");
        }
    }

    // Only the configured folder: the standard ones are already on the default search path.
    // Read from settings.json directly, since SettingsManager loads later.
    private static string ConfiguredLibDir()
    {
        string settingsFile = Path.Combine(YARG.Helpers.PathHelper.PersistentDataPath, "settings.json");
        if (!File.Exists(settingsFile))
            return null;

        string root = (string) Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(settingsFile))["VlcLibraryPath"];
        if (string.IsNullOrEmpty(root))
            return null;

        return LibDirCandidates(root).FirstOrDefault(dir => HasLibVlc(dir) && !IsVlc3(dir));
    }

    [DllImport("libc.so.6", EntryPoint = "execve", SetLastError = true)]
    private static extern int Execve(string path, string[] argv, string[] envp);
#endif
#endif

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // Only the standard installer location; both stable and nightly installers use it.
    private static IEnumerable<string> StandardRoots() => new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC"),
    };

    private static IEnumerable<string> LibDirCandidates(string root) => new[] { root };

    private static IEnumerable<string> PluginDirCandidates(string libDir) => new[]
    {
        Path.Combine(libDir, "plugins"),
    };

    private static bool HasLibVlc(string dir) =>
        File.Exists(Path.Combine(dir, "libvlc.dll")) && File.Exists(Path.Combine(dir, "libvlccore.dll"));

    // Windows libraries carry no version in their name; read it from libvlc.dll's version resource.
    private static bool IsVlc3(string dir)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(Path.Combine(dir, "libvlc.dll")).FileMajorPart < 4;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VlcLibraryLoader] Couldn't read libvlc.dll's version in '{dir}', assuming 4.x: {ex.Message}");
            return false;
        }
    }

    // libvlc depends on libvlccore, so load that first. Windows binds LibVLCSharp's later bare-name
    // DllImports to an already-loaded module with the same base name.
    private static bool LoadLibraries(string dir, out string error) =>
        LoadLibrary(Path.Combine(dir, "libvlccore.dll"), out error) &&
        LoadLibrary(Path.Combine(dir, "libvlc.dll"), out error);

    // Resolves the library's own dependencies from its folder rather than the app's.
    private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);

    private static bool LoadLibrary(string path, out string error)
    {
        if (LoadLibraryEx(path, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH) != IntPtr.Zero)
        {
            error = null;
            return true;
        }
        error = $"LoadLibraryEx failed for '{path}' (Win32 error {Marshal.GetLastWin32Error()})";
        return false;
    }
#endif
#endif
}
