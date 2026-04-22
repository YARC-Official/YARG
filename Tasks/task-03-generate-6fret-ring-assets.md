# Task 03: Generate 6-Fret Sidebar Ring Assets (Art Task)

## Goal

Create 4 new PNG image assets for the library sidebar difficulty rings, representing 6-fret instruments.

## Background

The library sidebar displays 10 difficulty rings per song (5 on top, 5 on bottom). Each ring shows an icon representing an instrument — guitar, bass, drums, keys, vocals, etc.

Currently, there are NO 6-fret instrument ring icons. Only 5-fret instrument icons exist in the addressable asset bundle system. This task is an art/design task that creates the missing 6-fret icon assets.

The 4 instruments that need icons are:
- `SixFretGuitar` — icon based on existing "guitar" icon
- `SixFretBass` — icon based on existing "bass" icon
- `SixFretRhythm` — icon based on existing "rhythm" icon
- `SixFretCoopGuitar` — icon based on existing "guitarCoop" icon

## File Locations

| Item | Location |
|------|----------|
| Existing 5-fret icons | `Assets/Addressables/` or asset bundles containing "guitar", "bass", "rhythm", "guitarCoop" |
| New 6-fret icons | Same location as existing icons |

## Current State

The existing 5-fret instrument icon filenames follow this pattern:
- `guitar` — FiveFretGuitar
- `bass` — FiveFretBass
- `rhythm` — FiveFretRhythm
- `guitarCoop` — FiveFretCoopGuitar

These are loaded via the addressable/asset bundle system.

## Steps

### Step 1: Locate Existing Icons

Search the project for the existing instrument icon assets:
- Look in `Assets/Addressables/` directories
- Search for files named "guitar", "bass", "rhythm", "guitarCoop"
- Note the file format (PNG, SPRITE, etc.) and dimensions

### Step 2: Create New Icon Assets

For each of the 4 instruments, create a new PNG image:

1. **Use the existing 5-fret icon as a base** — open the original icon image
2. **Add a "6" badge** in a corner of the icon:
   - Recommended position: top-right or bottom-right corner
   - Style: a small circle with "6" inside, or a simple text overlay
   - The badge should be subtle but clearly recognizable
   - Use the game's existing visual style (colors, fonts, shapes)
3. **Maintain the same dimensions** as the original icon
4. **Export as PNG** with the specified filename

### Step 3: Asset Filenames

Create these 4 files:

| Filename | Instrument | Based On |
|----------|------------|----------|
| `guitar6.png` | SixFretGuitar | guitar icon |
| `bass6.png` | SixFretBass | bass icon |
| `rhythm6.png` | SixFretRhythm | rhythm icon |
| `coop6.png` | SixFretCoopGuitar | guitarCoop icon |

### Step 4: Place Assets

Place the new PNG files in the same directory/folder as the original instrument icons. This is typically within the addressable asset bundle system.

### Step 5: Register Assets (If Needed)

If the project uses addressable asset groups:
1. Open Unity Editor
2. Navigate to Window → Asset Management → Addressables → Groups
3. Find the group containing the instrument icons
4. Drag the new PNG files into the appropriate group
5. Verify the addressable names are `guitar6`, `bass6`, `rhythm6`, `coop6`

## Design Guidelines

- The "6" badge should be **subtle but recognizable** — not overwhelming the base icon
- Consider using a small circle with "6" inside (similar to game badges/indicators)
- Maintain the same color scheme and visual style as the existing icons
- The badge should not obscure important parts of the original icon
- Test at the actual display size used in the sidebar rings to ensure readability

## Verification

After placing the assets:
1. Build the project to verify no asset reference errors:
   ```bash
   dotnet build Assembly-CSharp.csproj
   ```
2. In Unity Editor, verify the addressable assets are properly registered and can be loaded
3. Confirm the assets are included in the built asset bundles

## Notes

- This is primarily an **art/design task** — the code changes that consume these assets are in Task 04 (Sidebar) and Task 05 (InstrumentExtensions)
- The code in Tasks 04 and 05 already assumes these asset names exist (`guitar6`, `bass6`, `rhythm6`, `coop6`)
- If the addressable system requires specific naming or grouping, adjust the registration step accordingly
