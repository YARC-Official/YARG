# Building the VLC video backend

The VLC video path is built from two upstream projects:

- `vlc-unity/` — native renderer plugin (`libVLCUnityPlugin` /
  `VLCUnityPlugin`), built with meson
- `LibVLCSharp/` — managed C# binding (`LibVLCSharp.dll`), built with dotnet

These are **not** submodules; `vlc/build.sh` shallow-clones both, builds the
assets and imports them into the Unity project (see *Updating the assets*
below). The checkouts are gitignored. The commits they were built from are
recorded in `vlc/BUILT_WITH`.

The built artifacts are committed to the repo so a plain checkout works without
any build tooling:

- `Assets/Plugins/vlc/Linux/x86_64/libVLCUnityPlugin.so`
- `Assets/Plugins/vlc/Mac/libVLCUnityPlugin.dylib` (universal: arm64 + x86_64)
- `Assets/Plugins/vlc/Windows/x86_64/VLCUnityPlugin.dll`
- `Assets/Plugins/vlc/LibVLCSharp.dll`

Game code touching LibVLCSharp/VLCUnity is behind `VLC_SUPPORTED`
(`UNITY_EDITOR_*` / `UNITY_STANDALONE_*` for Linux, macOS and Windows in
`YargVideoPlayer.cs`). On other platforms the plugins are excluded in their
`.meta` import settings and the game falls back to Unity's built-in video
player.

`vlc/build.sh` always builds `LibVLCSharp.dll` plus the **host** native plugin
(Linux `.so` or universal macOS `.dylib`). The Windows `.dll` is cross-built
with mingw-w64 whenever `x86_64-w64-mingw32-g++` is on `PATH`.

All meson invocations pass `-Dwatermark=false`. Do **not** pass
`-Dwatermark=true` — that is the Videolabs trial build (watermark + 60s
playback limit).

## Prerequisites

- .NET SDK (for LibVLCSharp)
- meson + ninja (for vlc-unity)
- curl

### Linux

- libVLC 4.x development files (headers + pkg-config file `libvlc.pc`). This is
  an unreleased branch, so you most likely have to build libVLC itself from
  source first.

vlc-unity contains a helper script that builds libvlc in the same Docker image
its CI uses:

```sh
cd vlc-unity
./build-libvlc-linux.sh
# Output: ./linux-x86_64/linux-install/
```

### macOS

- Xcode Command Line Tools (`clang`, `lipo`, `install_name_tool`)
- pkg-config (Homebrew: `brew install meson ninja pkgconf`)

The VLC 4 macOS SDKs (arm64 and x86_64 nightlies) are downloaded automatically
by `vlc/build.sh` into `vlc/sdk/` (gitignored). The plugin is linked against
those SDKs; libvlc itself is **not** shipped — at runtime the dylib looks for
`@rpath/libvlc.dylib` next to itself (`@loader_path`).

### Windows (cross, mingw-w64)

From Linux or macOS:

```sh
# macOS
brew install mingw-w64
# Debian/Ubuntu
sudo apt install mingw-w64 p7zip-full
```

`vlc/build.sh` then downloads the matching VLC 4 win64 nightly (`sdk/` inside
the archive) and meson-cross-compiles `VLCUnityPlugin.dll`. GNU ld does not
accept vlc-unity's LLVM `-Wl,-pdb=` flag; the script strips that from the
checkout before configuring. winpthread is linked statically so the DLL only
needs `libvlc.dll` + system libraries (`d3d11`, UCRT) at runtime.

libvlc itself is **not** shipped; Unity DllImport is `VLCUnityPlugin`.

## LibVLCSharp.dll

From this directory (`vlc/`):

```sh
dotnet build LibVLCSharp/src/LibVLCSharp/LibVLCSharp.csproj -c Release \
    -p:DefineConstants="UNITY DESKTOP" -f netstandard2.1 -o .
```

This produces `LibVLCSharp.dll` next to the command output, which is then
copied to `Assets/Plugins/vlc/LibVLCSharp.dll`.

Note the `UNITY DESKTOP` define constant (space-separated) — it selects the
desktop-specific code paths in LibVLCSharp.

## libVLCUnityPlugin.so (Linux)

The native plugin is built with meson against the libVLC built above:

```sh
cd vlc-unity

# Fix up the prefix inside libvlc.pc so pkg-config resolves the local build
sed -i "1s|.*|prefix=$(pwd)/linux-x86_64/linux-install|" \
    linux-x86_64/linux-install/lib/pkgconfig/libvlc.pc

PKG_CONFIG_PATH=$(pwd)/linux-x86_64/linux-install/lib/pkgconfig \
    meson setup build_linux_x86_64 --buildtype release \
    -Dfatal_warnings=false -Dwatermark=false
ninja -C build_linux_x86_64

strip --strip-unneeded \
    build_linux_x86_64/PluginSource/libVLCUnityPlugin.so
```

Copy the resulting `libVLCUnityPlugin.so` to
`Assets/Plugins/vlc/Linux/x86_64/libVLCUnityPlugin.so`.

At runtime Unity loads it by name (`libVLCUnityPlugin`); standalone builds
flatten plugins into `<app>_Data/Plugins/`, while in the editor the plugin is
picked up from the `Assets/Plugins/vlc/Linux/x86_64` tree. The
`VLC_PLUGIN_PATH` environment variable (pointing at the libVLC plugin
directory) is set automatically by `OnLoad.cs` before anything is loaded.

## libVLCUnityPlugin.dylib (macOS)

Built twice (native host arch + the other arch via a meson cross file with
`clang -arch`) against the matching VLC 4 macOS SDK, then combined with
`lipo`. See `vlc/build.sh` (`build_plugin_macos`). Copy the result to
`Assets/Plugins/vlc/Mac/libVLCUnityPlugin.dylib`. The `.meta` enables it for
OSX Universal with CPU `AnyCPU`.

## VLCUnityPlugin.dll (Windows x86_64)

Cross-compiled with mingw-w64 against the win64 SDK nightly:

```sh
# After the SDK is extracted to vlc/sdk/win64 and libvlc.pc prefix= is patched:

cd vlc-unity

PKG_CONFIG_LIBDIR=$PWD/../sdk/win64/lib/pkgconfig \
    meson setup build_windows --buildtype release \
    -Dfatal_warnings=false -Dwatermark=false \
    --cross-file ../sdk/windows-x86_64.cross.ini
ninja -C build_windows

x86_64-w64-mingw32-strip --strip-unneeded \
    build_windows/PluginSource/libVLCUnityPlugin.dll
# MinGW emits libVLCUnityPlugin.dll; Unity DllImport is VLCUnityPlugin
cp build_windows/PluginSource/libVLCUnityPlugin.dll VLCUnityPlugin.dll
```

Copy to `Assets/Plugins/vlc/Windows/x86_64/VLCUnityPlugin.dll`. Graphics API is
Direct3D 11 (Unity's default on Windows).

## Updating the assets

Everything above is automated by `vlc/build.sh`, which:

1. Shallow-clones (depth 1) `LibVLCSharp` (from `master`, matching
   vlc-unity's CI — the default `3.x` branch has a restructured
   `LibVLCSharp.Shared.*` namespace that doesn't compile against vlc-unity)
   and `vlc-unity` into this directory
2. Builds `LibVLCSharp.dll` and the host native plugin, plus the Windows
   plugin when mingw-w64 is installed. SDK nightlies are downloaded into
   `vlc/sdk/` as needed.
3. Copies the binaries to `Assets/Plugins/vlc/` and mirrors
   `vlc-unity/Assets/VLCUnity/Internal` (+ asmdef) into `Assets/VLCUnity/`
4. Writes the used commits to `vlc/BUILT_WITH` (including
   `libvlc-macos-sdk=` / `libvlc-win64-sdk=` when those plugins were built)
5. Stages and commits everything as `Updating vlc assets`, with the exact
   hashes and a changelog against the previously built commits
   (obtained by comparing against `vlc/BUILT_WITH`)

```sh
./vlc/build.sh           # build + import + commit
./vlc/build.sh --no-commit  # build + import, leave changes staged
```

The script does not build libVLC itself. On Linux it links against the libvlc
4.x dev files available to pkg-config; on macOS and Windows it links against
the downloaded SDKs. Runtime still needs libvlc 4 next to the plugin (or on
the system library path).
