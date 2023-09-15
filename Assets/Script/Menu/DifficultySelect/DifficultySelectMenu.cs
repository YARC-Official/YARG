using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Core;
using YARG.Core.Extensions;
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

        [Space]
        [SerializeField]
        private DifficultyItem _difficultyItemPrefab;

        private int _playerIndex;
        private State _menuState;

        private readonly List<Instrument> _possibleInstruments = new();
        private readonly List<Difficulty> _possibleDifficulties = new();

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
            _navGroup.ClearNavigatables();
            _container.DestroyChildren();

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
            }

            _navGroup.SelectFirst();
        }

        private void CreateMainMenu()
        {
            var player = CurrentPlayer;

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

            // CreateItem("Modifiers", player.Profile.Modifiers.ToString(), () =>
            // {
            //     _menuState = State.Modifiers;
            //     UpdateForPlayer();
            // });

            CreateItem("Ready", () => ChangePlayer(1));
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
            var profile = CurrentPlayer.Profile;
            var songParts = GlobalVariables.Instance.CurrentSong.Parts;

            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
            {
                if (!songParts.HasPart(profile.Instrument, (int) difficulty)) continue;

                CreateItem(difficulty.ToLocalizedName(), () =>
                {
                    profile.Difficulty = difficulty;

                    _menuState = State.Main;
                    UpdateForPlayer();
                });
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
            if (!_possibleInstruments.Contains(profile.Instrument))
            {
                profile.Instrument = _possibleInstruments[0];
            }

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
                if (!songParts.HasPart(profile.Instrument, (int) difficulty)) continue;

                _possibleDifficulties.Add(difficulty);
            }

            // Set the difficulty to a valid one
            if (!_possibleDifficulties.Contains(profile.Difficulty))
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