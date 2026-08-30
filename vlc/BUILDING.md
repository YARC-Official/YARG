# Building the VLC video backend

The VLC video path is built from two upstream projects:

- `vlc-unity/` — native renderer plugin (`libVLCUnityPlugin`), built with meson
- `LibVLCSharp/` — managed C# binding (`LibVLCSharp.dll`), built with dotnet

These are **not** submodules; `vlc/build.sh` shallow-clones both, builds the
assets and imports them into the Unity project (see *Updating the assets*
below). The checkouts are gitignored. The commits they were built from are
recorded in `vlc/BUILT_WITH`.

The built artifacts are committed to the repo so a plain checkout works without
any build tooling:

- `Assets/Plugins/vlc/Linux/x86_64/libVLCUnityPlugin.so`
- `Assets/Plugins/vlc/LibVLCSharp.dll`

Both are only wired up for Linux (x86_64) right now. The plugin is excluded on
all other platforms in its `.meta` import settings, and all game code touching
LibVLCSharp/VLCUnity is behind `VLC_SUPPORTED`
(`UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX` in `YargVideoPlayer.cs`).

## Prerequisites

- libVLC 4.x development files (headers + pkg-config file `libvlc.pc`). This is
  an unreleased branch, so you most likely have to build libVLC itself from
  source first.
- .NET SDK (for LibVLCSharp)
- meson + ninja (for vlc-unity)

### Building libVLC 4.x (Linux)

vlc-unity contains a helper script that builds libvlc in the same Docker image
its CI uses:

```sh
cd vlc-unity
./build-libvlc-linux.sh
# Output: ./linux-x86_64/linux-install/
```

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

## libVLCUnityPlugin.so

The native plugin is built with meson against the libVLC built above:

```sh
cd vlc-unity

# Fix up the prefix inside libvlc.pc so pkg-config resolves the local build
sed -i "1s|.*|prefix=$(pwd)/linux-x86_64/linux-install|" \
    linux-x86_64/linux-install/lib/pkgconfig/libvlc.pc

PKG_CONFIG_PATH=$(pwd)/linux-x86_64/linux-install/lib/pkgconfig \
    meson setup build_linux_x86_64 --buildtype release
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

## Updating the assets

Everything above is automated by `vlc/build.sh`, which:

1. Shallow-clones (depth 1) `LibVLCSharp` (from `master`, matching
   vlc-unity's CI — the default `3.x` branch has a restructured
   `LibVLCSharp.Shared.*` namespace that doesn't compile against vlc-unity)
   and `vlc-unity` into this directory
2. Builds `LibVLCSharp.dll` and `libVLCUnityPlugin.so`
3. Copies the binaries to `Assets/Plugins/vlc/` and mirrors
   `vlc-unity/Assets/VLCUnity/Internal` (+ asmdef) into `Assets/VLCUnity/`
4. Writes the used commits to `vlc/BUILT_WITH`
5. Stages and commits everything as `Updating vlc assets`, with the exact
   hashes and a changelog against the previously built commits
   (obtained by comparing against `vlc/BUILT_WITH`)

```sh
./vlc/build.sh           # build + import + commit
./vlc/build.sh --no-commit  # build + import, leave changes staged
```

The script does not build libVLC itself — it links the plugin against the
libvlc 4.x dev files available to pkg-config.

YARG-specific changes to the mirrored upstream sources live in `patches/`
and are applied by the script after copying (currently: making the
auto-initialization tolerate a missing native plugin, e.g. on Windows,
where only `LibVLCSharp.dll` is shipped and the game falls back to Unity's
built-in video player).
