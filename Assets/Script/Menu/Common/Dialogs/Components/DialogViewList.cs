using System;
using System.Collections.Generic;
using YARG.Core.Song;
using YARG.Menu.ListMenu;
using YARG.Song;

namespace YARG.Menu.Dialogs.Components
{
    public class DialogViewList : ListMenu<SongViewType,SongView>
    {
        protected override int ExtraListViewPadding => 4;

        private SongCategory[] _songCategories;

        public event Action<SongEntry> SongSelected;

        private void OnSongClicked(SongEntry song)
        {
            SongSelected?.Invoke(song);
        }

        protected override List<SongViewType> CreateViewList()
        {
            var viewList = new List<SongViewType>();

            _songCategories ??= SongContainer.GetSortedCategory(SortAttribute.Artist);

            foreach (var songCategory in _songCategories)
            {
                foreach (var song in songCategory.Songs)
                {
                    viewList.Add(new SongViewType(song, OnSongClicked));
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