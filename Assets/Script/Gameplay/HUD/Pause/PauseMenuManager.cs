using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Song;

namespace YARG.Gameplay.HUD
{
    public class PauseMenuManager : MonoBehaviour
    {
        public enum Menu
        {
            None,
            QuickPlayPause,
            PracticePause,
            SelectSections,
        }

        private Dictionary<Menu, PauseMenuObject> _menus;

        private readonly Stack<Menu> _openMenus = new();

        [SerializeField]
        private GameManager _gameManager;

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

        private void Awake()
        {
            // Convert to dictionary with "Menu" as key
            var children = GetComponentsInChildren<PauseMenuObject>(true);
            _menus = children.ToDictionary(i => i.Menu, i => i);
        }

        private async void Start()
        {
            // Set text info
            _albumText.text = _gameManager.Song.Album;
            _songText.text = _gameManager.Song.Name;
            _artistText.text = _gameManager.Song.Artist;
            _sourceText.text = SongSources.SourceToGameName(_gameManager.Song.Source);

            // Set source icon
            _sourceIcon.sprite = await SongSources.SourceToIcon(_gameManager.Song.Source);
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

            foreach(var i in _openMenus)
            {
                Debug.Log(i);
            }

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
                _gameManager.Resume();
            }
        }

        public void OpenMenu(Menu menu)
        {
            PopMenu(false);
            PushMenu(menu);
        }

        public void Quit()
        {
            _gameManager.QuitSong();
        }

        public void Restart()
        {
            if (_gameManager.IsPractice)
            {
                _gameManager.PracticeManager.ResetPractice();
                PopMenu();
                return;
            }

            GlobalVariables.AudioManager.UnloadSong();
            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }
    }
}