# Task 05: Add 6-Fret Icon Resource Names

## Goal

Add resource name mappings for 6-fret instruments so the library sidebar can load the correct icon assets via the addressable system.

## Background

The `InstrumentExtensions.cs` file contains a `ToResourceName()` method that maps each `Instrument` enum value to a string resource name. This string is used by the library sidebar to load the corresponding icon asset from the addressable/asset bundle system.

Currently, the method has mappings for all 5-fret instruments but lacks entries for 6-fret instruments. Without these mappings, any attempt to load a 6-fret instrument icon will fall through to the catch-all `_ => null` case, resulting in a null resource name and a missing icon in the sidebar.

## File Locations

| File | Relevant Lines |
|------|----------------|
| `Assets/Script/Helpers/Extensions/InstrumentExtensions.cs` | Lines 67-91 (`ToResourceName` method) |

## Current Code

The `ToResourceName()` method currently looks like this:

```csharp
public static string ToResourceName(this Instrument instrument) => instrument switch
{
    Instrument.FiveFretGuitar     => "guitar",
    Instrument.FiveFretBass       => "bass",
    Instrument.FiveFretRhythm     => "rhythm",
    Instrument.FiveFretCoopGuitar => "guitarCoop",
    Instrument.FourLaneDrums      => "drums",
    Instrument.ProDrums           => "drumsPro",
    Instrument.FiveLaneDrums      => "drums",
    Instrument.EliteDrums         => "drumsElite",
    Instrument.Keys               => "keys",
    Instrument.ProKeys            => "keysPro",
    Instrument.Vocals             => "vocals",
    Instrument.CoopGuitar         => "guitarCoop",
    Instrument.Band               => "band",
    Instrument.Percussion         => "percussion",
    _ => null,
};
```

Note the `_ => null` catch-all at the end — this is what handles unhandled instruments (including 6-fret ones).

## Steps

1. Open `/home/theli/Projects/YARG/Assets/Script/Helpers/Extensions/InstrumentExtensions.cs`
2. Locate the `ToResourceName()` method (around line 67)
3. Add 4 new mapping entries for 6-fret instruments **before** the `_ => null` catch-all line

## Expected Final Code

```csharp
public static string ToResourceName(this Instrument instrument) => instrument switch
{
    Instrument.FiveFretGuitar     => "guitar",
    Instrument.FiveFretBass       => "bass",
    Instrument.FiveFretRhythm     => "rhythm",
    Instrument.FiveFretCoopGuitar => "guitarCoop",
    Instrument.SixFretGuitar      => "guitar6",
    Instrument.SixFretBass        => "bass6",
    Instrument.SixFretRhythm      => "rhythm6",
    Instrument.SixFretCoopGuitar  => "coop6",
    Instrument.FourLaneDrums      => "drums",
    Instrument.ProDrums           => "drumsPro",
    Instrument.FiveLaneDrums      => "drums",
    Instrument.EliteDrums         => "drumsElite",
    Instrument.Keys               => "keys",
    Instrument.ProKeys            => "keysPro",
    Instrument.Vocals             => "vocals",
    Instrument.CoopGuitar         => "guitarCoop",
    Instrument.Band               => "band",
    Instrument.Percussion         => "percussion",
    _ => null,
};
```

### Mapping Reference

| Instrument Enum Value | Resource Name String | Asset Filename (Task 03) |
|-----------------------|---------------------|--------------------------|
| `SixFretGuitar` | `"guitar6"` | `guitar6.png` |
| `SixFretBass` | `"bass6"` | `bass6.png` |
| `SixFretRhythm` | `"rhythm6"` | `rhythm6.png` |
| `SixFretCoopGuitar` | `"coop6"` | `coop6.png` |

## Verification

Run the build command:

```bash
dotnet build Assembly-CSharp.csproj
```

The build should complete with 0 errors.

## Notes

- The 4 new entries use the same naming convention as the existing 5-fret entries
- The resource names (`"guitar6"`, `"bass6"`, etc.) must match the filenames of the assets created in Task 03
- These resources are loaded by the sidebar via the addressable system (called from `SetInfo()` in `Sidebar.cs`)
- The `_ => null` catch-all remains unchanged — it still handles any other unhandled instrument types
