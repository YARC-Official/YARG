using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Replays.Analyzer;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Persistent;
using YARG.Replays;
using YARG.Scores;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.History
{
    public class GameRecordViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override bool UseFullContainer => true;

        public readonly GameRecord GameRecord;

        private readonly SongEntry _songEntry;

        public GameRecordViewType(GameRecord gameRecord)
        {
            GameRecord = gameRecord;

            if (SongContainer.SongsByHash.TryGetValue(HashWrapper.Create(gameRecord.SongChecksum), out var songs))
            {
                _songEntry = songs[0];
            }
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(GameRecord.SongName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(GameRecord.SongArtist, TextType.Secondary, selected);
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
            // Get the replay path
            var path = Path.Combine(ScoreContainer.ScoreReplayDirectory, GameRecord.ReplayFileName);
            if (!File.Exists(path))
            {
                DialogManager.Instance.ShowMessage("Cannot Play Replay",
                    "The replay for this song does not exist. It has probably been deleted.");
                return;
            }

            // Read
            var result = ReplayIO.ReadReplay(path, out var replay);
            if (result != ReplayReadResult.Valid || replay == null)
            {
                DialogManager.Instance.ShowMessage("Cannot Play Replay",
                    "The replay for this song is most likely corrupted, or out of date.");
                return;
            }

            // Create a replay entry
            var replayEntry = ReplayEntry.CreateFromReplay(replay);
            replayEntry.ReplayPath = path;

            // Compare hashes
            var databaseHash = HashWrapper.Create(GameRecord.ReplayChecksum);
            if (!replay.Header.ReplayChecksum.Equals(databaseHash))
            {
                DialogManager.Instance.ShowMessage("Cannot Play Replay",
                    "The replay's hash does not match the hash present in the database. Was the database modified?");
                return;
            }

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

            LoadIntoReplay(replayEntry, _songEntry);
        }

        private void AnalyzeReplay()
        {
            var chart = _songEntry.LoadChart();

            if (chart is null)
            {
                YargLogger.LogError("Chart did not load");
                return;
            }

            var replayReadResult = ReplayIO.ReadReplay(Path.Combine(ScoreContainer.ScoreReplayDirectory, GameRecord.ReplayFileName), out var replay);
            if (replayReadResult != ReplayReadResult.Valid)
            {
                YargLogger.LogFormatError("Replay did not load. {0}", replayReadResult);
                return;
            }

            replay = replay!;

            var results = ReplayAnalyzer.AnalyzeReplay(chart, replay);

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

#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            // TODO: Show "song missing" icon instead when _songEntry is null
            return _songEntry != null ? SongSources.SourceToIcon(_songEntry.Source) : null;
        }

        public override GameInfo? GetGameInfo()
        {
            return new GameInfo
            {
                BandScore = GameRecord.BandScore,
                BandStars = GameRecord.BandStars
            };
        }
    }
}
