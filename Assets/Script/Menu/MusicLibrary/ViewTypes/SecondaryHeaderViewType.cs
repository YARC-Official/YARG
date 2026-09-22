using Cysharp.Text;
using YARG.Helpers;
using YARG.Menu.Data;

namespace YARG.Menu.MusicLibrary
{
    /// <summary>
    /// A visual grouping label within a primary sort category. Secondary headers
    /// are deliberately skipped by list navigation.
    /// </summary>
    public sealed class SecondaryHeaderViewType : ViewType
    {
        private readonly string _text;
        private readonly int _songCount;

        public override BackgroundType Background => BackgroundType.SecondaryHeader;
        public override bool IsSelectable => false;
        public override bool UseWiderPrimaryText => true;
        public override string StableId => $"SecondaryHeader:{_text}";
        public int TotalStarsCount { get; set; }

        public SecondaryHeaderViewType(string text, int songCount)
        {
            _text = text;
            _songCount = songCount;
        }

        public override string GetPrimaryText(bool selected)
        {
            return TextColorer.StyleString(_text, MenuData.Colors.HeaderPrimary, 550);
        }

        public override string GetSecondaryText(bool selected)
        {
            var count = TextColorer.StyleString(
                ZString.Format("{0:N0}", _songCount),
                MenuData.Colors.HeaderSecondary,
                500);
            var label = TextColorer.StyleString(
                _songCount == 1 ? "SONG" : "SONGS",
                MenuData.Colors.HeaderTertiary,
                550);
            return ZString.Concat(count, " ", label);
        }

        public override string GetSideText(bool selected)
        {
            var obtainedStars = TextColorer.StyleString(
                ZString.Format("{0}", TotalStarsCount),
                MenuData.Colors.HeaderSecondary,
                600);

            var totalStars = TextColorer.StyleString(
                ZString.Format(" / {0}", _songCount * 5),
                MenuData.Colors.HeaderTertiary,
                550);

            return ZString.Concat(obtainedStars, totalStars);
        }
    }
}
