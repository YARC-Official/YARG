using YARG.Career;
using YARG.Menu.ListMenu;

namespace YARG.Menu.Career
{
    public abstract class ViewType : BaseViewType
    {
        public struct CareerInfo
        {
            public CareerBase Career;
            public bool       Unlocked;
            public bool       Completed;
        }

        public virtual void ViewClick()
        {

        }
    }
}