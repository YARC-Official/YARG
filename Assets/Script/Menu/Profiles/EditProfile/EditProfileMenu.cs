using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Input;
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

        [Space]
        [SerializeField]
        private InputControlDialogMenu _controlDialog;

        private YargPlayer _currentPlayer;
        private GameMode _selectedGameMode;

        private void OnEnable()
        {
            _currentPlayer = PlayerContainer.GetPlayerFromProfile(CurrentProfile);
            _currentPlayer.DisableInputs();
            _currentPlayer.Bindings.BindingsChanged += RefreshBindings;

            RefreshGameModes();
        }

        private void OnDisable()
        {
            _currentPlayer.Bindings.BindingsChanged -= RefreshBindings;
            _currentPlayer.EnableInputs();
        }

        private void RefreshGameModes()
        {
            // Remove old ones
            _gameModeList.transform.DestroyChildren();
            _gameModeNavGroup.ClearNavigatables();

            // Spawn in a game mode view for the selected game mode
            var go = Instantiate(_gameModeViewPrefab, _gameModeList);
            go.GetComponent<GameModeView>().Init(_currentPlayer.Profile.GameMode, this);
            _gameModeNavGroup.AddNavigatable(go);

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
            var bindings = _currentPlayer.Bindings;
            foreach (var binding in bindings[gameMode].Gameplay)
            {
                // Create header
                var header = Instantiate(_bindHeaderPrefab, _bindsList);
                header.GetComponent<BindHeader>().Init(this, _currentPlayer, binding);

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

        public UniTask<bool> ShowControlDialog(YargPlayer player, ControlBinding binding)
        {
            return _controlDialog.Show(player, binding);
        }
    }
}