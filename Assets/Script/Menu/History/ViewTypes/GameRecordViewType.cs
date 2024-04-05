using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Helpers;
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

            if (SongContainer.SongsByHash.TryGetValue(new HashWrapper(gameRecord.SongChecksum), out var songs))
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
            var result = ReplayIO.ReadReplay(path, out var replayFile);
            if (result != ReplayReadResult.Valid || replayFile == null)
            {
                DialogManager.Instance.ShowMessage("Cannot Play Replay",
                    "The replay for this song is most likely corrupted, or out of date.");
                return;
            }

            // Create a replay entry
            var replayEntry = ReplayContainer.CreateEntryFromReplayFile(replayFile);
            replayEntry.ReplayPath = path;

            // Compare hashes
            var databaseHash = new HashWrapper(GameRecord.ReplayChecksum);
            if (!replayFile.Header.ReplayChecksum.Equals(databaseHash))
            {
                DialogManager.Instance.ShowMessage("Cannot Play Replay",
                    "The replay's hash does not match the hash present in the database. Was the database modified?");
                return;
            }

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
            GlobalVariables.State = PersistentState.Default;
            GlobalVariables.State.CurrentSong = _songEntry;
            GlobalVariables.State.CurrentReplay = replayEntry;
            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }

        public override async UniTask<Sprite> GetIcon()
        {
            // TODO: Show "song missing" icon instead
            if (_songEntry is null) return null;

            return await SongSources.SourceToIcon(_songEntry.Source);
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