using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        private void Awake()
        {
            // Convert to dictionary with "Menu" as key
            var children = GetComponentsInChildren<PauseMenuObject>(true);
            _menus = children.ToDictionary(i => i.Menu, i => i);
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
            GlobalVariables.AudioManager.UnloadSong();
            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }
    }
}