using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Input;

namespace YARG
{
    [CreateAssetMenu(fileName = "MenuIcons", menuName = "YARG/Menu Icons", order = 1)]
    public class MenuIconsObject : ScriptableObject
    {
        [Serializable]
        private struct MenuIcon
        {
            public MenuAction Action;
            public Sprite Sprite;
            public Color Color;
        }

        [SerializeField]
        private List<MenuIcon> _menuIcons;

        private MenuIcon? GetIconByAction(MenuAction action)
        {
            int index = _menuIcons.FindIndex(i => i.Action == action);
            return index == -1 ? null : _menuIcons[index];
        }

        public bool HasIcon(MenuAction action)
        {
            return GetIconByAction(action) != null;
        }

        public Sprite GetIcon(MenuAction action)
        {
            return GetIconByAction(action)?.Sprite;
        }

        public Color GetColor(MenuAction action)
        {
            return GetIconByAction(action)?.Color ?? Color.white;
        }
    }
}