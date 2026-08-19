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
        private void DrawFftTimelineMiniBar()
        {
            double songLength = _bassSong?.Length ?? 0;
            double currentPos = _bassSong?.GetPosition() ?? 0;

            if (songLength <= 0.05)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                Rect barRect = GUILayoutUtility.GetRect(100, 1000, 14, 14);
                EditorGUI.DrawRect(barRect, new Color(0.12f, 0.13f, 0.16f, 1f));

                float normPos = (float) Math.Clamp(currentPos / songLength, 0.0, 1.0);
                float barW = normPos * barRect.width;

                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y + 1, barW, barRect.height - 2), new Color(0.15f, 0.85f, 0.95f, 0.5f));
                EditorGUI.DrawRect(new Rect(barRect.x + barW - 1, barRect.y, 2, barRect.height), new Color(0.4f, 1f, 1f, 1f));

                var evt = Event.current;
                if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && barRect.Contains(evt.mousePosition))
                {
                    float clickedNorm = (evt.mousePosition.x - barRect.x) / barRect.width;
                    double targetPos = clickedNorm * songLength;
                    _bassSong?.SetPosition(targetPos);
                    evt.Use();
                    Repaint();
                }

                GUILayout.Space(6);
                string timeStr = $"{FormatTime(currentPos)} / {FormatTime(songLength)}";
                GUILayout.Label(timeStr, EditorStyles.miniLabel, GUILayout.Width(90), GUILayout.Height(14));
            }
        }

        private void DrawFftHudRibbon()
        {
            int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 44100;
            int fftPoints = 1 << _fftSizeLog;
            float binWidth = (sampleRate * 0.5f) / (fftPoints / 2f);

            string dominantPitch = _dominantFrequencyHz >= 20f
                ? $"{_dominantNoteName} ({_dominantFrequencyHz:F0} Hz)"
                : "--";
            Color peakColor = _dominantDb > -12f ? new Color(1f, 0.35f, 0.35f) : (_dominantDb > -30f ? new Color(0.25f, 0.95f, 0.45f) : new Color(0.2f, 0.75f, 1f));

            string dbText = _dominantDb > -150f ? $"{_dominantDb:+0.0;-0.0;0.0} dBFS" : "-∞ dB";
            string centroidText = _spectralCentroidHz > 10f ? $"{_spectralCentroidHz:F0} Hz" : "--";
            string resText = $"{fftPoints} pts ({binWidth:F1} Hz/bin)";

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricTile("DOMINANT PEAK", dominantPitch, new Color(0.15f, 0.9f, 1f));
                DrawMetricTile("PEAK LEVEL", dbText, peakColor);
                DrawMetricTile("CENTROID (BRIGHTNESS)", centroidText, new Color(0.85f, 0.65f, 1f));
                DrawMetricTile("FFT RESOLUTION", resText, Color.white);
                DrawMetricTile("FRAME RATE", $"{_currentFps:F0} FPS", Color.white);

                GUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(80), GUILayout.Height(36)))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reset Peaks", EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(22)))
                    {
                        if (_peakFft != null)
                        {
                            for (int i = 0; i < _peakFft.Length; i++)
                            {
                                _peakFft[i] = _fftMinDb;
                            }
                        }
                        for (int b = 0; b < _fftBands.Length; b++)
                        {
                            _fftBands[b].PeakDb = _fftMinDb;
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawFftSpectrumGraph(Rect rect, Rect plotRect, float paddingLeft, float paddingTop, float paddingRight, float paddingBottom, float plotWidth, float plotHeight)
        {
            var evt = Event.current;
            if (evt.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.06f, 0.07f, 0.09f, 1f));

            int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 44100;
            int fftPoints = 1 << _fftSizeLog;
            int binCount = fftPoints / 2;

            if (_fftBuffer == null || _fftBuffer.Length != binCount || _smoothedFft == null || _smoothedFft.Length != binCount || _peakFft == null || _peakFft.Length != binCount)
            {
                _fftBuffer = new float[binCount];
                _smoothedFft = new float[binCount];
                _peakFft = new float[binCount];
                for (int i = 0; i < binCount; i++)
                {
                    _peakFft[i] = _fftMinDb;
                }
            }

            for (int b = 0; b < _fftBands.Length; b++)
            {
                var band = _fftBands[b];
                float normX1 = GetFreqNorm(band.MinFreq);
                float normX2 = GetFreqNorm(band.MaxFreq);
                float x1 = plotRect.x + (normX1 * plotWidth);
                float x2 = plotRect.x + (normX2 * plotWidth);
                float bandW = Mathf.Max(1f, x2 - x1);

                Color zoneBg = band.BandColor;
                zoneBg.a = (b % 2 == 0) ? 0.045f : 0.025f;
                EditorGUI.DrawRect(new Rect(x1, plotRect.y, bandW, plotHeight), zoneBg);

                var bandTagStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 8,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(band.BandColor.r, band.BandColor.g, band.BandColor.b, 0.45f) }
                };
                GUI.Label(new Rect(x1 + 3, plotRect.y + 2, bandW - 4, 12), band.Name.ToUpperInvariant(), bandTagStyle);
            }

            float[] dbSteps = { 0f, -12f, -24f, -36f, -48f, -60f, -72f, -84f, -96f };
            for (int i = 0; i < dbSteps.Length; i++)
            {
                float db = dbSteps[i];
                if (db < _fftMinDb || db > _fftMaxDb)
                {
                    continue;
                }

                float normY = Mathf.Clamp01((db - _fftMinDb) / (_fftMaxDb - _fftMinDb));
                float y = plotRect.y + plotHeight - (normY * plotHeight);

                Color lineCol = Mathf.Approximately(db, 0f)
                    ? new Color(0.40f, 0.45f, 0.55f, 0.7f)
                    : new Color(0.18f, 0.20f, 0.25f, 0.5f);
                EditorGUI.DrawRect(new Rect(plotRect.x, y, plotWidth, 1), lineCol);

                var dbLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 9,
                    normal = { textColor = new Color(0.55f, 0.60f, 0.70f, 0.8f) }
                };
                GUI.Label(new Rect(rect.x, y - 8, paddingLeft - 4, 16), $"{db:0} dB", dbLabelStyle);
            }

            float[] freqSteps = { 30f, 60f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f, 20000f };
            string[] freqLabels = { "30", "60", "125", "250", "500", "1k", "2k", "4k", "8k", "16k", "20k" };
            for (int i = 0; i < freqSteps.Length; i++)
            {
                float f = freqSteps[i];
                float normX = GetFreqNorm(f);
                float x = plotRect.x + (normX * plotWidth);

                EditorGUI.DrawRect(new Rect(x, plotRect.y, 1, plotHeight), new Color(0.18f, 0.20f, 0.25f, 0.5f));

                var fLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 8
                };
                GUI.Label(new Rect(x - 18, plotRect.y + plotHeight + 3, 36, 16), freqLabels[i], fLabelStyle);
            }

            if (_fftDisplayStyle == FftDisplayStyle.RtaBars || _fftDisplayStyle == FftDisplayStyle.Both)
            {
                int numBars = 48;
                float barSpacing = 1.5f;
                float totalSlotWidth = plotWidth / numBars;
                float barWidth = Mathf.Max(1f, totalSlotWidth - barSpacing);

                for (int b = 0; b < numBars; b++)
                {
                    float u1 = b / (float) numBars;
                    float u2 = (b + 1) / (float) numBars;
                    float f1 = GetNormFreq(u1);
                    float f2 = GetNormFreq(u2);
                    float fCenter = Mathf.Sqrt(f1 * f2);

                    float mag = SampleFftMagnitude(fCenter, sampleRate, binCount);
                    float db = 20f * Mathf.Log10(Mathf.Max(mag, 1e-6f));
                    float normY = Mathf.Clamp01((db - _fftMinDb) / (_fftMaxDb - _fftMinDb));

                    float barH = normY * plotHeight;
                    float barX = plotRect.x + (b * totalSlotWidth) + (barSpacing * 0.5f);
                    float barY = plotRect.y + plotHeight - barH;

                    Color barColor = GetBandColorForFreq(fCenter);
                    if (_fftDisplayStyle == FftDisplayStyle.Both)
                    {
                        barColor.a = 0.35f;
                    }

                    EditorGUI.DrawRect(new Rect(barX, barY, barWidth, barH), barColor);

                    if (_fftPeakHoldEnabled && _peakFft != null)
                    {
                        float peakDb = SamplePeakFftDb(fCenter, sampleRate, binCount);
                        float peakNormY = Mathf.Clamp01((peakDb - _fftMinDb) / (_fftMaxDb - _fftMinDb));
                        float peakY = plotRect.y + plotHeight - (peakNormY * plotHeight);
                        EditorGUI.DrawRect(new Rect(barX, peakY - 1, barWidth, 2), new Color(1f, 0.85f, 0.3f, 0.85f));
                    }
                }
            }

            if (_fftDisplayStyle == FftDisplayStyle.FilledCurve || _fftDisplayStyle == FftDisplayStyle.Both)
            {
                int steps = Mathf.Clamp((int) (plotWidth / 2.5f), 100, 400);
                var curvePoints = new List<Vector3>(steps + 1);
                var peakPoints = new List<Vector3>(steps + 1);

                for (int s = 0; s <= steps; s++)
                {
                    float u = s / (float) steps;
                    float screenX = plotRect.x + (u * plotWidth);
                    float f = GetNormFreq(u);

                    float mag = SampleFftMagnitude(f, sampleRate, binCount);
                    float db = 20f * Mathf.Log10(Mathf.Max(mag, 1e-6f));
                    float normY = Mathf.Clamp01((db - _fftMinDb) / (_fftMaxDb - _fftMinDb));
                    float screenY = plotRect.y + plotHeight - (normY * plotHeight);

                    curvePoints.Add(new Vector3(screenX, screenY, 0));

                    float sliceH = (plotRect.y + plotHeight) - screenY;
                    if (sliceH > 1f && _fftDisplayStyle == FftDisplayStyle.FilledCurve)
                    {
                        float sliceW = (plotWidth / steps) + 0.5f;
                        Color fillCol = new Color(0.12f, 0.65f, 0.95f, (normY * 0.28f) + 0.03f);
                        EditorGUI.DrawRect(new Rect(screenX, screenY, sliceW, sliceH), fillCol);
                    }

                    if (_fftPeakHoldEnabled && _peakFft != null)
                    {
                        float peakDb = SamplePeakFftDb(f, sampleRate, binCount);
                        float peakNormY = Mathf.Clamp01((peakDb - _fftMinDb) / (_fftMaxDb - _fftMinDb));
                        float peakScreenY = plotRect.y + plotHeight - (peakNormY * plotHeight);
                        peakPoints.Add(new Vector3(screenX, peakScreenY, 0));
                    }
                }

                if (_fftPeakHoldEnabled && peakPoints.Count > 1)
                {
                    Handles.color = new Color(1f, 0.8f, 0.25f, 0.7f);
                    Handles.DrawAAPolyLine(1.5f, peakPoints.ToArray());
                }

                if (curvePoints.Count > 1)
                {
                    Handles.color = new Color(0.1f, 0.85f, 1f, 0.35f);
                    Handles.DrawAAPolyLine(4.5f, curvePoints.ToArray());
                    Handles.color = new Color(0.35f, 0.95f, 1f, 1f);
                    Handles.DrawAAPolyLine(2.0f, curvePoints.ToArray());
                }
            }

            Handles.color = new Color(0.25f, 0.28f, 0.35f, 1f);
            Handles.DrawPolyLine(
                new Vector3(plotRect.x, plotRect.y, 0),
                new Vector3(plotRect.x, plotRect.y + plotHeight, 0),
                new Vector3(plotRect.x + plotWidth, plotRect.y + plotHeight, 0)
            );

            if (_bassSong == null)
            {
                var hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.6f, 0.65f, 0.75f, 0.6f) }
                };
                GUI.Label(new Rect(plotRect.x, plotRect.y + (plotHeight * 0.35f), plotWidth, 24), "Load an audio file or song folder above and press Play to visualize spectrum", hintStyle);
            }
            else if (_bassSong.IsPaused && _dominantDb <= _fftMinDb + 1f)
            {
                var hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.6f, 0.65f, 0.75f, 0.6f) }
                };
                GUI.Label(new Rect(plotRect.x, plotRect.y + (plotHeight * 0.35f), plotWidth, 24), "Playback paused. Press Play to start live FFT analysis", hintStyle);
            }

            DrawFftHoverCrosshair(rect, plotRect, paddingLeft, paddingTop, plotWidth, plotHeight, sampleRate, binCount);
        }

        private void DrawFftHoverCrosshair(Rect rect, Rect plotRect, float paddingLeft, float paddingTop, float plotWidth, float plotHeight, int sampleRate, int binCount)
        {
            Vector2 mousePos = Event.current.mousePosition;
            if (!plotRect.Contains(mousePos))
            {
                return;
            }

            float plotX = mousePos.x - plotRect.x;
            float normX = Mathf.Clamp01(plotX / plotWidth);
            float freq = GetNormFreq(normX);

            float mag = SampleFftMagnitude(freq, sampleRate, binCount);
            float db = 20f * Mathf.Log10(Mathf.Max(mag, 1e-6f));
            float normY = Mathf.Clamp01((db - _fftMinDb) / (_fftMaxDb - _fftMinDb));
            float curY = plotRect.y + plotHeight - (normY * plotHeight);

            Handles.color = new Color(1f, 1f, 1f, 0.4f);
            Handles.DrawLine(new Vector3(mousePos.x, plotRect.y, 0), new Vector3(mousePos.x, plotRect.y + plotHeight, 0));
            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            Handles.DrawLine(new Vector3(plotRect.x, curY, 0), new Vector3(plotRect.x + plotWidth, curY, 0));

            EditorGUI.DrawRect(new Rect(mousePos.x - 3f, curY - 3f, 6f, 6f), new Color(0.15f, 0.95f, 1f, 0.4f));
            EditorGUI.DrawRect(new Rect(mousePos.x - 1.5f, curY - 1.5f, 3f, 3f), new Color(0.5f, 1f, 1f, 1f));

            string noteText = "--";
            if (freq >= 20f)
            {
                float midi = FreqToMidi(freq);
                int roundedMidi = (int) MathF.Round(midi);
                int noteIndex = ((roundedMidi % 12) + 12) % 12;
                int octave = (roundedMidi / 12) - 1;
                float cents = (midi - roundedMidi) * 100f;
                noteText = $"{NOTE_NAMES[noteIndex]}{octave} ({cents:+0;-0;0}c)";
            }

            string bandName = GetBandNameForFreq(freq);
            string freqStr = freq >= 1000f ? $"{freq / 1000f:F2} kHz" : $"{freq:F0} Hz";
            string tooltip = $"Freq: {freqStr}\nNote: {noteText}\nLevel: {db:F1} dBFS\nBand: {bandName}";

            var tooltipContent = new GUIContent(tooltip);
            var tooltipStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            Vector2 tooltipSize = tooltipStyle.CalcSize(tooltipContent) + new Vector2(10, 6);
            float tooltipX = mousePos.x + 12;
            if (tooltipX + tooltipSize.x > rect.x + rect.width - 6)
            {
                tooltipX = mousePos.x - tooltipSize.x - 12;
            }

            float tooltipY = Mathf.Clamp(mousePos.y - (tooltipSize.y / 2), plotRect.y, plotRect.y + plotHeight - tooltipSize.y);
            GUI.Box(new Rect(tooltipX, tooltipY, tooltipSize.x, tooltipSize.y), tooltipContent, tooltipStyle);
        }



        private float SampleFftMagnitude(float freq, int sampleRate, int binCount)
        {
            if (_smoothedFft == null || _smoothedFft.Length == 0 || binCount <= 0)
            {
                return 0f;
            }

            float nyquist = sampleRate * 0.5f;
            float freqPerBin = nyquist / binCount;
            float binFloat = freq / freqPerBin;

            int binIndex = (int) binFloat;
            if (binIndex < 0)
            {
                return _smoothedFft[0];
            }
            if (binIndex >= _smoothedFft.Length - 1)
            {
                return _smoothedFft[_smoothedFft.Length - 1];
            }

            float frac = binFloat - binIndex;
            return Mathf.Lerp(_smoothedFft[binIndex], _smoothedFft[binIndex + 1], frac);
        }

        private float SamplePeakFftDb(float freq, int sampleRate, int binCount)
        {
            if (_peakFft == null || _peakFft.Length == 0 || binCount <= 0)
            {
                return _fftMinDb;
            }

            float nyquist = sampleRate * 0.5f;
            float freqPerBin = nyquist / binCount;
            float binFloat = freq / freqPerBin;

            int binIndex = (int) binFloat;
            if (binIndex < 0)
            {
                return _peakFft[0];
            }
            if (binIndex >= _peakFft.Length - 1)
            {
                return _peakFft[_peakFft.Length - 1];
            }

            float frac = binFloat - binIndex;
            return Mathf.Lerp(_peakFft[binIndex], _peakFft[binIndex + 1], frac);
        }

        private float GetFreqNorm(float freq)
        {
            if (_fftScaleMode == FftScaleMode.Linear)
            {
                return Mathf.Clamp01((freq - FFT_MIN_FREQ) / (FFT_MAX_FREQ - FFT_MIN_FREQ));
            }

            float clampedFreq = Mathf.Clamp(freq, FFT_MIN_FREQ, FFT_MAX_FREQ);
            return Mathf.Clamp01(Mathf.Log10(clampedFreq / FFT_MIN_FREQ) / Mathf.Log10(FFT_MAX_FREQ / FFT_MIN_FREQ));
        }

        private float GetNormFreq(float normX)
        {
            float clampedNorm = Mathf.Clamp01(normX);
            if (_fftScaleMode == FftScaleMode.Linear)
            {
                return FFT_MIN_FREQ + (clampedNorm * (FFT_MAX_FREQ - FFT_MIN_FREQ));
            }

            return FFT_MIN_FREQ * Mathf.Pow(FFT_MAX_FREQ / FFT_MIN_FREQ, clampedNorm);
        }

        private Color GetBandColorForFreq(float freq)
        {
            for (int i = 0; i < _fftBands.Length; i++)
            {
                if (freq >= _fftBands[i].MinFreq && freq < _fftBands[i].MaxFreq)
                {
                    return _fftBands[i].BandColor;
                }
            }
            return new Color(0.2f, 0.85f, 0.95f);
        }

        private string GetBandNameForFreq(float freq)
        {
            for (int i = 0; i < _fftBands.Length; i++)
            {
                if (freq >= _fftBands[i].MinFreq && freq < _fftBands[i].MaxFreq)
                {
                    return _fftBands[i].Name;
                }
            }
            return "Audio Band";
        }

        private static float FreqToMidi(float freq)
        {
            if (freq < 1e-4f)
            {
                return 0f;
            }

            return 69f + (12f * (float) (Math.Log(freq / 440.0) / 0.6931471805599453));
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            int mins = (int) (seconds / 60);
            double secs = seconds % 60;
            int wholeSecs = (int) secs;
            int frac = (int) ((secs - wholeSecs) * 100);
            return $"{mins:00}:{wholeSecs:00}.{frac:02}";
        }

    }
}
