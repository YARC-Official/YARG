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
        private void DrawOutputRoutingCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string currentDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
                var currentMode = GlobalAudioHandler.GetOutputMode(currentDevice);
                bool isAsio = currentMode == AudioOutputMode.Asio;
                bool isWasapi = currentMode == AudioOutputMode.WasapiExclusive;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Output Routing & Driver", EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();

                    var prevBg = GUI.backgroundColor;
                    if (isAsio)
                    {
                        GUI.backgroundColor = new Color(0.2f, 0.75f, 1f, 1f);
                        GUILayout.Label(" ASIO (EXCLUSIVE) ", EditorStyles.helpBox, GUILayout.Height(18));
                    }
                    else if (isWasapi)
                    {
                        GUI.backgroundColor = new Color(0.85f, 0.45f, 1f, 1f);
                        GUILayout.Label(" WASAPI (EXCLUSIVE) ", EditorStyles.helpBox, GUILayout.Height(18));
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f, 1f);
                        GUILayout.Label(" SHARED (SYSTEM) ", EditorStyles.helpBox, GUILayout.Height(18));
                    }
                    GUI.backgroundColor = prevBg;

                    GUILayout.Space(4);

                    if (GUILayout.Button("Switch Device ▾", EditorStyles.miniButton, GUILayout.Width(125), GUILayout.Height(18)))
                    {
                        ShowDeviceMenu();
                    }
                }

                EditorGUILayout.Space(4);

                int sampleRate = Bass.Info.SampleRate;
                int speakerCount = Bass.Info.SpeakerCount;
                double latencyMs = GlobalAudioHandler.PlaybackLatency;
                var bufferInfo = GlobalAudioHandler.GetOutputBufferInfo();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Active Device:", GUILayout.Width(90));
                    EditorGUILayout.SelectableLabel(currentDevice, EditorStyles.boldLabel, GUILayout.Height(18));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Format: {sampleRate} Hz, {speakerCount} ch", EditorStyles.miniLabel, GUILayout.Width(150));
                    EditorGUILayout.LabelField($"Latency: {latencyMs:F1} ms", EditorStyles.miniLabel, GUILayout.Width(110));

                    if (bufferInfo is { } info && info.PreferredLength > 0)
                    {
                        int bufferSamples = info.PreferredLength;
                        double bufferMs = info.SampleRate > 0 ? (bufferSamples * 1000.0 / info.SampleRate) : 0;
                        EditorGUILayout.LabelField($"Buffer: {bufferSamples} spl ({bufferMs:F1} ms)", EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (isAsio)
                    {
                        if (GUILayout.Button("Open ASIO Control Panel", EditorStyles.miniButton, GUILayout.Height(20)))
                        {
                            OpenAsioControlPanel();
                        }
                    }

                    if (GUILayout.Button("Restart / Re-link Output", EditorStyles.miniButton, GUILayout.Height(20)))
                    {
                        RestartOutput();
                    }

                    if (GUILayout.Button("🎯 Metronome & Routing Test", EditorStyles.miniButton, GUILayout.Height(20)))
                    {
                        _selectedBottomTab = 5;
                        Repaint();
                    }
                }

                if (!string.IsNullOrEmpty(_deviceStatusMessage) && (EditorApplication.timeSinceStartup - _lastDeviceStatusTime < 5.0))
                {
                    EditorGUILayout.Space(2);
                    var msgType = _deviceStatusIsError ? MessageType.Error : MessageType.Info;
                    EditorGUILayout.HelpBox(_deviceStatusMessage, msgType);
                }
            }
        }

        private void DrawSongSyncCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _modelSongSync = EditorGUILayout.ToggleLeft(" Enable Song Sync", _modelSongSync, EditorStyles.boldLabel, GUILayout.Width(150));

                    GUILayout.FlexibleSpace();

                    if (_bassSong != null && _modelSongSync)
                    {
                        var state = _audioSynchronizer?.State ?? AudioSynchronizer.SyncState.Idle;
                        float adj = _audioSynchronizer?.EffectiveAdjustment ?? 0f;
                        var prevBg = GUI.backgroundColor;
                        if (state == AudioSynchronizer.SyncState.Correcting)
                        {
                            GUI.backgroundColor = new Color(1f, 0.7f, 0.15f, 1f);
                            GUILayout.Label($" CORRECTING ({(adj * 100):+0.00;-0.00}%) ", EditorStyles.helpBox, GUILayout.Height(18));
                        }
                        else if (state == AudioSynchronizer.SyncState.Settling)
                        {
                            GUI.backgroundColor = new Color(0.3f, 0.75f, 1f, 1f);
                            GUILayout.Label(" SETTLING ", EditorStyles.helpBox, GUILayout.Height(18));
                        }
                        else
                        {
                            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.35f, 1f);
                            GUILayout.Label(" LOCKED (IDLE) ", EditorStyles.helpBox, GUILayout.Height(18));
                        }
                        GUI.backgroundColor = prevBg;
                    }
                }

                EditorGUILayout.Space(3);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Calibration:", GUILayout.Width(75));
                    float newCal = EditorGUILayout.Slider(_audioCalibrationMs, -200f, 200f);
                    if (Mathf.Abs(newCal - _audioCalibrationMs) > 0.01f)
                    {
                        _audioCalibrationMs = newCal;
                        _bassSong?.SetOutputLatency(_audioCalibrationMs / 1000.0);
                    }

                    if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(52)))
                    {
                        _audioCalibrationMs = 0f;
                        _bassSong?.SetOutputLatency(0.0);
                    }
                }

                if (_bassSong != null)
                {
                    float worstDelta = _audioSynchronizer?.WorstDelta * 1000f ?? 0f;
                    float effectiveSpeed = _playbackSpeed + (_audioSynchronizer?.EffectiveAdjustment ?? 0f);
                    float adjustment = _audioSynchronizer?.EffectiveAdjustment ?? 0f;

                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        var statStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.7f, 0.75f, 0.8f) } };
                        var valStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.35f, 0.9f, 0.5f) } };

                        EditorGUILayout.LabelField("Worst Error Δ:", statStyle, GUILayout.Width(78));
                        EditorGUILayout.LabelField($"{worstDelta:+0.0;-0.0;0.0} ms", valStyle, GUILayout.Width(50));
                        GUILayout.Space(8);

                        EditorGUILayout.LabelField("Effective Rate:", statStyle, GUILayout.Width(80));
                        EditorGUILayout.LabelField($"{effectiveSpeed:F3}x", valStyle, GUILayout.Width(45));
                        GUILayout.Space(8);

                        EditorGUILayout.LabelField("Sync Adj:", statStyle, GUILayout.Width(52));
                        EditorGUILayout.LabelField($"{(adjustment * 100):+0.0;-0.0}%", valStyle, GUILayout.Width(45));
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.Space(3);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Step Jump:", GUILayout.Width(75));
                        if (GUILayout.Button("-100ms", EditorStyles.miniButtonLeft, GUILayout.Width(52))) _simulatedClockDisturbance -= 0.100;
                        if (GUILayout.Button("-50ms", EditorStyles.miniButtonMid, GUILayout.Width(46))) _simulatedClockDisturbance -= 0.050;
                        if (GUILayout.Button("-20ms", EditorStyles.miniButtonMid, GUILayout.Width(46))) _simulatedClockDisturbance -= 0.020;
                        if (GUILayout.Button("0ms", EditorStyles.miniButtonMid, GUILayout.Width(36))) _simulatedClockDisturbance = 0;
                        if (GUILayout.Button("+20ms", EditorStyles.miniButtonMid, GUILayout.Width(46))) _simulatedClockDisturbance += 0.020;
                        if (GUILayout.Button("+50ms", EditorStyles.miniButtonMid, GUILayout.Width(46))) _simulatedClockDisturbance += 0.050;
                        if (GUILayout.Button("+100ms", EditorStyles.miniButtonRight, GUILayout.Width(52))) _simulatedClockDisturbance += 0.100;

                        GUILayout.Space(6);
                        string distText = $"Δ: {_simulatedClockDisturbance * 1000:+0.0;-0.0;0.0}ms";
                        EditorGUILayout.LabelField(distText, EditorStyles.miniLabel, GUILayout.Width(55));

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("⚡ GC Collect", EditorStyles.miniButton, GUILayout.Width(95)))
                        {
                            ForceGarbageCollection();
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Sim Drift:", GUILayout.Width(75));
                        _simulatedClockDriftPercent = EditorGUILayout.Slider(_simulatedClockDriftPercent, -2.0f, 2.0f);
                        if (GUILayout.Button("0%", EditorStyles.miniButton, GUILayout.Width(40)))
                        {
                            _simulatedClockDriftPercent = 0f;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Load a song to model input clock synchronization.", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private void DrawBufferDiagnosticsCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(120)))
            {
                EditorGUILayout.LabelField("Read-Ahead Buffer & Engine Health", EditorStyles.boldLabel);

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Buffer Size:", GUILayout.Width(75));
                    int newBuffer = EditorGUILayout.IntSlider(_readAheadBufferMs, 0, 2000);
                    if (newBuffer != _readAheadBufferMs)
                    {
                        _readAheadBufferMs = newBuffer;
                        ApplyReadAheadBuffer(_readAheadBufferMs);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Presets:", GUILayout.Width(55));
                    DrawBufferPresetPill(0);
                    DrawBufferPresetPill(50);
                    DrawBufferPresetPill(100);
                    DrawBufferPresetPill(250);
                    DrawBufferPresetPill(500);
                    DrawBufferPresetPill(1000);
                }

                EditorGUILayout.Space(4);

                if (_bassSong != null)
                {
                    var stats = _bassSong.GetReadAheadStats();
                    double tempoLatency = _bassSong.GetTempoStreamLatency() * 1000.0;

                    if (stats.TargetFrames > 0)
                    {
                        float fillRatio = Mathf.Clamp01((float) stats.QueuedFrames / stats.TargetFrames);
                        Rect progressRect = GUILayoutUtility.GetRect(100, 16);
                        EditorGUI.ProgressBar(progressRect, fillRatio, $"Buffer Fill: {fillRatio * 100f:F0}% ({stats.QueuedFrames} / {stats.TargetFrames} frames)");
                    }

                    EditorGUILayout.Space(2);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var latencyColor = tempoLatency < 5.0 ? new Color(0.25f, 0.85f, 0.35f) : (tempoLatency < 25.0 ? new Color(0.95f, 0.75f, 0.2f) : new Color(1f, 0.35f, 0.35f));
                        var latencyStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = latencyColor } };
                        EditorGUILayout.LabelField($"Active: {_bassSong.ReadAheadBufferLength} ms  |  Latency: {tempoLatency:F1} ms", latencyStyle);

                        if (stats.UnderrunEvents > 0)
                        {
                            var warningStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(1f, 0.35f, 0.35f) } };
                            EditorGUILayout.LabelField($"⚠️ {stats.UnderrunEvents} Underruns", warningStyle, GUILayout.Width(95));
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Underruns: 0", EditorStyles.miniLabel, GUILayout.Width(75));
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No active audio stream.", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private void DrawBassBufferSettingsCard()
        {
            string currentDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
            var currentMode = GlobalAudioHandler.GetOutputMode(currentDevice);
            bool isExclusive = currentMode != AudioOutputMode.Shared;
            int devicePeriod = Math.Max(1, Bass.GetConfig(Configuration.DevicePeriod));

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            bool supportsDeviceBuffer = !isExclusive;
#else
            bool supportsDeviceBuffer = false;
#endif

            if (!_bassBufferSettingsInitialized)
            {
                _bassBufferSettingsInitialized = true;
                _bassUpdatePeriodMs = Mathf.Clamp(Bass.UpdatePeriod, MIN_BASS_UPDATE_PERIOD_MS,
                    MAX_BASS_UPDATE_PERIOD_MS);
                _bassDeviceBufferLengthMs = Bass.DeviceBufferLength > 0
                    ? Bass.DeviceBufferLength
                    : 2 * devicePeriod;
            }

            int minDeviceBuffer = devicePeriod;
            int maxDeviceBuffer = Math.Max(MAX_BASS_DEVICE_BUFFER_MS, minDeviceBuffer);
            _bassDeviceBufferLengthMs = Mathf.Clamp(_bassDeviceBufferLengthMs, minDeviceBuffer, maxDeviceBuffer);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(120)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("BASS Timing & Device Buffer", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"Device period: {devicePeriod} ms", EditorStyles.miniLabel,
                        GUILayout.Width(125));
                }

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Update Period:", GUILayout.Width(105));
                    _bassUpdatePeriodMs = EditorGUILayout.IntSlider(_bassUpdatePeriodMs,
                        MIN_BASS_UPDATE_PERIOD_MS, MAX_BASS_UPDATE_PERIOD_MS);
                    EditorGUILayout.LabelField($"{_bassUpdatePeriodMs} ms", EditorStyles.miniLabel,
                        GUILayout.Width(48));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Device Buffer:", GUILayout.Width(105));
                    using (new EditorGUI.DisabledScope(!supportsDeviceBuffer))
                    {
                        _bassDeviceBufferLengthMs = EditorGUILayout.IntSlider(_bassDeviceBufferLengthMs,
                            minDeviceBuffer, maxDeviceBuffer);
                    }
                    EditorGUILayout.LabelField(supportsDeviceBuffer ? $"{_bassDeviceBufferLengthMs} ms" : "Driver",
                        EditorStyles.miniLabel, GUILayout.Width(48));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Device presets:", GUILayout.Width(105));
                    using (new EditorGUI.DisabledScope(!supportsDeviceBuffer))
                    {
                        DrawBassDeviceBufferPreset(devicePeriod);
                        DrawBassDeviceBufferPreset(2 * devicePeriod);
                        DrawBassDeviceBufferPreset(3 * devicePeriod);
                        DrawBassDeviceBufferPreset(4 * devicePeriod);
                    }
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool updatePeriodChanged = _bassUpdatePeriodMs != Bass.UpdatePeriod;
                    bool deviceBufferChanged = supportsDeviceBuffer &&
                        _bassDeviceBufferLengthMs != Bass.DeviceBufferLength;
                    bool hasChanges = updatePeriodChanged || deviceBufferChanged;

                    using (new EditorGUI.DisabledScope(!hasChanges))
                    {
                        if (GUILayout.Button("Apply BASS Settings", EditorStyles.miniButton, GUILayout.Width(140)))
                        {
                            ApplyBassBufferSettings(supportsDeviceBuffer);
                        }
                    }

                    if (GUILayout.Button("Defaults", EditorStyles.miniButton, GUILayout.Width(58)))
                    {
                        _bassUpdatePeriodMs = MIN_BASS_UPDATE_PERIOD_MS;
                        _bassDeviceBufferLengthMs = 2 * devicePeriod;
                    }

                    GUILayout.FlexibleSpace();
                    string deviceNote = supportsDeviceBuffer
                        ? "Device buffer applies to next BASS device initialization."
                        : "Output driver controls device buffer.";
                    EditorGUILayout.LabelField(deviceNote, EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private void DrawBassDeviceBufferPreset(int multiplier)
        {
            int devicePeriod = Math.Max(1, Bass.GetConfig(Configuration.DevicePeriod));
            int value = multiplier * devicePeriod;
            if (GUILayout.Button($"{multiplier}× ({value})", EditorStyles.miniButton, GUILayout.Width(64)))
            {
                _bassDeviceBufferLengthMs = value;
            }
        }

        private void ApplyBassBufferSettings(bool supportsDeviceBuffer)
        {
            Bass.UpdatePeriod = _bassUpdatePeriodMs;
            if (supportsDeviceBuffer)
            {
                Bass.DeviceBufferLength = _bassDeviceBufferLengthMs;
            }

            _bassUpdatePeriodMs = Bass.UpdatePeriod;
            _bassDeviceBufferLengthMs = Bass.DeviceBufferLength > 0
                ? Bass.DeviceBufferLength
                : _bassDeviceBufferLengthMs;
            _deviceStatusMessage = supportsDeviceBuffer
                ? "BASS timing settings applied. Device buffer takes effect when a BASS device is initialized."
                : "BASS update period applied. Device buffer is controlled by output driver.";
            _deviceStatusIsError = false;
            _lastDeviceStatusTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

    }
}
