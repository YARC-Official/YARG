using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.Filters;
using YARG.Menu.MusicLibrary;
using YARG.Player;
using YARG.Song;

namespace YARG.Menu.DifficultySelect
{
    public class DifficultySelectMenu : MonoBehaviour
    {
        /// <summary>
        /// The saved song speed value
        /// </summary>
        private static float _songSpeed = 1f;

        private enum State
        {
            Main,
            Instrument,
            Difficulty,
            Adjustments,
            Modifiers,
            OpenLane,
            Accessibility,
            Harmony
        }

        // Modifiers relocated from the Modifiers menu to the Accessibility menu.
        // RangeCompress is folded into the "No Range Shifts" toggle there.
        private const Modifier ACCESSIBILITY_MODIFIERS =
            Modifier.OpensToGreens | Modifier.NoKicks | Modifier.UnpitchedOnly | Modifier.RangeCompress;

        // Backdrop circle marking the selected instrument's ring — translucent
        // black (a blue tint blended into the row's blue selection highlight).
        private static readonly Color SELECTED_INSTRUMENT_COLOR = new Color(0f, 0f, 0f, 0.5f);

        // Non-selected ring arcs and instrument icons dim while the instrument
        // field is focused (blue row highlight, backdrop visible) and further
        // when another field has focus (black row, where the backdrop is
        // invisible). The intensity number is deliberately left undimmed.
        private const float RING_DIM_FOCUSSED = 0.6f;
        private const float RING_DIM_UNFOCUSSED = 0.2f;
        private const float ICON_DIM_FOCUSSED = 0.45f;
        private const float ICON_DIM_UNFOCUSSED = 0.3f;

        // Done buttons: the Ready-button text treatment in blue (blue text
        // normally, near-black blue on the row's highlight while selected).
        private static Color DONE_TEXT_COLOR => MenuData.Colors.NavigationBlue;
        private static readonly Color DONE_SELECTED_TEXT_COLOR = new Color32(0x01, 0x22, 0x27, 0xFF);

        [SerializeField]
        private TextMeshProUGUI _subHeader;
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private NavigationGroup _navGroup;
        [SerializeField]
        private TextMeshProUGUI _text;
        [SerializeField]
        private DifficultyRing _difficultyRing;
        [SerializeField]
        private TMP_InputField _speedInput;
        [SerializeField]
        private TextMeshProUGUI _loadingPhrase;
        [SerializeField]
        private TextMeshProUGUI _warningMessage;
        [SerializeField]
        private GameObject _warningMessageContainer;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _songTitleText;
        [SerializeField]
        private TextMeshProUGUI _artistText;
        [SerializeField]
        private Image _sourceIcon;

        [Space]
        [SerializeField]
        private DifficultyItem _difficultyItemPrefab;
        [SerializeField]
        private DifficultyItem _difficultyGreenPrefab;
        [SerializeField]
        private DifficultyItem _difficultyRedPrefab;
        [SerializeField]
        private DifficultyItem _difficultyItemSmallRedPrefab;
        [SerializeField]
        private GameObject _coloredItemPrefab;
        [SerializeField]
        private GameObject _ringsItemPrefab;
        [SerializeField]
        private ModifierItem _modifierItemPrefab;

        private int _playerIndex;
        private int _vocalModifierSelectIndex = -1;

        private State _lastMenuState;
        private State _menuState;

        private readonly List<Instrument> _possibleInstruments  = new();
        private readonly List<Difficulty> _possibleDifficulties = new();
        private readonly List<Modifier>   _possibleModifiers    = new();

        [NonSerialized]
        private Modifier _excusableModifiers;

        private int _maxHarmonyIndex = 3;

        private readonly List<ModifierItem> _modifierItems = new();
        private readonly List<Modifier> _itemModifiers = new();

        private List<SongEntry> _songList;

        private YargPlayer CurrentPlayer => PlayerContainer.Players[_playerIndex];

        private ScrollRect _scrollRect;
        private Scrollbar _scrollbar;

        private void OnEnable()
        {
            string subHeaderKey = GlobalVariables.State.IsPractice ? "Practice" : "Quickplay";
            _subHeader.text = Localize.Key("Menu.Main.Options", subHeaderKey);

            // Set navigation scheme
            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () =>
                {
                    if (_menuState == State.Main)
                    {
                        if (_playerIndex == 0)
                        {
                            MenuManager.Instance.PopMenu();
                        }
                        else
                        {
                            ChangePlayer(-1);
                        }
                    }
                    else
                    {
                        // Nested menus back out one level at a time:
                        // OpenLane -> Modifiers -> Adjustments -> Main.
                        _menuState = _menuState switch
                        {
                            State.OpenLane => State.Modifiers,
                            State.Modifiers or State.Accessibility => State.Adjustments,
                            _ => State.Main,
                        };
                        UpdateForPlayer();
                    }
                })
            }, false));

            _speedInput.text = $"{Mathf.RoundToInt(_songSpeed * 100f)}%";
            _songTitleText.text = GlobalVariables.State.CurrentSong.Name;
            _artistText.text = GlobalVariables.State.CurrentSong.Artist;

            if (GlobalVariables.State.PlayingAShow)
            {
                _songList = GlobalVariables.State.ShowSongs;
            }
            else
            {
                _songList = new List<SongEntry> { GlobalVariables.State.CurrentSong };
            }

            // Starting a fresh selection session: discard any session-scoped modifiers
            // imposed by a previous song (see ApplySessionModifiers) so each player's
            // own saved selection is what shows and is edited here.
            foreach (var player in PlayerContainer.Players)
            {
                player.Profile.RestoreSavedModifiers();
            }

            // ChangePlayer(0) will update for the current player
            _playerIndex = 0;
            _vocalModifierSelectIndex = -1;
            ChangePlayer(0);

            _loadingPhrase.text = RichTextUtils.StripRichTextTags(
                GlobalVariables.State.CurrentSong.LoadingPhrase, RichTextTags.BadTags);

            _sourceIcon.sprite = SongSources.SourceToIcon(GlobalVariables.State.CurrentSong.Source);
            _sourceIcon.gameObject.SetActive(_sourceIcon.sprite != null);

            // The header ring is only a template for the per-row rings now (the
            // header shows just the game-mode sprite and name); keep it inactive.
            if (_difficultyRing != null)
            {
                _difficultyRing.gameObject.SetActive(false);
            }

            _scrollRect = GetComponentInChildren<ScrollRect>();
            _scrollbar = GetComponentInChildren<Scrollbar>();
            _navGroup.SelectionChanged += UpdateForSelectionChanged;
        }

        private void UpdateForSelectionChanged(NavigatableBehaviour navigatableBehaviour,
            SelectionOrigin selectionOrigin)
        {
            UpdateScrollbarForSelection();
        }

        private void UpdateScrollbarForSelection()
        {
            if (!_scrollbar)
            {
                return;
            }

            int? index = _navGroup.SelectedIndex;
            if (index is not { } i) return;

            int count = _navGroup.Count;
            if (count <= 0)
            {
                return;
            }

            if (Mathf.Approximately(_scrollbar.size, 1f))
            {
                _scrollbar.value = 1f;
                return;
            }

            float highScrollBound = _scrollbar.size + (1 - _scrollbar.size) * _scrollbar.value;
            float lowScrollBound = (1 - _scrollbar.size) * _scrollbar.value;
            float indexHighBound = 1 - (1 / (float) count) * i;
            float indexLowBound = 1 - (1 / (float) count) * (i + 1);
            if (highScrollBound < indexHighBound)
            {
                _scrollbar.value = (indexHighBound - _scrollbar.size) / (1 - _scrollbar.size);
            }
            else if (lowScrollBound > indexLowBound)
            {
                _scrollbar.value = indexLowBound / (1 - _scrollbar.size);
            }
        }

        private void UpdateForPlayer()
        {
            // Set player text
            var profile = CurrentPlayer.Profile;
            _text.text = $"<sprite name=\"{profile.GameMode.ToResourceName()}\"> {profile.Name}";

            // Reset content
            _navGroup.ClearNavigatables();
            _container.DestroyChildren();
            StatsManager.Instance.UpdateActivePlayers();

            // Create the menu
            switch (_menuState)
            {
                case State.Main:
                    CreateMainMenu();
                    break;
                case State.Instrument:
                    CreateInstrumentMenu();
                    break;
                case State.Difficulty:
                    CreateDifficultyMenu();
                    break;
                case State.Adjustments:
                    CreateAdjustmentsMenu();
                    break;
                case State.Modifiers:
                    CreateModifierMenu();
                    break;
                case State.OpenLane:
                    CreateOpenLaneMenu();
                    break;
                case State.Accessibility:
                    CreateAccessibilityMenu();
                    break;
                case State.Harmony:
                    CreateHarmonyMenu();
                    break;
            }

            _lastMenuState = _menuState;
            RefreshScrollbar();
        }

        // Get the charter-rated tier values for an instrument. Harmony reads from
        // HarmonyVocals, which is empty on solo-only songs (no harmony chart) —
        // fall back to the lead vocals tier so the ring still shows meaningful
        // data instead of the dimmed state.
        private static PartValues GetTierValues(SongEntry song, Instrument instrument)
        {
            var tierValues = song[instrument];

            if (instrument is Instrument.Harmony && !tierValues.IsActive())
            {
                tierValues = song[Instrument.Vocals];
            }

            return tierValues;
        }

        // Resolve the bare Addressable icon name for the ring. Handles the 22-fret
        // pro-instrument gap (ToResourceName returns null for ProGuitar_22Fret /
        // ProBass_22Fret) and selects the part-count mic icon for harmony based on
        // the song's vocal part count.
        private static string GetInstrumentRingAsset(Instrument instrument, int vocalPartCount)
            => instrument switch
        {
            Instrument.ProGuitar_22Fret => "realGuitar",
            Instrument.ProBass_22Fret   => "realBass",
            Instrument.Harmony => vocalPartCount switch
            {
                >= 3 => "harmVocals",
                2    => "twoVocals",
                _    => "vocals",
            },
            _ => instrument.ToResourceName(),
        };

        private void RefreshScrollbar()
        {
            if (_scrollRect == null)
            {
                UpdateScrollbarForSelection();
                return;
            }

            Canvas.ForceUpdateCanvases();
            _scrollRect.Rebuild(CanvasUpdate.PostLayout);

            if (_scrollRect.ScrollableHeight() <= 0f)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            UpdateScrollbarForSelection();
        }

        private void CreateMainMenu()
        {
            var player = CurrentPlayer;

            if (player.IsMissingMicrophone)
            {
                ShowWarning(Localize.Key("Menu.DifficultySelect.WarningVocalistNoMicrophone"));
            }
            else if (player.IsMissingInputDevice)
            {
                ShowWarning(Localize.Key("Menu.DifficultySelect.WarningPlayerNoInputDevice"));
            }
            else
            {
                ShowWarning(null);
            }

            // Only show all these options if there are instruments available
            if (_possibleInstruments.Count > 0)
            {
                // Ready button
                CreateItem(LocalizeHeader("Ready"), _lastMenuState == State.Main, _difficultyGreenPrefab, () =>
                {
                    // If the player just selected vocal modifiers, don't show them again
                    if (player.Profile.GameMode == GameMode.Vocals &&
                        _vocalModifierSelectIndex == -1)
                    {
                        _vocalModifierSelectIndex = _playerIndex;
                    }

                    ChangePlayer(1);
                });

                var instrumentItem = CreateItem(LocalizeHeader("Instrument"),
                    player.Profile.CurrentInstrument.ToLocalizedName(),
                    _lastMenuState == State.Instrument, _ringsItemPrefab, () =>
                {
                    _menuState = State.Instrument;
                    UpdateForPlayer();
                });

                // Show every available instrument's tier wheel on its own row
                // within the item (localized instrument names can be long, so a
                // ring column beside the text doesn't reliably fit). The selected
                // instrument gets a backdrop circle behind its ring; the rest
                // are dimmed, deeper while another field has focus (the black
                // unfocused row hides the circle and shrinks the dim contrast).
                if (_difficultyRing != null && _possibleInstruments.Count > 0)
                {
                    const float ringSize = 40f;

                    var song = GlobalVariables.State.CurrentSong;
                    var rings = instrumentItem.GetComponent<DifficultyItemRings>()
                        .AttachRingRow(_difficultyRing, _possibleInstruments.Count, ringSize);

                    var currentInstrument = player.Profile.CurrentInstrument;
                    DifficultyRing selectedRing = null;

                    for (int i = 0; i < _possibleInstruments.Count; i++)
                    {
                        var instrument = _possibleInstruments[i];
                        rings[i].SetInfo(
                            GetInstrumentRingAsset(instrument, song.VocalsCount),
                            instrument,
                            GetTierValues(song, instrument));

                        if (instrument == currentInstrument)
                        {
                            // Extra size is in the ring's native units; scale it
                            // so the circle extends four *screen* pixels per
                            // side, giving the ring a prominent rim.
                            rings[i].ShowSelectionBackdrop(SELECTED_INSTRUMENT_COLOR,
                                extraSize: 8f * 65f / ringSize);
                            selectedRing = rings[i];
                        }
                    }

                    void ApplyInstrumentFieldFocus(bool focused)
                    {
                        selectedRing?.SetBackdropVisible(focused);
                        for (int i = 0; i < rings.Length; i++)
                        {
                            if (rings[i] != selectedRing)
                            {
                                rings[i].SetRingOpacity(
                                    focused ? RING_DIM_FOCUSSED : RING_DIM_UNFOCUSSED);
                                rings[i].SetIconColor(Color.white.WithAlpha(
                                    focused ? ICON_DIM_FOCUSSED : ICON_DIM_UNFOCUSSED));
                            }
                        }
                    }

                    instrumentItem.Button.SelectionStateChanged += (_, selected, _) =>
                        ApplyInstrumentFieldFocus(selected);
                    ApplyInstrumentFieldFocus(instrumentItem.Button.Selected);
                }

                CreateItem(LocalizeHeader("Difficulty"),
                    player.Profile.CurrentDifficulty.ToLocalizedName(),
                    _lastMenuState == State.Difficulty, () =>
                {
                    _menuState = State.Difficulty;
                    UpdateForPlayer();
                });

                // Harmony players must pick their harmony index
                if (player.Profile.CurrentInstrument == Instrument.Harmony)
                {
                    CreateItem(LocalizeHeader("Harmony"),
                        (player.Profile.HarmonyIndex + 1).ToString(),
                        _lastMenuState == State.Harmony, () =>
                    {
                        _menuState = State.Harmony;
                        UpdateForPlayer();
                    });
                }

                // Only allow vocal modifiers to be selected once (so they don't conflict)
                if (player.Profile.GameMode != GameMode.Vocals ||
                    _vocalModifierSelectIndex == -1 ||
                    _vocalModifierSelectIndex == _playerIndex)
                {
                    var adjustmentsItem = CreateItem(LocalizeHeader("Adjustments"),
                        BuildAdjustmentsSummary(player.Profile, out int optionCount),
                        _lastMenuState is State.Adjustments or State.Modifiers or State.Accessibility, () =>
                    {
                        _menuState = State.Adjustments;
                        UpdateForPlayer();
                    });

                    // With a single active option (or none) the summary fits at
                    // the normal body size; longer lists drop to the header size
                    // to keep the row compact.
                    if (optionCount >= 2)
                    {
                        adjustmentsItem.UseSmallBodyText();
                    }
                }
            }

            // Only show if there is more than one play, only if there is instruments available
            if (_possibleInstruments.Count <= 0 || PlayerContainer.Players.Count != 1)
            {
                // Sit out button
                CreateItem(LocalizeHeader("SitOut"), _possibleInstruments.Count <= 0, _difficultyItemSmallRedPrefab, () =>
                {
                    // If the user went back to sit out, and the vocal modifiers were selected,
                    // deselect them.
                    if (_vocalModifierSelectIndex == _playerIndex)
                    {
                        _vocalModifierSelectIndex = -1;
                    }

                    player.SittingOut = true;
                    ChangePlayer(1);
                });

                // Disconnect button
                CreateItem(LocalizeHeader("Disconnect"), _possibleInstruments.Count <= 0, _difficultyItemSmallRedPrefab, () =>
                {
                    // If the user disconnected, and the vocal modifiers were selected,
                    // deselect them.
                    if (_vocalModifierSelectIndex == _playerIndex)
                    {
                        _vocalModifierSelectIndex = -1;
                    }

                    PlayerContainer.DisposePlayer(player);

                    // Since we're removing one player from the active players list, don't increment the player index.
                    ChangePlayer(0);
                });
            }
        }

        private void ShowWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                _warningMessageContainer.SetActive(false);
                _warningMessage.text = "";
            }
            else
            {
                _warningMessageContainer.SetActive(true);
                _warningMessage.text = message;
            }
        }

        // "5 - Nightmare"-style tier text for the instrument menu, using the
        // filters menu's localized tier names. "No Part" can't come up here —
        // the menu only lists playable instruments.
        private static string GetTierLabel(PartValues values)
        {
            if (!values.IsActive() || values.Intensity < 0)
            {
                return "? - " + Localize.Key("Menu.Filters.Intensities.Unknown");
            }

            // The label clamps at the top tier; the number stays exact.
            string text = $"{values.Intensity} - {IntensityLabels.GetLabelByIndex(values.Intensity)}";

            // Top-tier colors (matching the web export's tier palette): orange
            // at 5, the ring segments' red at 6+.
            return values.Intensity switch
            {
                >= 6 => $"<color=#FB443F>{text}</color>",
                5    => $"<color=#FF8400>{text}</color>",
                _    => text,
            };
        }

        private void CreateInstrumentMenu()
        {
            var song = GlobalVariables.State.CurrentSong;

            foreach (var instrument in _possibleInstruments)
            {
                bool selected = CurrentPlayer.Profile.CurrentInstrument == instrument;

                // Instrument name with its charted tier on a smaller, dimmed
                // second line (mirroring the header text style).
                string label = instrument.ToLocalizedName()
                    + $"\n<size=18><color=#FFFFFF80>{GetTierLabel(GetTierValues(song, instrument))}</color></size>";

                CreateItem(label, selected, () =>
                {
                    var preferredInstrument = CurrentPlayer.Profile.PreferredInstrument;
                    CurrentPlayer.Profile.CurrentInstrument = instrument;

                    // Re-resolve after an instrument switch in case the raw harmony index is out
                    // of range for this song (ChangePlayer's check can be masked by the
                    // HarmonyIndex getter returning 0 when not on Harmony).
                    CurrentPlayer.Profile.ResolveHarmonyIndex(_maxHarmonyIndex);

                    // What we are doing here is resetting preferred instrument only if the current preferred instrument
                    // was an option for this chart. This ensures that preferred instrument does not change when the
                    // player is forced to use a different instrument.
                    if (instrument != preferredInstrument && _possibleInstruments.Contains(preferredInstrument))
                    {
                        CurrentPlayer.Profile.PreferredInstrument = instrument;
                    }

                    FiltersMenu.ResetIntensityFiltersForProfile(CurrentPlayer.Profile);
                    UpdatePossibleDifficulties();
                    UpdatePossibleModifiers();

                    _menuState = State.Main;
                    UpdateForPlayer();
                });
            }
        }

        private void CreateDifficultyMenu()
        {
            foreach (var difficulty in _possibleDifficulties)
            {
                bool selected = CurrentPlayer.Profile.CurrentDifficulty == difficulty;
                CreateItem(difficulty.ToLocalizedName(), selected, () =>
                {
                    CurrentPlayer.Profile.CurrentDifficulty
                        = CurrentPlayer.Profile.DifficultyFallback
                        = difficulty;

                    _menuState = State.Main;
                    UpdateForPlayer();
                });
            }
        }

        // Intermediate menu grouping the Modifiers and Accessibility sub-menus,
        // each previewing its active options in its body text.
        private void CreateAdjustmentsMenu()
        {
            var profile = CurrentPlayer.Profile;

            CreateItem(LocalizeHeader("Modifiers"),
                BuildModifierSummary(profile),
                _lastMenuState != State.Accessibility, () =>
            {
                _menuState = State.Modifiers;
                UpdateForPlayer();
            });

            if (HasAccessibilityOptions(profile))
            {
                CreateItem(LocalizeHeader("Accessibility"),
                    BuildAccessibilitySummary(profile),
                    _lastMenuState == State.Accessibility, () =>
                {
                    _menuState = State.Accessibility;
                    UpdateForPlayer();
                });
            }

            // Create done button
            CreateDoneItem(() =>
            {
                _menuState = State.Main;
                UpdateForPlayer();
            });
        }

        private void CreateDoneItem(UnityAction action)
        {
            var btn = Instantiate(_coloredItemPrefab, _container).GetComponent<DifficultyItem>();
            btn.Initialize(LocalizeHeader("Done"), action);
            btn.SetInteractable(true);
            _navGroup.AddNavigatable(btn.Button);

            btn.GetComponent<DifficultyItemColorizer>()
                .SetButtonColor(DONE_TEXT_COLOR, DONE_SELECTED_TEXT_COLOR);
        }

        private void CreateModifierMenu()
        {
            var profile = CurrentPlayer.Profile;

            _modifierItems.Clear();
            _itemModifiers.Clear();

            foreach (var modifier in _possibleModifiers)
            {
                // Accessibility-relocated modifiers live in the Accessibility menu
                if ((modifier & ACCESSIBILITY_MODIFIERS) != 0) continue;

                AddModifierToggle(profile, modifier);
            }

            // Five-lane keys: the three-state OpenLaneDisplayType gets its own
            // sub-menu with the options laid out explicitly (a pair of dependent
            // toggles read poorly, especially once localized).
            if (profile.GameMode == GameMode.ProKeys)
            {
                CreateItem(LocalizeHeader("DedicatedOpenLane"),
                    profile.OpenLaneDisplayType.ToLocalizedName(),
                    _lastMenuState == State.OpenLane, () =>
                {
                    _menuState = State.OpenLane;
                    UpdateForPlayer();
                });
            }

            // Create done button (back to the Adjustments menu these nest under)
            CreateDoneItem(() =>
            {
                _menuState = State.Adjustments;
                UpdateForPlayer();
            });

            _navGroup.SelectFirst();
        }

        private static readonly OpenLaneDisplayType[] OPEN_LANE_OPTIONS =
        {
            OpenLaneDisplayType.Never,
            OpenLaneDisplayType.Always,
            OpenLaneDisplayType.IfChartContainsOpens,
        };

        private void CreateOpenLaneMenu()
        {
            var profile = CurrentPlayer.Profile;

            // Title row (same string as the Modifiers-menu row that leads here)
            // so the menu identifies itself; not part of the nav group.
            Instantiate(_difficultyItemPrefab, _container)
                .InitializeAsTitle(LocalizeHeader("DedicatedOpenLane"));

            foreach (var displayType in OPEN_LANE_OPTIONS)
            {
                var capture = displayType;
                bool selected = profile.OpenLaneDisplayType == displayType;
                CreateItem(displayType.ToLocalizedName(), selected, () =>
                {
                    profile.OpenLaneDisplayType = capture;

                    _menuState = State.Modifiers;
                    UpdateForPlayer();
                });
            }
        }

        private void CreateAccessibilityMenu()
        {
            var profile = CurrentPlayer.Profile;

            _modifierItems.Clear();
            _itemModifiers.Clear();

            if (SupportsLeftyFlip(profile.GameMode))
            {
                // Takes effect at track build time since this menu precedes gameplay.
                AddProfileToggle(LocalizeHeader("LeftyFlip"), profile.LeftyFlip,
                    on => profile.LeftyFlip = on);
            }

            if (SupportsRangeShifts(profile.GameMode))
            {
                // One positive "No Range Shifts" switch backed by both range
                // mechanisms: the profile's RangeEnabled display setting (the
                // profile editor's Range Disable toggle) and, where the game mode
                // supports it, the RangeCompress chart modifier. Shown active if
                // either says shifts are off, so it tracks the profile editor.
                bool compressPossible = _possibleModifiers.Contains(Modifier.RangeCompress);
                bool noRangeShifts = !profile.RangeEnabled
                    || (compressPossible && profile.IsModifierActive(Modifier.RangeCompress));

                AddProfileToggle(LocalizeHeader("NoRangeShifts"), noRangeShifts, on =>
                {
                    profile.RangeEnabled = !on;

                    if (compressPossible)
                    {
                        if (on)
                        {
                            profile.AddSingleModifier(Modifier.RangeCompress);
                        }
                        else
                        {
                            profile.RemoveModifiers(Modifier.RangeCompress);
                        }
                    }
                });
            }

            foreach (var modifier in _possibleModifiers)
            {
                if ((modifier & ACCESSIBILITY_MODIFIERS) == 0) continue;
                if (modifier == Modifier.RangeCompress) continue; // folded in above

                AddModifierToggle(profile, modifier);
            }

            // Create done button (back to the Adjustments menu these nest under)
            CreateDoneItem(() =>
            {
                _menuState = State.Adjustments;
                UpdateForPlayer();
            });

            _navGroup.SelectFirst();
        }

        // A ModifierItem toggle bound to arbitrary state (profile flags, display
        // types) rather than a Modifier enum value.
        private ModifierItem AddProfileToggle(string label, bool active, Action<bool> onChanged)
        {
            var btn = Instantiate(_modifierItemPrefab, _container);
            btn.Initialize(label, active, onChanged);
            _navGroup.AddNavigatable(btn);
            return btn;
        }

        private void AddModifierToggle(YargProfile profile, Modifier modifier)
        {
            var btn = AddProfileToggle(modifier.ToLocalizedName(),
                profile.IsModifierActive(modifier), active =>
            {
                // Enable/disable the modifier
                if (active)
                {
                    profile.AddSingleModifier(modifier);
                }
                else
                {
                    profile.RemoveModifiers(modifier);
                }

                UpdateModifierMenu();
            });

            _modifierItems.Add(btn);
            _itemModifiers.Add(modifier);
        }

        private static bool SupportsLeftyFlip(GameMode mode)
            => mode is GameMode.FiveFretGuitar or GameMode.SixFretGuitar
                or GameMode.FourLaneDrums or GameMode.FiveLaneDrums or GameMode.EliteDrums;

        private static bool SupportsRangeShifts(GameMode mode)
            => mode is GameMode.FiveFretGuitar or GameMode.ProKeys;

        private bool HasAccessibilityOptions(YargProfile profile)
            => SupportsLeftyFlip(profile.GameMode)
                || SupportsRangeShifts(profile.GameMode)
                || _possibleModifiers.Any(m => (m & ACCESSIBILITY_MODIFIERS) != 0);

        // Summary body for the main menu's Adjustments row: the active options
        // from both nested menus (Modifiers and Accessibility) combined.
        // optionCount lets the caller pick a font size for the list.
        private string BuildAdjustmentsSummary(YargProfile profile, out int optionCount)
        {
            string none = Modifier.None.ToLocalizedName();
            string text = "";

            string modifierSummary = BuildModifierSummary(profile);
            if (modifierSummary != none)
            {
                text += modifierSummary + "\n";
            }

            if (HasAccessibilityOptions(profile))
            {
                string accessibilitySummary = BuildAccessibilitySummary(profile);
                if (accessibilitySummary != none)
                {
                    text += accessibilitySummary + "\n";
                }
            }

            text = text.Trim();

            if (text.Length == 0)
            {
                optionCount = 0;
                return none;
            }

            optionCount = text.Count(c => c == '\n') + 1;
            return text;
        }

        // Summary body for the Adjustments menu's Modifiers row. Accessibility-relocated
        // modifiers are summarized on their own row; the keys open-lane toggles
        // live in the Modifiers menu but are backed by OpenLaneDisplayType rather
        // than a Modifier flag, so they're listed here explicitly.
        private string BuildModifierSummary(YargProfile profile)
        {
            string text = "";

            if ((profile.CurrentModifiers & ~_excusableModifiers & ~ACCESSIBILITY_MODIFIERS) != Modifier.None)
            {
                foreach (var modifier in _possibleModifiers)
                {
                    if ((modifier & ACCESSIBILITY_MODIFIERS) != 0) continue;
                    if (!profile.IsModifierActive(modifier)) continue;

                    text += modifier.ToLocalizedName() + "\n";
                }
            }

            // The bare display-type names wouldn't be meaningful in a summary;
            // Never counts as "off" and isn't listed.
            if (profile.GameMode == GameMode.ProKeys)
            {
                switch (profile.OpenLaneDisplayType)
                {
                    case OpenLaneDisplayType.Always:
                        text += LocalizeHeader("OpenLaneAlways") + "\n";
                        break;
                    case OpenLaneDisplayType.IfChartContainsOpens:
                        text += LocalizeHeader("OpenLaneWhenCharted") + "\n";
                        break;
                }
            }

            text = text.Trim();
            return text.Length == 0 ? Modifier.None.ToLocalizedName() : text;
        }

        private string BuildAccessibilitySummary(YargProfile profile)
        {
            string text = "";

            if (SupportsLeftyFlip(profile.GameMode) && profile.LeftyFlip)
            {
                text += LocalizeHeader("LeftyFlip") + "\n";
            }

            if (SupportsRangeShifts(profile.GameMode)
                && (!profile.RangeEnabled || profile.IsModifierActive(Modifier.RangeCompress)))
            {
                text += LocalizeHeader("NoRangeShifts") + "\n";
            }

            foreach (var modifier in _possibleModifiers)
            {
                if ((modifier & ACCESSIBILITY_MODIFIERS) == 0) continue;
                if (modifier == Modifier.RangeCompress) continue; // covered above
                if (!profile.IsModifierActive(modifier)) continue;

                text += modifier.ToLocalizedName() + "\n";
            }

            text = text.Trim();
            return text.Length == 0 ? Modifier.None.ToLocalizedName() : text;
        }

        private void CreateHarmonyMenu()
        {
            for (int i = 0; i < _maxHarmonyIndex; i++)
            {
                int capture = i;
                bool selected = CurrentPlayer.Profile.HarmonyIndex == i;
                CreateItem((i + 1).ToString(), selected, () =>
                {
                    CurrentPlayer.Profile.HarmonyIndex = (byte) capture;

                    _menuState = State.Main;
                    UpdateForPlayer();
                });
            }
        }

        private void UpdateModifierMenu()
        {
            var profile = CurrentPlayer.Profile;

            for (int i = 0; i < _modifierItems.Count; i++)
            {
                var item = _modifierItems[i];
                var modifier = _itemModifiers[i];

                item.Active = profile.IsModifierActive(modifier);
            }
        }

        private void UpdatePossibleModifiers()
        {
            var profile = CurrentPlayer.Profile;

            // Get the possible modifiers (split the enum into multiple) and
            // make sure current modifiers are valid, and remove the invalid ones
            _possibleModifiers.Clear();
            var (possible, excusable) = profile.GameMode.PossibleModifiers(profile.CurrentInstrument);
            _excusableModifiers = excusable;

            foreach (var modifier in EnumExtensions<Modifier>.Values)
            {
                // Skip if the modifier is not a possible one
                if ((possible & modifier) == 0)
                {
                    // Also try to clear it if it isn't considered excusable yet the player somehow has it
                    if (((excusable & modifier) == 0) && profile.IsModifierActive(modifier))
                    {
                        profile.RemoveModifiers(modifier);
                    }

                    continue;
                }

                _possibleModifiers.Add(modifier);

                if (profile.IsModifierActive(modifier) && !_possibleModifiers.Contains(modifier))
                {
                    profile.RemoveModifiers(modifier);
                }
            }

        }

        private void ChangePlayer(int add)
        {
            _playerIndex += add;
            _menuState = State.Main;

            // When the user(s) have selected all of their difficulties, move on
            if (_playerIndex >= PlayerContainer.Players.Count)
            {
                // If everyone is sitting out, show a warning and boot back to music library
                if (PlayerContainer.Players.All(i => i.SittingOut))
                {
                    MenuManager.Instance.PopMenu();

                    DialogManager.Instance.ShowMessage("Nobody's Playing!",
                        "You tried to play a song with every player sitting out.");

                    return;
                }

                // Ensure all vocal players have the same modifiers active
                if (_vocalModifierSelectIndex != -1)
                {
                    // Call the player with the selected modifiers, the "primary player"
                    var primaryPlayer = PlayerContainer.Players[_vocalModifierSelectIndex];

                    // Apply the primary player's modifiers to the other vocal players
                    // for this session only, so their own saved selections survive
                    foreach (var player in PlayerContainer.Players)
                    {
                        if (player.SittingOut) continue;
                        if (player == primaryPlayer) continue;

                        if (player.Profile.GameMode == GameMode.Vocals)
                        {
                            player.Profile.ApplySessionModifiers(primaryPlayer.Profile);
                        }
                    }
                }

                // This will always work (as it's set up in the input field)
                // The max speed that the game can keep up with is 5000%
                float speed = float.Parse(_speedInput.text.TrimEnd('%')) / 100f;
                speed = Mathf.Clamp(speed, 0.1f, 50.0f);
                _songSpeed = speed;
                GlobalVariables.State.SongSpeed = speed;

                GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
                return;
            }

            var profile = CurrentPlayer.Profile;
            var song = GlobalVariables.State.CurrentSong;

            // Get the possible instruments for this show and player
            // TODO: We should probably allow players to select instruments that are not in
            //  all songs and have them sit out songs that don't have that instrument
            // TODO: We should also let Ekit users choose an option that switches them between
            // each song's native drum format
            _possibleInstruments.Clear();
            var allowedInstruments = profile.GameMode.PossibleInstrumentsForSong(GlobalVariables.State.CurrentSong);

            foreach (var instrument in allowedInstruments)
            {
                bool invalidInstrument = false;
                foreach (var showSong in _songList)
                {
                    if (!HasPlayableInstrument(showSong, instrument))
                    {
                        invalidInstrument = true;
                        break;
                    }
                }

                if (!invalidInstrument)
                {
                    _possibleInstruments.Add(instrument);
                }
            }

            // If the player's preferred instrument is available, set CurrentInstrument to that
            if (_possibleInstruments.Contains(profile.PreferredInstrument))
            {
                profile.CurrentInstrument = profile.PreferredInstrument;
            }

            // Set the instrument to a valid one
            if (!_possibleInstruments.Contains(profile.CurrentInstrument) && _possibleInstruments.Count > 0)
            {
                profile.CurrentInstrument = _possibleInstruments[0];
            }

            // Get the possible harmonies for this show
            _maxHarmonyIndex = song.VocalsCount;
            foreach (var showsong in GlobalVariables.State.ShowSongs)
            {
                _maxHarmonyIndex = Mathf.Min(_maxHarmonyIndex, showsong.VocalsCount);
            }

            // Resolve the effective harmony index for this song from the player's last
            // explicit selection (clamped to the available parts). Uses ResolveHarmonyIndex
            // so the raw backing field is checked regardless of CurrentInstrument
            // (HarmonyIndex getter returns 0 when not on Harmony, which would mask an
            // out-of-range value from a direct comparison), and so a song with fewer
            // parts doesn't permanently erase the selection — like DifficultyFallback
            // preserves Expert+ across songs that lack it.
            profile.ResolveHarmonyIndex(_maxHarmonyIndex);

            UpdatePossibleModifiers();

            // Don't sit out by default
            CurrentPlayer.SittingOut = false;

            // Update the possible difficulties as well
            UpdatePossibleDifficulties();

            UpdateForPlayer();
        }

        private void UpdatePossibleDifficulties()
        {
            _possibleDifficulties.Clear();

            var profile = CurrentPlayer.Profile;

            // Get the possible difficulties for the player's instrument in the song
            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
            {
                bool invalidDifficulty = false;
                foreach (var showsong in _songList)
                {
                    if (!HasPlayableDifficulty(showsong, profile.CurrentInstrument, difficulty))
                    {
                        invalidDifficulty = true;
                        break;
                    }
                }

                if (!invalidDifficulty)
                {
                    _possibleDifficulties.Add(difficulty);
                }
            }

            // TODO: Handle difficulty fallback better in play a show mode

            var diff = (int) profile.DifficultyFallback;
            while (diff >= (int) Difficulty.Beginner && !_possibleDifficulties.Contains((Difficulty) diff))
            {
                --diff;
            }

            if (diff < (int) Difficulty.Beginner)
            {
                diff = (int) profile.DifficultyFallback;
                while (diff < (int) Difficulty.ExpertPlus)
                {
                    ++diff;
                    if (_possibleDifficulties.Contains((Difficulty) diff))
                    {
                        break;
                    }
                }
            }
            profile.CurrentDifficulty = (Difficulty) diff;
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        private DifficultyItem CreateItem(string header, string body, bool selected, DifficultyItem difficultyItem, UnityAction a, bool interactable = true)
        {
            var btn = Instantiate(difficultyItem, _container);
            return FinishCreateItem(btn, header, body, selected, a, interactable);
        }

        private DifficultyItem CreateItem(string header, string body, bool selected, GameObject itemPrefab, UnityAction a)
        {
            var btn = Instantiate(itemPrefab, _container).GetComponent<DifficultyItem>();
            return FinishCreateItem(btn, header, body, selected, a);
        }

        private DifficultyItem FinishCreateItem(DifficultyItem btn, string header, string body,
            bool selected, UnityAction a, bool interactable = true)
        {
            if (header is null)
            {
                btn.Initialize(body, a);
            }
            else
            {
                btn.Initialize(header, body, a);
            }

            btn.SetInteractable(interactable);

            // Non-interactable items (e.g. a forced single-chart choice) are shown dimmed and
            // kept out of the nav group so they can't be focused or activated.
            if (!interactable)
            {
                return btn;
            }

            _navGroup.AddNavigatable(btn.Button);

            if (selected)
            {
                _navGroup.SelectLast();
            }

            return btn;
        }

        private DifficultyItem CreateItem(string body, bool selected, DifficultyItem difficultyItem, UnityAction a)
        {
            return CreateItem(null, body, selected, difficultyItem, a);
        }

        private DifficultyItem CreateItem(string header, string body, bool selected, UnityAction a, bool interactable = true)
        {
            return CreateItem(header, body, selected, _difficultyItemPrefab, a, interactable);
        }

        private DifficultyItem CreateItem(string body, bool selected, UnityAction a)
        {
            return CreateItem(null, body, selected, a);
        }

        private string LocalizeHeader(string key)
        {
            return Localize.Key("Menu.DifficultySelect", key);
        }

        private bool HasPlayableInstrument(SongEntry entry, in Instrument instrument)
        {
            // For vocals, all players *must* select the same gamemode (solo/harmony)
            if (instrument is Instrument.Vocals or Instrument.Harmony)
            {
                if (!entry.HasInstrument(instrument))
                {
                    return false;
                }

                // Loop through all of the players up to the current one
                // to see what has already been selected.
                for (int i = 0; i < _playerIndex; i++)
                {
                    var player = PlayerContainer.Players[i];
                    var playerInstrument = player.Profile.CurrentInstrument;
                    if (playerInstrument is Instrument.Vocals or Instrument.Harmony)
                    {
                        return playerInstrument == instrument;
                    }
                }
            }

            return entry.HasInstrument(instrument) || instrument switch
            {
                // Allow 5 -> 4-lane conversions to be played on 4-lane
                Instrument.FourLaneDrums or
                Instrument.ProDrums      => entry.HasInstrument(Instrument.FiveLaneDrums),
                // Allow 4 -> 5-lane conversions to be played on 5-lane
                Instrument.FiveLaneDrums => entry.HasInstrument(Instrument.ProDrums),
                _ => false
            };
        }

        private bool HasPlayableDifficulty(SongEntry entry, in Instrument instrument, in Difficulty difficulty)
        {
            // For vocals, insert special difficulties
            if (instrument is Instrument.Vocals or Instrument.Harmony)
            {
                return difficulty is not Difficulty.ExpertPlus;
            }

            // For PK, disallow beginner
            if (instrument is Instrument.ProKeys && difficulty is Difficulty.Beginner)
            {
                return false;
            }

            // Otherwise, we can do this
            return entry[instrument][difficulty] || instrument switch
            {
                // Allow 5 -> 4-lane conversions to be played on 4-lane
                Instrument.FourLaneDrums or
                Instrument.ProDrums      => entry[Instrument.FiveLaneDrums][difficulty],
                // Allow 4 -> 5-lane conversions to be played on 5-lane
                Instrument.FiveLaneDrums => entry[Instrument.ProDrums][difficulty],
                _ => false
            };
        }

        public void SongSpeedEndEdit(string text)
        {
            if (!float.TryParse(text.TrimEnd('%'), NumberStyles.Number, null, out var speed))
            {
                speed = 100;
            }

            int intSpeed = (int) Math.Clamp(speed, 10, 5000);

            _speedInput.SetTextWithoutNotify($"{intSpeed}%");
        }
    }
}
