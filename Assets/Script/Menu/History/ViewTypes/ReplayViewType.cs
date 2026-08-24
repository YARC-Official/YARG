using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Replays.Analyzer;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Persistent;
using YARG.Menu.ScoreScreen;
using YARG.Player;
using YARG.Replays;
using YARG.Scores;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.History
{
#nullable enable
    public class ReplayViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;
        private const   string         BASE_LOCALIZATION_KEY = "Menu.Dialog.ReplayMenu";


        public override bool UseFullContainer => true;

        // Non-readonly as we may find it later
        private ReplayInfo? _entry;
        private readonly GameRecord? _record;

        private readonly SongEntry? _songEntry;
        private readonly string _songName;
        private readonly string _artistName;

        private readonly GameInfo _gameInfo;
        private readonly Sprite? _sprite;

        public ReplayViewType(GameRecord record, List<PlayerScoreRecord>? playerScoreRecords)
        {
            _record = record;
            if (SongContainer.SongsByHash.TryGetValue(HashWrapper.Create(record.SongChecksum), out var songs))
            {
                _songEntry = songs[0];
                _sprite = SongSources.SourceToIcon(_songEntry.Source);
            }
            _gameInfo.BandScore = _record.BandScore;
            _gameInfo.BandStars = _record.BandStars;
            _gameInfo.PlayerScoreRecords = playerScoreRecords;
            _songName = _record.SongName;
            _artistName = _record.SongArtist;
        }

        public ReplayViewType(ReplayInfo entry)
        {
            _entry = entry;
            if (SongContainer.SongsByHash.TryGetValue(entry.SongChecksum, out var songs))
            {
                _songEntry = songs[0];
                _sprite = SongSources.SourceToIcon(_songEntry.Source);
            }
            _gameInfo.BandScore = _entry.BandScore;
            _gameInfo.BandStars = _entry.BandStars;
            _songName = _entry.SongName;
            _artistName = _entry.ArtistName;
        }


        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_songName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return FormatAs(_artistName, TextType.Secondary, selected);
        }

        public override Sprite? GetIcon()
        {
            // TODO: Show "song missing" icon instead when _songEntry is null
            return _sprite;
        }

        // AKA, the Play Replay Button
        public override async void ViewClick()
        {
            if (_songEntry == null)
            {
                var title = Localize.Key(BASE_LOCALIZATION_KEY, "UnavailableSong.Title");
                var desc = Localize.Key(BASE_LOCALIZATION_KEY, "UnavailableSong.Description");
                DialogManager.Instance.ShowMessage(title, desc);
                return;
            }

            _entry ??= LoadReplay("Cannot Play Replay");
            if (_entry == null)
            {
                return;
            }

            // Show warning
            if (SettingsManager.Settings.ShowEngineInconsistencyDialog)
            {
                var dialog = DialogManager.Instance.ShowOneTimeMessage(
                    "Menu.Dialog.ReplayMenu.EngineInconsistency",
                    () =>
                    {
                        SettingsManager.Settings.ShowEngineInconsistencyDialog = false;
                        SettingsManager.SaveSettings();
                    });

                await dialog.WaitUntilClosed();
            }

            LoadIntoReplay(_entry, _songEntry);
        }

        // Analyze Replay Button
        public override void AnalyzeReplayClick()
        {
            if (_songEntry == null)
            {
                var title = Localize.Key(BASE_LOCALIZATION_KEY, "UnavailableSong.Title");
                var desc = Localize.Key(BASE_LOCALIZATION_KEY, "UnavailableSong.Description");
                DialogManager.Instance.ShowMessage(title, desc);
                return;
            }

            _entry ??= LoadReplay("Cannot Analyze Replay");
            if (_entry == null)
            {
                return;
            }

            var chart = _songEntry.LoadChart();
            if (chart == null)
            {
                YargLogger.LogError("Failed to load chart");
                return;
            }

            if (_entry.CensorshipEnabled)
            {
                chart.ApplyCensorship();
            }

            var replayOptions = new ReplayReadOptions { KeepFrameTimes = GlobalVariables.VerboseReplays };
            var (result, data) = ReplayIO.TryLoadData(_entry, replayOptions);
            if (result != ReplayReadResult.Valid)
            {
                YargLogger.LogFormatError("Failed to load replay. {0}", result);
                return;
            }

            var results = ReplayAnalyzer.AnalyzeReplay(chart, _entry, data);
            for (int i = 0; i < results.Length; i++)
            {
                var analysisResult = results[i];

                var profile = data.Frames[i].Profile;
                if (analysisResult.Passed)
                {
                    YargLogger.LogFormatInfo("({0}, {1}/{2}) PASSED verification!", profile.Name, profile.CurrentInstrument, profile.CurrentDifficulty);
                }
                else
                {
                    YargLogger.LogFormatWarning("({0}, {1}/{2}) FAILED verification. Stats:\n{3}",
                        profile.Name, profile.CurrentDifficulty, profile.CurrentDifficulty, item4: analysisResult.StatLog);
                }
            }
        }

        public override void ViewScoreCardClick()
        {
            _entry ??= LoadReplay("Cannot load replay.");
            if (_entry == null)
            {
                return;
            }

            if (_songEntry == null)
            {
                var title = Localize.Key(BASE_LOCALIZATION_KEY, "UnavailableSong.Title");
                var desc = Localize.Key(BASE_LOCALIZATION_KEY, "UnavailableSong.Description");
                DialogManager.Instance.ShowMessage(title, desc);
                return;
            }

            GlobalVariables.State.CurrentSong = _songEntry;
            GlobalVariables.State.CurrentReplay = _entry;
            var chart = _songEntry.LoadChart();
            if (chart == null)
            {
                YargLogger.LogError("Failed to load chart");
                return;
            }

            var replayOptions = new ReplayReadOptions
            {
                KeepFrameTimes = GlobalVariables.VerboseReplays
            };
            var (result, data) = ReplayIO.TryLoadData(_entry, replayOptions);
            if (result != ReplayReadResult.Valid)
            {
                YargLogger.LogFormatError("Failed to load replay data: {0}", result);
                DialogManager.Instance.ShowMessage(Localize.Key(BASE_LOCALIZATION_KEY, "ReplayLoadFail.Title"), Localize.Key(BASE_LOCALIZATION_KEY, "ReplayLoadFail.Description"));
                return;
            }

            var results = ReplayAnalyzer.AnalyzeReplay(chart, _entry, data);
            bool replayConsistent = results.All(r => r.Passed);
            if (!replayConsistent)
            {
                YargLogger.LogInfo("Replay did not pass verification!");
            }

            var playerScoreCards = new PlayerScoreCard[results.Length];
            for (int i = 0; i < _entry.Stats.Length; i++)
            {
                var playerResult = results[i];
                var playerEntry = _entry.Stats[i];
                bool isHighScore;
                if (_gameInfo.PlayerScoreRecords == null)
                {
                    // Imported replays do not have PlayerScoreRecords
                    isHighScore = false;
                }
                else
                {
                    var playerScoreRecord = _gameInfo.PlayerScoreRecords[i];
                    var currentHighScore = ScoreContainer.GetHighScore(_entry.SongChecksum, playerScoreRecord.PlayerId,
                        playerResult.Frame.Profile.CurrentInstrument, false);
                    isHighScore = currentHighScore != null &&
                        playerScoreRecord.GameRecordId == currentHighScore.GameRecordId;
                }

                playerScoreCards[i] = new PlayerScoreCard
                {
                    // If it's null, technically it is a high score but who knows what the hell happened so don't show that
                    // This compares ids, so imported replays should never be the high score.
                    IsHighScore = isHighScore,
                    Player = new YargPlayer(playerResult.Frame, data),
                    Stats = playerResult.ResultStats,
                    IsReplay = playerEntry.IsReplayPlayer
                };
            }

            GlobalVariables.State.ScoreScreenStats = new ScoreScreenStats
            {
                PlayerScores = playerScoreCards,
                BandScore = _entry.BandScore,
                BandStars = (int) _entry.BandStars,
                ReplayInfo = _entry,
                ReplayWasConsistent = results.All(r => r.Passed),
            };
            // Go to the score screen
            GlobalVariables.Instance.LoadScene(SceneIndex.Score);
        }

        public void ExportReplay()
        {
            _entry ??= LoadReplay("Cannot Export Replay");
            if (_entry == null)
            {
                return;
            }

            // Ask the user for an ending location
            FileExplorerHelper.OpenSaveFile(null, _entry!.ReplayName, "replay", path => File.Copy(_entry.FilePath, path, true));
        }

        public override void PlayWithReplayClick()
        {
            if (_songEntry == null)
            {
                DialogManager.Instance.ShowMessage("Unavailable Song", "A song compatible with the selected play is not present in your library! Most likely deleted!");
                return;
            }

            _entry ??= LoadReplay("Cannot Play Replay");
            if (_entry == null)
            {
                return;
            }

            PlayWithReplay(_entry, _songEntry);
        }

        public override GameInfo? GetGameInfo()
        {
            return _gameInfo;
        }
        //TODO: This method needs to be localized
        private ReplayInfo? LoadReplay(string messageBoxTitle)
        {
            if (_record == null)
            {
                YargLogger.LogDebug("Do not use this function with a non-gamerecord view");
                return null;
            }

            if (_record.ReplayFileName == null)
            {
                DialogManager.Instance.ShowMessage(messageBoxTitle, "This playthrough did not generate an accompanying replay!");
                return null;
            }

            // Get the replay path, mirroring the serialization code
            var path = Path.Combine(ScoreContainer.ScoreReplayDirectory, _record.ReplayFileName + ".replay");

            if (!File.Exists(path))
            {
                DialogManager.Instance.ShowMessage(messageBoxTitle, "The replay for this song does not exist! It has probably been deleted!");
                return null;
            }

            // Read
            var (result, entry) = ReplayIO.TryReadMetadata(path);
            if (result != ReplayReadResult.Valid)
            {
                string message = result switch
                {
                    ReplayReadResult.MetadataOnly => "The replay for this song cannot be played with this version of YARG",
                    ReplayReadResult.InvalidVersion => "The replay for this song has an invalid version and is most likely corrupted or *very* out of date",
                    ReplayReadResult.DataMismatch => "The replay data for this song does not match the expected data.",
                    ReplayReadResult.FileNotFound => "The replay for this song does not exist! It has probably been deleted!",
                    ReplayReadResult.NotAReplay => "A file was found, but it is not a replay.",
                    ReplayReadResult.Corrupted => "The replay for this song is corrupted (checksum does not match)",
                    _ => "The replay for this song is most likely corrupted, or out of date!"
                };

                DialogManager.Instance.ShowMessage(messageBoxTitle, message);
                return null;
            }

            // Compare hashes
            var databaseHash = HashWrapper.Create(_record.ReplayChecksum);
            if (!entry.ReplayChecksum.Equals(databaseHash))
            {
                DialogManager.Instance.ShowMessage(messageBoxTitle, "The replay's hash does not match the hash present in the database! Was the database modified?");
                return null;
            }

            ReplayContainer.AddEntry(entry);
            return entry;
        }
    }
}