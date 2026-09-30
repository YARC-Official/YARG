using System.Collections.Generic;
using YARG.Menu.ListMenu;
using YARG.Song;

namespace YARG.Menu.Dialogs.Components
{
    public class DialogViewList : ListMenu<SongViewType,SongView>
    {
        protected override int ExtraListViewPadding => 0;

        private SongCategory[] _songCategories;

        protected override List<SongViewType> CreateViewList()
        {
            var viewList = new List<SongViewType>();

            _songCategories ??= SongContainer.GetSortedCategory(SortAttribute.Artist);

            foreach (var songCategory in _songCategories)
            {
                foreach (var song in songCategory.Songs)
                {
                    viewList.Add(new SongViewType(song));
                }
            }

            return viewList;
        }

        public void UpdateCategories(SongCategory[] songCategories)
        {
            _songCategories = songCategories;
            RequestViewListUpdate();
        }
    }
}