using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Menu.Navigation;

namespace YARG.Menu
{
    [RequireComponent(typeof(NavigationGroup))]
    public class HeaderTabs : MonoBehaviour
    {
        [Serializable]
        private struct TabInfo
        {
            public Sprite Icon;
            public string Id;

            // TODO: Localize this
            public string DisplayName;
        }

        public string SelectedTabId { get; set; }

        [SerializeField]
        private List<TabInfo> _tabs;

        [SerializeField]
        private GameObject _tabPrefab;

        private List<GameObject> _tabsObjects = new();

        private void Start()
        {
            var navGroup = GetComponent<NavigationGroup>();

            foreach (var tabInfo in _tabs)
            {
                var tab = Instantiate(_tabPrefab, transform);

                var tabComponent = tab.GetComponent<HeaderTab>();
                tabComponent.Init(this, tabInfo.Id, tabInfo.DisplayName, tabInfo.Icon);

                _tabsObjects.Add(tab);
                navGroup.AddNavigatable(tabComponent);
            }

            navGroup.SelectFirst();
        }
    }
}