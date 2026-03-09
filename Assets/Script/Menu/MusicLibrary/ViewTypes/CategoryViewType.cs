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
        public override string StableId => _stableId;

        public readonly string SourceCountText;
        public readonly string CharterCountText;
        public readonly string GenreCountText;
        public readonly string SubgenreCountText;

        protected readonly string Primary;

        protected readonly int SongCount;
        private readonly Action _clickAction;
        private readonly string _stableId;

        private static readonly HashSet<string> SourceCounter  = new();
        private static readonly HashSet<string> CharterCounter = new();
        private static readonly HashSet<string> GenreCounter   = new();
        private static readonly HashSet<string> SubgenreCounter = new();
        public CategoryViewType(string primary, int songCount, SongEntry[] songsUnderCategory,
            Action clickAction = null, string stableId = null)
        {
            Primary = primary;
            SongCount = songCount;
            _clickAction = clickAction;
            _stableId = stableId ?? $"Category:{primary}";

            foreach (var song in songsUnderCategory)
            {
                SourceCounter.Add(song.Source);
                CharterCounter.Add(song.Charter);
                GenreCounter.Add(song.Genre);
                if (!string.IsNullOrEmpty(song.Subgenre))
                {
                    SubgenreCounter.Add(song.Subgenre);
                }
            }

            SourceCountText = Pluralize("Source", SourceCounter.Count);
            CharterCountText = Pluralize("Charter", CharterCounter.Count);
            GenreCountText = Pluralize("Genre", GenreCounter.Count);
            SubgenreCountText = Pluralize("Subgenre", SubgenreCounter.Count);
            SourceCounter.Clear();
            CharterCounter.Clear();
            GenreCounter.Clear();
            SubgenreCounter.Clear();
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(Primary, TextType.Bright, selected);
        }

        public override string GetSideText(bool selected)
        {
            return CreateSongCountString(SongCount);
        }

        private static string Pluralize(string item, int count)
        {
            return $"{count} {item}{(count == 1 ? "" : "s")}";
        }

        public override void PrimaryButtonClick()
        {
            _clickAction?.Invoke();
        }
    }
}