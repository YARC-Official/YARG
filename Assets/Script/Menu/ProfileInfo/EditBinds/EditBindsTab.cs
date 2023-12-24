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
        [SerializeField]
        private GameObject _bindHeaderPrefab;

        [Space]
        [SerializeField]
        private SingleButtonBindView _singleButtonViewPrefab;
        [SerializeField]
        private SingleAxisBindView _singleAxisViewPrefab;
        [SerializeField]
        private SingleIntegerBindView _singleIntegerViewPrefab;

        private YargPlayer _currentPlayer;

        public GameMode SelectedGameMode { get; private set; }
        public bool SelectingMenuBinds { get; private set; }

        private void OnEnable()
        {
            _currentPlayer = PlayerContainer.GetPlayerFromProfile(_profileInfoMenu.CurrentProfile);
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
                // Create header
                var header = Instantiate(_bindHeaderPrefab, _bindsList);
                header.GetComponent<BindHeader>().Init(this, _currentPlayer, binding);

                // Create the actual bindings
                switch (binding)
                {
                    case ButtonBinding button:
                        RefreshBinding<SingleButtonBindView, float, ButtonBinding, SingleButtonBinding>(
                            button, _singleButtonViewPrefab);
                        break;

                    case AxisBinding axis:
                        RefreshBinding<SingleAxisBindView, float, AxisBinding, SingleAxisBinding>(
                            axis, _singleAxisViewPrefab);
                        break;

                    case IntegerBinding integer:
                        RefreshBinding<SingleIntegerBindView, int, IntegerBinding, SingleIntegerBinding>(
                            integer, _singleIntegerViewPrefab);
                        break;
                }
            }

            LayoutRebuilder.MarkLayoutForRebuild(_gameModeList as RectTransform);
            LayoutRebuilder.MarkLayoutForRebuild(_bindsList as RectTransform);
        }

        private void RefreshBinding<TView, TState, TBinding, TSingle>(TBinding binding, TView prefab)
            where TView : SingleBindView<TState, TBinding, TSingle>
            where TState : struct
            where TBinding : ControlBinding<TState, TSingle>
            where TSingle : SingleBinding<TState>
        {
            foreach (var control in binding.Bindings)
            {
                // Create bind view
                var bindView = Instantiate(prefab, _bindsList);
                bindView.Init(this, binding, control);
            }
        }

        public void RefreshBindings()
        {
            if (SelectingMenuBinds)
            {
                RefreshMenuBindings();
            }
            else
            {
                RefreshBindings(SelectedGameMode);
            }
        }

        public UniTask<bool> ShowControlDialog(YargPlayer player, ControlBinding binding)
        {
            return _controlDialog.Show(player, binding);
        }
    }
}