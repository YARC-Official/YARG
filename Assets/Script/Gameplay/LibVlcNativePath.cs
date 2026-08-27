using System;
using System.IO;
using System.Linq;
using UnityEngine;
using YARG.Core.Logging;

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
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    if (ResolveVendored("libvlc.dll", "Windows") is { } vendoredWin)
                    {
                        return vendoredWin;
                    }

                    YargLogger.LogError("Could not locate libVLC for Windows -- video backgrounds will fall " +
                        "back to Unity's built-in player. (No local-install detection exists for Windows yet.)");
                    return null;

                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return ResolveVendored("libvlc.dylib", "Mac") ?? ResolveMacVlcApp();

                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    // Linux ships a SONAME-versioned file (libvlc.so.5), not an unversioned
                    // libvlc.so -- that symlink only exists in a `-dev` package, which we don't
                    // bundle.
                    if (ResolveVendored("libvlc.so*", "Linux") is { } vendoredLinux)
                    {
                        return vendoredLinux;
                    }

                    // No bundled copy. Fall back to a system-installed libvlc (e.g. `apt install
                    // libvlc-dev` / `vlc`) via LibVLCSharp's default search -- returning null here
                    // tells the caller to call Core.Initialize() with no explicit path.
                    YargLogger.LogInfo("No bundled libVLC found for Linux; falling back to the " +
                        "system-installed libvlc (install via your package manager, e.g. libvlc-dev).");
                    return null;

                default:
                    YargLogger.LogFormatError("libVLC is not supported on platform {0}", Application.platform);
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

            string arch = Environment.Is64BitProcess ? "x64" : "x86";
            var matches = Directory.GetFiles(root, libGlob, SearchOption.AllDirectories);
            var best = matches.FirstOrDefault(p => p.Contains(arch)) ?? matches.FirstOrDefault();

            if (best == null)
            {
                return null;
            }

            var nativeDir = Path.GetDirectoryName(best);
            var pluginsDir = Path.Combine(nativeDir!, "plugins");
            return new Result(nativeDir, Directory.Exists(pluginsDir) ? pluginsDir : null);
        }

        // Stopgap until we have a real distribution story for libVLC on macOS: borrow libvlc and
        // its plugins straight out of a locally-installed VLC.app rather than bundling binaries
        // we're not allowed to redistribute.
        private static Result? ResolveMacVlcApp()
        {
            const string nativeDir = "/Applications/VLC.app/Contents/MacOS/lib";
            const string pluginsDir = "/Applications/VLC.app/Contents/MacOS/plugins";

            if (!File.Exists(Path.Combine(nativeDir, "libvlc.dylib")))
            {
                YargLogger.LogError("Could not find libVLC -- install VLC (https://www.videolan.org/) so " +
                    "it's at /Applications/VLC.app. Video backgrounds will fall back to Unity's built-in player.");
                return null;
            }

            YargLogger.LogInfo("Using libVLC from the system VLC.app install at /Applications/VLC.app.");
            return new Result(nativeDir, Directory.Exists(pluginsDir) ? pluginsDir : null);
        }
    }
}
