﻿using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Core;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;

namespace YARG.Menu.DifficultySelect
{
    public class DifficultySelectMenu : MonoBehaviour
    {
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
        private State _lastMenuState;
        private State _menuState;

        private readonly List<Instrument> _possibleInstruments  = new();
        private readonly List<Difficulty> _possibleDifficulties = new();
        private readonly List<Modifier>   _possibleModifiers    = new();

        private int _maxHarmonyIndex = 3;

        private readonly List<ModifierItem> _modifierItems = new();

        private YargPlayer CurrentPlayer => PlayerContainer.Players[_playerIndex];

        private void OnEnable()
        {
            _subHeader.text = GlobalVariables.Instance.IsPractice ? "Practice" : "Quickplay";

            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Back", () =>
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

            _speedInput.text = "100%";

            // ChangePlayer(0) will update for the current player
            _playerIndex = 0;
            ChangePlayer(0);

            _loadingPhrase.text = RichTextUtils.StripRichTextTags(
                GlobalVariables.Instance.CurrentSong.LoadingPhrase, RichTextTags.BadTags);
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

            // Only show all these options if there are instruments available
            if (_possibleInstruments.Count > 0)
            {
                CreateItem("Instrument", player.Profile.CurrentInstrument.ToLocalizedName(), _lastMenuState == State.Instrument, () =>
                {
                    _menuState = State.Instrument;
                    UpdateForPlayer();
                });

                CreateItem("Difficulty", player.Profile.CurrentDifficulty.ToLocalizedName(), _lastMenuState == State.Difficulty, () =>
                {
                    _menuState = State.Difficulty;
                    UpdateForPlayer();
                });

                // Harmony players must pick their harmony index
                if (player.Profile.CurrentInstrument == Instrument.Harmony)
                {
                    CreateItem("Harmony", (player.Profile.HarmonyIndex + 1).ToString(), _lastMenuState == State.Harmony, () =>
                    {
                        _menuState = State.Harmony;
                        UpdateForPlayer();
                    });
                }

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

                CreateItem("Modifiers", modifierText, _lastMenuState == State.Modifiers, () =>
                {
                    _menuState = State.Modifiers;
                    UpdateForPlayer();
                });

                // Ready button
                CreateItem("Ready", _lastMenuState == State.Main, _difficultyGreenPrefab, () => ChangePlayer(1));
            }

            // Only show if there is more than one play, only if there is instruments available
            if (_possibleInstruments.Count <= 0 || PlayerContainer.Players.Count != 1)
            {
                // Sit out button
                CreateItem("Sit Out", _possibleInstruments.Count <= 0, _difficultyRedPrefab, () =>
                {
                    player.SittingOut = true;
                    ChangePlayer(1);
                });
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
                    CurrentPlayer.Profile.CurrentDifficulty = difficulty;

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
            CreateItem("Done", _difficultyGreenPrefab, () =>
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
                // This will always work (as it's set up in the input field)
                // The max speed that the game can keep up with is 4995%
                float speed = float.Parse(_speedInput.text.TrimEnd('%')) / 100f;
                speed = Mathf.Clamp(speed, 0.1f, 49.95f);
                GlobalVariables.Instance.SongSpeed = speed;

                GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
                return;
            }

            var profile = CurrentPlayer.Profile;
            var songParts = GlobalVariables.Instance.CurrentSong.Parts;

            // Get the possible instruments for this song and player
            _possibleInstruments.Clear();
            var allowedInstruments = profile.GameMode.PossibleInstruments();
            foreach (var instrument in allowedInstruments)
            {
                if (!HasPlayableInstrument(songParts, instrument)) continue;

                _possibleInstruments.Add(instrument);
            }

            // Set the instrument to a valid one
            if (!_possibleInstruments.Contains(profile.CurrentInstrument) && _possibleInstruments.Count > 0)
            {
                profile.CurrentInstrument = _possibleInstruments[0];
            }

            // Get the possible harmonies for this song
            _maxHarmonyIndex = songParts.VocalsCount;

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
            var songParts = GlobalVariables.Instance.CurrentSong.Parts;

            // Get the possible difficulties for the player's instrument in the song
            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
            {
                if (!HasPlayableDifficulty(songParts, profile.CurrentInstrument, difficulty))
                {
                    continue;
                }

                _possibleDifficulties.Add(difficulty);
            }

            // TODO: Remove Expert+
            // This is temporary until we replace expert+ with a modifier
            if (profile.CurrentInstrument is Instrument.ProDrums or Instrument.FourLaneDrums or Instrument.FiveLaneDrums &&
                _possibleDifficulties.Contains(Difficulty.Expert))
            {
                _possibleDifficulties.Add(Difficulty.ExpertPlus);
            }

            // Set the difficulty to a valid one
            if (!_possibleDifficulties.Contains(profile.CurrentDifficulty) && _possibleDifficulties.Count > 0)
            {
                profile.CurrentDifficulty = _possibleDifficulties[0];
            }
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

        private bool HasPlayableInstrument(AvailableParts parts, Instrument instrument)
        {
            // For vocals, all players *must* select the same gamemode (solo/harmony)
            if (instrument is Instrument.Vocals or Instrument.Harmony)
            {
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

            return parts.HasInstrument(instrument) || instrument switch
            {
                // Allow 5 -> 4-lane conversions to be played on 4-lane
                Instrument.FourLaneDrums or
                Instrument.ProDrums      => parts.HasInstrument(Instrument.FiveLaneDrums),
                // Allow 4 -> 5-lane conversions to be played on 5-lane
                Instrument.FiveLaneDrums => parts.HasInstrument(Instrument.ProDrums),
                _ => false
            };
        }

        private bool HasPlayableDifficulty(AvailableParts parts, Instrument instrument, Difficulty difficulty)
        {
            // For vocals, insert special difficulties
            if (instrument is Instrument.Vocals or Instrument.Harmony)
            {
                return difficulty is not (Difficulty.Beginner or Difficulty.ExpertPlus);
            }

            // Otherwise, we can do this
            return parts.HasDifficulty(instrument, difficulty) || instrument switch
            {
                // Allow 5 -> 4-lane conversions to be played on 4-lane
                Instrument.FourLaneDrums or
                Instrument.ProDrums      => parts.HasDifficulty(Instrument.FiveLaneDrums, difficulty),
                // Allow 4 -> 5-lane conversions to be played on 5-lane
                Instrument.FiveLaneDrums => parts.HasDifficulty(Instrument.ProDrums, difficulty),
                _ => false
            };
        }

        public void SongSpeedEndEdit(string text)
        {
            if (!int.TryParse(text.TrimEnd('%'), NumberStyles.Number, null, out int speed))
                speed = 100;
            speed = Math.Clamp(speed, 10, 4995);
            _speedInput.SetTextWithoutNotify($"{speed}%");
        }
    }
}