#!/usr/bin/env bash
# Shallow-checkout, build and import the VLC video backend assets:
#
#   - LibVLCSharp.dll            (managed binding, dotnet)
#   - libVLCUnityPlugin.so       (Linux native renderer plugin, meson/ninja)
#   - libVLCUnityPlugin.dylib    (macOS universal arm64+x86_64 plugin, meson/ninja)
#   - VLCUnityPlugin.dll         (Windows x86_64 plugin, meson + mingw-w64)
#   - Assets/VLCUnity/Internal   (C# sources shipped with vlc-unity)
#
# Native plugins:
#   Linux  — host build, links against system libvlc 4.x via pkg-config
#   Darwin — downloads VLC 4 macOS SDK nightlies, builds a universal dylib
#   Windows x86_64 is always cross-built with mingw-w64 when the toolchain
#   is available (from Linux or macOS). Never pass -Dwatermark=true; that is
#   the Videolabs trial build (60s limit + overlay).
#
# The upstream commits used are persisted in vlc/BUILT_WITH and the result is
# committed with a changelog against the previously built commits.
#
# Usage: ./vlc/build.sh [--no-commit]

set -euo pipefail

NO_COMMIT=false
[[ "${1:-}" == "--no-commit" ]] && NO_COMMIT=true

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VLC_DIR="$ROOT/vlc"
HOST_OS="$(uname -s)"
HOST_ARCH="$(uname -m)"

LIBVLCSHARP_URL="https://code.videolan.org/videolan/LibVLCSharp.git"
LIBVLCSHARP_DIR="$VLC_DIR/LibVLCSharp"
VLC_UNITY_URL="https://code.videolan.org/videolan/vlc-unity.git"
VLC_UNITY_DIR="$VLC_DIR/vlc-unity"

PROVENANCE="$VLC_DIR/BUILT_WITH"
CHANGELOG_DEPTH=500

# Never enable the Videolabs trial watermark / 60s limiter.
MESON_OPTS=(-Dfatal_warnings=false -Dwatermark=false)

# VLC 4.x SDK nightlies (same upstream commit across OS/arch).
# Downloaded as needed; not committed (see .gitignore vlc/sdk/).
VLC_SDK_COMMIT="2b3db140"
MACOS_SDK_ARM64_URL="https://artifacts.videolan.org/vlc/nightly-macos-arm64/20260901-0413/vlc-macos-sdk-4.0.0-dev-arm64-2b3db140.tar.gz"
MACOS_SDK_X64_URL="https://artifacts.videolan.org/vlc/nightly-macos-x86_64/20260901-0411/vlc-macos-sdk-4.0.0-dev-intel64-2b3db140.tar.gz"
WIN64_SDK_7Z_URL="https://artifacts.videolan.org/vlc/nightly-win64/20260901-0424/vlc-4.0.0-dev-win64-2b3db140.7z"
WIN64_SDK_ZIP_URL="https://artifacts.videolan.org/vlc/nightly-win64/20260901-0424/vlc-4.0.0-dev-win64-2b3db140.zip"

die() { echo "error: $*" >&2; exit 1; }
log() { echo "==> $*"; }

# Homebrew tools (meson, ninja, pkg-config, mingw) live here on macOS.
if [[ "$HOST_OS" == Darwin ]]; then
    [[ -d /opt/homebrew/bin ]] && export PATH="/opt/homebrew/bin:$PATH"
    [[ -d /usr/local/bin ]] && export PATH="/usr/local/bin:$PATH"
fi

prev_libvlcsharp="$(sed -n 's/^libvlcsharp=//p' "$PROVENANCE" 2>/dev/null || true)"
prev_vlc_unity="$(sed -n 's/^vlc-unity=//p' "$PROVENANCE" 2>/dev/null || true)"
prev_macos_sdk="$(sed -n 's/^libvlc-macos-sdk=//p' "$PROVENANCE" 2>/dev/null || true)"
prev_win64_sdk="$(sed -n 's/^libvlc-win64-sdk=//p' "$PROVENANCE" 2>/dev/null || true)"

have_mingw=false
command -v x86_64-w64-mingw32-g++ >/dev/null && have_mingw=true

# ---------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------
for tool in git dotnet meson ninja pkg-config curl; do
    command -v "$tool" >/dev/null || die "$tool not found in PATH"
done

case "$HOST_OS" in
    Linux)
        command -v strip >/dev/null || die "strip not found in PATH"
        pkg-config --exists libvlc || die "libvlc 4.x dev files not found via pkg-config \
(adjust PKG_CONFIG_PATH or install libvlc-dev 4.x)"
        pkg-config --atleast-version=4 libvlc || die "libvlc 4.x required, \
found: $(pkg-config --modversion libvlc)"
        ;;
    Darwin)
        for tool in clang lipo install_name_tool strip; do
            command -v "$tool" >/dev/null || die "$tool not found in PATH"
        done
        ;;
    *)
        die "native plugin build is only supported on Linux and macOS (host: $HOST_OS)"
        ;;
esac

# ---------------------------------------------------------------------------
# Shallow checkouts
# ---------------------------------------------------------------------------
clone_shallow() {
    local url="$1" dir="$2" branch="${3:-}"
    log "Cloning $url (depth 1${branch:+, branch $branch})"
    rm -rf "$dir"
    if [[ -n "$branch" ]]; then
        git clone --quiet --depth 1 --branch "$branch" "$url" "$dir"
    else
        git clone --quiet --depth 1 "$url" "$dir"
    fi
}

# Widen the shallow history just enough to diff against the previously built
# commit; silently gives up if it is older than what we fetched.
changes_since() {
    local dir="$1" prev="$2"
    [[ -n "$prev" ]] || return 0
    (
        cd "$dir" &&
        git fetch --quiet --deepen="$CHANGELOG_DEPTH" origin &&
        git merge-base --is-ancestor "$prev" HEAD &&
        git log --oneline --no-decorate "$prev"..HEAD
    ) 2>/dev/null || echo "(changes since $prev not available: outside fetched history)"
}

# LibVLCSharp: use the master branch (like vlc-unity's CI does) — it keeps the
# flat `LibVLCSharp.*` namespace the Assets/VLCUnity/ sources are written
# against. The default branch (3.x) restructured the sources into
# `LibVLCSharp.Shared.*`, which does not compile against vlc-unity.
clone_shallow "$LIBVLCSHARP_URL" "$LIBVLCSHARP_DIR" master
clone_shallow "$VLC_UNITY_URL" "$VLC_UNITY_DIR"

libvlcsharp_hash="$(git -C "$LIBVLCSHARP_DIR" rev-parse HEAD)"
vlc_unity_hash="$(git -C "$VLC_UNITY_DIR" rev-parse HEAD)"
log "LibVLCSharp @ ${libvlcsharp_hash:0:9}, vlc-unity @ ${vlc_unity_hash:0:9}"

libvlcsharp_changes="$(changes_since "$LIBVLCSHARP_DIR" "$prev_libvlcsharp")"
vlc_unity_changes="$(changes_since "$VLC_UNITY_DIR" "$prev_vlc_unity")"

# ---------------------------------------------------------------------------
# LibVLCSharp.dll
# ---------------------------------------------------------------------------
log "Building LibVLCSharp.dll"
OUT="$VLC_DIR/out"
rm -rf "$OUT"
dotnet build "$LIBVLCSHARP_DIR/src/LibVLCSharp/LibVLCSharp.csproj" -c Release \
    -p:DefineConstants="UNITY DESKTOP" -f netstandard2.1 -o "$OUT" --nologo -v q

# ---------------------------------------------------------------------------
# Native plugins
# ---------------------------------------------------------------------------
macos_sdk_hash=""
win64_sdk_hash=""
built_windows=false

set_pc_prefix() {
    local pc="$1" abs="$2" tmp
    tmp="$(mktemp)"
    sed "s|^prefix=.*|prefix=$abs|" "$pc" > "$tmp"
    mv "$tmp" "$pc"
}

download() {
    local url="$1" dest="$2"
    mkdir -p "$(dirname "$dest")"
    if [[ ! -f "$dest" ]]; then
        log "Downloading $(basename "$url")"
        curl -L --fail --retry 3 -o "$dest" "$url"
    fi
}

write_darwin_cross_file() {
    local arch="$1" cpu_family="$2" dest="$3"
    cat > "$dest" <<EOF
[binaries]
c = ['clang', '-arch', '$arch']
cpp = ['clang++', '-arch', '$arch']
objc = ['clang', '-arch', '$arch']
objcpp = ['clang++', '-arch', '$arch']
ar = 'ar'
strip = 'strip'
pkg-config = 'pkg-config'

[host_machine]
system = 'darwin'
cpu_family = '$cpu_family'
cpu = '$arch'
endian = 'little'
EOF
}

fetch_macos_sdk() {
    local url="$1" dest="$2" tarball="$3"
    download "$url" "$tarball"
    if [[ ! -f "$dest/lib/pkgconfig/libvlc.pc" ]]; then
        log "Extracting $(basename "$tarball")"
        rm -rf "$dest"
        mkdir -p "$dest"
        tar -xzf "$tarball" -C "$dest"
    fi
    set_pc_prefix "$dest/lib/pkgconfig/libvlc.pc" "$(cd "$dest" && pwd)"
}

# Replace every LC_RPATH with @loader_path so dyld looks next to the plugin.
set_loader_rpath() {
    local dylib="$1" rpath
    while rpath="$(otool -l "$dylib" | awk '/cmd LC_RPATH$/{c=1} c && /path /{print $2; exit}')" \
            && [[ -n "$rpath" ]]; do
        install_name_tool -delete_rpath "$rpath" "$dylib"
    done
    install_name_tool -add_rpath "@loader_path" "$dylib"
}

build_macos_arch() {
    local arch="$1" cpu_family="$2" sdk="$3"
    local builddir="$VLC_UNITY_DIR/build_macos_$arch"
    local meson_args=(setup "$builddir" --buildtype release "${MESON_OPTS[@]}")

    if [[ "$HOST_ARCH" != "$arch" ]]; then
        local cross="$VLC_DIR/sdk/macos-$arch.cross.ini"
        write_darwin_cross_file "$arch" "$cpu_family" "$cross"
        meson_args+=(--cross-file "$cross")
        log "Building libVLCUnityPlugin.dylib ($arch, cross)"
    else
        log "Building libVLCUnityPlugin.dylib ($arch, native)"
    fi

    rm -rf "$builddir"
    (
        cd "$VLC_UNITY_DIR"
        PKG_CONFIG_LIBDIR="$sdk/lib/pkgconfig" PKG_CONFIG_PATH="" \
            meson "${meson_args[@]}"
        ninja -C "$builddir"
    )
    local dylib="$builddir/PluginSource/libVLCUnityPlugin.dylib"
    [[ -f "$dylib" ]] || die "meson did not produce $dylib"
    set_loader_rpath "$dylib"
    strip -x "$dylib"
}

build_plugin_linux() {
    log "Building libVLCUnityPlugin.so"
    cd "$VLC_UNITY_DIR"
    meson setup build_linux_x86_64 --buildtype release "${MESON_OPTS[@]}"
    ninja -C build_linux_x86_64
    strip --strip-unneeded build_linux_x86_64/PluginSource/libVLCUnityPlugin.so
}

build_plugin_macos() {
    local sdk_root="$VLC_DIR/sdk"
    local sdk_arm="$sdk_root/macos-arm64"
    local sdk_x64="$sdk_root/macos-x86_64"

    fetch_macos_sdk "$MACOS_SDK_ARM64_URL" "$sdk_arm" \
        "$sdk_root/vlc-macos-sdk-arm64-${VLC_SDK_COMMIT}.tar.gz"
    fetch_macos_sdk "$MACOS_SDK_X64_URL" "$sdk_x64" \
        "$sdk_root/vlc-macos-sdk-x86_64-${VLC_SDK_COMMIT}.tar.gz"

    build_macos_arch arm64 aarch64 "$sdk_arm"
    build_macos_arch x86_64 x86_64 "$sdk_x64"

    log "Creating universal libVLCUnityPlugin.dylib"
    lipo -create \
        "$VLC_UNITY_DIR/build_macos_arm64/PluginSource/libVLCUnityPlugin.dylib" \
        "$VLC_UNITY_DIR/build_macos_x86_64/PluginSource/libVLCUnityPlugin.dylib" \
        -output "$VLC_UNITY_DIR/libVLCUnityPlugin.dylib"
    lipo -info "$VLC_UNITY_DIR/libVLCUnityPlugin.dylib"
    macos_sdk_hash="$VLC_SDK_COMMIT"
}

# GNU ld does not understand LLVM's -Wl,-pdb= (vlc-unity's Windows link line).
drop_llvm_pdb_flag() {
    python3 - <<'PY'
from pathlib import Path
p = Path("PluginSource/meson.build")
t = p.read_text()
old = """    vlc_unity_ldflags += [ '-static-libgcc', '-static-libstdc++', '-shared',
                           '-Wl,-pdb=' ]"""
new = """    vlc_unity_ldflags += [ '-static-libgcc', '-static-libstdc++', '-shared' ]"""
if old not in t:
    raise SystemExit("vlc-unity PluginSource/meson.build: expected -Wl,-pdb= link args not found")
p.write_text(t.replace(old, new, 1))
PY
}

fetch_win64_sdk() {
    local dest="$1" archive
    if [[ -f "$dest/lib/pkgconfig/libvlc.pc" ]]; then
        set_pc_prefix "$dest/lib/pkgconfig/libvlc.pc" "$(cd "$dest" && pwd)"
        return
    fi
    rm -rf "$dest"
    mkdir -p "$dest"
    local tmp
    tmp="$(mktemp -d)"
    if command -v 7zz >/dev/null || command -v 7z >/dev/null; then
        local sevenz
        sevenz="$(command -v 7zz || command -v 7z)"
        archive="$VLC_DIR/sdk/vlc-4.0.0-dev-win64-${VLC_SDK_COMMIT}.7z"
        download "$WIN64_SDK_7Z_URL" "$archive"
        log "Extracting $(basename "$archive")"
        "$sevenz" x -y -o"$tmp" "$archive" "vlc-4.0.0-dev/sdk/*" >/dev/null
    else
        archive="$VLC_DIR/sdk/vlc-4.0.0-dev-win64-${VLC_SDK_COMMIT}.zip"
        download "$WIN64_SDK_ZIP_URL" "$archive"
        log "Extracting $(basename "$archive")"
        unzip -q "$archive" "vlc-4.0.0-dev/sdk/*" -d "$tmp"
    fi
    # Nightly layout: vlc-4.0.0-dev/sdk/{include,lib}
    local sdk_src
    sdk_src="$(find "$tmp" -type d -name sdk | head -n1)"
    [[ -n "$sdk_src" ]] || die "win64 archive did not contain an sdk/ directory"
    mv "$sdk_src"/* "$dest/"
    rm -rf "$tmp"
    set_pc_prefix "$dest/lib/pkgconfig/libvlc.pc" "$(cd "$dest" && pwd)"
}

build_plugin_windows() {
    local sdk="$VLC_DIR/sdk/win64"
    fetch_win64_sdk "$sdk"

    log "Building VLCUnityPlugin.dll (x86_64 mingw, non-trial)"
    (
        cd "$VLC_UNITY_DIR"
        drop_llvm_pdb_flag
        local cross="$VLC_DIR/sdk/windows-x86_64.cross.ini"
        cat > "$cross" <<'EOF'
[binaries]
c = 'x86_64-w64-mingw32-gcc'
cpp = 'x86_64-w64-mingw32-g++'
ar = 'x86_64-w64-mingw32-ar'
strip = 'x86_64-w64-mingw32-strip'
pkg-config = 'pkg-config'
windres = 'x86_64-w64-mingw32-windres'

[built-in options]
c_link_args = ['-static-libgcc', '-Wl,-Bstatic', '-lwinpthread', '-Wl,-Bdynamic']
cpp_link_args = ['-static-libgcc', '-static-libstdc++', '-Wl,-Bstatic', '-lwinpthread', '-Wl,-Bdynamic']

[host_machine]
system = 'windows'
cpu_family = 'x86_64'
cpu = 'x86_64'
endian = 'little'
EOF
        rm -rf build_windows
        PKG_CONFIG_LIBDIR="$sdk/lib/pkgconfig" PKG_CONFIG_PATH="" \
            meson setup build_windows --buildtype release "${MESON_OPTS[@]}" \
            --cross-file "$cross"
        ninja -C build_windows
    )
    local dll="$VLC_UNITY_DIR/build_windows/PluginSource/libVLCUnityPlugin.dll"
    [[ -f "$dll" ]] || die "meson did not produce $dll"
    x86_64-w64-mingw32-strip --strip-unneeded "$dll"
    # Unity DllImport("VLCUnityPlugin") — drop the MinGW lib prefix.
    cp "$dll" "$VLC_UNITY_DIR/VLCUnityPlugin.dll"
    win64_sdk_hash="$VLC_SDK_COMMIT"
    built_windows=true
}

case "$HOST_OS" in
    Linux)  build_plugin_linux ;;
    Darwin) build_plugin_macos ;;
esac

if $have_mingw; then
    build_plugin_windows
else
    log "mingw-w64 not found; skipping Windows plugin (install x86_64-w64-mingw32-g++)"
fi

# ---------------------------------------------------------------------------
# Import assets into the Unity project
# ---------------------------------------------------------------------------
log "Copying assets"
install -m 644 "$OUT/LibVLCSharp.dll" "$ROOT/Assets/Plugins/vlc/LibVLCSharp.dll"

case "$HOST_OS" in
    Linux)
        install -d "$ROOT/Assets/Plugins/vlc/Linux/x86_64"
        install -m 644 "$VLC_UNITY_DIR/build_linux_x86_64/PluginSource/libVLCUnityPlugin.so" \
            "$ROOT/Assets/Plugins/vlc/Linux/x86_64/libVLCUnityPlugin.so"
        ;;
    Darwin)
        install -d "$ROOT/Assets/Plugins/vlc/Mac"
        install -m 755 "$VLC_UNITY_DIR/libVLCUnityPlugin.dylib" \
            "$ROOT/Assets/Plugins/vlc/Mac/libVLCUnityPlugin.dylib"
        ;;
esac

if $built_windows; then
    install -d "$ROOT/Assets/Plugins/vlc/Windows/x86_64"
    install -m 644 "$VLC_UNITY_DIR/VLCUnityPlugin.dll" \
        "$ROOT/Assets/Plugins/vlc/Windows/x86_64/VLCUnityPlugin.dll"
fi

# Mirror the C# sources we ship (Internal/ + asmdef); deletes files the
# upstream removed. Metas come from upstream so GUIDs stay stable.
rm -rf "$ROOT/Assets/VLCUnity/Internal"
cp -r "$VLC_UNITY_DIR/Assets/VLCUnity/Internal" "$ROOT/Assets/VLCUnity/Internal"
cp "$VLC_UNITY_DIR/Assets/VLCUnity/VLCUnity.asmdef" "$ROOT/Assets/VLCUnity/"
cp "$VLC_UNITY_DIR/Assets/VLCUnity/VLCUnity.asmdef.meta" "$ROOT/Assets/VLCUnity/"
cp "$VLC_UNITY_DIR/Assets/VLCUnity/Internal.meta" "$ROOT/Assets/VLCUnity/"

rm -rf "$OUT"

# ---------------------------------------------------------------------------
# Provenance + commit
# ---------------------------------------------------------------------------
{
    echo "# Provenance of the VLC assets committed to the repo (LibVLCSharp.dll,"
    echo "# libVLCUnityPlugin.{so,dylib} / VLCUnityPlugin.dll and the"
    echo "# Assets/VLCUnity/ C# sources)."
    echo "# Managed by vlc/build.sh; committed together with the assets so we always"
    echo "# know which upstream commits the binaries were built from."
    echo "libvlcsharp=$libvlcsharp_hash"
    echo "vlc-unity=$vlc_unity_hash"
    echo "date=$(date +%F)"
    if [[ -n "$macos_sdk_hash" ]]; then
        echo "libvlc-macos-sdk=$macos_sdk_hash"
    elif [[ -n "$prev_macos_sdk" ]]; then
        echo "libvlc-macos-sdk=$prev_macos_sdk"
    fi
    if [[ -n "$win64_sdk_hash" ]]; then
        echo "libvlc-win64-sdk=$win64_sdk_hash"
    elif [[ -n "$prev_win64_sdk" ]]; then
        echo "libvlc-win64-sdk=$prev_win64_sdk"
    fi
} > "$PROVENANCE"

cd "$ROOT"
git add Assets/VLCUnity Assets/Plugins/vlc vlc/BUILT_WITH
# Drop removed patch files from the index if they were tracked.
git add -u vlc/patches 2>/dev/null || true

if $NO_COMMIT; then
    log "--no-commit: changes staged, not committed"
    exit 0
fi

if git diff --cached --quiet; then
    log "Nothing changed, skipping commit"
    exit 0
fi

MSG="$(mktemp)"
trap 'rm -f "$MSG"' EXIT
{
    echo "Updating vlc assets"
    echo
    echo "LibVLCSharp: $libvlcsharp_hash"
    echo "vlc-unity: $vlc_unity_hash"
    if [[ -n "$macos_sdk_hash" ]]; then
        echo "libvlc macOS SDK: $macos_sdk_hash"
    fi
    if [[ -n "$win64_sdk_hash" ]]; then
        echo "libvlc win64 SDK: $win64_sdk_hash"
    fi
    if [[ -n "$libvlcsharp_changes" ]]; then
        echo
        echo "LibVLCSharp changes since ${prev_libvlcsharp:0:9}:"
        echo "$libvlcsharp_changes"
    fi
    if [[ -n "$vlc_unity_changes" ]]; then
        echo
        echo "vlc-unity changes since ${prev_vlc_unity:0:9}:"
        echo "$vlc_unity_changes"
    fi
} > "$MSG"

git commit --quiet -F "$MSG"
log "Committed new vlc assets"
