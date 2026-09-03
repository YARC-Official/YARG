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
        private static void DrawMetricTile(string title, string value, Color valueColor)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.Height(30)))
            {
                var titleStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 8,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(0.60f, 0.65f, 0.72f) }
                };
                GUILayout.Label(title.ToUpperInvariant(), titleStyle, GUILayout.Height(10));

                var valStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = valueColor }
                };
                GUILayout.Label(value, valStyle, GUILayout.Height(14));
            }
        }

        private void DrawBottomDashboard()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawBottomLeftSidebar();

                GUILayout.Space(6);

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    switch (_selectedBottomTab)
                    {
                        case 0:
                            DrawSongSyncCard();
                            break;
                        case 1:
                            DrawStemMixerCard();
                            break;
                        case 2:
                            DrawOutputRoutingCard();
                            EditorGUILayout.Space(6);
                            DrawBassBufferSettingsCard();
                            EditorGUILayout.Space(6);
                            DrawBufferDiagnosticsCard();
                            break;
                        case 3:
                            DrawMicrophoneStudioDashboard();
                            break;
                        case 4:
                            DrawClockDriftTestCard();
                            break;
                        case 5:
                            DrawMetronomeRoutingTestCard();
                            break;
                    }
                }
            }
        }

        private void DrawBottomLeftSidebar()
        {
            int channelCount = _bassSong?.Channels?.Count ?? 0;
            string stemBadge = channelCount > 0 ? $"({channelCount})" : "";
            string micBadge = _micSlots.Count(s => s.ActiveDevice != null) > 0 ? $"● {_micSlots.Count(s => s.ActiveDevice != null)} Active" : "";
            string driftBadge = _isDriftTestRunning ? "● Running" : "";
            string metronomeBadge = _metronomeLoopRunning ? "● Playing" : "";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(145)))
            {
                EditorGUILayout.LabelField("TOOLS", EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(3);

                DrawSidebarTab(0, "⏱️ Song Sync", "", GraphMode.SyncConvergence);
                DrawSidebarTab(1, "🎚️ Stem Mixer", stemBadge, GraphMode.SyncConvergence);
                DrawSidebarTab(2, "🔊 Device & Buffer", "", GraphMode.PositionJitter);
                DrawSidebarTab(3, "🎤 Microphone", micBadge, GraphMode.MicPitchAndHits);

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("TESTS", EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(3);

                DrawSidebarTab(4, "🧪 Clock Drift", driftBadge, GraphMode.ClockDrift);
                DrawSidebarTab(5, "🎯 Metronome & Routing", metronomeBadge, GraphMode.SyncConvergence);

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSidebarTab(int index, string label, string badge, GraphMode graphMode)
        {
            bool isSelected = _selectedBottomTab == index;
            Rect itemRect = GUILayoutUtility.GetRect(10, 1000, 24, 24, GUILayout.ExpandWidth(true));

            bool isHovered = itemRect.Contains(Event.current.mousePosition);

            if (isSelected)
            {
                EditorGUI.DrawRect(itemRect, new Color(0.18f, 0.38f, 0.65f, 1f));
                EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y, 3, itemRect.height), new Color(0.35f, 0.75f, 1f, 1f));
            }
            else if (isHovered)
            {
                EditorGUI.DrawRect(itemRect, new Color(1f, 1f, 1f, 0.06f));
            }

            var labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = isSelected ? Color.white : new Color(0.85f, 0.88f, 0.92f) }
            };

            GUI.Label(new Rect(itemRect.x + 8, itemRect.y, itemRect.width - (string.IsNullOrEmpty(badge) ? 12 : 55), itemRect.height), label, labelStyle);

            if (!string.IsNullOrEmpty(badge))
            {
                var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = isSelected ? new Color(0.85f, 0.95f, 1f) : new Color(0.6f, 0.65f, 0.7f) }
                };
                GUI.Label(new Rect(itemRect.x + itemRect.width - 50, itemRect.y, 44, itemRect.height), badge, badgeStyle);
            }

            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                if (_selectedBottomTab != index)
                {
                    _selectedBottomTab = index;
                    _graphMode = graphMode;
                    Repaint();
                }
                Event.current.Use();
            }

            EditorGUILayout.Space(2);
        }

        private void DrawMicrophoneStudioDashboard()
        {
            EnsureDefaultMicSlot();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawMicSlotSelectorBar();

                EditorGUILayout.Space(6);

                var activeSlot = ActiveMicSlot;
                if (activeSlot != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawMicInputAndMonitorCard(activeSlot);

                        GUILayout.Space(6);

                        DrawMicTunerCard(activeSlot);
                    }

                    EditorGUILayout.Space(6);

                    DrawMicRecordPlaybackSection(activeSlot);

                    if (!string.IsNullOrEmpty(activeSlot.StatusMessage) && (EditorApplication.timeSinceStartup - activeSlot.LastStatusTime < 4.0))
                    {
                        EditorGUILayout.Space(4);
                        var msgType = activeSlot.StatusIsError ? MessageType.Error : MessageType.Info;
                        EditorGUILayout.HelpBox(activeSlot.StatusMessage, msgType);
                    }
                }
            }
        }

        private void DrawMicSlotSelectorBar()
        {
            var activeSlot = ActiveMicSlot;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                for (int i = 0; i < _micSlots.Count; i++)
                {
                    var slot = _micSlots[i];
                    bool isSelected = i == _selectedMicSlotIndex;
                    bool isConnected = slot.ActiveDevice != null;

                    var prevBg = GUI.backgroundColor;
                    if (isSelected)
                    {
                        GUI.backgroundColor = slot.ThemeColor;
                    }
                    else if (isConnected)
                    {
                        GUI.backgroundColor = new Color(0.35f, 0.45f, 0.55f, 1f);
                    }

                    if (GUILayout.Button($" {slot.DisplayLabel} ", EditorStyles.toolbarButton, GUILayout.Height(18)))
                    {
                        _selectedMicSlotIndex = i;
                    }
                    GUI.backgroundColor = prevBg;
                }

                if (GUILayout.Button("+ Add Mic", EditorStyles.toolbarDropDown, GUILayout.Width(85), GUILayout.Height(18)))
                {
                    ShowAddMicDeviceMenu();
                }

                GUILayout.FlexibleSpace();

                if (activeSlot != null)
                {
                    string cleanDev = CleanDeviceName(activeSlot.SelectedDevice?.DisplayName);
                    if (string.IsNullOrEmpty(cleanDev) || activeSlot.ActiveDevice == null)
                    {
                        cleanDev = "Select Device…";
                    }
                    else if (cleanDev.Length > 32)
                    {
                        cleanDev = cleanDev.Substring(0, 30) + "…";
                    }

                    if (GUILayout.Button($"🎤 {cleanDev} ▾", EditorStyles.toolbarDropDown, GUILayout.MaxWidth(260), GUILayout.Height(18)))
                    {
                        ShowSlotDeviceMenu(activeSlot);
                    }

                    if (_micSlots.Count > 1)
                    {
                        GUILayout.Space(4);
                        if (GUILayout.Button("✕ Remove", EditorStyles.toolbarButton, GUILayout.Width(70), GUILayout.Height(18)))
                        {
                            RemoveMicSlot(_selectedMicSlotIndex);
                        }
                    }
                }
            }
        }

        private void DrawMicInputAndMonitorCard(MicSlot slot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("🎚️ INPUT & MONITOR", EditorStyles.boldLabel, GUILayout.Width(135));

                    if (_gcDisabledForMonitoring)
                    {
                        var gcStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal = { textColor = new Color(0.35f, 0.9f, 0.5f) },
                            fontStyle = FontStyle.Bold
                        };
                        GUILayout.Label("⚡ GC: Disabled", gcStyle);
                    }

                    GUILayout.FlexibleSpace();

                    var prevBg = GUI.backgroundColor;
                    if (slot.Mute) GUI.backgroundColor = new Color(0.95f, 0.3f, 0.3f);
                    if (GUILayout.Button("Mute", EditorStyles.miniButtonLeft, GUILayout.Width(45), GUILayout.Height(16)))
                    {
                        slot.Mute = !slot.Mute;
                        if (slot.ActiveDevice != null)
                        {
                            slot.ActiveDevice.SetMonitoringLevel(!slot.Mute ? slot.MonitoringVolume : 0f);
                        }
                    }
                    GUI.backgroundColor = prevBg;

                    if (_micSlots.Count > 1)
                    {
                        if (slot.Solo) GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
                        if (GUILayout.Button("Solo", EditorStyles.miniButtonRight, GUILayout.Width(45), GUILayout.Height(16)))
                        {
                            slot.Solo = !slot.Solo;
                        }
                        GUI.backgroundColor = prevBg;
                    }
                }

                EditorGUILayout.Space(3);

                DrawMicBufferDiagnosticsBar(slot);

                EditorGUILayout.Space(4);

                DrawStudioVuMeter(slot);

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Gate Sensitivity", EditorStyles.label, GUILayout.Width(105));
                    float currentSens = SettingsManager.Settings?.MicrophoneSensitivity.Value ?? slot.GateThreshold;
                    float newSens = EditorGUILayout.Slider(currentSens, -50f, 50f);
                    if (MathF.Abs(newSens - currentSens) > 0.05f)
                    {
                        slot.GateThreshold = newSens;
                        if (SettingsManager.Settings != null)
                        {
                            SettingsManager.Settings.MicrophoneSensitivity.Value = newSens;
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Monitor Volume", EditorStyles.label, GUILayout.Width(105));
                    EditorGUI.BeginChangeCheck();
                    float maxVol = (float) (SettingsManager.Settings?.VocalMonitoring.Max ?? 1f);
                    slot.MonitoringVolume = EditorGUILayout.Slider(slot.MonitoringVolume, 0f, maxVol);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (slot.ActiveDevice != null)
                        {
                            slot.ActiveDevice.SetMonitoringLevel(!slot.Mute ? slot.MonitoringVolume : 0f);
                        }
                        if (SettingsManager.Settings != null)
                        {
                            SettingsManager.Settings.VocalMonitoring.Value = slot.MonitoringVolume;
                        }
                    }
                }
            }
        }

        private static void DrawMicBufferDiagnosticsBar(MicSlot slot)
        {
            var bufferInfo = slot.ActiveDevice?.GetBufferInfo();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (bufferInfo is { } bInfo)
                {
                    bool isWasapi = slot.SelectedDevice?.DisplayName.StartsWith("WASAPI: ", StringComparison.OrdinalIgnoreCase) == true;

                    Color tagColor = bInfo.IsAsio
                        ? new Color(0.35f, 0.9f, 0.5f)
                        : (isWasapi ? new Color(0.85f, 0.45f, 1f) : new Color(0.45f, 0.8f, 1f));

                    var tagStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        normal = { textColor = tagColor },
                        alignment = TextAnchor.MiddleLeft
                    };
                    var statStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.75f, 0.8f, 0.9f) },
                        alignment = TextAnchor.MiddleLeft
                    };

                    string modeTag = bInfo.IsAsio
                        ? "⚡ ASIO"
                        : (isWasapi ? "⚡ WASAPI" : "🎙 Shared");
                    GUILayout.Label(modeTag, tagStyle, GUILayout.Width(62));

                    string bufStr;
                    if (bInfo.IsAsio)
                    {
                        bufStr = $"{bInfo.SampleRate} Hz • {bInfo.BufferFrames} spl ({bInfo.BufferMilliseconds:F1} ms buffer)";
                    }
                    else if (isWasapi)
                    {
                        bufStr = $"{bInfo.SampleRate} Hz • {bInfo.Channels} ch • {bInfo.BufferFrames} spl ({bInfo.BufferMilliseconds:F1} ms buffer) • {GetWaitingMilliseconds(bInfo)} ms queued";
                    }
                    else
                    {
                        bufStr = $"{bInfo.SampleRate} Hz • {bInfo.Channels} ch • {bInfo.BufferMilliseconds} ms ({bInfo.BufferFrames} spl) buffer • {bInfo.CushionMilliseconds} ms target • {GetWaitingMilliseconds(bInfo)} ms queued";
                    }

                    GUILayout.Label(bufStr, statStyle);
                }
                else
                {
                    GUILayout.Label("🎙 Disconnected", EditorStyles.miniLabel, GUILayout.Width(90));
                    GUILayout.Label("Select an input device above to connect.", EditorStyles.centeredGreyMiniLabel);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private static int GetWaitingMilliseconds(MicBufferInfo bufferInfo)
        {
            int bytesPerSecond = bufferInfo.SampleRate * bufferInfo.Channels * sizeof(float);
            return bytesPerSecond > 0
                ? (int) Math.Round(bufferInfo.WaitingBytes * 1000.0 / bytesPerSecond)
                : 0;
        }

        private static void DrawStudioVuMeter(MicSlot slot)
        {
            Rect meterRect = GUILayoutUtility.GetRect(100, 1000, 18, 18);
            EditorGUI.DrawRect(meterRect, new Color(0.10f, 0.11f, 0.14f, 1f));

            float minDb = -20f;
            float maxDb = 50f;
            float rangeDb = maxDb - minDb;

            float normCurrent = Mathf.Clamp01((slot.CurrentDb - minDb) / rangeDb);
            float normPeak = Mathf.Clamp01((slot.PeakHoldDb - minDb) / rangeDb);
            float sensitivity = SettingsManager.Settings?.MicrophoneSensitivity.Value ?? slot.GateThreshold;
            float normSensitivity = Mathf.Clamp01((sensitivity - minDb) / rangeDb);

            float fillWidth = normCurrent * meterRect.width;

            if (fillWidth > 0)
            {
                float yellowSplit = Mathf.Clamp01((30f - minDb) / rangeDb);
                float redSplit = Mathf.Clamp01((42f - minDb) / rangeDb);

                float greenWidth = Math.Min(fillWidth, yellowSplit * meterRect.width);
                if (greenWidth > 0)
                {
                    EditorGUI.DrawRect(new Rect(meterRect.x, meterRect.y + 1, greenWidth, meterRect.height - 2), new Color(0.2f, 0.85f, 0.4f, 0.85f));
                }

                if (fillWidth > yellowSplit * meterRect.width)
                {
                    float yellowStartX = meterRect.x + (yellowSplit * meterRect.width);
                    float yellowWidth = Math.Min(fillWidth - (yellowSplit * meterRect.width), (redSplit - yellowSplit) * meterRect.width);
                    EditorGUI.DrawRect(new Rect(yellowStartX, meterRect.y + 1, yellowWidth, meterRect.height - 2), new Color(0.95f, 0.75f, 0.2f, 0.85f));
                }

                if (fillWidth > redSplit * meterRect.width)
                {
                    float redStartX = meterRect.x + (redSplit * meterRect.width);
                    float redWidth = fillWidth - (redSplit * meterRect.width);
                    EditorGUI.DrawRect(new Rect(redStartX, meterRect.y + 1, redWidth, meterRect.height - 2), new Color(1f, 0.3f, 0.3f, 0.9f));
                }
            }

            float gateX = meterRect.x + (normSensitivity * meterRect.width);
            EditorGUI.DrawRect(new Rect(gateX - 1, meterRect.y, 2, meterRect.height), new Color(1f, 0.65f, 0.15f, 0.95f));

            if (normPeak > 0)
            {
                float peakX = meterRect.x + (normPeak * meterRect.width);
                EditorGUI.DrawRect(new Rect(peakX - 1, meterRect.y, 2, meterRect.height), new Color(1f, 1f, 1f, 0.9f));
            }

            double hitAge = EditorApplication.timeSinceStartup - slot.LastHitTime;
            bool isHit = hitAge < 0.25;

            string levelText = isHit
                ? "🥁 HIT!"
                : (slot.CurrentDb > -50f ? $"{slot.CurrentDb:F1} dB" : "Silent");

            var textStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isHit ? new Color(0.2f, 1f, 0.4f) : new Color(0.95f, 0.95f, 0.95f, 0.9f) },
                fontSize = 9
            };
            GUI.Label(meterRect, levelText, textStyle);
        }

        private static void DrawMicTunerCard(MicSlot slot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("🎵 VOCAL TUNER", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                string note = slot.IsVoiced ? slot.CurrentNoteName : "--";
                bool inTune = slot.IsVoiced && MathF.Abs(slot.CurrentCents) < 10f;
                Color noteColor = !slot.IsVoiced
                    ? new Color(0.45f, 0.48f, 0.55f)
                    : (inTune ? new Color(0.25f, 0.95f, 0.45f) : slot.ThemeColor);

                var heroStyle = new GUIStyle(EditorStyles.largeLabel)
                {
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = noteColor }
                };
                EditorGUILayout.LabelField(note, heroStyle, GUILayout.Height(30));

                DrawCentDeviationBar(slot, inTune);

                EditorGUILayout.Space(4);

                string subtext = slot.IsVoiced
                    ? $"{slot.CurrentPitchHz:F1} Hz  •  {slot.CurrentCents:+0;-0;0} cents"
                    : "Sing or speak into mic";
                var subStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 9,
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUILayout.LabelField(subtext, subStyle);
            }
        }

        private static void DrawCentDeviationBar(MicSlot slot, bool inTune)
        {
            Rect barRect = GUILayoutUtility.GetRect(100, 1000, 14, 14);
            EditorGUI.DrawRect(barRect, new Color(0.12f, 0.13f, 0.16f, 1f));

            float centerX = barRect.x + (barRect.width * 0.5f);

            float sweetSpotWidth = barRect.width * 0.20f;
            EditorGUI.DrawRect(new Rect(centerX - (sweetSpotWidth * 0.5f), barRect.y, sweetSpotWidth, barRect.height), new Color(0.2f, 0.8f, 0.4f, 0.15f));
            EditorGUI.DrawRect(new Rect(centerX - 1, barRect.y, 2, barRect.height), new Color(1f, 1f, 1f, 0.45f));

            if (slot.IsVoiced)
            {
                float normCents = Mathf.Clamp(slot.CurrentCents / 50f, -1f, 1f);
                float needleX = centerX + (normCents * (barRect.width * 0.48f));
                Color needleColor = inTune
                    ? new Color(0.25f, 0.95f, 0.45f, 1f)
                    : new Color(1f, 0.65f, 0.15f, 1f);
                EditorGUI.DrawRect(new Rect(needleX - 2, barRect.y + 1, 4, barRect.height - 2), needleColor);
            }
        }

    }
}
