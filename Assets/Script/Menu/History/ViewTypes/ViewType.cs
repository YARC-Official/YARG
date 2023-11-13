using YARG.Core.Game;
using YARG.Menu.ListMenu;

namespace YARG.Menu.History
{
    public abstract class ViewType : BaseViewType
    {
        public struct GameInfo
        {
            public int BandScore;
            public StarAmount BandStars;
        }

        public abstract bool UseFullContainer { get; }

        public virtual void ViewClick()
        {

        }

        public virtual GameInfo? GetGameInfo()
        {
            return null;
        }
    }
}