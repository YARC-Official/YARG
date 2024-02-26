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

        public virtual void Confirm()
        {

        }

        public virtual void Shortcut1()
        {

        }

        public virtual void Shortcut2()
        {

        }

        public virtual void Shortcut3()
        {

        }

        public virtual GameInfo? GetGameInfo()
        {
            return null;
        }
    }
}