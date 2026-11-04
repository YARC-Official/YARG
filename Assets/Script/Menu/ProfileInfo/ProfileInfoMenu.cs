using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.ProfileInfo.Overview;

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

        private OverviewTab _overview;

        private void OnEnable()
        {
            _overview ??= _overviewTab.GetComponent<OverviewTab>();
            _tabs.TabChanged += OnTabChanged;
            _tabs.SelectFirstTab();

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                    context => _overview.NavigateUp(context.IsRepeat)),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down",
                    context => _overview.NavigateDown(context.IsRepeat)),
                new NavigationScheme.Entry(MenuAction.Yellow, "Menu.ProfileInfo.SwapSide",
                    _overview.SwapSide),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                    () => MenuManager.Instance.PopMenu(), hide: true)
            }, true));
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
