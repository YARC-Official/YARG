using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;
using UnityEditor;
using UnityEngine;
using YARG.Audio.BASS;
using YARG.Core.Audio;
using YARG.Helpers;
using YARG.Settings;
using YARG.Settings.Types;

namespace Editor
{
    public static class VerifyAudioDelays
    {
        private const int TEST_BUFFER_MS = 150;
        private const double EPSILON = 0.0001;
        private const int TEMPO_TEST_SAMPLE_RATE = 48000;
        private const int TEMPO_MATRIX_SAMPLE_COUNT = 20;
        private const int TEMPO_MATRIX_UPDATE_PERIOD_MS = 10;
        private const int TEMPO_MATRIX_BASELINE_BUFFER_MS = 150;
        private const int TEMPO_COMMAND_JITTER_MAX_MS = 100;
        private const double TEMPO_TRACE_VALUE_EPSILON_MS = 0.01;
        private static readonly float[] TEMPO_TEST_SPEEDS = { 0.5f, 0.75f, 1.5f, 2.0f };
        private static readonly int[] TEMPO_PIPELINE_UPDATE_PERIODS_MS = { 5, 10, 20 };
        private static readonly int[] TEMPO_PIPELINE_SEEK_WINDOWS_MS = { 10, 20, 28, 40 };
        // 150ms baseline is already covered by the speed sweep.
        private static readonly int[] TEMPO_TEST_BUFFERS_MS = { 50, 100, 250 };
        private static readonly System.Random TempoTestRandom = new();

        private readonly struct TempoLatencyMeasurement
        {
            public readonly double EarliestLatencyMs;
            public readonly double LatestLatencyMs;
            public readonly double AvailableBeforeMs;
            public readonly double AvailableAfterMs;
            public readonly double PositionChangeMedianMs;
            public readonly double AvailableChangeMedianMs;

            public TempoLatencyMeasurement(double earliestLatencyMs, double latestLatencyMs,
                double availableBeforeMs, double availableAfterMs, double positionChangeMedianMs,
                double availableChangeMedianMs)
            {
                EarliestLatencyMs = earliestLatencyMs;
                LatestLatencyMs = latestLatencyMs;
                AvailableBeforeMs = availableBeforeMs;
                AvailableAfterMs = availableAfterMs;
                PositionChangeMedianMs = positionChangeMedianMs;
                AvailableChangeMedianMs = availableChangeMedianMs;
            }
        }

        private readonly struct TempoTraceSample
        {
            public readonly double Time;
            public readonly double Position;
            public readonly double AvailableMs;

            public TempoTraceSample(double time, double position, double availableMs)
            {
                Time = time;
                Position = position;
                AvailableMs = availableMs;
            }
        }

        private readonly struct TempoPipelineSample
        {
            public readonly double Time;
            public readonly double PlayedPosition;
            public readonly double DecodePosition;
            public readonly double AvailableMs;
            public readonly double BlockDurationMs;

            public TempoPipelineSample(double time, double playedPosition, double decodePosition,
                double availableMs, double blockDurationMs)
            {
                Time = time;
                PlayedPosition = playedPosition;
                DecodePosition = decodePosition;
                AvailableMs = availableMs;
                BlockDurationMs = blockDurationMs;
            }
        }

        private readonly struct TempoPipelineMeasurement
        {
            public readonly double GeneratedLatencyMs;
            public readonly double PlayedLatencyMs;
            public readonly double DownstreamLatencyMs;
            public readonly double AvailableBeforeMs;
            public readonly double QueueErrorMs;
            public readonly double BlockDurationMs;

            public TempoPipelineMeasurement(double generatedLatencyMs, double playedLatencyMs,
                double downstreamLatencyMs, double availableBeforeMs, double queueErrorMs,
                double blockDurationMs)
            {
                GeneratedLatencyMs = generatedLatencyMs;
                PlayedLatencyMs = playedLatencyMs;
                DownstreamLatencyMs = downstreamLatencyMs;
                AvailableBeforeMs = availableBeforeMs;
                QueueErrorMs = queueErrorMs;
                BlockDurationMs = blockDurationMs;
            }
        }

        [MenuItem("Tests/Verify Audio Delays")]
        public static void RunVerification()
        {
            Debug.Log("Starting Audio Delays Verification Test...");

            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            var originalSettings = SettingsManager.Settings;
            var settingsSetter = GetSettingsSetter();
            if (settingsSetter == null)
            {
                Debug.LogError("Could not find setter for SettingsManager.Settings");
                return;
            }

            settingsSetter.Invoke(null, new object[] { new SettingsManager.SettingContainer() });

            var audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("Failed to get active BassAudioManager instance!");
                settingsSetter.Invoke(null, new object[] { originalSettings });
                return;
            }

            try
            {
                SetSettingValue(SettingsManager.Settings.PlaybackBufferLength, TEST_BUFFER_MS);
                GlobalAudioHandler.SetBufferLength(TEST_BUFFER_MS);

                VerifyMixerDelays(audioManager, TEST_BUFFER_MS);

                Debug.Log("SUCCESS: All audio delay verification checks passed!");
                EditorUtility.DisplayDialog("Test Passed", "All audio delay verification checks passed successfully!", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Test Failed: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Test Failed", $"Audio delay verification failed:\n{ex.Message}", "OK");
            }
            finally
            {
                Debug.Log("Restoring original settings...");
                settingsSetter.Invoke(null, new object[] { originalSettings });

                if (originalSettings != null)
                {
                    GlobalAudioHandler.SetBufferLength(originalSettings.PlaybackBufferLength.Value);
                }
                Debug.Log("Restore complete.");
            }
        }

        [MenuItem("Tests/Measure Real Seek Latency")]
        public static async void RunRealSeekLatencyMeasurement()
        {
            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            var originalSettings = SettingsManager.Settings;
            var settingsSetter = GetSettingsSetter();
            if (settingsSetter == null)
            {
                Debug.LogError("Could not find setter for SettingsManager.Settings");
                return;
            }

            settingsSetter.Invoke(null, new object[] { new SettingsManager.SettingContainer() });

            var audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("Failed to get active BassAudioManager instance!");
                settingsSetter.Invoke(null, new object[] { originalSettings });
                return;
            }

            try
            {
                SetSettingValue(SettingsManager.Settings.PlaybackBufferLength, TEST_BUFFER_MS);
                GlobalAudioHandler.SetBufferLength(TEST_BUFFER_MS);

                await MeasureMixerSeekLatency(audioManager);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Measurement Failed: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Measurement Failed", $"Error:\n{ex.Message}", "OK");
            }
            finally
            {
                settingsSetter.Invoke(null, new object[] { originalSettings });

                if (originalSettings != null)
                {
                    GlobalAudioHandler.SetBufferLength(originalSettings.PlaybackBufferLength.Value);
                }
            }
        }

        private static void VerifyMixerDelays(BassAudioManager audioManager, int testBufferMs)
        {
            var mixer = CreateTestMixer(audioManager);
            try
            {
                double deviceLatency = GlobalAudioHandler.PlaybackLatency / 1000.0;
                int minBufferLength = GlobalAudioHandler.MinimumBufferLength;
                int effectiveBufferLength = testBufferMs > 0 && minBufferLength > 0 && testBufferMs < minBufferLength
                    ? minBufferLength
                    : testBufferMs;
                double configuredLatency = Math.Max(0, effectiveBufferLength) / 1000.0;

                double playbackLatency = mixer.GetPlaybackStartOffset();
                double tempoLatency = mixer.GetTempoStreamLatency();

                Debug.Log($"Calculated parameters: Device Latency: {deviceLatency * 1000:0.0}ms, " +
                          $"Configured Buffer Latency: {configuredLatency * 1000:0.0}ms, " +
                          $"Command Update Midpoint: {GetExpectedCommandUpdateLatency() * 1000:0.0}ms");
                Debug.Log($"Mixer reported latencies: Playback: {playbackLatency * 1000:0.0}ms, " +
                          $"Tempo: {tempoLatency * 1000:0.0}ms");

                double expectedPlayback = GetExpectedPlaybackLatency(deviceLatency);
                if (Math.Abs(playbackLatency - expectedPlayback) > EPSILON)
                {
                    throw new Exception($"PlaybackLatency did not match! Actual: {playbackLatency * 1000:0.0}ms ({playbackLatency}s), Expected: {expectedPlayback * 1000:0.0}ms ({expectedPlayback}s)");
                }

                // Empty test mixer has no queued audio, so dynamic buffer latency is zero.
                // Only BASS command-update latency should remain.
                double expectedTempo = GetExpectedCommandUpdateLatency();
                if (Math.Abs(tempoLatency - expectedTempo) > EPSILON)
                {
                    throw new Exception($"TempoLatency did not match! Actual: {tempoLatency * 1000:0.0}ms ({tempoLatency}s), Expected: {expectedTempo * 1000:0.0}ms ({expectedTempo}s)");
                }
            }
            finally
            {
                mixer.Dispose();
            }
        }

        [MenuItem("Tests/Measure BASS Output Buffer Position")]
        public static async void RunOutputBufferPositionMeasurement()
        {
            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            var originalSettings = SettingsManager.Settings;
            var settingsSetter = GetSettingsSetter();
            if (settingsSetter == null)
            {
                Debug.LogError("Could not find setter for SettingsManager.Settings");
                return;
            }

            settingsSetter.Invoke(null, new object[] { new SettingsManager.SettingContainer() });

            var audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("Failed to get active BassAudioManager instance!");
                settingsSetter.Invoke(null, new object[] { originalSettings });
                return;
            }

            try
            {
                SetSettingValue(SettingsManager.Settings.PlaybackBufferLength, TEST_BUFFER_MS);
                GlobalAudioHandler.SetBufferLength(TEST_BUFFER_MS);

                await MeasureOutputBufferPosition(audioManager);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Measurement Failed: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Measurement Failed", $"Error:\n{ex.Message}", "OK");
            }
            finally
            {
                settingsSetter.Invoke(null, new object[] { originalSettings });

                if (originalSettings != null)
                {
                    GlobalAudioHandler.SetBufferLength(originalSettings.PlaybackBufferLength.Value);
                }
            }
        }

        [MenuItem("Tests/Measure Tempo Change Latency")]
        public static async void RunTempoChangeLatencyMeasurement()
        {
            Debug.Log("<b>[Tempo Change Latency]</b> Starting measurement...");

            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            var originalSettings = SettingsManager.Settings;
            var settingsSetter = GetSettingsSetter();
            bool createdSettings = originalSettings == null;
            if (createdSettings)
            {
                if (settingsSetter == null)
                {
                    Debug.LogError("Could not initialize SettingsManager.Settings");
                    return;
                }

                settingsSetter.Invoke(null, new object[] { new SettingsManager.SettingContainer() });
            }

            var audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("Failed to get active BassAudioManager instance!");
                if (createdSettings)
                {
                    settingsSetter.Invoke(null, new object[] { originalSettings });
                }
                return;
            }

            int originalUpdatePeriod = Bass.UpdatePeriod;
            int originalBufferLength = SettingsManager.Settings.PlaybackBufferLength.Value;
            try
            {
                Bass.UpdatePeriod = TEMPO_MATRIX_UPDATE_PERIOD_MS;

                SetTempoTestBuffer(TEMPO_MATRIX_BASELINE_BUFFER_MS);
                foreach (float speed in TEMPO_TEST_SPEEDS)
                {
                    string label = $"Speed {speed:0.##}x; buffer {TEMPO_MATRIX_BASELINE_BUFFER_MS}ms";
                    await RunTempoLatencyBatch(audioManager, label, speed);
                }

                foreach (int bufferMs in TEMPO_TEST_BUFFERS_MS)
                {
                    SetTempoTestBuffer(bufferMs);
                    string label = $"Speed 0.5x; buffer {bufferMs}ms";
                    await RunTempoLatencyBatch(audioManager, label, 0.5f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Tempo measurement failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                Bass.UpdatePeriod = originalUpdatePeriod;
                SetTempoTestBuffer(originalBufferLength);
                if (createdSettings)
                {
                    settingsSetter.Invoke(null, new object[] { originalSettings });
                }
            }
        }

        [MenuItem("Tests/Diagnose Tempo Pipeline Latency")]
        public static async void RunTempoPipelineLatencyDiagnostic()
        {
            Debug.Log("<b>[Tempo Pipeline Latency]</b> Starting DSP-boundary diagnostic...");

            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            var originalSettings = SettingsManager.Settings;
            var settingsSetter = GetSettingsSetter();
            bool createdSettings = originalSettings == null;
            if (createdSettings)
            {
                if (settingsSetter == null)
                {
                    Debug.LogError("Could not initialize SettingsManager.Settings");
                    return;
                }

                settingsSetter.Invoke(null, new object[] { new SettingsManager.SettingContainer() });
            }

            var audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("Failed to get active BassAudioManager instance!");
                return;
            }

            int originalBufferLength = SettingsManager.Settings.PlaybackBufferLength.Value;
            try
            {
                SetTempoTestBuffer(TEMPO_MATRIX_BASELINE_BUFFER_MS);
                foreach (float speed in TEMPO_TEST_SPEEDS)
                {
                    await RunTempoPipelineLatencyBatch(audioManager, speed);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Tempo pipeline diagnostic failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                SetTempoTestBuffer(originalBufferLength);
                if (createdSettings)
                {
                    settingsSetter.Invoke(null, new object[] { originalSettings });
                }
            }
        }

        [MenuItem("Tests/Diagnose Tempo Update Period")]
        public static async void RunTempoUpdatePeriodDiagnostic()
        {
            Debug.Log("<b>[Tempo Update Period]</b> Starting update-period sweep...");

            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            var originalSettings = SettingsManager.Settings;
            var settingsSetter = GetSettingsSetter();
            bool createdSettings = originalSettings == null;
            if (createdSettings)
            {
                if (settingsSetter == null)
                {
                    Debug.LogError("Could not initialize SettingsManager.Settings");
                    return;
                }

                settingsSetter.Invoke(null, new object[] { new SettingsManager.SettingContainer() });
            }

            var audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("Failed to get active BassAudioManager instance!");
                if (createdSettings)
                {
                    settingsSetter.Invoke(null, new object[] { originalSettings });
                }
                return;
            }

            int originalUpdatePeriod = Bass.UpdatePeriod;
            int originalBufferLength = SettingsManager.Settings.PlaybackBufferLength.Value;
            try
            {
                SetTempoTestBuffer(TEMPO_MATRIX_BASELINE_BUFFER_MS);
                foreach (int updatePeriodMs in TEMPO_PIPELINE_UPDATE_PERIODS_MS)
                {
                    Bass.UpdatePeriod = updatePeriodMs;
                    await RunTempoPipelineLatencyBatch(audioManager, 0.5f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Tempo update-period diagnostic failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                Bass.UpdatePeriod = originalUpdatePeriod;
                SetTempoTestBuffer(originalBufferLength);
                if (createdSettings)
                {
                    settingsSetter.Invoke(null, new object[] { originalSettings });
                }
            }
        }

        [MenuItem("Tests/Diagnose Tempo Seek Window")]
        public static async void RunTempoSeekWindowDiagnostic()
        {
            Debug.Log("<b>[Tempo Seek Window]</b> Starting seek-window and quick-mode sweep...");

            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            var originalSettings = SettingsManager.Settings;
            var settingsSetter = GetSettingsSetter();
            bool createdSettings = originalSettings == null;
            if (createdSettings)
            {
                if (settingsSetter == null)
                {
                    Debug.LogError("Could not initialize SettingsManager.Settings");
                    return;
                }

                settingsSetter.Invoke(null, new object[] { new SettingsManager.SettingContainer() });
            }

            var audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("Failed to get active BassAudioManager instance!");
                if (createdSettings)
                {
                    settingsSetter.Invoke(null, new object[] { originalSettings });
                }
                return;
            }

            int originalUpdatePeriod = Bass.UpdatePeriod;
            int originalBufferLength = SettingsManager.Settings.PlaybackBufferLength.Value;
            try
            {
                Bass.UpdatePeriod = 5;
                SetTempoTestBuffer(TEMPO_MATRIX_BASELINE_BUFFER_MS);

                await RunTempoPipelineLatencyBatch(audioManager, 0.5f, "automatic defaults");
                foreach (int seekWindowMs in TEMPO_PIPELINE_SEEK_WINDOWS_MS)
                {
                    string label = $"sequence 82ms; seek {seekWindowMs}ms; overlap 8ms; quick false";
                    await RunTempoPipelineLatencyBatch(audioManager, 0.5f, label, 82, seekWindowMs, 8, false);
                }

                await RunTempoPipelineLatencyBatch(audioManager, 0.5f,
                    "sequence 82ms; seek 28ms; overlap 8ms; quick true", 82, 28, 8, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Tempo seek-window diagnostic failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                Bass.UpdatePeriod = originalUpdatePeriod;
                SetTempoTestBuffer(originalBufferLength);
                if (createdSettings)
                {
                    settingsSetter.Invoke(null, new object[] { originalSettings });
                }
            }
        }

        private static async Task RunTempoPipelineLatencyBatch(BassAudioManager audioManager, float speed,
            string configurationLabel = null, float? sequenceMs = null, float? seekWindowMs = null,
            float? overlapMs = null, bool? useQuickAlgorithm = null)
        {
            var generatedLatencies = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            var playedLatencies = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            var downstreamLatencies = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            var availableValues = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            var queueErrors = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            var blockDurations = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);

            Debug.Log($"<b>[Tempo Pipeline Latency]</b> Speed {speed:0.##}x; " +
                      $"samples: {TEMPO_MATRIX_SAMPLE_COUNT}; jitter: 0–{TEMPO_COMMAND_JITTER_MAX_MS}ms");

            for (int sample = 0; sample < TEMPO_MATRIX_SAMPLE_COUNT; sample++)
            {
                int commandJitterMs = TempoTestRandom.Next(TEMPO_COMMAND_JITTER_MAX_MS + 1);
                var measurement = await MeasureTempoPipelineLatency(audioManager, speed, commandJitterMs,
                    sequenceMs, seekWindowMs, overlapMs, useQuickAlgorithm);
                AddFiniteValue(generatedLatencies, measurement.GeneratedLatencyMs);
                AddFiniteValue(playedLatencies, measurement.PlayedLatencyMs);
                AddFiniteValue(downstreamLatencies, measurement.DownstreamLatencyMs);
                AddFiniteValue(availableValues, measurement.AvailableBeforeMs);
                AddFiniteValue(queueErrors, measurement.QueueErrorMs);
                AddFiniteValue(blockDurations, measurement.BlockDurationMs);
            }

            generatedLatencies.Sort();
            playedLatencies.Sort();
            downstreamLatencies.Sort();
            availableValues.Sort();
            queueErrors.Sort();
            blockDurations.Sort();

            string configuration = configurationLabel == null ? string.Empty : $"\nConfig: {configurationLabel}";
            Debug.Log($"<b>[Tempo Pipeline Latency — Speed {speed:0.##}x Summary]</b>{configuration}\n" +
                      $"Command → generated median/p95: {GetMedianOrNaN(generatedLatencies):0.00}/" +
                      $"{GetPercentileOrNaN(generatedLatencies, 0.95):0.00}ms\n" +
                      $"Command → played median/p95: {GetMedianOrNaN(playedLatencies):0.00}/" +
                      $"{GetPercentileOrNaN(playedLatencies, 0.95):0.00}ms\n" +
                      $"Generated → played median/p95: {GetMedianOrNaN(downstreamLatencies):0.00}/" +
                      $"{GetPercentileOrNaN(downstreamLatencies, 0.95):0.00}ms\n" +
                      $"Available median/p95: {GetMedianOrNaN(availableValues):0.00}/" +
                      $"{GetPercentileOrNaN(availableValues, 0.95):0.00}ms\n" +
                      $"Queue error (generated → played - available) median/p95: " +
                      $"{GetMedianOrNaN(queueErrors):+0.00;-0.00;0.00}/" +
                      $"{GetPercentileOrNaN(queueErrors, 0.95):+0.00;-0.00;0.00}ms\n" +
                      $"DSP block duration median: {GetMedianOrNaN(blockDurations):0.00}ms; " +
                      $"BASS update period: {Bass.UpdatePeriod}ms; buffer: " +
                      $"{SettingsManager.Settings.PlaybackBufferLength.Value}ms");
        }

        private static async Task<TempoPipelineMeasurement> MeasureTempoPipelineLatency(
            BassAudioManager audioManager, float speed, int commandJitterMs, float? sequenceMs = null,
            float? seekWindowMs = null, float? overlapMs = null, bool? useQuickAlgorithm = null)
        {
            var mixer = CreateTestMixer(audioManager);
            var testStream = CreateTempoTestTrack();
            var samples = new List<TempoPipelineSample>();
            var sampleLock = new object();
            var stopwatch = new System.Diagnostics.Stopwatch();
            int dspHandle = 0;

            try
            {
                if (!mixer.AddChannel(testStream, SongStem.Song))
                {
                    throw new Exception("Failed to add generated test track to mixer");
                }

                int tempoHandle = GetTempoStreamHandle(mixer);
                SetTempoFxAttribute(tempoHandle, ChannelAttribute.TempoSequenceMilliseconds, sequenceMs);
                SetTempoFxAttribute(tempoHandle, ChannelAttribute.TempoSeekWindowMilliseconds, seekWindowMs);
                SetTempoFxAttribute(tempoHandle, ChannelAttribute.TempoOverlapMilliseconds, overlapMs);
                float? quickAlgorithm = useQuickAlgorithm.HasValue
                    ? useQuickAlgorithm.Value ? 1 : 0
                    : null;
                SetTempoFxAttribute(tempoHandle, ChannelAttribute.TempoUseQuickAlgorithm, quickAlgorithm);

                DSPProcedure callback = (_, _, _, length, _) =>
                {
                    double time = stopwatch.Elapsed.TotalSeconds;
                    double playedPosition = GetPositionMs(tempoHandle, PositionFlags.Bytes) / 1000.0;
                    double decodePosition = GetPositionMs(tempoHandle,
                        PositionFlags.Bytes | PositionFlags.Decode) / 1000.0;
                    double availableMs = GetAvailableBufferMs(tempoHandle);
                    double blockDurationMs = Bass.ChannelBytes2Seconds(tempoHandle, length) * 1000.0;
                    var sample = new TempoPipelineSample(time, playedPosition, decodePosition, availableMs,
                        blockDurationMs);
                    lock (sampleLock)
                    {
                        samples.Add(sample);
                    }
                };

                dspHandle = Bass.ChannelSetDSP(tempoHandle, callback, IntPtr.Zero, int.MaxValue);
                if (dspHandle == 0)
                {
                    throw new Exception($"Failed to attach tempo DSP diagnostic: {Bass.LastError}");
                }

                stopwatch.Start();
                mixer.Play();
                await Task.Delay(500 + commandJitterMs);

                double availableBeforeMs = GetAvailableBufferMs(tempoHandle);
                double commandStart = stopwatch.Elapsed.TotalSeconds;
                mixer.SetPlaybackSpeed(speed);
                double commandEnd = stopwatch.Elapsed.TotalSeconds;
                double commandTime = (commandStart + commandEnd) / 2.0;
                await Task.Delay(750);

                List<TempoPipelineSample> snapshot;
                lock (sampleLock)
                {
                    snapshot = new List<TempoPipelineSample>(samples);
                }

                double decodeTransitionMs = FindPipelineRateTransition(snapshot, commandTime, speed, true);
                double playedTransitionMs = FindPipelineRateTransition(snapshot, commandTime, speed, false);
                double downstreamMs = playedTransitionMs - decodeTransitionMs;
                var attributes = GetTempoFxAttributes(tempoHandle);
                double medianBlockMs = GetPipelineBlockMedian(snapshot);

                Debug.Log($"<b>[Tempo Pipeline Latency — Speed {speed:0.##}x]</b> " +
                          $"jitter {commandJitterMs}ms; generated {decodeTransitionMs:0.00}ms; " +
                          $"played {playedTransitionMs:0.00}ms; downstream {downstreamMs:0.00}ms; " +
                          $"available {availableBeforeMs:0.00}ms; " +
                          $"queue error {downstreamMs - availableBeforeMs:+0.00;-0.00;0.00}ms; " +
                          $"block {medianBlockMs:0.00}ms; callbacks {snapshot.Count}\n" +
                          $"Tempo FX: sequence {attributes.sequenceMs:0.00}ms; seek {attributes.seekWindowMs:0.00}ms; " +
                          $"overlap {attributes.overlapMs:0.00}ms; quick {attributes.useQuickAlgorithm}; " +
                          $"AA {attributes.useAAFilter} ({attributes.aaFilterLength:0.00})\n" +
                          $"BASS update period: {Bass.UpdatePeriod}ms; buffer: " +
                          $"{SettingsManager.Settings.PlaybackBufferLength.Value}ms");

                GC.KeepAlive(callback);
                return new TempoPipelineMeasurement(decodeTransitionMs, playedTransitionMs, downstreamMs,
                    availableBeforeMs, downstreamMs - availableBeforeMs, medianBlockMs);
            }
            finally
            {
                if (dspHandle != 0)
                {
                    Bass.ChannelRemoveDSP(GetTempoStreamHandle(mixer), dspHandle);
                }

                mixer.Dispose();
                testStream.Dispose();
            }
        }

        private static double FindPipelineRateTransition(List<TempoPipelineSample> samples, double commandTime,
            float speed, bool useDecodePosition)
        {
            int sustained = 0;
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].Time < commandTime)
                {
                    continue;
                }

                double elapsed = samples[i].Time - samples[i - 1].Time;
                if (elapsed <= 0)
                {
                    continue;
                }

                double current = useDecodePosition ? samples[i].DecodePosition : samples[i].PlayedPosition;
                double previous = useDecodePosition ? samples[i - 1].DecodePosition : samples[i - 1].PlayedPosition;
                double slope = (current - previous) / elapsed;
                bool changedRate = Math.Abs(slope - speed) < Math.Abs(slope - 1.0);
                sustained = changedRate ? sustained + 1 : 0;
                if (sustained >= 2)
                {
                    return (samples[i - 1].Time - commandTime) * 1000.0;
                }
            }

            return double.NaN;
        }

        private static double GetPipelineBlockMedian(List<TempoPipelineSample> samples)
        {
            var durations = new List<double>(samples.Count);
            foreach (var sample in samples)
            {
                AddFiniteValue(durations, sample.BlockDurationMs);
            }

            durations.Sort();
            return GetMedianOrNaN(durations);
        }

        private static void SetTempoTestBuffer(int bufferMs)
        {
            SetSettingValue(SettingsManager.Settings.PlaybackBufferLength, bufferMs);
            GlobalAudioHandler.SetBufferLength(bufferMs);
        }

        private static async Task RunTempoLatencyBatch(BassAudioManager audioManager, string label, float speed)
        {
            var lowerErrors = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            var upperErrors = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            var boundWidths = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            var positionChangeIntervals = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            var availableChangeIntervals = new List<double>(TEMPO_MATRIX_SAMPLE_COUNT);
            int estimateInsideBoundsCount = 0;
            Debug.Log($"<b>[Tempo Change Latency]</b> {label}; update period: {Bass.UpdatePeriod}ms; " +
                      $"samples: {TEMPO_MATRIX_SAMPLE_COUNT}; jitter: 0–{TEMPO_COMMAND_JITTER_MAX_MS}ms");

            for (int sample = 1; sample <= TEMPO_MATRIX_SAMPLE_COUNT; sample++)
            {
                int commandJitterMs = TempoTestRandom.Next(TEMPO_COMMAND_JITTER_MAX_MS + 1);
                var measurement = await MeasureTempoChangeLatency(audioManager, commandJitterMs, speed);
                double lowerErrorMs = measurement.EarliestLatencyMs - measurement.AvailableBeforeMs;
                double upperErrorMs = measurement.LatestLatencyMs - measurement.AvailableBeforeMs;
                double boundWidthMs = measurement.LatestLatencyMs - measurement.EarliestLatencyMs;
                bool estimateInsideBounds = lowerErrorMs <= 0 && upperErrorMs >= 0;
                lowerErrors.Add(lowerErrorMs);
                upperErrors.Add(upperErrorMs);
                boundWidths.Add(boundWidthMs);
                AddFiniteValue(positionChangeIntervals, measurement.PositionChangeMedianMs);
                AddFiniteValue(availableChangeIntervals, measurement.AvailableChangeMedianMs);
                estimateInsideBoundsCount += estimateInsideBounds ? 1 : 0;

                Debug.Log($"<b>[Tempo Change Latency]</b> {label}; " +
                          $"sample {sample}/{TEMPO_MATRIX_SAMPLE_COUNT}: jitter {commandJitterMs}ms; " +
                          $"transition [{measurement.EarliestLatencyMs:0.00}, {measurement.LatestLatencyMs:0.00}]ms; " +
                          $"available {measurement.AvailableBeforeMs:0.00}ms " +
                          $"(after {measurement.AvailableAfterMs:0.00}ms); " +
                          $"error interval <b>[{lowerErrorMs:+0.00;-0.00;0.00}, " +
                          $"{upperErrorMs:+0.00;-0.00;0.00}]ms</b>; " +
                          $"inside: {estimateInsideBounds}; position cadence: " +
                          $"{measurement.PositionChangeMedianMs:0.00}ms; available cadence: " +
                          $"{measurement.AvailableChangeMedianMs:0.00}ms");
            }

            LogTempoLatencySummary(label, lowerErrors, upperErrors, boundWidths, positionChangeIntervals,
                availableChangeIntervals, estimateInsideBoundsCount);
        }

        private static async Task<TempoLatencyMeasurement>
            MeasureTempoChangeLatency(BassAudioManager audioManager, int commandJitterMs, float speed)
        {
            var mixer = CreateTestMixer(audioManager);
            var testStream = CreateTempoTestTrack();

            try
            {
                if (!mixer.AddChannel(testStream, SongStem.Song))
                {
                    throw new Exception("Failed to add generated test track to mixer");
                }

                mixer.Play();
                await Task.Delay(500);

                int tempoHandle = GetTempoStreamHandle(mixer);
                return await Task.Run(() => MeasurePositionRateChange(mixer, tempoHandle, commandJitterMs, speed));
            }
            finally
            {
                mixer.Dispose();
                testStream.Dispose();
            }
        }

        private static void LogTempoLatencySummary(string label, List<double> lowerErrors,
            List<double> upperErrors, List<double> boundWidths, List<double> positionChangeIntervals,
            List<double> availableChangeIntervals, int estimateInsideBoundsCount)
        {
            lowerErrors.Sort();
            upperErrors.Sort();
            boundWidths.Sort();
            positionChangeIntervals.Sort();
            availableChangeIntervals.Sort();

            Debug.Log($"<b>[Tempo Change Latency — {label} Summary]</b>\n" +
                      $"Error interval = transition bounds - available before command\n" +
                      $"Available inside bounds: {estimateInsideBoundsCount}/{lowerErrors.Count} " +
                      $"({estimateInsideBoundsCount * 100.0 / lowerErrors.Count:0.0}%)\n" +
                      $"Lower error median/p95: {GetPercentile(lowerErrors, 0.5):0.00}/" +
                      $"{GetPercentile(lowerErrors, 0.95):0.00}ms; upper error median/p95: " +
                      $"{GetPercentile(upperErrors, 0.5):0.00}/{GetPercentile(upperErrors, 0.95):0.00}ms\n" +
                      $"Bound width median/p95: {GetPercentile(boundWidths, 0.5):0.00}/" +
                      $"{GetPercentile(boundWidths, 0.95):0.00}ms\n" +
                      $"Position change cadence median: {GetMedianOrNaN(positionChangeIntervals):0.00}ms; " +
                      $"available change cadence median: {GetMedianOrNaN(availableChangeIntervals):0.00}ms");
        }

        private static void AddFiniteValue(List<double> values, double value)
        {
            if (!double.IsNaN(value) && !double.IsInfinity(value))
            {
                values.Add(value);
            }
        }

        private static double GetMedianOrNaN(List<double> sortedValues) =>
            sortedValues.Count > 0 ? GetPercentile(sortedValues, 0.5) : double.NaN;

        private static double GetPercentileOrNaN(List<double> sortedValues, double percentile) =>
            sortedValues.Count > 0 ? GetPercentile(sortedValues, percentile) : double.NaN;

        private static double GetPercentile(List<double> sortedValues, double percentile)
        {
            double index = (sortedValues.Count - 1) * percentile;
            int lowerIndex = (int) Math.Floor(index);
            int upperIndex = (int) Math.Ceiling(index);
            double interpolation = index - lowerIndex;
            return sortedValues[lowerIndex] +
                   (sortedValues[upperIndex] - sortedValues[lowerIndex]) * interpolation;
        }

        private static (float sequenceMs, float seekWindowMs, float overlapMs, bool useQuickAlgorithm,
            bool useAAFilter, float aaFilterLength) GetTempoFxAttributes(int tempoHandle)
        {
            float sequenceMs = GetChannelAttribute(tempoHandle, ChannelAttribute.TempoSequenceMilliseconds);
            float seekWindowMs = GetChannelAttribute(tempoHandle, ChannelAttribute.TempoSeekWindowMilliseconds);
            float overlapMs = GetChannelAttribute(tempoHandle, ChannelAttribute.TempoOverlapMilliseconds);
            float quickAlgorithm = GetChannelAttribute(tempoHandle, ChannelAttribute.TempoUseQuickAlgorithm);
            float aaFilter = GetChannelAttribute(tempoHandle, ChannelAttribute.TempoUseAAFilter);
            float aaFilterLength = GetChannelAttribute(tempoHandle, ChannelAttribute.TempoAAFilterLength);
            return (sequenceMs, seekWindowMs, overlapMs, quickAlgorithm != 0, aaFilter != 0, aaFilterLength);
        }

        private static float GetChannelAttribute(int channelHandle, ChannelAttribute attribute)
        {
            if (!Bass.ChannelGetAttribute(channelHandle, attribute, out float value))
            {
                throw new Exception($"Failed to read {attribute} from tempo stream: {Bass.LastError}");
            }

            return value;
        }

        private static void SetTempoFxAttribute(int tempoHandle, ChannelAttribute attribute, float? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            if (!Bass.ChannelSetAttribute(tempoHandle, attribute, value.Value))
            {
                throw new Exception($"Failed to set {attribute} on tempo stream: {Bass.LastError}");
            }
        }

        private static TempoLatencyMeasurement
            MeasurePositionRateChange(BassStemMixer mixer, int tempoHandle, int commandJitterMs, float speed)
        {
            const double warmupSeconds = 0.1;
            const double measurementSeconds = 0.5;
            const double slopeWindowSeconds = 0.005;
            var samples = new List<TempoTraceSample>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < warmupSeconds)
            {
                AddPositionSample(samples, stopwatch, tempoHandle);
                Thread.SpinWait(100);
            }

            double commandTimeTarget = warmupSeconds + commandJitterMs / 1000.0;
            while (stopwatch.Elapsed.TotalSeconds < commandTimeTarget)
            {
                AddPositionSample(samples, stopwatch, tempoHandle);
                Thread.SpinWait(100);
            }

            double availableBeforeMs = GetAvailableBufferMs(tempoHandle);
            double commandStartTime = stopwatch.Elapsed.TotalSeconds;
            mixer.SetPlaybackSpeed(speed);
            double commandEndTime = stopwatch.Elapsed.TotalSeconds;
            double commandTime = (commandStartTime + commandEndTime) / 2.0;
            double availableAfterMs = GetAvailableBufferMs(tempoHandle);

            while (stopwatch.Elapsed.TotalSeconds < commandTime + measurementSeconds)
            {
                AddPositionSample(samples, stopwatch, tempoHandle);
                Thread.SpinWait(100);
            }

            int start = 0;
            int sustainedSamples = 0;
            int firstChangedSample = -1;
            int firstChangedWindowStart = -1;
            for (int end = 1; end < samples.Count; end++)
            {
                if (samples[end].Time < commandTime)
                {
                    continue;
                }

                while (start + 1 < end &&
                       samples[end].Time - samples[start + 1].Time >= slopeWindowSeconds)
                {
                    start++;
                }

                double elapsed = samples[end].Time - samples[start].Time;
                double slope = (samples[end].Position - samples[start].Position) / elapsed;
                bool changedRate = Math.Abs(slope - speed) < Math.Abs(slope - 1.0);
                sustainedSamples = changedRate ? sustainedSamples + 1 : 0;
                firstChangedSample = changedRate && sustainedSamples == 1 ? end : firstChangedSample;
                firstChangedWindowStart = changedRate && sustainedSamples == 1 ? start : firstChangedWindowStart;
                if (sustainedSamples >= 3)
                {
                    int transitionObservation = firstChangedSample;
                    double earliestLatencyMs = (samples[firstChangedWindowStart].Time - commandTime) * 1000.0;
                    double latestLatencyMs = (samples[transitionObservation].Time - commandTime) * 1000.0;
                    double positionChangeMedianMs = GetTraceChangeMedianMs(samples, true);
                    double availableChangeMedianMs = GetTraceChangeMedianMs(samples, false);
                    return new TempoLatencyMeasurement(earliestLatencyMs, latestLatencyMs, availableBeforeMs,
                        availableAfterMs, positionChangeMedianMs, availableChangeMedianMs);
                }
            }

            throw new Exception("Tempo stream position did not change to expected rate");
        }

        private static void AddPositionSample(List<TempoTraceSample> samples,
            System.Diagnostics.Stopwatch stopwatch, int tempoHandle)
        {
            long positionBytes = Bass.ChannelGetPosition(tempoHandle, PositionFlags.Bytes);
            if (positionBytes < 0)
            {
                throw new Exception($"Failed to read tempo stream position: {Bass.LastError}");
            }

            double position = Bass.ChannelBytes2Seconds(tempoHandle, positionBytes);
            double availableMs = GetAvailableBufferMs(tempoHandle);
            samples.Add(new TempoTraceSample(stopwatch.Elapsed.TotalSeconds, position, availableMs));
        }

        private static double GetTraceChangeMedianMs(List<TempoTraceSample> samples, bool usePosition)
        {
            var intervals = new List<double>();
            double previousValue = usePosition ? samples[0].Position * 1000.0 : samples[0].AvailableMs;
            double previousChangeTime = samples[0].Time;

            for (int i = 1; i < samples.Count; i++)
            {
                double value = usePosition ? samples[i].Position * 1000.0 : samples[i].AvailableMs;
                if (Math.Abs(value - previousValue) < TEMPO_TRACE_VALUE_EPSILON_MS)
                {
                    continue;
                }

                double intervalMs = (samples[i].Time - previousChangeTime) * 1000.0;
                if (intervalMs > 0)
                {
                    intervals.Add(intervalMs);
                }

                previousValue = value;
                previousChangeTime = samples[i].Time;
            }

            intervals.Sort();
            return GetMedianOrNaN(intervals);
        }

        private static MemoryStream CreateTempoTestTrack()
        {
            const int durationSeconds = 10;
            int frames = TEMPO_TEST_SAMPLE_RATE * durationSeconds;
            var stream = new MemoryStream(44 + frames * 4);
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + frames * 4);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short) 1);
                writer.Write((short) 2);
                writer.Write(TEMPO_TEST_SAMPLE_RATE);
                writer.Write(TEMPO_TEST_SAMPLE_RATE * 4);
                writer.Write((short) 4);
                writer.Write((short) 16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(frames * 4);
                writer.Write(new byte[frames * 4]);
            }
            stream.Position = 0;
            return stream;
        }

        private static async Task MeasureMixerSeekLatency(BassAudioManager audioManager)
        {
            var mixer = CreateTestMixer(audioManager);
            FileStream fileStream = null;

            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "metronome", "sine_hi.ogg");
                if (!File.Exists(path))
                {
                    throw new Exception($"Audio file not found: {path}");
                }

                fileStream = File.OpenRead(path);
                if (!mixer.AddChannel(fileStream, SongStem.Song))
                {
                    throw new Exception("Failed to add channel to mixer");
                }

                int tempoStreamHandle = GetTempoStreamHandle(mixer);
                mixer.Play();

                await Task.Delay(500);

                long totalBytes = Bass.ChannelGetLength(tempoStreamHandle);
                double fileLength = Bass.ChannelBytes2Seconds(tempoStreamHandle, totalBytes);

                double seekTarget = 0.0;
                double syncTarget = Math.Min(0.05, fileLength * 0.5);
                double playbackDuration = syncTarget - seekTarget;
                long syncTargetBytes = Bass.ChannelSeconds2Bytes(tempoStreamHandle, syncTarget);

                var tcs = new TaskCompletionSource<long>();
                var stopwatch = new System.Diagnostics.Stopwatch();

                SyncProcedure syncCallback = (handle, channel, data, user) =>
                {
                    stopwatch.Stop();
                    tcs.TrySetResult(stopwatch.ElapsedMilliseconds);
                };

                int syncHandle = Bass.ChannelSetSync(
                    tempoStreamHandle,
                    SyncFlags.Position | SyncFlags.Onetime,
                    syncTargetBytes,
                    syncCallback,
                    IntPtr.Zero
                );

                if (syncHandle == 0)
                {
                    throw new Exception($"Failed to set BASS position sync: {Bass.LastError}");
                }

                stopwatch.Start();
                mixer.SetPosition(seekTarget);

                var timeoutTask = Task.Delay(2000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask != tcs.Task)
                {
                    Bass.ChannelRemoveSync(tempoStreamHandle, syncHandle);
                    throw new Exception("Timeout waiting for BASS position sync (audio did not play or seek failed)");
                }

                long totalElapsedMs = await tcs.Task;
                long actualSeekLatencyMs = totalElapsedMs - (long) (playbackDuration * 1000);

                var info = Bass.Info;
                int infoLatency = info.Latency;
                int deviceBufferLength = Bass.DeviceBufferLength;
                int devPeriod = Bass.GetConfig(Configuration.DevicePeriod);
                int updatePeriod = Bass.UpdatePeriod;
                int minBufferLength = info.MinBufferLength;
                int configuredBufferLength = SettingsManager.Settings.PlaybackBufferLength.Value;
                double deviceLatency = GlobalAudioHandler.PlaybackLatency;
                double playback = mixer.GetPlaybackStartOffset() * 1000.0;
                double tempo = mixer.GetTempoStreamLatency() * 1000.0;

                Debug.Log($"<b>[Real Seek Latency]</b>\n" +
                          $"  - Actual Seek Latency: <b>{actualSeekLatencyMs}ms</b>\n" +
                          $"  - SongRunner Seek Latency: {playback:0.0}ms\n" +
                          $"  - Tempo Latency: {tempo:0.0}ms\n" +
                          $"  - User Configured Buffer Size: {configuredBufferLength}ms\n" +
                          $"  - Device Latency: {deviceLatency:0.0}ms\n" +
                          $"  - BASS Latency Components: info.Latency={infoLatency}ms, " +
                          $"DeviceBufferLength={deviceBufferLength}ms, updatePeriod={updatePeriod}ms, " +
                          $"devPeriod={devPeriod}ms, MinBuf={minBufferLength}ms");

                GC.KeepAlive(syncCallback);
            }
            finally
            {
                mixer.Dispose();
                fileStream?.Dispose();
            }
        }

        private static async Task MeasureOutputBufferPosition(BassAudioManager audioManager)
        {
            var mixer = CreateTestMixer(audioManager);
            FileStream fileStream = null;

            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "metronome", "sine_hi.ogg");
                if (!File.Exists(path))
                {
                    throw new Exception($"Audio file not found: {path}");
                }

                fileStream = File.OpenRead(path);
                if (!mixer.AddChannel(fileStream, SongStem.Song))
                {
                    throw new Exception("Failed to add channel to mixer");
                }

                int tempoStreamHandle = GetTempoStreamHandle(mixer);
                mixer.Play();
                await Task.Delay(250);

                var log = new StringBuilder();
                log.AppendLine("<b>[BASS Output Buffer Position]</b>");
                log.AppendLine("elapsedMs | playedMs | decodedMs | decodeAheadMs | availableMs");

                await AppendOutputBufferSamples(log, tempoStreamHandle, 6, 50);

                mixer.SetPosition(0);
                mixer.Play();
                log.AppendLine("After SetPosition(0) + Play():");

                await AppendOutputBufferSamples(log, tempoStreamHandle, 6, 50);

                Debug.Log(log.ToString());
            }
            finally
            {
                mixer.Dispose();
                fileStream?.Dispose();
            }
        }

        private static async Task AppendOutputBufferSamples(StringBuilder log, int tempoStreamHandle,
            int sampleCount, int sampleIntervalMs)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < sampleCount; i++)
            {
                await Task.Delay(sampleIntervalMs);
                AppendOutputBufferSample(log, tempoStreamHandle, stopwatch.ElapsedMilliseconds);
            }
        }

        private static void AppendOutputBufferSample(StringBuilder log, int tempoStreamHandle, long elapsedMs)
        {
            double playedMs = GetPositionMs(tempoStreamHandle, PositionFlags.Bytes);
            double decodedMs = GetPositionMs(tempoStreamHandle, PositionFlags.Bytes | PositionFlags.Decode);
            double decodeAheadMs = decodedMs - playedMs;
            double availableMs = GetAvailableBufferMs(tempoStreamHandle);

            log.AppendLine($"{elapsedMs,9} | {playedMs,8:0.0} | {decodedMs,9:0.0} | {decodeAheadMs,13:0.0} | " +
                           $"{availableMs,11:0.0}");
        }

        private static double GetPositionMs(int channelHandle, PositionFlags flags)
        {
            long bytes = Bass.ChannelGetPosition(channelHandle, flags);
            if (bytes < 0)
            {
                return double.NaN;
            }

            double seconds = Bass.ChannelBytes2Seconds(channelHandle, bytes);
            if (seconds < 0)
            {
                return double.NaN;
            }

            return seconds * 1000.0;
        }

        private static double GetAvailableBufferMs(int channelHandle)
        {
            int bytes = Bass.ChannelGetData(channelHandle, IntPtr.Zero, (int) DataFlags.Available);
            if (bytes < 0)
            {
                return double.NaN;
            }

            double seconds = Bass.ChannelBytes2Seconds(channelHandle, bytes);
            if (seconds < 0)
            {
                return double.NaN;
            }

            return seconds * 1000.0;
        }

        private static MethodInfo GetSettingsSetter()
        {
            var settingsProp = typeof(SettingsManager).GetProperty("Settings", BindingFlags.Static | BindingFlags.Public);
            return settingsProp?.GetSetMethod(nonPublic: true);
        }

        private static BassAudioManager GetAudioManager()
        {
            var instanceField = typeof(GlobalAudioHandler).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            return instanceField?.GetValue(null) as BassAudioManager;
        }

        private static BassStemMixer CreateTestMixer(BassAudioManager audioManager)
        {
            var createMixerMethod = typeof(BassAudioManager).GetMethod("CreateMixer", BindingFlags.Instance | BindingFlags.NonPublic);
            if (createMixerMethod == null)
            {
                throw new Exception("Could not find internal method CreateMixer on BassAudioManager");
            }

            BassStemMixer mixer;
            try
            {
                mixer = createMixerMethod.Invoke(audioManager,
                    new object[] { "TestMixer", 1.0f, 1.0, false, false }) as BassStemMixer;
            }
            catch (TargetInvocationException ex)
            {
                throw new Exception($"Failed to invoke BassAudioManager.CreateMixer: {ex.InnerException?.Message ?? ex.Message}",
                    ex.InnerException ?? ex);
            }

            if (mixer == null)
            {
                throw new Exception("Failed to create temporary BassStemMixer");
            }

            return mixer;
        }

        private static int GetTempoStreamHandle(BassStemMixer mixer)
        {
            var tempoStreamHandleField = typeof(BassStemMixer).GetField("_tempoStreamHandle", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tempoStreamHandleField == null)
            {
                throw new Exception("Failed to find tempo stream handle field");
            }

            int tempoStreamHandle = (int) tempoStreamHandleField.GetValue(mixer);
            if (tempoStreamHandle == 0)
            {
                throw new Exception("Failed to retrieve valid tempo stream handle");
            }

            return tempoStreamHandle;
        }

        private static void InitializePaths()
        {
            if (!string.IsNullOrEmpty(PathHelper.PersistentDataPath))
            {
                return;
            }

            var pathHelperInit = typeof(PathHelper).GetMethod("Init", BindingFlags.Static | BindingFlags.NonPublic);
            if (pathHelperInit != null)
            {
                pathHelperInit.Invoke(null, null);
            }
        }

        private static double GetExpectedPlaybackLatency(double deviceLatency)
        {
            return deviceLatency;
        }

        private static double GetExpectedCommandUpdateLatency()
        {
            return Math.Max(0, Bass.UpdatePeriod) / 2000.0;
        }



        private static void SetSettingValue<T>(AbstractSetting<T> setting, T value)
        {
            var field = typeof(AbstractSetting<T>).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new Exception($"Could not find backing field _value on AbstractSetting<{typeof(T).Name}>");
            }

            field.SetValue(setting, value);
        }
    }
}
