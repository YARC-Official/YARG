using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Input;

namespace YARG.Menu.Data
{
    [CreateAssetMenu(fileName = "NavigationIcons", menuName = "YARG/Navigation Icons")]
    public class NavigationIcons : ScriptableObject
    {
        [Serializable]
        private struct NavigationIcon
        {
            public MenuAction Action;
            public Sprite Sprite;
            public Color Color;
        }

        [SerializeField]
        private List<NavigationIcon> _menuIcons;

        private NavigationIcon? GetIconByAction(MenuAction action)
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