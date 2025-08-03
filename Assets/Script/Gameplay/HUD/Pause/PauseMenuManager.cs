using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Song;

namespace YARG.Gameplay.HUD
{
    public class PauseMenuManager : GameplayBehaviour
    {
        public enum Menu
        {
            None,
            QuickPlayPause,
            PracticePause,
            SelectSections,
            ReplayPause,
            QuickSettings,
            SettingsMenu,
            SetlistPause,
        }

        private Dictionary<Menu, PauseMenuObject> _menus;

        private readonly Stack<Menu> _openMenus = new();

        [Space]
        [SerializeField]
        private TextMeshProUGUI _albumText;
        [SerializeField]
        private TextMeshProUGUI _songText;
        [SerializeField]
        private TextMeshProUGUI _artistText;
        [SerializeField]
        private TextMeshProUGUI _sourceText;
        [SerializeField]
        private Image _sourceIcon;
        [SerializeField]
        private RawImage _albumCover;

        public bool IsOpen => _openMenus.Count > 0;

        protected override void GameplayAwake()
        {
            // Convert to dictionary with "Menu" as key
            var children = GetComponentsInChildren<PauseMenuObject>(true);
            _menus = children.ToDictionary(i => i.Menu, i => i);
        }

        private void Start()
        {
            // Set text info
            _albumText.text = GameManager.Song.Album;
            _songText.text = GameManager.Song.Name;
            _artistText.text = GameManager.Song.Artist;
            _sourceText.text = SongSources.SourceToGameName(GameManager.Song.Source);

            // Set source icon
            _sourceIcon.sprite = SongSources.SourceToIcon(GameManager.Song.Source);

            // Set album cover
            _albumCover.LoadAlbumCover(GameManager.Song, CancellationToken.None);
        }

        protected override void GameplayDestroy()
        {
            if (_albumCover.texture != null)
            {
                // Make sure to free the texture. *This is NOT destroying the raw image*.
                Destroy(_albumCover.texture);
            }
        }

        public PauseMenuObject PushMenu(Menu menu)
        {
            // Active if not
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // Get the new one
            if (!_menus.TryGetValue(menu, out var newMenu))
            {
                throw new InvalidOperationException($"Failed to open menu {menu}.");
            }

            // Close the currently open one
            if (_openMenus.TryPeek(out var currentMenuEnum) &&
                _menus.TryGetValue(currentMenuEnum, out var currentMenu))
            {
                currentMenu.gameObject.SetActive(false);
            }

            // Show the new one
            newMenu.gameObject.SetActive(true);

            // ... and push it onto the stack
            _openMenus.Push(menu);

            return newMenu;
        }

        public void PopMenu()
        {
            // Close the currently open one
            if (_openMenus.TryPeek(out var currentMenuEnum) &&
                _menus.TryGetValue(currentMenuEnum, out var currentMenu))
            {
                currentMenu.gameObject.SetActive(false);
            }

            _openMenus.Pop();
            var menu = _openMenus.Count <= 0 ? Menu.None : _openMenus.Peek();

            // Show the under one
            if (_menus.TryGetValue(menu, out var newMenu))
            {
                newMenu.gameObject.SetActive(true);
            }
            else if (menu != Menu.None)
            {
                throw new InvalidOperationException($"Failed to open menu {menu}.");
            }
        }

        public void PopAllMenusWithResume()
        {
            PopAllMenus();
            GameManager.Resume();
        }

        public void PopAllMenus()
        {
            foreach (var menu in _openMenus)
            {
                _menus[menu].gameObject.SetActive(false);
            }

            _openMenus.Clear();
            gameObject.SetActive(false);
        }

        public void Quit()
        {
            GameManager.ForceQuitSong();
        }

        public void Restart()
        {
            if (GameManager.IsPractice && GlobalVariables.State.IsPractice)
            {
                PopMenu();
                GameManager.PracticeManager.ResetPractice();
                return;
            }

            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }

        public void Skip()
        {
            if (!GlobalVariables.State.PlayingAShow)
            {
                // We should not be called in this case, so do nothing
                return;
            }

            if (GlobalVariables.State.ShowIndex >= GlobalVariables.State.ShowSongs.Count - 1)
            {
                // There is no next song, so again we shouldn't have been called, but we
                // can do something this time
                Quit();
            }

            // Go to next song in setlist
            GlobalVariables.State.ShowIndex++;
            GlobalVariables.State.CurrentSong = GlobalVariables.State.ShowSongs[GlobalVariables.State.ShowIndex];
            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }
    }
}