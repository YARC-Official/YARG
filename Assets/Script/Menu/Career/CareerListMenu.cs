using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Settings.Customization;

namespace YARG.Menu.Career
{
    public class CareerListMenu : ListMenu<ViewType, CareerView>
    {
        protected override int ExtraListViewPadding => 10;

        protected override void OnEnable()
        {
            base.OnEnable();

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                    ctx => {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex--;
                    }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down",
                    ctx => {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex++;
                    }),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                    () => CurrentSelection?.ViewClick()),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                    Back, hide: true),
            }, false));

            RequestViewListUpdate();
        }

        protected override List<ViewType> CreateViewList()
        {
            var viewList = new List<ViewType>();

            if (CustomContentManager.Careers == null)
            {
                return viewList;
            }

            foreach (var career in CustomContentManager.Careers.DefaultPresets)
            {
                viewList.Add(new CareerViewType(career));
            }

            foreach (var career in CustomContentManager.Careers.CustomPresets)
            {
                viewList.Add(new CareerViewType(career));
            }

            return viewList;
        }

        private void Back()
        {
            MenuManager.Instance.PopMenu();
        }
    }
}
