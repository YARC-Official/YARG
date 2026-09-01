using System;
using System.IO;
using System.Linq;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Gameplay
{
    // Locates the native libvlc binaries so LibVLCSharp's Core.Initialize() can find them.
    // Binaries live under Assets/Plugins/LibVLCNative/<Platform>/, mirroring
    // Assets/Plugins/BassNative -- NuGetForUnity's restore of VideoLAN.LibVLC.* is
    // metadata-only (no real binaries), so it isn't searched here.
    internal static class LibVlcNativePath
    {
        // Also exposes the plugins directory -- libvlc needs it to decode anything, and
        // callers should set VLC_PLUGIN_PATH explicitly rather than rely on Core.Initialize's
        // relative-path guessing.
        public readonly struct Result
        {
            public readonly string NativeDir;
            public readonly string PluginsDir;

            public Result(string nativeDir, string pluginsDir)
            {
                NativeDir = nativeDir;
                PluginsDir = pluginsDir;
            }
        }

        // We're not allowed to redistribute libVLC binaries with YARG, so nothing is vendored
        // under Assets/Plugins/LibVLCNative/ by default -- ResolveVendored exists for a future
        // platform-specific distribution story (e.g. an opt-in downloader), but for now each
        // platform below resolves against a local libVLC install instead.
        public static Result? Resolve()
        {
            string libGlob = GetLibGlob(logging: true);
            if (libGlob == null)
            {
                return null;
            }

            // A user-configured path always wins, if it actually resolves. A stale/wrong
            // path shouldn't be worse than not having the setting at all, so a miss here
            // just falls through to auto-detection below instead of failing outright.
            var userPath = SettingsManager.Settings.VlcLibraryPath.Value;
            if (!string.IsNullOrEmpty(userPath))
            {
                if (ResolveFromRoot(userPath, libGlob) is { } fromUserPath)
                {
                    YargLogger.LogFormatInfo("Using libVLC from user-configured path: {0}", userPath);
                    return fromUserPath;
                }

                YargLogger.LogFormatWarning(
                    "Configured libVLC path '{0}' doesn't contain libvlc -- falling back to auto-detection.",
                    userPath);
            }

            return AutoDetect(libGlob, logging: true);
        }

        // What Resolve() would find with no user-configured path -- used by the settings UI to
        // preview auto-detection results (e.g. to show the detected path, or "not found") without
        // touching the user's setting. Silent: this can run on every settings-menu refresh, so it
        // must not spam the log the way a real resolve failure does.
        public static string GetAutoDetectedPath()
        {
            string libGlob = GetLibGlob(logging: false);
            if (libGlob == null)
            {
                return null;
            }

            var nativeDir = AutoDetect(libGlob, logging: false)?.NativeDir;
            return nativeDir == null ? null : GetDisplayPath(nativeDir);
        }

        // On macOS, a VLC.app-derived native dir sits three levels inside the bundle
        // (VLC.app/Contents/MacOS/lib) -- showing that full path as "what got auto-detected"
        // is needlessly confusing when what the user actually installed is just VLC.app itself.
        private static string GetDisplayPath(string nativeDir)
        {
            if (Application.platform is not (RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer))
            {
                return nativeDir;
            }

            var lib = new DirectoryInfo(nativeDir);
            var macOs = lib.Name == "lib" ? lib.Parent : null;
            var contents = macOs is { Name: "MacOS" } ? macOs.Parent : null;
            var app = contents is { Name: "Contents" } ? contents.Parent : null;

            return app != null && app.Name.EndsWith(".app") ? app.FullName : nativeDir;
        }

        private static string GetLibGlob(bool logging)
        {
            string libGlob = Application.platform switch
            {
                RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => "libvlc.dll",
                RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer         => "libvlc.dylib",
                // Linux ships a SONAME-versioned file (libvlc.so.5), not an unversioned
                // libvlc.so -- that symlink only exists in a `-dev` package, which we don't
                // bundle.
                RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer     => "libvlc.so*",
                _ => null
            };

            if (libGlob == null && logging)
            {
                YargLogger.LogFormatError("libVLC is not supported on platform {0}", Application.platform);
            }

            return libGlob;
        }

        private static Result? AutoDetect(string libGlob, bool logging)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    if (ResolveVendored(libGlob, "Windows") is { } vendoredWin)
                    {
                        return vendoredWin;
                    }

                    if (ResolveWindowsVlcInstall(logging) is { } windowsInstall)
                    {
                        return windowsInstall;
                    }

                    if (logging)
                    {
                        YargLogger.LogError("Could not locate libVLC for Windows -- install VLC " +
                            "(https://www.videolan.org/) to its default location, or set a custom path in " +
                            "Settings > File Management. Video backgrounds will fall back to Unity's built-in player.");
                    }
                    return null;

                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return ResolveVendored(libGlob, "Mac") ?? ResolveMacVlcApp(logging);

                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    if (ResolveVendored(libGlob, "Linux") is { } vendoredLinux)
                    {
                        return vendoredLinux;
                    }

                    // No bundled copy. Fall back to a system-installed libvlc (e.g. `apt install
                    // libvlc-dev` / `vlc`) via LibVLCSharp's default search -- returning null here
                    // tells the caller to call Core.Initialize() with no explicit path.
                    if (logging)
                    {
                        YargLogger.LogInfo("No bundled libVLC found for Linux; falling back to the " +
                            "system-installed libvlc (install via your package manager, e.g. libvlc-dev).");
                    }
                    return null;

                default:
                    if (logging)
                    {
                        YargLogger.LogFormatError("libVLC is not supported on platform {0}", Application.platform);
                    }
                    return null;
            }
        }

        private static Result? ResolveVendored(string libGlob, string platformDir)
        {
            var root = Path.Combine(Application.dataPath, "Plugins", "LibVLCNative", platformDir);
            if (!Directory.Exists(root))
            {
                return null;
            }

            return ResolveFromRoot(root, libGlob);
        }

        // Shared by vendored lookups and user-configured paths: recursively searches `root`
        // for the native library, then locates its plugins directory as either a sibling or a
        // child of wherever the library was found (covers both the vendored layout -- lib and
        // plugins side-by-side under the same folder -- and a VLC.app-style bundle, where
        // plugins/ is a sibling of Contents/MacOS/lib, not nested inside it).
        private static Result? ResolveFromRoot(string root, string libGlob)
        {
            if (!Directory.Exists(root))
            {
                return null;
            }

            string arch = Environment.Is64BitProcess ? "x64" : "x86";
            var matches = Directory.GetFiles(root, libGlob, SearchOption.AllDirectories);
            var best = matches.FirstOrDefault(p => p.Contains(arch)) ?? matches.FirstOrDefault();

            if (best == null)
            {
                return null;
            }

            var nativeDir = Path.GetDirectoryName(best);

            var childPluginsDir = Path.Combine(nativeDir!, "plugins");
            var siblingPluginsDir = Path.Combine(Directory.GetParent(nativeDir!)?.FullName ?? nativeDir!, "plugins");

            string pluginsDir = Directory.Exists(childPluginsDir) ? childPluginsDir
                : Directory.Exists(siblingPluginsDir) ? siblingPluginsDir
                : null;

            return new Result(nativeDir, pluginsDir);
        }

        // Stopgap until we have a real distribution story for libVLC on macOS: borrow libvlc and
        // its plugins straight out of a locally-installed VLC.app rather than bundling binaries
        // we're not allowed to redistribute.
        private static Result? ResolveMacVlcApp(bool logging)
        {
            const string nativeDir = "/Applications/VLC.app/Contents/MacOS/lib";
            const string pluginsDir = "/Applications/VLC.app/Contents/MacOS/plugins";

            if (!File.Exists(Path.Combine(nativeDir, "libvlc.dylib")))
            {
                if (logging)
                {
                    YargLogger.LogError("Could not find libVLC -- install VLC (https://www.videolan.org/) so " +
                        "it's at /Applications/VLC.app, or set a custom path in Settings > File Management. " +
                        "Video backgrounds will fall back to Unity's built-in player.");
                }
                return null;
            }

            if (logging)
            {
                YargLogger.LogInfo("Using libVLC from the system VLC.app install at /Applications/VLC.app.");
            }
            return new Result(nativeDir, Directory.Exists(pluginsDir) ? pluginsDir : null);
        }

        // Same idea as ResolveMacVlcApp, for Windows's default VLC install locations -- there's
        // no vendoring/local-install detection today, so this is the first real default-location
        // check for this platform.
        private static Result? ResolveWindowsVlcInstall(bool logging)
        {
            var candidateRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var programFiles in candidateRoots)
            {
                if (string.IsNullOrEmpty(programFiles))
                {
                    continue;
                }

                var installDir = Path.Combine(programFiles, "VideoLAN", "VLC");
                if (File.Exists(Path.Combine(installDir, "libvlc.dll")))
                {
                    if (logging)
                    {
                        YargLogger.LogFormatInfo("Using libVLC from the system VLC install at {0}.", installDir);
                    }
                    var pluginsDir = Path.Combine(installDir, "plugins");
                    return new Result(installDir, Directory.Exists(pluginsDir) ? pluginsDir : null);
                }
            }

            return null;
        }
    }
}
