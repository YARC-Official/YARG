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
        #region Metronome & Multi-Channel Routing Test Bench

        private void UpdateMetronome(double now, double dt)
        {
            float decayDb = (float) (dt * 65f);
            for (int i = 0; i < 4; i++)
            {
                if (_channelPairPeaks[i] > -96f)
                {
                    _channelPairPeaks[i] = Mathf.Max(-96f, _channelPairPeaks[i] - decayDb);
                }
                if (now - _channelPairPeakHoldTime[i] > 0.8)
                {
                    _channelPairPeakHold[i] = Mathf.Max(-96f, _channelPairPeakHold[i] - (decayDb * 1.5f));
                }
            }

            if (_metronomeLoopRunning)
            {
                double beatInterval = 60.0 / Mathf.Clamp(_metronomeBpm, 30f, 300f);
                if (_nextMetronomeBeatTime <= 0 || now >= _nextMetronomeBeatTime)
                {
                    if (_nextMetronomeBeatTime <= 0 || (now - _nextMetronomeBeatTime) > beatInterval * 2)
                    {
                        _nextMetronomeBeatTime = now;
                    }
                    _nextMetronomeBeatTime += beatInterval;

                    bool isHi = _metronomeCurrentBeat == 0;
                    TriggerMetronomeClick(isHi);

                    _metronomeCurrentBeat = (_metronomeCurrentBeat + 1) % Math.Max(1, _metronomeBeatsPerBar);
                    Repaint();
                }
            }

            if (_selectedBottomTab == 5)
            {
                Repaint();
            }
        }

        private void TriggerMetronomeClick(bool isHi)
        {
            _lastMetronomeClickTime = EditorApplication.timeSinceStartup;
            _lastMetronomeClickIsHi = isHi;
            _totalMetronomeClicks++;

            try
            {
                EnsureAudioInitialized();

                var pitch = isHi ? MetronomePitch.Hi : MetronomePitch.Lo;
                GlobalAudioHandler.PlayMetronomeSoundEffect(_testMetronomeSound, pitch);

                int targetPair = _metronomeTargetChannel switch
                {
                    3 => 1,
                    5 => 2,
                    7 => 3,
                    _ => 0
                };

                _channelPairPeaks[targetPair] = 0f;
                _channelPairPeakHold[targetPair] = 0f;
                _channelPairPeakHoldTime[targetPair] = EditorApplication.timeSinceStartup;

                if (_headphoneAuditionMode && targetPair != 0)
                {
                    GlobalAudioHandler.PlayMetronomeSoundEffectToChannel(_testMetronomeSound, pitch, 1);
                    _channelPairPeaks[0] = Mathf.Max(_channelPairPeaks[0], -4f);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Metronome trigger failed: {ex.Message}");
            }
        }

        private void SetMetronomeTargetChannel(int channelId)
        {
            _metronomeTargetChannel = channelId;
            if (SettingsManager.Settings != null)
            {
                SettingsManager.Settings.OutputChannelMetronome.Value = channelId;
            }
            else
            {
                GlobalAudioHandler.SetOutputChannel(SongStem.Metronome, channelId == -1 ? 1 : channelId);
            }
        }

        private void HandleTapTempo()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_tapTimes[0] > 0 && (now - _tapTimes[(_tapIndex + 3) % 4]) > 2.5)
            {
                Array.Clear(_tapTimes, 0, _tapTimes.Length);
                _tapIndex = 0;
            }

            _tapTimes[_tapIndex] = now;
            _tapIndex = (_tapIndex + 1) % 4;

            int validTaps = 0;
            double totalDelta = 0;
            for (int i = 1; i < 4; i++)
            {
                int prev = i - 1;
                if (_tapTimes[prev] > 0 && _tapTimes[i] > 0 && _tapTimes[i] > _tapTimes[prev])
                {
                    totalDelta += (_tapTimes[i] - _tapTimes[prev]);
                    validTaps++;
                }
            }

            if (validTaps > 0)
            {
                double avgDelta = totalDelta / validTaps;
                if (avgDelta > 0.1)
                {
                    _metronomeBpm = Mathf.Clamp((float) Math.Round(60.0 / avgDelta), 40f, 240f);
                }
            }
        }

        private void DrawMetronomeRoutingTestCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawMetronomeTopStatusBar();

                EditorGUILayout.Space(4);

                EditorGUILayout.HelpBox(
                    "How to run: Click 'Start Metronome' (or test pads) to generate clicks, then select a channel pair (e.g. Pair 2 for Drummer In-Ear) " +
                    "to verify routing and channel isolation on the VU meters. Enable 'Stereo Headphone Audition Mode' to preview multi-channel routing on standard 2-channel headphones.",
                    MessageType.Info);

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetronomeSequencerColumn();

                    GUILayout.Space(10);

                    DrawMetronomeRoutingAndMeterColumn();
                }
            }
        }

        private void DrawMetronomeTopStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = _metronomeLoopRunning ? new Color(0.2f, 0.85f, 0.45f, 1f) : new Color(0.45f, 0.5f, 0.55f, 1f);
                GUILayout.Label(_metronomeLoopRunning ? " ● PLAYING " : " ⏹ IDLE ", EditorStyles.miniButton, GUILayout.Width(85), GUILayout.Height(18));
                GUI.backgroundColor = prevBg;

                GUILayout.Space(8);

                string channelDesc = _metronomeTargetChannel switch
                {
                    3 => "Aux / In-Ear (Ch 3-4)",
                    5 => "Center / Sub (Ch 5-6)",
                    7 => "Surround (Ch 7-8)",
                    _ => "Main Front (Ch 1-2)"
                };

                var statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.85f, 0.88f, 0.92f) }
                };
                GUILayout.Label($"Route: {channelDesc}   •   Sample: {_testMetronomeSound}   •   Tempo: {_metronomeBpm:F0} BPM ({_metronomeBeatsPerBar}/4)", statusStyle, GUILayout.Height(18));

                GUILayout.FlexibleSpace();

                if (_headphoneAuditionMode)
                {
                    var audBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.2f, 0.75f, 1f, 1f);
                    GUILayout.Label(" 🎧 Auditioning in Stereo ", EditorStyles.helpBox, GUILayout.Height(18));
                    GUI.backgroundColor = audBg;
                    GUILayout.Space(4);
                }

                if (GUILayout.Button("Stop Audio", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    _metronomeLoopRunning = false;
                    _bassSong?.Pause();
                }
            }
        }

        private void DrawMetronomeSequencerColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(360)))
            {
                EditorGUILayout.LabelField("Metronome & Rhythm", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                // Hero Play / Stop Transport Button
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = _metronomeLoopRunning ? new Color(0.92f, 0.35f, 0.35f, 1f) : new Color(0.2f, 0.82f, 0.45f, 1f);
                var heroBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                string loopBtnText = _metronomeLoopRunning ? "⏹  Stop Metronome" : "▶  Start Metronome";
                if (GUILayout.Button(loopBtnText, heroBtnStyle, GUILayout.Height(32)))
                {
                    _metronomeLoopRunning = !_metronomeLoopRunning;
                    if (_metronomeLoopRunning)
                    {
                        _metronomeCurrentBeat = 0;
                        _nextMetronomeBeatTime = EditorApplication.timeSinceStartup;
                    }
                }
                GUI.backgroundColor = prevBg;

                EditorGUILayout.Space(8);

                // Tempo Section
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Tempo", EditorStyles.miniBoldLabel, GUILayout.Width(45));

                    if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        _metronomeBpm = Mathf.Clamp(_metronomeBpm - 1f, 40f, 240f);
                    }

                    _metronomeBpm = EditorGUILayout.Slider(_metronomeBpm, 40f, 240f);

                    if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        _metronomeBpm = Mathf.Clamp(_metronomeBpm + 1f, 40f, 240f);
                    }

                    GUILayout.Space(4);
                    if (GUILayout.Button("Tap", EditorStyles.miniButton, GUILayout.Width(42), GUILayout.Height(18)))
                    {
                        HandleTapTempo();
                    }
                }

                // Quick BPM presets
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawBpmPresetPill(60f);
                    DrawBpmPresetPill(90f);
                    DrawBpmPresetPill(120f);
                    DrawBpmPresetPill(140f);
                    DrawBpmPresetPill(160f);
                    DrawBpmPresetPill(180f);
                }

                EditorGUILayout.Space(8);

                // Time Signature & Beat Visualizer
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Time Signature", EditorStyles.miniBoldLabel, GUILayout.Width(95));
                    DrawTimeSigPill(4, "4/4");
                    DrawTimeSigPill(3, "3/4");
                    DrawTimeSigPill(6, "6/8");
                    DrawTimeSigPill(1, "1/4");
                }

                EditorGUILayout.Space(4);
                DrawMetronomeBeatVisualizer();

                EditorGUILayout.Space(10);

                // Sound Preset Selection
                EditorGUILayout.LabelField("Sound Sample", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetronomeSoundPill(MetronomeSample.Clap, "Clap");
                    DrawMetronomeSoundPill(MetronomeSample.Castanet, "Castanet");
                    DrawMetronomeSoundPill(MetronomeSample.Party, "Party");
                    DrawMetronomeSoundPill(MetronomeSample.Quartz, "Quartz");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetronomeSoundPill(MetronomeSample.Sine, "Sine");
                    DrawMetronomeSoundPill(MetronomeSample.Square, "Square");
                    DrawMetronomeSoundPill(MetronomeSample.Trashcan, "Trashcan");
                }

                EditorGUILayout.Space(8);

                // Volume slider with clean formatted percentage
                using (new EditorGUILayout.HorizontalScope())
                {
                    int volPct = Mathf.RoundToInt(_metronomeVolume * 100f);
                    EditorGUILayout.LabelField($"Volume: {volPct}%", EditorStyles.miniBoldLabel, GUILayout.Width(82));
                    EditorGUI.BeginChangeCheck();
                    float newVol = EditorGUILayout.Slider(_metronomeVolume, 0f, 2f);
                    if (EditorGUI.EndChangeCheck() || MathF.Abs(newVol - _metronomeVolume) > 0.01f)
                    {
                        _metronomeVolume = newVol;
                        if (SettingsManager.Settings != null)
                        {
                            SettingsManager.Settings.MetronomeVolume.Value = newVol;
                        }
                        GlobalAudioHandler.SetVolumeMultiplier(SongStem.Metronome, newVol);
                    }
                }

                EditorGUILayout.Space(6);

                // Manual Preview / Test Pads
                using (new EditorGUILayout.HorizontalScope())
                {
                    double hitAge = EditorApplication.timeSinceStartup - _lastMetronomeClickTime;
                    bool flashHi = hitAge < 0.15 && _lastMetronomeClickIsHi;
                    bool flashLo = hitAge < 0.15 && !_lastMetronomeClickIsHi;

                    var padPrevBg = GUI.backgroundColor;
                    GUI.backgroundColor = flashHi ? new Color(0.3f, 0.95f, 0.55f, 1f) : new Color(0.25f, 0.55f, 0.38f, 1f);
                    if (GUILayout.Button(flashHi ? "● Downbeat (Hi)" : "🔊 Test Downbeat", GUILayout.Height(24)))
                    {
                        TriggerMetronomeClick(true);
                    }

                    GUI.backgroundColor = flashLo ? new Color(0.35f, 0.85f, 1f, 1f) : new Color(0.28f, 0.48f, 0.65f, 1f);
                    if (GUILayout.Button(flashLo ? "● Upbeat (Lo)" : "🔉 Test Upbeat", GUILayout.Height(24)))
                    {
                        TriggerMetronomeClick(false);
                    }
                    GUI.backgroundColor = padPrevBg;
                }
            }
        }

        private void DrawMetronomeSoundPill(MetronomeSample sample, string label)
        {
            bool isSelected = _testMetronomeSound == sample;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.75f, 1f, 1f) : new Color(0.32f, 0.35f, 0.40f, 0.75f);
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(20)))
            {
                _testMetronomeSound = sample;
                if (SettingsManager.Settings != null)
                {
                    SettingsManager.Settings.MetronomeSound.Value = sample;
                }
                TriggerMetronomeClick(true);
            }
            GUI.backgroundColor = prevBg;
        }

        private void DrawBpmPresetPill(float bpm)
        {
            bool isSelected = MathF.Abs(_metronomeBpm - bpm) < 0.5f;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.85f, 0.5f, 1f) : new Color(0.32f, 0.35f, 0.40f, 0.75f);
            if (GUILayout.Button($"{bpm:F0}", EditorStyles.miniButton, GUILayout.Height(18)))
            {
                _metronomeBpm = bpm;
            }
            GUI.backgroundColor = prevBg;
        }

        private void DrawTimeSigPill(int beats, string label)
        {
            bool isSelected = _metronomeBeatsPerBar == beats;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.95f, 0.75f, 0.25f, 1f) : new Color(0.32f, 0.35f, 0.40f, 0.75f);
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(18)))
            {
                _metronomeBeatsPerBar = beats;
                _metronomeCurrentBeat = 0;
            }
            GUI.backgroundColor = prevBg;
        }

        private void DrawMetronomeBeatVisualizer()
        {
            Rect barRect = GUILayoutUtility.GetRect(100, 1000, 24, 24);
            EditorGUI.DrawRect(barRect, new Color(0.09f, 0.10f, 0.13f, 1f));

            int beats = Math.Clamp(_metronomeBeatsPerBar, 1, 8);
            float spacing = 4f;
            float beatWidth = (barRect.width - (beats - 1) * spacing) / beats;
            double hitAge = EditorApplication.timeSinceStartup - _lastMetronomeClickTime;
            bool isFlashing = hitAge < 0.15;

            for (int b = 0; b < beats; b++)
            {
                Rect bRect = new Rect(barRect.x + b * (beatWidth + spacing), barRect.y + 2, beatWidth, barRect.height - 4);
                bool isActive = _metronomeLoopRunning && _metronomeCurrentBeat == ((b + 1) % beats) && isFlashing;
                bool isDownbeat = b == 0;

                Color bColor;
                if (isActive)
                {
                    bColor = isDownbeat ? new Color(0.22f, 0.95f, 0.50f, 0.95f) : new Color(0.25f, 0.85f, 1f, 0.95f);
                }
                else
                {
                    bColor = isDownbeat ? new Color(0.18f, 0.26f, 0.22f, 0.85f) : new Color(0.15f, 0.17f, 0.20f, 0.85f);
                }

                EditorGUI.DrawRect(bRect, bColor);

                var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = isActive ? Color.black : (isDownbeat ? new Color(0.75f, 0.95f, 0.80f) : new Color(0.75f, 0.82f, 0.90f)) },
                    fontSize = 10
                };
                string bLabel = isDownbeat ? $"{b + 1} (Accent)" : $"{b + 1}";
                GUI.Label(bRect, bLabel, labelStyle);
            }
        }

        private void DrawMetronomeRoutingAndMeterColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Output Routing & Level Meters", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                int targetPair = _metronomeTargetChannel switch
                {
                    3 => 1,
                    5 => 2,
                    7 => 3,
                    _ => 0
                };

                // Channel Strip List (unified route buttons + VU meters)
                DrawChannelPairMeter(0, "Pair 1 [Ch 1-2]", "Front Left + Right (Main FOH)", -1, targetPair == 0);
                EditorGUILayout.Space(3);
                DrawChannelPairMeter(1, "Pair 2 [Ch 3-4]", "Aux / Drummer In-Ear Click", 3, targetPair == 1);
                EditorGUILayout.Space(3);
                DrawChannelPairMeter(2, "Pair 3 [Ch 5-6]", "Center / Subwoofer (LFE)", 5, targetPair == 2);
                EditorGUILayout.Space(3);
                DrawChannelPairMeter(3, "Pair 4 [Ch 7-8]", "Surround Back Left + Right", 7, targetPair == 3);

                EditorGUILayout.Space(8);

                // Headphone Audition Mode Card
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        _headphoneAuditionMode = EditorGUILayout.ToggleLeft(" 🎧 Stereo Headphone Audition Mode", _headphoneAuditionMode, EditorStyles.boldLabel);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Repaint();
                        }
                    }

                    var helpStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        normal = { textColor = new Color(0.75f, 0.8f, 0.85f) }
                    };
                    GUILayout.Label("Audition non-default output channels (Ch 3-8) in standard stereo headphones while testing multi-channel routing isolation.", helpStyle);
                }
            }
        }

        private void DrawChannelPairMeter(int pairIndex, string title, string subtitle, int channelId, bool isTarget)
        {
            var prevBg = GUI.backgroundColor;
            if (isTarget)
            {
                GUI.backgroundColor = new Color(0.25f, 0.65f, 0.95f, 1f);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prevBg;

                // Channel info
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(200)))
                {
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 11,
                        normal = { textColor = isTarget ? new Color(0.35f, 0.85f, 1f) : new Color(0.85f, 0.88f, 0.92f) }
                    };
                    GUILayout.Label(title, titleStyle, GUILayout.Height(16));
                    GUILayout.Label(subtitle, EditorStyles.miniLabel);
                }

                GUILayout.Space(4);

                // Real-time LED VU Meter Bar
                Rect meterRect = GUILayoutUtility.GetRect(100, 1000, 22, 22);
                EditorGUI.DrawRect(meterRect, new Color(0.08f, 0.09f, 0.12f, 1f));

                float minDb = -60f;
                float maxDb = 0f;
                float curDb = _channelPairPeaks[pairIndex];
                float peakHoldDb = _channelPairPeakHold[pairIndex];

                float normCurrent = Mathf.Clamp01((curDb - minDb) / (maxDb - minDb));
                float normPeak = Mathf.Clamp01((peakHoldDb - minDb) / (maxDb - minDb));
                float fillWidth = normCurrent * meterRect.width;

                if (fillWidth > 0)
                {
                    float yellowSplit = (-12f - minDb) / (maxDb - minDb);
                    float redSplit = (-3f - minDb) / (maxDb - minDb);

                    float greenW = Math.Min(fillWidth, yellowSplit * meterRect.width);
                    if (greenW > 0)
                    {
                        EditorGUI.DrawRect(new Rect(meterRect.x, meterRect.y + 1, greenW, meterRect.height - 2), new Color(0.2f, 0.85f, 0.45f, 0.9f));
                    }

                    if (fillWidth > yellowSplit * meterRect.width)
                    {
                        float yStart = meterRect.x + (yellowSplit * meterRect.width);
                        float yW = Math.Min(fillWidth - (yellowSplit * meterRect.width), (redSplit - yellowSplit) * meterRect.width);
                        EditorGUI.DrawRect(new Rect(yStart, meterRect.y + 1, yW, meterRect.height - 2), new Color(0.95f, 0.75f, 0.25f, 0.9f));
                    }

                    if (fillWidth > redSplit * meterRect.width)
                    {
                        float rStart = meterRect.x + (redSplit * meterRect.width);
                        float rW = fillWidth - (redSplit * meterRect.width);
                        EditorGUI.DrawRect(new Rect(rStart, meterRect.y + 1, rW, meterRect.height - 2), new Color(1f, 0.32f, 0.32f, 0.95f));
                    }
                }

                if (normPeak > 0)
                {
                    float pX = meterRect.x + (normPeak * meterRect.width);
                    EditorGUI.DrawRect(new Rect(pX - 1, meterRect.y, 2, meterRect.height), new Color(1f, 1f, 1f, 0.9f));
                }

                string dbText = curDb > -80f ? $"{curDb:F1} dB" : "-inf dB";
                var textStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.95f, 0.95f, 0.95f, 0.9f) },
                    fontSize = 9
                };
                GUI.Label(meterRect, dbText, textStyle);

                GUILayout.Space(6);

                // Active Badge / Route Here Button
                if (!isTarget)
                {
                    if (GUILayout.Button("Route Here", EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(20)))
                    {
                        SetMetronomeTargetChannel(channelId);
                        TriggerMetronomeClick(true);
                    }
                }
                else
                {
                    var activeBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f, 1f);
                    GUILayout.Label("● ACTIVE", EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(20));
                    GUI.backgroundColor = activeBg;
                }
            }
        }

        #endregion
    }
}
