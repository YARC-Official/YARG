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
        private void DrawMicGraph(Rect rect, Rect plotRect, float paddingLeft, float paddingTop, float paddingRight, float paddingBottom, float plotWidth, float plotHeight)
        {
            double now = EditorApplication.timeSinceStartup;
            var activeSlot = ActiveMicSlot;
            var primarySamples = activeSlot?.Samples ?? _fallbackMicSamples;

            double latestTime = primarySamples.Count > 0 ? primarySamples[primarySamples.Count - 1].RealTime : now;
            double firstTime = primarySamples.Count > 0 ? primarySamples[0].RealTime : now;

            for (int i = 0; i < _micSlots.Count; i++)
            {
                var sList = _micSlots[i].Samples;
                if (sList.Count > 0)
                {
                    if (sList[sList.Count - 1].RealTime > latestTime) latestTime = sList[sList.Count - 1].RealTime;
                    if (sList[0].RealTime < firstTime) firstTime = sList[0].RealTime;
                }
            }

            if (_autoScroll || _viewEndTime < 0)
            {
                _viewEndTime = latestTime;
            }
            else
            {
                _viewEndTime = Math.Clamp(_viewEndTime, firstTime + _graphTimeWindow, Math.Max(firstTime + _graphTimeWindow, latestTime));
            }

            double maxTime = _viewEndTime;
            double minTime = maxTime - _graphTimeWindow;
            if (maxTime <= minTime)
            {
                maxTime = minTime + 1.0;
            }

            var evt = Event.current;
            if (evt.type == EventType.ScrollWheel && rect.Contains(evt.mousePosition))
            {
                float zoomFactor = 1f + (evt.delta.y * 0.08f);
                _graphTimeWindow = Mathf.Clamp(_graphTimeWindow * zoomFactor, 0.25f, 30f);
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseDrag && (evt.button == 0 || evt.button == 2) && plotRect.Contains(evt.mousePosition))
            {
                _autoScroll = false;
                double dt = (evt.delta.x / plotWidth) * (maxTime - minTime);
                _viewEndTime -= dt;
                _viewEndTime = Math.Clamp(_viewEndTime, firstTime + _graphTimeWindow, Math.Max(firstTime + _graphTimeWindow, latestTime));
                evt.Use();
                Repaint();
            }

            if (evt.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.06f, 0.06f, 0.08f, 1f));

            bool hasAnySamples = _micSlots.Any(s => s.Samples.Count >= 2);
            if (!hasAnySamples)
            {
                var centeredStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 12
                };
                GUI.Label(rect, "No microphone samples recorded yet. Connect an input below and sing or speak.", centeredStyle);
                return;
            }

            double minY = 36.0;
            double maxY = 84.0;
            double yRange = maxY - minY;

            int[] octaves = { 36, 48, 60, 72 };
            for (int i = 0; i < octaves.Length; i++)
            {
                float yBottom = (float) ((octaves[i] - minY) / yRange);
                float yTop = (float) (((octaves[i] + 12) - minY) / yRange);
                float screenY0 = rect.y + paddingTop + plotHeight - (yTop * plotHeight);
                float h = (yTop - yBottom) * plotHeight;
                Color laneBg = (i % 2 == 0)
                    ? new Color(0.11f, 0.12f, 0.16f, 0.7f)
                    : new Color(0.07f, 0.08f, 0.10f, 0.7f);
                EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY0, plotWidth, h), laneBg);
            }

            int[] fifthMidis = { 43, 55, 67, 79 };
            string[] fifthLabels = { "G2", "G3", "G4", "G5" };
            for (int i = 0; i < fifthMidis.Length; i++)
            {
                float normY = (float) ((fifthMidis[i] - minY) / yRange);
                float y = rect.y + paddingTop + plotHeight - (normY * plotHeight);
                EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, y, plotWidth, 1), new Color(0.20f, 0.22f, 0.28f, 0.35f));
                var fifthStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.40f, 0.44f, 0.52f, 0.7f) },
                    fontSize = 8
                };
                GUI.Label(new Rect(rect.x + 8, y - 8, paddingLeft - 10, 16), fifthLabels[i], fifthStyle);
            }

            int[] octaveMidis = { 36, 48, 60, 72, 84 };
            string[] octaveLabels = { "C2", "C3", "C4", "C5", "C6" };
            for (int i = 0; i < octaveMidis.Length; i++)
            {
                float normY = (float) ((octaveMidis[i] - minY) / yRange);
                float y = rect.y + paddingTop + plotHeight - (normY * plotHeight);
                EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, y, plotWidth, 1), new Color(0.28f, 0.32f, 0.40f, 0.8f));
                var octaveStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = new Color(0.70f, 0.75f, 0.88f, 1f) }
                };
                float labelY = i == 0 ? y - 14 : (i == octaveMidis.Length - 1 ? y - 4 : y - 9);
                GUI.Label(new Rect(rect.x, labelY, paddingLeft - 4, 18), octaveLabels[i], octaveStyle);
            }

            const int NUM_V_DIVS = 5;
            for (int i = 0; i <= NUM_V_DIVS; i++)
            {
                float normX = (float) i / NUM_V_DIVS;
                float x = rect.x + paddingLeft + (normX * plotWidth);
                double timeValue = minTime + (normX * (maxTime - minTime));

                EditorGUI.DrawRect(new Rect(x, rect.y + paddingTop, 1, plotHeight), new Color(0.18f, 0.20f, 0.25f, 0.5f));
                GUI.Label(new Rect(x - 25, rect.y + paddingTop + plotHeight + 2, 50, 16), $"{timeValue:F1}s", EditorStyles.centeredGreyMiniLabel);
            }

            Handles.color = new Color(0.25f, 0.28f, 0.35f, 1f);
            Handles.DrawPolyLine(
                new Vector3(rect.x + paddingLeft, rect.y + paddingTop, 0),
                new Vector3(rect.x + paddingLeft, rect.y + paddingTop + plotHeight, 0),
                new Vector3(rect.x + paddingLeft + plotWidth, rect.y + paddingTop + plotHeight, 0)
            );

            // Draw pitch traces for all active slots
            for (int sIdx = 0; sIdx < _micSlots.Count; sIdx++)
            {
                var slot = _micSlots[sIdx];
                if (slot.Samples.Count < 2) continue;

                bool isCurrentActive = sIdx == _selectedMicSlotIndex;
                Color traceColor = slot.ThemeColor;
                if (!isCurrentActive)
                {
                    traceColor.a = 0.55f;
                }

                var windowSamples = new List<MicSample>();
                for (int i = 0; i < slot.Samples.Count; i++)
                {
                    var s = slot.Samples[i];
                    if (s.RealTime >= minTime && s.RealTime <= maxTime)
                    {
                        windowSamples.Add(s);
                    }
                }

                for (int i = 0; i < windowSamples.Count; i++)
                {
                    if (windowSamples[i].IsHit)
                    {
                        float normX = (float) ((windowSamples[i].RealTime - minTime) / (maxTime - minTime));
                        float screenX = rect.x + paddingLeft + (normX * plotWidth);

                        EditorGUI.DrawRect(new Rect(screenX - 1f, rect.y + paddingTop + plotHeight - 16, 2, 16), new Color(0.2f, 0.95f, 0.45f, 0.85f));
                        EditorGUI.DrawRect(new Rect(screenX - 3.5f, rect.y + paddingTop + plotHeight - 18, 7, 3), new Color(0.25f, 1f, 0.5f, 0.95f));
                    }
                }

                var currentSegment = new List<Vector3>();
                for (int i = 0; i < windowSamples.Count; i++)
                {
                    var s = windowSamples[i];
                    float normX = (float) ((s.RealTime - minTime) / (maxTime - minTime));
                    float screenX = rect.x + paddingLeft + (normX * plotWidth);

                    if (s.IsVoiced && s.MidiNote >= minY && s.MidiNote <= maxY)
                    {
                        float normY = Mathf.Clamp01((float) ((s.MidiNote - minY) / yRange));
                        float screenY = rect.y + paddingTop + plotHeight - (normY * plotHeight);
                        currentSegment.Add(new Vector3(screenX, screenY, 0));
                    }
                    else
                    {
                        DrawPitchSegment(currentSegment, traceColor);
                        currentSegment.Clear();
                    }
                }

                DrawPitchSegment(currentSegment, traceColor);

                if (isCurrentActive && windowSamples.Count >= 2)
                {
                    DrawMicGraphHoverCrosshair(rect, minTime, maxTime, paddingLeft, paddingTop, plotWidth, plotHeight, windowSamples);
                }
            }
        }

        private static void DrawPitchSegment(List<Vector3> segment, Color color)
        {
            if (segment.Count > 1)
            {
                var points = segment.ToArray();
                Handles.color = new Color(color.r, color.g, color.b, 0.25f);
                Handles.DrawAAPolyLine(5f, points);
                Handles.color = color;
                Handles.DrawAAPolyLine(2.2f, points);

                for (int p = 0; p < points.Length; p++)
                {
                    var pt = points[p];
                    EditorGUI.DrawRect(new Rect(pt.x - 1f, pt.y - 1f, 2f, 2f), new Color(Mathf.Min(1f, color.r + 0.3f), Mathf.Min(1f, color.g + 0.3f), Mathf.Min(1f, color.b + 0.3f), 0.85f));
                }
            }
            else if (segment.Count == 1)
            {
                var pt = segment[0];
                EditorGUI.DrawRect(new Rect(pt.x - 3f, pt.y - 3f, 6f, 6f), new Color(color.r, color.g, color.b, 0.35f));
                EditorGUI.DrawRect(new Rect(pt.x - 1.5f, pt.y - 1.5f, 3f, 3f), color);
            }
        }

        private void DrawMicGraphHoverCrosshair(Rect rect, double minTime, double maxTime, float paddingLeft, float paddingTop, float plotWidth, float plotHeight, List<MicSample> windowSamples)
        {
            Vector2 mousePos = Event.current.mousePosition;
            if (!rect.Contains(mousePos))
            {
                return;
            }

            float plotX = mousePos.x - (rect.x + paddingLeft);
            if (plotX < 0 || plotX > plotWidth || windowSamples.Count < 2)
            {
                return;
            }

            float normX = plotX / plotWidth;
            double targetTime = minTime + (normX * (maxTime - minTime));

            int closestIdx = 0;
            double minDiff = double.MaxValue;
            for (int i = 0; i < windowSamples.Count; i++)
            {
                double diff = Math.Abs(windowSamples[i].RealTime - targetTime);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestIdx = i;
                }
            }

            var sample = windowSamples[closestIdx];
            float sampleNormX = (float) ((sample.RealTime - minTime) / (maxTime - minTime));
            float sampleScreenX = rect.x + paddingLeft + (sampleNormX * plotWidth);

            Handles.color = new Color(1f, 1f, 1f, 0.4f);
            Handles.DrawLine(new Vector3(sampleScreenX, rect.y + paddingTop, 0), new Vector3(sampleScreenX, rect.y + paddingTop + plotHeight, 0));

            string noteLabel = "--";
            if (sample.IsVoiced && sample.MidiNote > 0)
            {
                int roundedMidi = (int) MathF.Round(sample.MidiNote);
                int noteIndex = ((roundedMidi % 12) + 12) % 12;
                int octave = (roundedMidi / 12) - 1;
                float cents = (sample.MidiNote - roundedMidi) * 100f;
                noteLabel = $"{NOTE_NAMES[noteIndex]}{octave} ({cents:+0;-0;0}c)";
            }

            string tooltip = $"Time: {sample.RealTime:F2}s\nNote: {noteLabel}\nMIDI: {(sample.IsVoiced ? sample.MidiNote.ToString("F2") : "--")}\nLevel: {sample.VolumeDb:F1} dB\nHit: {(sample.IsHit ? "YES" : "NO")}";

            var tooltipContent = new GUIContent(tooltip);
            var tooltipStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            Vector2 tooltipSize = tooltipStyle.CalcSize(tooltipContent) + new Vector2(10, 6);
            float tooltipX = sampleScreenX + 12;
            if (tooltipX + tooltipSize.x > rect.x + rect.width - 6)
            {
                tooltipX = sampleScreenX - tooltipSize.x - 12;
            }

            float tooltipY = Mathf.Clamp(mousePos.y - (tooltipSize.y / 2), rect.y + paddingTop, rect.y + paddingTop + plotHeight - tooltipSize.y);
            GUI.Box(new Rect(tooltipX, tooltipY, tooltipSize.x, tooltipSize.y), tooltipContent, tooltipStyle);
        }

        private void DrawMicHudRibbon()
        {
            var activeSlot = ActiveMicSlot;
            string devName = CleanDeviceName(activeSlot?.SelectedDevice?.DisplayName);
            string devText = activeSlot?.ActiveDevice != null ? $"{activeSlot.DisplayLabel}: {devName}" : "INACTIVE";
            Color devColor = activeSlot?.ActiveDevice != null ? activeSlot.ThemeColor : Color.gray;

            string noteText = activeSlot?.IsVoiced == true ? $"{activeSlot.CurrentNoteName} ({activeSlot.CurrentCents:+0;-0;0}c)" : "--";
            Color noteColor = activeSlot?.IsVoiced == true ? (MathF.Abs(activeSlot.CurrentCents) < 10f ? new Color(0.25f, 0.95f, 0.45f) : activeSlot.ThemeColor) : Color.gray;

            string dbText = activeSlot != null ? $"{activeSlot.CurrentDb:F1} dB" : "-160.0 dB";
            Color dbColor = (activeSlot?.CurrentDb ?? -160f) > 42f ? new Color(1f, 0.35f, 0.35f) : ((activeSlot?.CurrentDb ?? -160f) > 20f ? new Color(0.25f, 0.85f, 0.35f) : ((activeSlot?.CurrentDb ?? -160f) > 2f ? new Color(0.95f, 0.75f, 0.2f) : Color.gray));

            double hitAge = activeSlot != null ? (EditorApplication.timeSinceStartup - activeSlot.LastHitTime) : 999.0;
            int totalHits = activeSlot?.TotalHitCount ?? 0;
            string hitText = hitAge < 0.25 ? $"HIT! ({totalHits})" : $"{totalHits} Hits";
            Color hitColor = hitAge < 0.25 ? new Color(0.25f, 1f, 0.45f) : new Color(0.8f, 0.85f, 0.9f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricTile("ACTIVE INPUT", devText, devColor);
                DrawMetricTile("PITCH TRACK", noteText, noteColor);
                DrawMetricTile("LEVEL (RMS)", dbText, dbColor);
                DrawMetricTile("HIT DETECTIONS", hitText, hitColor);

                GUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(88), GUILayout.Height(30)))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reset Hits", EditorStyles.miniButton, GUILayout.Width(88), GUILayout.Height(22)))
                    {
                        if (activeSlot != null)
                        {
                            activeSlot.TotalHitCount = 0;
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

    }
}
