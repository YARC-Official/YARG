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
                switch (binding)
                {
                    case ButtonBinding button:
                        RefreshBinding<ButtonBindGroup, SingleButtonBindView,
                            float, ButtonBinding, SingleButtonBinding>(
                            _buttonGroupPrefab, _singleButtonViewPrefab, button);
                        break;

                    case AxisBinding axis:
                        RefreshBinding<AxisBindGroup, SingleAxisBindView,
                            float, AxisBinding, SingleAxisBinding>(
                            _axisGroupPrefab, _singleAxisViewPrefab, axis);
                        break;

                    case IntegerBinding integer:
                        RefreshBinding<IntegerBindGroup, SingleIntegerBindView,
                            int, IntegerBinding, SingleIntegerBinding>(
                            _integerGroupPrefab, _singleIntegerViewPrefab, integer);
                        break;
                }
            }

            LayoutRebuilder.MarkLayoutForRebuild(_gameModeList as RectTransform);
            LayoutRebuilder.MarkLayoutForRebuild(_bindsList as RectTransform);
        }

        private void RefreshBinding<TGroup, TView, TState, TBinding, TSingle>(
            TGroup groupFab, TView viewFab, TBinding binding)
            where TGroup : BindGroup<TState, TBinding, TSingle>
            where TView : SingleBindView<TState, TBinding, TSingle>
            where TState : struct
            where TBinding : ControlBinding<TState, TSingle>
            where TSingle : SingleBinding<TState>
        {
            var group = Instantiate(groupFab, _bindsList);
            group.Init(this, _currentPlayer, binding);

            foreach (var control in binding.Bindings)
            {
                // Create bind view
                var bindView = Instantiate(viewFab, _bindsList);
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