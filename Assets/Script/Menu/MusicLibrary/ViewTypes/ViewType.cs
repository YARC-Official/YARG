using Cysharp.Text;
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
        public struct FavoriteInfo
        {
            public bool ShowFavoriteButton;
            public bool IsFavorited;
        }

        public virtual bool UseAsMadeFamousBy => false;

        public virtual bool UseWiderPrimaryText => false;

        public override string GetSecondaryText(bool selected) => string.Empty;
        public virtual string GetSideText(bool selected) => string.Empty;

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
                MenuData.Colors.PrimaryText,
                500);

            var songs = TextColorer.StyleString(
                songCount == 1 ? "SONG" : "SONGS",
                MenuData.Colors.PrimaryText.WithAlpha(0.5f),
                500);

            return ZString.Concat(count, " ", songs);
        }
    }
}