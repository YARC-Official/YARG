using YARG.Menu.ListMenu;

namespace YARG.Menu.Dialogs.Components
{
    public class SongView : ViewObject<SongViewType>
    {
        public void OnClick()
        {
            if (Showing)
            {
                ViewType.ViewClick();
            }
        }
    }
}