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

        private OverviewTab _overview;

        private void OnEnable()
        {
            _overview ??= GetComponentInChildren<OverviewTab>(true);

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
            Navigator.Instance.PopScheme();
        }
    }
}
