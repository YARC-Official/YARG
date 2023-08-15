using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Song;
using YARG.Player;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class SongViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override string PrimaryText => SongMetadata.Name;
        public override string SecondaryText => SongMetadata.Artist;
        public override bool UseAsMadeFamousBy => !SongMetadata.IsMaster;

        public override string SideText =>
            // TODO: Disable scores for now
            // get
            // {
            //     var score = ScoreManager.GetScore(SongEntry);
            //     if (score == null || score.highestPercent.Count <= 0)
            //     {
            //         return string.Empty;
            //     }
            //
            //     var (instrument, highest) = score.GetHighestPercent();
            //     return $"<sprite name=\"{instrument}\"> <b>{highest.difficulty.ToChar()}</b> " +
            //         $"{Mathf.Floor(highest.percent * 100f):N0}%";
            // }
            string.Empty;

        public SongMetadata SongMetadata { get; private set; }

        public SongViewType(SongMetadata songMetadata)
        {
            SongMetadata = songMetadata;
        }

        public override async UniTask<Sprite> GetIcon()
        {
            return await SongSources.SourceToIcon(SongMetadata.Source);
        }

        public override void SecondaryTextClick()
        {
            base.SecondaryTextClick();

            MusicLibraryMenu.Instance.SetSearchInput($"artist:{SongMetadata.Artist}");
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

            MusicLibraryMenu.Instance.SetSearchInput($"source:{SongMetadata.Source}");
        }
    }
}