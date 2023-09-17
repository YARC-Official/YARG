using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Core;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
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
            Modifiers
        }

        [SerializeField]
        private TextMeshProUGUI _subHeader;
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private NavigationGroup _navGroup;
        [SerializeField]
        private TextMeshProUGUI _text;

        [Space]
        [SerializeField]
        private DifficultyItem _difficultyItemPrefab;
        [SerializeField]
        private DifficultyItem _difficultyReadyPrefab;
        [SerializeField]
        private ModifierItem _modifierItemPrefab;

        private int _playerIndex;
        private State _menuState;

        private readonly List<Instrument> _possibleInstruments  = new();
        private readonly List<Difficulty> _possibleDifficulties = new();
        private readonly List<Modifier>   _possibleModifiers    = new();

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

            // ChangePlayer(0) will update for the current player
            _playerIndex = 0;
            ChangePlayer(0);
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
            }

            _navGroup.SelectFirst();
        }

        private void CreateMainMenu()
        {
            var player = CurrentPlayer;

            // Only show all these options if there are instruments available
            if (_possibleInstruments.Count > 0)
            {
                CreateItem("Instrument", player.Profile.Instrument.ToLocalizedName(), () =>
                {
                    _menuState = State.Instrument;
                    UpdateForPlayer();
                });

                CreateItem("Difficulty", player.Profile.Difficulty.ToLocalizedName(), () =>
                {
                    _menuState = State.Difficulty;
                    UpdateForPlayer();
                });

                CreateItem("Modifiers", player.Profile.Modifiers.ToString(), () =>
                {
                    _menuState = State.Modifiers;
                    UpdateForPlayer();
                });

                // Ready button
                var readyButton = Instantiate(_difficultyReadyPrefab, _container);
                readyButton.Initialize("Ready", () => ChangePlayer(1));
                _navGroup.AddNavigatable(readyButton.Button);
            }

            // Only show if there is more than one play, only if there is instruments available
            if (_possibleInstruments.Count <= 0 || PlayerContainer.Players.Count != 1) {
                // Sit out button
                CreateItem("Sit Out", () =>
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
                CreateItem(instrument.ToLocalizedName(), () =>
                {
                    CurrentPlayer.Profile.Instrument = instrument;
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
                CreateItem(difficulty.ToLocalizedName(), () =>
                {
                    CurrentPlayer.Profile.Difficulty = difficulty;

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
                        profile.AddModifiers(modifier);
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
                if (!songParts.HasInstrument(instrument)) continue;

                _possibleInstruments.Add(instrument);
            }

            // Set the instrument to a valid one
            if (!_possibleInstruments.Contains(profile.Instrument) && _possibleInstruments.Count > 0)
            {
                profile.Instrument = _possibleInstruments[0];
            }

            // Get the possible modifiers
            _possibleModifiers.Clear();
            _possibleModifiers.AddRange(profile.GameMode.PossibleModifiers());

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
                if (!songParts.HasDifficulty(profile.Instrument, difficulty)) continue;

                _possibleDifficulties.Add(difficulty);
            }

            // TODO: Remove Expert+
            // This is temporary until we replace expert+ with a modifier
            if (profile.Instrument is Instrument.ProDrums or Instrument.FourLaneDrums or Instrument.FiveLaneDrums &&
                _possibleDifficulties.Contains(Difficulty.Expert))
            {
                _possibleDifficulties.Add(Difficulty.ExpertPlus);
            }

            // Set the difficulty to a valid one
            if (!_possibleDifficulties.Contains(profile.Difficulty) && _possibleDifficulties.Count > 0)
            {
                profile.Difficulty = _possibleDifficulties[0];
            }
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        private void CreateItem(string header, string body, UnityAction a)
        {
            var btn = Instantiate(_difficultyItemPrefab, _container);
            btn.Initialize(header, body, a);
            _navGroup.AddNavigatable(btn.Button);
        }

        private void CreateItem(string body, UnityAction a)
        {
            var btn = Instantiate(_difficultyItemPrefab, _container);
            btn.Initialize(body, a);
            _navGroup.AddNavigatable(btn.Button);
        }
    }
}