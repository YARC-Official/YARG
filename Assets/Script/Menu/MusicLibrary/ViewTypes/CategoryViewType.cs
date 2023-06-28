using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Song;

namespace YARG.UI.MusicLibrary.ViewTypes
{
    public class CategoryViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override string PrimaryText => $"<color=white>{_primary}</color>";
        public override string SideText => _side;

        private string _primary;
        private string _side;

        public IEnumerable<SongEntry> SongsUnderCategory { get; private set; }

        public CategoryViewType(string primary, string side, IEnumerable<SongEntry> songsUnderCategory = null)
        {
            _primary = primary;
            _side = side;

            if (songsUnderCategory == null)
            {
                SongsUnderCategory = Enumerable.Empty<SongEntry>();
            }
            else
            {
                SongsUnderCategory = songsUnderCategory;
            }
        }

        public int CountOf<T>(Func<SongEntry, T> selector)
        {
            return SongsUnderCategory.Select(selector).Distinct().Count();
        }
    }
}