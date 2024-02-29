using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Playlists;
using YARG.Scores;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class SongViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override bool UseAsMadeFamousBy => !SongEntry.IsMaster;

        public override bool ShowFavoriteButton => true;
        public override bool IsFavorited => PlaylistContainer.FavoritesPlaylist.ContainsSong(SongEntry);

        private readonly MusicLibraryMenu _musicLibrary;
        public readonly SongEntry SongEntry;

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

        public override string GetSideText(bool selected)
        {
            var score = ScoreContainer.GetHighScore(SongEntry.Hash);

            // Never played!
            if (score is null) return string.Empty;

            var instrument = score.Instrument.ToResourceName();
            var difficultyChar = score.Difficulty.ToChar();
            var percent = Mathf.Floor(score.Percent * 100f);

            return $"<sprite name=\"{instrument}\"> <b>{difficultyChar}</b> {percent:N0}%";
        }

        public override async UniTask<Sprite> GetIcon()
        {
            return await SongSources.SourceToIcon(SongEntry.Source);
        }

        public override void SecondaryTextClick()
        {
            base.SecondaryTextClick();
           _musicLibrary.SetSearchInput(SongAttribute.Artist, SongEntry.Artist.SortStr);
        }

        public override void PrimaryButtonClick()
        {
            base.PrimaryButtonClick();

            if (PlayerContainer.Players.Count <= 0) return;

            GlobalVariables.Instance.CurrentSong = SongEntry;
            MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
        }

        public override void IconClick()
        {
            base.IconClick();
           _musicLibrary.SetSearchInput(SongAttribute.Source, SongEntry.Source.SortStr);
        }

        public override void FavoriteClick()
        {
            base.FavoriteClick();

            if (!IsFavorited)
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