using System.Collections.Generic;
using Cysharp.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Menu.Data;

namespace YARG.Menu.MusicLibrary
{
    public class SortHeaderViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Category;

        public override bool UseWiderPrimaryText => true;

        public readonly  string HeaderText;
        public readonly  string ShortcutName;
        public readonly  string SourceCountText;
        public readonly  string CharterCountText;
        public readonly  string GenreCountText;
        private readonly int    _songCount;
        public           int    TotalStarsCount { get; set; }

        private static readonly HashSet<string> SourceCounter  = new();
        private static readonly HashSet<string> CharterCounter = new();
        private static readonly HashSet<string> GenreCounter   = new();

        public SortHeaderViewType(string headerText, int songCount, string shortcutName, SongEntry[] songsUnderCategory)
        {
            HeaderText = headerText;
            _songCount = songCount;

            ShortcutName = shortcutName;

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

        public override string GetPrimaryText(bool selected)
        {
            if (selected)
            {
                return TextColorer.StyleString(HeaderText, MenuData.Colors.HeaderSelectedPrimary, 600);
            }
            else
            {
                return TextColorer.StyleString(HeaderText, MenuData.Colors.HeaderPrimary, 600);
            }
        }

        public override string GetSecondaryText(bool selected)
        {
            return CreateSongCountString(_songCount);
        }

        public override string GetSideText(bool selected)
        {
            var obtainedStars = TextColorer.StyleString(
                ZString.Format("{0}", TotalStarsCount),
                MenuData.Colors.HeaderSecondary,
                700);

            var totalStars = TextColorer.StyleString(
                ZString.Format(" / {0}", _songCount * 5),
                MenuData.Colors.HeaderTertiary,
                600);

            return ZString.Concat(obtainedStars, totalStars);
        }


#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            return Addressables.LoadAssetAsync<Sprite>("MusicLibraryUpIcon").WaitForCompletion();
        }
    }
}