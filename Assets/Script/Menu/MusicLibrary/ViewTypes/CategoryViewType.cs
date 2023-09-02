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

        public override string PrimaryText { get; }
        public override string SideText { get; }

        public readonly string SourceCountText;
        public readonly string CharterCountText;
        public readonly string GenreCountText;

        public CategoryViewType(string primary, string side, IEnumerable<SongMetadata> songsUnderCategory)
        {
            PrimaryText = $"<color=white>{primary}</color>";
            SideText = side;

            SourceCountText = $"{CountOf(songsUnderCategory, i => i.Source)} sources";
            CharterCountText = $"{CountOf(songsUnderCategory, i => i.Charter)} charters";
            GenreCountText = $"{CountOf(songsUnderCategory, i => i.Genre)} genres";
        }

        public CategoryViewType(string primary, string side, SortedDictionary<string, List<SongMetadata>> songsUnderCategory)
        {
            PrimaryText = $"<color=white>{primary}</color>";
            SideText = side;

            int sources = 0;
            int charters = 0;
            int genres = 0;

            foreach (var n in songsUnderCategory)
            {
                sources += CountOf(n.Value, i => i.Source);
                charters += CountOf(n.Value, i => i.Charter);
                genres += CountOf(n.Value, i => i.Genre);
            }

            SourceCountText = $"{sources} sources";
            CharterCountText = $"{charters} charters";
            GenreCountText = $"{genres} genres";
        }

        private int CountOf(IEnumerable<SongMetadata> songs, Func<SongMetadata, SortString> selector)
        {
            return songs.Select(selector).Distinct().Count();
        }
    }
}