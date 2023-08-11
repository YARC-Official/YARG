﻿using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Song;
using YARG.Data;
using YARG.Menu.Navigation;
using YARG.Player;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class SongViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override string PrimaryText => SongEntry.Name;
        public override string SecondaryText => SongEntry.Artist;
        public override bool UseAsMadeFamousBy => !SongEntry.IsMaster;

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

        public SongMetadata SongEntry { get; private set; }

        public SongViewType(SongMetadata songEntry)
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

            MusicLibraryMenu.Instance.SetSearchInput($"artist:{SongEntry.Artist}");
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

            MusicLibraryMenu.Instance.SetSearchInput($"source:{SongEntry.Source}");
        }
    }
}