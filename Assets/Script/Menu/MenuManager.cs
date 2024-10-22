using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YARG.Menu
{
    public class MenuManager : MonoSingleton<MenuManager>
    {
        public enum Menu
        {
            None,
            MainMenu,
            MusicLibrary,
            DifficultySelect,
            Credits,
            ProfileList,
            ProfileInfo,
            History,
        }

        /// <summary>
        /// The values that <see cref="_lastOpenMenu"/> is allowed to be set to
        /// (not including <see cref="Menu.None"/>.
        /// </summary>
        private static readonly HashSet<Menu> _allowedLastOpenMenus = new()
        {
            Menu.MusicLibrary,
            Menu.History
        };

        /// <summary>
        /// The menu that was last open when the menu scene gets disabled.
        /// </summary>
        private static Menu _lastOpenMenu = Menu.None;

        private Dictionary<Menu, MenuObject> _menus;

        private readonly Stack<Menu> _openMenus = new();

        protected override void SingletonAwake()
        {
            // Convert to dictionary with "Menu" as key
            var children = GetComponentsInChildren<MenuObject>(true);
            _menus = children.ToDictionary(i => i.Menu, i => i);
        }

        private void Start()
        {
            // Always push the main menu
            PushMenu(Menu.MainMenu);

            if (_lastOpenMenu != Menu.None)
            {
                PushMenu(_lastOpenMenu);
            }
        }

        private void OnDisable()
        {
            _lastOpenMenu = Menu.None;

            // Set the last open menu to the first instance of the allowed menu
            // Loops from top to bottom
            foreach (var menu in _openMenus)
            {
                if (_allowedLastOpenMenus.Contains(menu))
                {
                    _lastOpenMenu = menu;
                    break;
                }
            }
        }

        public MenuObject PushMenu(Menu menu, bool setActiveImmediate = true)
        {
            bool hideOther;

            // Get the new one
            if (_menus.TryGetValue(menu, out var newMenu))
            {
                hideOther = newMenu.HideBelow;
            }
            else
            {
                throw new InvalidOperationException($"Failed to open menu {menu}.");
            }

            // Close the currently open one
            if (hideOther && _openMenus.TryPeek(out var currentMenuEnum) &&
                _menus.TryGetValue(currentMenuEnum, out var currentMenu))
            {
                currentMenu.gameObject.SetActive(false);
            }

            // Show the new one
            if (setActiveImmediate)
            {
                newMenu.gameObject.SetActive(true);
            }

            // ... and push it onto the stack
            _openMenus.Push(menu);

            return newMenu;
        }

        public void PopMenu()
        {
            //Don't pop the only remaining menu
            if (_openMenus.Count == 1)
            {
                return;
            }

            // Close the currently open one
            if (_openMenus.TryPop(out var currentMenuEnum) &&
                _menus.TryGetValue(currentMenuEnum, out var currentMenu))
            {
                currentMenu.gameObject.SetActive(false);
            }

            if (_openMenus.TryPeek(out var newMenuEnum) &&
                _menus.TryGetValue(newMenuEnum, out var newMenu))
            {
                newMenu.gameObject.SetActive(true);
            }
            else
            {
                throw new InvalidOperationException($"Failed to open menu {newMenuEnum}.");
            }
        }

        public void ReactivateCurrentMenu()
        {
            // Show the under one
            if (_openMenus.TryPeek(out var menu) && _menus.TryGetValue(menu, out var newMenu))
            {
                newMenu.gameObject.SetActive(false);
                newMenu.gameObject.SetActive(true);
            }
            else
            {
                throw new InvalidOperationException($"Failed to activate menu {menu}.");
            }
        }
    }
}