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
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
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
            Modifiers,
            Harmony
        }

        [SerializeField]
        private TextMeshProUGUI _subHeader;
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private NavigationGroup _navGroup;
        [SerializeField]
        private TextMeshProUGUI _text;
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
        private ModifierItem _modifierItemPrefab;

        private int _playerIndex;
        private int _vocalModifierSelectIndex = -1;

        private State _lastMenuState;
        private State _menuState;

        private readonly List<Instrument> _possibleInstruments  = new();
        private readonly List<Difficulty> _possibleDifficulties = new();
        private readonly List<Modifier>   _possibleModifiers    = new();

        private int _maxHarmonyIndex = 3;

        private readonly List<ModifierItem> _modifierItems = new();

        private List<SongEntry> _songList;

        private YargPlayer CurrentPlayer => PlayerContainer.Players[_playerIndex];

        private void OnEnable()
        {
            string subHeaderKey = GlobalVariables.State.IsPractice ? "Practice" : "Quickplay";
            _subHeader.text = Localize.Key("Menu.Main.Options", subHeaderKey);

            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
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
                        _menuState = State.Main;
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

            // ChangePlayer(0) will update for the current player
            _playerIndex = 0;
            _vocalModifierSelectIndex = -1;
            ChangePlayer(0);

            _loadingPhrase.text = RichTextUtils.StripRichTextTags(
                GlobalVariables.State.CurrentSong.LoadingPhrase, RichTextTags.BadTags);

            _sourceIcon.sprite = SongSources.SourceToIcon(GlobalVariables.State.CurrentSong.Source);
            _sourceIcon.gameObject.SetActive(_sourceIcon.sprite != null);
        }

        private void UpdateForPlayer()
        {
            // Set player text
            var profile = CurrentPlayer.Profile;
            _text.text = $"<sprite name=\"{profile.GameMode.ToResourceName()}\"> {profile.Name}";

            // Reset content
            _navGroup.ClearNavigatables();
            _container.DestroyChildren();

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
                case State.Modifiers:
                    CreateModifierMenu();
                    break;
                case State.Harmony:
                    CreateHarmonyMenu();
                    break;
            }

            _lastMenuState = _menuState;
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
                CreateItem(LocalizeHeader("Instrument"),
                    player.Profile.CurrentInstrument.ToLocalizedName(),
                    _lastMenuState == State.Instrument, () =>
                {
                    _menuState = State.Instrument;
                    UpdateForPlayer();
                });

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
                if (player.Profile.CurrentInstrument.ToGameMode() != GameMode.Vocals ||
                    _vocalModifierSelectIndex == -1 ||
                    _vocalModifierSelectIndex == _playerIndex)
                {
                    // Create modifiers body text
                    string modifierText = "";
                    if (player.Profile.CurrentModifiers == Modifier.None)
                    {
                        // If there are no modifiers, then just say "none"
                        modifierText = Modifier.None.ToLocalizedName();
                    }
                    else
                    {
                        // Combine all modifiers
                        foreach (var modifier in _possibleModifiers)
                        {
                            if (!player.Profile.IsModifierActive(modifier)) continue;

                            modifierText += modifier.ToLocalizedName() + "\n";
                        }

                        modifierText = modifierText.Trim();
                    }

                    CreateItem(LocalizeHeader("Modifiers"),
                        modifierText, _lastMenuState == State.Modifiers, () =>
                    {
                        _menuState = State.Modifiers;
                        UpdateForPlayer();
                    });
                }

                // Ready button
                CreateItem(LocalizeHeader("Ready"), _lastMenuState == State.Main, _difficultyGreenPrefab, () =>
                {
                    // If the player just selected vocal modifiers, don't show them again
                    if (player.Profile.CurrentInstrument.ToGameMode() == GameMode.Vocals &&
                        _vocalModifierSelectIndex == -1)
                    {
                        _vocalModifierSelectIndex = _playerIndex;
                    }

                    ChangePlayer(1);
                });
            }

            // Only show if there is more than one play, only if there is instruments available
            if (_possibleInstruments.Count <= 0 || PlayerContainer.Players.Count != 1)
            {
                // Sit out button
                CreateItem(LocalizeHeader("SitOut"), _possibleInstruments.Count <= 0, _difficultyRedPrefab, () =>
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

        private void CreateInstrumentMenu()
        {
            foreach (var instrument in _possibleInstruments)
            {
                bool selected = CurrentPlayer.Profile.CurrentInstrument == instrument;
                CreateItem(instrument.ToLocalizedName(), selected, () =>
                {
                    CurrentPlayer.Profile.CurrentInstrument = instrument;
                    UpdatePossibleDifficulties();

                    _menuState = State.Main;
                    UpdateForPlayer();

                    // Update the instrument icons on the Status Bar (if enabled)
                    StatsManager.Instance.UpdateActivePlayers();
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

        private void CreateModifierMenu()
        {
            var profile = CurrentPlayer.Profile;

            _modifierItems.Clear();
            foreach (var modifier in _possibleModifiers)
            {
                var btn = Instantiate(_modifierItemPrefab, _container);
                btn.Initialize(modifier.ToLocalizedName(), profile.IsModifierActive(modifier), active =>
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

                _navGroup.AddNavigatable(btn);
                _modifierItems.Add(btn);
            }

            // Create done button
            CreateItem(LocalizeHeader("Done"), _difficultyGreenPrefab, () =>
            {
                _menuState = State.Main;
                UpdateForPlayer();
            });
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
                var modifier = _possibleModifiers[i];

                item.Active = profile.IsModifierActive(modifier);
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

                    // Copy modifiers to all other vocal players
                    foreach (var player in PlayerContainer.Players)
                    {
                        if (player.SittingOut) continue;
                        if (player == primaryPlayer) continue;

                        if (player.Profile.CurrentInstrument.ToGameMode() == GameMode.Vocals)
                        {
                            player.Profile.CopyModifiers(primaryPlayer.Profile);
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
            _possibleInstruments.Clear();
            var allowedInstruments = profile.GameMode.PossibleInstruments();

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

            // Set the harmony index to a valid one
            if (profile.HarmonyIndex >= _maxHarmonyIndex)
            {
                profile.HarmonyIndex = 0;
            }

            // Get the possible modifiers (split the enum into multiple) and
            // make sure current modifiers are valid, and remove the invalid ones
            _possibleModifiers.Clear();
            var possible = profile.GameMode.PossibleModifiers();
            foreach (var modifier in EnumExtensions<Modifier>.Values)
            {
                // Skip if the modifier is not a possible one
                if ((possible & modifier) == 0)
                {
                    // Also try to remove it if the player has it for some reason
                    if (profile.IsModifierActive(modifier))
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

        private void CreateItem(string header, string body, bool selected, DifficultyItem difficultyItem, UnityAction a)
        {
            var btn = Instantiate(difficultyItem, _container);

            if (header is null)
            {
                btn.Initialize(body, a);
            }
            else
            {
                btn.Initialize(header, body, a);
            }

            _navGroup.AddNavigatable(btn.Button);

            if (selected)
            {
                _navGroup.SelectLast();
            }
        }

        private void CreateItem(string body, bool selected, DifficultyItem difficultyItem, UnityAction a)
        {
            CreateItem(null, body, selected, difficultyItem, a);
        }

        private void CreateItem(string header, string body, bool selected, UnityAction a)
        {
            CreateItem(header, body, selected, _difficultyItemPrefab, a);
        }

        private void CreateItem(string body, bool selected, UnityAction a)
        {
            CreateItem(null, body, selected, a);
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
                return difficulty is not (Difficulty.Beginner or Difficulty.ExpertPlus);
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