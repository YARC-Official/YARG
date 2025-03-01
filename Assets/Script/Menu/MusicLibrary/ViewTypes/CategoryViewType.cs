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

        public readonly string SourceCountText;
        public readonly string CharterCountText;
        public readonly string GenreCountText;

        private readonly string _primary;

        private readonly int _songCount;
        private readonly Action _clickAction;

        public CategoryViewType(string primary, int songCount, SongEntry[] songsUnderCategory,
            Action clickAction = null)
        {
            _primary = primary;
            _songCount = songCount;
            _clickAction = clickAction;

            SourceCountText = $"{CountOf(songsUnderCategory, i => i.Source.SortStr)} sources";
            CharterCountText = $"{CountOf(songsUnderCategory, i => i.Charter.SortStr)} charters";
            GenreCountText = $"{CountOf(songsUnderCategory, i => i.Genre.SortStr)} genres";
        }

        public CategoryViewType(string primary, int songCount, SongCategory[] songsUnderCategory)
        {
            _primary = primary;
            _songCount = songCount;

            int sources = 0;
            int charters = 0;
            int genres = 0;

            foreach (var n in songsUnderCategory)
            {
                sources += CountOf(n.Songs, i => i.Source);
                charters += CountOf(n.Songs, i => i.Charter);
                genres += CountOf(n.Songs, i => i.Genre);
            }

            SourceCountText = $"{sources} sources";
            CharterCountText = $"{charters} charters";
            GenreCountText = $"{genres} genres";
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_primary, TextType.Bright, selected);
        }

        public override string GetSideText(bool selected)
        {
            return CreateSongCountString(_songCount);
        }

        public override void PrimaryButtonClick()
        {
            _clickAction?.Invoke();
        }

        private static int CountOf(SongEntry[] songs, Func<SongEntry, string> selector)
        {
            var set = new HashSet<string>();
            foreach (var song in songs)
            {
                set.Add(selector(song));
            }
            return set.Count;
        }
    }
}