using UnityEngine;
using YARG.Core;
using YARG.Helpers.Extensions;

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
            // TODO: Make this *not* hard coded
            void CreateHeader(string id)
            {
                var go = Instantiate(_bindHeaderPrefab, _bindsList);
                go.GetComponent<BindHeader>().Init(id);

                _bindsNavGroup.AddNavigatable(go);
            }

            _selectedGameMode = gameMode;

            // Remove old ones
            _bindsList.DestroyChildren();
            _bindsNavGroup.ClearNavigatables();

            CreateHeader("greenFret");
            CreateHeader("redFret");
            CreateHeader("yellowFret");
            CreateHeader("blueFret");
            CreateHeader("orangeFret");
        }
    }
}