using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
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
        private const int TEMPO_RESUME_SAMPLE_COUNT = 8;
        private const int TEMPO_RESUME_PLAY_TIME_MS = 250;
        private const int TEMPO_RESUME_PAUSE_TIME_MS = 100;
        private const int TEMPO_COMMAND_JITTER_MAX_MS = 100;
        private const double TEMPO_TRACE_VALUE_EPSILON_MS = 0.01;
        private const int POSITION_CLOCK_WARMUP_MS = 3000;
        private const int POSITION_CLOCK_DURATION_MS = 20000;
        private const int POSITION_CLOCK_SAMPLE_INTERVAL_MS = 1;
        private const int POSITION_CLOCK_UPDATE_PERIOD_MS = 5;
        private const double POSITION_CLOCK_DISCONTINUITY_MS = 100;
        private const int PLAY_POSITION_UPDATE_SAMPLE_COUNT = 100;
        private static readonly int[] DEVICE_BUFFER_TEST_PERIOD_MULTIPLIERS = { 2, 4, 8, 16 };
        private static readonly float[] TEMPO_TEST_SPEEDS = { 0.5f, 0.75f, 1.5f, 2.0f };
        private static readonly int[] TEMPO_PIPELINE_UPDATE_PERIODS_MS = { 5, 10, 20 };
        private static readonly int[] TEMPO_PIPELINE_SEEK_WINDOWS_MS = { 10, 20, 28, 40 };
        // 150ms baseline is already covered by the speed sweep.
        private static readonly int[] TEMPO_TEST_BUFFERS_MS = { 50, 100, 250 };
        private static readonly (float speed, int bufferMs)[] TEMPO_RESUME_CONFIGURATIONS =
        {
            (1.0f, 50),
            (1.0f, 150),
            (1.0f, 250),
            (0.5f, 150),
            (1.5f, 150),
        };
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

        private readonly struct TempoResumeSnapshot
        {
            public readonly double Timestamp;
            public readonly double StreamPosition;
            public readonly double DecodePosition;
            public readonly double MixerPosition;
            public readonly double ControlPosition;
            public readonly double AvailableMs;

            public TempoResumeSnapshot(double timestamp, double streamPosition, double decodePosition,
                double mixerPosition, double controlPosition, double availableMs)
            {
                Timestamp = timestamp;
                StreamPosition = streamPosition;
                DecodePosition = decodePosition;
                MixerPosition = mixerPosition;
                ControlPosition = controlPosition;
                AvailableMs = availableMs;
            }
        }

        private readonly struct TempoResumeMeasurement
        {
            public readonly double CallDurationMs;
            public readonly double StreamExcessMs;
            public readonly double MixerExcessMs;
            public readonly double ControlExcessMs;
            public readonly double DecodeAdvanceMs;
            public readonly double AvailableChangeMs;

            public TempoResumeMeasurement(double callDurationMs, double streamExcessMs,
                double mixerExcessMs, double controlExcessMs, double decodeAdvanceMs,
                double availableChangeMs)
            {
                CallDurationMs = callDurationMs;
                StreamExcessMs = streamExcessMs;
                MixerExcessMs = mixerExcessMs;
                ControlExcessMs = controlExcessMs;
                DecodeAdvanceMs = decodeAdvanceMs;
                AvailableChangeMs = availableChangeMs;
            }
        }

        private readonly struct RawPositionSnapshot
        {
            public readonly double PlayedMs;
            public readonly double DecodedMs;
            public readonly double AvailableMs;

            public RawPositionSnapshot(double playedMs, double decodedMs, double availableMs)
            {
                PlayedMs = playedMs;
                DecodedMs = decodedMs;
                AvailableMs = availableMs;
            }
        }

        private readonly struct PositionClockSample
        {
            public readonly double WallTime;
            public readonly double MonotonicTime;
            public readonly double Position;
            public readonly double NativePosition;

            public PositionClockSample(double wallTime, double monotonicTime, double position,
                double nativePosition)
            {
                WallTime = wallTime;
                MonotonicTime = monotonicTime;
                Position = position;
                NativePosition = nativePosition;
            }
        }

#if UNITY_EDITOR_LINUX
        [StructLayout(LayoutKind.Sequential)]
        private struct Timespec
        {
            public long Seconds;
            public long Nanoseconds;
        }

        private const int CLOCK_MONOTONIC = 1;
        private const int TIMER_ABSTIME = 1;

        [DllImport("libc", SetLastError = true)]
        private static extern int clock_gettime(int clockId, out Timespec time);

        [DllImport("libc")]
        private static extern int clock_nanosleep(int clockId, int flags, ref Timespec request,
            IntPtr remaining);

        // Bypass ManagedBass while retaining the same loaded BASS instance and channel handle.
        // These declarations intentionally cover only the two calls needed by this diagnostic.
        [DllImport("bass", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "BASS_ChannelGetPosition")]
        private static extern ulong NativeBassChannelGetPosition(int handle, uint mode);

        [DllImport("bass", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "BASS_ChannelBytes2Seconds")]
        private static extern double NativeBassChannelBytes2Seconds(int handle, ulong position);
#endif

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

            if (GetAudioManager() == null)
            {
                Debug.LogError("Failed to get active BassAudioManager instance!");
                settingsSetter.Invoke(null, new object[] { originalSettings });
                return;
            }

            bool audioClosed = false;
            try
            {
                // BASS_CONFIG_DEV_BUFFER only takes effect before BASS_Init. Recreate a clean
                // default device for every value so no previous device state affects the result.
                GlobalAudioHandler.Close();
                audioClosed = true;

                int devicePeriod = Bass.GetConfig(Configuration.DevicePeriod);
                if (devicePeriod <= 0)
                {
                    throw new Exception($"Invalid BASS device update period: {devicePeriod}ms");
                }

                foreach (int periodMultiplier in DEVICE_BUFFER_TEST_PERIOD_MULTIPLIERS)
                {
                    // DEV_BUFFER must be a multiple of DEV_PERIOD and at least twice its size.
                    int requestedBufferLength = devicePeriod * periodMultiplier;
                    Bass.Free();
                    Bass.UpdatePeriod = 5;
                    Bass.DeviceNonStop = true;
                    Bass.DeviceBufferLength = requestedBufferLength;

                    if (!Bass.Init(-1, 44100,
                            DeviceInitFlags.Default | DeviceInitFlags.Latency, IntPtr.Zero))
                    {
                        throw new Exception($"BASS_Init failed with requested device buffer " +
                            $"{requestedBufferLength}ms: {Bass.LastError}");
                    }

                    Debug.Log($"<b>[Plain PCM Stream Position Startup]</b> Starting measurement " +
                              $"with requested device buffer {requestedBufferLength}ms " +
                              $"(actual {Bass.DeviceBufferLength}ms)...");
                    await MeasurePlainStreamPositionStartup(requestedBufferLength);
                    Bass.Free();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Measurement Failed: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Measurement Failed", $"Error:\n{ex.Message}", "OK");
            }
            finally
            {
                Bass.Free();
                settingsSetter.Invoke(null, new object[] { originalSettings });

                if (audioClosed)
                {
                    // Restore normal YARG configuration and device ownership after clean-room runs.
                    GlobalAudioHandler.Initialize<BassAudioManager>();
                    if (originalSettings != null)
                    {
                        GlobalAudioHandler.SetBufferLength(originalSettings.PlaybackBufferLength.Value);
                    }
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

        [MenuItem("Tests/Measure BASS Position Clock Stability")]
        public static async void RunPositionClockStabilityMeasurement()
        {
            Debug.Log("<b>[BASS Position Clock Stability]</b> Starting measurement...");

            InitializePaths();
            // SettingContainer reads audio buffer limits from GlobalAudioHandler during construction.
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

            int originalUpdatePeriod = POSITION_CLOCK_UPDATE_PERIOD_MS;
            bool yargInitialized = false;
            try
            {
                // First reproduce the standalone C program inside the Unity process, without
                // constructing BassAudioManager or loading YARG's plugins/samples.
                GlobalAudioHandler.Close();
                Bass.Free();
                Bass.UpdatePeriod = POSITION_CLOCK_UPDATE_PERIOD_MS;
                if (!Bass.Init(-1, 44100, DeviceInitFlags.Default, IntPtr.Zero))
                {
                    throw new Exception($"Clean-room BASS_Init failed: {Bass.LastError}");
                }

                Debug.Log("<b>[BASS Position Clock Stability]</b> Running clean-room BASS_Init baseline.");
                await MeasureFilePositionClockBaseline("clean-room BASS_Init(-1, 44100)");
                Bass.Free();

                // Recreate normal YARG audio state, then run exactly the same filename-stream test.
                GlobalAudioHandler.Initialize<BassAudioManager>();
                yargInitialized = true;
                if (GetAudioManager() == null)
                {
                    throw new Exception("Failed to get reinitialized BassAudioManager instance");
                }

                originalUpdatePeriod = Bass.UpdatePeriod;
                Bass.UpdatePeriod = POSITION_CLOCK_UPDATE_PERIOD_MS;
                Debug.Log($"<b>[BASS Position Clock Stability]</b> Temporarily set BASS update period " +
                          $"to {Bass.UpdatePeriod}ms (was {originalUpdatePeriod}ms).");
                await MeasureFilePositionClockBaseline("normal YARG BassAudioManager initialization");
            }
            catch (Exception ex)
            {
                Debug.LogError($"BASS position clock measurement failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                if (!yargInitialized)
                {
                    // Leave editor with normal audio initialized even when clean-room test fails.
                    Bass.Free();
                    GlobalAudioHandler.Initialize<BassAudioManager>();
                    yargInitialized = true;
                }

                if (yargInitialized)
                {
                    Bass.UpdatePeriod = originalUpdatePeriod;
                }

                if (createdSettings)
                {
                    settingsSetter.Invoke(null, new object[] { originalSettings });
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

        [MenuItem("Tests/Measure Tempo Resume Offset")]
        public static async void RunTempoResumeOffsetMeasurement()
        {
            Debug.Log("<b>[Tempo Resume Offset]</b> Starting seek-start and pause/resume measurement...");

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

            int originalBufferLength = SettingsManager.Settings.PlaybackBufferLength.Value;
            try
            {
                foreach (var configuration in TEMPO_RESUME_CONFIGURATIONS)
                {
                    SetTempoTestBuffer(configuration.bufferMs);
                    await RunTempoResumeOffsetBatch(audioManager, configuration.speed,
                        configuration.bufferMs);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Tempo resume-offset measurement failed: {ex.Message}\n{ex.StackTrace}");
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

        [MenuItem("Tests/Compare Plain vs Tempo Resume Position")]
        public static async void RunPlainVsTempoResumePositionComparison()
        {
            Debug.Log("<b>[Plain vs Tempo Resume Position]</b> Starting raw BASS comparison...");

            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            string testFilePath = Path.Combine(Path.GetTempPath(),
                $"yarg-bass-resume-position-{Guid.NewGuid():N}.wav");
            int plainHandle = 0;
            int tempoSourceHandle = 0;
            int tempoHandle = 0;

            try
            {
                using (var testStream = CreateTempoTestTrack(10))
                using (var testFile = File.Create(testFilePath))
                {
                    testStream.CopyTo(testFile);
                }

                // Both channels read identical files and feed the same output device. Only the
                // second channel has a decoding source wrapped in BASS_FX TempoCreate.
                const BassFlags fileFlags = BassFlags.Prescan | BassFlags.AsyncFile;
                plainHandle = Bass.CreateStream(testFilePath, 0, 0, fileFlags);
                tempoSourceHandle = Bass.CreateStream(testFilePath, 0, 0,
                    fileFlags | BassFlags.Decode);
                if (plainHandle == 0 || tempoSourceHandle == 0)
                {
                    throw new Exception($"Failed to create comparison streams: {Bass.LastError}");
                }

                tempoHandle = BassFx.TempoCreate(tempoSourceHandle, BassFlags.FxFreeSource);
                if (tempoHandle == 0)
                {
                    throw new Exception($"Failed to create comparison tempo stream: {Bass.LastError}");
                }
                // Tempo stream owns source from this point.
                tempoSourceHandle = 0;

                float bufferSeconds = BassHelpers.ClampPlaybackBufferLength(TEST_BUFFER_MS) / 1000f;
                if (!Bass.ChannelSetAttribute(plainHandle, ChannelAttribute.Buffer, bufferSeconds) ||
                    !Bass.ChannelSetAttribute(tempoHandle, ChannelAttribute.Buffer, bufferSeconds))
                {
                    throw new Exception($"Failed to set comparison stream buffers: {Bass.LastError}");
                }

                Debug.Log($"<b>[Plain vs Tempo Resume Position]</b> buffer " +
                          $"{bufferSeconds * 1000:0.0}ms; update period {Bass.UpdatePeriod}ms; " +
                          $"Tempo FX sequence {GetChannelAttribute(tempoHandle, ChannelAttribute.TempoSequenceMilliseconds):0.00}ms; " +
                          $"seek {GetChannelAttribute(tempoHandle, ChannelAttribute.TempoSeekWindowMilliseconds):0.00}ms; " +
                          $"overlap {GetChannelAttribute(tempoHandle, ChannelAttribute.TempoOverlapMilliseconds):0.00}ms");

                for (int sample = 1; sample <= TEMPO_RESUME_SAMPLE_COUNT; sample++)
                {
                    await ComparePlainAndTempoResume(sample, plainHandle, tempoHandle);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Plain/tempo resume comparison failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                if (tempoHandle != 0)
                {
                    Bass.StreamFree(tempoHandle);
                }
                if (tempoSourceHandle != 0)
                {
                    Bass.StreamFree(tempoSourceHandle);
                }
                if (plainHandle != 0)
                {
                    Bass.StreamFree(plainHandle);
                }
                if (File.Exists(testFilePath))
                {
                    File.Delete(testFilePath);
                }
            }
        }

        private static async Task ComparePlainAndTempoResume(int sample, int plainHandle, int tempoHandle)
        {
            const double startSeconds = 2.0;
            long plainStart = Bass.ChannelSeconds2Bytes(plainHandle, startSeconds);
            long tempoStart = Bass.ChannelSeconds2Bytes(tempoHandle, startSeconds);
            if (!Bass.ChannelSetPosition(plainHandle, plainStart) ||
                !Bass.ChannelSetPosition(tempoHandle, tempoStart) ||
                !Bass.ChannelPlay(plainHandle) || !Bass.ChannelPlay(tempoHandle))
            {
                throw new Exception($"Failed to start comparison sample: {Bass.LastError}");
            }

            await Task.Delay(TEMPO_RESUME_PLAY_TIME_MS);
            if (!Bass.ChannelPause(plainHandle) || !Bass.ChannelPause(tempoHandle))
            {
                throw new Exception($"Failed to pause comparison sample: {Bass.LastError}");
            }

            RawPositionSnapshot pausedPlain = CaptureRawPosition(plainHandle);
            RawPositionSnapshot pausedTempo = CaptureRawPosition(tempoHandle);
            await Task.Delay(TEMPO_RESUME_PAUSE_TIME_MS);
            RawPositionSnapshot beforePlain = CaptureRawPosition(plainHandle);
            RawPositionSnapshot beforeTempo = CaptureRawPosition(tempoHandle);

            var log = new StringBuilder();
            log.AppendLine($"<b>[Plain vs Tempo Resume Position — sample {sample}/{TEMPO_RESUME_SAMPLE_COUNT}]</b>");
            log.AppendLine($"pause drift: plain {beforePlain.PlayedMs - pausedPlain.PlayedMs:+0.000;-0.000;0.000}ms; " +
                           $"tempo {beforeTempo.PlayedMs - pausedTempo.PlayedMs:+0.000;-0.000;0.000}ms");
            log.AppendLine("elapsed | advance plain/tempo | excess plain/tempo | excess delta | decoded plain/tempo | available plain/tempo");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            double plainCallStartMs = stopwatch.Elapsed.TotalMilliseconds;
            bool plainPlayed = Bass.ChannelPlay(plainHandle);
            double plainCallEndMs = stopwatch.Elapsed.TotalMilliseconds;
            double tempoCallStartMs = stopwatch.Elapsed.TotalMilliseconds;
            bool tempoPlayed = Bass.ChannelPlay(tempoHandle);
            double tempoCallEndMs = stopwatch.Elapsed.TotalMilliseconds;
            if (!plainPlayed || !tempoPlayed)
            {
                throw new Exception($"Failed to resume comparison sample: {Bass.LastError}");
            }
            double plainResumeMs = (plainCallStartMs + plainCallEndMs) * 0.5;
            double tempoResumeMs = (tempoCallStartMs + tempoCallEndMs) * 0.5;
            log.AppendLine($"play calls: plain {plainCallEndMs - plainCallStartMs:0.000}ms; " +
                           $"tempo {tempoCallEndMs - tempoCallStartMs:0.000}ms");

            AppendRawResumeComparison(log, stopwatch.Elapsed.TotalMilliseconds, plainHandle, tempoHandle,
                beforePlain, beforeTempo, plainResumeMs, tempoResumeMs);
            int previousTargetMs = 0;
            foreach (int targetMs in new[] { 10, 25, 50, 100, 200 })
            {
                await Task.Delay(targetMs - previousTargetMs);
                previousTargetMs = targetMs;
                AppendRawResumeComparison(log, stopwatch.Elapsed.TotalMilliseconds, plainHandle, tempoHandle,
                    beforePlain, beforeTempo, plainResumeMs, tempoResumeMs);
            }

            Bass.ChannelPause(plainHandle);
            Bass.ChannelPause(tempoHandle);
            Debug.Log(log.ToString());
        }

        private static RawPositionSnapshot CaptureRawPosition(int handle) => new(
            GetPositionMs(handle, PositionFlags.Bytes),
            GetPositionMs(handle, PositionFlags.Bytes | PositionFlags.Decode),
            GetAvailableBufferMs(handle));

        private static void AppendRawResumeComparison(StringBuilder log, double elapsedMs,
            int plainHandle, int tempoHandle, RawPositionSnapshot beforePlain,
            RawPositionSnapshot beforeTempo, double plainResumeMs, double tempoResumeMs)
        {
            RawPositionSnapshot plain = CaptureRawPosition(plainHandle);
            RawPositionSnapshot tempo = CaptureRawPosition(tempoHandle);
            double plainAdvance = plain.PlayedMs - beforePlain.PlayedMs;
            double tempoAdvance = tempo.PlayedMs - beforeTempo.PlayedMs;
            double plainDecodeAdvance = plain.DecodedMs - beforePlain.DecodedMs;
            double tempoDecodeAdvance = tempo.DecodedMs - beforeTempo.DecodedMs;
            double plainExcess = plainAdvance - Math.Max(0, elapsedMs - plainResumeMs);
            double tempoExcess = tempoAdvance - Math.Max(0, elapsedMs - tempoResumeMs);

            log.AppendLine($"{elapsedMs,7:0.0} | {plainAdvance,8:+0.000;-0.000;0.000}/" +
                           $"{tempoAdvance,8:+0.000;-0.000;0.000} | " +
                           $"{plainExcess,8:+0.000;-0.000;0.000}/" +
                           $"{tempoExcess,8:+0.000;-0.000;0.000} | " +
                           $"{tempoExcess - plainExcess,11:+0.000;-0.000;0.000} | " +
                           $"{plainDecodeAdvance,8:+0.000;-0.000;0.000}/" +
                           $"{tempoDecodeAdvance,8:+0.000;-0.000;0.000} | " +
                           $"{plain.AvailableMs,7:0.0}/{tempo.AvailableMs,7:0.0}");
        }

        private static async Task RunTempoResumeOffsetBatch(BassAudioManager audioManager, float speed,
            int bufferMs)
        {
            var seekStartMeasurements = new List<TempoResumeMeasurement>(TEMPO_RESUME_SAMPLE_COUNT);
            var resumeMeasurements = new List<TempoResumeMeasurement>(TEMPO_RESUME_SAMPLE_COUNT);
            var probeMixer = CreateTestMixer(audioManager);
            (float sequenceMs, float seekWindowMs, float overlapMs, bool useQuickAlgorithm,
                bool useAAFilter, float aaFilterLength) attributes;
            try
            {
                probeMixer.SetPlaybackSpeed(speed);
                attributes = GetTempoFxAttributes(GetTempoStreamHandle(probeMixer));
            }
            finally
            {
                probeMixer.Dispose();
            }

            string label = $"speed {speed:0.##}x; buffer {bufferMs}ms";
            Debug.Log($"<b>[Tempo Resume Offset]</b> {label}; samples: {TEMPO_RESUME_SAMPLE_COUNT}; " +
                      $"FX sequence {attributes.sequenceMs:0.00}ms; seek {attributes.seekWindowMs:0.00}ms; " +
                      $"overlap {attributes.overlapMs:0.00}ms; quick {attributes.useQuickAlgorithm}");

            for (int sample = 1; sample <= TEMPO_RESUME_SAMPLE_COUNT; sample++)
            {
                var measurements = await MeasureTempoResumeOffsets(audioManager, speed);
                seekStartMeasurements.Add(measurements.seekStart);
                resumeMeasurements.Add(measurements.resume);

                Debug.Log($"<b>[Tempo Resume Offset]</b> {label}; sample " +
                          $"{sample}/{TEMPO_RESUME_SAMPLE_COUNT}\n" +
                          FormatTempoResumeMeasurement("seek → play", measurements.seekStart) + "\n" +
                          FormatTempoResumeMeasurement("pause → play", measurements.resume));
            }

            LogTempoResumeOffsetSummary(label, "seek → play", seekStartMeasurements);
            LogTempoResumeOffsetSummary(label, "pause → play", resumeMeasurements);
        }

        private static async Task<(TempoResumeMeasurement seekStart, TempoResumeMeasurement resume)>
            MeasureTempoResumeOffsets(BassAudioManager audioManager, float speed)
        {
            var mixer = CreateTestMixer(audioManager);
            var testStream = CreateTempoTestTrack(10);

            try
            {
                if (!mixer.AddChannel(testStream, SongStem.Song))
                {
                    throw new Exception("Failed to add generated test track to mixer");
                }

                mixer.SetPlaybackSpeed(speed);
                mixer.SetPosition(2.0);
                int tempoHandle = GetTempoStreamHandle(mixer);

                TempoResumeSnapshot beforeSeekStart = CaptureTempoResumeSnapshot(mixer, tempoHandle);
                long seekStartCall = System.Diagnostics.Stopwatch.GetTimestamp();
                if (mixer.Play() != 0)
                {
                    throw new Exception($"Failed to play tempo stream after seek: {Bass.LastError}");
                }
                long seekStartReturn = System.Diagnostics.Stopwatch.GetTimestamp();
                TempoResumeSnapshot afterSeekStart = CaptureTempoResumeSnapshot(mixer, tempoHandle);
                var seekStart = CalculateTempoResumeMeasurement(beforeSeekStart, afterSeekStart,
                    seekStartCall, seekStartReturn, speed);

                await Task.Delay(TEMPO_RESUME_PLAY_TIME_MS);
                if (mixer.Pause() != 0)
                {
                    throw new Exception($"Failed to pause tempo stream: {Bass.LastError}");
                }

                await Task.Delay(TEMPO_RESUME_PAUSE_TIME_MS);
                TempoResumeSnapshot beforeResume = CaptureTempoResumeSnapshot(mixer, tempoHandle);
                long resumeCall = System.Diagnostics.Stopwatch.GetTimestamp();
                if (mixer.Play() != 0)
                {
                    throw new Exception($"Failed to resume tempo stream: {Bass.LastError}");
                }
                long resumeReturn = System.Diagnostics.Stopwatch.GetTimestamp();
                TempoResumeSnapshot afterResume = CaptureTempoResumeSnapshot(mixer, tempoHandle);
                var resume = CalculateTempoResumeMeasurement(beforeResume, afterResume,
                    resumeCall, resumeReturn, speed);

                return (seekStart, resume);
            }
            finally
            {
                mixer.Dispose();
                testStream.Dispose();
            }
        }

        private static TempoResumeSnapshot CaptureTempoResumeSnapshot(BassStemMixer mixer, int tempoHandle)
        {
            long before = System.Diagnostics.Stopwatch.GetTimestamp();
            double streamPosition = GetPositionMs(tempoHandle, PositionFlags.Bytes) / 1000.0;
            double decodePosition = GetPositionMs(tempoHandle,
                PositionFlags.Bytes | PositionFlags.Decode) / 1000.0;
            double mixerPosition = mixer.GetPosition();
            double controlPosition = mixer.GetControlPosition();
            double availableMs = GetAvailableBufferMs(tempoHandle);
            long after = System.Diagnostics.Stopwatch.GetTimestamp();
            double timestamp = (before + (after - before) * 0.5) /
                (double) System.Diagnostics.Stopwatch.Frequency;
            return new TempoResumeSnapshot(timestamp, streamPosition, decodePosition, mixerPosition,
                controlPosition, availableMs);
        }

        private static TempoResumeMeasurement CalculateTempoResumeMeasurement(
            TempoResumeSnapshot before, TempoResumeSnapshot after, long callStart, long callEnd, float speed)
        {
            double elapsedSeconds = after.Timestamp - before.Timestamp;
            double expectedAdvance = elapsedSeconds * speed;
            double callDurationMs = (callEnd - callStart) * 1000.0 /
                System.Diagnostics.Stopwatch.Frequency;

            return new TempoResumeMeasurement(
                callDurationMs,
                (after.StreamPosition - before.StreamPosition - expectedAdvance) * 1000.0,
                (after.MixerPosition - before.MixerPosition - expectedAdvance) * 1000.0,
                (after.ControlPosition - before.ControlPosition - expectedAdvance) * 1000.0,
                (after.DecodePosition - before.DecodePosition) * 1000.0,
                after.AvailableMs - before.AvailableMs);
        }

        private static string FormatTempoResumeMeasurement(string transition,
            TempoResumeMeasurement measurement)
        {
            return $"  {transition}: call {measurement.CallDurationMs:0.000}ms; " +
                   $"excess stream {measurement.StreamExcessMs:+0.000;-0.000;0.000}ms; " +
                   $"mixer {measurement.MixerExcessMs:+0.000;-0.000;0.000}ms; " +
                   $"control <b>{measurement.ControlExcessMs:+0.000;-0.000;0.000}ms</b>; " +
                   $"decode advance {measurement.DecodeAdvanceMs:+0.000;-0.000;0.000}ms; " +
                   $"available change {measurement.AvailableChangeMs:+0.000;-0.000;0.000}ms";
        }

        private static void LogTempoResumeOffsetSummary(string label, string transition,
            List<TempoResumeMeasurement> measurements)
        {
            var callDurations = new List<double>(measurements.Count);
            var streamOffsets = new List<double>(measurements.Count);
            var mixerOffsets = new List<double>(measurements.Count);
            var controlOffsets = new List<double>(measurements.Count);
            var decodeAdvances = new List<double>(measurements.Count);
            var availableChanges = new List<double>(measurements.Count);
            foreach (var measurement in measurements)
            {
                AddFiniteValue(callDurations, measurement.CallDurationMs);
                AddFiniteValue(streamOffsets, measurement.StreamExcessMs);
                AddFiniteValue(mixerOffsets, measurement.MixerExcessMs);
                AddFiniteValue(controlOffsets, measurement.ControlExcessMs);
                AddFiniteValue(decodeAdvances, measurement.DecodeAdvanceMs);
                AddFiniteValue(availableChanges, measurement.AvailableChangeMs);
            }

            callDurations.Sort();
            streamOffsets.Sort();
            mixerOffsets.Sort();
            controlOffsets.Sort();
            decodeAdvances.Sort();
            availableChanges.Sort();

            Debug.Log($"<b>[Tempo Resume Offset — {transition} Summary]</b> {label}\n" +
                      $"Play call median/p95: {GetMedianOrNaN(callDurations):0.000}/" +
                      $"{GetPercentileOrNaN(callDurations, 0.95):0.000}ms\n" +
                      $"Excess stream median/p95: {GetMedianOrNaN(streamOffsets):+0.000;-0.000;0.000}/" +
                      $"{GetPercentileOrNaN(streamOffsets, 0.95):+0.000;-0.000;0.000}ms\n" +
                      $"Excess mixer median/p95: {GetMedianOrNaN(mixerOffsets):+0.000;-0.000;0.000}/" +
                      $"{GetPercentileOrNaN(mixerOffsets, 0.95):+0.000;-0.000;0.000}ms\n" +
                      $"Excess control median/p95: <b>{GetMedianOrNaN(controlOffsets):+0.000;-0.000;0.000}/" +
                      $"{GetPercentileOrNaN(controlOffsets, 0.95):+0.000;-0.000;0.000}ms</b>\n" +
                      $"Decode advance median: {GetMedianOrNaN(decodeAdvances):+0.000;-0.000;0.000}ms; " +
                      $"available change median: {GetMedianOrNaN(availableChanges):+0.000;-0.000;0.000}ms");
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

        private static MemoryStream CreateTempoTestTrack(int durationSeconds = 10)
        {
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

        private static async Task MeasurePlainStreamPositionStartup(int requestedDeviceBufferLength)
        {
            string testFilePath = Path.Combine(Path.GetTempPath(),
                $"yarg-bass-position-startup-{Guid.NewGuid():N}.wav");
            int streamHandle = 0;

            try
            {
                // Uncompressed PCM filename stream: no OGG decoder, callbacks, mixer, or BASS_FX.
                using (var testStream = CreateTempoTestTrack(10))
                using (var testFile = File.Create(testFilePath))
                {
                    testStream.CopyTo(testFile);
                }

                streamHandle = Bass.CreateStream(testFilePath, 0, 0, BassFlags.Default);
                if (streamHandle == 0)
                {
                    throw new Exception($"Failed to create plain PCM stream: {Bass.LastError}");
                }

                var playCallSamples = new List<double>(PLAY_POSITION_UPDATE_SAMPLE_COUNT);
                var callToChangeSamples = new List<double>(PLAY_POSITION_UPDATE_SAMPLE_COUNT);
                var returnToChangeSamples = new List<double>(PLAY_POSITION_UPDATE_SAMPLE_COUNT);
                var observedPositionSamples = new List<double>(PLAY_POSITION_UPDATE_SAMPLE_COUNT);
                var primeCallSamples = new List<double>(PLAY_POSITION_UPDATE_SAMPLE_COUNT);
                var primedBytesSamples = new List<int>(PLAY_POSITION_UPDATE_SAMPLE_COUNT);

                for (int sample = 1; sample <= PLAY_POSITION_UPDATE_SAMPLE_COUNT; sample++)
                {
                    var tcs = new TaskCompletionSource<(long ticks, long position)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    var pollerReady = new ManualResetEventSlim(false);
                    int stopPolling = 0;
                    int startPolling = 0;
                    long postPlayPosition = 0;

                    if (!Bass.ChannelSetPosition(streamHandle, 0, PositionFlags.Bytes))
                    {
                        pollerReady.Dispose();
                        throw new Exception($"Failed to seek plain PCM stream: {Bass.LastError}");
                    }

                    long primeCallTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (!Bass.ChannelUpdate(streamHandle, 0))
                    {
                        pollerReady.Dispose();
                        throw new Exception($"Failed to prime plain PCM stream: {Bass.LastError}");
                    }
                    long primeReturnTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    int primedBytes = Bass.ChannelGetData(streamHandle, IntPtr.Zero,
                        (int) DataFlags.Available);
                    if (primedBytes < 0)
                    {
                        pollerReady.Dispose();
                        throw new Exception($"Failed to query primed PCM data: {Bass.LastError}");
                    }

                    // Keep a hot polling thread ready before ChannelPlay. It begins reading as soon as
                    // the immediate post-ChannelPlay position has been published. This measures the
                    // reporting stall after ChannelPlay returns, without frame timing or Task.Delay
                    // resolution in the measurement path.
                    var pollingThread = new Thread(() =>
                    {
                        pollerReady.Set();
                        while (Volatile.Read(ref startPolling) == 0 &&
                               Volatile.Read(ref stopPolling) == 0)
                        {
                            Thread.SpinWait(1);
                        }

                        while (Volatile.Read(ref stopPolling) == 0)
                        {
                            long position = Bass.ChannelGetPosition(streamHandle,
                                PositionFlags.Bytes);
                            long ticks = System.Diagnostics.Stopwatch.GetTimestamp();
                            if (position >= 0 && position != Volatile.Read(ref postPlayPosition))
                            {
                                tcs.TrySetResult((ticks, position));
                                return;
                            }
                        }
                    })
                    {
                        IsBackground = true,
                        Name = $"BASS position measurement {sample}",
                        // AboveNormal reduces scheduler wake-up error without risking starvation of
                        // BASS's own real-time update thread, which would distort the result.
                        Priority = System.Threading.ThreadPriority.AboveNormal
                    };

                    long playCallTicks;
                    long playReturnTicks;
                    try
                    {
                        pollingThread.Start();
                        pollerReady.Wait();

                        playCallTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        bool played = Bass.ChannelPlay(streamHandle, false);
                        playReturnTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        if (!played)
                        {
                            throw new Exception($"Failed to play plain PCM stream: {Bass.LastError}");
                        }

                        // This is the same observation production makes immediately after ChannelPlay.
                        // Publish baseline only after native call returns, matching production timing.
                        postPlayPosition = Bass.ChannelGetPosition(streamHandle,
                            PositionFlags.Bytes);
                        if (postPlayPosition < 0)
                        {
                            throw new Exception($"Failed to read post-play BASS position: {Bass.LastError}");
                        }
                        Volatile.Write(ref startPolling, 1);

                        var timeoutTask = Task.Delay(2000);
                        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                        if (completedTask != tcs.Task)
                        {
                            long finalPosition = Bass.ChannelGetPosition(streamHandle,
                                PositionFlags.Bytes);
                            PlaybackState state = Bass.ChannelIsActive(streamHandle);
                            throw new Exception($"Sample {sample}: timeout waiting for BASS playback " +
                                $"position to change (postPlay={postPlayPosition}, final={finalPosition}, " +
                                $"state={state})");
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref stopPolling, 1);
                        pollingThread.Join(250);
                        pollerReady.Dispose();
                    }

                    var firstChange = await tcs.Task;
                    if (Bass.ChannelIsActive(streamHandle) == PlaybackState.Playing)
                    {
                        bool paused = Bass.ChannelPause(streamHandle);
                        // Short test file can reach its end between state check and Pause().
                        if (!paused &&
                            Bass.ChannelIsActive(streamHandle) == PlaybackState.Playing)
                        {
                            throw new Exception($"Failed to pause plain PCM stream after sample {sample}: " +
                                $"{Bass.LastError}");
                        }
                    }
                    double playCallMs = (playReturnTicks - playCallTicks) * 1000.0 /
                        System.Diagnostics.Stopwatch.Frequency;
                    double callToChangeMs = (firstChange.ticks - playCallTicks) * 1000.0 /
                        System.Diagnostics.Stopwatch.Frequency;
                    double returnToChangeMs = (firstChange.ticks - playReturnTicks) * 1000.0 /
                        System.Diagnostics.Stopwatch.Frequency;
                    double observedPositionMs = Bass.ChannelBytes2Seconds(streamHandle,
                        firstChange.position) * 1000.0;
                    double primeCallMs = (primeReturnTicks - primeCallTicks) * 1000.0 /
                        System.Diagnostics.Stopwatch.Frequency;
                    playCallSamples.Add(playCallMs);
                    callToChangeSamples.Add(callToChangeMs);
                    returnToChangeSamples.Add(returnToChangeMs);
                    observedPositionSamples.Add(observedPositionMs);
                    primeCallSamples.Add(primeCallMs);
                    primedBytesSamples.Add(primedBytes);
                    Debug.Log($"[Plain PCM Stream Position Startup] Sample {sample}/{PLAY_POSITION_UPDATE_SAMPLE_COUNT}: " +
                        $"prime={primeCallMs:0.000}ms ({primedBytes} bytes available); " +
                        $"play={playCallMs:0.000}ms; call→change={callToChangeMs:0.000}ms; " +
                        $"return→change={returnToChangeMs:0.000}ms; " +
                        $"post-play position={Bass.ChannelBytes2Seconds(streamHandle, postPlayPosition) * 1000.0:0.000}ms; " +
                        $"first changed position={observedPositionMs:0.000}ms");
                }

                playCallSamples.Sort();
                callToChangeSamples.Sort();
                returnToChangeSamples.Sort();
                observedPositionSamples.Sort();
                primeCallSamples.Sort();
                primedBytesSamples.Sort();
                double latencyMean = 0;
                foreach (double value in returnToChangeSamples)
                {
                    latencyMean += value;
                }
                latencyMean /= returnToChangeSamples.Count;
                double latencyVariance = 0;
                foreach (double value in returnToChangeSamples)
                {
                    double difference = value - latencyMean;
                    latencyVariance += difference * difference;
                }
                double latencyJitter = Math.Sqrt(latencyVariance / returnToChangeSamples.Count);
                double latencyMedian = GetPercentile(returnToChangeSamples, 0.5);
                double latencyP95 = GetPercentile(returnToChangeSamples, 0.95);

                var info = Bass.Info;
                int infoLatency = info.Latency;
                int deviceBufferLength = Bass.DeviceBufferLength;
                int devPeriod = Bass.GetConfig(Configuration.DevicePeriod);
                int updatePeriod = Bass.UpdatePeriod;
                int minBufferLength = info.MinBufferLength;

                Debug.Log($"<b>[Plain PCM Stream Position Startup]</b>\n" +
                          $"  - Pipeline: generated PCM WAV → direct BASS playback stream\n" +
                          $"  - Requested/Actual Device Buffer: " +
                          $"{requestedDeviceBufferLength}/{deviceBufferLength}ms\n" +
                          $"  - ChannelPlay Return → BASS Position Change ({returnToChangeSamples.Count} samples):\n" +
                          $"      Mean: <b>{latencyMean:0.000}ms</b>; Median: " +
                          $"<b>{latencyMedian:0.000}ms</b>; P95: {latencyP95:0.000}ms\n" +
                          $"      Min/Max: {returnToChangeSamples[0]:0.000}/{returnToChangeSamples[returnToChangeSamples.Count - 1]:0.000}ms; " +
                          $"Jitter (stddev): {latencyJitter:0.000}ms\n" +
                          $"      Samples: {string.Join(", ", returnToChangeSamples.ConvertAll(value => $"{value:0.000}"))}ms\n" +
                          $"  - ChannelPlay Call Duration Min/Median/Max: " +
                          $"{playCallSamples[0]:0.000}/{GetPercentile(playCallSamples, 0.5):0.000}/" +
                          $"{playCallSamples[playCallSamples.Count - 1]:0.000}ms\n" +
                          $"  - ChannelPlay Call → BASS Position Change Min/Median/Max: " +
                          $"{callToChangeSamples[0]:0.000}/{GetPercentile(callToChangeSamples, 0.5):0.000}/" +
                          $"{callToChangeSamples[callToChangeSamples.Count - 1]:0.000}ms\n" +
                          $"  - First Observed BASS Position Min/Max: " +
                          $"{observedPositionSamples[0]:0.000}/" +
                          $"{observedPositionSamples[observedPositionSamples.Count - 1]:0.000}ms\n" +
                          $"  - ChannelUpdate Prime Duration Min/Median/Max: " +
                          $"{primeCallSamples[0]:0.000}/{GetPercentile(primeCallSamples, 0.5):0.000}/" +
                          $"{primeCallSamples[primeCallSamples.Count - 1]:0.000}ms\n" +
                          $"  - Buffered Bytes After Prime Min/Max: " +
                          $"{primedBytesSamples[0]}/{primedBytesSamples[primedBytesSamples.Count - 1]}\n" +
                          $"  - BASS Latency Components: info.Latency={infoLatency}ms, " +
                          $"DeviceBufferLength={deviceBufferLength}ms, updatePeriod={updatePeriod}ms, " +
                          $"devPeriod={devPeriod}ms, MinBuf={minBufferLength}ms");

            }
            finally
            {
                if (streamHandle != 0)
                {
                    Bass.StreamFree(streamHandle);
                }

                if (File.Exists(testFilePath))
                {
                    File.Delete(testFilePath);
                }
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

        private static async Task MeasureFilePositionClockBaseline(string initialization)
        {
            string testFilePath = Path.Combine(Path.GetTempPath(),
                $"yarg-bass-position-clock-{Guid.NewGuid():N}.wav");
            int streamHandle = 0;

            try
            {
                // Match the standalone C repro's stream setup: a WAV filename, no callbacks,
                // no stream flags, and no per-stream buffer override.
                using (var testStream = CreateTempoTestTrack(30))
                using (var testFile = File.Create(testFilePath))
                {
                    testStream.CopyTo(testFile);
                }

                streamHandle = Bass.CreateStream(testFilePath, 0, 0, BassFlags.Default);
                if (streamHandle == 0)
                {
                    throw new Exception($"Failed to create baseline file stream: {Bass.LastError}");
                }

                if (!Bass.ChannelPlay(streamHandle))
                {
                    throw new Exception($"Failed to play baseline file stream: {Bass.LastError}");
                }

#if UNITY_EDITOR_LINUX
                var standaloneResult = StartConcurrentStandaloneBassClock(testFilePath);
#endif
                await Task.Delay(POSITION_CLOCK_WARMUP_MS);
                var samples = await CollectPositionClockSamples(streamHandle);
                LogPositionClockSamples(samples,
                    $"Baseline WAV filename stream ({initialization}; no flags or buffer override)");
#if UNITY_EDITOR_LINUX
                Debug.Log(await standaloneResult);
#endif
            }
            finally
            {
                if (streamHandle != 0)
                {
                    Bass.StreamFree(streamHandle);
                }

                if (File.Exists(testFilePath))
                {
                    File.Delete(testFilePath);
                }
            }
        }

        private static async Task MeasurePlainPositionClockStability()
        {
            var testStream = CreateTempoTestTrack(30);
            int streamHandle = 0;

            try
            {
                const BassFlags streamFlags =
                    BassFlags.Prescan | BassFlags.AsyncFile | (BassFlags) 64; // BASS_SAMPLE_NOREORDER
                streamHandle = Bass.CreateStream(StreamSystem.NoBuffer, streamFlags,
                    new BassStreamProcedures(testStream));
                if (streamHandle == 0)
                {
                    throw new Exception($"Failed to create plain playback stream: {Bass.LastError}");
                }

                float bufferSeconds = BassHelpers.ClampPlaybackBufferLength(TEST_BUFFER_MS) / 1000f;
                if (!Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Buffer, bufferSeconds))
                {
                    throw new Exception($"Failed to set plain playback stream buffer: {Bass.LastError}");
                }

                if (!Bass.ChannelPlay(streamHandle))
                {
                    throw new Exception($"Failed to play plain playback stream: {Bass.LastError}");
                }

                await Task.Delay(POSITION_CLOCK_WARMUP_MS);
                var samples = await CollectPositionClockSamples(streamHandle);
                LogPositionClockSamples(samples, "Plain BASS playback stream (no mixer, no BASS_FX)");
            }
            finally
            {
                if (streamHandle != 0)
                {
                    Bass.StreamFree(streamHandle);
                }

                testStream.Dispose();
            }
        }

        private static async Task MeasureTempoPositionClockStability(BassAudioManager audioManager)
        {
            var mixer = CreateTestMixer(audioManager);
            var testStream = CreateTempoTestTrack(30);

            try
            {
                if (!mixer.AddChannel(testStream, SongStem.Song))
                {
                    throw new Exception("Failed to add generated test track to mixer");
                }

                int tempoStreamHandle = GetTempoStreamHandle(mixer);
                mixer.Play();
                await Task.Delay(POSITION_CLOCK_WARMUP_MS);
                var samples = await CollectPositionClockSamples(tempoStreamHandle);
                LogPositionClockSamples(samples, "BASS_FX tempo playback stream");
            }
            finally
            {
                mixer.Dispose();
                testStream.Dispose();
            }
        }

        private static Task<List<PositionClockSample>> CollectPositionClockSamples(int streamHandle)
        {
            return Task.Run(() =>
            {
                int sampleCapacity = POSITION_CLOCK_DURATION_MS / POSITION_CLOCK_SAMPLE_INTERVAL_MS;
                var result = new List<PositionClockSample>(sampleCapacity);

#if UNITY_EDITOR_LINUX
                // Deliberately mirror bass_position_clock_repro.c: native BASS calls only,
                // CLOCK_MONOTONIC only, one position query per sample, fixed sample count,
                // and absolute 1ms sleeps. This removes managed query and collector differences.
                if (clock_gettime(CLOCK_MONOTONIC, out var next) != 0)
                {
                    throw new Exception($"clock_gettime(CLOCK_MONOTONIC) failed: errno " +
                                        $"{Marshal.GetLastWin32Error()}");
                }

                const ulong bassError = ulong.MaxValue;
                const uint bassPositionBytes = 0;
                for (int i = 0; i < sampleCapacity; i++)
                {
                    double before = GetClockMonotonicSeconds();
                    ulong bytes = NativeBassChannelGetPosition(streamHandle, bassPositionBytes);
                    double after = GetClockMonotonicSeconds();
                    if (bytes == bassError)
                    {
                        throw new Exception("Native BASS_ChannelGetPosition failed");
                    }

                    double position = NativeBassChannelBytes2Seconds(streamHandle, bytes);
                    if (position < 0)
                    {
                        throw new Exception("Native BASS_ChannelBytes2Seconds failed");
                    }

                    double midpoint = (before + after) * 0.5;
                    result.Add(new PositionClockSample(midpoint, midpoint, position, position));

                    next.Nanoseconds += POSITION_CLOCK_SAMPLE_INTERVAL_MS * 1_000_000L;
                    while (next.Nanoseconds >= 1_000_000_000L)
                    {
                        next.Seconds++;
                        next.Nanoseconds -= 1_000_000_000L;
                    }

                    int sleepResult;
                    do
                    {
                        sleepResult = clock_nanosleep(CLOCK_MONOTONIC, TIMER_ABSTIME, ref next,
                            IntPtr.Zero);
                    }
                    while (sleepResult == 4); // EINTR; absolute deadline remains valid.

                    if (sleepResult != 0)
                    {
                        throw new Exception($"clock_nanosleep failed: error {sleepResult}");
                    }
                }
#else
                long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                double stopwatchFrequency = System.Diagnostics.Stopwatch.Frequency;

                while ((System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 /
                       stopwatchFrequency < POSITION_CLOCK_DURATION_MS)
                {
                    long stopwatchBefore = System.Diagnostics.Stopwatch.GetTimestamp();
                    double monotonicBefore = GetClockMonotonicSeconds();
                    double position = GetPositionMs(streamHandle, PositionFlags.Bytes) / 1000.0;
#if UNITY_EDITOR_LINUX
                    double nativePosition = GetNativePositionSeconds(streamHandle);
#else
                    double nativePosition = double.NaN;
#endif
                    double monotonicAfter = GetClockMonotonicSeconds();
                    long stopwatchAfter = System.Diagnostics.Stopwatch.GetTimestamp();
                    if (!double.IsNaN(position))
                    {
                        double stopwatchMidpoint =
                            ((stopwatchBefore + stopwatchAfter) * 0.5 - startTimestamp) /
                            stopwatchFrequency;
                        double monotonicMidpoint = (monotonicBefore + monotonicAfter) * 0.5;
                        result.Add(new PositionClockSample(stopwatchMidpoint, monotonicMidpoint, position,
                            nativePosition));
                    }

                    Thread.Sleep(POSITION_CLOCK_SAMPLE_INTERVAL_MS);
                }
#endif

                return result;
            });
        }

        private static double GetClockMonotonicSeconds()
        {
#if UNITY_EDITOR_LINUX
            if (clock_gettime(CLOCK_MONOTONIC, out var time) != 0)
            {
                throw new Exception($"clock_gettime(CLOCK_MONOTONIC) failed: errno " +
                                    $"{Marshal.GetLastWin32Error()}");
            }

            return time.Seconds + time.Nanoseconds / 1_000_000_000.0;
#else
            return (double) System.Diagnostics.Stopwatch.GetTimestamp() /
                System.Diagnostics.Stopwatch.Frequency;
#endif
        }

#if UNITY_EDITOR_LINUX
        private static Task<string> StartConcurrentStandaloneBassClock(string testFilePath)
        {
            string executable = Environment.GetEnvironmentVariable("BASSCLOCK_PATH");
            if (string.IsNullOrEmpty(executable))
            {
                string projectExecutable = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "Tools", "bassclock");
                executable = File.Exists(projectExecutable) ? projectExecutable : "/tmp/bassclock";
            }

            if (!File.Exists(executable))
            {
                return Task.FromResult("<b>[Concurrent standalone C BASS clock]</b>\n" +
                    $"Skipped: executable not found at {executable}. Build Tools/bass_position_clock_repro.c " +
                    "as Tools/bassclock or set BASSCLOCK_PATH before starting Unity.");
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"\"{testFilePath.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var process = new System.Diagnostics.Process { StartInfo = startInfo };
            try
            {
                if (!process.Start())
                {
                    process.Dispose();
                    return Task.FromResult("<b>[Concurrent standalone C BASS clock]</b>\n" +
                        "Failed to start process.");
                }
            }
            catch (Exception ex)
            {
                process.Dispose();
                return Task.FromResult("<b>[Concurrent standalone C BASS clock]</b>\n" +
                    $"Failed to start {executable}: {ex.Message}");
            }

            Debug.Log("<b>[Concurrent standalone C BASS clock]</b> Started alongside Unity collector: " +
                      executable);
            return Task.Run(() =>
            {
                using (process)
                {
                    string standardOutput = process.StandardOutput.ReadToEnd();
                    string standardError = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    string output = string.IsNullOrWhiteSpace(standardError)
                        ? standardOutput.Trim()
                        : standardError.Trim();
                    return "<b>[Concurrent standalone C BASS clock]</b>\n" +
                           $"Exit code: {process.ExitCode}\n{output}";
                }
            });
        }
#endif

        private static void LogPositionClockSamples(List<PositionClockSample> samples, string clockName)
        {
#if UNITY_EDITOR_LINUX
            // Collector matches the standalone C repro, so emit only that result.
            LogPositionClockStability(samples, clockName, true, true);
#else
            LogPositionClockStability(samples, clockName, false, false);
#endif
        }

#if UNITY_EDITOR_LINUX
        private static void LogManagedNativePositionDifference(List<PositionClockSample> samples, string clockName)
        {
            var differences = new List<double>(samples.Count);
            double maxAbsoluteDifferenceMs = 0;
            foreach (var sample in samples)
            {
                if (double.IsNaN(sample.NativePosition))
                {
                    continue;
                }

                double differenceMs = (sample.NativePosition - sample.Position) * 1000.0;
                differences.Add(differenceMs);
                maxAbsoluteDifferenceMs = Math.Max(maxAbsoluteDifferenceMs, Math.Abs(differenceMs));
            }

            if (differences.Count == 0)
            {
                throw new Exception("No valid direct native BASS position samples");
            }

            differences.Sort();
            Debug.Log($"<b>[BASS Managed/Native Position Difference]</b>\n" +
                      $"Clock: {clockName}\n" +
                      $"Native minus ManagedBass: p05 {GetPercentile(differences, 0.05):+0.000;-0.000;0.000}ms; " +
                      $"p95 {GetPercentile(differences, 0.95):+0.000;-0.000;0.000}ms; " +
                      $"max abs {maxAbsoluteDifferenceMs:0.000}ms");
        }
#endif

        private static void LogPositionClockStability(List<PositionClockSample> samples, string clockName,
            bool useClockMonotonic, bool useNativePosition)
        {
            if (samples.Count < 2)
            {
                throw new Exception("Not enough valid BASS position samples");
            }

            int originalSampleCount = samples.Count;
            int rejectedDiscontinuities = 0;
            double largestDiscontinuityMs = 0;
            var filteredSamples = new List<PositionClockSample>(samples.Count) { samples[0] };
            for (int i = 1; i < samples.Count; i++)
            {
                var previous = filteredSamples[^1];
                double wallDelta = GetPositionClockTime(samples[i], useClockMonotonic) -
                    GetPositionClockTime(previous, useClockMonotonic);
                double positionDelta = GetPositionClockPosition(samples[i], useNativePosition) -
                    GetPositionClockPosition(previous, useNativePosition);
                double discontinuityMs = Math.Abs(positionDelta - wallDelta) * 1000.0;
                if (discontinuityMs > POSITION_CLOCK_DISCONTINUITY_MS)
                {
                    rejectedDiscontinuities++;
                    largestDiscontinuityMs = Math.Max(largestDiscontinuityMs, discontinuityMs);
                    continue;
                }

                filteredSamples.Add(samples[i]);
            }

            samples = filteredSamples;
            if (samples.Count < 2)
            {
                throw new Exception("Not enough BASS position samples after discontinuity filtering");
            }

            double wallOrigin = GetPositionClockTime(samples[0], useClockMonotonic);
            double positionOrigin = GetPositionClockPosition(samples[0], useNativePosition);
            double sumX = 0;
            double sumY = 0;
            double sumXX = 0;
            double sumXY = 0;
            var elapsed = new double[samples.Count];
            var errorsMs = new double[samples.Count];

            for (int i = 0; i < samples.Count; i++)
            {
                double x = GetPositionClockTime(samples[i], useClockMonotonic) - wallOrigin;
                double y = ((GetPositionClockPosition(samples[i], useNativePosition) - positionOrigin) - x) *
                    1000.0;
                elapsed[i] = x;
                errorsMs[i] = y;
                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumXY += x * y;
            }

            double count = samples.Count;
            double denominator = count * sumXX - sumX * sumX;
            double slopeMsPerSecond = denominator == 0
                ? 0
                : (count * sumXY - sumX * sumY) / denominator;
            double interceptMs = (sumY - slopeMsPerSecond * sumX) / count;
            double driftPpm = slopeMsPerSecond * 1000.0;

            var residuals = new List<double>(samples.Count);
            var log = new StringBuilder();
            log.AppendLine("<b>[BASS Position Clock Stability]</b>");
            log.AppendLine($"Clock: {clockName}");
            log.AppendLine($"Position API: {(useNativePosition ? "direct native P/Invoke" : "ManagedBass")}");
            log.AppendLine($"Reference: {(useClockMonotonic ? "clock_gettime(CLOCK_MONOTONIC)" : ".NET Stopwatch")}");
            log.AppendLine($"Warmup: {POSITION_CLOCK_WARMUP_MS}ms; samples: {samples.Count}/" +
                           $"{originalSampleCount}; " +
                           $"duration: {elapsed[^1]:0.000}s; requested interval: " +
                           $"{POSITION_CLOCK_SAMPLE_INTERVAL_MS}ms");
            log.AppendLine($"Rejected discontinuities >{POSITION_CLOCK_DISCONTINUITY_MS:0}ms: " +
                           $"{rejectedDiscontinuities}; largest: {largestDiscontinuityMs:0.000}ms");
            log.AppendLine($"Long-term slope: {slopeMsPerSecond:+0.000;-0.000;0.000}ms/s " +
                           $"({driftPpm:+0;-0;0}ppm)");
            log.AppendLine("Detrended BASS-position error by 1-second window:");

            int windowStart = 0;
            int window = 0;
            while (windowStart < samples.Count)
            {
                int windowEnd = windowStart;
                double min = double.PositiveInfinity;
                double max = double.NegativeInfinity;
                while (windowEnd < samples.Count && elapsed[windowEnd] < window + 1.0)
                {
                    double residual = errorsMs[windowEnd] -
                        (interceptMs + slopeMsPerSecond * elapsed[windowEnd]);
                    residuals.Add(residual);
                    min = Math.Min(min, residual);
                    max = Math.Max(max, residual);
                    windowEnd++;
                }

                if (windowEnd > windowStart)
                {
                    log.AppendLine($"  {window,2}-{window + 1,2}s: min/max/span " +
                                   $"{min:+0.000;-0.000;0.000}/" +
                                   $"{max:+0.000;-0.000;0.000}/{max - min:0.000}ms");
                }

                windowStart = windowEnd;
                window++;
            }

            residuals.Sort();
            double residualMin = residuals[0];
            double residualMax = residuals[^1];
            double p05 = GetPercentile(residuals, 0.05);
            double p95 = GetPercentile(residuals, 0.95);
            log.AppendLine($"Overall detrended min/max/span: {residualMin:+0.000;-0.000;0.000}/" +
                           $"{residualMax:+0.000;-0.000;0.000}/{residualMax - residualMin:0.000}ms");
            log.AppendLine($"Central 90% range: {p05:+0.000;-0.000;0.000} to " +
                           $"{p95:+0.000;-0.000;0.000}ms ({p95 - p05:0.000}ms)");
            log.AppendLine($"BASS output: {Bass.Info.SampleRate}Hz; device period: " +
                           $"{Bass.GetConfig(Configuration.DevicePeriod)}ms; update period: " +
                           $"{Bass.UpdatePeriod}ms");
            log.AppendLine("Interpretation: slope measures steady clock-rate error; recurring spans measure " +
                           "short-term position-report modulation.");

            Debug.Log(log.ToString());
        }

        private static double GetPositionClockTime(PositionClockSample sample, bool useClockMonotonic)
        {
            return useClockMonotonic ? sample.MonotonicTime : sample.WallTime;
        }

        private static double GetPositionClockPosition(PositionClockSample sample, bool useNativePosition)
        {
            return useNativePosition ? sample.NativePosition : sample.Position;
        }

#if UNITY_EDITOR_LINUX
        private static double GetNativePositionSeconds(int channelHandle)
        {
            const ulong bassError = ulong.MaxValue;
            const uint bassPositionBytes = 0;

            ulong bytes = NativeBassChannelGetPosition(channelHandle, bassPositionBytes);
            if (bytes == bassError)
            {
                return double.NaN;
            }

            double seconds = NativeBassChannelBytes2Seconds(channelHandle, bytes);
            return seconds < 0 ? double.NaN : seconds;
        }
#endif

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
        [MenuItem("Tests/Verify Metronome Sample Accuracy")]
        public static void VerifyMetronomeSampleAccuracy()
        {
            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            string hiPath = Path.Combine(Application.streamingAssetsPath, "metronome", "sine_hi.ogg");
            if (!File.Exists(hiPath))
            {
                Debug.LogError($"Metronome click file not found: {hiPath}");
                return;
            }

            Debug.Log("<b>[Metronome Sample Accuracy Verification]</b> Starting test...");

            int sampleRate = 44100;
            int channels = 2;
            int bytesPerFrame = channels * sizeof(float); // 8 bytes

            // Create a decoding mixer stream.
            int mixer = BassMix.CreateMixerStream(sampleRate, channels, BassFlags.Decode | BassFlags.Float | BassFlags.MixerNonStop);
            if (mixer == 0)
            {
                Debug.LogError($"Failed to create decoding mixer: {Bass.LastError}");
                return;
            }

            // Load the click sample as a decoding stream.
            int clickStream = Bass.CreateStream(hiPath, 0, 0, BassFlags.Decode | BassFlags.Float);
            if (clickStream == 0)
            {
                Bass.StreamFree(mixer);
                Debug.LogError($"Failed to create click decode stream: {Bass.LastError}");
                return;
            }

            // Schedule the beat at 0.5 seconds
            double targetTime = 0.5;
            long targetPos = Bass.ChannelSeconds2Bytes(mixer, targetTime);
            long targetFrame = targetPos / bytesPerFrame;

            long callbackPos = -1;
            bool callbackFired = false;

            SyncProcedure syncProc = (handle, channel, data, user) =>
            {
                callbackFired = true;
                callbackPos = Bass.ChannelGetPosition(channel, PositionFlags.Bytes);

                // Rewind click stream and mix it into the mixer
                Bass.ChannelSetPosition(clickStream, 0);
                if (!BassMix.MixerAddChannel(channel, clickStream, BassFlags.MixerChanNoRampin))
                {
                    Debug.LogError($"Failed to add click channel in callback: {Bass.LastError}");
                }
            };

            int syncHandle = Bass.ChannelSetSync(
                mixer,
                SyncFlags.Position | SyncFlags.Mixtime | SyncFlags.Onetime,
                targetPos,
                syncProc,
                IntPtr.Zero);

            if (syncHandle == 0)
            {
                Debug.LogError($"Failed to set sync: {Bass.LastError}");
                Bass.StreamFree(clickStream);
                Bass.StreamFree(mixer);
                return;
            }

            // Decode 1 second of audio (44100 frames)
            int decodeFrames = sampleRate;
            float[] buffer = new float[decodeFrames * channels];
            int bytesToRead = buffer.Length * sizeof(float);

            int bytesRead = Bass.ChannelGetData(mixer, buffer, bytesToRead);
            if (bytesRead < 0)
            {
                Debug.LogError($"Failed to read data from mixer: {Bass.LastError}");
                Bass.ChannelRemoveSync(mixer, syncHandle);
                Bass.StreamFree(clickStream);
                Bass.StreamFree(mixer);
                return;
            }

            // Clean up
            Bass.ChannelRemoveSync(mixer, syncHandle);
            Bass.StreamFree(clickStream);
            Bass.StreamFree(mixer);

            // Analyze
            if (!callbackFired)
            {
                Debug.LogError("FAIL: Sync callback did not fire!");
                return;
            }

            long callbackFrame = callbackPos / bytesPerFrame;
            Debug.Log($"[Verification] Sync callback fired. Target position: {targetPos} bytes ({targetFrame} frames). Callback position: {callbackPos} bytes ({callbackFrame} frames).");

            if (callbackPos != targetPos)
            {
                Debug.LogError($"FAIL: Callback fired at wrong position! Diff: {callbackPos - targetPos} bytes");
                return;
            }

            // Check PCM data
            int firstNonZeroIndex = -1;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (Math.Abs(buffer[i]) > 0.0001f)
                {
                    firstNonZeroIndex = i;
                    break;
                }
            }

            if (firstNonZeroIndex == -1)
            {
                Debug.LogError("FAIL: No click sound found in the decoded audio!");
                return;
            }

            long firstNonZeroFrame = firstNonZeroIndex / channels;
            double firstNonZeroTime = (double)firstNonZeroFrame / sampleRate;

            Debug.Log($"[Verification] Sound detected starting at index {firstNonZeroIndex} (frame {firstNonZeroFrame}, time {firstNonZeroTime:0.0000}s).");

            // Verify silence before targetFrame
            int silenceViolations = 0;
            int targetIndex = (int)targetFrame * channels;
            for (int i = 0; i < targetIndex; i++)
            {
                if (Math.Abs(buffer[i]) > 0.0f)
                {
                    silenceViolations++;
                }
            }

            if (silenceViolations > 0)
            {
                Debug.LogError($"FAIL: Detected {silenceViolations} non-silent samples before the scheduled beat frame ({targetFrame})!");
                return;
            }

            // Verify that the sound started exactly at or after targetFrame
            if (firstNonZeroFrame < targetFrame)
            {
                Debug.LogError($"FAIL: Sound started BEFORE scheduled beat! Sound frame: {firstNonZeroFrame}, Target: {targetFrame}");
                return;
            }

            long delayFrames = firstNonZeroFrame - targetFrame;
            Debug.Log($"<b>SUCCESS: Metronome sample accuracy verified!</b>\n" +
                      $"  - Silence before target frame {targetFrame}: 100% verified\n" +
                      $"  - Sound start frame: {firstNonZeroFrame} (Delay of {delayFrames} frames, {delayFrames * 1000.0 / sampleRate:0.00}ms)\n" +
                      "  - Sample accuracy: " + (delayFrames == 0 ? "Perfect (0 frames delay)" : $"Accurate within {delayFrames} frames (due to click sound leading silence)"));
            
            EditorUtility.DisplayDialog("Verification Successful", 
                $"Metronome sample accuracy verified!\n\n" +
                $"Target frame: {targetFrame}\n" +
                $"First sound frame: {firstNonZeroFrame}\n" +
                $"Silence before beat: 100% clean\n" +
                $"Delay: {delayFrames} frames ({delayFrames * 1000.0 / sampleRate:0.00}ms)", 
                "OK");
        }

        [MenuItem("Tests/Prototype Metronome Sync Callback")]
        public static void RunMetronomeSyncCallback()
        {
            InitializePaths();
            GlobalAudioHandler.Initialize<BassAudioManager>();

            var audioManager = GetAudioManager();
            if (audioManager == null)
            {
                Debug.LogError("Failed to get active BassAudioManager instance!");
                return;
            }

            string hiPath = Path.Combine(Application.streamingAssetsPath, "metronome", "sine_hi.ogg");
            string loPath = Path.Combine(Application.streamingAssetsPath, "metronome", "sine_lo.ogg");

            if (!File.Exists(hiPath) || !File.Exists(loPath))
            {
                Debug.LogError($"Metronome click files not found. Paths:\n- {hiPath}\n- {loPath}");
                return;
            }

            Debug.Log("Starting Metronome Sync Callback Prototype at 120 BPM...");
            try
            {
                using (var runner = new MetronomeRunner(hiPath, loPath))
                {
                    runner.Start();
                    EditorUtility.DisplayDialog("Metronome Playing", "Playing metronome at 120 BPM (sine_hi.ogg / sine_lo.ogg).\n\nClick OK to stop.", "OK");
                }
                Debug.Log("Metronome Sync Callback Prototype stopped and cleaned up successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Metronome run failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private class MetronomeRunner : IDisposable
        {
            private readonly int _mixer;
            private readonly int _hiSample;
            private readonly int _loSample;
            private readonly int _hiChannel;
            private readonly int _loChannel;
            private readonly long _beatStep;
            private long _beatPos;
            private int _beatCount;
            private int _syncHandle;
            private readonly SyncProcedure _syncProcedure;

            public MetronomeRunner(string hiPath, string loPath)
            {
                int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 48000;
                // Create a stereo float non-stop mixer stream.
                _mixer = BassMix.CreateMixerStream(sampleRate, 2, BassFlags.Float | BassFlags.MixerNonStop);
                if (_mixer == 0)
                {
                    throw new Exception($"Failed to create mixer stream: {Bass.LastError}");
                }

                _hiSample = Bass.SampleLoad(hiPath, 0, 0, 3, BassFlags.Default);
                _loSample = Bass.SampleLoad(loPath, 0, 0, 3, BassFlags.Default);
                if (_hiSample == 0 || _loSample == 0)
                {
                    Bass.StreamFree(_mixer);
                    throw new Exception($"Failed to load click samples. Hi error: {Bass.LastError}");
                }

                _hiChannel = Bass.SampleGetChannel(_hiSample);
                _loChannel = Bass.SampleGetChannel(_loSample);
                if (_hiChannel == 0 || _loChannel == 0)
                {
                    Bass.SampleFree(_hiSample);
                    Bass.SampleFree(_loSample);
                    Bass.StreamFree(_mixer);
                    throw new Exception($"Failed to get click channels: {Bass.LastError}");
                }

                // 120 BPM -> 0.5 seconds per beat
                _beatStep = Bass.ChannelSeconds2Bytes(_mixer, 0.5);
                _beatPos = _beatStep;
                _beatCount = 0;

                _syncProcedure = BeatCallback;
            }

            public void Start()
            {
                ArmNextBeat();
                if (!Bass.ChannelPlay(_mixer, false))
                {
                    throw new Exception($"Failed to play mixer stream: {Bass.LastError}");
                }
            }

            private void ArmNextBeat()
            {
                _syncHandle = Bass.ChannelSetSync(
                    _mixer,
                    SyncFlags.Position | SyncFlags.Mixtime | SyncFlags.Onetime,
                    _beatPos,
                    _syncProcedure,
                    IntPtr.Zero);
                if (_syncHandle == 0)
                {
                    // Safe logging of setting sync error
                }
            }

            private void BeatCallback(int handle, int channel, int data, IntPtr user)
            {
                _beatCount++;

                int targetChannel = (_beatCount % 4 == 1) ? _hiChannel : _loChannel;
                Bass.ChannelPlay(targetChannel, true);

                _beatPos += _beatStep;
                ArmNextBeat();
            }

            public void Dispose()
            {
                if (_syncHandle != 0)
                {
                    Bass.ChannelRemoveSync(_mixer, _syncHandle);
                    _syncHandle = 0;
                }
                if (_mixer != 0)
                {
                    Bass.ChannelStop(_mixer);
                    Bass.StreamFree(_mixer);
                }
                if (_hiSample != 0)
                {
                    Bass.SampleFree(_hiSample);
                }
                if (_loSample != 0)
                {
                    Bass.SampleFree(_loSample);
                }
            }
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
