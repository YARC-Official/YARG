# Task 01: Remove Range Shift Setting from 6-Fret Guitar

## Goal

Remove the "5-Lane Range Shift Markers" toggle from the profile settings sidebar when the game mode is `SixFretGuitar`. Range shift is a 5-lane-specific mechanic and should not appear for 6-fret guitar.

## Background

The `GameModeExtensions.cs` file defines which profile settings are available for each game mode via the `PossibleProfileSettings()` method. Currently, the `SixFretGuitar` case incorrectly includes the `RANGE_DISABLE` setting, which controls "5-Lane Range Shift Markers" — a mechanic that only exists for 5-lane guitar.

## File Locations

| File | Relevant Lines |
|------|----------------|
| `Assets/Script/Helpers/Extensions/GameModeExtensions.cs` | Lines 89-93 |

## Current Code

In `GameModeExtensions.cs`, the `PossibleProfileSettings()` method contains this case for 6-fret guitar:

```csharp
GameMode.SixFretGuitar => new()
{
    (ProfileSettingStrings.LEFTY_FLIP, null),
    (ProfileSettingStrings.RANGE_DISABLE, "5-LANE RANGE SHIFT MARKERS"),
},
```

## Steps

1. Open `/home/theli/Projects/YARG/Assets/Script/Helpers/Extensions/GameModeExtensions.cs`
2. Locate the `PossibleProfileSettings()` method (around line 89)
3. Find the `GameMode.SixFretGuitar` case
4. Remove the line `(ProfileSettingStrings.RANGE_DISABLE, "5-LANE RANGE SHIFT MARKERS")`
5. The case should only contain the `LEFTY_FLIP` setting after the edit

## Expected Final Code

```csharp
GameMode.SixFretGuitar => new()
{
    (ProfileSettingStrings.LEFTY_FLIP, null),
},
```

## Verification

Run the build command:

```bash
dotnet build Assembly-CSharp.csproj
```

The build should complete with 0 errors.

## Notes

- This is a simple one-line removal — no additional cleanup is needed
- The `RANGE_DISABLE` setting will naturally still be available for 5-lane game modes
- No other files need to be modified for this task
