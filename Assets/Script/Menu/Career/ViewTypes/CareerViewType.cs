using UnityEngine;
using YARG.Career;
using YARG.Menu.Persistent;
using YARG.Song;

namespace YARG.Menu.Career
{
    public class CareerViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        private readonly CareerBase _career;
        public CareerBase Career => _career;

        public CareerViewType(CareerBase career)
        {
            _career = career;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_career.Name, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(_career.Description ?? "", TextType.Secondary, selected);
        }

        #nullable enable
        public override Sprite? GetIcon()
        #nullable restore
        {
            var source = string.IsNullOrEmpty(_career.Source) ? "unknown" : _career.Source;
            return SongSources.SourceToIcon(source);
        }

        public override void ViewClick()
        {
            var menu = MenuManager.Instance.PushMenu(MenuManager.Menu.Career, false);
            if (menu.TryGetComponent<CareerMenu>(out var careerMenu))
            {
                // The save and its progress are resolved once, here, and handed over for the menu to
                // cache. Browsing the list afterwards never reads the database again.
                careerMenu.Initialize(_career, CareerMenu.LoadProgress(_career));
                menu.gameObject.SetActive(true);
            }
            else
            {
                ToastManager.ToastError("Failed to initialize career.");
            }
        }
    }
}
