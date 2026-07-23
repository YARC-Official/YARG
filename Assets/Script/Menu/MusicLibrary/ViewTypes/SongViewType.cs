using System;
using System.Linq;
using Cysharp.Text;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Player;
using YARG.Playlists;
using YARG.Scores;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public enum HighScoreInfoMode
    {
        Stars,
        Score,
        Off
    }

    public class SongViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override bool UseAsMadeFamousBy => !SongEntry.IsMaster;

        public readonly SongEntry SongEntry;
        public override string StableId => _stableId;
        public string ContentStableId => _contentStableId;

        private readonly MusicLibraryMenu _musicLibrary;
        private readonly string _stableId;
        private readonly string _contentStableId;

        private bool _fetchedScores;
        private PlayerScoreRecord _playerScoreRecord;
        private GameRecord _bandScoreRecord;
        private int _fetchedHumanCount;
        private Guid _fetchedPlayerId;
        private Instrument _fetchedInstrument;
        private Difficulty _fetchedDifficulty;
        private HighScoreHistoryMode _fetchedHighScoreHistoryMode;

        public SongViewType(MusicLibraryMenu musicLibrary, SongEntry songEntry, string context = "library")
        {
            _musicLibrary = musicLibrary;
            SongEntry = songEntry;
            _contentStableId = $"Song:{SongEntry.Hash}_{SongEntry.ActualLocation}";
            _stableId = $"Song:{context}:{_contentStableId}";
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(SongEntry.Name, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(SongEntry.Artist, TextType.Secondary, selected);
        }

#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            return SongSources.SourceToIcon(SongEntry.Source);
        }

        public override string GetSideText(bool selected)
        {
            FetchHighScores();

            using var builder = ZString.CreateStringBuilder();

            // If non-null, band score is being requested
            if (_bandScoreRecord is not null)
            {
                builder.AppendFormat("{0:N0}", _bandScoreRecord.BandScore);
                return builder.ToString();
            }

            // Never played!
            if (_playerScoreRecord is null)
            {
                return string.Empty;
            }

            var scoreColor = _playerScoreRecord.IsFc ? "#ffd029" : "#ffffff";
            builder.AppendFormat("<mspace=.5em><color={1}>{0:N0}</color></mspace>",
                _playerScoreRecord.Score, scoreColor);
            return builder.ToString();
        }

        public override ScoreInfo? GetScoreInfo()
        {
            FetchHighScores();

            // Never played!
            if (_playerScoreRecord is null)
            {
                return null;
            }

            return new ScoreInfo
            {
                Score = _playerScoreRecord.Score,
                Difficulty = _playerScoreRecord.Difficulty,
                Percent = _playerScoreRecord.GetPercent(),
                Instrument = _playerScoreRecord.Instrument,
                IsFc = _playerScoreRecord.IsFc
            };
        }

        public override StarAmount? GetStarAmount()
        {
            FetchHighScores();

            return GetStarAmount(_playerScoreRecord, _bandScoreRecord);
        }

        public static StarAmount? GetStarAmountForSong(SongEntry songEntry)
        {
            FetchHighScores(songEntry, out var playerScoreRecord, out var bandScoreRecord);

            return GetStarAmount(playerScoreRecord, bandScoreRecord);
        }

#nullable enable
        private static StarAmount? GetStarAmount(
            PlayerScoreRecord? playerScoreRecord,
            GameRecord? bandScoreRecord)
#nullable disable
        {
            if (bandScoreRecord is not null)
            {
                return bandScoreRecord.BandStars;
            }

            return playerScoreRecord?.Stars;
        }

        public override FavoriteInfo GetFavoriteInfo()
        {
            return new FavoriteInfo
            {
                ShowFavoriteButton = true,
                IsFavorited = PlaylistContainer.FavoritesPlaylist.ContainsSong(SongEntry)
            };
        }

        public override void SecondaryTextClick()
        {
            base.SecondaryTextClick();
           _musicLibrary.SetSearchInput(SortAttribute.Artist, $"\"{SongEntry.Artist.SearchStr}\"");
        }

        public override void PrimaryButtonClick()
        {
            base.PrimaryButtonClick();

            if (PlayerContainer.Players.Count <= 0)
            {
                return;
            }

            // Reset library's main index so we don't return to the index set by play a show
            MusicLibraryMenu.ResetMainLibraryIndex();
            MusicLibraryMenu.SetReload(MusicLibraryReloadState.Partial);

            GlobalVariables.State.CurrentSong = SongEntry;
            // This just makes stuff in DifficultySelectMenu easier
            GlobalVariables.State.ShowSongs.Clear();
            GlobalVariables.State.ShowSongs.Add(SongEntry);
            GlobalVariables.State.PlayingAShow = false;

            MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
        }

        public override void IconClick()
        {
           _musicLibrary.SetSearchInput(SortAttribute.Source, $"\"{SongEntry.Source.SearchStr}\"");
        }

        public override void FavoriteClick()
        {
            base.FavoriteClick();

            var info = GetFavoriteInfo();

            if (!info.IsFavorited)
            {
                PlaylistContainer.FavoritesPlaylist.AddSong(SongEntry);
            }
            else
            {
                PlaylistContainer.FavoritesPlaylist.RemoveSong(SongEntry);

                // Refresh the view to update the filter results
                _musicLibrary.RefreshAndReselect();
            }

            _musicLibrary.RefreshSidebar();
        }

        public override void AddToPlaylist(Playlist playlist)
        {
            playlist.AddSong(SongEntry);
        }

        public override void RemoveFromPlaylist(Playlist playlist)
        {
            playlist.RemoveSong(SongEntry);

            // Refresh the view to update the filter results
            _musicLibrary.RefreshAndReselect();
        }

        private void FetchHighScores()
        {
            var context = GetCurrentScoreContext();
            if (_fetchedScores &&
                _fetchedHumanCount == context.HumanCount &&
                _fetchedPlayerId == context.PlayerId &&
                _fetchedInstrument == context.Instrument &&
                _fetchedDifficulty == context.Difficulty &&
                _fetchedHighScoreHistoryMode == context.HighScoreHistoryMode)
            {
                return;
            }

            FetchHighScores(SongEntry, out _playerScoreRecord, out _bandScoreRecord);
            _fetchedHumanCount = context.HumanCount;
            _fetchedPlayerId = context.PlayerId;
            _fetchedInstrument = context.Instrument;
            _fetchedDifficulty = context.Difficulty;
            _fetchedHighScoreHistoryMode = context.HighScoreHistoryMode;
            _fetchedScores = true;
        }

        private static ScoreContext GetCurrentScoreContext()
        {
            var player = PlayerContainer.Players.FirstOrDefault(e => !e.Profile.IsBot);
            return new ScoreContext(
                PlayerContainer.Players.Count(p => !p.Profile.IsBot),
                player?.Profile.Id ?? Guid.Empty,
                player?.Profile.CurrentInstrument ?? Instrument.Band,
                player?.Profile.CurrentDifficulty ?? Difficulty.Easy,
                SettingsManager.Settings.HighScoreHistory.Value);
        }

        private static void FetchHighScores(SongEntry songEntry, out PlayerScoreRecord playerScoreRecord, out GameRecord bandScoreRecord)
        {
            ScoreContainer.GetPreferredHighScoresForCurrentPlayers(
                songEntry.Hash, out playerScoreRecord, out bandScoreRecord);
        }

        private readonly struct ScoreContext
        {
            public readonly int HumanCount;
            public readonly Guid PlayerId;
            public readonly Instrument Instrument;
            public readonly Difficulty Difficulty;
            public readonly HighScoreHistoryMode HighScoreHistoryMode;

            public ScoreContext(
                int humanCount,
                Guid playerId,
                Instrument instrument,
                Difficulty difficulty,
                HighScoreHistoryMode highScoreHistoryMode)
            {
                HumanCount = humanCount;
                PlayerId = playerId;
                Instrument = instrument;
                Difficulty = difficulty;
                HighScoreHistoryMode = highScoreHistoryMode;
            }
        }
    }
}
