# Task 02: Fix Instrument Names for 6-Fret Guitar

## Goal

Update localization strings so that 6-fret instruments display with "(6-Fret)" suffix in the UI, matching the naming pattern used for 5-fret instruments.

## Background

The English localization file `en-US.json` contains instrument display names in two sections:
- `Enum.Instrument` — used for display names in menus and UI
- `SortAttribute` — used for sorting/alphabetic ordering

Currently, the `Enum.Instrument` section has bare names for 6-fret instruments (e.g., "Guitar", "Bass") while the `SortAttribute` section already has the correct suffixed names (e.g., "Guitar (6-Fret)", "Bass (6-Fret)"). The `Enum.Instrument` section needs to be updated to match.

## File Locations

| File | Relevant Lines |
|------|----------------|
| `Assets/StreamingAssets/lang/en-US.json` | Lines 37-40 |

## Current Code

In the `Enum.Instrument` section of `en-US.json`:

```json
"Enum.Instrument": {
    ...
    "SixFretGuitar": "Guitar",
    "SixFretBass": "Bass",
    "SixFretRhythm": "Rhythm",
    "SixFretCoopGuitar": "Co-op",
    ...
}
```

For comparison, the `SortAttribute` section already has the correct format:

```json
"SortAttribute.Instrument": {
    ...
    "SixFretGuitar": "Guitar (6-Fret)",
    "SixFretBass": "Bass (6-Fret)",
    "SixFretRhythm": "Rhythm (6-Fret)",
    "SixFretCoop": "Co-op (6-Fret)",
    ...
}
```

## Steps

1. Open `/home/theli/Projects/YARG/Assets/StreamingAssets/lang/en-US.json`
2. Locate the `Enum.Instrument` section (around line 37)
3. Update the 6-fret instrument display names:

   Change:
   ```json
   "SixFretGuitar": "Guitar",
   "SixFretBass": "Bass",
   "SixFretRhythm": "Rhythm",
   "SixFretCoopGuitar": "Co-op",
   ```

   To:
   ```json
   "SixFretGuitar": "Guitar (6-Fret)",
   "SixFretBass": "Bass (6-Fret)",
   "SixFretRhythm": "Rhythm (6-Fret)",
   "SixFretCoopGuitar": "Co-op (6-Fret)",
   ```

## Verification

Run the build command:

```bash
dotnet build Assembly-CSharp.csproj
```

The build should complete with 0 errors.

Additionally, verify the JSON file is valid by running:

```bash
python3 -c "import json; json.load(open('Assets/StreamingAssets/lang/en-US.json'))"
```

This should produce no output (valid JSON).

## Notes

- The `SortAttribute` section already has the correct format — no changes needed there
- This change affects all UI displays of 6-fret instrument names (song library, profile selection, etc.)
- If other language files exist (e.g., `es-ES.json`, `fr-FR.json`), they should be updated similarly, but this task focuses on the English file
