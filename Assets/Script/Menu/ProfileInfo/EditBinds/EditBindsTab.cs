using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Helpers.Extensions;
using YARG.Input;
using YARG.Menu.Navigation;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    public class EditBindsTab : MonoBehaviour
    {
        [SerializeField]
        private ProfileInfoMenu _profileInfoMenu;

        [Space]
        [SerializeField]
        private NavigationGroup _gameModeNavGroup;
        [SerializeField]
        private InputControlDialogMenu _controlDialog;

        [Space]
        [SerializeField]
        private Transform _gameModeList;
        [SerializeField]
        private Transform _bindsList;

        [Space]
        [SerializeField]
        private GameObject _gameModeViewPrefab;

        [Space]
        [SerializeField]
        private ButtonBindGroup _buttonGroupPrefab;
        [SerializeField]
        private AxisBindGroup _axisGroupPrefab;
        [SerializeField]
        private IntegerBindGroup _integerGroupPrefab;

        private YargPlayer _currentPlayer;

        public GameMode SelectedGameMode { get; private set; }
        public bool SelectingMenuBinds { get; private set; }

        private void OnEnable()
        {
            _currentPlayer = PlayerContainer.GetPlayerFromProfile(_profileInfoMenu.CurrentProfile);
            _currentPlayer.DisableInputs();

            RefreshGameModes();
        }

        private void OnDisable()
        {
            // Destroy binds list, since they register events with the bindings
            // and need to be unregistered
            _bindsList.DestroyChildren();
            _gameModeList.DestroyChildren();
            _currentPlayer.EnableInputs();
        }

        private void RefreshGameModes()
        {
            // Remove old ones
            _gameModeList.DestroyChildren();
            _gameModeNavGroup.ClearNavigatables();

            // Spawn in a game mode view for the selected game mode
            var gameModeView = Instantiate(_gameModeViewPrefab, _gameModeList);
            gameModeView.GetComponent<GameModeView>().Init(_currentPlayer.Profile.GameMode, this);
            _gameModeNavGroup.AddNavigatable(gameModeView);

            // Spawn in a game mode view for the menu binds
            gameModeView = Instantiate(_gameModeViewPrefab, _gameModeList);
            gameModeView.GetComponent<GameModeView>().InitAsMenuBindings(this);
            _gameModeNavGroup.AddNavigatable(gameModeView);

            // Select first game mode
            _gameModeNavGroup.SelectFirst();
        }

        // This is initially called from the "OnSelectionChanged." See method usages.
        public void RefreshBindings(GameMode gameMode)
        {
            SelectedGameMode = gameMode;
            SelectingMenuBinds = false;

            RefreshFromBindingCollection(_currentPlayer.Bindings[gameMode]);
        }

        public void RefreshMenuBindings()
        {
            SelectingMenuBinds = true;

            RefreshFromBindingCollection(_currentPlayer.Bindings.MenuBindings);
        }

        private void RefreshFromBindingCollection(BindingCollection collection)
        {
            // Remove old ones
            _bindsList.DestroyChildren();

            // Create the list of bindings
            foreach (var binding in collection)
            {
                switch (binding)
                {
                    case ButtonBinding button:
                        var buttonGroup = Instantiate(_buttonGroupPrefab, _bindsList);
                        buttonGroup.Init(this, _currentPlayer, button);
                        break;

                    case AxisBinding axis:
                        var axisGroup = Instantiate(_axisGroupPrefab, _bindsList);
                        axisGroup.Init(this, _currentPlayer, axis);
                        break;

                    case IntegerBinding integer:
                        var integerGroup = Instantiate(_integerGroupPrefab, _bindsList);
                        integerGroup.Init(this, _currentPlayer, integer);
                        break;
                }
            }
        }

        public UniTask<bool> ShowControlDialog(YargPlayer player, ControlBinding binding)
        {
            return _controlDialog.Show(player, binding);
        }
    }
}