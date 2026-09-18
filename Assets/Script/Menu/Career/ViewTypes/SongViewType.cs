using System;
using YARG.Career;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Localization;

namespace YARG.Menu.Career
{
    public class SongViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;
        public bool HasSongEntry => _songEntry != null;

        private readonly CareerSong _song;
        private readonly SongEntry  _songEntry;
        private readonly Guid  _careerId;
        private readonly Guid  _tierId;
        private readonly int   _tierIndex;
        private readonly int   _songIndex;
        private readonly CareerEvaluation.SongResult _result;
        private readonly Action _onPlay;

        /// <param name="result">
        /// This song's row from the cached <see cref="CareerEvaluation"/>, or null when progress is
        /// not available (no completion is shown then).
        /// </param>
        /// <param name="onPlay">
        /// Invoked when the song is actually launched, so the menu knows its cached progress is stale
        /// and only reloads it when something could have changed.
        /// </param>
        public SongViewType(CareerSong song, Guid careerId, Guid tierId, int tierIndex, int songIndex,
            CareerEvaluation.SongResult result = null, Action onPlay = null)
        {
            _song = song;
            _songEntry = song.SongEntry;
            _careerId = careerId;
            _tierId = tierId;
            _tierIndex = tierIndex;
            _songIndex = songIndex;
            _result = result;
            _onPlay = onPlay;
        }

        public override CareerInfo? GetCareerInfo()
        {
            return new CareerInfo
            {
                Kind        = CareerRowKind.Song,
                Unlocked    = true, // songs are only listed once their tier is unlocked
                Completed   = _result?.Completed ?? false,
                BestStars   = _result?.BestStars ?? 0,
                Artist      = _songEntry?.Artist.ToString() ?? string.Empty,
                Album       = _songEntry?.Album.ToString() ?? string.Empty,
            };
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_songEntry?.Name ?? Localize.Key("Menu.Career.SongMissing"),
                TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(DetailLine(), TextType.Secondary, selected);
        }

        /// <summary>
        /// The artist/album line shown under every song, played or not (D11).
        /// </summary>
        private string DetailLine()
        {
            if (_songEntry is null)
            {
                // Content fallback for songs that are not in the library.
                return _song.Description ?? string.Empty;
            }

            var artist = _songEntry.Artist.ToString();
            var album = _songEntry.Album.ToString();

            return string.IsNullOrEmpty(album)
                ? artist
                : Localize.KeyFormat("Menu.Career.SongArtistAlbum", artist, album);
        }

        // Start the song in career mode: carry the career context through to gameplay so the score
        // can be linked to a committed career completion.
        public override void ViewClick()
        {
            if (_songEntry is null)
            {
                YargLogger.LogError("Career: cannot start song, it was not found in the library.");
                return;
            }

            GlobalVariables.State.CurrentSong = _songEntry;
            GlobalVariables.State.ShowSongs.Clear();
            GlobalVariables.State.ShowSongs.Add(_songEntry);
            GlobalVariables.State.PlayingAShow = false;
            GlobalVariables.State.CurrentCareer = new CareerContext
            {
                CareerId = _careerId,
                TierId = _tierId,
                TierIndex = _tierIndex,
                CareerSongId = _song.Id,
                SongIndex = _songIndex,
            };

            _onPlay?.Invoke();

            MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
        }
    }
}
