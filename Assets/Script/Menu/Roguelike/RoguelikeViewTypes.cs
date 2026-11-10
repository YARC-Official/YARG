using System;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Roguelike;
using CareerViewType = YARG.Menu.Career.ViewType;

namespace YARG.Menu.Roguelike
{
    // The roguelike menu reuses the career menu's row prefab (SongView), which lays itself out from
    // CareerInfo. So these rows derive from the career ViewType and report a matching row kind:
    // TierHeader for headers and buttons (title + detail line), Song for songs (title, artist, stars).

    /// <summary>A header or info row: a title and a detail line, with no action.</summary>
    public class RoguelikeHeaderViewType : CareerViewType
    {
        private readonly string _title;
        private readonly string _detail;

        public override BackgroundType Background { get; }
        public override bool IsClickable => false;

        public RoguelikeHeaderViewType(string title, string detail, BackgroundType background = BackgroundType.Category)
        {
            _title = title;
            _detail = detail;
            Background = background;
        }

        public override CareerInfo? GetCareerInfo()
        {
            return new CareerInfo { Kind = CareerRowKind.TierHeader, Unlocked = true };
        }

        public override string GetPrimaryText(bool selected) => FormatAs(_title, TextType.Primary, selected);
        public override string GetSecondaryText(bool selected) => FormatAs(_detail ?? string.Empty, TextType.Secondary, selected);
    }

    /// <summary>A row that runs an action when confirmed, e.g. "Start new run".</summary>
    public class RoguelikeActionViewType : RoguelikeHeaderViewType
    {
        private readonly Action _action;

        public override bool IsClickable => true;

        public RoguelikeActionViewType(string title, string detail, Action action)
            : base(title, detail, BackgroundType.Button)
        {
            _action = action;
        }

        public override void ViewClick() => _action?.Invoke();
    }

    /// <summary>One of the current round's song choices. Confirming it starts the song.</summary>
    public class RoguelikeChoiceViewType : CareerViewType
    {
        private readonly SongEntry _song;
        private readonly int       _rating;
        private readonly Action<SongEntry> _onPlay;

        public override BackgroundType Background => BackgroundType.Normal;

        public RoguelikeChoiceViewType(SongEntry song, int rating, Action<SongEntry> onPlay)
        {
            _song = song;
            _rating = rating;
            _onPlay = onPlay;
        }

        public override CareerInfo? GetCareerInfo()
        {
            return new CareerInfo
            {
                Kind = CareerRowKind.Song,
                Unlocked = true,
                Artist = _song.Artist.ToString(),
                Album = _song.Album.ToString(),
            };
        }

        public override string GetPrimaryText(bool selected) => FormatAs(_song.Name, TextType.Primary, selected);

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(Localize.KeyFormat("Menu.Roguelike.ChoiceDetail", _song.Artist.ToString(), _rating),
                TextType.Secondary, selected);
        }

        public override void ViewClick() => _onPlay?.Invoke(_song);
    }

    /// <summary>A round already played this run: the song and the stars it earned.</summary>
    public class RoguelikeHistoryViewType : CareerViewType
    {
        private readonly RoguelikeRoundResult _result;
        private readonly SongEntry            _song;

        public override BackgroundType Background => BackgroundType.Normal;
        public override bool IsClickable => false;

        public RoguelikeHistoryViewType(RoguelikeRoundResult result, SongEntry song)
        {
            _result = result;
            _song = song;
        }

        public override CareerInfo? GetCareerInfo()
        {
            return new CareerInfo
            {
                Kind = CareerRowKind.Song,
                Unlocked = true,
                Completed = _result.Passed,
                BestStars = _result.Failed ? 0 : _result.Stars,
            };
        }

        public override string GetPrimaryText(bool selected)
        {
            var name = _song?.Name ?? Localize.Key("Menu.Career.SongMissing");
            return FormatAs(name, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            var key = _result.Passed ? "Menu.Roguelike.HistoryPassed" : "Menu.Roguelike.HistoryFailed";
            return FormatAs(Localize.KeyFormat(key, _result.Round + 1, _result.PlayedInstrument.ToLocalizedName()),
                TextType.Secondary, selected);
        }
    }
}
