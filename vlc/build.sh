#!/usr/bin/env bash
# Shallow-checkout, build and import the VLC video backend assets:
#
#   - LibVLCSharp.dll            (managed binding, dotnet)
#   - libVLCUnityPlugin.so       (native renderer plugin, meson/ninja)
#   - Assets/VLCUnity/Internal   (C# sources shipped with vlc-unity)
#
# Requires libVLC 4.x dev files visible to pkg-config (NOT built here).
# The upstream commits used are persisted in vlc/BUILT_WITH and the result is
# committed with a changelog against the previously built commits.
#
# Usage: ./vlc/build.sh [--no-commit]

set -euo pipefail

NO_COMMIT=false
[[ "${1:-}" == "--no-commit" ]] && NO_COMMIT=true

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VLC_DIR="$ROOT/vlc"

LIBVLCSHARP_URL="https://code.videolan.org/videolan/LibVLCSharp.git"
LIBVLCSHARP_DIR="$VLC_DIR/LibVLCSharp"
VLC_UNITY_URL="https://code.videolan.org/videolan/vlc-unity.git"
VLC_UNITY_DIR="$VLC_DIR/vlc-unity"

PROVENANCE="$VLC_DIR/BUILT_WITH"
CHANGELOG_DEPTH=500

die() { echo "error: $*" >&2; exit 1; }
log() { echo "==> $*"; }

prev_libvlcsharp="$(sed -n 's/^libvlcsharp=//p' "$PROVENANCE" 2>/dev/null || true)"
prev_vlc_unity="$(sed -n 's/^vlc-unity=//p' "$PROVENANCE" 2>/dev/null || true)"

# ---------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------
for tool in git dotnet meson ninja pkg-config strip; do
    command -v "$tool" >/dev/null || die "$tool not found in PATH"
done
pkg-config --exists libvlc || die "libvlc 4.x dev files not found via pkg-config \
(adjust PKG_CONFIG_PATH or install libvlc-dev 4.x)"
pkg-config --atleast-version=4 libvlc || die "libvlc 4.x required, \
found: $(pkg-config --modversion libvlc)"

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
# libVLCUnityPlugin.so (links against system libvlc via pkg-config)
# ---------------------------------------------------------------------------
log "Building libVLCUnityPlugin.so"
cd "$VLC_UNITY_DIR"
meson setup build_linux_x86_64 --buildtype release
ninja -C build_linux_x86_64
strip --strip-unneeded build_linux_x86_64/PluginSource/libVLCUnityPlugin.so

# ---------------------------------------------------------------------------
# Import assets into the Unity project
# ---------------------------------------------------------------------------
log "Copying assets"
install -m 644 "$OUT/LibVLCSharp.dll" "$ROOT/Assets/Plugins/vlc/LibVLCSharp.dll"
install -m 644 "$VLC_UNITY_DIR/build_linux_x86_64/PluginSource/libVLCUnityPlugin.so" \
    "$ROOT/Assets/Plugins/vlc/Linux/x86_64/libVLCUnityPlugin.so"

# Mirror the C# sources we ship (Internal/ + asmdef); deletes files the
# upstream removed. Metas come from upstream so GUIDs stay stable.
rm -rf "$ROOT/Assets/VLCUnity/Internal"
cp -r "$VLC_UNITY_DIR/Assets/VLCUnity/Internal" "$ROOT/Assets/VLCUnity/Internal"
cp "$VLC_UNITY_DIR/Assets/VLCUnity/VLCUnity.asmdef" "$ROOT/Assets/VLCUnity/"
cp "$VLC_UNITY_DIR/Assets/VLCUnity/VLCUnity.asmdef.meta" "$ROOT/Assets/VLCUnity/"
cp "$VLC_UNITY_DIR/Assets/VLCUnity/Internal.meta" "$ROOT/Assets/VLCUnity/"

# Apply YARG-specific patches on top (see patches/0001-*: tolerate a missing
# native plugin on platforms we don't ship one for, e.g. Windows)
shopt -s nullglob
for p in "$VLC_DIR"/patches/*.patch; do
    git -C "$ROOT" apply --whitespace=nowarn "$p"
done

rm -rf "$OUT"

# ---------------------------------------------------------------------------
# Provenance + commit
# ---------------------------------------------------------------------------
cat > "$PROVENANCE" <<EOF
# Provenance of the VLC assets committed to the repo (LibVLCSharp.dll,
# libVLCUnityPlugin.so and the Assets/VLCUnity/ C# sources).
# Managed by vlc/build.sh; committed together with the assets so we always
# know which upstream commits the binaries were built from.
libvlcsharp=$libvlcsharp_hash
vlc-unity=$vlc_unity_hash
date=$(date +%F)
EOF

cd "$ROOT"
git add Assets/VLCUnity Assets/Plugins/vlc vlc/BUILT_WITH

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
