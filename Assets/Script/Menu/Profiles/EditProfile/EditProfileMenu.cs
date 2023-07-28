using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
using YARG.Player;

namespace YARG.Menu.Profiles
{
    public class EditProfileMenu : MonoBehaviour
    {
        public static YargProfile CurrentProfile { get; set; }

        [SerializeField]
        private NavigationGroup _gameModeNavGroup;
        [SerializeField]
        private NavigationGroup _bindsNavGroup;

        [Space]
        [SerializeField]
        private Transform _gameModeList;
        [SerializeField]
        private Transform _bindsList;

        [Space]
        [SerializeField]
        private GameObject _gameModeViewPrefab;
        [SerializeField]
        private GameObject _bindHeaderPrefab;
        [SerializeField]
        private GameObject _bindViewPrefab;

        private YargPlayer _currentPlayer;
        private GameMode _selectedGameMode;

        private void OnEnable()
        {
            _currentPlayer = PlayerContainer.GetPlayerFromProfile(CurrentProfile);
            _currentPlayer.DisableInputs();
            RefreshGameModes();
        }

        private void OnDisable()
        {
            _currentPlayer.EnableInputs();
        }

        private void RefreshGameModes()
        {
            // Remove old ones
            _gameModeList.transform.DestroyChildren();
            _gameModeNavGroup.ClearNavigatables();

            // Spawn in a profile view for each player
            foreach (var gameMode in EnumExtensions<GameMode>.Values)
            {
                var go = Instantiate(_gameModeViewPrefab, _gameModeList);
                go.GetComponent<GameModeView>().Init(gameMode, this);

                _gameModeNavGroup.AddNavigatable(go);
            }

            // Select first game mode
            _gameModeNavGroup.SelectFirst();
        }

        // This is initially called from the "OnSelectionChanged." See method usages.
        public void RefreshBindings(GameMode gameMode)
        {
            _selectedGameMode = gameMode;

            // Remove old ones
            _bindsList.DestroyChildren();
            _bindsNavGroup.ClearNavigatables();

            // Create the list of bindings
            var deviceBindings = _currentPlayer.Bindings.GetBindingsForFirstDevice();
            foreach (var binding in deviceBindings[gameMode].Gameplay)
            {
                // Create header
                var header = Instantiate(_bindHeaderPrefab, _bindsList);
                header.GetComponent<BindHeader>().Init(this, deviceBindings.Device, binding);

                _bindsNavGroup.AddNavigatable(header);

                // Create the actual bindings
                foreach (var control in binding.Controls)
                {
                    // Create bind view
                    var bindView = Instantiate(_bindViewPrefab, _bindsList);
                    bindView.GetComponent<BindView>().Init(this, binding, control.InputControl);

                    _bindsNavGroup.AddNavigatable(bindView);
                }
            }
        }

        public void RefreshBindings()
        {
            RefreshBindings(_selectedGameMode);
        }
    }
}