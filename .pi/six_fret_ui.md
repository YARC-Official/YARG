# 6-Fret UI Overhaul — GHL-Style 3-Lane Highway

## Goal

Replace current 6-lane highway with GHL-style 3-lane highway. Each lane represents a black+white fret pair. Notes display as "up" (one row), "down" (other row), or "barre" (both rows). Remove split/combined toggle — 3-lane is the only mode.

## GHL Visual Reference

From research: GHL highway has 3 scrolling lanes. Black notes (top row) and white notes (bottom row) appear in the same lane. Barres (black+white same pair) show as a square half-black/half-white. HOPO notes have a cyan glow outline.

## Architecture Overview

```
Engine (YARG.Core)          Frontend (Unity)
─────────────────           ─────────────────
6 frets internally          3 visual lanes
Black1..Black3              Lane 0, 1, 2
White1..White3              Each lane shows:
                            - "up" note (one row)
                            - "down" note (other row)
                            - "barre" note (both rows)
                            - open / wildcard (unchanged)
```

Engine unchanged — still 6 frets, same `GuitarNote` data, same `SixFretGuitarFret` enum.
Frontend maps 6 frets → 3 lanes at render time.

## "Up" / "Down" Definition

- **Normal mode** (LeftyFlip = false): black = UP, white = DOWN
- **Lefty flip** (LeftyFlip = true): white = UP, black = DOWN

Rationale: in GHL the "up" row is the row physically closer to the player's palm/top of controller. Lefty flip swaps which row is "top".

Helper in `SixFretGuitarPlayer`:
```csharp
// Returns true if fret belongs to the "up" row for current lefty setting
bool IsUpFret(SixFretGuitarFret fret) =>
    Profile.LeftyFlip ? fret is >= White1 and <= White3
                      : fret is >= Black1 and <= Black3;

// Returns lane pair index (0, 1, 2) for a fret
int GetLaneIndex(SixFretGuitarFret fret);
```

## New Theme Note Types

Add to `ThemeNoteType` enum:

```csharp
// 6-fret guitar specific types
SixFretUp       = 15,   // Single note, "up" row (black normal / white flipped)
SixFretDown     = 16,   // Single note, "down" row (white normal / black flipped)
SixFretBarre    = 17,   // Both black+white in same lane pair
SixFretUpHOPO   = 18,
SixFretDownHOPO = 19,
SixFretUpTap    = 20,
SixFretDownTap  = 21,
```

No barre+HOPO or barre+Tap types — GHL doesn't have them.

Open and Wildcard use existing `ThemeNoteType.Open`, `ThemeNoteType.Wildcard` — same full-width bar across 3 lanes, identical to 5-fret guitar behavior.

### Dual-Color Support (Barre Notes)

Barre notes need BOTH BlackNote and WhiteNote colors from the color profile. Approach: existing color methods untouched — add new `SetSecondaryColor` method alongside them.

- `ThemeNote` gets new field: `MeshEmissionMaterialIndex[] ColoredSecondaryMaterials`
- `NoteGroup` gets new method: `SetSecondaryColor(Color secondary, Color secondaryNoSP)` — applies to materials listed in `ColoredSecondaryMaterials` only
- Existing `SetColorWithEmission()` unchanged — still handles primary color for all existing call sites
- For barre notes: call `SetColorWithEmission()` for primary (BlackNote), then `SetSecondaryColor()` for secondary (WhiteNote)
- For non-barre notes: only `SetColorWithEmission()` called, `SetSecondaryColor()` never invoked
- Theme author's `SixFretBarre` model has materials tagged as primary (e.g., top half) and secondary (e.g., bottom half)

### Theme Authoring (ThemeComponent)

`ThemeComponent` already has `_sixFretNotes` and `_sixFretFret` fields. Theme authors will place `ThemeNote` children under `_sixFretNotes` with the new note types.

No changes to `ThemeComponent.cs` needed — it already scans all `ThemeNote` children dynamically.

### Theme Model Selection Logic

In `SixFretGuitarNoteElement.SetThemeModels()`, map internal note types to theme models:

```
Note is Black1 (solo in lane)  → SixFretUp (normal) or SixFretUpHOPO or SixFretUpTap
Note is White1 (solo in lane)  → SixFretDown (normal) or SixFretDownHOPO or SixFretDownTap
Note is Black1+White1 (barre)  → SixFretBarre (normal only, no HOPO/Tap variants)
Note is Open                   → Open (existing, full-width across 3 lanes)
Note is Wildcard               → Wildcard (existing, full-width across 3 lanes)
```

The element determines its visual type at spawn time by checking:
1. Its own fret (black or white → up or down)
2. Whether a sibling note exists in the same lane pair → barre

## Files To Modify

### 1. `Assets/Script/Themes/Authoring/ThemeNote.cs`

**Changes**:

- Add new `ThemeNoteType` enum values: `SixFretUp`(15), `SixFretDown`(16), `SixFretBarre`(17), `SixFretUpHOPO`(18), `SixFretDownHOPO`(19), `SixFretUpTap`(20), `SixFretDownTap`(21)
- Add new field on `ThemeNote` class:
  ```csharp
  [SerializeField]
  private MeshEmissionMaterialIndex[] _coloredSecondaryMaterials;
  public IEnumerable<MeshEmissionMaterialIndex> ColoredSecondaryMaterials => _coloredSecondaryMaterials;
  ```
- This field is parallel to existing `_coloredMaterials` — materials listed here receive the secondary color (WhiteNote for barre notes)

**Risk**: Enum + class change. Existing themes won't have models for new types — `ThemeManager` falls back to default theme. Existing themes won't have `_coloredSecondaryMaterials` set — defaults to null/empty, secondary color = primary (no visual change).

### 2. `Assets/Script/Gameplay/Player/SixFretGuitarPlayer.cs`

**Major refactor**. Changes:

- `LaneCount` → `3` (was 6)
- `DEFAULT_HIGHWAY_ORDERING` → 3 entries mapping lane pairs to positions 0, 1, 2
- Remove `COMBINED_PAIR_INDEX` — no longer needed (pairs ARE lanes now)
- Remove `GetPairIndex()`, `FindPairInChord()` — replaced by `HasSiblingInLane()`
- Add `GetLaneIndex(SixFretGuitarFret)` — maps fret to lane 0/1/2
- Add `IsUpFret(SixFretGuitarFret)` — determines up/down for lefty
- `GetFretIndex()` — unchanged (still maps actions to 6 fret values)
- `GetLanePosition()` — return lane index (0-2) for a fret
- `InitializeFretArray()` — pass `LaneCount = 3`, use `HighwayOrderingInfo` for per-fret color mapping
- `InitializeSpawnedNote()` — set `element.LaneType` (Up/Down/Barre) based on fret + sibling check
- `InitializeSpawnedLane()` — lane appears at lane pair position, color based on note type
- `MakeHighwayOrdering()` — 3-lane ordering, lefty flip reverses 0↔2
- `OnNoteHit()` / `OnNoteMissed()` / `OnSustainStart()` / `OnSustainEnd()` — still iterate all notes in barre, play fret animation for each individual fret
- Remove all `SixFretSplitLanes` references

**`_lanePositions` dict**: Maps fret → lane index (0-2). Both Black1 and White1 map to lane 0, etc.

### 3. `Assets/Script/Gameplay/Visuals/TrackElements/Guitar/SixFretGuitarNoteElement.cs`

**Major refactor**. Changes:

- New internal enum `LaneNoteType`: Up, Down, Barre (determines which theme model to use)
- New field `LaneNoteType LaneType` — set by player during `InitializeSpawnedNote`
- `SetThemeModels()` — map 8 new theme types to note group arrays:
  - Up/Strum → SixFretUp
  - Up/HOPO → SixFretUpHOPO
  - Up/Tap → SixFretUpTap
  - Down/Strum → SixFretDown
  - Down/HOPO → SixFretDownHOPO
  - Down/Tap → SixFretDownTap
  - Barre/Strum → SixFretBarre (only strum, no HOPO/Tap variants)
  - Open → Open (existing)
  - Wildcard → Wildcard (existing)
- `InitializeElement()`:
  - Position note at `GetElementX(laneIndex, 3)` where laneIndex = 0, 1, or 2
  - Select note group based on `LaneType` + `NoteRef.Type` (Strum/HOPO/Tap)
  - No more combined centering or scale multiplier logic
  - Sustain line: same as before, positioned per-lane
- Remove `IsPaired`, `GetCombinedCenterX()`, `GetPairedLane()`, `SINGLE_NOTE_MULTIPLIER`
- `UpdateColor()` — dual-color support:
  - Up notes: call `SetColorWithEmission(BlackNote, ...)` only
  - Down notes: call `SetColorWithEmission(WhiteNote, ...)` only
  - Barre notes: call `SetColorWithEmission(BlackNote, ...)` then `SetSecondaryColor(WhiteNote, ...)`
  - `SetSecondaryColor()` only invoked for barre notes; existing callers of `SetColorWithEmission()` unaffected
- `HideElement()` — simplify, no scale reset needed

### 3b. `Assets/Script/Gameplay/Visuals/TrackElements/NoteGroup.cs`

**Changes**: Add `SetSecondaryColor` — new method, existing methods untouched.

- New field on `ThemeNote`: `MeshEmissionMaterialIndex[] ColoredSecondaryMaterials`
- New method on `NoteGroup`: `SetSecondaryColor(Color secondary, Color secondaryNoSP)` — iterates `ColoredSecondaryMaterials` and applies secondary color to those material indices
- Existing `SetColorWithEmission()` completely unchanged — zero impact on existing callers (5-fret, drums, vocals, etc.)
- Call pattern: `SetColorWithEmission(primary, primaryNoSP)` then `SetSecondaryColor(secondary, secondaryNoSP)` — order matters, secondary layers on top
- If `ColoredSecondaryMaterials` is null/empty, `SetSecondaryColor()` is no-op
- For non-barre notes `SetSecondaryColor()` is never called — behavior identical to current

### 4. `Assets/Script/Gameplay/Visuals/TrackElements/LaneElement.cs`

**Changes**:

- Remove `SetCombinedSpan(bool)` and `_isCombinedSpan` field
- `RenderScale()` — remove combined span logic, always use `_scale`
- Lane scale now based on 3 subdivisions (was calculated per-instrument)

### 5. `Assets/Script/Themes/Authoring/ThemeFret.cs`

**Changes**: Add new optional fields for secondary (white) half. Existing fields untouched.

```csharp
// Secondary half materials (for 6-fret dual-half frets)
[SerializeField]
private MeshMaterialIndex[] _secondaryColoredMaterials;
[SerializeField]
private MeshMaterialIndex[] _secondaryInnerMaterials;

// Secondary half effect (press only — hit/miss/sustain animations out of scope)
[field: Space]
[field: SerializeField]
public EffectGroup SecondaryPressedEffect { get; private set; }
```

- Open hit/miss effects shared (both halves participate in open/wildcard)
- `GetSecondaryColoredMaterials()` / `GetSecondaryInnerColoredMaterials()` — parallel to existing getters
- **Risk**: New serialized fields only. Old themes → null/empty → `Fret` skips secondary half. Zero breakage.

### 6. `Assets/Script/Gameplay/Visuals/Fret/Fret.cs`

**Changes**: New secondary methods + new `Initialize` overload. Existing methods untouched. No `FretHalf` enum — separate methods for clarity.

**New fields** (parallel to existing primary):
```csharp
private readonly List<Material> _secondaryTopMaterials   = new();
private readonly List<Material> _secondaryInnerMaterials = new();

// Secondary half original colors (for SP transitions, dim/reset)
private UnityEngine.Color _secondaryOriginalUnityTopColor;
private UnityEngine.Color _secondaryOriginalUnityInnerColor;
private UnityEngine.Color _secondaryOriginalEmissionColor;

// Secondary half animator param existence
private bool _hasSecondaryPressedParam;
```

**Animator hashes** (add to static readonly block):
```csharp
private static readonly int _secondaryPressed = Animator.StringToHash("SecondaryPressed");
```

**New `Initialize` overload** (existing 4-param unchanged):
```csharp
public void Initialize(Color top, Color inner, Color particles, Color openParticles,
    Color secondaryTop, Color secondaryInner, Color secondaryParticles)
```
- Runs existing logic for primary half first (calls `Initialize(top, inner, particles, openParticles)`)
- Then sets secondary: `_secondaryOriginalUnityTopColor`, `_secondaryOriginalUnityInnerColor`, `_secondaryOriginalEmissionColor = secondaryTop * 11.5f`
- Sets secondary material colors on `ThemeBind.GetSecondaryColoredMaterials()` / `GetSecondaryInnerColoredMaterials()`
- Sets secondary particle colors on `SecondaryPressedEffect`
- Checks animator for "SecondaryPressed" param → sets `_hasSecondaryPressedParam`
- If secondary materials null/empty → secondary half inert (single-color fret fallback for 5-fret)

**`SetPressedSecondary(bool pressed, float value)`** — parallel to `SetPressed(bool, float)`:
```csharp
public void SetPressedSecondary(bool pressed, float value)
{
    foreach (var material in _secondaryInnerMaterials)
        material.SetFloat(_fade, value);

    if (_hasSecondaryPressedParam)
        ThemeBind.Animator.SetBool(_secondaryPressed, pressed);

    if (pressed)
        ThemeBind.SecondaryPressedEffect.Play();
    else
        ThemeBind.SecondaryPressedEffect.Stop();
}
```

**`SetSecondaryColor(Color top, Color inner)`** — for Star Power color transitions on secondary half:
```csharp
public void SetSecondaryColor(Color top, Color inner)
{
    foreach (var mat in _secondaryTopMaterials)
    {
        mat.color = top.ToUnityColor();
        mat.SetColor(_emissionColor, top.ToUnityColor() * 11.5f);
    }
    foreach (var mat in _secondaryInnerMaterials)
        mat.color = inner.ToUnityColor();
}
```

**Open hit/miss**: Primary half only. `PlayOpenHitAnimation()` / `PlayOpenMissAnimation()` unchanged — iterate ALL frets, call existing primary methods. (Secondary hit/miss animations out of scope.)

**`WhitenFretColor()` / `RestoreFretColor()`**: Operate on ALL materials (both primary + secondary). No half-aware versions needed — drum-only accent feature, completeness only.

**`DimColor()` / `ResetColor()`**: Shared `_inactiveColor` between halves. No half-aware overloads — BRE dims entire fret object.

### 7. `Assets/Script/Gameplay/Visuals/Fret/FretArray.cs`

**Changes**: New `Initialize` overload + new half-routing methods. Existing methods untouched.

**New `Initialize` overload** for dual-half frets:
```csharp
public void Initialize(Dictionary<int, HighwayOrderingInfo> highwayOrdering, int laneCount,
    GameObject kickFretPrefab, IFretColorProvider fretColorProvider,
    ThemePreset themePreset, VisualStyle style, bool dualHalfFrets)
```

When `dualHalfFrets=true`:
- Creates 1 Fret GameObject per unique position (same as current)
- `_frets` dict: both frets at same position map to SAME Fret object (Black1+White1 → lane 0 Fret)
- Calls `Fret.Initialize(primaryTop, primaryInner, primaryParticles, openParticles, secondaryTop, secondaryInner, secondaryParticles)`
- Primary colors = first fret at position (Black1 → BlackFret), Secondary = second fret (White1 → WhiteFret)
- Relies on enum ordering: Black frets come before White frets, so Black = Primary, White = Secondary

When `dualHalfFrets=false`: delegates to existing `Initialize()` (unchanged behavior).

**New secondary-routing method** (existing methods untouched):
```csharp
public void SetPressedSecondary(int fretIndex, bool pressed)
{
    _frets[fretIndex].SetPressedSecondary(pressed, pressed ? 1f : 0f);
}

public void SetPressedSecondary(int fretIndex, bool pressed, float value)
{
    _frets[fretIndex].SetPressedSecondary(pressed, value);
}
```

### 8. `Assets/Script/Gameplay/Player/FiveFretGuitarPlayer.cs`

**Minor change**: Make `UpdateFretArray()` `protected virtual` (was `private`). Body unchanged.

### 9. `Assets/Script/Gameplay/Player/SixFretGuitarPlayer.cs`

**Changes**: Override methods to use half-aware routing.

`InitializeFretArray()`:
```csharp
_fretArray.Initialize(_lanePositions, 3, null,
    Player.ColorProfile.SixFretGuitar, Player.ThemePreset,
    VisualStyle.SixFretGuitar, dualHalfFrets: true);
```

**Override `UpdateFretArray()`** — iterate lane pairs, check black + white independently, call primary/secondary/both:
```csharp
protected override void UpdateFretArray()
{
    // Iterate lane pairs (0, 1, 2)
    for (int pair = 0; pair < 3; pair++)
    {
        var blackFret  = (SixFretGuitarFret)(pair + 1);              // Black1=1, Black2=2, Black3=3
        var whiteFret  = (SixFretGuitarFret)(pair + 4);              // White1=4, White2=5, White3=6
        var blackHeld  = Engine.IsFretHeld((GuitarAction)(int)blackFret);
        var whiteHeld  = Engine.IsFretHeld((GuitarAction)(int)whiteFret);
        var fretIndex  = (int)blackFret;  // _frets dict: black and white map to same Fret object

        if (blackHeld && whiteHeld)
        {
            // Both pressed — light both halves
            _fretArray.SetPressed(fretIndex, true);
            _fretArray.SetPressedSecondary(fretIndex, true);
        }
        else if (blackHeld)
        {
            // Black only — primary half
            _fretArray.SetPressed(fretIndex, true);
            _fretArray.SetPressedSecondary(fretIndex, false);
        }
        else if (whiteHeld)
        {
            // White only — secondary half
            _fretArray.SetPressed(fretIndex, false);
            _fretArray.SetPressedSecondary(fretIndex, true);
        }
        else
        {
            // Neither — both off
            _fretArray.SetPressed(fretIndex, false);
            _fretArray.SetPressedSecondary(fretIndex, false);
        }
    }
}
```

**Hit/sustain/miss**: No override needed — existing `OnNoteHit`, `OnSustainStart`, `OnSustainEnd` call primary methods only. Secondary hit/miss/sustain animations out of scope for now.

### 10. Theme prefab (`sixFretFret`)

Single GameObject per lane (3 total). Mesh has two visual halves (top=black, bottom=white).
- `_coloredMaterials` → top half mesh materials (black fret)
- `_innerMaterials` → top half inner materials
- `_secondaryColoredMaterials` → bottom half mesh materials (white fret)
- `_secondaryInnerMaterials` → bottom half inner materials
- `HitEffect`, `SustainEffect`, `PressedEffect` → top half particles/lights
- `SecondaryPressedEffect` → bottom half particles/lights (hit/sustain effects deferred)
- `Animator` → parameters: "Pressed", "Sustain", "Hit", "Miss" (primary) AND "SecondaryPressed" (secondary — sustain/hit/miss deferred)

### 11. Lefty flip and fret halves

Fret halves are **physical** (top/bottom), not logical. Lefty flip swaps lane positions (0↔2) but NOT which half is black vs white. Black = Primary (top), White = Secondary (bottom) always. Lane ordering handles visual swap. Matches GHL behavior.

### 17. `Assets/Script/Gameplay/Visuals/Fret/Fret.cs`

**See section 6 above** — dual-half support via new methods. Existing methods untouched.

### 6. `Assets/Script/Gameplay/Player/FiveFretGuitarPlayer.cs`

**Minor change**: Make `UpdateFretArray()` `protected virtual` (was `private`). Body unchanged. Enables `SixFretGuitarPlayer` override.

### 7. `Assets/Script/Themes/ThemeManager.cs`

**No changes**. `VisualStyle.SixFretGuitar` already exists. Theme manager already handles 6-fret note/fret prefabs.

### 8. `Assets/Script/Menu/ProfileList/ProfileSidebar.cs`

**Changes**:

- Remove `_sixFretSplitLanes` Toggle field
- Remove `ChangeSixFretSplitLanes()` method
- Remove `SixFretSplitLanes` from `UpdateSidebar()` binding

### 9. `Assets/Script/Helpers/Extensions/GameModeExtensions.cs`

**Changes**:

- Remove `SIX_FRET_SPLIT_LANES` from `SixFretGuitar` case in `PossibleProfileSettings()`

### 10. `Assets/Script/Helpers/ProfileSettingStrings.cs`

**Changes**:

- Remove `SIX_FRET_SPLIT_LANES` constant (or leave for backward compat — recommend removing)

### 11. `YARG.Core/YARG.Core/Game/YargProfile.cs`

**Changes**:

- Remove `SixFretSplitLanes` field
- Remove from `Serialize()` / `Deserialize()`
- Bump `PROFILE_VERSION`

### 12. `Assets/Script/Settings/Preview/FakeTrackPlayer.cs`

**Changes**:

- `SixFretGuitar` entry: `LaneCount = 3`
- `HighwayOrdering` → 3 lanes
- `CreateFakeNote` → generate notes with new `ThemeNoteType` values (SixFretUp, SixFretDown, SixFretBarre, etc.)
- `NoteColorProvider` → map fret to correct color based on up/down

### 13. `Assets/Script/Settings/Preview/FakeNote.cs`

**Changes**:

- Position calculation: use 3 lanes instead of 6 for 6-fret game mode
- Note type mapping: handle new theme note types

### 14. `Assets/Script/Settings/Metadata/Tabs/PresetSubTab.Generic.cs`

**No changes**. Already maps `ColorProfile.SixFretGuitar` → `GameMode.SixFretGuitar`.

### 15. `Assets/Script/Gameplay/Visuals/TrackElements/SustainLine.cs`

**Changes**:

- Barre sustain line: single line, blended color of BlackNote + WhiteNote
- Add `SetSecondaryColor(Color secondary)` — blends with existing primary color for sustain line
- Existing `SetColor()` unchanged — still sets primary. `SetSecondaryColor()` called after for barre notes only
- For non-barre notes `SetSecondaryColor()` never called — behavior identical to current
- Width: standard 3-lane width (wider than old 6-lane by default, no multiplier needed)

### 16. `Assets/Script/Gameplay/GameManager.Loading.cs`

**No changes**. `_sixFretGuitarPrefab` reference unchanged.

### 17b. `Assets/Script/Gameplay/Visuals/Fret/Fret.cs`

**No changes** (duplicate entry removed — see section 17 above).

### 18. Scene files

- `Gameplay.unity` — update if `_sixFretGuitarPrefab` has lane count changes
- Profile sidebar UI — remove "Split Lanes" toggle GameObject

### 19. `YARG.Core/YARG.Core/Game/Presets/ColorProfile.SixFretGuitar.cs`

**No changes**. Color profile still has BlackFret/WhiteFret/BlackNote/WhiteNote colors. These map correctly to the new system.

## Lane Mapping Details

```
Fret        → Lane → Position (normal) → Position (lefty)
─────────────────────────────────────────────────────────
Black1      → 0    → 0 (left)          → 2 (right)
White1      → 0    → 0 (left)          → 2 (right)
Black2      → 1    → 1 (center)        → 1 (center)
White2      → 1    → 1 (center)        → 1 (center)
Black3      → 2    → 2 (right)         → 0 (left)
White3      → 2    → 2 (right)         → 0 (left)
```

`_lanePositions` dict: maps fret int → lane index (0-2).

```csharp
_lanePositions = new()
{
    { (int)SixFretGuitarFret.Black1, 0 },
    { (int)SixFretGuitarFret.White1, 0 },
    { (int)SixFretGuitarFret.Black2, 1 },
    { (int)SixFretGuitarFret.White2, 1 },
    { (int)SixFretGuitarFret.Black3, 2 },
    { (int)SixFretGuitarFret.White3, 2 },
};
```

Lefty flip swaps lane positions 0↔2:

```csharp
protected override void MakeHighwayOrdering()
{
    if (Player.Profile.LeftyFlip)
    {
        _lanePositions = new()
        {
            { (int)SixFretGuitarFret.Black1, 2 },
            { (int)SixFretGuitarFret.White1, 2 },
            { (int)SixFretGuitarFret.Black2, 1 },
            { (int)SixFretGuitarFret.White2, 1 },
            { (int)SixFretGuitarFret.Black3, 0 },
            { (int)SixFretGuitarFret.White3, 0 },
        };
    }
    else
    {
        _lanePositions = DEFAULT_LANE_POSITIONS;
    }
}
```

## Note Element LaneType Determination

In `SixFretGuitarPlayer.InitializeSpawnedNote()`:

```csharp
protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
{
    var element = (SixFretGuitarNoteElement)poolable;
    element.NoteRef = note;

    if (note.Fret == (int)SixFretGuitarFret.Open ||
        note.Fret == (int)SixFretGuitarFret.Wildcard)
    {
        element.LaneType = SixFretGuitarNoteElement.LaneNoteType.None; // open/wildcard
        return;
    }

    // Check if sibling in same lane pair exists
    bool hasSibling = note.ParentOrSelf.AllNotes.Any(other =>
        other != note &&
        other.Fret != (int)SixFretGuitarFret.Open &&
        other.Fret != (int)SixFretGuitarFret.Wildcard &&
        GetLaneIndex((SixFretGuitarFret)other.Fret) == GetLaneIndex((SixFretGuitarFret)note.Fret) &&
        other.Fret != note.Fret // different fret in same lane
    );

    if (hasSibling)
    {
        element.LaneType = SixFretGuitarNoteElement.LaneNoteType.Barre;
    }
    else if (IsUpFret((SixFretGuitarFret)note.Fret))
    {
        element.LaneType = SixFretGuitarNoteElement.LaneNoteType.Up;
    }
    else
    {
        element.LaneType = SixFretGuitarNoteElement.LaneNoteType.Down;
    }
}
```

## Fret Array (Hit Target) Setup

3 Fret objects, 3 visual positions. Each Fret has two independent halves (Primary=black/top, Secondary=white/bottom).

`FretArray.Initialize()` for 6-fret uses `dualHalfFrets: true`:

```csharp
// Highway ordering: each lane pair maps to a single position
// Black1+White1 → position 0, Black2+White2 → position 1, Black3+White3 → position 2
var ordering = new Dictionary<int, HighwayOrderingInfo>
{
    { (int)SixFretGuitarFret.Black1, new(0, (int)SixFretGuitarFret.Black1) },
    { (int)SixFretGuitarFret.White1, new(0, (int)SixFretGuitarFret.White1) },
    { (int)SixFretGuitarFret.Black2, new(1, (int)SixFretGuitarFret.Black2) },
    { (int)SixFretGuitarFret.White2, new(1, (int)SixFretGuitarFret.White2) },
    { (int)SixFretGuitarFret.Black3, new(2, (int)SixFretGuitarFret.Black3) },
    { (int)SixFretGuitarFret.White3, new(2, (int)SixFretGuitarFret.White3) },
};

_fretArray.Initialize(ordering, 3, null,
    Player.ColorProfile.SixFretGuitar, Player.ThemePreset,
    VisualStyle.SixFretGuitar, dualHalfFrets: true);
```

`FretArray.Initialize(dualHalfFrets: true)`: when 2 frets share a position, creates ONE Fret object initialized with BOTH primary (Black) and secondary (White) colors. `_frets` dict maps both fret ints to the same Fret object. `SetPressedSecondary(int, bool)` delegates to `Fret.SetPressedSecondary()` on the shared Fret object.

## BRE (Beginner / Big Rock Ending)

BRE reduces active lanes. With 3-lane highway:
- BRE still works on individual frets (engine-level)
- Visually, inactive frets dim their half of the lane
- `RescaleLanesForBRE()` → `LaneElement.DefineLaneScale(instrument, 3, true)`

## Replay

Replay data stores fret presses (6-fret actions). No changes needed — replay still records which fret buttons were pressed. Visual replay just maps to 3 lanes.

## Color Profile

`SixFretGuitarColors` unchanged. Still has:
- `BlackFret` / `WhiteFret` — fret button colors
- `BlackFretInner` / `WhiteFretInner` — fret inner colors
- `BlackParticles` / `WhiteParticles` — particle colors
- `BlackNote` / `WhiteNote` — note colors
- `BlackNoteStarPower` / `WhiteNoteStarPower` — SP note colors
- `Metal` / `MetalStarPower` / `Miss` — shared colors

Color mapping:
- **Up notes**: primary = BlackNote (or BlackNoteStarPower during SP)
- **Down notes**: primary = WhiteNote (or WhiteNoteStarPower during SP)
- **Barre notes**: primary = BlackNote, secondary = WhiteNote (dual-color via `ColoredSecondaryMaterials`)
- **Sustain line for barres**: blend of BlackNote + WhiteNote
- **Open/Wildcard**: WhiteNote (same as 5-fret)

## Implementation Order

### Phase 1: Foundation (blocking) ✅ [Completed 2026-05-02]

1. **ThemeNoteType enum** — add new values (15-21) ✅
2. **ThemeNote** — add `ColoredSecondaryMaterials` field ✅
3. **NoteGroup** — add `SetSecondaryColor` method (existing methods untouched) ✅
4. **YargProfile** — remove `SixFretSplitLanes`, bump `PROFILE_VERSION` to 9 ✅

### Phase 2: Core (3-lane highway) ✅ [Completed 2026-05-02]

5. **SixFretGuitarPlayer** — refactor to 3 lanes, lane type determination ✅
6. **SixFretGuitarNoteElement** — refactor note rendering, dual-color support ✅
7. **LaneElement** — remove combined span ✅
8. **SustainLine** — add `SetSecondaryColor` for blend support ✅

### Phase 3: Fret array (hit targets) ✅ [Completed 2026-05-02]

9. **Fret dual-half (press only)** — `ThemeFret` + `Fret` + `FretArray` get new optional secondary-half fields + `SetPressedSecondary`. Scope: press fade + animator bool + pressed effect only. No sustain/hit/miss secondary animations. `FretArray.Initialize(dualHalfFrets: true)` creates 1 Fret per lane with 2 independent halves. `FiveFretGuitarPlayer.UpdateFretArray()` → `protected virtual`. `SixFretGuitarPlayer` override checks black/white independently, calls `SetPressed` / `SetPressedSecondary` / both. Existing methods untouched. ✅

### Phase 4: UI cleanup ✅ [Completed 2026-05-02]

11. **ProfileSidebar** — remove split lanes toggle ✅
12. **GameModeExtensions** — remove `SIX_FRET_SPLIT_LANES` ✅
13. **ProfileSettingStrings** — remove constant ✅

### Phase 5: Preview + polish ✅ [Completed 2026-05-02]

14. **FakeTrackPlayer / FakeNote** — update 6-fret preview to 3 lanes ✅
15. **Theme prefab** — create default 6-fret theme note/fret models (deferred: Unity editor task)
16. **Testing** — verify all note types, lefty flip, BRE, replay, sustain, color preview (deferred: manual testing)

## Resolved Decisions

1. **Barre note color**: BOTH — primary via existing `SetColorWithEmission()` + secondary via new `SetSecondaryColor()`. Existing color methods untouched. `ColoredSecondaryMaterials` on `ThemeNote` indexes which materials get the secondary color.
2. **Barre + HOPO/Tap**: Skipped — GHL doesn't have them
3. **Fret dual-half (press only)**: 1 Fret per lane with 2 independent halves (Primary=black/top, Secondary=white/bottom). `SetPressedSecondary(bool, float)` — separate method, no enum. Handles secondary inner material fade, "SecondaryPressed" animator bool, `SecondaryPressedEffect`. Sustain/hit/miss secondary animations deferred. `SixFretGuitarPlayer.UpdateFretArray()` checks black/white independently, calls `SetPressed`, `SetPressedSecondary`, or both. New optional fields on `ThemeFret`, `Fret`, `FretArray`. Existing methods untouched.
4. **Sustain line for barres**: Single line, blend of both colors
5. **Open/Wildcard**: Same full-width bar across 3 lanes, identical to 5-fret guitar
6. **HOPO across lanes**: Both notes show as HOPO type. Direction implicit from note order. Same as current.

## Remaining Questions

(None — proceed with implementation.)

## Backward Compatibility

- Existing 6-fret charts: work unchanged (engine still 6 frets)
- Existing themes: won't have new `SixFret*` note models. `ThemeManager` falls back to default theme models. Default theme MUST include all new note types.
- Existing profiles: `SixFretSplitLanes` field removed. On deserialize with old profile version, field is simply ignored. `PROFILE_VERSION` bump ensures new profiles don't try to read it.

## What Does NOT Change

- YARG.Core engine (`YargSixFretGuitarEngine`) — unchanged
- `SixFretGuitarFret` enum — unchanged
- `GuitarAction` aliases — unchanged
- Chart format / parsing — unchanged
- Input bindings — unchanged (still 6 fret buttons)
- Scoring — unchanged
- `SixFretGuitarColors` color profile — unchanged
- `VisualStyle.SixFretGuitar` enum — unchanged
- 5-fret guitar — completely unaffected
