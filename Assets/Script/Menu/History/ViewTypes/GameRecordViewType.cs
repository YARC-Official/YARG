﻿using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Menu.Persistent;
using YARG.Replays;
using YARG.Scores;
using YARG.Song;

namespace YARG.Menu.History
{
    public class GameRecordViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override bool UseFullContainer => true;

        private readonly GameRecord _gameRecord;
        private readonly SongMetadata _songMetadata;

        public GameRecordViewType(GameRecord gameRecord)
        {
            _gameRecord = gameRecord;

            var songsByHash = GlobalVariables.Instance.SongContainer.SongsByHash;
            _songMetadata = songsByHash.GetValueOrDefault(new HashWrapper(gameRecord.SongChecksum))[0];
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_gameRecord.SongName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(_gameRecord.SongArtist, TextType.Secondary, selected);
        }

        public override void ViewClick()
        {
            if (_songMetadata is null) return;

            // Get the replay path
            var path = Path.Combine(ScoreContainer.ScoreReplayDirectory, _gameRecord.ReplayFileName);
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
                    "The replay for this song is most likely corrupted.");
                return;
            }

            // Create a replay entry
            var replayEntry = ReplayContainer.CreateEntryFromReplayFile(replayFile);
            replayEntry.ReplayPath = path;

            // Compare hashes
            var databaseHash = new HashWrapper(_gameRecord.ReplayChecksum);
            if (!replayFile.Header.ReplayChecksum.Equals(databaseHash))
            {
                DialogManager.Instance.ShowMessage("Cannot Play Replay",
                    "The replay's hash does not match the hash present in the database. Was the database modified?");
                return;
            }

            // We're good!
            GlobalVariables.Instance.IsReplay = true;
            GlobalVariables.Instance.CurrentReplay = replayEntry;

            GlobalVariables.AudioManager.UnloadSong();
            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }

        public override async UniTask<Sprite> GetIcon()
        {
            // TODO: Show "song missing" icon instead
            if (_songMetadata is null) return null;

            return await SongSources.SourceToIcon(_songMetadata.Source);
        }

        public override GameInfo? GetGameInfo()
        {
            return new GameInfo
            {
                BandScore = _gameRecord.BandScore,
                BandStars = _gameRecord.BandStars
            };
        }
    }
}