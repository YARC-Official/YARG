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
        private void DrawClockDriftTestCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = _isDriftTestRunning ? new Color(0.2f, 0.85f, 0.4f, 1f) : new Color(0.45f, 0.5f, 0.55f, 1f);
                    GUILayout.Label(_isDriftTestRunning ? " ● DRIFT TEST RUNNING " : " ⏹ DRIFT TEST STOPPED ", EditorStyles.miniButton, GUILayout.Width(160), GUILayout.Height(18));
                    GUI.backgroundColor = prevBg;

                    GUILayout.Space(6);
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 10 };
                    GUILayout.Label("Hardware Clock Drift Calibration", titleStyle, GUILayout.Height(18));

                    GUILayout.FlexibleSpace();

                    if (_isDriftTestRunning)
                    {
                        var stopBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
                        if (GUILayout.Button("Stop Test", EditorStyles.toolbarButton, GUILayout.Width(78)))
                        {
                            StopDriftTest();
                        }
                        GUI.backgroundColor = stopBg;
                    }
                    else
                    {
                        var startBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f, 1f);
                        if (GUILayout.Button("Start Test", EditorStyles.toolbarButton, GUILayout.Width(78)))
                        {
                            StartDriftTest();
                        }
                        GUI.backgroundColor = startBg;
                    }
                }

                EditorGUILayout.Space(4);

                EditorGUILayout.HelpBox(
                    "How to run: Click 'Start Test' and let the audio stream play continuously for 1–2 minutes. Cumulative drift and estimated rate (ppm) will track whether the audio hardware DAC is staying locked with the system clock without runaway drift.",
                    MessageType.Info);

                EditorGUILayout.Space(4);

                string durationStr = FormatTime(_driftElapsedHostSeconds);
                Color driftColor = Math.Abs(_driftCumulativeMs) > 10.0 ? new Color(1f, 0.4f, 0.4f) : (Math.Abs(_driftCumulativeMs) > 3.0 ? new Color(1f, 0.75f, 0.2f) : new Color(0.25f, 0.95f, 0.45f));
                string driftStr = !_driftBaselineEstablished ? "STARTING..." : $"{_driftCumulativeMs:+0.00;-0.00;0.00} ms";
                string rateStr = !_driftBaselineEstablished || _driftElapsedHostSeconds < 1.0 ? "CALCULATING..." : $"{_driftRatePpm:+0.0;-0.0;0.0} ppm ({_driftMsPerMin:+0.00;-0.00;0.00} ms/min)";
                string outputRate = !_driftBaselineEstablished || _driftElapsedHostSeconds < 1.0 ? "CALCULATING..." : $"{_driftCallbackRatePpm:+0.0;-0.0;0.0} ppm";

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetricTile("DURATION", durationStr, Color.white);
                    DrawMetricTile("CUMULATIVE DRIFT", driftStr, driftColor);
                    DrawMetricTile("ESTIMATED RATE", rateStr, new Color(0.3f, 0.8f, 1f));
                    DrawMetricTile("CALLBACK RATE", outputRate, new Color(0.7f, 0.85f, 1f));
                }

                EditorGUILayout.Space(4);

                DrawDriftPhaseGauge();

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    var tipStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = new Color(0.7f, 0.75f, 0.8f) } };
                    GUILayout.Label("💡 Long-term drift measures high-precision host timer (QPC) against hardware audio crystal DAC rate over extended playback to quantify clock drift and stream stability.", tipStyle);
                }
            }
        }

        private void DrawMainOscilloscopeGraph(Rect rect, Rect plotRect, float paddingLeft, float paddingTop, float paddingRight, float paddingBottom, float plotWidth, float plotHeight)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(plotRect, new Color(0.06f, 0.07f, 0.09f, 1f));

            float centerY = plotRect.y + (plotHeight * 0.5f);

            float[] levels = { 1.0f, 0.5f, 0.0f, -0.5f, -1.0f };
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.55f, 0.55f, 0.6f, 0.6f) },
                fontSize = 9,
            };

            foreach (float lvl in levels)
            {
                float y = centerY - (lvl * (plotHeight * 0.45f));
                Color lineCol = lvl == 0f ? new Color(0.2f, 0.85f, 0.4f, 0.4f) : new Color(1f, 1f, 1f, 0.06f);
                EditorGUI.DrawRect(new Rect(plotRect.x, y, plotWidth, 1), lineCol);
                GUI.Label(new Rect(rect.x + 4, y - 8, paddingLeft - 6, 16), $"{lvl:+0.0;-0.0;0.0}", labelStyle);
            }

            int divCount = 10;
            float divWidth = plotWidth / divCount;
            float msPerDiv = (_scopeTimebase * 1000f) / divCount;
            for (int d = 0; d <= divCount; d++)
            {
                float x = plotRect.x + (d * divWidth);
                EditorGUI.DrawRect(new Rect(x, plotRect.y, 1, plotHeight), new Color(1f, 1f, 1f, 0.04f));
                if (d % 2 == 0)
                {
                    GUI.Label(new Rect(x - 15, plotRect.yMax + 2, 30, 16), $"{d * msPerDiv:0.#}ms", labelStyle);
                }
            }

            if (_scopePcmBuffer == null || _scopePcmBuffer.Length < 2048)
            {
                _scopePcmBuffer = new float[4096];
            }

            bool isPlaying = _bassSong != null && !_bassSong.IsPaused;
            int samplesRead = 0;
            if (isPlaying)
            {
                samplesRead = _bassSong!.GetSampleData(_scopePcmBuffer);
            }

            if (samplesRead <= 0 || !isPlaying)
            {
                Handles.color = new Color(0.2f, 0.85f, 1f, 0.4f);
                Handles.DrawLine(new Vector3(plotRect.x, centerY, 0), new Vector3(plotRect.xMax, centerY, 0));

                var emptyStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(0.5f, 0.5f, 0.55f, 0.5f) },
                    alignment = TextAnchor.MiddleCenter,
                };
                GUI.Label(plotRect, isPlaying ? "Buffering audio stream..." : "Paused / Stopped", emptyStyle);
                return;
            }

            int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 44100;
            int windowSamples = Math.Min(samplesRead, (int) (sampleRate * _scopeTimebase));
            if (windowSamples < 10)
            {
                windowSamples = 10;
            }

            int triggerIdx = 0;
            for (int i = 0; i < Math.Min(samplesRead - windowSamples, 1024); i++)
            {
                if (_scopePcmBuffer[i] <= 0f && _scopePcmBuffer[i + 1] > 0f)
                {
                    triggerIdx = i;
                    break;
                }
            }

            int renderCount = Math.Min(windowSamples, samplesRead - triggerIdx);
            if (renderCount < 2)
            {
                return;
            }

            var points = new Vector3[renderCount];
            float peak = 0f;
            double sumSq = 0;

            for (int i = 0; i < renderCount; i++)
            {
                float s = _scopePcmBuffer[triggerIdx + i] * _scopeGain;
                float absVal = Math.Abs(s);
                if (absVal > peak)
                {
                    peak = absVal;
                }
                sumSq += s * s;

                float normX = i / (float) (renderCount - 1);
                float px = plotRect.x + (normX * plotWidth);
                float py = centerY - (Mathf.Clamp(s, -1.2f, 1.2f) * (plotHeight * 0.45f));
                points[i] = new Vector3(px, py, 0);
            }

            float rms = (float) Math.Sqrt(sumSq / renderCount);
            float peakDb = 20f * Mathf.Log10(Mathf.Max(peak / _scopeGain, 1e-5f));
            float rmsDb = 20f * Mathf.Log10(Mathf.Max(rms / _scopeGain, 1e-5f));

            Handles.color = new Color(0.15f, 0.95f, 0.65f, 0.25f);
            Handles.DrawAAPolyLine(4.5f, points);

            Handles.color = new Color(0.25f, 1f, 0.75f, 0.95f);
            Handles.DrawAAPolyLine(2.0f, points);

            var hudStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f, 0.9f) },
            };
            string hudText = $"Timebase: {_scopeTimebase * 1000f:0}ms | Gain: {_scopeGain:0}x | Peak: {peak / _scopeGain:0.00} ({peakDb:+0.0;-0.0;0.0} dBFS) | RMS: {rms / _scopeGain:0.00} ({rmsDb:+0.0;-0.0;0.0} dBFS)";
            GUI.Label(new Rect(plotRect.x + 8, plotRect.y + 4, plotWidth - 16, 16), hudText, hudStyle);
        }

        private void DrawDriftPhaseGauge()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Phase Alignment:", EditorStyles.miniBoldLabel, GUILayout.Width(105));

                Rect gaugeRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(18));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(gaugeRect, new Color(0.07f, 0.08f, 0.11f, 1f));

                    float centerX = gaugeRect.x + (gaugeRect.width * 0.5f);

                    // Draw subtle grid subdivisions (-20ms, -10ms, +10ms, +20ms)
                    float maxRangeMs = 25f;
                    float[] ticks = { -20f, -10f, 10f, 20f };
                    foreach (float tick in ticks)
                    {
                        float tickX = centerX + ((tick / maxRangeMs) * ((gaugeRect.width * 0.5f) - 8));
                        EditorGUI.DrawRect(new Rect(tickX, gaugeRect.y + 3, 1, gaugeRect.height - 6), new Color(1f, 1f, 1f, 0.08f));
                    }

                    // Center zero reference line
                    EditorGUI.DrawRect(new Rect(centerX - 1, gaugeRect.y, 2, gaugeRect.height), new Color(1f, 1f, 1f, 0.4f));

                    float normOffset = Mathf.Clamp((float) (_driftCumulativeMs / maxRangeMs), -1f, 1f);
                    float markerX = centerX + (normOffset * ((gaugeRect.width * 0.5f) - 8));

                    Color markerColor = Math.Abs(_driftCumulativeMs) > 10.0
                        ? new Color(1f, 0.35f, 0.35f, 1f)
                        : (Math.Abs(_driftCumulativeMs) > 3.0 ? new Color(1f, 0.75f, 0.2f, 1f) : new Color(0.25f, 0.95f, 0.45f, 1f));

                    // Marker pill
                    Rect markerRect = new Rect(markerX - 4, gaugeRect.y + 2, 8, gaugeRect.height - 4);
                    EditorGUI.DrawRect(markerRect, markerColor);

                    var leftStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.65f, 0.7f, 0.7f) }, alignment = TextAnchor.MiddleLeft, fontSize = 8 };
                    var rightStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.65f, 0.7f, 0.7f) }, alignment = TextAnchor.MiddleRight, fontSize = 8 };
                    GUI.Label(new Rect(gaugeRect.x + 4, gaugeRect.y + 1, 95, gaugeRect.height), "◄ Lag (-25ms)", leftStyle);
                    GUI.Label(new Rect(gaugeRect.xMax - 99, gaugeRect.y + 1, 95, gaugeRect.height), "(+25ms) Lead ►", rightStyle);
                }

                string offsetText = $"{_driftCumulativeMs:+0.00;-0.00;0.00} ms";
                Color offsetColor = Math.Abs(_driftCumulativeMs) > 10.0
                    ? new Color(1f, 0.4f, 0.4f)
                    : (Math.Abs(_driftCumulativeMs) > 3.0 ? new Color(1f, 0.75f, 0.2f) : new Color(0.25f, 0.95f, 0.45f));

                var offsetStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = offsetColor },
                    alignment = TextAnchor.MiddleRight
                };
                EditorGUILayout.LabelField(offsetText, offsetStyle, GUILayout.Width(62));

                if (_driftMaxPositionStepMs > 0)
                {
                    EditorGUILayout.LabelField($"• Max Step: {_driftMaxPositionStepMs:0.00}ms", EditorStyles.miniLabel, GUILayout.Width(110));
                }
            }
        }

        private void StartDriftTest()
        {
            if (!Mathf.Approximately(_playbackSpeed, 1f))
            {
                EditorUtility.DisplayDialog("Drift Test Requires 1x", "Set playback speed to 1.0x before starting this test.", "OK");
                return;
            }

            EnsureAudioInitialized();
            DisposeSong();
            _isDriftTestRunning = true;
            _driftBaselineEstablished = false;
            _driftTestStartTime = EditorApplication.timeSinceStartup;
            _driftTestQpcStart = Stopwatch.GetTimestamp();
            _driftInitialAudioStart = 0;
            _driftElapsedHostSeconds = 0;
            _driftElapsedAudioSeconds = 0;
            _driftCumulativeMs = 0;
            _driftRatePpm = 0;
            _driftMsPerMin = 0;
            _driftAudioLoopCount = 0;
            _driftMeasurements.Clear();
            _driftStartRequestedFrames = 0;
            _driftCallbackRatePpm = 0;
            _driftMaxPositionStepMs = 0;
            _driftLargePositionStepCount = 0;
            _driftPreviousSampleTime = 0;
            _driftPreviousSongPosition = 0;
            _driftPreviousPositionValid = false;

            string audioPath = GetDriftAudioFilePath();
            if (File.Exists(audioPath))
            {
                LoadAudioFile(audioPath);
                if (_bassSong != null)
                {
                    _bassSong.SetVolume(0f);
                    _calibrationTrackLength = _bassSong.Length;
                    _bassSong.SetPosition(0);
                    PlaySong();
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Audio File Missing", $"Could not find {audioPath}", "OK");
                _isDriftTestRunning = false;
            }
        }

        private void StopDriftTest()
        {
            _isDriftTestRunning = false;
            if (_bassSong != null && !_bassSong.IsPaused)
            {
                _bassSong.Pause();
            }
        }

        private void UpdateDriftTest(double now, double dt)
        {
            if (!_isDriftTestRunning)
            {
                return;
            }

            double songPos = _bassSong != null ? _bassSong.GetPosition() : 0;
            var readAheadStats = _bassSong?.GetReadAheadStats() ?? default;
            int sampleRate = Bass.Info.SampleRate;

            if (!_driftBaselineEstablished)
            {
                if (_bassSong != null && !_bassSong.IsPaused && songPos >= 0.1)
                {
                    _driftTestQpcStart = Stopwatch.GetTimestamp();
                    _driftInitialAudioStart = songPos;
                    _driftStartRequestedFrames = readAheadStats.RequestedFrames;
                    _driftBaselineEstablished = true;
                    _driftPreviousSampleTime = 0;
                    _driftPreviousSongPosition = songPos;
                    _driftPreviousPositionValid = true;
                }
                else
                {
                    return;
                }
            }

            if (_bassSong != null)
            {
                _bassSong.SetVolume(0f);
            }

            long nowTicks = Stopwatch.GetTimestamp();
            _driftElapsedHostSeconds = (double) (nowTicks - _driftTestQpcStart) / Stopwatch.Frequency;
            _driftElapsedAudioSeconds = (_driftAudioLoopCount * _calibrationTrackLength) + (songPos - _driftInitialAudioStart);
            _driftCumulativeMs = (_driftElapsedHostSeconds - _driftElapsedAudioSeconds) * 1000.0;

            double positionStepResidualMs = 0;
            if (_driftPreviousPositionValid)
            {
                double elapsedDelta = _driftElapsedHostSeconds - _driftPreviousSampleTime;
                double positionDelta = songPos - _driftPreviousSongPosition;
                if (positionDelta < -0.5)
                {
                    _driftPreviousSampleTime = _driftElapsedHostSeconds;
                    _driftPreviousSongPosition = songPos;
                }
                else if (elapsedDelta > 0)
                {
                    positionStepResidualMs = (positionDelta - elapsedDelta) * 1000.0;
                    double absoluteResidual = Math.Abs(positionStepResidualMs);
                    _driftMaxPositionStepMs = Math.Max(_driftMaxPositionStepMs, absoluteResidual);
                    if (absoluteResidual > 1.0)
                    {
                        _driftLargePositionStepCount++;
                    }

                    _driftPreviousSampleTime = _driftElapsedHostSeconds;
                    _driftPreviousSongPosition = songPos;
                }
            }

            if (sampleRate > 0 && _driftElapsedHostSeconds >= 1.0 &&
                readAheadStats.RequestedFrames >= _driftStartRequestedFrames)
            {
                double outputElapsed = (readAheadStats.RequestedFrames - _driftStartRequestedFrames) / (double) sampleRate;
                _driftCallbackRatePpm = ((outputElapsed / _driftElapsedHostSeconds) - 1.0) * 1_000_000.0;
            }
            else
            {
                _driftCallbackRatePpm = 0;
            }

            _driftMeasurements.Add(new DriftMeasurement
            {
                HostSeconds = _driftElapsedHostSeconds,
                SongPositionSeconds = songPos,
                DriftMs = _driftCumulativeMs,
                PositionStepResidualMs = positionStepResidualMs,
                ConsumedFrames = readAheadStats.ConsumedFrames,
                RequestedFrames = readAheadStats.RequestedFrames,
                QueuedFrames = readAheadStats.QueuedFrames,
                PositionOutputFrame = readAheadStats.PositionOutputFrame,
                CallbackFrames = readAheadStats.CallbackFrames,
                CallbackElapsedFrames = readAheadStats.CallbackElapsedFrames,
                CallbackCorrectionFrames = readAheadStats.CallbackCorrectionFrames,
                CallbackClockOffsetFrames = readAheadStats.CallbackClockOffsetFrames,
                UnderrunFrames = readAheadStats.UnderrunFrames,
                UnderrunEvents = readAheadStats.UnderrunEvents
            });
            if (_driftMeasurements.Count > MAX_DRIFT_MEASUREMENTS)
            {
                _driftMeasurements.RemoveAt(0);
            }

            if (_driftElapsedHostSeconds >= 1.0 && _driftMeasurements.Count >= 5)
            {
                double sumT = 0;
                double sumD = 0;
                for (int i = 0; i < _driftMeasurements.Count; i++)
                {
                    sumT += _driftMeasurements[i].HostSeconds;
                    sumD += _driftMeasurements[i].DriftMs;
                }
                double meanT = sumT / _driftMeasurements.Count;
                double meanD = sumD / _driftMeasurements.Count;

                double num = 0;
                double denom = 0;
                for (int i = 0; i < _driftMeasurements.Count; i++)
                {
                    double dtSample = _driftMeasurements[i].HostSeconds - meanT;
                    num += dtSample * (_driftMeasurements[i].DriftMs - meanD);
                    denom += dtSample * dtSample;
                }

                double slopeMsPerSec = denom > 1e-6 ? num / denom : 0.0;
                _driftRatePpm = slopeMsPerSec * 1000.0;
                _driftMsPerMin = slopeMsPerSec * 60.0;
            }
            else
            {
                _driftRatePpm = 0;
                _driftMsPerMin = 0;
            }

            Repaint();
        }

        private string GetDriftAudioFilePath()
        {
            string tempFolder = Path.Combine(Application.temporaryCachePath, "YARG_DriftTest");
            Directory.CreateDirectory(tempFolder);

            string path = Path.Combine(tempFolder, "drift_test_silence.wav");
            if (!File.Exists(path) || new FileInfo(path).Length < 10000000)
            {
                CreateSilenceWavFile(path, 1800f, 44100);
            }
            return path;
        }

        private static void CreateSilenceWavFile(string path, float durationSec, int sampleRate)
        {
            int numSamples = (int) (sampleRate * durationSec);
            int subChunk2Size = numSamples * sizeof(short);

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
            using var writer = new BinaryWriter(fs);

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + subChunk2Size);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short) 1);
            writer.Write((short) 1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * sizeof(short));
            writer.Write((short) 2);
            writer.Write((short) 16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(subChunk2Size);

            const int BUFFER_SIZE = 8192;
            byte[] byteBuffer = new byte[BUFFER_SIZE * sizeof(short)];
            int samplesWritten = 0;

            while (samplesWritten < numSamples)
            {
                int count = Math.Min(BUFFER_SIZE, numSamples - samplesWritten);
                writer.Write(byteBuffer, 0, count * sizeof(short));
                samplesWritten += count;
            }
        }

    }
}
