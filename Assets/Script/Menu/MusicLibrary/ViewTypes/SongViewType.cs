﻿using System.Linq;
using Cysharp.Text;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Song;
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

        private readonly MusicLibraryMenu _musicLibrary;

        private bool _fetchedScores;
        private PlayerScoreRecord _playerScoreRecord;
        private GameRecord _bandScoreRecord;

        public SongViewType(MusicLibraryMenu musicLibrary, SongEntry songEntry)
        {
            _musicLibrary = musicLibrary;
            SongEntry = songEntry;
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

            if (_bandScoreRecord is not null)
            {
                // Append the band score if the setting is enabled
                if (SettingsManager.Settings.HighScoreInfo.Value == HighScoreInfoMode.Score)
                {
                    builder.AppendFormat("<space=2em> {0:N0}", _bandScoreRecord.BandScore);
                }

                return builder.ToString();
            }

            // Never played!
            if (_playerScoreRecord is null)
            {
                return string.Empty;
            }

            var difficultySprite = _playerScoreRecord.Difficulty.ToString();
            var percent = Mathf.Floor(_playerScoreRecord.GetPercent() * 100f);
            var percentColor = _playerScoreRecord.IsFc ? "#fcd13c" : "#ffffff";

            builder.AppendFormat("<sprite name=\"{0}\"> <color={1}>{2:N0}%</color>",
                difficultySprite, percentColor, percent);

            // Append the score if the setting is enabled
            if (SettingsManager.Settings.HighScoreInfo.Value == HighScoreInfoMode.Score)
            {
                builder.AppendFormat("<space=2em> {0:N0}", _playerScoreRecord.Score);
            }

            return builder.ToString();
        }

        public override StarAmount? GetStarAmount()
        {
            // Only show stars if enabled
            if (SettingsManager.Settings.HighScoreInfo.Value != HighScoreInfoMode.Stars)
            {
                return null;
            }

            FetchHighScores();

            if (_bandScoreRecord is not null)
            {
                return _bandScoreRecord.BandStars;
            }

            return _playerScoreRecord?.Stars;
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

            GlobalVariables.State.CurrentSong = SongEntry;
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

                // If we are in the favorites menu, then update the playlist
                // to remove the song that was just removed.
                if (MusicLibraryMenu.SelectedPlaylist == PlaylistContainer.FavoritesPlaylist)
                {
                    _musicLibrary.RefreshAndReselect();
                }
            }
        }

        private void FetchHighScores()
        {
            if (_fetchedScores)
            {
                return;
            }

            _fetchedScores = true;

            _playerScoreRecord = GetHighScoreForSong(SongEntry);
            _bandScoreRecord = GetBandHighScoreForSong(SongEntry);
        }

        private PlayerScoreRecord GetHighScoreForSong(SongEntry song)
        {
            if (!_musicLibrary.ShouldDisplaySoloHighScores)
            {
                return null;
            }

            var player = PlayerContainer.Players.First(e => !e.Profile.IsBot);

            var result = ScoreContainer.GetHighScore(
                song.Hash, player.Profile.Id, player.Profile.CurrentInstrument);

            var percResult = ScoreContainer.GetBestPercentageScore(
                song.Hash, player.Profile.Id, player.Profile.CurrentInstrument);

            if (result is not null)
            {
                YargLogger.Assert(percResult is not null, "Best Percentage score is missing!");
                result.Percent = percResult.GetPercent();
            }

            return result;
        }

        private GameRecord GetBandHighScoreForSong(SongEntry song)
        {
            if (_musicLibrary.ShouldDisplaySoloHighScores)
            {
                return null;
            }

            return ScoreContainer.GetBandHighScore(song.Hash);
        }
    }
}