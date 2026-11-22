using System;
using Cysharp.Text;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.MusicLibrary;
using YARG.Roguelike;
using LibraryViewType = YARG.Menu.MusicLibrary.ViewType;

namespace YARG.Menu.Roguelike
{
    // The roguelike menu is built from the Quick Play (music library) prefab and uses its row prefab,
    // so these rows derive from the music library's view types. Song rows subclass SongViewType so
    // they get the full Quick Play song row (source icon, best stars/score) and drive the sidebar.

    /// <summary>A header row in the Quick Play header style: a title and a detail line.</summary>
    public class RoguelikeHeaderViewType : LibraryViewType
    {
        private readonly string _title;
        private readonly string _detail;

        public override BackgroundType Background { get; }
        public override bool IsSelectable => false;
        public override bool UseWiderPrimaryText => true;
        public override string StableId => $"RoguelikeHeader:{_title}";

        public RoguelikeHeaderViewType(string title, string detail, BackgroundType background = BackgroundType.Category)
        {
            _title = title;
            _detail = detail;
            Background = background;
        }

        public override string GetPrimaryText(bool selected)
        {
            return TextColorer.StyleString(_title, MenuData.Colors.HeaderPrimary, 550);
        }

        public override string GetSecondaryText(bool selected)
        {
            return string.IsNullOrEmpty(_detail)
                ? string.Empty
                : TextColorer.StyleString(_detail, MenuData.Colors.HeaderSecondary, 500);
        }
    }

    /// <summary>A button row, e.g. "Start New Run".</summary>
    public class RoguelikeActionViewType : LibraryViewType
    {
        private readonly string _title;
        private readonly string _detail;
        private readonly Action _action;

        public override BackgroundType Background => BackgroundType.Button;
        public override string StableId => $"RoguelikeAction:{_title}";

        public RoguelikeActionViewType(string title, string detail, Action action)
        {
            _title = title;
            _detail = detail;
            _action = action;
        }

        public override string GetPrimaryText(bool selected) => FormatAs(_title, TextType.Bright, selected);
        public override string GetSecondaryText(bool selected) => FormatAs(_detail ?? string.Empty, TextType.Secondary, selected);

        public override void PrimaryButtonClick() => _action?.Invoke();
    }

    /// <summary>
    /// A Quick Play song row with the music-library-only actions (search by artist/source, favorites,
    /// playlists) switched off, since there's no music library behind it.
    /// </summary>
    public abstract class RoguelikeSongViewType : SongViewType
    {
        protected RoguelikeSongViewType(SongEntry song, string context)
            : base(null, song, context)
        {
        }

        public override FavoriteInfo GetFavoriteInfo() => new() { ShowFavoriteButton = false };

        public override void SecondaryTextClick() { }
        public override void IconClick() { }
        public override void FavoriteClick() { }
        public override void AddToPlaylist(YARG.Playlists.Playlist playlist) { }
        public override void RemoveFromPlaylist(YARG.Playlists.Playlist playlist) { }
    }

    /// <summary>One of the current round's song choices. Confirming it starts the song.</summary>
    public class RoguelikeChoiceViewType : RoguelikeSongViewType
    {
        private readonly int _rating;
        private readonly Action<SongEntry> _onPlay;

        public RoguelikeChoiceViewType(SongEntry song, int rating, Action<SongEntry> onPlay)
            : base(song, "roguelike")
        {
            _rating = rating;
            _onPlay = onPlay;
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(Localize.KeyFormat("Menu.Roguelike.ChoiceDetail", SongEntry.Artist.ToString(), _rating),
                TextType.Secondary, selected);
        }

        public override void PrimaryButtonClick() => _onPlay?.Invoke(SongEntry);
    }

    /// <summary>A round already played this run: the song and the stars it earned in the run.</summary>
    public class RoguelikeHistoryViewType : RoguelikeSongViewType
    {
        private readonly RoguelikeRoundResult _result;

        public RoguelikeHistoryViewType(RoguelikeRoundResult result, SongEntry song)
            : base(song, $"roguelike-history-{result.Round}-{result.Attempt}")
        {
            _result = result;
        }

        public override string GetSecondaryText(bool selected)
        {
            var key = _result.Passed ? "Menu.Roguelike.HistoryPassed" : "Menu.Roguelike.HistoryFailed";
            return FormatAs(Localize.KeyFormat(key, _result.Round + 1, _result.PlayedInstrument.ToLocalizedName()),
                TextType.Secondary, selected);
        }

        // Show what this round earned, not the player's all-time best on the song
        public override StarAmount? GetStarAmount() => _result.Failed ? StarAmount.None : (StarAmount) _result.Stars;
        public override ScoreInfo? GetScoreInfo() => null;
        public override string GetSideText(bool selected) => string.Empty;

        public override void PrimaryButtonClick() { }
    }
}
