using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Career;
using YARG.Core.Input;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;

namespace YARG.Menu.Career
{
    public class CareerMenu : ListMenu<ViewType, CareerView>
    {
        protected override int ExtraListViewPadding => 10;

        private CareerBase _career;

        private string CareerName => _career?.Name ?? "Career not loaded. How?";
        private string CareerDescription => _career?.Description ?? "";

        [SerializeField]
        private TextMeshProUGUI _careerNameText;
        [SerializeField]
        private TextMeshProUGUI _careerDescriptionText;
        [SerializeField]
        private Texture2D _bgImage;
        [Space]
        [SerializeField]
        private GameObject _bgImageContainer;

        protected override void OnEnable()
        {
            base.OnEnable();

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new ()
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
        }

        protected override List<ViewType> CreateViewList()
        {
            var viewList = new List<ViewType>();

            if (_career == null)
            {
                // Not initialized yet, so return an empty list
                return viewList;
            }

            foreach (var tier in _career.Tiers)
            {
                viewList.Add(new TierViewType(tier));

                foreach (var song in tier.Songs)
                {
                    viewList.Add(new SongViewType(song));
                }
            }

            return viewList;
        }

        private void Back()
        {
            MenuManager.Instance.PopMenu();
        }

        public void Initialize(CareerBase career)
        {
            _career = career;
            RequestViewListUpdate();
        }
    }
}