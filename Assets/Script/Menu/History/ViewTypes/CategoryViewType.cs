namespace YARG.Menu.History
{
    public class CategoryViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override bool UseFullContainer => false;

        private readonly string _headerText;

        public CategoryViewType(string headerText)
        {
            _headerText = headerText;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_headerText, TextType.Bright, selected);
        }

        public override string GetSecondaryText(bool selected) => string.Empty;
    }
}