using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Data;
using YARG.Song;

namespace YARG.UI.MusicLibrary.ViewTypes
{
    public class SongViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override string PrimaryText => SongEntry.Name;
        public override string SecondaryText => SongEntry.Artist;
        public override bool UseAsMadeFamousBy => !SongEntry.IsMaster;

        public override string SideText
        {
            get
            {
                // Song score
                var score = ScoreManager.GetScore(SongEntry);
                if (score == null || score.highestPercent.Count <= 0)
                {
                    return string.Empty;
                }
                else
                {
                    var (instrument, highest) = score.GetHighestPercent();
                    return
                        $"<sprite name=\"{instrument}\"> <b>{highest.difficulty.ToChar()}</b> {Mathf.Floor(highest.percent * 100f):N0}%";
                }
            }
        }

        public SongEntry SongEntry { get; private set; }

        public SongViewType(SongEntry songEntry)
        {
            SongEntry = songEntry;
        }

        public override async UniTask<Sprite> GetIcon()
        {
            return await SongSources.SourceToIcon(SongEntry.Source);
        }

        public override void SecondaryTextClick()
        {
            base.SecondaryTextClick();

            SongSelection.Instance.SetSearchInput($"artist:{SongEntry.Artist}");
        }

        public override void PrimaryButtonClick()
        {
            base.PrimaryButtonClick();

            MainMenu.Instance.ShowPreSong();
        }

        public override void IconClick()
        {
            base.IconClick();

            SongSelection.Instance.SetSearchInput($"source:{SongEntry.Source}");
        }
    }
}