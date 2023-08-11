using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Song;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class CategoryViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override string PrimaryText => $"<color=white>{_primary}</color>";
        public override string SideText => _side;

        private string _primary;
        private string _side;

        public IEnumerable<SongMetadata> SongsUnderCategory { get; private set; }

        public CategoryViewType(string primary, string side, IEnumerable<SongMetadata> songsUnderCategory = null)
        {
            _primary = primary;
            _side = side;

            if (songsUnderCategory == null)
            {
                SongsUnderCategory = Enumerable.Empty<SongMetadata>();
            }
            else
            {
                SongsUnderCategory = songsUnderCategory;
            }
        }

        public int CountOf<T>(Func<SongMetadata, T> selector)
        {
            return SongsUnderCategory.Select(selector).Distinct().Count();
        }
    }
}