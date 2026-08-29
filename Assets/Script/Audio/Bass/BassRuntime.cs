using System;
using System.IO;
using ManagedBass;
using ManagedBass.Asio;
using ManagedBass.Mix;
using ManagedBass.Wasapi;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Starts BASS, loads its plugins, and releases them when audio shuts down.
    /// </summary>
    internal sealed class BassRuntime : IDisposable
    {
        private const Configuration BASS_CONFIG_MIXER_POSEX = (Configuration) 0x10602;
        private const int BASS_MIXER_POSEX_MILLISECONDS = 10_000;

        internal BassRuntime()
        {
            YargLogger.LogInfo("Initializing BASS...");
            string bassPath = GetBassDirectory();
            string opusLibDirectory = Path.Combine(bassPath, "bassopus");

            int opusHandle = Bass.PluginLoad(opusLibDirectory);
            if (opusHandle == 0)
            {
                YargLogger.LogFormatError("Failed to load .opus plugin: {0}!", Bass.LastError);
            }

            Bass.Configure(Configuration.IncludeDefaultDevice, true);

            Bass.UpdatePeriod = 5;
            Bass.DeviceNonStop = true;
            Bass.AsyncFileBufferLength = 65536;

            int devPeriod = Bass.GetConfig(Configuration.DevicePeriod);
            Bass.DeviceBufferLength = 2 * devPeriod;
            Bass.UnicodeDeviceInformation = true;
            Bass.FloatingPointDSP = true;
            Bass.VistaTruePlayPosition = false;
            Bass.UpdateThreads = GlobalAudioHandler.MAX_THREADS;

            Bass.Configure((Configuration) 68, 1);
            Bass.Configure((Configuration) 70, false);
            _ = BassMix.Version;
            if (!Bass.Configure(BASS_CONFIG_MIXER_POSEX, BASS_MIXER_POSEX_MILLISECONDS))
            {
                YargLogger.LogFormatError("Failed to configure BASS mixer position history: {0}", Bass.LastError);
            }

            int deviceCount = Bass.DeviceCount;
            YargLogger.LogFormatInfo("Devices found: {0}", deviceCount);

#if UNITY_EDITOR
            for (int deviceIndex = 0; Bass.RecordGetDeviceInfo(deviceIndex, out var recordInfo); deviceIndex++)
            {
                if (!recordInfo.IsInitialized)
                {
                    continue;
                }

                Bass.CurrentRecordingDevice = deviceIndex;
                if (!Bass.RecordFree())
                {
                    YargLogger.LogWarning(
                        $"Failed to free stale BASS recording device [{deviceIndex}] '{recordInfo.Name}': " +
                        $"{Bass.LastError}");
                }
            }

            if (Bass.CurrentDevice != -1)
            {
                YargLogger.LogInfo("BASS already initialized, cleaning up first");
                try
                {
                    Bass.Free();
                    Bass.PluginFree(0);
                    BassOutputDevice.ResetForEditor();
                }
                catch (Exception ex)
                {
                    YargLogger.LogWarning(
                        $"Exception encountered during BASS pre-initialization cleanup: {ex.Message}");
                }
            }
#endif

#if UNITY_EDITOR_WIN
            int asioDeviceCount = 0;
            try
            {
                asioDeviceCount = BassAsio.DeviceCount;
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Exception reading ASIO devices during cleanup: {ex.Message}");
            }

            for (int i = 0; i < asioDeviceCount; i++)
            {
                try
                {
                    BassAsio.CurrentDevice = i;
                    BassAsio.Free();
                }
                catch (BassException exception) when (exception.ErrorCode == Errors.Init)
                {
                }
                catch (Exception ex)
                {
                    YargLogger.LogWarning($"Exception freeing ASIO device {i}: {ex.Message}");
                }
            }

            for (int i = 0; ; i++)
            {
                bool found;
                try
                {
                    found = BassWasapi.GetDeviceInfo(i, out var wasapiInfo);
                    if (!found)
                    {
                        break;
                    }

                    if (wasapiInfo.IsInitialized)
                    {
                        BassWasapi.CurrentDevice = i;
                        BassWasapi.Free();
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogWarning($"Exception freeing WASAPI device {i}: {ex.Message}");
                    break;
                }
            }
#endif
        }

        public void Dispose()
        {
            YargLogger.LogInfo("Unloading BASS plugins");
            Bass.Free();
            Bass.PluginFree(0);
        }

        private static string GetBassDirectory()
        {
            string pluginDirectory = Path.Combine(Application.dataPath, "Plugins");

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
#if UNITY_64
			pluginDirectory = Path.Combine(pluginDirectory, "x86_64");
#else
			pluginDirectory = Path.Combine(pluginDirectory, "x86");
#endif
#endif

#if UNITY_EDITOR
            pluginDirectory = Path.Combine(pluginDirectory, "BassNative");
#endif

#if UNITY_EDITOR_WIN
            pluginDirectory = Path.Combine(pluginDirectory, "Windows/x86_64");
#elif UNITY_EDITOR_OSX
			pluginDirectory = Path.Combine(pluginDirectory, "Mac");
#elif UNITY_EDITOR_LINUX
            pluginDirectory = Path.Combine(pluginDirectory, "Linux/x86_64");
#endif

            return pluginDirectory;
        }
    }
}
