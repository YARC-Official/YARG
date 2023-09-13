using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Core;
using YARG.Core.Input;
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
                new NavigationScheme.Entry(MenuAction.Red, "Back", () => MenuManager.Instance.PopMenu())
            }, false));

            _playerIndex = 0;
            _menuState = State.Main;

            UpdateForPlayer();
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

            CreateItem("Instrument", player.Profile.Instrument.ToString(), () =>
            {
                _menuState = State.Instrument;
                UpdateForPlayer();
            });

            CreateItem("Difficulty", player.Profile.Difficulty.ToString(), () =>
            {
                _menuState = State.Difficulty;
                UpdateForPlayer();
            });

            // CreateItem("Modifiers", player.Profile.Modifiers.ToString(), () =>
            // {
            //     _menuState = State.Modifiers;
            //     UpdateForPlayer();
            // });

            CreateItem("Ready", NextPlayer);
        }

        private void CreateInstrumentMenu()
        {
            var profile = CurrentPlayer.Profile;
            var songParts = GlobalVariables.Instance.CurrentSong.Parts;

            var possibleInstruments = profile.GameMode.PossibleInstruments();

            foreach (var instrument in possibleInstruments)
            {
                if (!songParts.HasInstrument(instrument)) continue;

                CreateItem(instrument.ToString(), () =>
                {
                    profile.Instrument = instrument;
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

                CreateItem(difficulty.ToString(), () =>
                {
                    profile.Difficulty = difficulty;
                    _menuState = State.Main;
                    UpdateForPlayer();
                });
            }
        }

        private void NextPlayer()
        {
            _playerIndex++;
            _menuState = State.Main;

            // When the user(s) have selected all of their difficulties, move on
            if (_playerIndex >= PlayerContainer.Players.Count)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
            }
            else
            {
                UpdateForPlayer();
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