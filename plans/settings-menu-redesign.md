# Settings Menu Redesign Plan

## 1. Objectives & Scope
- **Ergonomic Instrument Navigation**: Enable full navigation using rhythm game controllers (Guitar Strum Up/Down and Green/Red frets; Drums navigation) without requiring D-pad or analog stick inputs.
- **Screen Real Estate Optimization**: Eliminate top tabs and move major categories into a collapsible left rail to maximize horizontal space for sections, settings, and the right preview pane.
- **Settings-Only Scope**: Restrict this architecture to `SettingsMenu`. Retain simple top tabs (`HeaderTabs`) for binary two-tab views like `HistoryMenu` and `ProfileInfoMenu`.

---

## 2. Layout & Visual Architecture

### Viewport Budget (1920x1080 Reference Resolution)
- **Category Rail (Far Left)**:
  - **Expanded**: 240px width displaying `[Icon + Category Name]`.
  - **Collapsed**: 64px width displaying `[Icon Only]`, serving as an active category indicator.
  - **Sub-Canvas Isolation**: Housed on a child `Canvas` component so width tweens dirty only local canvas batches, causing 0% rebuild overhead on the settings list or 3D preview.
- **Sections Panel (Mid Left)**:
  - 280px–300px width.
  - Inset floating pill pattern with subtle background surface contrast and cyan active indicator bar.
- **Settings List (Center)**:
  - ~680px flexible width.
  - Vertically scrolling list of setting controls with uniform row heights.
  - Redundant section headers suppressed in single-section views.
- **Detail & Preview Pane (Far Right)**:
  - ~760px–800px width.
  - Displays setting title, detailed multi-line description, and live track previews (`Graphics`, `Presets`).
  - Eliminates the legacy fixed bottom description footer.
- **Top Header Strip**:
  - Reclaimed space hosts `[< Back]`, `SETTINGS` title, breadcrumb trail (`Settings > Graphics`), and an expanded search bar.

---

## 3. Interaction & Controller Navigation Model

### Three-Tier Drill-Down State Machine
```
[Level 1: Categories (Expanded 240px)]
   │   ▲
 Green │   │ Red
   ▼   │
[Level 2: Sections (Category Rail Collapsed 64px)]
   │   ▲
 Green │   │ Red
   ▼   │
[Level 3: Settings Options]
   │   ▲
 Green │   │ Red (Cancels edit or exits to Level 2)
   ▼   │
[Active Control Edit Mode (Sliders / Dropdowns)]
```

### Navigation Rules
1. **Default Entry State**:
   - Opens with `General` pre-selected and the category rail expanded (240px).
   - Focus initializes on Level 1 (Categories) with `General` highlighted and its options visible in the background.
2. **Level 1 (Categories)**:
   - Rail is expanded (240px). Strum Up/Down navigates categories with live preview updating.
   - `Green` (`Confirm`): Collapses rail to 64px and transfers focus to Level 2 (Sections).
   - `Red` (`Back`): Exits Settings to previous screen.
3. **Level 2 (Sections)**:
   - Rail is collapsed (64px). Strum Up/Down navigates section pills.
   - `Green` (`Confirm`): Transfers focus to Level 3 (first setting of active section).
   - `Red` (`Back`): Expands category rail to 240px and transfers focus to Level 1 (Categories).
4. **Level 3 (Settings)**:
   - Strum Up/Down scrolls and navigates setting controls.
   - `Green` (`Confirm`): Toggles checkbox/switch, opens dropdown list, or enters slider adjustment mode.
   - `Red` (`Back`): Transfers focus back to Level 2 (active section pill).
5. **Control Capture Stack**:
   - Sliders and dropdowns intercept `Green` and `Red` during active value manipulation.
   - Pressing `Red` during slider adjustment or open dropdown cancels/finishes the edit and restores Level 3 row focus without jumping back to sections.
6. **Zero-Latency Logical Transition**:
   - Navigation groups transfer focus on frame 0 upon input.
   - Never gate strum or button inputs behind tween completion callbacks.
   - Reverse tweens smoothly if direction flips mid-animation.
7. **Pointer Bypass**:
   - Mouse and touch input bypass drill-down layers; clicking any category, section, or setting focuses and activates it directly.

---

### DOTween Collapse Animation Specification
- **Width Tween**: Animate `LayoutElement.preferredWidth` between 240px (expanded) and 64px (collapsed) over 150ms using `Ease.OutCubic`.
- **Label Crossfade**: Simultaneously tween a child `CanvasGroup.DOFade(collapsed ? 0f : 1f, 0.12f)` on category text labels to eliminate label clipping and text wrapping during collapse.
- **Pause Safety**: Apply `.SetUpdate(true)` so animations run smoothly even when opened from paused gameplay (`Time.timeScale == 0`).
- **Interruption Retargeting**: Retain the active `Tween` handle and dynamically kill/retarget if the player toggles collapse mid-animation.
- **Lifecycle Linking**: Chain `.SetLink(gameObject)` to guarantee automatic garbage collection and tween disposal when the menu is disabled.

### Control Navigation Verification
- **Sliders**: `SliderSettingVisual` natively binds `MenuAction.Up` and `MenuAction.Down` to step values in 5% increments, and binds `MenuAction.Red` to `BaseSettingVisual.NavigateFinish` (`Navigator.Instance.PopScheme()`). Verified fully functional with no refactoring required.
- **Dropdowns & Pickers**: Intercept `Green` to open, Up/Down to cycle items, and `Red` to cancel/close the picker before restoring row focus.

---

## 4. Implementation Plan (Commit After Each Phase)

### Phase 1: CategorySidebar Component & Prefab Structure
- Create `CategorySidebar` and `CategoryItemView` scripts in `Assets/Script/Menu/Settings/Category/`.
- Create the sidebar UI hierarchy with child `Canvas` and `LayoutElement`.
- Connect categories (`General`, `Songs`, `Sound`, `Graphics`, `Presets`, `All Settings`) to `SettingsMenu`.
- *Commit*: Add category sidebar UI structure and component

### Phase 2: DOTween Collapse Animation & Layout Integration
- Implement DOTween width tween (240px ↔ 64px) and label `CanvasGroup` crossfade in `CategorySidebar`.
- Position category sidebar on the far left of `SettingsMenu.prefab`.
- Adjust header area: center search bar and breadcrumb path in reclaimed top space.
- *Commit*: Add animated collapsible category sidebar and header layout refactor

### Phase 3: Three-Tier Controller Navigation State Machine
- Implement `_categoryNavGroup` in `SettingsMenu.cs` alongside existing `_sectionsNavGroup` and `_settingsNavGroup`.
- Wire `Green` (`Confirm`) and `Red` (`Back`) transitions between Categories, Sections, and Settings.
- Implement distinct "active but unfocused" visual pill state for sections when navigating settings.
- *Commit*: Implement three-tier hierarchical controller navigation for settings

### Phase 4: Verification, Polish, and Platform Testing
- Test keyboard, gamepad, guitar, and drum inputs.
- Verify 16:9 (1920x1080) and 16:10 (Steam Deck 1280x800) layout responsiveness.
- Inspect and finalize prefab changes via `unity cmd eval` on Editor port 7800.
- *Commit*: Polish settings menu layout transitions and controller navigation responsiveness
