using YARG.Career;

namespace YARG.Menu.Career
{
    public class TierViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        private string     _name;
        private string     _description;
        private string     _imagePath;
        private CareerTier _tier;

        public TierViewType(CareerTier tier)
        {
            _tier = tier;

            _name = tier.Name;
            _description = tier.Description;
            _imagePath = tier.BackgroundImage; // This actually needs to get the full path to which the career extra data was extracted
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_name, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(_description, TextType.Secondary, selected);
        }
    }
}