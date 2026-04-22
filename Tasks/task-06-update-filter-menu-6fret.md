# Task 06: Update Filter Menu for 6-Fret

## Goal

Update the difficulty filter in the library's filter menu to consider 6-fret instruments when a 6-fret profile is active.

## Background

The library has a difficulty filter menu that lets players filter songs by instrument and difficulty. The filter determines which instrument to use for matching by calling `GetDifficultyFilterInstrument()`.

The current implementation returns the first non-bot player's `CurrentInstrument` property, which would naturally be a 6-fret instrument enum value (e.g., `SixFretGuitar`) when the player is in a 6-fret game mode. This task investigates whether additional changes are needed.

## File Locations

| File | Relevant Lines |
|------|----------------|
| `Assets/Script/Menu/Filters/FiltersMenu.cs` | Lines 1459-1466 (`GetDifficultyFilterInstrument` method) |

## Current Code

The `GetDifficultyFilterInstrument()` method:

```csharp
private static Instrument GetDifficultyFilterInstrument()
{
    foreach (var player in PlayerContainer.Players)
    {
        if (!player.Profile.IsBot) return player.Profile.CurrentInstrument;
    }
    return Instrument.FiveFretGuitar;
}
```

## Analysis

### What the Current Code Does

1. Iterates through all connected players
2. Returns the `CurrentInstrument` of the first non-bot player
3. If all players are bots (or no players), defaults to `Instrument.FiveFretGuitar`

### Does This Work for 6-Fret?

**Yes, the current logic already works correctly for 6-fret profiles:**

- When a player has a 6-fret profile active, `player.Profile.CurrentInstrument` returns the appropriate 6-fret enum value (e.g., `Instrument.SixFretGuitar`)
- This value is used directly by the filter matching logic
- The filter comparison uses the returned `Instrument` value to match against song difficulty entries

### Potential Issues to Investigate

While the core logic works, verify these areas:

1. **Filter dropdown population** — Does the filter dropdown show 6-fret instruments as options?
   - Check if the dropdown items are populated from the `Instrument` enum
   - If the dropdown explicitly lists only 5-fret instruments, it needs to be updated

2. **Difficulty matching** — Does the filter correctly match 6-fret difficulties?
   - Verify that songs with `SixFretGuitar` track data are matched when the filter is set to `SixFretGuitar`
   - Check the filter comparison logic (likely uses `entry.ContainsKey(filterInstrument)`)

3. **Default fallback** — When no 6-fret profile is active, should the default remain `FiveFretGuitar`?
   - The current default is `Instrument.FiveFretGuitar` — this is probably correct for non-6-fret users
   - Consider whether the default should adapt based on the first available 6-fret instrument

## Steps

### Step 1: Investigate Filter Dropdown

Search for where the filter dropdown populates its instrument options:
- Look for code that builds the dropdown list in `FiltersMenu.cs`
- Check if the list includes 6-fret instrument enum values
- If 6-fret instruments are missing from the dropdown, add them

### Step 2: Verify Filter Matching

Trace the filter matching logic:
- Find where `GetDifficultyFilterInstrument()` result is used
- Verify the comparison handles 6-fret enum values correctly
- Ensure songs with 6-fret track data are properly filtered

### Step 3: Apply Changes (If Needed)

If investigation reveals missing 6-fret support in the dropdown or matching logic, apply the necessary fixes.

## Possible Changes

If the filter dropdown needs updating, the change might look like:

```csharp
// If the dropdown explicitly lists instruments, add 6-fret entries:
new FilterOption("Guitar", Instrument.SixFretGuitar),
new FilterOption("Bass", Instrument.SixFretBass),
new FilterOption("Rhythm", Instrument.SixFretRhythm),
new FilterOption("Co-op", Instrument.SixFretCoopGuitar),
```

## Verification

Run the build command:

```bash
dotnet build Assembly-CSharp.csproj
```

The build should complete with 0 errors.

## Notes

- **This task may require no code changes** if the existing implementation already handles 6-fret correctly
- The investigation is the primary deliverable — confirm whether additional changes are needed
- If changes are minimal (e.g., just adding dropdown entries), they should be included in this task
- The filter relies on `PlayerContainer.Players` and `player.Profile.CurrentInstrument` — these should already return correct 6-fret values
- If the filter dropdown is populated from the `Instrument` enum directly (rather than hardcoded), no changes are needed
