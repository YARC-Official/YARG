#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ManagedBass;
using UnityEditor;
using UnityEngine;
using YARG.Audio.BASS;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Input;
using YARG.Playback;
using YARG.Settings;
using YARG.Song;

namespace YARG.Editor
{
    public sealed partial class AudioDebugWindow
    {
        private void InitializeLoadedSong(string songName, string sourcePath)
        {
            if (_bassSong == null)
            {
                return;
            }

            _audioSynchronizer = new AudioSynchronizer(_bassSong);
            _bassSong.SetReadAheadBuffer(_readAheadBufferMs);
            _bassSong.SetOutputLatency(_audioCalibrationMs / 1000.0);
            _bassSong.SetPosition(0);
            _bassSong.SongEnd += OnSongEnd;
            _loadedSongName = songName;
            _sourcePath = sourcePath;
            _playbackClock = 0;
            _simulatedClockDisturbance = 0;
            _simulatedClockDriftPercent = 0;
            _inputTimeOffset = InputManager.CurrentInputTime;
            _samples.Clear();
            _viewEndTime = -1;
            AddRecentPath(sourcePath);
            ResetStemControls();
        }

        private void LoadAudioFile(string filePath)
        {
            EnsureAudioInitialized();
            DisposeSong();

            var mixer = GlobalAudioHandler.LoadCustomFile(filePath, _playbackSpeed, _volume, normalize: false, SongStem.Song);
            _bassSong = mixer as BassSong;

            if (_bassSong != null)
            {
                InitializeLoadedSong(Path.GetFileName(filePath), filePath);
            }
            else
            {
                EditorUtility.DisplayDialog("Audio Load Failed", $"Failed to create mixer for audio file:\n{filePath}", "OK");
            }
        }

        private void LoadSongFolder(string folderPath)
        {
            EnsureAudioInitialized();
            DisposeSong();

            string songName = Path.GetFileName(folderPath);
            var mixer = GlobalAudioHandler.CreateMixer(songName, _playbackSpeed, _volume, clampStemVolume: false, normalize: false);
            if (mixer == null)
            {
                EditorUtility.DisplayDialog("Mixer Creation Failed", "Failed to allocate BASS StemMixer.", "OK");
                return;
            }

            string[] subFiles = Directory.GetFiles(folderPath);
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in subFiles)
            {
                fileMap[Path.GetFileName(file)] = file;
            }

            bool addedAny = false;
            foreach (string stem in IniAudio.SupportedStems)
            {
                var stemEnum = AudioHelpers.SupportedStems[stem];
                foreach (string format in IniAudio.SupportedFormats)
                {
                    string stemFileName = stem + format;
                    if (fileMap.TryGetValue(stemFileName, out string filePath))
                    {
                        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                        if (mixer.AddChannel(stream, stemEnum))
                        {
                            addedAny = true;
                            break;
                        }

                        stream.Dispose();
                    }
                }
            }

            if (!addedAny)
            {
                foreach (string file in subFiles)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (IniAudio.SupportedFormats.Contains(ext))
                    {
                        var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                        if (mixer.AddChannel(stream, SongStem.Song))
                        {
                            addedAny = true;
                            break;
                        }

                        stream.Dispose();
                    }
                }
            }

            if (addedAny)
            {
                _bassSong = mixer as BassSong;
                if (_bassSong != null)
                {
                    InitializeLoadedSong(songName, folderPath);
                }
            }
            else
            {
                mixer.Dispose();
                EditorUtility.DisplayDialog("Audio Load Failed", $"No supported audio stem files found in:\n{folderPath}", "OK");
            }
        }

        private void LoadSongEntry(SongEntry entry)
        {
            EnsureAudioInitialized();
            DisposeSong();

            var mixer = entry.LoadAudio(_playbackSpeed, _volume, SettingsManager.Settings?.CensorMatureContent.Value ?? false);
            _bassSong = mixer as BassSong;

            if (_bassSong != null)
            {
                InitializeLoadedSong($"{entry.Artist.Original} - {entry.Name.Original}", entry.ActualLocation);
            }
            else
            {
                EditorUtility.DisplayDialog("Audio Load Failed", $"Failed to load audio for song entry:\n{entry.Name.Original}", "OK");
            }
        }

        private void LoadRecentPaths()
        {
            string raw = EditorPrefs.GetString(RECENT_PATHS_KEY, string.Empty);
            _recentPaths = string.IsNullOrEmpty(raw)
                ? new List<string>()
                : raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private void AddRecentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            _recentPaths.Remove(path);
            _recentPaths.Insert(0, path);
            if (_recentPaths.Count > MAX_RECENT_PATHS)
            {
                _recentPaths.RemoveRange(MAX_RECENT_PATHS, _recentPaths.Count - MAX_RECENT_PATHS);
            }

            EditorPrefs.SetString(RECENT_PATHS_KEY, string.Join("|", _recentPaths));
        }

    }
}
