#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Menu.Persistent;
using YARG.Settings;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YARG.Audio.BASS
{
    public class BassAudioManager : AudioManager
    {
        private const string DEFAULT_OUTPUT_DEVICE = "Default";

        private static readonly string[] FORMATS =
        {
            ".ogg",
            ".mogg",
            ".wav",
            ".mp3",
            ".aiff",
            ".opus",
        };

        private readonly BassAudioRouter _router = new();
        private readonly BassOutputFactory _outputFactory;
        private readonly BassRuntime       _runtime;
        private readonly BassSampleLoader  _sampleLoader;
        private          BassOutput?       _output;

        public BassAudioManager()
        {
            _runtime = new BassRuntime();
            _sampleLoader = new BassSampleLoader(_router, FORMATS);
            _outputFactory = new BassOutputFactory(_router);

            string startupDevice = SettingsManager.OutputDeviceAtStartup;
            bool result = SetOutputDeviceInternal(startupDevice);

            if (!result)
            {
                var error = Bass.LastError;
                YargLogger.LogFormatError("BASS Initialization Failure: Failed to set default output device: {0}",
                    error);

#if UNITY_STANDALONE_LINUX
                if (error == Errors.Driver)
                {
                    YargLogger.LogError("Failed to set default output device. This is likely due to a missing ALSA plugin. Install pipewire-alsa or equivalent.");
                    ToastManager.ToastError("Failed to initialize audio device. Make sure you have pipewire-alsa or equivalent installed.");
                }
#endif
                return;
            }

            var info = Bass.Info;
            UpdatePlaybackLatency();
            MinimumBufferLength = info.MinBufferLength + Bass.UpdatePeriod;
            MaximumBufferLength = 5000;

            YargLogger.LogInfo("BASS Successfully Initialized");
            YargLogger.LogFormatInfo("BASS: {0} - BASS.FX: {1} - BASS.Mix: {2}", Bass.Version, BassFx.Version,
                BassMix.Version);
            YargLogger.LogFormatInfo(
                "Update Period: {0}ms. Device Buffer Length: {1}ms. Playback Buffer Length: {2}ms. Device Playback Latency: {3}ms",
                Bass.UpdatePeriod, Bass.DeviceBufferLength, Bass.PlaybackBufferLength, PlaybackLatency);

            YargLogger.LogFormatInfo("Current Device: {0}", _output?.Name);
        }

        protected override ReadOnlySpan<string> SupportedFormats => FORMATS;

        protected override bool SetOutputDevice(string name) => SetOutputDeviceInternal(name);

        protected override OutputBufferInfo? GetOutputBufferInfo() => _output?.GetBufferInfo();

        protected override bool OpenOutputControlPanel() => _output?.OpenControlPanel() ?? false;

        protected override void Update()
        {
            var underruns = _router.TakeReadAheadUnderruns();
            if (underruns == 0)
            {
                return;
            }

            if (!SettingsManager.SettingContainer.IsInitialized ||
                !SettingsManager.Settings.AutomaticPlaybackBuffer.Value)
            {
                return;
            }

            var setting = SettingsManager.Settings.PlaybackBufferLength;
            if (setting.Value == setting.Max)
            {
                return;
            }

            setting.Value++;
        }

        protected override AudioOutputMode GetOutputMode(string name) => _outputFactory.ModeFor(name);

        protected override List<(int id, string name)> GetAllOutputDevices() => _outputFactory.GetAllDevices();

        protected override bool ReinitializeOutput() => _output != null && ApplyOutputDevice(_output.Name);

        protected override StemMixer? CreateMixer(string name, float speed, double mixerVolume, bool clampStemVolume,
            bool normalize)
        {
            if (GlobalAudioHandler.LogMixerStatus)
            {
                YargLogger.LogDebug("Loading song");
            }

            var song = BassSong.Create(name, this, speed, mixerVolume, clampStemVolume, normalize,
                CreateOutputChannel(SettingsManager.Settings?.OutputChannelDefault.Value ?? 0));
            if (song == null)
            {
                return null;
            }

            if (!_router.AddSong(song))
            {
                song.Dispose();
                return null;
            }

            return song;
        }

        protected override List<InputDeviceInfo> GetAllInputDevices() =>
            _output == null ? new List<InputDeviceInfo>() : new List<InputDeviceInfo>(_output.GetInputs());

        protected override MicDevice? CreateInputDevice(InputDeviceInfo device) => _output?.CreateInput(device);

        protected override OutputChannel? CreateOutputChannel(int channelId) => BassOutputChannel.Create(channelId);

        protected override int GetOutputChannelCount() => _output?.ChannelCount ?? BassHelpers.GetOutputChannelCount();

        protected override void SetMasterVolume(double volume)
        {
#if UNITY_EDITOR
            if (EditorUtility.audioMasterMute)
            {
                volume = 0;
            }
#endif
            Bass.GlobalStreamVolume = (int) (10_000 * volume);
            Bass.GlobalSampleVolume = (int) (10_000 * volume);
            _router.SetVolume(volume);
        }

        protected override void SetBufferLength_Internal(int length)
        {
        }

        public override void LoadVenueSample(string name, byte[] sampleData, OutputChannel? outputChannel = null)
        {
            if (VenueSamples.TryGetValue(name, out var existing))
            {
                existing.Dispose();
                VenueSamples.Remove(name);
            }

            var sample = _sampleLoader.CreateVenueSample(name, sampleData, outputChannel);
            if (sample != null)
            {
                VenueSamples[name] = sample;
            }
        }

        public override void ClearVenueSamples() => UnloadVenueSamples();

        protected override void PlayMetronomeSoundEffectToChannel(MetronomeSample sample, MetronomePitch pitch, int channelId)
        {
            if ((int) sample < 0 || (int) sample >= MetronomeSamples.Length)
            {
                return;
            }

            var metronomeChannel = MetronomeSamples[(int) sample];
            if (metronomeChannel == null)
            {
                return;
            }

            int voice = metronomeChannel.CreateStream(pitch);
            if (voice == 0)
            {
                return;
            }

            double volume = GlobalAudioHandler.GetTrueVolume(SongStem.Metronome) * AudioHelpers.MetronomeSamples[(int) sample].Volume;
            if (!Bass.ChannelSetAttribute(voice, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set audition metronome sample volume: {0}!", Bass.LastError);
            }

            var outputChannel = CreateOutputChannel(channelId);
            if (!_router.PlaySample(voice, outputChannel))
            {
                Bass.StreamFree(voice);
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            _router.Dispose();
            _output?.Dispose();
            _output = null;
            _outputFactory.Dispose();
            _runtime.Dispose();
        }

        private void UpdatePlaybackLatency() => PlaybackLatency = _router.HeardLatencyMilliseconds;

        private bool SetOutputDeviceInternal(string name)
        {
            if (_output?.Name == name)
            {
                return true;
            }

            return ApplyOutputDevice(name) || RestoreDefaultOutput(name);
        }

        private bool RestoreDefaultOutput(string failedOutput)
        {
            if (failedOutput == DEFAULT_OUTPUT_DEVICE)
            {
                return false;
            }

            YargLogger.LogFormatError("Failed to initialize audio output '{0}', falling back to Default", failedOutput);
            bool restored = ApplyOutputDevice(DEFAULT_OUTPUT_DEVICE);
            if (restored && SettingsManager.SettingContainer.IsInitialized)
            {
                SettingsManager.Settings.OutputDevice.SetValueWithoutNotify(DEFAULT_OUTPUT_DEVICE);
                ToastManager.ToastError($"Failed to initialize {failedOutput}. Using Default audio output.");
            }

            return restored;
        }

        private bool ApplyOutputDevice(string name)
        {
            var venueSamples = CaptureVenueSamples();
            var previous = _output;
            if (previous != null)
            {
                Disconnect(previous);
            }

            var nextOutput = _outputFactory.Create(name);
            if (nextOutput == null || !nextOutput.Start() || !Connect(nextOutput))
            {
                nextOutput?.Dispose();
                if (previous != null)
                {
                    RestorePreviousOutput(previous, venueSamples, name);
                }

                return false;
            }

            if (previous != null)
            {
                previous.RestartRequested -= OnOutputRestartRequested;
                previous.Dispose();
            }

            _output = nextOutput;
            nextOutput.Device.Use();
            nextOutput.RestartRequested += OnOutputRestartRequested;

            UpdatePlaybackLatency();

            YargLogger.LogFormatInfo("Current audio output: {0}", name);

            ReloadSamples(venueSamples);
            return true;
        }

        private List<(string Name, byte[] Data, OutputChannel? OutputChannel)> CaptureVenueSamples()
        {
            var venueSamples = new List<(string Name, byte[] Data, OutputChannel? OutputChannel)>();
            foreach (var sample in VenueSamples.Values)
            {
                if (sample is BassVenueSampleChannel bassSample)
                {
                    venueSamples.Add((bassSample.SampleName, bassSample.SampleData, bassSample.OutputChannel));
                }
            }

            return venueSamples;
        }

        private void Disconnect(BassOutput output)
        {
            output.Device.Use();
            UnloadSamples();
            _router.Disconnect();
            output.Stop();
        }

        private bool Connect(BassOutput output)
        {
            output.Device.Use();
            MoveActiveMixersTo(output.Device);
            if (!_router.Connect(output, output.Device.DeviceId))
            {
                return false;
            }

            output.Device.Use();
            return true;
        }

        private void RestorePreviousOutput(BassOutput previous,
            List<(string Name, byte[] Data, OutputChannel? OutputChannel)> venueSamples, string failedOutput)
        {
            YargLogger.LogError($"Failed to start audio output '{failedOutput}', restoring '{previous.Name}'");

            if (!previous.Start())
            {
                YargLogger.LogFormatError("Failed to reactivate audio output '{0}'", previous.Name);
                previous.RestartRequested -= OnOutputRestartRequested;
                previous.Dispose();
                _output = null;
                return;
            }

            previous.Device.Use();
            MoveActiveMixersTo(previous.Device);
            if (!_router.Connect(previous, previous.Device.DeviceId))
            {
                YargLogger.LogFormatError("Failed to restore audio output '{0}'", previous.Name);
                previous.RestartRequested -= OnOutputRestartRequested;
                previous.Dispose();
                _output = null;
                return;
            }

            UpdatePlaybackLatency();
            ReloadSamples(venueSamples);
        }

        private void OnOutputRestartRequested()
        {
            if (_output == null)
            {
                return;
            }

            if (!ReinitializeOutput())
            {
                YargLogger.LogError("Failed to reinitialize audio after driver settings changed");
                ToastManager.ToastError("Failed to reinitialize audio after driver settings changed.");
                return;
            }

            if (SettingsManager.SettingContainer.IsInitialized)
            {
                SettingsManager.Settings.RefreshAsioBufferSize();
            }
        }

        private void ReloadSamples(List<(string Name, byte[] Data, OutputChannel? OutputChannel)> venueSamples)
        {
            YargLogger.LogInfo("Loading SFX");
            SfxSamples = _sampleLoader.LoadSfx();
            YargLogger.LogInfo("Finished loading SFX");

            YargLogger.LogInfo("Loading Drum SFX");
            DrumSfxSamples = _sampleLoader.LoadDrumSfx();
            YargLogger.LogInfo("Finished loading Drum SFX");

            YargLogger.LogInfo("Loading VOX");
            VoxSamples = _sampleLoader.LoadVox();
            YargLogger.LogInfo("Finished loading VOX");

            YargLogger.LogInfo("Loading Metronome");
            MetronomeSamples = _sampleLoader.LoadMetronome();
            YargLogger.LogInfo("Finished loading Metronome");

            foreach (var sample in venueSamples)
            {
                LoadVenueSample(sample.Name, sample.Data, sample.OutputChannel);
            }
        }

        private void UnloadSamples()
        {
            DisposeSamples(SfxSamples);
            SfxSamples = new SampleChannel[AudioHelpers.SfxSamples.Count];

            DisposeSamples(DrumSfxSamples);
            DrumSfxSamples = new DrumSampleChannel[AudioHelpers.DrumSamples.Count];

            DisposeSamples(VoxSamples);
            VoxSamples = new VoxSampleChannel[AudioHelpers.VoxSamples.Count];

            DisposeSamples(MetronomeSamples);
            MetronomeSamples = new MetronomeSampleChannel[AudioHelpers.MetronomeSamples.Count];

            UnloadVenueSamples();
        }

        private static void DisposeSamples<T>(IEnumerable<T?> samples) where T : class, IDisposable
        {
            foreach (var sample in samples)
            {
                sample?.Dispose();
            }
        }

        private void UnloadVenueSamples()
        {
            foreach (var sample in VenueSamples.Values)
            {
                sample.Stop();
                sample.Dispose();
            }

            VenueSamples.Clear();
        }
    }
}
