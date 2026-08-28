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
    public sealed partial class AudioDebugWindow : EditorWindow
    {
        [MenuItem("Window/YARG/Audio Debug Player")]
        [MenuItem("YARG/Debug/Audio Debug Player")]
        public static void Open()
        {
            var window = GetWindow<AudioDebugWindow>("Audio Debug");
            window.minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
            var pos = window.position;
            if (pos.height < DEFAULT_WINDOW_HEIGHT || pos.width < DEFAULT_WINDOW_WIDTH)
            {
                pos.width = Mathf.Max(pos.width, DEFAULT_WINDOW_WIDTH);
                pos.height = Mathf.Max(pos.height, DEFAULT_WINDOW_HEIGHT);
                window.position = pos;
            }
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            EditorApplication.update += OnEditorUpdate;
            EnsureAudioInitialized();
            LoadRecentPaths();
            RefreshAvailableMicrophones();
            EnsureDefaultMicSlot();

            if (SettingsManager.Settings != null)
            {
                _readAheadBufferMs = SettingsManager.Settings.PlaybackBufferLength.Value;
                _audioCalibrationMs = SettingsManager.Settings.AudioCalibration.Value;
                if (SettingsManager.Settings.MetronomeSound.Value != MetronomeSample.None)
                {
                    _testMetronomeSound = SettingsManager.Settings.MetronomeSound.Value;
                }
                _metronomeTargetChannel = SettingsManager.Settings.OutputChannelMetronome.Value;
                _metronomeVolume = (float) SettingsManager.Settings.MetronomeVolume.Value;
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _metronomeLoopRunning = false;
            StopDriftTest();
            DisposeSong();
            DisposeAllMicSlots();
            RestoreGcMode();
        }

        private void OnDestroy()
        {
            _metronomeLoopRunning = false;
            StopDriftTest();
            DisposeSong();
            DisposeAllMicSlots();
            RestoreGcMode();
        }

        private void UpdateMonitoringGcState()
        {
            bool anySolo = _micSlots.Any(s => s.Solo);
            bool isMonitoringActive = _micSlots.Any(s =>
                s.ActiveDevice != null &&
                (anySolo ? s.Solo && s.MonitoringVolume > 0f : !s.Mute && s.MonitoringVolume > 0f));

            try
            {
                if (isMonitoringActive && !_gcDisabledForMonitoring)
                {
                    UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Disabled;
                    _gcDisabledForMonitoring = true;
                }
                else if (!isMonitoringActive && _gcDisabledForMonitoring)
                {
                    UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Enabled;
                    _gcDisabledForMonitoring = false;
                }
            }
            catch (Exception)
            {
                // Setting GC mode is not supported by Unity inside the Editor; ignore safely
            }
        }

        private void RestoreGcMode()
        {
            if (_gcDisabledForMonitoring)
            {
                try
                {
                    UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Enabled;
                }
                catch (Exception)
                {
                }
                _gcDisabledForMonitoring = false;
            }
        }

        private static void ForceGarbageCollection()
        {
            var previousMode = UnityEngine.Scripting.GarbageCollector.GCMode;
            bool changedMode = false;

            try
            {
                if (previousMode == UnityEngine.Scripting.GarbageCollector.Mode.Disabled)
                {
                    UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Manual;
                    changedMode = true;
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            finally
            {
                if (changedMode)
                {
                    UnityEngine.Scripting.GarbageCollector.GCMode = previousMode;
                }
            }
        }

        private static void EnsureAudioInitialized()
        {
            PathHelper.Init();
            StemSettings.ApplySettings = true;

            if (!SettingsManager.SettingContainer.IsInitialized)
            {
                SettingsManager.LoadStartupSettings();
                GlobalAudioHandler.Initialize<BassAudioManager>();
                SettingsManager.LoadSettings();
            }
            else
            {
                GlobalAudioHandler.Initialize<BassAudioManager>();
            }

            StemSettings.ApplySettings = true;
        }

        private void DisposeSong()
        {
            if (_bassSong != null)
            {
                _bassSong.SongEnd -= OnSongEnd;
                _bassSong.Dispose();
                _bassSong = null;
                _audioSynchronizer = null;
            }

            foreach (var stem in ALL_STEMS)
            {
                GlobalAudioHandler.SetReverbSetting(stem, false);
            }
            _stemReverbs.Clear();

            if (_smoothedFft != null && _peakFft != null)
            {
                Array.Clear(_smoothedFft, 0, _smoothedFft.Length);
                for (int i = 0; i < _peakFft.Length; i++)
                {
                    _peakFft[i] = _fftMinDb;
                }
            }
            for (int b = 0; b < _fftBands.Length; b++)
            {
                _fftBands[b].CurrentDb = _fftMinDb;
                _fftBands[b].PeakDb = _fftMinDb;
            }
            _dominantFrequencyHz = 0f;
            _dominantDb = _fftMinDb;
            _dominantNoteName = "--";
            _dominantCents = 0f;
            _spectralCentroidHz = 0f;
        }

        private void OnSongEnd()
        {
            if (_isDriftTestRunning && _bassSong != null)
            {
                _driftAudioLoopCount++;
                _bassSong.SetPosition(0);
                PlaySong();
            }
            Repaint();
        }

        private void OnEditorUpdate()
        {
            GlobalAudioHandler.Update();

            double now = EditorApplication.timeSinceStartup;
            double dt = now - _lastUpdateTime;
            _lastUpdateTime = now;

            UpdateMicrophone(now, dt);
            UpdateSongPlayback(now, dt);
            UpdateDriftTest(now, dt);
            UpdateFft(now, dt);
            UpdateMetronome(now, dt);
            UpdateMicRecordAndPlayback(now, dt);
        }
        private void OnGUI()
        {
            try
            {
                EnsureAudioInitialized();
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"Audio initialization status: {ex.Message}", MessageType.Warning);
            }

            HandleDragAndDrop();
            HandleKeyboardShortcuts();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_mainScroll))
            {
                _mainScroll = scroll.scrollPosition;

                EditorGUILayout.Space(6);
                DrawTopBar();

                if (_showLibrarySection)
                {
                    EditorGUILayout.Space(6);
                    DrawLibraryDrawer();
                }

                EditorGUILayout.Space(6);
                DrawTransportBar();

                EditorGUILayout.Space(6);
                DrawOscilloscopeCard();

                EditorGUILayout.Space(6);
                DrawBottomDashboard();

                EditorGUILayout.Space(8);
            }

            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }
        }
    }
}
