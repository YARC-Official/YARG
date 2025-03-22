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

        private static readonly HashSet<string> SourceCounter  = new();
        private static readonly HashSet<string> CharterCounter = new();
        private static readonly HashSet<string> GenreCounter   = new();
        public CategoryViewType(string primary, int songCount, SongEntry[] songsUnderCategory,
            Action clickAction = null)
        {
            _primary = primary;
            _songCount = songCount;
            _clickAction = clickAction;

            foreach (var song in songsUnderCategory)
            {
                SourceCounter.Add(song.Source);
                CharterCounter.Add(song.Charter);
                GenreCounter.Add(song.Genre);
            }

            SourceCountText = $"{SourceCounter.Count} sources";
            CharterCountText = $"{CharterCounter.Count} charters";
            GenreCountText = $"{GenreCounter.Count} genres";
            SourceCounter.Clear();
            CharterCounter.Clear();
            GenreCounter.Clear();
        }

        public CategoryViewType(string primary, int songCount, SongCategory[] songsUnderCategory)
        {
            _primary = primary;
            _songCount = songCount;

            foreach (var category in songsUnderCategory)
            {
                foreach (var song in category.Songs)
                {
                    SourceCounter.Add(song.Source);
                    CharterCounter.Add(song.Charter);
                    GenreCounter.Add(song.Genre);
                }
            }

            SourceCountText = $"{SourceCounter.Count} sources";
            CharterCountText = $"{CharterCounter.Count} charters";
            GenreCountText = $"{GenreCounter.Count} genres";
            SourceCounter.Clear();
            CharterCounter.Clear();
            GenreCounter.Clear();
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
    }
}