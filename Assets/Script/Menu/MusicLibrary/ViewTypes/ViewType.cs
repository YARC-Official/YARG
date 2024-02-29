﻿using YARG.Menu.ListMenu;

namespace YARG.Menu.MusicLibrary
{
    public abstract class ViewType : BaseViewType
    {
        public virtual bool UseAsMadeFamousBy => false;

        public virtual bool ShowFavoriteButton => false;
        public virtual bool IsFavorited => false;

        public override string GetSecondaryText(bool selected) => string.Empty;
        public virtual string GetSideText(bool selected) => string.Empty;

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