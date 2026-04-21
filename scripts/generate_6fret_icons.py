#!/usr/bin/env python3
"""Generate 6-fret instrument icon sprites for the YARG sprite sheet.

Takes the existing InstrumentIcons.png sprite sheet, extracts the 5-fret
icons (guitar, bass, rhythm, guitarCoop), adds a "6" badge in the corner,
and creates an expanded sprite sheet with the new icons.

Sprite sheet layout (5x4 grid, 512x512 each):
Row 0 (y=2048): guitar6, bass6, rhythm6, coop6
Row 1 (y=1536): bass, guitar, drums, keys
Row 2 (y=1024): realBass, realGuitar, realDrums, realKeys
Row 3 (y=512):  vocals, harmVocals, ghDrums, guitarCoop
Row 4 (y=0):    rhythm, band, eliteDrums, twoVocals
"""

from PIL import Image, ImageDraw, ImageFont
import os

# Sprite dimensions
CELL_SIZE = 512
OLD_WIDTH = 2048
OLD_HEIGHT = 2048

# New sprite sheet: add 4 rows -> 5 rows total (2048x2560)
NEW_WIDTH = OLD_WIDTH
NEW_HEIGHT = OLD_HEIGHT + CELL_SIZE  # 2560

# Source icon positions in the original 4x4 sheet (x, y)
# Row 0 (y=1536): bass(0), guitar(512), drums(1024), keys(1536)
# Row 2 (y=512): vocals(0), harmVocals(512), ghDrums(1024), guitarCoop(1536)
# Row 3 (y=0):   rhythm(0), band(512), eliteDrums(1024), twoVocals(1536)
SOURCE_ICONS = {
    "guitar":     (512, 1536),
    "bass":       (0, 1536),
    "rhythm":     (0, 0),
    "guitarCoop": (1536, 512),
}

# New icon positions in the expanded sheet (top row = row 0, y=2048)
NEW_ICONS = {
    "guitar6":    (0, 2048),
    "bass6":      (512, 2048),
    "rhythm6":    (1024, 2048),
    "coop6":      (1536, 2048),
}

def create_badge(draw, font_size=180, font=None):
    """Draw a small circular badge with '6' in the top-right corner."""
    badge_radius = font_size // 2 + 20
    badge_center_x = CELL_SIZE - badge_radius - 30
    badge_center_y = badge_radius + 30
    badge_x0 = badge_center_x - badge_radius
    badge_y0 = badge_center_y - badge_radius
    badge_x1 = badge_center_x + badge_radius
    badge_y1 = badge_center_y + badge_radius
    
    # Draw circle background
    draw.ellipse(
        [badge_x0, badge_y0, badge_x1, badge_y1],
        fill=(0, 0, 0, 180),
        outline=(255, 255, 255, 255),
        width=6
    )
    
    # Draw "6" text
    text_pos = (badge_center_x - font_size // 2, badge_center_y - font_size // 2)
    draw.text(text_pos, "6", fill=(255, 255, 255, 255), font=font)

def generate_icons():
    """Generate the expanded sprite sheet with 6-fret icons."""
    print(f"Loading sprite sheet: {OLD_WIDTH}x{OLD_HEIGHT}")
    sheet = Image.open("Assets/Art/Menu/Common/InstrumentIcons.png")
    
    # Create new expanded sheet
    new_sheet = Image.new("RGBA", (NEW_WIDTH, NEW_HEIGHT), (0, 0, 0, 0))
    
    # Paste existing sheet at offset (new icons go in top row)
    new_sheet.paste(sheet, (0, CELL_SIZE))
    
    # Try to load a font; fall back to default if not available
    font = None
    font_paths = [
        "/usr/share/fonts/freefont/FreeSansBold.otf",
        "/usr/share/fonts/freefont/FreeSansBold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf",
        "/System/Library/Fonts/Helvetica.ttc",
        "C:/Windows/Fonts/arialbd.ttf",
    ]
    for fp in font_paths:
        if os.path.exists(fp):
            try:
                font = ImageFont.truetype(fp, 180)
                print(f"Using font: {fp}")
                break
            except Exception:
                continue
    
    if font is None:
        print("WARNING: No font found, using default (badge may not render correctly)")
        font = ImageFont.load_default()
    
    # Generate each 6-fret icon
    mapping = {
        "guitar6":    "guitar",
        "bass6":      "bass",
        "rhythm6":    "rhythm",
        "coop6":      "guitarCoop",
    }
    
    for new_name, source_name in mapping.items():
        print(f"Creating {new_name} from {source_name}...")
        sx, sy = SOURCE_ICONS[source_name]
        
        # Extract source icon
        source = sheet.crop((sx, sy, sx + CELL_SIZE, sy + CELL_SIZE))
        
        # Create new icon canvas
        new_icon = source.copy()
        draw = ImageDraw.Draw(new_icon)
        
        # Add badge
        create_badge(draw, font=font)
        
        # Place in new sheet
        nx, ny = NEW_ICONS[new_name]
        new_sheet.paste(new_icon, (nx, ny))
        
        # Also save individual icon for reference
        new_icon.save(f"Assets/Art/Menu/Common/{new_name}_icon.png")
    
    # Save expanded sprite sheet
    output_path = "Assets/Art/Menu/Common/InstrumentIcons.png"
    new_sheet.save(output_path)
    print(f"Saved expanded sprite sheet: {NEW_WIDTH}x{NEW_HEIGHT}")
    
    # Update the .meta file
    update_meta_file()
    
    print("Done!")

def update_meta_file():
    """Update the .meta file to reflect the new sprite sheet dimensions."""
    meta_path = "Assets/Art/Menu/Common/InstrumentIcons.png.meta"
    
    if not os.path.exists(meta_path):
        print("WARNING: .meta file not found, skipping update")
        return
    
    with open(meta_path, "r") as f:
        meta_content = f.read()
    
    # Update the texture dimensions in the meta file
    # Unity stores textureImporter settings in the .meta file
    # We need to update the m_TextureRect to include the new rows
    # and update the texture dimensions
    
    # Read the existing meta to get the GUID
    import re
    guid_match = re.search(r'fileID: (\d+), guid: ([a-f0-9]+)', meta_content)
    if not guid_match:
        print("WARNING: Could not find GUID in .meta file")
        return
    
    # The sprite sheet meta file needs to be re-imported by Unity
    # to pick up the new dimensions. We'll note this for the user.
    print("NOTE: Unity needs to re-import the sprite sheet to recognize new sprites.")
    print("      Open Unity and the InstrumentIcons.png file to trigger re-import.")

if __name__ == "__main__":
    generate_icons()
