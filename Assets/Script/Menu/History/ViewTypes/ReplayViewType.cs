using Cysharp.Threading.Tasks;
using System.Drawing.Drawing2D;
using System.IO;
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
#nullable enable
    public class ReplayViewType : ViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;

        public override bool UseFullContainer => true;

        // Non-readonly as we may find it later
        private ReplayInfo? _entry;
        private readonly GameRecord? _record;

        private readonly SongEntry? _songEntry;
        private readonly string _songName;
        private readonly string _artistName;

        private readonly GameInfo _gameInfo;
        private readonly Sprite? _sprite;

        public ReplayViewType(GameRecord record)
        {
            _record = record;
            if (SongContainer.SongsByHash.TryGetValue(HashWrapper.Create(record.SongChecksum), out var songs))
            {
                _songEntry = songs[0];
                _sprite = SongSources.SourceToIcon(_songEntry.Source);
            }
            _gameInfo.BandScore = _record.BandScore;
            _gameInfo.BandStars = _record.BandStars;
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

#nullable enable
        public override Sprite? GetIcon()
#nullable disable
        {
            // TODO: Show "song missing" icon instead when _songEntry is null
            return _sprite;
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

        public void ExportReplay()
        {
            if (_entry == null)
            {
                if (!LoadReplay("Cannot Export Replay"))
                {
                    return;
                }
            }

            // Ask the user for an ending location
            FileExplorerHelper.OpenSaveFile(null, _entry.ReplayName, "replay", path => File.Copy(_entry.FilePath, path, true));
        }

        private async UniTaskVoid PlayReplay()
        {
            if (_entry == null)
            {
                if (!LoadReplay("Cannot Play Replay"))
                {
                    return;
                }
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

            LoadIntoReplay(_entry, _songEntry);
        }

        private void AnalyzeReplay()
        {
            var chart = _songEntry.LoadChart();
            if (chart == null)
            {
                YargLogger.LogError("Failed to load chart");
                return;
            }

            var (result, data) = ReplayIO.TryLoadData(_entry);
            if (result != ReplayReadResult.Valid)
            {
                YargLogger.LogFormatError("Failed to load replay. {0}", result);
                return;
            }

            var results = ReplayAnalyzer.AnalyzeReplay(chart, data);
            for(int i = 0; i < results.Length; i++)
            {
                var analysisResult = results[i];

                var profile = data.Frames[i].PlayerInfo.Profile;
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
            return _gameInfo;
        }

        private bool LoadReplay(string messageBoxTitle)
        {
            if (_record.ReplayFileName == null)
            {
                DialogManager.Instance.ShowMessage(messageBoxTitle, "A replay was not created for this score");
                return false;
            }

            // Get the replay path
            var path = Path.Combine(ScoreContainer.ScoreReplayDirectory, _record.ReplayFileName);
            // Accounts the change to ReplayName to remove the ".replay"
            path = Path.ChangeExtension(path, ".replay");
            if (!File.Exists(path))
            {
                DialogManager.Instance.ShowMessage(messageBoxTitle, "The replay for this song does not exist. It has probably been deleted.");
                return false;
            }

            // Read
            var (result, entry) = ReplayIO.TryReadMetadata(path);
            if (result != ReplayReadResult.Valid)
            {
                DialogManager.Instance.ShowMessage(messageBoxTitle, "The replay for this song is most likely corrupted, or out of date.");
                return false;
            }

            // Compare hashes
            var databaseHash = HashWrapper.Create(_record.ReplayChecksum);
            if (!entry.ReplayChecksum.Equals(databaseHash))
            {
                DialogManager.Instance.ShowMessage(messageBoxTitle, "The replay's hash does not match the hash present in the database. Was the database modified?");
                return false;
            }

            _entry = entry;
            ReplayContainer.AddEntry(entry);
            return true;
        }
    }
}