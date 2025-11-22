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
            OnlineMultiplayer,
            LobbyBrowser,
            LobbyRoom,
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
        
        public int MenuStackCount => _openMenus.Count;
        public Menu CurrentMenu => _openMenus.Count > 0 ? _openMenus.Peek() : Menu.None;
        public bool IsMenuInStack(Menu menu) => _openMenus.Contains(menu);

        protected override void SingletonAwake()
        {
            // Convert to dictionary with "Menu" as key
            var children = GetComponentsInChildren<MenuObject>(true);
            _menus = children.ToDictionary(i => i.Menu, i => i);
            
            // Log all registered menus for debugging
            Debug.Log($"[MenuManager] Registered {_menus.Count} menus: {string.Join(", ", _menus.Keys)}");
            
            // Check for missing menus
            var allMenus = System.Enum.GetValues(typeof(Menu)).Cast<Menu>().Where(m => m != Menu.None);
            var missingMenus = allMenus.Except(_menus.Keys).ToList();
            if (missingMenus.Any())
            {
                Debug.LogWarning($"[MenuManager] Missing menu objects for: {string.Join(", ", missingMenus)}");
            }
        }

        private void Start()
        {
            // Always push the main menu
            PushMenu(Menu.MainMenu);

            // Check if there's a menu navigation target from multiplayer (host quitting song)
            var targetMenus = Networking.YargNetworkManager.GetAndClearMenuNavigationAfterSceneLoad();
            if (targetMenus.Count > 0)
            {
                Debug.Log($"[MenuManager] Navigating to {string.Join(" > ", targetMenus)} after scene load (from multiplayer)");
                foreach (var menu in targetMenus)
                {
                    PushMenu(menu);
                }
            }
            else if (_lastOpenMenu != Menu.None)
            {
                // Only restore last open menu if we're NOT coming from multiplayer
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
                Debug.LogError($"[MenuManager] Failed to open menu {menu}. Available menus: {string.Join(", ", _menus.Keys)}");
                Debug.LogError($"[MenuManager] Make sure a GameObject with MenuObject component exists in the scene hierarchy with Menu={menu}");
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
            Debug.Log($"[MenuManager] PopMenu called - Stack count: {_openMenus.Count}, Stack: [{string.Join(" > ", _openMenus.Reverse())}]");
            
            //Don't pop the only remaining menu
            if (_openMenus.Count == 1)
            {
                Debug.LogWarning($"[MenuManager] PopMenu blocked - only 1 menu in stack: {_openMenus.Peek()}");
                return;
            }

            // Close the currently open one
            if (_openMenus.TryPop(out var currentMenuEnum) &&
                _menus.TryGetValue(currentMenuEnum, out var currentMenu))
            {
                Debug.Log($"[MenuManager] Closing menu: {currentMenuEnum}");
                currentMenu.gameObject.SetActive(false);
            }

            if (_openMenus.TryPeek(out var newMenuEnum) &&
                _menus.TryGetValue(newMenuEnum, out var newMenu))
            {
                Debug.Log($"[MenuManager] Opening menu: {newMenuEnum}");
                newMenu.gameObject.SetActive(true);
            }
            else
            {
                throw new InvalidOperationException($"Failed to open menu {newMenuEnum}.");
            }
            
            Debug.Log($"[MenuManager] PopMenu complete - Stack count: {_openMenus.Count}");
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