using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Scores;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class SongViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;
        public override bool UseAsMadeFamousBy => !SongMetadata.IsMaster;

        private readonly MusicLibraryMenu _musicLibraryMenu;
        public readonly SongMetadata SongMetadata;

        public SongViewType(MusicLibraryMenu musicLibraryMenu, SongMetadata songMetadata)
        {
            _musicLibraryMenu = musicLibraryMenu;

            SongMetadata = songMetadata;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(SongMetadata.Name, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(SongMetadata.Artist, TextType.Secondary, selected);
        }

        public override string GetSideText(bool selected)
        {
            var score = ScoreContainer.GetHighScore(SongMetadata.Hash);

            // Never played!
            if (score is null) return string.Empty;

            var instrument = score.Instrument.ToResourceName();
            var difficultyChar = score.Difficulty.ToChar();
            var percent = Mathf.Floor(score.Percent * 100f);

            return $"<sprite name=\"{instrument}\"> <b>{difficultyChar}</b> {percent:N0}%";
        }

        public override async UniTask<Sprite> GetIcon()
        {
            return await SongSources.SourceToIcon(SongMetadata.Source);
        }

        public override void SecondaryTextClick()
        {
            base.SecondaryTextClick();

           _musicLibraryMenu.SetSearchInput($"artist:{SongMetadata.Artist}");
        }

        public override void PrimaryButtonClick()
        {
            base.PrimaryButtonClick();

            if (PlayerContainer.Players.Count <= 0) return;

            GlobalVariables.Instance.CurrentSong = SongMetadata;
            MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);
        }

        public override void IconClick()
        {
            base.IconClick();

           _musicLibraryMenu.SetSearchInput($"source:{SongMetadata.Source}");
        }
    }
}