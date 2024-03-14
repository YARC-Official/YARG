using YARG.Core.Game;
using YARG.Menu.ListMenu;

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

        public virtual void FavoriteClick()
        {
        }
    }
}