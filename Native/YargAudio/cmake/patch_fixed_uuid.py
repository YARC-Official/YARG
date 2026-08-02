#!/usr/bin/env python3
"""Overwrite the LC_UUID load command with a fixed constant in every Mach-O
slice of a binary (thin or universal/fat).

Why: dyld4 (macOS 15+) refuses to load dylibs without an LC_UUID, so the
linker must not strip it. The linker's default random UUID, however, breaks
the byte-reproducible builds that CI uses to verify the committed plugin.
Patching a fixed UUID after linking keeps the binary deterministic AND
loadable on modern macOS.
"""

import struct
import sys

FAT_MAGIC = 0xCafebabe
FAT_MAGIC_64 = 0xCafebabf
MH_MAGIC_64 = 0xFeedFacf
MH_MAGIC = 0xFeedFace
LC_UUID = 0x1B

# A fixed, arbitrary UUID (no semantic meaning; see module docstring).
UUID_BYTES = bytes.fromhex("2DF88E0BA3EF46D0B4370A0125CCE21C")


def patch_slice(data, offset):
    magic = struct.unpack_from("<I", data, offset)[0]
    if magic == MH_MAGIC_64:
        header_size = 32
    elif magic == MH_MAGIC:
        header_size = 28
    else:
        raise ValueError(f"not a Mach-O slice at offset {offset} (magic {magic:#x})")

    ncmds, sizeofcmds = struct.unpack_from("<II", data, offset + 16)
    cursor = offset + header_size
    region_end = cursor + sizeofcmds
    for _ in range(ncmds):
        cmd, cmdsize = struct.unpack_from("<II", data, cursor)
        if cmd == LC_UUID:
            if cursor + cmdsize > region_end:
                raise ValueError("LC_UUID extends past load command region")
            data[cursor + 8:cursor + 24] = UUID_BYTES
            return
        cursor += cmdsize
    raise ValueError(f"no LC_UUID load command found at offset {offset}")


def patch_binary(path):
    with open(path, "r+b") as f:
        data = bytearray(f.read())

    magic = struct.unpack_from(">I", data, 0)[0]
    if magic in (FAT_MAGIC, FAT_MAGIC_64):
        nfat_arch, = struct.unpack_from(">I", data, 4)
        entry_size = 32 if magic == FAT_MAGIC_64 else 20
        for i in range(nfat_arch):
            entry = 8 + i * entry_size
            if magic == FAT_MAGIC_64:
                offset, = struct.unpack_from(">Q", data, entry + 8)
            else:
                offset, = struct.unpack_from(">I", data, entry + 8)
            patch_slice(data, offset)
    else:
        patch_slice(data, 0)

    with open(path, "wb") as f:
        f.write(data)


def main():
    if len(sys.argv) != 2:
        print(f"usage: {sys.argv[0]} <mach-o binary>", file=sys.stderr)
        return 2
    patch_binary(sys.argv[1])
    return 0


if __name__ == "__main__":
    sys.exit(main())
