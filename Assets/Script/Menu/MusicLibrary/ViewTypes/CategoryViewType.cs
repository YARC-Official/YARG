using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;
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

        public CategoryViewType(string primary, int songCount, IReadOnlyList<SongEntry> songsUnderCategory)
        {
            _primary = primary;
            _songCount = songCount;

            SourceCountText = $"{CountOf(songsUnderCategory, i => i.Source)} sources";
            CharterCountText = $"{CountOf(songsUnderCategory, i => i.Charter)} charters";
            GenreCountText = $"{CountOf(songsUnderCategory, i => i.Genre)} genres";
        }

        public CategoryViewType(string primary, int songCount, IReadOnlyList<SongCategory> songsUnderCategory)
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
            var count = TextColorer.FormatString(
                _songCount.ToString("N0"),
                MenuData.Colors.PrimaryText,
                500);

            var songs = TextColorer.FormatString(
                _songCount == 1 ? "SONG" : "SONGS",
                MenuData.Colors.PrimaryText.WithAlpha(0.5f),
                500);

            return $"{count} {songs}";
        }

        private static int CountOf(IEnumerable<SongEntry> songs, Func<SongEntry, SortString> selector)
        {
            return songs.Select(selector).Distinct().Count();
        }
    }
}