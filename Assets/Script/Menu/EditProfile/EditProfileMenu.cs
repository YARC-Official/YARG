using UnityEngine;
using YARG.Core;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Menu.EditProfile
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

        private GameMode _selectedGameMode;

        private void OnEnable()
        {
            RefreshGameModes();
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

            // Get the bindings
            var player = PlayerContainer.GetPlayerFromProfile(CurrentProfile);
            var deviceBindings = player.Bindings.GetBindingsForFirstDevice();
            var gameModeBindings = deviceBindings.GetOrCreateBindingsForGameMode(gameMode);

            // Create the list of bindings
            foreach (var binding in gameModeBindings)
            {
                var go = Instantiate(_bindHeaderPrefab, _bindsList);
                go.GetComponent<BindHeader>().Init(binding);

                _bindsNavGroup.AddNavigatable(go);
            }
        }
    }
}