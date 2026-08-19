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
        private static void BuildPositionJitterValues(IReadOnlyList<PositionSample> samples,
            List<double> heardValues, List<double>? controlValues = null)
        {
            heardValues.Add(0.0);
            controlValues?.Add(0.0);

            for (int i = 1; i < samples.Count; i++)
            {
                var previous = samples[i - 1];
                var current = samples[i];
                double elapsed = current.RealTime - previous.RealTime;
                if (elapsed <= 0)
                {
                    heardValues.Add(0.0);
                    controlValues?.Add(0.0);
                    continue;
                }

                heardValues.Add(((current.HeardPosition - previous.HeardPosition) - elapsed) * 1000.0);
                controlValues?.Add(((current.ControlPosition - previous.ControlPosition) - elapsed) * 1000.0);
            }
        }

        private void DrawGraphArea(Rect rect)
        {
            if (rect.width < 10 || rect.height < 10)
            {
                return;
            }

            float paddingLeft = 52f;
            float paddingBottom = 20f;
            float paddingTop = 10f;
            float paddingRight = 10f;

            float plotWidth = rect.width - paddingLeft - paddingRight;
            float plotHeight = rect.height - paddingTop - paddingBottom;
            var plotRect = new Rect(rect.x + paddingLeft, rect.y + paddingTop, plotWidth, plotHeight);

            if (_graphMode == GraphMode.Oscilloscope)
            {
                DrawMainOscilloscopeGraph(rect, plotRect, paddingLeft, paddingTop, paddingRight, paddingBottom, plotWidth, plotHeight);
                return;
            }

            if (_graphMode == GraphMode.MicPitchAndHits)
            {
                DrawMicGraph(rect, plotRect, paddingLeft, paddingTop, paddingRight, paddingBottom, plotWidth, plotHeight);
                return;
            }

            if (_graphMode == GraphMode.FrequencySpectrum)
            {
                DrawFftSpectrumGraph(rect, plotRect, paddingLeft, paddingTop, paddingRight, paddingBottom, plotWidth, plotHeight);
                return;
            }

            double latestTime = _samples.Count > 0 ? _samples[_samples.Count - 1].RealTime : 0;
            double firstTime = _samples.Count > 0 ? _samples[0].RealTime : 0;

            if (_autoScroll || _viewEndTime < 0)
            {
                _viewEndTime = latestTime;
            }
            else
            {
                _viewEndTime = Math.Clamp(_viewEndTime, firstTime + _graphTimeWindow, Math.Max(firstTime + _graphTimeWindow, latestTime));
            }

            double maxTime = _viewEndTime;
            double minTime = Math.Max(firstTime, maxTime - _graphTimeWindow);
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

            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.10f, 1f));

            if (_samples.Count < 2)
            {
                var centeredStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 12
                };
                GUI.Label(rect, "No playback position sampled yet. Press Play to visualize real-time sliding window jitter.", centeredStyle);
                return;
            }

            var windowSamples = new List<PositionSample>();
            for (int i = 0; i < _samples.Count; i++)
            {
                var s = _samples[i];
                if (s.RealTime >= minTime && s.RealTime <= maxTime)
                {
                    windowSamples.Add(s);
                }
            }

            if (windowSamples.Count < 2)
            {
                return;
            }

            var heardValues = new List<double>(windowSamples.Count);
            var controlValues = new List<double>(windowSamples.Count);

            switch (_graphMode)
            {
                case GraphMode.SyncConvergence:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        heardValues.Add(windowSamples[i].HeardErrorMs);
                        controlValues.Add(windowSamples[i].ControlErrorMs);
                    }
                    break;
                }

                case GraphMode.PositionJitter:
                {
                    BuildPositionJitterValues(windowSamples, heardValues, controlValues);
                    break;
                }

                case GraphMode.FrameStepDelta:
                {
                    heardValues.Add(16.67);
                    controlValues.Add(16.67);

                    for (int i = 1; i < windowSamples.Count; i++)
                    {
                        double dHeardMs = (windowSamples[i].HeardPosition - windowSamples[i - 1].HeardPosition) * 1000.0;
                        double dControlMs = (windowSamples[i].ControlPosition - windowSamples[i - 1].ControlPosition) * 1000.0;
                        heardValues.Add(dHeardMs);
                        controlValues.Add(dControlMs);
                    }
                    break;
                }

                case GraphMode.PositionMappingStep:
                {
                    heardValues.Add(16.67);
                    controlValues.Add(16.67);

                    for (int i = 1; i < windowSamples.Count; i++)
                    {
                        double heardStep = (windowSamples[i].HeardPosition - windowSamples[i - 1].HeardPosition) * 1000.0;
                        double outputStep = (windowSamples[i].OutputFramePosition - windowSamples[i - 1].OutputFramePosition) * 1000.0;
                        heardValues.Add(heardStep);
                        controlValues.Add(outputStep);
                    }
                    break;
                }

                case GraphMode.CallbackTimingStep:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        heardValues.Add(windowSamples[i].CallbackFramesMs);
                        controlValues.Add(windowSamples[i].CallbackElapsedMs);
                    }
                    break;
                }

                case GraphMode.ControlHeardDelta:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        double deltaMs = (windowSamples[i].ControlPosition - windowSamples[i].HeardPosition) * 1000.0;
                        heardValues.Add(0.0);
                        controlValues.Add(deltaMs);
                    }
                    break;
                }

                case GraphMode.ClockDrift:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        heardValues.Add(0.0);
                        controlValues.Add(windowSamples[i].DriftErrorMs);
                    }
                    break;
                }

                case GraphMode.AbsolutePosition:
                default:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        heardValues.Add(windowSamples[i].HeardPosition);
                        controlValues.Add(windowSamples[i].ControlPosition);
                    }
                    break;
                }
            }

            double minY = double.MaxValue;
            double maxY = double.MinValue;

            for (int i = 0; i < heardValues.Count; i++)
            {
                if (heardValues[i] < minY) minY = heardValues[i];
                if (heardValues[i] > maxY) maxY = heardValues[i];
                if (controlValues[i] < minY) minY = controlValues[i];
                if (controlValues[i] > maxY) maxY = controlValues[i];
            }

            if (_graphMode == GraphMode.SyncConvergence)
            {
                if (_jitterScaleMs > 0)
                {
                    minY = -_jitterScaleMs;
                    maxY = _jitterScaleMs;
                }
                else
                {
                    double absMax = Math.Max(Math.Abs(minY), Math.Abs(maxY));
                    absMax = Math.Max(absMax, 5.0);
                    minY = -absMax;
                    maxY = absMax;
                }
            }
            else if (_graphMode == GraphMode.PositionJitter)
            {
                if (_jitterScaleMs > 0)
                {
                    minY = -_jitterScaleMs;
                    maxY = _jitterScaleMs;
                }
                else
                {
                    double absMax = Math.Max(Math.Abs(minY), Math.Abs(maxY));
                    absMax = Math.Max(absMax, 1.0);
                    minY = -absMax;
                    maxY = absMax;
                }
            }
            else if (_graphMode == GraphMode.FrameStepDelta || _graphMode == GraphMode.PositionMappingStep)
            {
                if (_jitterScaleMs > 0)
                {
                    minY = Math.Max(0, 16.67 - _jitterScaleMs);
                    maxY = 16.67 + _jitterScaleMs;
                }
                else
                {
                    minY = Math.Min(minY, 0);
                    maxY = Math.Max(maxY, 33.33);
                }
            }
            else if (_graphMode == GraphMode.CallbackTimingStep)
            {
                minY = Math.Min(minY, 0);
                maxY = Math.Max(maxY, 25);
            }
            else if (_graphMode == GraphMode.ControlHeardDelta || _graphMode == GraphMode.ClockDrift)
            {
                if (_jitterScaleMs > 0)
                {
                    minY = -_jitterScaleMs;
                    maxY = _jitterScaleMs;
                }
                else
                {
                    double absMax = Math.Max(Math.Abs(minY), Math.Abs(maxY));
                    absMax = Math.Max(absMax, 5.0);
                    minY = -absMax;
                    maxY = absMax;
                }
            }

            if (minY >= maxY)
            {
                minY = -1.0;
                maxY = 1.0;
            }

            double yRange = maxY - minY;

            DrawGrid(rect, minTime, maxTime, minY, maxY, _graphMode);

            if (_graphMode == GraphMode.PositionJitter || _graphMode == GraphMode.ControlHeardDelta || _graphMode == GraphMode.SyncConvergence || _graphMode == GraphMode.ClockDrift)
            {
                float normZeroY = (float) ((0.0 - minY) / yRange);
                if (normZeroY >= 0f && normZeroY <= 1f)
                {
                    float screenZeroY = rect.y + paddingTop + plotHeight - (normZeroY * plotHeight);
                    EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenZeroY, plotWidth, 1), new Color(1f, 1f, 1f, 0.35f));
                }

                if (_graphMode == GraphMode.SyncConvergence)
                {
                    float normStartPos = (float) ((3.0 - minY) / yRange);
                    float normStartNeg = (float) ((-3.0 - minY) / yRange);
                    if (normStartPos >= 0f && normStartPos <= 1f)
                    {
                        float screenY = rect.y + paddingTop + plotHeight - (normStartPos * plotHeight);
                        EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY, plotWidth, 1), new Color(1f, 0.75f, 0.15f, 0.25f));
                    }
                    if (normStartNeg >= 0f && normStartNeg <= 1f)
                    {
                        float screenY = rect.y + paddingTop + plotHeight - (normStartNeg * plotHeight);
                        EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY, plotWidth, 1), new Color(1f, 0.75f, 0.15f, 0.25f));
                    }

                    float normStopPos = (float) ((1.5 - minY) / yRange);
                    float normStopNeg = (float) ((-1.5 - minY) / yRange);
                    if (normStopPos >= 0f && normStopPos <= 1f)
                    {
                        float screenY = rect.y + paddingTop + plotHeight - (normStopPos * plotHeight);
                        EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY, plotWidth, 1), new Color(0.2f, 0.85f, 0.35f, 0.20f));
                    }
                    if (normStopNeg >= 0f && normStopNeg <= 1f)
                    {
                        float screenY = rect.y + paddingTop + plotHeight - (normStopNeg * plotHeight);
                        EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY, plotWidth, 1), new Color(0.2f, 0.85f, 0.35f, 0.20f));
                    }
                }
            }
            else if (_graphMode == GraphMode.FrameStepDelta || _graphMode == GraphMode.PositionMappingStep)
            {
                float normNominalY = (float) ((16.67 - minY) / yRange);
                if (normNominalY >= 0f && normNominalY <= 1f)
                {
                    float screenNominalY = rect.y + paddingTop + plotHeight - (normNominalY * plotHeight);
                    EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenNominalY, plotWidth, 1), new Color(1f, 1f, 1f, 0.3f));
                }
            }

            if (_graphMode == GraphMode.SyncConvergence)
            {
                int i = 0;
                while (i < windowSamples.Count)
                {
                    var s = windowSamples[i];
                    bool isSettling = s.SyncState == AudioSynchronizer.SyncState.Settling;
                    bool isAdjusting = Math.Abs(s.Adjustment) > 0.0001f || s.SyncState == AudioSynchronizer.SyncState.Correcting;

                    if (!isSettling && !isAdjusting)
                    {
                        i++;
                        continue;
                    }

                    int startIdx = i;
                    bool currentIsSettling = isSettling;

                    while (i < windowSamples.Count)
                    {
                        var next = windowSamples[i];
                        bool nextSettling = next.SyncState == AudioSynchronizer.SyncState.Settling;
                        bool nextAdjusting = Math.Abs(next.Adjustment) > 0.0001f || next.SyncState == AudioSynchronizer.SyncState.Correcting;

                        if (currentIsSettling ? !nextSettling : !nextAdjusting)
                        {
                            break;
                        }

                        i++;
                    }

                    double startTime = windowSamples[startIdx].RealTime;
                    double endTime = i < windowSamples.Count ? windowSamples[i].RealTime : windowSamples[i - 1].RealTime + SAMPLE_INTERVAL;

                    float normX0 = (float) ((startTime - minTime) / (maxTime - minTime));
                    float normX1 = (float) ((endTime - minTime) / (maxTime - minTime));
                    float x0 = rect.x + paddingLeft + (normX0 * plotWidth);
                    float x1 = rect.x + paddingLeft + (normX1 * plotWidth);
                    float width = Math.Max(1f, x1 - x0);

                    Color bandColor = currentIsSettling
                        ? new Color(0.25f, 0.65f, 1f, 0.08f)
                        : new Color(1f, 0.65f, 0.15f, 0.15f);

                    EditorGUI.DrawRect(new Rect(x0, rect.y + paddingTop, width, plotHeight), bandColor);
                }
            }

            var heardPoints = new List<Vector3>(windowSamples.Count);
            var controlPoints = new List<Vector3>(windowSamples.Count);
            var targetPoints = new List<Vector3>(windowSamples.Count);

            for (int i = 0; i < windowSamples.Count; i++)
            {
                float normX = (float) ((windowSamples[i].RealTime - minTime) / (maxTime - minTime));
                float screenX = rect.x + paddingLeft + (normX * plotWidth);

                float normHeardY = Mathf.Clamp01((float) ((heardValues[i] - minY) / yRange));
                float screenHeardY = rect.y + paddingTop + plotHeight - (normHeardY * plotHeight);
                heardPoints.Add(new Vector3(screenX, screenHeardY, 0));

                float normControlY = Mathf.Clamp01((float) ((controlValues[i] - minY) / yRange));
                float screenControlY = rect.y + paddingTop + plotHeight - (normControlY * plotHeight);
                controlPoints.Add(new Vector3(screenX, screenControlY, 0));

                if (_graphMode == GraphMode.AbsolutePosition)
                {
                    float normTargetY = Mathf.Clamp01((float) ((windowSamples[i].TargetTime - minY) / yRange));
                    float screenTargetY = rect.y + paddingTop + plotHeight - (normTargetY * plotHeight);
                    targetPoints.Add(new Vector3(screenX, screenTargetY, 0));
                }
            }

            Handles.color = new Color(0.25f, 0.28f, 0.35f, 1f);
            Handles.DrawPolyLine(
                new Vector3(rect.x + paddingLeft, rect.y + paddingTop, 0),
                new Vector3(rect.x + paddingLeft, rect.y + paddingTop + plotHeight, 0),
                new Vector3(rect.x + paddingLeft + plotWidth, rect.y + paddingTop + plotHeight, 0)
            );

            if (_graphMode == GraphMode.AbsolutePosition && targetPoints.Count > 1)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.75f);
                Handles.DrawAAPolyLine(1.8f, targetPoints.ToArray());
            }

            if (controlPoints.Count > 1)
            {
                Handles.color = new Color(1f, 0.65f, 0.15f, 0.9f);
                Handles.DrawAAPolyLine(2.2f, controlPoints.ToArray());
            }

            if (heardPoints.Count > 1)
            {
                Handles.color = new Color(0f, 0.85f, 1f, 1f);
                Handles.DrawAAPolyLine(2.2f, heardPoints.ToArray());
            }

            DrawGraphHoverCrosshair(rect, minTime, maxTime, paddingLeft, paddingTop, plotWidth, plotHeight, windowSamples, heardValues, controlValues);
        }

        private void DrawGraphHoverCrosshair(Rect rect, double minTime, double maxTime, float paddingLeft, float paddingTop, float plotWidth, float plotHeight, List<PositionSample> windowSamples, List<double> heardValues, List<double> controlValues)
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
            double heardVal = heardValues[closestIdx];
            double controlVal = controlValues[closestIdx];

            float sampleNormX = (float) ((sample.RealTime - minTime) / (maxTime - minTime));
            float sampleScreenX = rect.x + paddingLeft + (sampleNormX * plotWidth);

            Handles.color = new Color(1f, 1f, 1f, 0.4f);
            Handles.DrawLine(
                new Vector3(sampleScreenX, rect.y + paddingTop, 0),
                new Vector3(sampleScreenX, rect.y + paddingTop + plotHeight, 0)
            );

            string tooltip = _graphMode switch
            {
                GraphMode.PositionJitter => $"Time: {sample.RealTime:F2}s\nHeard Jitter: {heardVal:+0.00;-0.00;0.00} ms\nCtrl Jitter: {controlVal:+0.00;-0.00;0.00} ms",
                GraphMode.SyncConvergence => $"Time: {sample.RealTime:F2}s\nHeard Err: {sample.HeardErrorMs:+0.00;-0.00;0.00} ms\nCtrl Err: {sample.ControlErrorMs:+0.00;-0.00;0.00} ms\nState: {sample.SyncState} ({sample.Adjustment * 100:+0.00;-0.00;0.00}%)",
                GraphMode.FrameStepDelta => $"Time: {sample.RealTime:F2}s\nHeard Step: {heardVal:F2} ms\nCtrl Step: {controlVal:F2} ms",
                GraphMode.PositionMappingStep => $"Time: {sample.RealTime:F2}s\nHeard Step: {heardVal:F2} ms\nOutput Step: {controlVal:F2} ms",
                GraphMode.CallbackTimingStep => $"Time: {sample.RealTime:F2}s\nCallback: {heardVal:F2} ms\nElapsed: {controlVal:F2} ms\nCorrection: {sample.CallbackCorrectionMs:+0.00;-0.00;0.00} ms\nClock Offset: {sample.CallbackClockOffsetMs:+0.00;-0.00;0.00} ms",
                GraphMode.ControlHeardDelta => $"Time: {sample.RealTime:F2}s\nDelta: {controlVal:+0.00;-0.00;0.00} ms",
                GraphMode.ClockDrift => $"Time: {sample.RealTime:F2}s\nDrift: {sample.DriftErrorMs:+0.00;-0.00;0.00} ms\nRate: {_driftRatePpm:+0.0;-0.0;0.0} ppm ({_driftMsPerMin:+0.00;-0.00;0.00} ms/min)",
                _ => $"Time: {sample.RealTime:F2}s\nTarget: {sample.TargetTime:F3}s\nHeard: {sample.HeardPosition:F3}s\nCtrl: {sample.ControlPosition:F3}s"
            };

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

        private void DrawGraphTimelineMiniBar()
        {
            if (_graphMode == GraphMode.FrequencySpectrum || _graphMode == GraphMode.Oscilloscope)
            {
                if (_graphMode == GraphMode.FrequencySpectrum)
                {
                    DrawFftTimelineMiniBar();
                }
                return;
            }

            double firstTime;
            double latestTime;

            if (_graphMode == GraphMode.MicPitchAndHits)
            {
                if (_micSamples.Count < 2)
                {
                    return;
                }
                firstTime = _micSamples[0].RealTime;
                latestTime = _micSamples[_micSamples.Count - 1].RealTime;
            }
            else
            {
                if (_samples.Count < 2)
                {
                    return;
                }
                firstTime = _samples[0].RealTime;
                latestTime = _samples[_samples.Count - 1].RealTime;
            }

            double totalSpan = latestTime - firstTime;

            if (totalSpan <= 0.05)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                Rect barRect = GUILayoutUtility.GetRect(100, 1000, 14, 14);
                EditorGUI.DrawRect(barRect, new Color(0.12f, 0.13f, 0.16f, 1f));

                double maxTime = _autoScroll ? latestTime : _viewEndTime;
                double minTime = Math.Max(firstTime, maxTime - _graphTimeWindow);

                float normStart = (float) Math.Clamp((minTime - firstTime) / totalSpan, 0.0, 1.0);
                float normEnd = (float) Math.Clamp((maxTime - firstTime) / totalSpan, 0.0, 1.0);

                float viewX = barRect.x + (normStart * barRect.width);
                float viewWidth = Math.Max(6f, (normEnd - normStart) * barRect.width);

                EditorGUI.DrawRect(new Rect(viewX, barRect.y + 1, viewWidth, barRect.height - 2), new Color(0.2f, 0.6f, 0.95f, 0.45f));

                var evt = Event.current;
                if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && barRect.Contains(evt.mousePosition))
                {
                    float clickedNorm = (evt.mousePosition.x - barRect.x) / barRect.width;
                    double clickedTime = firstTime + (clickedNorm * totalSpan);
                    _viewEndTime = Math.Clamp(clickedTime + (_graphTimeWindow * 0.5), firstTime + _graphTimeWindow, latestTime);
                    _autoScroll = false;
                    evt.Use();
                    Repaint();
                }

                if (!_autoScroll)
                {
                    GUILayout.Space(6);
                    if (GUILayout.Button("Jump to Live ⏩", EditorStyles.miniButton, GUILayout.Width(110), GUILayout.Height(14)))
                    {
                        _autoScroll = true;
                        _viewEndTime = latestTime;
                        Repaint();
                    }
                }
            }
        }

        private static void DrawGrid(Rect rect, double minTime, double maxTime, double minY, double maxY, GraphMode mode)
        {
            float paddingLeft = 52f;
            float paddingBottom = 20f;
            float paddingTop = 10f;
            float paddingRight = 10f;

            float plotWidth = rect.width - paddingLeft - paddingRight;
            float plotHeight = rect.height - paddingTop - paddingBottom;

            const int NUM_H_DIVS = 4;
            for (int i = 0; i <= NUM_H_DIVS; i++)
            {
                float normY = (float) i / NUM_H_DIVS;
                float y = rect.y + paddingTop + plotHeight - (normY * plotHeight);
                double yValue = minY + (normY * (maxY - minY));

                EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, y, plotWidth, 1), new Color(0.18f, 0.20f, 0.24f, 0.6f));

                string label = mode switch
                {
                    GraphMode.AbsolutePosition => $"{yValue:F2}s",
                    GraphMode.PositionJitter => $"{yValue:+0.0;-0.0;0.0}ms",
                    GraphMode.SyncConvergence => $"{yValue:+0.0;-0.0;0.0}ms",
                    GraphMode.FrameStepDelta => $"{yValue:F1}ms",
                    GraphMode.PositionMappingStep => $"{yValue:F1}ms",
                    GraphMode.ControlHeardDelta => $"{yValue:+0.0;-0.0;0.0}ms",
                    GraphMode.ClockDrift => $"{yValue:+0.0;-0.0;0.0}ms",
                    _ => $"{yValue:F1}"
                };

                GUI.Label(new Rect(rect.x, y - 9, paddingLeft - 4, 18), label, EditorStyles.miniLabel);
            }

            const int NUM_V_DIVS = 5;
            for (int i = 0; i <= NUM_V_DIVS; i++)
            {
                float normX = (float) i / NUM_V_DIVS;
                float x = rect.x + paddingLeft + (normX * plotWidth);
                double timeValue = minTime + (normX * (maxTime - minTime));

                EditorGUI.DrawRect(new Rect(x, rect.y + paddingTop, 1, plotHeight), new Color(0.18f, 0.20f, 0.24f, 0.6f));
                GUI.Label(new Rect(x - 25, rect.y + paddingTop + plotHeight + 2, 50, 16), $"{timeValue:F1}s", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawGraphHudRibbon()
        {
            if (_graphMode == GraphMode.MicPitchAndHits)
            {
                DrawMicHudRibbon();
                return;
            }

            if (_graphMode == GraphMode.FrequencySpectrum || _graphMode == GraphMode.Oscilloscope)
            {
                if (_graphMode == GraphMode.FrequencySpectrum)
                {
                    DrawFftHudRibbon();
                }
                return;
            }

            double heard = _bassSong?.GetPosition() ?? 0;
            double control = _bassSong?.GetControlPosition() ?? 0;
            double deltaMs = (control - heard) * 1000.0;

            double peakToPeakJitter = 0.0;
            double stdDevJitter = 0.0;

            if (_samples.Count >= 10)
            {
                double latest = _samples[_samples.Count - 1].RealTime;
                double windowStart = latest - _graphTimeWindow;
                var window = _samples.Where(s => s.RealTime >= windowStart).ToList();

                if (window.Count >= 4)
                {
                    var heardJitterValues = new List<double>(window.Count);
                    BuildPositionJitterValues(window, heardJitterValues);
                    var residuals = heardJitterValues.Skip(1).ToList();
                    peakToPeakJitter = residuals.Max() - residuals.Min();
                    double meanRes = residuals.Average();
                    stdDevJitter = Math.Sqrt(residuals.Average(r => (r - meanRes) * (r - meanRes)));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_graphMode == GraphMode.ClockDrift)
                {
                    double latestDrift = _samples.Count > 0 ? _samples[_samples.Count - 1].DriftErrorMs : _driftCumulativeMs;
                    Color driftColor = Math.Abs(latestDrift) > 10.0 ? new Color(1f, 0.35f, 0.35f) : (Math.Abs(latestDrift) > 3.0 ? new Color(1f, 0.75f, 0.2f) : new Color(0.25f, 0.95f, 0.45f));

                    DrawMetricTile("CUMULATIVE DRIFT", $"{latestDrift:+0.00;-0.00;0.00} ms", driftColor);
                    DrawMetricTile("ESTIMATED RATE", $"{_driftRatePpm:+0.0;-0.0;0.0} ppm", new Color(0.3f, 0.8f, 1f));
                    DrawMetricTile("DRIFT SPEED", $"{_driftMsPerMin:+0.00;-0.00;0.00} ms/min", new Color(0.85f, 0.65f, 1f));
                    DrawMetricTile("FRAME RATE", $"{_currentFps:F0} FPS", Color.white);
                }
                else if (_graphMode == GraphMode.SyncConvergence)
                {
                    double latestHeardErr = _samples.Count > 0 ? _samples[_samples.Count - 1].HeardErrorMs : 0;
                    double latestCtrlErr = _samples.Count > 0 ? _samples[_samples.Count - 1].ControlErrorMs : 0;
                    var state = _audioSynchronizer?.State ?? AudioSynchronizer.SyncState.Idle;
                    float adj = _audioSynchronizer?.EffectiveAdjustment ?? 0f;

                    var stateColor = !_modelSongSync ? Color.gray : (state == AudioSynchronizer.SyncState.Correcting ? new Color(1f, 0.7f, 0.15f) : (state == AudioSynchronizer.SyncState.Settling ? new Color(0.3f, 0.75f, 1f) : new Color(0.25f, 0.85f, 0.35f)));
                    string stateText = !_modelSongSync ? "SYNC OFF" : (state == AudioSynchronizer.SyncState.Correcting ? $"CORRECTING ({adj * 100:+0.0;-0.0}%)" : state.ToString().ToUpperInvariant());

                    DrawMetricTile("HEARD ERROR", $"{latestHeardErr:+0.00;-0.00;0.00} ms", new Color(0f, 0.85f, 1f));
                    DrawMetricTile("CONTROL ERROR", $"{latestCtrlErr:+0.00;-0.00;0.00} ms", new Color(1f, 0.65f, 0.15f));
                    DrawMetricTile("SYNC STATE", stateText, stateColor);
                    DrawMetricTile("FRAME RATE", $"{_currentFps:F0} FPS", Color.white);
                }
                else
                {
                    var jitterColor = peakToPeakJitter < 2.0 ? new Color(0.25f, 0.85f, 0.35f) : (peakToPeakJitter < 5.0 ? new Color(0.95f, 0.75f, 0.2f) : new Color(1f, 0.35f, 0.35f));
                    DrawMetricTile("PEAK-TO-PEAK", $"{peakToPeakJitter:F2} ms", jitterColor);
                    DrawMetricTile("STD DEVIATION", $"{stdDevJitter:F2} ms", new Color(0.8f, 0.85f, 0.9f));
                    DrawMetricTile("CTRL-HEARD Δ", $"{deltaMs:+0.0;-0.0;0.0} ms", new Color(1f, 0.65f, 0.15f));
                    DrawMetricTile("FRAME RATE", $"{_currentFps:F0} FPS", Color.white);
                }
            }
        }

    }
}
