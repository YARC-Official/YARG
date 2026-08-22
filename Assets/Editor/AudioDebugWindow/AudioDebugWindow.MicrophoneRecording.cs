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
        #region Voice Check (Microphone Quick Record & Playback)

        private bool AttachMicRecordingChannel(MicSlot slot)
        {
            slot.DetachRecordingChannel();
            if (slot.ActiveDevice == null)
            {
                return false;
            }

            if (slot.ActiveDevice is BassMicDevice bassMic &&
                bassMic.TryCreateRecordingChannel(slot.RecordFx, out int channelHandle, out int sampleRate))
            {
                slot.RecordSampleRate = sampleRate;
                slot.RecordChannelHandle = channelHandle;
                if (slot.RecordState == MicVoiceCheckState.Recording)
                {
                    slot.StartRecordReader();
                }
                return true;
            }

            return false;
        }

        private void StartMicRecording(MicSlot slot, float duration)
        {
            EnsureAudioInitialized();

            if (slot.ActiveDevice == null)
            {
                if (slot.SelectedDevice.HasValue)
                {
                    ConnectMicSlot(slot, slot.SelectedDevice.Value);
                }
                else if (_availableMicDevices.Count > 0)
                {
                    ConnectMicSlot(slot, _availableMicDevices[0]);
                }

                if (slot.ActiveDevice == null)
                {
                    slot.StatusMessage = "Please select and connect a microphone first.";
                    slot.StatusIsError = true;
                    slot.LastStatusTime = EditorApplication.timeSinceStartup;
                    return;
                }
            }

            if (slot.PlaybackMixer != null && !slot.PlaybackMixer.IsPaused)
            {
                slot.PlaybackMixer.Pause();
                slot.PlaybackMixer.SetPosition(0);
            }

            if (!AttachMicRecordingChannel(slot))
            {
                slot.RecordState = MicVoiceCheckState.Idle;
                slot.StatusMessage = "Failed to create microphone recording stream.";
                slot.StatusIsError = true;
                slot.LastStatusTime = EditorApplication.timeSinceStartup;
                return;
            }

            int sampleRate = slot.RecordSampleRate > 0 ? slot.RecordSampleRate : 48000;
            int maxSamples = (int) Math.Ceiling(duration * sampleRate);
            if (slot.RecordBuffer == null || slot.RecordBuffer.Length < maxSamples)
            {
                slot.RecordBuffer = new float[maxSamples];
            }
            slot.RecordSampleCount = 0;
            slot.RecordTargetDuration = duration;
            slot.RecordStartTime = EditorApplication.timeSinceStartup;
            slot.RecordElapsedSeconds = 0;
            slot.RecordJustFinished = false;
            slot.RecordState = MicVoiceCheckState.Recording;

            slot.ActiveDevice.IsRecordingOutput = true;
            slot.StartRecordReader();
            string fxLabel = slot.RecordFx ? "with FX" : "dry";
            slot.StatusMessage = $"Recording {slot.DisplayLabel} ({duration:F0}s, {fxLabel})... Sing or speak!";
            slot.StatusIsError = false;
            slot.LastStatusTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void StopMicRecording(MicSlot slot)
        {
            if (slot.RecordState != MicVoiceCheckState.Recording)
            {
                return;
            }

            slot.DetachRecordingChannel();
            slot.RecordState = MicVoiceCheckState.Ready;

            int count = slot.RecordSampleCount;
            if (count == 0 || slot.RecordBuffer == null)
            {
                slot.StatusMessage = "No audio recorded.";
                slot.StatusIsError = true;
                slot.LastStatusTime = EditorApplication.timeSinceStartup;
                return;
            }

            slot.RecordedSamples = new float[count];
            Array.Copy(slot.RecordBuffer, slot.RecordedSamples, count);

            int sampleRate = slot.RecordSampleRate > 0 ? slot.RecordSampleRate : 48000;
            slot.RecordedDuration = slot.RecordedSamples.Length / (double) sampleRate;

            float maxAbs = 0f;
            double sumSq = 0;
            for (int i = 0; i < slot.RecordedSamples.Length; i++)
            {
                float s = Math.Abs(slot.RecordedSamples[i]);
                if (s > maxAbs) maxAbs = s;
                sumSq += s * s;
            }
            slot.RecordedPeakDb = maxAbs > 1e-6f ? 20f * MathF.Log10(maxAbs) : -96f;
            float rms = (float) Math.Sqrt(sumSq / slot.RecordedSamples.Length);
            slot.RecordedRmsDb = rms > 1e-6f ? 20f * MathF.Log10(rms) : -96f;

            GenerateWaveformOverview(slot, slot.RecordedSamples, 250);

            byte[] wavBytes = BuildWavData(slot.RecordedSamples, sampleRate);
            slot.DisposePlayback();

            try
            {
                var ms = new MemoryStream(wavBytes);
                slot.PlaybackMixer = GlobalAudioHandler.LoadCustomFile($"VoiceCheck_{slot.Id}", ms, 1f, slot.PlaybackVolume, normalize: false);
                if (slot.PlaybackMixer != null)
                {
                    var capturedSlot = slot;
                    slot.PlaybackMixer.SongEnd += () => OnMicPlaybackEnded(capturedSlot);
                    string fxDesc = slot.RecordFx ? " (with FX)" : " (dry)";
                    slot.StatusMessage = $"Recorded {slot.RecordedDuration:F1}s clip{fxDesc} (Peak: {slot.RecordedPeakDb:F1} dB, RMS: {slot.RecordedRmsDb:F1} dB)";
                    slot.StatusIsError = false;
                    slot.LastStatusTime = EditorApplication.timeSinceStartup;
                }
                else
                {
                    slot.StatusMessage = "Failed to create playback stream.";
                    slot.StatusIsError = true;
                    slot.LastStatusTime = EditorApplication.timeSinceStartup;
                }
            }
            catch (Exception ex)
            {
                slot.StatusMessage = $"Playback init failed: {ex.Message}";
                slot.StatusIsError = true;
                slot.LastStatusTime = EditorApplication.timeSinceStartup;
            }

            Repaint();
        }

        private void StartMicPlayback(MicSlot slot)
        {
            if (slot.PlaybackMixer == null)
            {
                return;
            }

            slot.PlaybackMixer.SetVolume(slot.PlaybackVolume);
            slot.PlaybackMixer.Play();
            slot.RecordState = MicVoiceCheckState.Playing;
            Repaint();
        }

        private void PauseMicPlayback(MicSlot slot)
        {
            if (slot.PlaybackMixer == null)
            {
                return;
            }

            slot.PlaybackMixer.Pause();
            slot.RecordState = MicVoiceCheckState.Paused;
            Repaint();
        }

        private void StopMicPlayback(MicSlot slot)
        {
            if (slot.PlaybackMixer == null)
            {
                return;
            }

            slot.PlaybackMixer.Pause();
            slot.PlaybackMixer.SetPosition(0);
            slot.RecordState = MicVoiceCheckState.Ready;
            Repaint();
        }

        private void SeekMicPlayback(MicSlot slot, double position)
        {
            if (slot.PlaybackMixer == null)
            {
                return;
            }

            position = Math.Clamp(position, 0, slot.RecordedDuration);
            slot.PlaybackMixer.SetPosition(position);
            Repaint();
        }

        private void DiscardMicRecording(MicSlot slot)
        {
            slot.DetachRecordingChannel();
            StopMicPlayback(slot);
            slot.DisposePlayback();
            slot.RecordSampleCount = 0;
            slot.RecordedSamples = Array.Empty<float>();
            slot.WaveformOverview = null;
            slot.RecordedDuration = 0;
            slot.RecordedPeakDb = -96f;
            slot.RecordedRmsDb = -96f;
            slot.RecordState = MicVoiceCheckState.Idle;
            slot.StatusMessage = "Voice check recording discarded.";
            slot.StatusIsError = false;
            slot.LastStatusTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void OnMicPlaybackEnded(MicSlot slot)
        {
            if (slot.PlaybackLoop && slot.RecordState == MicVoiceCheckState.Playing && slot.PlaybackMixer != null)
            {
                slot.PlaybackMixer.SetPosition(0);
                slot.PlaybackMixer.Play();
            }
            else
            {
                slot.RecordState = MicVoiceCheckState.Ready;
            }
            Repaint();
        }

        private void UpdateMicRecordAndPlayback(double now, double dt)
        {
            for (int i = 0; i < _micSlots.Count; i++)
            {
                var slot = _micSlots[i];
                if (slot.RecordState == MicVoiceCheckState.Recording)
                {
                    slot.RecordElapsedSeconds = now - slot.RecordStartTime;
                    if (slot.RecordJustFinished || slot.RecordElapsedSeconds >= slot.RecordTargetDuration)
                    {
                        slot.RecordJustFinished = false;
                        StopMicRecording(slot);
                        if (slot.AutoPlay)
                        {
                            StartMicPlayback(slot);
                        }
                    }
                    Repaint();
                }
                else if (slot.RecordState == MicVoiceCheckState.Playing)
                {
                    if (slot.PlaybackMixer != null)
                    {
                        double pos = slot.PlaybackMixer.GetPosition();
                        double len = slot.PlaybackMixer.Length;
                        if (len > 0 && pos >= len - 0.03)
                        {
                            if (slot.PlaybackLoop)
                            {
                                slot.PlaybackMixer.SetPosition(0);
                                slot.PlaybackMixer.Play();
                            }
                            else
                            {
                                slot.PlaybackMixer.Pause();
                                slot.PlaybackMixer.SetPosition(0);
                                slot.RecordState = MicVoiceCheckState.Ready;
                            }
                        }
                    }
                    Repaint();
                }
            }
        }

        private static byte[] BuildWavData(float[] samples, int sampleRate)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            int channelCount = 1;
            int bitsPerSample = 16;
            int byteRate = sampleRate * channelCount * 2;
            int blockAlign = channelCount * 2;
            int dataSize = samples.Length * 2;

            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short) 1);
            writer.Write((short) channelCount);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short) blockAlign);
            writer.Write((short) bitsPerSample);

            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);
            for (int i = 0; i < samples.Length; i++)
            {
                short pcm = (short) Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                writer.Write(pcm);
            }

            return ms.ToArray();
        }

        private static void GenerateWaveformOverview(MicSlot slot, float[] samples, int bucketCount)
        {
            if (samples.Length == 0)
            {
                slot.WaveformOverview = null;
                return;
            }

            int count = Math.Min(bucketCount, samples.Length);
            slot.WaveformOverview = new WaveformBucket[count];
            double samplesPerBucket = samples.Length / (double) count;

            for (int i = 0; i < count; i++)
            {
                int start = (int) (i * samplesPerBucket);
                int end = Math.Min((int) ((i + 1) * samplesPerBucket), samples.Length);
                if (start >= end) start = Math.Max(0, end - 1);

                float min = 1f;
                float max = -1f;
                double sumSq = 0;
                int n = end - start;

                for (int j = start; j < end; j++)
                {
                    float s = samples[j];
                    if (s < min) min = s;
                    if (s > max) max = s;
                    sumSq += s * s;
                }

                slot.WaveformOverview[i] = new WaveformBucket
                {
                    Min = min,
                    Max = max,
                    Rms = n > 0 ? (float) Math.Sqrt(sumSq / n) : 0f
                };
            }
        }
        private void DrawMicRecordPlaybackSection(MicSlot slot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("🎙️ VOICE CHECK", EditorStyles.boldLabel, GUILayout.Width(110));

                    GUILayout.FlexibleSpace();

                    if (slot.RecordedDuration > 0)
                    {
                        string info = $"{slot.RecordedDuration:F1}s clip • Peak: {slot.RecordedPeakDb:F1} dB";
                        GUILayout.Label(info, EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.Space(3);

                Rect waveRect = GUILayoutUtility.GetRect(100, 10000, 48, 48);
                DrawMicWaveformAndTimeline(slot, waveRect);

                EditorGUILayout.Space(4);

                DrawVoiceCheckControls(slot);
            }
        }

        private void DrawMicWaveformAndTimeline(MicSlot slot, Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.07f, 0.08f, 0.10f, 1f));

            float centerY = rect.y + (rect.height * 0.5f);
            EditorGUI.DrawRect(new Rect(rect.x, centerY, rect.width, 1), new Color(1f, 1f, 1f, 0.08f));

            if (slot.RecordState == MicVoiceCheckState.Recording)
            {
                float recordProgress = Mathf.Clamp01((float) (slot.RecordElapsedSeconds / Math.Max(0.1, slot.RecordTargetDuration)));
                float fillW = recordProgress * rect.width;
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, fillW, rect.height), new Color(0.92f, 0.25f, 0.25f, 0.18f));
                EditorGUI.DrawRect(new Rect(rect.x + fillW - 2, rect.y, 2, rect.height), new Color(1f, 0.3f, 0.3f, 0.9f));

                var recStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.4f, 0.4f, 0.95f) },
                    fontSize = 11
                };
                string recText = $"🔴 Recording... [{slot.RecordElapsedSeconds:F1}s / {slot.RecordTargetDuration:F0}s]";
                GUI.Label(rect, recText, recStyle);
                return;
            }

            if (slot.WaveformOverview != null && slot.WaveformOverview.Length > 0 && slot.RecordedDuration > 0)
            {
                int count = slot.WaveformOverview.Length;
                float barWidth = rect.width / count;
                float halfH = rect.height * 0.46f;

                for (int i = 0; i < count; i++)
                {
                    var b = slot.WaveformOverview[i];
                    float x = rect.x + (i * barWidth);
                    float w = Math.Max(1f, barWidth - 0.5f);

                    float minNorm = Mathf.Clamp(b.Min, -1f, 1f);
                    float maxNorm = Mathf.Clamp(b.Max, -1f, 1f);
                    float topY = centerY - (maxNorm * halfH);
                    float botY = centerY - (minNorm * halfH);
                    float h = Math.Max(1f, botY - topY);

                    EditorGUI.DrawRect(new Rect(x, topY, w, h), new Color(slot.ThemeColor.r * 0.7f, slot.ThemeColor.g * 0.7f, slot.ThemeColor.b * 0.7f, 0.55f));

                    float rmsNorm = Mathf.Clamp(b.Rms, 0f, 1f);
                    if (rmsNorm > 0.01f)
                    {
                        float rmsH = Math.Max(1f, rmsNorm * halfH * 2f);
                        float rmsY = centerY - (rmsH * 0.5f);
                        EditorGUI.DrawRect(new Rect(x, rmsY, w, rmsH), slot.ThemeColor);
                    }
                }

                double currentPos = slot.PlaybackMixer?.GetPosition() ?? 0;
                float playProgress = Mathf.Clamp01((float) (currentPos / slot.RecordedDuration));
                float playheadX = rect.x + (playProgress * rect.width);

                if (playheadX > rect.x)
                {
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, playheadX - rect.x, rect.height), new Color(slot.ThemeColor.r, slot.ThemeColor.g, slot.ThemeColor.b, 0.12f));
                }

                EditorGUI.DrawRect(new Rect(playheadX - 1, rect.y, 2, rect.height), new Color(1f, 0.88f, 0.30f, 0.95f));

                var timeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = new Color(0.95f, 0.95f, 0.95f, 0.85f) },
                    fontSize = 9
                };
                GUI.Label(new Rect(rect.x + 6, rect.y + 3, 120, 14), $"{currentPos:F1}s / {slot.RecordedDuration:F1}s", timeStyle);

                var evt = Event.current;
                if (rect.Contains(evt.mousePosition))
                {
                    EditorGUI.DrawRect(new Rect(evt.mousePosition.x - 0.5f, rect.y, 1, rect.height), new Color(1f, 1f, 1f, 0.35f));

                    if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && evt.button == 0)
                    {
                        float hoverProgress = (evt.mousePosition.x - rect.x) / rect.width;
                        double hoverTime = hoverProgress * slot.RecordedDuration;
                        SeekMicPlayback(slot, hoverTime);
                        evt.Use();
                    }
                }
            }
            else
            {
                var hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.6f, 0.65f, 0.72f, 0.6f) },
                    fontSize = 11
                };
                GUI.Label(rect, "Click 'Record' to test this microphone", hintStyle);
            }
        }

        private void DrawVoiceCheckControls(MicSlot slot)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool isRecording = slot.RecordState == MicVoiceCheckState.Recording;
                var recBg = GUI.backgroundColor;
                if (isRecording) GUI.backgroundColor = new Color(0.95f, 0.25f, 0.25f, 1f);
                var recBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                string recLabel = isRecording
                    ? $"⏹ Stop ({slot.RecordElapsedSeconds:F1}s)"
                    : "● Record";

                if (GUILayout.Button(recLabel, recBtnStyle, GUILayout.Width(110), GUILayout.Height(24)))
                {
                    if (isRecording)
                    {
                        StopMicRecording(slot);
                        if (slot.AutoPlay)
                        {
                            StartMicPlayback(slot);
                        }
                    }
                    else
                    {
                        StartMicRecording(slot, 5f);
                    }
                }
                GUI.backgroundColor = recBg;

                GUILayout.Space(6);

                bool hasRecording = slot.PlaybackMixer != null && slot.RecordedDuration > 0;
                bool isPlaying = slot.RecordState == MicVoiceCheckState.Playing;

                EditorGUI.BeginDisabledGroup(!hasRecording);
                var playBg = GUI.backgroundColor;
                if (isPlaying) GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f, 1f);
                string playLabel = isPlaying ? "⏸ Pause" : "▶ Play";
                if (GUILayout.Button(playLabel, EditorStyles.miniButton, GUILayout.Width(65), GUILayout.Height(24)))
                {
                    if (isPlaying)
                    {
                        PauseMicPlayback(slot);
                    }
                    else
                    {
                        StartMicPlayback(slot);
                    }
                }
                GUI.backgroundColor = playBg;
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(8);

                var fxBg = GUI.backgroundColor;
                if (slot.RecordFx) GUI.backgroundColor = slot.ThemeColor;
                EditorGUI.BeginChangeCheck();
                slot.RecordFx = GUILayout.Toggle(slot.RecordFx, new GUIContent("✨ Vocal FX", "Record with vocal FX chain (High-Pass, Noise Gate, Auto-Leveler, Compressor, Echo, Reverb, Limiter) vs raw dry mic"), EditorStyles.miniButton, GUILayout.Width(82), GUILayout.Height(24));
                if (EditorGUI.EndChangeCheck())
                {
                    if (slot.RecordState == MicVoiceCheckState.Recording)
                    {
                        AttachMicRecordingChannel(slot);
                    }
                }
                GUI.backgroundColor = fxBg;

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField("Vol:", EditorStyles.miniLabel, GUILayout.Width(26));
                EditorGUI.BeginChangeCheck();
                slot.PlaybackVolume = EditorGUILayout.Slider(slot.PlaybackVolume, 0f, 2f, GUILayout.Width(110));
                if (EditorGUI.EndChangeCheck() && slot.PlaybackMixer != null)
                {
                    slot.PlaybackMixer.SetVolume(slot.PlaybackVolume);
                }
            }
        }

        #endregion
    }
}
