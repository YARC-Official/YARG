#!/usr/bin/env python3
"""Update the InstrumentIcons.png .meta file to include 6-fret sprites."""

import hashlib
import os

# New sprite entries to add (top row, y=2048)
NEW_SPRITES = [
    {"name": "guitar6", "x": 0, "y": 2048},
    {"name": "bass6", "x": 512, "y": 2048},
    {"name": "rhythm6", "x": 1024, "y": 2048},
    {"name": "coop6", "x": 1536, "y": 2048},
]

def generate_sprite_id(name):
    """Generate a consistent spriteID hash from the name."""
    h = hashlib.md5(name.encode()).hexdigest()
    return h

def update_meta():
    meta_path = "Assets/Art/Menu/Common/InstrumentIcons.png.meta"
    with open(meta_path, "r") as f:
        lines = f.readlines()
    
    # Find the spriteSheet section and the nameFileIdTable section
    # We need to:
    # 1. Update maxTextureSize from 2048 to 2560
    # 2. Add 4 new sprite entries before the "outline:" line in spriteSheet
    # 3. Add entries to nameFileIdTable
    
    output_lines = []
    in_sprites_section = False
    in_namefile_table = False
    
    for i, line in enumerate(lines):
        # Update maxTextureSize
        if "maxTextureSize: 2048" in line and i > 60:  # In platformSettings
            # Check if this is the first occurrence (platformSettings) or second (textureSettings)
            # We only want to change the one in textureSettings (around line 34)
            pass
        
        # Change maxTextureSize in textureSettings (line ~34)
        if line.strip() == "maxTextureSize: 2048" and i < 40:
            output_lines.append(line.replace("maxTextureSize: 2048", "maxTextureSize: 2560"))
            continue
        
        # Add new sprite entries before the "outline:" line in spriteSheet
        if line.strip() == "outline: []" and i > 460:
            # This is the outline line in spriteSheet - add new sprites before it
            for sprite in NEW_SPRITES:
                sprite_id = generate_sprite_id(sprite["name"])
                output_lines.append(f"    - serializedVersion: 2\n")
                output_lines.append(f"      name: {sprite['name']}\n")
                output_lines.append(f"      rect:\n")
                output_lines.append(f"        serializedVersion: 2\n")
                output_lines.append(f"        x: {sprite['x']}\n")
                output_lines.append(f"        y: {sprite['y']}\n")
                output_lines.append(f"        width: 512\n")
                output_lines.append(f"        height: 512\n")
                output_lines.append(f"      alignment: 0\n")
                output_lines.append(f"      pivot: {{x: 0.5, y: 0.5}}\n")
                output_lines.append(f"      border: {{x: 0, y: 0, z: 0, w: 0}}\n")
                output_lines.append(f"      customData: \n")
                output_lines.append(f"      outline: []\n")
                output_lines.append(f"      physicsShape: []\n")
                output_lines.append(f"      tessellationDetail: 0\n")
                output_lines.append(f"      bones: []\n")
                output_lines.append(f"      spriteID: {sprite_id}\n")
                output_lines.append(f"      internalID: -1\n")
                output_lines.append(f"      vertices: []\n")
                output_lines.append(f"      indices: \n")
                output_lines.append(f"      edges: []\n")
                output_lines.append(f"      weights: []\n")
            
            output_lines.append(line)
            continue
        
        # Add entries to nameFileIdTable
        if line.strip() == "nameFileIdTable:":
            in_namefile_table = True
            output_lines.append(line)
            continue
        
        if in_namefile_table:
            # Check if we've reached the end of nameFileIdTable
            if line.strip() and not line.startswith(" ") and not line.startswith("\t"):
                in_namefile_table = False
                output_lines.append(line)
                continue
            # Add new entries
            if line.strip() == "":
                for sprite in NEW_SPRITES:
                    output_lines.append(f"      {sprite['name']}: 1095511443\n")
                output_lines.append(line)
            else:
                output_lines.append(line)
            continue
        
        output_lines.append(line)
    
    with open(meta_path, "w") as f:
        f.writelines(output_lines)
    
    print(f"Updated {meta_path}")

if __name__ == "__main__":
    update_meta()
