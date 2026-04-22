# Task 04: Replace Elite Drums Ring with 6-Fret Guitar Ring

## Goal

Add 6-fret instrument rings to the library sidebar, replacing the elite drums ring. The sidebar should display 6-fret instrument rings when the connected profile is using a 6-fret game mode.

## Background

The library sidebar (`Sidebar.cs`) shows exactly 10 difficulty rings per song — 5 on top and 5 on bottom. Each ring displays a difficulty tier for a specific instrument.

Currently, ring index `[7]` is assigned to `EliteDrums` (Pro Drums with extra lanes). This ring should be replaced with 6-fret guitar instruments. The sidebar should intelligently show the appropriate 6-fret ring based on which 6-fret track data is available in the song and/or which game mode the connected profile is using.

## File Locations

| File | Relevant Lines |
|------|----------------|
| `Assets/Script/Menu/MusicLibrary/Sidebar.cs` | Lines 342-423 (`UpdateDifficulties` method) |

## Current Ring Assignments

The `UpdateDifficulties()` method (around line 342-423) hardcodes ring assignments:

| Ring Index | Instrument | Notes |
|------------|------------|-------|
| `[0]` | FiveFretGuitar | Main guitar |
| `[1]` | FiveFretBass | Main bass |
| `[2]` | Drums | FourLane / Pro / 5Lane |
| `[3]` | Keys | Piano keys |
| `[4]` | Vocals | Singing |
| `[5]` | ProGuitar / CoopGuitar | Conditional assignment |
| `[6]` | ProBass / Rhythm | Conditional assignment |
| `[7]` | **EliteDrums** | ← THIS WILL BE REPLACED |
| `[8]` | ProKeys | Pro keyboard |
| `[9]` | Band | Full band |

## Current Code for Ring [7]

In `UpdateDifficulties()`, ring `[7]` is handled like this:

```csharp
if (entry.ContainsKey(Instrument.EliteDrums))
{
    _difficultyRings[7].SetInfo("drumsElite", Instrument.EliteDrums, entry[Instrument.EliteDrums]);
}
else
{
    _difficultyRings[7].SetActive(false);
}
```

## Reference: How Other Conditional Rings Work

Ring `[5]` and `[6]` show good patterns for conditional assignments:

```csharp
// Ring [5] example
if (entry.ContainsKey(Instrument.ProGuitar))
{
    _difficultyRings[5].SetInfo("keys", Instrument.ProGuitar, entry[Instrument.ProGuitar]);
}
else if (entry.ContainsKey(Instrument.CoopGuitar))
{
    _difficultyRings[5].SetInfo("guitarCoop", Instrument.CoopGuitar, entry[Instrument.CoopGuitar]);
}
else
{
    _difficultyRings[5].SetActive(false);
}
```

## Steps

1. Open `/home/theli/Projects/YARG/Assets/Script/Menu/MusicLibrary/Sidebar.cs`
2. Locate the `UpdateDifficulties()` method (around line 342-423)
3. Find the code block that handles ring `[7]` (EliteDrums assignment)
4. Replace the EliteDrums logic with 6-fret instrument logic:

## Expected Final Code

Replace the EliteDrums block with:

```csharp
if (entry.ContainsKey(Instrument.SixFretGuitar))
{
    _difficultyRings[7].SetInfo("guitar6", Instrument.SixFretGuitar, entry[Instrument.SixFretGuitar]);
}
else if (entry.ContainsKey(Instrument.SixFretBass))
{
    _difficultyRings[7].SetInfo("bass6", Instrument.SixFretBass, entry[Instrument.SixFretBass]);
}
else if (entry.ContainsKey(Instrument.SixFretRhythm))
{
    _difficultyRings[7].SetInfo("rhythm6", Instrument.SixFretRhythm, entry[Instrument.SixFretRhythm]);
}
else if (entry.ContainsKey(Instrument.SixFretCoopGuitar))
{
    _difficultyRings[7].SetInfo("coop6", Instrument.SixFretCoopGuitar, entry[Instrument.SixFretCoopGuitar]);
}
else
{
    _difficultyRings[7].SetActive(false);
}
```

### Priority Order Rationale

The instruments are checked in this order:
1. `SixFretGuitar` — primary 6-fret instrument
2. `SixFretBass` — bass variant
3. `SixFretRhythm` — rhythm guitar variant
4. `SixFretCoopGuitar` — co-op variant

This matches the priority pattern used for the other conditional rings (`[5]` and `[6]`).

## Verification

Run the build command:

```bash
dotnet build Assembly-CSharp.csproj
```

The build should complete with 0 errors.

## Notes

- The icon resource names (`guitar6`, `bass6`, `rhythm6`, `coop6`) are defined in Task 05
- The actual PNG assets for these icons are created in Task 03
- If the song has no 6-fret track data, the ring will be hidden (`SetActive(false)`) — same behavior as other rings
- The `entry` dictionary contains the difficulty data keyed by `Instrument` enum values
- No changes to the ring count or layout are needed — this is a straight swap of one ring's instrument type
