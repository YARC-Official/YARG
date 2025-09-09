using System.Linq;
using Cysharp.Text;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Player;
using YARG.Playlists;
using YARG.Scores;
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
        private PlayerScoreRecord _playerPercentRecord;
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

            var percentColor = _playerPercentRecord.IsFc ? "#ffd029" : "#ffffff";
            builder.AppendFormat("<mspace=.5em><color={1}>{0:N0}</color></mspace>",
                _playerScoreRecord.Score, percentColor);
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
                Percent = _playerPercentRecord.GetPercent(),
                Instrument = _playerScoreRecord.Instrument,
                IsFc = _playerPercentRecord.IsFc
            };
        }

        public override StarAmount? GetStarAmount()
        {
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

                // If we are in the favorites menu, then update the playlist
                // to remove the song that was just removed.
                if (_musicLibrary.SelectedPlaylist == PlaylistContainer.FavoritesPlaylist)
                {
                    _musicLibrary.RefreshAndReselect();
                }
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

            if (_musicLibrary.SelectedPlaylist == playlist)
            {
                _musicLibrary.RefreshAndReselect();
            }
        }

        private void FetchHighScores()
        {
            if (_fetchedScores)
            {
                return;
            }

            _fetchedScores = true;

            if (_musicLibrary.ShouldDisplaySoloHighScores)
            {
                var player = PlayerContainer.Players.First(e => !e.Profile.IsBot);
                _playerScoreRecord = ScoreContainer.GetHighScore(
                    SongEntry.Hash, player.Profile.Id, player.Profile.CurrentInstrument);
                _playerPercentRecord = ScoreContainer.GetBestPercentageScore(
                    SongEntry.Hash, player.Profile.Id, player.Profile.CurrentInstrument);
            }
            else
            {
                _bandScoreRecord = ScoreContainer.GetBandHighScore(SongEntry.Hash);
            }
        }
    }
}