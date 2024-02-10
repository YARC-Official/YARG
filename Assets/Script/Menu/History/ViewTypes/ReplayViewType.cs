using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Menu.Persistent;
using YARG.Replays;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.History
{
    public class ReplayViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override bool UseFullContainer => true;

        private readonly ReplayEntry _replayEntry;
        private readonly SongEntry _songEntry;

        public ReplayViewType(ReplayEntry replayEntry)
        {
            _replayEntry = replayEntry;

            var songsByHash = GlobalVariables.Instance.SongContainer.SongsByHash;

            var songsForHash = songsByHash.GetValueOrDefault(replayEntry.SongChecksum);
            if (songsForHash is not null)
            {
                _songEntry = songsForHash[0];
            }
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_replayEntry.SongName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(_replayEntry.ArtistName, TextType.Secondary, selected);
        }
         
        public override async UniTask<Sprite> GetIcon()
        {
            // TODO: Show "song missing" icon instead
            if (_songEntry is null) return null;

            return await SongSources.SourceToIcon(_songEntry.Source);
        }

        public override void ViewClick()
        {
            if (_songEntry is null) return;

            PlayReplay().Forget();
        }

        private async UniTaskVoid PlayReplay()
        {
            // Show warning
            if (SettingsManager.Settings.ShowEngineInconsistencyDialog)
            {
                var dialog = DialogManager.Instance.ShowOneTimeMessage(
                    LocaleHelper.LocalizeString("Dialogs.EngineInconsistency.Title"),
                    LocaleHelper.LocalizeString("Dialogs.EngineInconsistency"),
                    () =>
                    {
                        SettingsManager.Settings.ShowEngineInconsistencyDialog = false;
                        SettingsManager.SaveSettings();
                    });

                await dialog.WaitUntilClosed();
            }

            // We're good!
            GlobalVariables.Instance.IsReplay = true;
            GlobalVariables.Instance.CurrentReplay = _replayEntry;
            GlobalVariables.Instance.CurrentSong = _songEntry;

            GlobalVariables.AudioManager.UnloadSong();
            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }

        public override GameInfo? GetGameInfo()
        {
            return new GameInfo
            {
                BandScore = _replayEntry.BandScore,
                BandStars = _replayEntry.BandStars
            };
        }
    }
}