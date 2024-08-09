using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Replays.Analyzer;
using YARG.Core.Song;
using YARG.Localization;
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
            if (SongContainer.SongsByHash.TryGetValue(replayEntry.SongChecksum, out var songs))
            {
                _songEntry = songs[0];
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

#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            // TODO: Show "song missing" icon instead when _songEntry is null
            return _songEntry != null ? SongSources.SourceToIcon(_songEntry.Source) : null;
        }

        public override void ViewClick()
        {
            if (_songEntry is null) return;

            PlayReplay().Forget();
        }

        public override void Shortcut1()
        {
            if (_songEntry is null) return;

            AnalyzeReplay();
        }

        private async UniTaskVoid PlayReplay()
        {
            // Show warning
            if (SettingsManager.Settings.ShowEngineInconsistencyDialog)
            {
                var dialog = DialogManager.Instance.ShowOneTimeMessage(
                    Localize.Key("Menu.Dialog.EngineInconsistency.Title"),
                    Localize.Key("Menu.Dialog.EngineInconsistency.Description"),
                    () =>
                    {
                        SettingsManager.Settings.ShowEngineInconsistencyDialog = false;
                        SettingsManager.SaveSettings();
                    });

                await dialog.WaitUntilClosed();
            }

            LoadIntoReplay(_replayEntry, _songEntry);
        }

        private void AnalyzeReplay()
        {
            var chart = _songEntry.LoadChart();

            if (chart is null)
            {
                YargLogger.LogError("Chart did not load");
                return;
            }

            var replayReadResult = ReplayIO.ReadReplay(_replayEntry.ReplayPath, out var replay);
            if (replayReadResult != ReplayReadResult.Valid)
            {
                YargLogger.LogFormatError("Replay did not load. {0}", replayReadResult);
                return;
            }

            var results = ReplayAnalyzer.AnalyzeReplay(chart, replay!);

            for(int i = 0; i < results.Length; i++)
            {
                var analysisResult = results[i];

                var profile = replay.Frames[i].PlayerInfo.Profile;
                if (analysisResult.Passed)
                {
                    YargLogger.LogFormatInfo("({0}, {1}/{2}) PASSED verification!", profile.Name, profile.CurrentInstrument, profile.CurrentDifficulty);
                }
                else
                {
                    YargLogger.LogFormatWarning("({0}, {1}/{2}) FAILED verification",
                        profile.Name, profile.CurrentDifficulty, profile.CurrentDifficulty);
                }
            }
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