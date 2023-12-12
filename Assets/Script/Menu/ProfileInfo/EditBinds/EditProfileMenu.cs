using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Input;
using YARG.Menu.Navigation;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    public class EditProfileMenu : MonoBehaviour
    {
        public static YargProfile CurrentProfile { get; set; }

        [SerializeField]
        private NavigationGroup _gameModeNavGroup;

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
        private GameObject _buttonViewPrefab;
        [SerializeField]
        private GameObject _axisViewPrefab;
        [SerializeField]
        private GameObject _integerViewPrefab;

        [Space]
        [SerializeField]
        private InputControlDialogMenu _controlDialog;

        private YargPlayer _currentPlayer;

        public GameMode SelectedGameMode { get; private set; }
        public bool SelectingMenuBinds { get; private set; }

        private void OnEnable()
        {
            _currentPlayer = PlayerContainer.GetPlayerFromProfile(CurrentProfile);
            _currentPlayer.DisableInputs();
            _currentPlayer.Bindings.BindingsChanged += RefreshBindings;

            RefreshGameModes();

            Navigator.Instance.PushScheme(NavigationScheme.EmptyWithMusicPlayer);
        }

        private void OnDisable()
        {
            _currentPlayer.Bindings.BindingsChanged -= RefreshBindings;
            _currentPlayer.EnableInputs();

            Navigator.Instance.PopScheme();
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
            gameModeView.GetComponent<GameModeView>().InitAsMenu(this);
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
                        RefreshBinding<ButtonBindView, float, ButtonBinding, SingleButtonBinding>(
                            button, _buttonViewPrefab);
                        break;

                    case AxisBinding axis:
                        RefreshBinding<AxisBindView, float, AxisBinding, SingleAxisBinding>(
                            axis, _axisViewPrefab);
                        break;

                    case IntegerBinding integer:
                        RefreshBinding<IntegerBindView, int, IntegerBinding, SingleIntegerBinding>(
                            integer, _integerViewPrefab);
                        break;
                }
            }

            LayoutRebuilder.MarkLayoutForRebuild(_gameModeList as RectTransform);
            LayoutRebuilder.MarkLayoutForRebuild(_bindsList as RectTransform);
        }

        private void RefreshBinding<TView, TState, TBinding, TSingle>(TBinding binding, GameObject prefab)
            where TView : BindView<TState, TBinding, TSingle>
            where TState : struct
            where TBinding : ControlBinding<TState, TSingle>
            where TSingle : SingleBinding<TState>
        {
            foreach (var control in binding.Bindings)
            {
                // Create bind view
                var bindView = Instantiate(prefab, _bindsList);
                bindView.GetComponent<TView>().Init(this, binding, control);
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