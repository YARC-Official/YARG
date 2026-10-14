using System;
using YARG.Menu.ListMenu;

namespace YARG.Menu.Dialogs.Components
{
    public class SongView : ViewObject<SongViewType>
    {
        private static readonly Type[] MarqueeScrollTypes = { typeof(SongViewType) };

        protected override bool   AllowMarqueeScroll        => true;
        protected override Type[] AllowedMarqueeScrollTypes => MarqueeScrollTypes;

        public void OnClick()
        {
            if (Showing)
            {
                ViewType.ViewClick();
            }
        }
    }
}