using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ManagedBass;
using UnityEditor;
using UnityEngine;
using YARG.Audio.BASS;
using YARG.Core.Audio;
using YARG.Helpers;
using YARG.Playback;
using YARG.Settings;
using YARG.Settings.Types;

namespace Editor
{
    public static class VerifyAudioDelays
    {
        private const int TEST_BUFFER_MS = 150;
        private const double EPSILON = 0.0001;

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

                double audibleSyncLatency = mixer.GetAudibleSyncLatency();
                double commandLatency = mixer.GetCommandLatency();
                double startLatency = mixer.GetStartLatency();
                double pausedResumeLatency = mixer.GetPausedResumeLatency();

                Debug.Log($"Calculated parameters: Device Latency: {deviceLatency * 1000:0.0}ms, " +
                          $"Configured Buffer Latency: {configuredLatency * 1000:0.0}ms, " +
                          $"Command Update Midpoint: {GetExpectedCommandUpdateLatency() * 1000:0.0}ms, " +
                          $"Device Period Midpoint: {GetExpectedDevicePeriodMidpoint() * 1000:0.0}ms");
                Debug.Log($"Mixer reported latencies: AudibleSync: {audibleSyncLatency * 1000:0.0}ms, " +
                          $"Command: {commandLatency * 1000:0.0}ms, Start: {startLatency * 1000:0.0}ms, " +
                          $"PausedResume: {pausedResumeLatency * 1000:0.0}ms");

                if (Math.Abs(audibleSyncLatency) > EPSILON)
                {
                    throw new Exception($"AudibleSyncLatency did not match! Actual: {audibleSyncLatency * 1000:0.0}ms ({audibleSyncLatency}s), Expected: 0.0ms (0s)");
                }

                double expectedCommand = configuredLatency + deviceLatency;
                if (Math.Abs(commandLatency - expectedCommand) > EPSILON)
                {
                    throw new Exception($"CommandLatency did not match! Actual: {commandLatency * 1000:0.0}ms ({commandLatency}s), Expected: {expectedCommand * 1000:0.0}ms ({expectedCommand}s)");
                }

                double expectedStart = GetExpectedStartLatency(deviceLatency);
                if (Math.Abs(startLatency - expectedStart) > EPSILON)
                {
                    throw new Exception($"StartLatency did not match! Actual: {startLatency * 1000:0.0}ms ({startLatency}s), Expected: {expectedStart * 1000:0.0}ms ({expectedStart}s)");
                }

                double expectedPausedResume = expectedStart + GetExpectedDevicePeriodMidpoint();
                if (Math.Abs(pausedResumeLatency - expectedPausedResume) > EPSILON)
                {
                    throw new Exception($"PausedResumeLatency did not match! Actual: {pausedResumeLatency * 1000:0.0}ms ({pausedResumeLatency}s), Expected: {expectedPausedResume * 1000:0.0}ms ({expectedPausedResume}s)");
                }

                VerifySongRunnerResumeLatencyHelpers(0, expectedStart, expectedPausedResume, EPSILON);
            }
            finally
            {
                mixer.Dispose();
            }
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
                double deviceLatency = GlobalAudioHandler.PlaybackLatency;
                double audibleSync = mixer.GetAudibleSyncLatency() * 1000.0;
                double start = mixer.GetStartLatency() * 1000.0;
                double pausedResume = mixer.GetPausedResumeLatency() * 1000.0;
                double expectedSongRunnerSeek = Math.Max(Math.Max(audibleSync, start), pausedResume);

                Debug.Log($"<b>[Real Seek Latency]</b>\n" +
                          $"  - Measured Total Elapsed (to {syncTarget * 1000:0.0}ms mark): {totalElapsedMs}ms\n" +
                          $"  - Calculated Actual Seek Latency: <b>{actualSeekLatencyMs}ms</b>\n" +
                          $"  - Expected SongRunner Seek Latency: {expectedSongRunnerSeek:0.0}ms\n" +
                          $"  - Reported AudibleSync Latency: {audibleSync:0.0}ms\n" +
                          $"  - Reported Device Latency: {deviceLatency:0.0}ms\n" +
                          $"  - Reported Start Latency: {start:0.0}ms\n" +
                          $"  - Reported Paused Resume Latency: {pausedResume:0.0}ms\n" +
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

            var mixer = createMixerMethod.Invoke(audioManager, new object[] { "TestMixer", 1.0f, 1.0, false, false }) as BassStemMixer;
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

        private static double GetExpectedStartLatency(double deviceLatency)
        {
            return deviceLatency + GetExpectedCommandUpdateLatency() + Math.Max(0, Bass.DeviceBufferLength) / 1000.0;
        }

        private static double GetExpectedCommandUpdateLatency()
        {
            return Math.Max(0, Bass.UpdatePeriod) / 2000.0;
        }

        private static double GetExpectedDevicePeriodMidpoint()
        {
            return Math.Max(0, Bass.GetConfig(Configuration.DevicePeriod)) / 2000.0;
        }

        private static void VerifySongRunnerResumeLatencyHelpers(
            double audibleSyncLatency,
            double startLatency,
            double pausedResumeLatency,
            double epsilon)
        {
            var resumeMethod = typeof(SongRunner).GetMethod("GetResumeLatency", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(double), typeof(double), typeof(double) }, null);
            if (resumeMethod == null)
            {
                throw new Exception("Could not find SongRunner resume latency helper method");
            }

            double expected = Math.Max(Math.Max(audibleSyncLatency, startLatency), pausedResumeLatency);
            double actual = (double) resumeMethod.Invoke(null, new object[] { audibleSyncLatency, startLatency, pausedResumeLatency });
            if (Math.Abs(actual - expected) > epsilon)
            {
                throw new Exception($"GetResumeLatency did not match! Actual: {actual * 1000:0.0}ms ({actual}s), Expected: {expected * 1000:0.0}ms ({expected}s)");
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
