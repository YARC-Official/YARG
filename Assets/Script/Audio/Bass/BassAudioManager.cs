using System;
using System.Collections.Generic;
using System.IO;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.IO;
using YARG.Core.Logging;
using YARG.Settings;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YARG.Audio.BASS
{
    internal class StreamHandle : IDisposable
    {
        public static StreamHandle? Create(int sourceStream, double volume, int[] indices)
        {
            const BassFlags splitFlags = BassFlags.Decode | BassFlags.SplitPosition;
            const BassFlags tempoFlags = BassFlags.SampleOverrideLowestVolume | BassFlags.Decode | BassFlags.FxFreeSource;

#nullable enable
            int[]? channelMap = null;
#nullable disable
            if (indices != null)
            {
                channelMap = new int[indices.Length + 1];
                for (int i = 0; i < indices.Length; ++i)
                {
                    channelMap[i] = indices[i];
                }
                channelMap[indices.Length] = -1;
            }

            int streamSplit = BassMix.CreateSplitStream(sourceStream, splitFlags, channelMap);
            if (streamSplit == 0)
            {
                YargLogger.LogFormatError("Failed to create split stream: {0}!", Bass.LastError);
                return null;
            }

            var handle = new StreamHandle(BassFx.TempoCreate(streamSplit, tempoFlags));
            if (!Bass.ChannelSetAttribute(handle.Stream, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set channel volume: {0}!", Bass.LastError);
            }

            handle.CompressorFX = BassHelpers.AddCompressorToChannel(handle.Stream);
            if (handle.CompressorFX == 0)
            {
                YargLogger.LogError("Failed to set up compressor for split stream!");
            }
            return handle;
        }

        private bool _disposed;
        public readonly int Stream;

        public int CompressorFX;
        public int PitchFX;
        public int ReverbFX;

        public int LowEQ;
        public int MidEQ;
        public int HighEQ;

        private StreamHandle(int stream)
        {
            Stream = stream;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // FX handles are freed automatically, we only need to free the stream
                if (!Bass.StreamFree(Stream))
                {
                    YargLogger.LogFormatError("Failed to free channel stream (THIS WILL LEAK MEMORY): {0}!", Bass.LastError);
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~StreamHandle()
        {
            Dispose(false);
        }
    }

    public class BassAudioManager : AudioManager
    {
        private static readonly string[] FORMATS =
        {
            ".ogg", ".mogg", ".wav", ".mp3", ".aiff", ".opus",
        };

        public override ReadOnlySpan<string> SupportedFormats => FORMATS;

        private int _opusHandle = 0;

        public BassAudioManager()
        {
            YargLogger.LogInfo("Initializing BASS...");
            string bassPath = GetBassDirectory();
            string opusLibDirectory = Path.Combine(bassPath, "bassopus");

            _opusHandle = Bass.PluginLoad(opusLibDirectory);
            if (_opusHandle == 0) YargLogger.LogFormatError("Failed to load .opus plugin: {0}!", Bass.LastError);

            Bass.Configure(Configuration.IncludeDefaultDevice, true);

            Bass.UpdatePeriod = 5;
            Bass.DeviceBufferLength = 10;
            Bass.PlaybackBufferLength = BassHelpers.PLAYBACK_BUFFER_LENGTH;
            Bass.DeviceNonStop = true;

            PlaybackBufferLength = Bass.PlaybackBufferLength / 1000.0;

            // Affects Windows only. Forces device names to be in UTF-8 on Windows rather than ANSI.
            Bass.Configure(Configuration.UnicodeDeviceInformation, true);
            Bass.Configure(Configuration.TruePlayPosition, 0);
            Bass.Configure(Configuration.UpdateThreads, 2);
            Bass.Configure(Configuration.FloatDSP, true);

            // Undocumented BASS_CONFIG_MP3_OLDGAPS config.
            Bass.Configure((Configuration) 68, 1);

            // Disable undocumented BASS_CONFIG_DEV_TIMEOUT config. Prevents pausing audio output if a device times out.
            Bass.Configure((Configuration) 70, false);

            int deviceCount = Bass.DeviceCount;
            YargLogger.LogFormatInfo("Devices found: {0}", deviceCount);

            if (!Bass.Init(-1, 44100, DeviceInitFlags.Default | DeviceInitFlags.Latency, IntPtr.Zero))
            {
                var error = Bass.LastError;
                if (error == Errors.Already)
                    YargLogger.LogError("BASS is already initialized! An error has occurred somewhere and Unity must be restarted!");
                else
                    YargLogger.LogFormatError("Failed to initialize BASS: {0}!", error);
                return;
            }

            LoadSfx();

            YargLogger.LogInfo("BASS Successfully Initialized");
            YargLogger.LogFormatInfo("BASS: {0} - BASS.FX: {1} - BASS.Mix: {2}", Bass.Version, BassFx.Version, BassMix.Version);
            YargLogger.LogFormatInfo("Update Period: {0}ms. Device Buffer Length: {1}ms. Playback Buffer Length: {2}ms",
                Bass.UpdatePeriod, Bass.DeviceBufferLength, Bass.PlaybackBufferLength);
            YargLogger.LogFormatInfo("Current Device: {0}", Bass.GetDeviceInfo(Bass.CurrentDevice).Name);
        }

        protected override StemMixer? CreateMixer_Internal(string name, float speed)
        {
            YargLogger.LogDebug("Loading song");
            if (!CreateMixerHandle(out int handle))
            {
                return null;
            }
            return new BassStemMixer(name, this, speed, handle, 0);
        }

        protected override StemMixer CreateMixer_Internal(string name, Stream stream, float speed)
        {
            YargLogger.LogDebug("Loading song");
            if (!CreateMixerHandle(out int handle))
            {
                return null;
            }

            if (!CreateSourceStream(stream, out int sourceStream))
            {
                return null;
            }
            return new BassStemMixer(name, this, speed, handle, sourceStream);
        }

        protected override MicDevice? GetInputDevice_Internal(string name)
        {
            for (int deviceIndex = 0; Bass.RecordGetDeviceInfo(deviceIndex, out var info); deviceIndex++)
            {
                // Ignore disabled/claimed devices
                if (!info.IsEnabled || info.IsInitialized) continue;

                // Ignore loopback devices, they're potentially confusing and can cause feedback loops
                if (info.IsLoopback) continue;

                // Check if type is in whitelist
                // The "Default" device is also excluded here since we want the user to explicitly pick which microphone to use
                // if (!typeWhitelist.Contains(info.Type) || info.Name == "Default") continue;
                if (info.Name == "Default" || info.Name != name) continue;

                return CreateDevice(deviceIndex, name);
            }
            return null;
        }

        protected override List<(int id, string name)> GetAllInputDevices_Internal()
        {
            var mics = new List<(int id, string name)>();

            // Ignored for now since it causes issues on Linux, BASS must not report device info correctly there
            // TODO: allow configuring this at runtime?
            // Also put into a static variable instead of instantiating every time
            // var typeWhitelist = new List<DeviceType>()
            // {
            //     DeviceType.Headset,
            //     DeviceType.Digital,
            //     DeviceType.Line,
            //     DeviceType.Headphones,
            //     DeviceType.Microphone,
            // };

            for (int deviceIndex = 0; Bass.RecordGetDeviceInfo(deviceIndex, out var info); deviceIndex++)
            {
                // Ignore disabled/claimed devices
                if (!info.IsEnabled || info.IsInitialized) continue;

                // Ignore loopback devices, they're potentially confusing and can cause feedback loops
                if (info.IsLoopback) continue;

                // Check if type is in whitelist
                // The "Default" device is also excluded here since we want the user to explicitly pick which microphone to use
                // if (!typeWhitelist.Contains(info.Type) || info.Name == "Default") continue;
                if (info.Name == "Default") continue;

                mics.Add((deviceIndex, info.Name));
            }

            return mics;
        }

        protected override MicDevice? CreateDevice_Internal(int deviceId, string name)
        {
            var device = BassMicDevice.Create(deviceId, name);
            device?.SetMonitoringLevel(SettingsManager.Settings.VocalMonitoring.Value);
            return device;
        }

        private void LoadSfx()
        {
            YargLogger.LogInfo("Loading SFX");

            string sfxFolder = Path.Combine(Application.streamingAssetsPath, "sfx");

            foreach (string sfxFile in AudioHelpers.SfxPaths)
            {
                string sfxBase = Path.Combine(sfxFolder, sfxFile);
                foreach (string format in SupportedFormats)
                {
                    string sfxPath = sfxBase + format;
                    if (File.Exists(sfxPath))
                    {
                        var sfxSample = AudioHelpers.GetSfxFromName(sfxFile);
                        var sfx = BassSampleChannel.Create(this, sfxSample, sfxPath, 8);
                        if (sfx != null)
                        {
                            _sfxSamples[(int) sfxSample] = sfx;
                            YargLogger.LogFormatInfo("Loaded {0}", sfxFile);
                        }
                        break;
                    }  
                }
            }

            YargLogger.LogInfo("Finished loading SFX");
        }

        protected override void SetMasterVolume_Internal(double volume)
        {
#if UNITY_EDITOR
            if (EditorUtility.audioMasterMute)
                volume = 0;
#endif
            Bass.GlobalStreamVolume = (int) (10_000 * volume);
            Bass.GlobalSampleVolume = (int) (10_000 * volume);
        }

        protected override void DisposeUnmanagedResources()
        {
            YargLogger.LogInfo("Unloading BASS plugins");
            Bass.PluginFree(0);
            Bass.Free();
        }

        private static string GetBassDirectory()
        {
            string pluginDirectory = Path.Combine(Application.dataPath, "Plugins");

            // Locate windows directory
            // Checks if running on 64 bit and sets the path accordingly
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
#if UNITY_64
			pluginDirectory = Path.Combine(pluginDirectory, "x86_64");
#else
			pluginDirectory = Path.Combine(pluginDirectory, "x86");
#endif
#endif

            // Unity Editor directory, Assets/Plugins/Bass/
#if UNITY_EDITOR
            pluginDirectory = Path.Combine(pluginDirectory, "BassNative");
#endif

            // Editor paths differ to standalone paths, as the project contains platform specific folders
#if UNITY_EDITOR_WIN
            pluginDirectory = Path.Combine(pluginDirectory, "Windows/x86_64");
#elif UNITY_EDITOR_OSX
			pluginDirectory = Path.Combine(pluginDirectory, "Mac");
#elif UNITY_EDITOR_LINUX
			pluginDirectory = Path.Combine(pluginDirectory, "Linux/x86_64");
#endif

            return pluginDirectory;
        }

        private static bool CreateMixerHandle(out int mixerHandle)
        {
            mixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Default);
            if (mixerHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create mixer: {0}!", Bass.LastError);
                return false;
            }

            // Mixer processing threads (for some reason this attribute is undocumented in ManagedBass?)
            if (!Bass.ChannelSetAttribute(mixerHandle, (ChannelAttribute) 86017, 2))
            {
                YargLogger.LogFormatError("Failed to set mixer processing threads: {0}!", Bass.LastError);
                Bass.StreamFree(mixerHandle);
                return false;
            }
            return true;
        }

        internal static bool CreateSourceStream(Stream stream, out int streamHandle)
        {
            // Last flag is new BASS_SAMPLE_NOREORDER flag, which is not in the BassFlags enum,
            // as it was made as part of an update to fix <= 8 channel oggs.
            // https://www.un4seen.com/forum/?topic=20148.msg140872#msg140872
            const BassFlags streamFlags = BassFlags.Prescan | BassFlags.Decode | (BassFlags) 64;

            streamHandle = Bass.CreateStream(StreamSystem.NoBuffer, streamFlags, new BassStreamProcedures(stream));
            if (streamHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create source stream: {0}!", Bass.LastError);
                return false;
            }
            return true;
        }

        internal static void SetSpeed(float speed, int streamHandle, int reverbHandle)
        {
            // Gets relative speed from 100% (so 1.05f = 5% increase)
            float percentageSpeed = speed * 100;
            float relativeSpeed = percentageSpeed - 100;

            if (!Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Tempo, relativeSpeed) ||
                !Bass.ChannelSetAttribute(reverbHandle, ChannelAttribute.Tempo, relativeSpeed))
            {
                YargLogger.LogFormatError("Failed to set channel speed: {0}!", Bass.LastError);
            }
        }

        internal static bool CreateSplitStreams(int sourceStream, double volume, int[] channelMap, out StreamHandle? streamHandles, out StreamHandle? reverbHandles)
        {
            streamHandles = StreamHandle.Create(sourceStream, volume, channelMap);
            if (streamHandles == null)
            {
                reverbHandles = null;
                return false;
            }

            reverbHandles = StreamHandle.Create(sourceStream, 0, channelMap);
            if (reverbHandles == null)
            {
                streamHandles.Dispose();
                return false;
            }
            return true;
        }

        internal static PitchShiftParametersStruct SetPitchParams(SongStem stem, float speed, StreamHandle streamHandles, StreamHandle reverbHandles)
        {
            PitchShiftParametersStruct pitchParams = new(1, 0, WHAMMY_FFT_DEFAULT, WHAMMY_OVERSAMPLE_DEFAULT);
            // Set whammy pitch bending if enabled
            if (UseWhammyFx && AudioHelpers.PitchBendAllowedStems.Contains(stem))
            {
                // Setting the FFT size causes a crash in BASS_FX :/
                // _pitchParams.FFTSize = _manager.Options.WhammyFFTSize;
                pitchParams.OversampleFactor = WhammyOversampleFactor;
                if (SetupPitchBend(pitchParams, streamHandles))
                {
                    SetupPitchBend(pitchParams, reverbHandles);
                }
            }

            if (!Mathf.Approximately(speed, 1f))
            {
                speed = (float) Math.Round(Math.Clamp(speed, 0.05, 50), 2);
                SetSpeed(speed, streamHandles.Stream, reverbHandles.Stream);
                if (IsChipmunkSpeedup)
                {
                    SetChipmunking(speed, streamHandles.Stream, reverbHandles.Stream);
                }
            }
            return pitchParams;
        }

        internal static void SetChipmunking(float speed, int streamHandle, int reverbHandle)
        {
            double accurateSemitoneShift = 12 * Math.Log(speed, 2);
            float finalSemitoneShift = (float) Math.Clamp(accurateSemitoneShift, -60, 60);
            if (!Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Pitch, finalSemitoneShift) ||
                !Bass.ChannelSetAttribute(reverbHandle, ChannelAttribute.Pitch, finalSemitoneShift))
            {
                YargLogger.LogFormatError("Failed to set channel pitch: {0}!", Bass.LastError);
            }
        }

        internal static bool SetupPitchBend(in PitchShiftParametersStruct pitchParams, StreamHandle handles)
        {
            handles.CompressorFX = BassHelpers.FXAddParameters(handles.Stream, EffectType.PitchShift, pitchParams);
            if (handles.CompressorFX == 0)
            {
                YargLogger.LogError("Failed to set up pitch bend for main stream!");
                return false;
            }
            return true;
        }

        internal static double GetLengthInSeconds(int handle)
        {
            long length = Bass.ChannelGetLength(handle);
            if (length < 0)
            {
                YargLogger.LogFormatError("Failed to get channel length in bytes: {0}!", Bass.LastError);
                return -1;
            }

            double seconds = Bass.ChannelBytes2Seconds(handle, length);
            if (seconds < 0)
            {
                YargLogger.LogFormatError("Failed to get channel length in seconds: {0}!", Bass.LastError);
                return -1;
            }

            return seconds;
        }
    }
}