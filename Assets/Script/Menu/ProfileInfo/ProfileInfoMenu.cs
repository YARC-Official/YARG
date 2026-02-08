using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

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

        public async void ShowQuickBind()
        {
            if (CurrentProfile is { GameMode: GameMode.FourLaneDrums or GameMode.ProKeys or
                GameMode.FiveLaneDrums or GameMode.EliteDrums})
            {
                var dialog = DialogManager.Instance.ShowFriendlyBindingDialog(CurrentProfile, CurrentProfile.GameMode);
                await dialog.WaitUntilClosed();
            }
            else
            {
                var dialog = DialogManager.Instance.ShowMessage("Unsupported Instrument Type",
                    "Quick binding is currently only supported for Drums and Keys.");
                await dialog.WaitUntilClosed();
            }
        }

        private void OnTabChanged(string tabId)
        {
            _overviewTab.SetActive(tabId == "overview");
            _editBindsTab.SetActive(tabId == "binds");
        }
    }
}