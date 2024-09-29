using Cysharp.Text;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
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

        private readonly MusicLibraryMenu _musicLibrary;
        public readonly SongEntry SongEntry;
        public readonly PlayerScoreRecord PlayerScoreRecord;
        public readonly GameRecord BandScoreRecord;

        public SongViewType(MusicLibraryMenu musicLibrary, SongEntry songEntry, PlayerScoreRecord playerScoreRecord, GameRecord bandScoreRecord)
        {
            _musicLibrary = musicLibrary;
            SongEntry = songEntry;
            PlayerScoreRecord = playerScoreRecord;
            BandScoreRecord = bandScoreRecord;
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
            using var builder = ZString.CreateStringBuilder();

            if (BandScoreRecord != null)
            {
                // Append the band score if the setting is enabled
                if (SettingsManager.Settings.HighScoreInfo.Value == HighScoreInfoMode.Score)
                {
                    builder.AppendFormat("<space=2em> {0:N0}", BandScoreRecord.BandScore);
                }

                return builder.ToString();
            }

            // Never played!
            if (PlayerScoreRecord is null)
            {
                return string.Empty;
            }

            var difficultySprite = PlayerScoreRecord.Difficulty.ToString();
            var percent = Mathf.Floor(PlayerScoreRecord.GetPercent() * 100f);
            var percentColor = PlayerScoreRecord.IsFc ? "#fcd13c" : "#ffffff";

            builder.AppendFormat( "<sprite name=\"{0}\"> <color={1}>{2:N0}%</color>", difficultySprite, percentColor, percent);

            // Append the score if the setting is enabled
            if (SettingsManager.Settings.HighScoreInfo.Value == HighScoreInfoMode.Score)
            {
                builder.AppendFormat("<space=2em> {0:N0}", PlayerScoreRecord.Score);
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

            if (BandScoreRecord != null)
            {
                return BandScoreRecord.BandStars;
            }

            return PlayerScoreRecord?.Stars;
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
    }
}