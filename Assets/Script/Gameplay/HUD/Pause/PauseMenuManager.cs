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

        protected override void GameplayAwake()
        {
            // Convert to dictionary with "Menu" as key
            var children = GetComponentsInChildren<PauseMenuObject>(true);
            _menus = children.ToDictionary(i => i.Menu, i => i);
        }

        private async void Start()
        {
            // Set text info
            _albumText.text = GameManager.Song.Album;
            _songText.text = GameManager.Song.Name;
            _artistText.text = GameManager.Song.Artist;
            _sourceText.text = SongSources.SourceToGameName(GameManager.Song.Source);

            // Set source icon
            _sourceIcon.sprite = await SongSources.SourceToIcon(GameManager.Song.Source);

            // Set album cover
            _albumCover.LoadAlbumCover(GameManager.Song, CancellationToken.None).Forget();
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

        public void PopMenu(bool resume = true)
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

            // Clear all menus if resuming
            if (resume)
            {
                while (_openMenus.Count > 0)
                {
                    var popped = _openMenus.Pop();
                    if (_menus.TryGetValue(popped, out var poppedMenu))
                    {
                        poppedMenu.gameObject.SetActive(false);
                    }
                }
            }

            // Resume if nothing left
            if (_openMenus.Count <= 0 && resume)
            {
                GameManager.Resume();
            }
        }

        public void OpenMenu(Menu menu)
        {
            PopMenu(false);
            PushMenu(menu);
        }

        public void Quit()
        {
            GameManager.ForceQuitSong();
        }

        public void Restart()
        {
            if (GameManager.IsPractice && GlobalVariables.State.IsPractice)
            {
                PopMenu(false);
                GameManager.PracticeManager.ResetPractice();
                return;
            }

            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }
    }
}