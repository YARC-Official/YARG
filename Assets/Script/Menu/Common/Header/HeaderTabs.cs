using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Rendering;
using UnityEngine;
using YARG.Core.Input;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;

namespace YARG.Menu
{
    [RequireComponent(typeof(NavigationGroup))]
    public class HeaderTabs : MonoBehaviour
    {
        [Serializable]
        public struct TabInfo
        {
            public Sprite Icon;
            public string Id;

            // TODO: Localize this
            public string DisplayName;
        }

        [SerializeField]
        private GameObject _tabPrefab;
        [SerializeField]
        private List<TabInfo> _tabs;
        public List<TabInfo> Tabs
        {
            get => _tabs;
            set
            {
                _tabs = value;
                RefreshTabs();
            }
        }

        public string SelectedTabId { get; private set; }

        public NavigationScheme.Entry NavigateNextTab => new(MenuAction.Right, "Menu.Common.NextTab", () =>
        {
            _navigationGroup.SelectNext();
        });
        public NavigationScheme.Entry NavigatePreviousTab => new(MenuAction.Left, "Menu.Common.PreviousTab", () =>
        {
            _navigationGroup.SelectPrevious();
        });

        public event Action<string> TabChanged;

        private NavigationGroup _navigationGroup;

        private void Awake()
        {
            _navigationGroup = GetComponent<NavigationGroup>();
            _navigationGroup.SelectionChanged += OnSelectionChanged;
        }

        private void Start()
        {
            RefreshTabs();
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null || selected is not HeaderTab tab)
            {
                SelectedTabId = null;
                return;
            }

            SelectedTabId = tab.Id;
            TabChanged?.Invoke(SelectedTabId);
        }

        public void RefreshTabs()
        {
            // Skip if not initialized yet
            if (_navigationGroup == null) return;

            transform.DestroyChildren();
            _navigationGroup.ClearNavigatables();

            foreach (var tabInfo in _tabs)
            {
                var tab = Instantiate(_tabPrefab, transform);

                var tabComponent = tab.GetComponent<HeaderTab>();
                tabComponent.Init(tabInfo.Id, tabInfo.DisplayName, tabInfo.Icon);

                _navigationGroup.AddNavigatable(tabComponent);
            }

            _navigationGroup.SelectFirst();
        }

        public void SelectFirstTab()
        {
            if (_navigationGroup == null) return;

            _navigationGroup.SelectFirst();
        }

        public void SelectTabById(string id)
        {
            if (_navigationGroup == null) return;

            var index = _tabs.FindIndex(i => i.Id == id);

            if (index == -1)
            {
                _navigationGroup.SelectAt(null);
                SelectedTabId = null;
            }
            else
            {
                _navigationGroup.SelectAt(index);
                SelectedTabId = id;
            }
        }

        private void OnDestroy()
        {
            _navigationGroup.SelectionChanged -= OnSelectionChanged;
        }
    }
}