#!/usr/bin/env python3
"""Add 6-fret guitar icons to InstrumentIcons sprite sheets.

Adds a new column at x=2048 with guitar6, bass6, rhythm6, coop6 icons.
All existing icon coordinates remain unchanged.

Source icons:
  guitar  -> guitar6  (from 512,1536)
  bass    -> bass6    (from 0,1536)
  rhythm  -> rhythm6  (from 0,0)
  guitarCoop -> coop6 (from 1536,512)

New column positions (x=2048):
  guitar6  at (2048, 1536)
  bass6    at (2048, 1536)
  rhythm6  at (2048, 0)
  coop6    at (2048, 512)
"""

from PIL import Image, ImageDraw, ImageFont
import os
import re
import hashlib

SOURCE_ICONS = {
    "guitar6":    ("guitar",     512, 1536),
    "bass6":      ("bass",       0,   1536),
    "rhythm6":    ("rhythm",     0,   0),
    "coop6":      ("guitarCoop", 1536, 512),
}

NEW_COL_X = 2048
NEW_WIDTH = 2560
NEW_HEIGHT = 2048
CELL_SIZE = 512


def find_font():
    for fp in ["/usr/share/fonts/freefont/FreeSansBold.otf",
               "/usr/share/fonts/freefont/FreeSansBold.ttf"]:
        if os.path.exists(fp):
            try:
                return ImageFont.truetype(fp, 180)
            except:
                pass
    return ImageFont.load_default()


def create_badge(draw, font):
    r = 110
    cx, cy = CELL_SIZE - r - 30, r + 30
    draw.ellipse([cx-r, cy-r, cx+r, cy+r],
                 fill=(0, 0, 0, 180),
                 outline=(255, 255, 255, 255),
                 width=6)
    draw.text((cx-90, cy-90), "6", fill=(255, 255, 255, 255), font=font)


def generate_sheet(orig_path, out_path, meta_path):
    sheet = Image.open(orig_path)
    print(f"Loaded {orig_path}: {sheet.size}")

    # Create expanded sheet - paste original at (0,0) to keep all coords intact
    new_sheet = Image.new("RGBA", (NEW_WIDTH, NEW_HEIGHT), (0, 0, 0, 0))
    new_sheet.paste(sheet, (0, 0))

    font = find_font()

    # Add 6-fret icons in new column
    for icon_name, (source_name, sx, sy) in SOURCE_ICONS.items():
        print(f"  {icon_name} <- {source_name} at ({sx},{sy}) -> ({NEW_COL_X},{sy})")
        icon = sheet.crop((sx, sy, sx + CELL_SIZE, sy + CELL_SIZE)).copy()
        create_badge(ImageDraw.Draw(icon), font)
        new_sheet.paste(icon, (NEW_COL_X, sy))

    # Save image
    new_sheet.save(out_path)
    print(f"Saved {out_path}: {NEW_WIDTH}x{NEW_HEIGHT}")

    # Update meta file minimally
    with open(meta_path, "r") as f:
        content = f.read()

    # 1. Update maxTextureSize
    content = content.replace("maxTextureSize: 2048", "maxTextureSize: 2560", 1)

    # 2. Add sprite entries before "    outline: []" (after last sprite entry)
    new_sprites_block = ""
    for icon_name in SOURCE_ICONS:
        sid = hashlib.md5(icon_name.encode()).hexdigest()
        x, y = NEW_COL_X, SOURCE_ICONS[icon_name][2]
        new_sprites_block += f"""    - serializedVersion: 2
      name: {icon_name}
      rect:
        serializedVersion: 2
        x: {x}
        y: {y}
        width: 512
        height: 512
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData: 
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: {sid}
      internalID: -1
      vertices: []
      indices: 
      edges: []
      weights: []
"""

    # Insert before the global outline section (after last sprite's weights: [])
    content = content.replace(
        "      weights: []\n    outline: []",
        "      weights: []\n" + new_sprites_block + "    outline: []",
        1
    )

    # 3. Add nameFileIdTable entries before the closing of nameFileIdTable
    # Find the last entry in nameFileIdTable and add after it
    namefile_entries = ""
    for icon_name in SOURCE_ICONS:
        namefile_entries += f"      {icon_name}: 1095511443\n"

    # Insert before the line after nameFileIdTable entries (mipmapLimitGroupName or similar)
    content = content.replace(
        "      vocals: 1095511443\n  mipmapLimitGroupName:",
        "      vocals: 1095511443\n" + namefile_entries + "  mipmapLimitGroupName:",
        1
    )
    # Fallback for NoInstrumentIcons which may have different last entry
    if "mipmapLimitGroupName" not in content.split("nameFileIdTable:")[1].split("  ")[0] if "nameFileIdTable:" in content else True:
        content = content.replace(
            "      vocals: -595053334\n  spritePackingTag:",
            "      vocals: -595053334\n" + namefile_entries + "  spritePackingTag:",
            1
        )

    with open(meta_path, "w") as f:
        f.write(content)

    print(f"Updated {meta_path}")


if __name__ == "__main__":
    print("=== InstrumentIcons.png ===")
    generate_sheet(
        "Assets/Art/Menu/Common/InstrumentIcons.png",
        "Assets/Art/Menu/Common/InstrumentIcons.png",
        "Assets/Art/Menu/Common/InstrumentIcons.png.meta"
    )
    print()
    print("=== NoInstrumentIcons.png ===")
    generate_sheet(
        "Assets/Art/Menu/Common/NoInstrumentIcons.png",
        "Assets/Art/Menu/Common/NoInstrumentIcons.png",
        "Assets/Art/Menu/Common/NoInstrumentIcons.png.meta"
    )
    print("\nDone!")
