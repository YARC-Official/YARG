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
using System.Text;
using LibVLCSharp;
using UnityEngine;
using Debug = UnityEngine.Debug;
#endif

/// <summary>
/// Creates the process-wide <c>VLCMediaPlayer.LibVLC</c> from YARG code, so the vendored vlc-unity
/// files stay unmodified. Nothing in the repo ships a libvlc. It finds a libVLC 4 install (the
/// VlcLibraryPath setting, then standard install locations) and preloads it by absolute path, which
/// is what binds LibVLCSharp's imports (on Linux, together with a Mono dllmap entry). It must run
/// before the first <c>VLCMediaPlayer.Awake</c>, which only builds its own LibVLC when none exists;
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
            if (!TryPreload(out string libDir, out string pluginsDir))
            {
                Debug.LogWarning("[VlcLibraryLoader] No usable libVLC 4 found; using Unity's video player.");
                return false;
            }

#if !(UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
            // Not on Linux, where vlc-unity's renderer is unused (see VlcCpuMediaPlayer).
            CompleteVlcUnityOnLoad();
#endif

            // Needs a non-empty path on Mac/Windows and ignores it on Linux. Its own lookup misses
            // (and logs it). LibVLCSharp's DllImports then bind to the libraries preloaded above.
            Core.Initialize(libDir);

            // Must be the last write before new LibVLC, whose libvlc_new reads it. Two others
            // overwrite it before this point: Core.Initialize, through the plugin's SetPluginPath
            // (Mac/Windows; a backslash-joined path that never exists on Mac), and vlc-unity's
            // OnLoad (Linux, at startup, when its plugin loads then). Any earlier set is lost.
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

#if !(UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
    // vlc-unity's OnLoad runs before the first scene, when its plugin can't load yet (the plugin
    // links libvlc, which isn't loaded until TryPreload), so it throws before setting the colour
    // space (read by the D3D11 renderer) and hooking its renderer-cleanup pump. The pump is
    // replaced by ScheduleRendererCleanup.
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

    [DllImport(VlcUnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_set_color_space")]
    private static extern void SetColorSpace(int colorSpace);
#endif

    /// <summary>
    /// Frees the native renderers of VLC players destroyed around now. A released player's renderer
    /// is only retired; the plugin destroys it after two cleanup render events on separate frames,
    /// or at graphics shutdown. Call when a VLC player
    /// is torn down; the pump runs from a static hook, so it outlives the scene. On Linux the
    /// renderer is never used, but every player is still created through the plugin, which
    /// registers one.
    /// </summary>
    public static void ScheduleRendererCleanup()
    {
        if (VLCMediaPlayer.LibVLC == null)
            return;

        // Covers the player's own OnDestroy running after the caller's (another GameObject).
        _cleanupMinFrames = CLEANUP_MIN_FRAMES;
        if (_cleanupHooked)
            return;
        _cleanupHooked = true;
        UnityEngine.Application.onBeforeRender += PumpRendererCleanup;
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

    // Not libVLCUnityPlugin, which vlc-unity's own imports use on Mac and Linux: that name's first
    // load fails at startup, before libvlc is loaded, and every import using it then stays failed
    // for the process. VLCUnityPlugin is the name LibVLCSharp uses, so it first loads (on Mac as
    // the re-export shim) after the preload.
    private const string VlcUnityPlugin = "VLCUnityPlugin";

    [DllImport(VlcUnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_has_retired_renderers")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool HasRetiredRenderers();

    [DllImport(VlcUnityPlugin, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetRenderEventFunc();

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
#endif

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private const string LIBDL = "libSystem.dylib";
    private const int RTLD_GLOBAL = 0x8;
#else
    // Not libc.so.6, which only has dlopen from glibc 2.34; libdl.so.2 has it on every glibc.
    private const string LIBDL = "libdl.so.2";
    private const int RTLD_GLOBAL = 0x100;
    private const int RTLD_NOLOAD = 0x4;

    [DllImport(LIBDL, EntryPoint = "dlsym")]
    private static extern IntPtr Dlsym(IntPtr handle, string symbol);
#endif
    private const int RTLD_NOW = 0x2;

    // NativeLibrary isn't available at this project's API compatibility level, hence raw dlopen.
    [DllImport(LIBDL, EntryPoint = "dlopen")]
    private static extern IntPtr DlopenNative(string path, int mode);

    [DllImport(LIBDL, EntryPoint = "dlerror")]
    private static extern IntPtr Dlerror();

    private static bool Dlopen(string path, out string error)
    {
        // Binds the import (its first-call resolution runs dlopen/dlsym, clearing the error) and
        // clears any stale error, so the one read below is this dlopen's.
        Dlerror();
        if (DlopenNative(path, RTLD_NOW | RTLD_GLOBAL) != IntPtr.Zero)
        {
            error = null;
            return true;
        }
        error = Marshal.PtrToStringAnsi(Dlerror()) ?? $"dlopen failed for '{path}'";
        return false;
    }
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    // Searched after VlcLibraryPath. A distro's libVLC is usually 3.x, which IsVlc3 skips.
    private static IEnumerable<string> StandardRoots() => new[]
    {
        "/usr/local/lib",
        "/usr/lib/x86_64-linux-gnu",
        "/usr/lib64",
        "/usr/lib",
    };

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

    // libVLC 3's SONAME is libvlc.so.5; 4's is libvlc.so.12.
    private const int LIBVLC_4_SONAME_MAJOR = 12;

    private static bool HasLibVlc(string dir) =>
        LibVlcFiles(dir).Any(file => SonameMajor(ReadElfDynamic(file)?.Soname) >= 0);

    private static bool IsVlc3(string dir) => LibVlcPath(dir) == null;

    // The libVLC 4 library in dir, by its SONAME rather than its file name: dllmap needs no
    // unversioned libvlc.so, and only the SONAME tells 3 from 4 when that is all there is.
    private static string LibVlcPath(string dir)
    {
        foreach (string file in LibVlcFiles(dir))
        {
            string soname = ReadElfDynamic(file)?.Soname;
            if (SonameMajor(soname) >= LIBVLC_4_SONAME_MAJOR)
            {
                string bySoname = Path.Combine(dir, soname);
                return File.Exists(bySoname) ? bySoname : file;
            }
        }
        return null;
    }

    private static IEnumerable<string> LibVlcFiles(string dir) =>
        Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "libvlc.so*").OrderBy(f => f, StringComparer.Ordinal)
            : Enumerable.Empty<string>();

    private static int SonameMajor(string soname)
    {
        const string prefix = "libvlc.so.";
        return soname != null && soname.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(soname.Substring(prefix.Length).Split('.')[0], out int major)
                ? major
                : -1;
    }

    // Loads libvlc by absolute path with the libraries it needs, then the bundled libraries its
    // plugins need. libVLC dlopens its plugins itself, and glibc resolves their DT_NEEDED only
    // against the search path and already-loaded SONAMEs, so a dependency that only ships beside
    // libVLC (a snap's FFmpeg, a self-contained build's libidn.so.12) must already be loaded.
    // LibVLCSharp's bare "libvlc" import isn't matched by SONAME, so MapLibVlcImport binds it.
    private static bool LoadLibraries(string dir, out string error)
    {
        string libvlc = LibVlcPath(dir);
        var loader = new BundledLibraryLoader(dir);
        if (!loader.Load(libvlc, out error) || !MapLibVlcImport(libvlc, out error))
            return false;

        string pluginsDir = PluginDirCandidates(dir).FirstOrDefault(Directory.Exists);
        if (pluginsDir != null)
            loader.LoadPluginDependencies(pluginsDir);
        return true;
    }

    // A bundle (a snap's usr/lib, an SDK's lib) can carry its own copies of system libraries,
    // down to a whole graphics stack (Mesa's libGL and libgbm, X11, Wayland). Loading those
    // beside the system's would put two copies of one library in the process, so a library from
    // the bundle's multiarch folder is loaded only when the system has no library by that
    // SONAME. libVLC's own folders always win: its libvlccore.so.9 must not resolve to a system
    // VLC 3's of the same SONAME, and its plugins' helper libraries (<libdir>/vlc/libvlc_*.so.0)
    // are found through a RUNPATH of /usr/lib/vlc, wrong once the bundle is moved. Dependencies
    // load first, so glibc finds each one loaded when it resolves the DT_NEEDED naming it.
    private sealed class BundledLibraryLoader
    {
        // Debian multiarch, where a snap or Debian-built bundle keeps its third-party libraries.
        private const string MULTIARCH_DIR = "x86_64-linux-gnu";

        // Where the system's own libraries live, across Fedora, Debian/Ubuntu, Arch and the
        // Freedesktop runtime.
        private static readonly string[] SystemLibDirs =
            { "/usr/lib64", "/lib64", "/usr/lib/x86_64-linux-gnu", "/lib/x86_64-linux-gnu", "/usr/lib", "/lib" };

        private readonly string[] _ownDirs;
        private readonly string _multiarchDir;
        private readonly HashSet<string> _visited = new();

        public BundledLibraryLoader(string libDir)
        {
            _ownDirs = new[] { libDir, Path.Combine(libDir, "vlc") };
            _multiarchDir = Path.Combine(libDir, MULTIARCH_DIR);
        }

        public bool Load(string path, out string error)
        {
            error = null;
            if (!_visited.Add(path))
                return true;

            foreach (string needed in ReadElfDynamic(path)?.Needed ?? Enumerable.Empty<string>())
            {
                string bundled = BundledPath(needed);
                if (bundled != null && !Load(bundled, out error))
                    return false;
            }

            return Dlopen(path, out error);
        }

        // Plugins whose dependencies still fail are only unavailable; libVLC logs and skips them.
        // gui/ is skipped: those are VLC's own interfaces (Qt), never used here.
        public void LoadPluginDependencies(string pluginsDir)
        {
            int before = _visited.Count;
            var failed = new List<string>();
            var plugins = Directory.EnumerateFiles(pluginsDir, "*.so", SearchOption.AllDirectories)
                .Where(plugin => !plugin.StartsWith(Path.Combine(pluginsDir, "gui") + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            foreach (string plugin in plugins)
            {
                foreach (string needed in ReadElfDynamic(plugin)?.Needed ?? Enumerable.Empty<string>())
                {
                    string bundled = BundledPath(needed);
                    if (bundled != null && !_visited.Contains(bundled) && !Load(bundled, out string error))
                        failed.Add(error);
                }
            }

            if (_visited.Count > before)
                Debug.Log($"[VlcLibraryLoader] Loaded {_visited.Count - before} bundled libraries for libVLC's plugins.");
            if (failed.Count > 0)
                Debug.LogWarning("[VlcLibraryLoader] Some bundled libraries didn't load, so the plugins needing them " +
                    $"are unavailable: {string.Join("; ", failed.Distinct())}");
        }

        private string BundledPath(string soname)
        {
            if (IsLoaded(soname))
                return null;

            string own = _ownDirs.Select(dir => Path.Combine(dir, soname)).FirstOrDefault(File.Exists);
            if (own != null)
                return own;

            string multiarch = Path.Combine(_multiarchDir, soname);
            if (File.Exists(multiarch) && !SystemHas(soname))
                return multiarch;
            return null;
        }

        private static bool IsLoaded(string soname) => DlopenNative(soname, RTLD_NOW | RTLD_NOLOAD) != IntPtr.Zero;

        // ReadElfDynamic rejects a 32-bit library in a shared /usr/lib.
        private static bool SystemHas(string soname) =>
            SystemLibDirs.Any(dir => ReadElfDynamic(Path.Combine(dir, soname)) != null);
    }

    // Mono resolves a DllImport by name, trying "libvlc.so" for "libvlc". glibc finds no file by
    // that name on its search path, and a bare name only matches an already-loaded library's
    // SONAME (libvlc.so.12). A dllmap entry sends the name to the loaded file instead. Mono is
    // already loaded, so RTLD_NOLOAD finds it by its SONAME (the same in every Unity player and
    // Editor); mono_dllmap_insert is part of its embedding API.
    private static bool MapLibVlcImport(string libvlcPath, out string error)
    {
        const string monoSoname = "libmonoboehm-2.0.so.1";
        IntPtr mono = DlopenNative(monoSoname, RTLD_NOW | RTLD_NOLOAD);
        IntPtr insert = mono != IntPtr.Zero ? Dlsym(mono, "mono_dllmap_insert") : IntPtr.Zero;
        if (insert == IntPtr.Zero)
        {
            error = $"couldn't find mono_dllmap_insert in {monoSoname}, so LibVLCSharp can't bind to '{libvlcPath}'";
            return false;
        }

        // Null assembly: a global entry, for LibVLCSharp's imports and vlc-unity's alike.
        Marshal.GetDelegateForFunctionPointer<MonoDllMapInsert>(insert)(IntPtr.Zero, "libvlc", null, libvlcPath, null);
        Debug.Log($"[VlcLibraryLoader] Mapped LibVLCSharp's libvlc imports to '{libvlcPath}'.");
        error = null;
        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MonoDllMapInsert(IntPtr assembly,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dll, [MarshalAs(UnmanagedType.LPUTF8Str)] string func,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string targetDll, [MarshalAs(UnmanagedType.LPUTF8Str)] string targetFunc);

    private sealed class ElfDynamic
    {
        public string Soname;
        public readonly List<string> Needed = new();
    }

    // The SONAME and DT_NEEDED entries of a 64-bit little-endian ELF file; null if it isn't one.
    private static ElfDynamic ReadElfDynamic(string path)
    {
        const uint PT_LOAD = 1, PT_DYNAMIC = 2;
        const long DT_NULL = 0, DT_NEEDED = 1, DT_STRTAB = 5, DT_SONAME = 14;

        try
        {
            using var file = File.OpenRead(path);
            using var reader = new BinaryReader(file);
            // "\x7fELF", ELFCLASS64, ELFDATA2LSB.
            if (reader.ReadUInt32() != 0x464C457F || reader.ReadByte() != 2 || reader.ReadByte() != 1)
                return null;

            file.Position = 0x20;
            long programHeaders = (long) reader.ReadUInt64();
            file.Position = 0x36;
            int entrySize = reader.ReadUInt16();
            int entryCount = reader.ReadUInt16();

            var loads = new List<(ulong vaddr, ulong offset, ulong size)>();
            long dynamicOffset = -1;
            long dynamicSize = 0;
            for (int i = 0; i < entryCount; i++)
            {
                file.Position = programHeaders + (long) i * entrySize;
                uint type = reader.ReadUInt32();
                reader.ReadUInt32(); // p_flags
                ulong offset = reader.ReadUInt64();
                ulong vaddr = reader.ReadUInt64();
                reader.ReadUInt64(); // p_paddr
                ulong fileSize = reader.ReadUInt64();
                if (type == PT_LOAD)
                    loads.Add((vaddr, offset, fileSize));
                else if (type == PT_DYNAMIC)
                    (dynamicOffset, dynamicSize) = ((long) offset, (long) fileSize);
            }
            if (dynamicOffset < 0)
                return null;

            ulong stringTable = 0;
            long soname = -1;
            var needed = new List<long>();
            file.Position = dynamicOffset;
            for (long read = 0; read + 16 <= dynamicSize; read += 16)
            {
                long tag = reader.ReadInt64();
                ulong value = reader.ReadUInt64();
                if (tag == DT_NULL)
                    break;
                if (tag == DT_NEEDED)
                    needed.Add((long) value);
                else if (tag == DT_STRTAB)
                    stringTable = value;
                else if (tag == DT_SONAME)
                    soname = (long) value;
            }

            // DT_STRTAB is a virtual address; the PT_LOAD segment holding it maps it to the file.
            var segment = loads.FirstOrDefault(load => stringTable >= load.vaddr && stringTable < load.vaddr + load.size);
            if (segment.size == 0)
                return null;
            long stringTableOffset = (long) (stringTable - segment.vaddr + segment.offset);

            string ReadString(long offset)
            {
                file.Position = stringTableOffset + offset;
                var bytes = new List<byte>();
                for (byte b = reader.ReadByte(); b != 0; b = reader.ReadByte())
                    bytes.Add(b);
                return Encoding.UTF8.GetString(bytes.ToArray());
            }

            var result = new ElfDynamic { Soname = soname >= 0 ? ReadString(soname) : null };
            result.Needed.AddRange(needed.Select(ReadString));
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
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
