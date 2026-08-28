# Building the VLC video backend

This directory contains pinned checkouts of the two upstream projects the VLC
video path is built from:

- `vlc-unity/` — native renderer plugin (`libVLCUnityPlugin`), built with meson
- `LibVLCSharp/` — managed C# binding (`LibVLCSharp.dll`), built with dotnet

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

## Updating the submodules

`vlc-unity/` and `LibVLCSharp/` are git submodules used to pin exactly which
upstream commits the committed binaries were built from. When rebuilding,
bump the submodule pointers together with the new binaries.
