using YARG.Career;

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

        public override void ViewClick()
        {
            var menu = MenuManager.Instance.PushMenu(MenuManager.Menu.Career, false);
            if (menu.TryGetComponent<CareerMenu>(out var careerMenu))
            {
                careerMenu.Initialize(_career);
            }
            menu.gameObject.SetActive(true);
        }
    }
}
