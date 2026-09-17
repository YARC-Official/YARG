using Cysharp.Text;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;
using YARG.Menu.ListMenu;
using YARG.Playlists;

namespace YARG.Menu.MusicLibrary
{
    public abstract class ViewType : BaseViewType
    {
        public abstract string StableId { get; }

        public struct FavoriteInfo
        {
            public bool ShowFavoriteButton;
            public bool IsFavorited;
        }

        public struct ScoreInfo
        {
            public float      Percent;
            public int        Score;
            public Difficulty Difficulty;
            public Instrument Instrument;
            public bool       IsFc;
        }

        public virtual bool UseAsMadeFamousBy => false;

        public virtual bool UseWiderPrimaryText => false;

        public override string GetSecondaryText(bool selected) => string.Empty;
        public virtual string GetSideText(bool selected) => string.Empty;
        public virtual ScoreInfo? GetScoreInfo() => null;

        public virtual StarAmount? GetStarAmount() => null;

        public virtual FavoriteInfo GetFavoriteInfo()
        {
            return new FavoriteInfo
            {
                ShowFavoriteButton = false,
                IsFavorited = false
            };
        }

        public virtual void SecondaryTextClick()
        {
        }

        public virtual void PrimaryButtonClick()
        {
        }

        public override void IconClick()
        {
            PrimaryButtonClick();
        }

        public virtual void FavoriteClick()
        {
        }

        public virtual void AddToPlaylist(Playlist playlist)
        {
        }

        public virtual void RemoveFromPlaylist(Playlist playlist)
        {
        }

        protected static string CreateSongCountString(int songCount)
        {
            var count = TextColorer.StyleString(
                ZString.Format("{0:N0}", songCount),
                MenuData.Colors.HeaderSecondary,
                500);

            var songs = TextColorer.StyleString(
                songCount == 1 ? "SONG" : "SONGS",
                MenuData.Colors.HeaderTertiary,
                600);

            return ZString.Concat(count, " ", songs);
        }
    }

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
