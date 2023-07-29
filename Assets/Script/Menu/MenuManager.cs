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
            Profiles,
            EditProfile,
            Replays,
        }

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
            PushMenu(Menu.MainMenu);
        }

        public void PushMenu(Menu menu)
        {
            bool hideOther;

            // Show the new one
            if (_menus.TryGetValue(menu, out var newMenu))
            {
                newMenu.gameObject.SetActive(true);
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

            // ... and push it onto the stack
            _openMenus.Push(menu);
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
            var menu = _openMenus.Peek();

            // Show the new one
            if (_menus.TryGetValue(menu, out var newMenu))
            {
                newMenu.gameObject.SetActive(true);
            }
            else
            {
                throw new InvalidOperationException($"Failed to open menu {menu}.");
            }
        }
    }
}