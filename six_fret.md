# 6-Fret Guitar — Agent Prompt

## Core Philosophy

6-fret guitar support is **fully based on 5-fret guitar** in both engine and visuals. The only structural difference is **6 lanes instead of 5**. Everything else inherits or mirrors 5-fret behavior.

## 6-Fret Guitar Layout

- **6 lanes**: 2 rows of 3 frets
  - Top row: 3 black frets (Black1, Black2, Black3)
  - Bottom row: 3 white frets (White1, White2, White3)
- **2 colors only**: black fret and white fret (no 5-color scheme)
- **No range shift indicators** — range shift is a 5-lane-only mechanic
- **No input viewer** — 6-fret guitar has no input viewer HUD element

## Architecture

### Inheritance Chain

| Layer | 5-Fret (base) | 6-Fret (derived) | Relationship |
|-------|---------------|------------------|--------------|
| Engine (YARG.Core) | `YargFiveFretGuitarEngine` | `YargSixFretGuitarEngine` | Inherits, overrides fret mask + coda fret count (6) |
| Player (frontend) | `FiveFretGuitarPlayer` | `SixFretGuitarPlayer` | Inherits, overrides lane count (6), fret mapping, highway ordering |
| Note Element | `FiveFretGuitarNoteElement` | `SixFretGuitarNoteElement` | Inherits from `NoteElement<GuitarNote, SixFretGuitarPlayer>`, mirrors 5-fret note logic |
| Color Profile | `FiveFretGuitarColors` | `SixFretGuitarColors` | Separate class, 2-color scheme (black/white) |
| Engine Preset | `FiveFretGuitarPreset` | `FiveFretGuitarPreset` (reused) | 6-fret reuses the same preset type |
| Visual Style | `VisualStyle.FiveFretGuitar` | `VisualStyle.SixFretGuitar` | Separate enum value, same theme model structure |

### Key Enums

- `SixFretGuitarFret`: Black1(1), Black2, Black3, White1, White2, White3, Open, Wildcard
- `GuitarAction` aliases: Black1Fret=Fret1, Black2Fret=Fret2, Black3Fret=Fret3, White1Fret=Fret4, White2Fret=Fret5, White3Fret=Fret6
- Highway ordering: Black1→0, White1→1, Black2→2, White2→3, Black3→4, White3→5

### What 6-Fret Shares with 5-Fret

- Engine preset type (`FiveFretGuitarPreset` reused directly)
- Note types (Strum, HOPO, Tap, Open, Wildcard, Sustain)
- Fret array system (same `FretArray` component, different lane count)
- Stem mixing (Rhythm/Bass stem logic)
- Star Power, whammy, sustain mute, overstrum
- BRE (Beginner/Big Rock Ending) lane logic
- Coda section handling
- Score card display
- Replay frame construction
- Theme model assignment (Normal, HOPO, Tap, Open, OpenHOPO, Wildcard)

### What 6-Fret Excludes

- **Range shift** — no `SixFretRangeShift`, no range indicator pools, no shift indicator pools, no `RANGE_DISABLE` profile setting
- **Input viewer** — no `SixFretInputViewer`; the `BaseInputViewer` in `BasePlayer` is not wired for 6-fret
- **5-color scheme** — only black and white

## What Is Already Implemented

### YARG.Core (backend engine)

- [x] `YargSixFretGuitarEngine` — inherits `YargFiveFretGuitarEngine`, overrides `GetChordLowestFretMask` (iterates GreenFret→White3Fret), `CreateCodaFretMask` (6 bytes), `GetCodaFretCount` (6)
- [x] `SixFretGuitarColors` — 2-color profile (BlackFret/WhiteFret, BlackNote/WhiteNote, etc.) in `ColorProfile.SixFretGuitar.cs`
- [x] `ColorProfile` — `SixFretGuitar` sub-section wired into serialization/deserialization/copy
- [x] `EnginePreset` — `SixFretGuitar` field (type `FiveFretGuitarPreset`) wired into serialization/deserialization/copy
- [x] `SixFretGuitarFret` enum — Black1–Black3, White1–White3, Open, Wildcard
- [x] `GuitarAction` aliases — Black1Fret–White3Fret mapped to Fret1–Fret6

### Frontend (Unity)

- [x] `SixFretGuitarPlayer` — inherits `FiveFretGuitarPlayer`, overrides `LaneCount` (6), fret mapping, highway ordering, engine creation (`YargSixFretGuitarEngine`), note/lane initialization with `SixFretGuitarColors`, fret array init with 6 lanes
- [x] `FiveFretGuitarPlayer` refactored — made `sealed` → non-sealed, key members `virtual`/`protected virtual` (`LaneCount`, `GetFretFromAction`, `GetFretIndex`, `GetDefaultHighwayOrdering`, `GetFretActionMax`), `LANE_COUNT` const → `LaneCount` property
- [x] `SixFretGuitarNoteElement` — inherits `NoteElement<GuitarNote, SixFretGuitarPlayer>`, mirrors 5-fret note element with 6-fret fret types and colors
- [x] `SixFretGuitarVisual.prefab` — gameplay visual prefab with `SixFretGuitarPlayer` component
- [x] `GameManager.Loading.cs` — `_sixFretGuitarPrefab` field wired into prefab instantiation switch
- [x] `GameModeExtensions.cs` — `ToResourceName()` returns `"guitar6"` for 6-fret; `PossibleProfileSettings` excludes `RANGE_DISABLE`
- [x] `InstrumentExtensions.cs` — resource name mappings for SixFretGuitar/Bass/Rhythm/CoopGuitar (guitar6, bass6, rhythm6, coop6)
- [x] `ThemeComponent.cs` — `VisualStyle.SixFretGuitar` cases for note and fret model selection
- [x] `ThemeManager.cs` — `VisualStyle.SixFretGuitar` enum value
- [x] `ProfileSidebar.cs` — `GameMode.SixFretGuitar` in game mode dropdown
- [x] `Sidebar.cs` (MusicLibrary) — ring 7 shows 6-fret variants (guitar6, bass6, rhythm6, coop6) with fallback to elite drums
- [x] `GuitarScoreCard.cs` — 6-fret instruments in icon switch
- [x] `PresetSubTab.Generic.cs` — `SixFretGuitar` color profile preview mapping
- [x] `GameManager.Debug.cs` — `SixFretGuitarPlayer` case in player type switch
- [x] `TrackPlayer.cs` — `SixFretBass` in bass instrument check
- [x] `BindingCollection.SixFretGuitar.cs` — default gameplay and menu bindings for 6-fret guitar controller
- [x] `en-US.json` — instrument names with "(6-Fret)" suffix
- [x] `InstrumentIcons.png` / `NoInstrumentIcons.png` — sprite entries for guitar6, bass6, rhythm6, coop6
- [x] `FontSprites.asset` — font sprite updates for 6-fret instruments
- [x] `Gameplay.unity` — `_sixFretGuitarPrefab` reference wired

## What Remains

### Localization

- [x] `en-US.json` — SubSection dropdown entry for `SixFretGuitar` ("Guitar (6-Fret)")
- [x] `en-US.json` — Color field labels: `BlackFret`, `WhiteFret`, `BlackFretInner`, `WhiteFretInner`, `BlackParticles`, `WhiteParticles`

> Track unfinished work here as the project evolves.

## Rules

1. **Always inherit from 5-fret** — 6-fret is a specialization of 5-fret, not a parallel implementation. When in doubt, mirror `FiveFretGuitarPlayer` / `FiveFretGuitarNoteElement` patterns.
2. **No range shift** — never add range shift logic to 6-fret. It's a 5-lane mechanic.
3. **No input viewer** — 6-fret has no input viewer.
4. **2 colors** — black frets and white frets only. No green/red/yellow/blue/orange.
5. **6 lanes** — always use `LaneCount => 6`, never hardcode 5.
6. **Update this doc** — whenever a feature is implemented or a design decision is made, update this document with what was done and why.
