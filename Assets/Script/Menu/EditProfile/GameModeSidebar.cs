using System;
using UnityEngine;
using YARG.Core;
using YARG.Helpers.Extensions;
using YARG.Menu.Profiles;
using YARG.Player;

namespace YARG.Menu.EditProfile
{
    public class GameModeSidebar : MonoBehaviour
    {
        [SerializeField]
        private NavigationGroup _navigationGroup;

        [Space]
        [SerializeField]
        private Transform _gameModeList;

        [Space]
        [SerializeField]
        private GameObject _gameModeViewPrefab;

        private void OnEnable()
        {
            RefreshList();
        }

        private void RefreshList()
        {
            // Remove old ones
            _gameModeList.transform.DestroyChildren();
            _navigationGroup.ClearNavigatables();

            // Spawn in a profile view for each player
            foreach (var gameMode in EnumExtensions<GameMode>.Values)
            {
                var go = Instantiate(_gameModeViewPrefab, _gameModeList);
                go.GetComponent<GameModeView>().Init(gameMode);

                _navigationGroup.AddNavigatable(go.GetComponent<NavigatableBehaviour>());
            }

            // Select first game mode
            _navigationGroup.SelectFirst();
        }
    }
}