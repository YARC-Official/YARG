# 6-Fret Guitar — Agent Prompt

> **UI overhaul in progress**: See [`.pi/six_fret_ui.md`](./six_fret_ui.md) for the GHL-style 3-lane highway plan. The combined/split lane mode below is **superseded** — the target is 3 lanes (one per black+white pair), not 6.

## Core Philosophy

6-fret guitar support is **fully based on 5-fret guitar** in both engine and visuals. Engine remains 6 frets internally. Frontend maps to **3 visual lanes** (GHL-style), one per black+white fret pair.

## 6-Fret Guitar Layout (Target — GHL-Style)

- **3 visual lanes**: each lane = one black+white fret pair
  - Lane 0: Black1 + White1
  - Lane 1: Black2 + White2
  - Lane 2: Black3 + White3
- **2 colors only**: black fret and white fret (no 5-color scheme)
- **Note types**: "up" (one row), "down" (other row), "barre" (both rows)
- **No range shift indicators** — range shift is a 5-lane-only mechanic
- **No input viewer** — 6-fret guitar has no input viewer HUD element
- **Engine unchanged**: still 6 frets (`SixFretGuitarFret` enum), same `GuitarNote` data

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
- **Lane mapping** (target): Black1+White1→lane 0, Black2+White2→lane 1, Black3+White3→lane 2
- **Up/Down**: normal mode black=UP/white=DOWN; lefty flip swaps (white=UP/black=DOWN)

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
- Fake track player (color preset live preview)

### GHL-Style 3-Lane Highway (Target)

> Supersedes the old combined/split lane mode. See `.pi/six_fret_ui.md` for full plan.

3 visual lanes. Each lane = one black+white fret pair. Notes in a lane display as:
- **Up**: single note from "up" row (black in normal, white in lefty flip)
- **Down**: single note from "down" row (white in normal, black in lefty flip)
- **Barre**: both black+white in same pair — dual-color note (black + white)
- **Open/Wildcard**: full-width bar across all 3 lanes (same as 5-fret)

Fret array: single `sixFretFret` prefab per lane, dual halves (top=black, bottom=white). Each half animates independently on press/sustain.

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

### Combined/Split Lane Mode

- [x] `YargProfile.SixFretSplitLanes` — profile field (default `false` = combined), serialized, `PROFILE_VERSION` bumped to 8
- [x] `ProfileSettingStrings.SIX_FRET_SPLIT_LANES` — string constant
- [x] `GameModeExtensions.PossibleProfileSettings` — setting wired for `GameMode.SixFretGuitar`
- [x] `ProfileSidebar.cs` — "Split Lanes" toggle wired to `profile.SixFretSplitLanes`, visible only for 6-fret game modes
- [x] `SixFretGuitarPlayer` — `COMBINED_PAIR_INDEX`, `GetPairIndex`, `FindPairInBarre`, `InitializeSpawnedNote` sets `element.IsPaired`, `InitializeSpawnedLane(note)` calls `lane.SetCombinedSpan(!isPaired)`
- [x] `SixFretGuitarNoteElement` — `IsPaired` field, `GetPairedLane`, `GetCombinedCenterX`, combined center position + `SINGLE_NOTE_MULTIPLIER` (1.95x) scale for solo notes, `HideElement` resets X scales
- [x] `LaneElement` — `_isCombinedSpan` flag, `SetCombinedSpan(bool)`, `RenderScale` doubles X scale when combined
- [x] Lefty flip — pairs remain adjacent (even, odd) in reversed ordering, `GetPairedLane` works correctly

### Fake Track Player (Color Preview)

- [x] `FakeTrackPlayer` — `GameMode.SixFretGuitar` entry in `_gameModeInfos`: 6 lanes, `SixFretGuitarPlayer.DEFAULT_HIGHWAY_ORDERING`, `SixFretGuitarColors` fret+note color providers, `enginePreset.SixFretGuitar.HitWindow`, fake note generator (frets 1-6, Normal/HOPO/Tap/Open types)
- [x] `PresetSubTab.Generic.cs` — `ColorProfile.SixFretGuitar` → `GameMode.SixFretGuitar` preview builder mapping (already present from base 6-fret work)

## What Remains (GHL-Style 3-Lane Overhaul)

> See `.pi/six_fret_ui.md` for detailed implementation plan.

### Phase 1: Foundation ✅ [Completed 2026-05-02]

- [x] `ThemeNoteType` enum — add SixFretUp, SixFretDown, SixFretBarre, SixFretUpHOPO, SixFretDownHOPO, SixFretUpTap, SixFretDownTap
- [x] `ThemeNote` — add `ColoredSecondaryMaterials` field for dual-color barre notes
- [x] `NoteGroup` — add `SetSecondaryColor` method (existing methods untouched)
- [x] `YargProfile` — remove `SixFretSplitLanes`, bump `PROFILE_VERSION` to 9

### Phase 2: Core (3-lane highway) ✅ [Completed 2026-05-02]

- [x] `SixFretGuitarPlayer` — `LaneCount` → 3, lane mapping, up/down/barre determination
- [x] `SixFretGuitarNoteElement` — new note type selection, dual-color support, 3-lane positioning
- [x] `LaneElement` — remove combined span logic
- [x] `SustainLine` — dual-color blend for barre sustain

### Phase 3: Fret array ✅ [Completed 2026-05-02]

- [x] `Fret.cs` — add dual-half support (press only), secondary color/animation methods
- [x] `FretArray.cs` — 3-lane init with dual-state frets, `SetPressedSecondary`
- [x] `FiveFretGuitarPlayer.UpdateFretArray()` → `protected virtual`

### Phase 4: UI cleanup ✅ [Completed 2026-05-02]

- [x] `ProfileSidebar` — remove split lanes toggle
- [x] `GameModeExtensions` — remove `SIX_FRET_SPLIT_LANES`
- [x] `ProfileSettingStrings` — remove constant

### Phase 5: Preview + polish ✅ [Completed 2026-05-02]

- [x] `FakeTrackPlayer` / `FakeNote` — 3-lane 6-fret preview
- [ ] Default theme — create 6-fret note/fret models for all new types (deferred: Unity editor task)
- [ ] Testing — all note types, lefty flip, BRE, replay, sustain, color preview (deferred: manual testing)

### Superseded (old combined/split mode — DO NOT implement)

- ~~`SustainLine.SetWidthMultiplier()` for combined mode~~ — not needed, 3 lanes have proper width
- ~~Combined lane testing~~ — replaced by 3-lane testing

> Track unfinished work here as the project evolves.

## Rules

1. **Always inherit from 5-fret** — 6-fret is a specialization of 5-fret, not a parallel implementation. When in doubt, mirror `FiveFretGuitarPlayer` / `FiveFretGuitarNoteElement` patterns.
2. **No range shift** — never add range shift logic to 6-fret. It's a 5-lane mechanic.
3. **No input viewer** — 6-fret has no input viewer.
4. **2 colors** — black frets and white frets only. No green/red/yellow/blue/orange.
5. **3 visual lanes** — highway has 3 lanes (one per black+white pair). Engine still 6 frets internally.
6. **Update this doc** — whenever a feature is implemented or a design decision is made, update this document with what was done and why. Do not add this document to git.
