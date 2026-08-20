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
        private void DrawOscilloscopeCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Audio Visualizer", EditorStyles.boldLabel, GUILayout.Width(120));

                    GUILayout.FlexibleSpace();

                    int currentModeIdx = Array.FindIndex(GRAPH_MODE_ITEMS, i => i.Mode == _graphMode);
                    if (currentModeIdx < 0) currentModeIdx = 0;
                    int newModeIdx = EditorGUILayout.Popup(currentModeIdx, GRAPH_MODE_LABELS, GUILayout.Width(190));
                    if (newModeIdx >= 0 && newModeIdx < GRAPH_MODE_ITEMS.Length && newModeIdx != currentModeIdx)
                    {
                        _graphMode = GRAPH_MODE_ITEMS[newModeIdx].Mode;
                    }

                    if (_graphMode == GraphMode.FrequencySpectrum)
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("FFT:", EditorStyles.miniLabel, GUILayout.Width(26));
                        DrawFftSizePill(8, "256", EditorStyles.miniButtonLeft);
                        DrawFftSizePill(9, "512", EditorStyles.miniButtonMid);
                        DrawFftSizePill(10, "1k", EditorStyles.miniButtonMid);
                        DrawFftSizePill(11, "2k", EditorStyles.miniButtonMid);
                        DrawFftSizePill(12, "4k", EditorStyles.miniButtonRight);

                        GUILayout.Space(6);
                        DrawFftStylePill(FftDisplayStyle.FilledCurve, "Curve", EditorStyles.miniButtonLeft);
                        DrawFftStylePill(FftDisplayStyle.RtaBars, "Bars", EditorStyles.miniButtonMid);
                        DrawFftStylePill(FftDisplayStyle.Both, "Both", EditorStyles.miniButtonRight);

                        GUILayout.Space(6);
                        DrawFftScalePill(FftScaleMode.Logarithmic, "Log", EditorStyles.miniButtonLeft);
                        DrawFftScalePill(FftScaleMode.Linear, "Lin", EditorStyles.miniButtonRight);
                    }
                    else if (_graphMode == GraphMode.Oscilloscope)
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Timebase:", EditorStyles.miniLabel, GUILayout.Width(54));
                        DrawScopeTimebasePill(0.002f, "2ms", EditorStyles.miniButtonLeft);
                        DrawScopeTimebasePill(0.005f, "5ms", EditorStyles.miniButtonMid);
                        DrawScopeTimebasePill(0.010f, "10ms", EditorStyles.miniButtonMid);
                        DrawScopeTimebasePill(0.020f, "20ms", EditorStyles.miniButtonMid);
                        DrawScopeTimebasePill(0.050f, "50ms", EditorStyles.miniButtonRight);

                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Gain:", EditorStyles.miniLabel, GUILayout.Width(32));
                        DrawScopeGainPill(1f, "1x", EditorStyles.miniButtonLeft);
                        DrawScopeGainPill(2f, "2x", EditorStyles.miniButtonMid);
                        DrawScopeGainPill(5f, "5x", EditorStyles.miniButtonRight);
                    }
                    else if (_graphMode == GraphMode.MicPitchAndHits)
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Win:", EditorStyles.miniLabel, GUILayout.Width(26));
                        DrawWindowPill(1f, "1s", EditorStyles.miniButtonLeft);
                        DrawWindowPill(2f, "2s", EditorStyles.miniButtonMid);
                        DrawWindowPill(3f, "3s", EditorStyles.miniButtonMid);
                        DrawWindowPill(5f, "5s", EditorStyles.miniButtonMid);
                        DrawWindowPill(10f, "10s", EditorStyles.miniButtonRight);

                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Range: C2–C6", EditorStyles.miniLabel, GUILayout.Width(85));
                    }
                    else
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Win:", EditorStyles.miniLabel, GUILayout.Width(26));
                        DrawWindowPill(1f, "1s", EditorStyles.miniButtonLeft);
                        DrawWindowPill(2f, "2s", EditorStyles.miniButtonMid);
                        DrawWindowPill(3f, "3s", EditorStyles.miniButtonMid);
                        DrawWindowPill(5f, "5s", EditorStyles.miniButtonMid);
                        DrawWindowPill(10f, "10s", EditorStyles.miniButtonRight);

                        if (_graphMode != GraphMode.AbsolutePosition)
                        {
                            GUILayout.Space(6);
                            EditorGUILayout.LabelField("Y:", EditorStyles.miniLabel, GUILayout.Width(16));
                            DrawYScalePill(0f, "Auto", EditorStyles.miniButtonLeft);
                            DrawYScalePill(5f, "±5", EditorStyles.miniButtonMid);
                            DrawYScalePill(10f, "±10", EditorStyles.miniButtonMid);
                            DrawYScalePill(25f, "±25", EditorStyles.miniButtonRight);
                        }
                    }

                    GUILayout.Space(6);

                    var prevBg = GUI.backgroundColor;
                    if (_autoScroll)
                    {
                        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f, 1f);
                    }
                    if (GUILayout.Button(_autoScroll ? "● Live" : "Live", EditorStyles.miniButton, GUILayout.Width(56)))
                    {
                        _autoScroll = !_autoScroll;
                        if (_autoScroll)
                        {
                            if (_graphMode == GraphMode.MicPitchAndHits && _micSamples.Count > 0)
                            {
                                _viewEndTime = _micSamples[_micSamples.Count - 1].RealTime;
                            }
                            else if (_samples.Count > 0)
                            {
                                _viewEndTime = _samples[_samples.Count - 1].RealTime;
                            }
                        }
                    }
                    GUI.backgroundColor = prevBg;

                    if (_freezeGraph)
                    {
                        GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f, 1f);
                    }
                    if (GUILayout.Button("Freeze", EditorStyles.miniButton, GUILayout.Width(56)))
                    {
                        _freezeGraph = !_freezeGraph;
                    }
                    GUI.backgroundColor = prevBg;

                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        _samples.Clear();
                        foreach (var slot in _micSlots)
                        {
                            slot.Samples.Clear();
                        }
                        _fallbackMicSamples.Clear();
                        _viewEndTime = -1;
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
                    }
                }

                EditorGUILayout.Space(4);

                Rect graphRect = GUILayoutUtility.GetRect(100, 2000, 160, 185);
                DrawGraphArea(graphRect);

                EditorGUILayout.Space(3);
                DrawGraphTimelineMiniBar();

                EditorGUILayout.Space(4);
                DrawGraphHudRibbon();
            }
        }

        private void DrawWindowPill(float windowSeconds, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isActive = Mathf.Approximately(_graphTimeWindow, windowSeconds);
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, style, GUILayout.Width(34), GUILayout.Height(18)))
            {
                _graphTimeWindow = windowSeconds;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawYScalePill(float scaleMs, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isActive = Mathf.Approximately(_jitterScaleMs, scaleMs);
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, style, GUILayout.Width(44), GUILayout.Height(18)))
            {
                _jitterScaleMs = scaleMs;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawFftSizePill(int logSize, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isActive = _fftSizeLog == logSize;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, style, GUILayout.Width(40), GUILayout.Height(18)))
            {
                _fftSizeLog = logSize;
                _fftBuffer = null;
                _smoothedFft = null;
                _peakFft = null;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawFftStylePill(FftDisplayStyle style, string label, GUIStyle? btnStyle = null)
        {
            btnStyle ??= EditorStyles.miniButton;
            bool isActive = _fftDisplayStyle == style;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, btnStyle, GUILayout.Width(52), GUILayout.Height(18)))
            {
                _fftDisplayStyle = style;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawFftScalePill(FftScaleMode mode, string label, GUIStyle? btnStyle = null)
        {
            btnStyle ??= EditorStyles.miniButton;
            bool isActive = _fftScaleMode == mode;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, btnStyle, GUILayout.Width(40), GUILayout.Height(18)))
            {
                _fftScaleMode = mode;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawScopeTimebasePill(float timebase, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isSelected = Mathf.Approximately(_scopeTimebase, timebase);
            var prevBg = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 1f, 1f);
            }
            if (GUILayout.Button(label, style, GUILayout.Width(42), GUILayout.Height(18)))
            {
                _scopeTimebase = timebase;
            }
            GUI.backgroundColor = prevBg;
        }

        private void DrawScopeGainPill(float gain, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isSelected = Mathf.Approximately(_scopeGain, gain);
            var prevBg = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 1f, 1f);
            }
            if (GUILayout.Button(label, style, GUILayout.Width(34), GUILayout.Height(18)))
            {
                _scopeGain = gain;
            }
            GUI.backgroundColor = prevBg;
        }

    }
}
