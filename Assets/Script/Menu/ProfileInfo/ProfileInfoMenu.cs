using UnityEngine;
using YARG.Core.Game;
using YARG.Menu.Navigation;

namespace YARG.Menu.ProfileInfo
{
    public class ProfileInfoMenu : MonoBehaviour
    {
        public YargProfile CurrentProfile { get; set; }

        [SerializeField]
        private HeaderTabs _tabs;

        [Space]
        [SerializeField]
        private GameObject _overviewTab;
        [SerializeField]
        private GameObject _editBindsTab;

        private void OnEnable()
        {
            _tabs.TabChanged += OnTabChanged;
            _tabs.SelectFirstTab();

            Navigator.Instance.PushScheme(NavigationScheme.EmptyWithMusicPlayer);
        }

        private void OnDisable()
        {
            _tabs.TabChanged -= OnTabChanged;

            Navigator.Instance.PopScheme();
        }

        private void OnTabChanged(string tabId)
        {
            _overviewTab.SetActive(tabId == "overview");
            _editBindsTab.SetActive(tabId == "binds");
        }
    }
}