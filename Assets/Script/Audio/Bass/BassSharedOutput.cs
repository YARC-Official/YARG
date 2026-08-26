#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Outputs songs, sound effects, and microphone monitors to the system's standard shared audio device
    ///     using BASS's internal playback mixer and output buffering.
    /// </summary>
    internal sealed class BassSharedOutput : BassOutput
    {
        private const int DEFAULT_SAMPLE_RATE = 44_100;
        private const int DEFAULT_CHANNEL_COUNT = 2;
        private const int MINIMUM_READ_BLOCK_FRAMES = 128;

        private readonly BassMicManager _microphones;

        public override int HeardLatencyMilliseconds =>
            (int) Math.Round(SongPlaybackStartDelay * 1000.0);
        internal override int EndpointDelayFrames => SampleRate > 0
            ? checked((int) Math.Round(SongPlaybackStartDelay * SampleRate))
            : 0;
        internal override double SongPlaybackStartDelay => BassLatencyProvider.StartupLatency;

        private BassSharedOutput(string name, BassOutputDevice device, BassAudioRouter router)
            : base(name, device)
        {
            _microphones = new BassMicManager(router);
        }

        public static BassSharedOutput? Find(string name, BassAudioRouter router)
        {
            for (int deviceIndex = 0; Bass.GetDeviceInfo(deviceIndex, out var info); deviceIndex++)
            {
                if (!info.IsEnabled || info.IsLoopback || info.Name != name)
                {
                    continue;
                }

                var device = BassOutputDevice.Create(deviceIndex, name);
                return device == null ? null : new BassSharedOutput(name, device, router);
            }

            return null;
        }

        public static List<(int id, string name)> GetDevices()
        {
            var devices = new List<(int id, string name)>();
            for (int deviceIndex = 1; Bass.GetDeviceInfo(deviceIndex, out var info); deviceIndex++)
            {
                if (info.IsEnabled && !info.IsLoopback)
                {
                    devices.Add((deviceIndex, info.Name));
                }
            }

            return devices;
        }

        public override bool Start()
        {
            Device.Use();
            if (!Bass.Start())
            {
                YargLogger.LogFormatError("Failed to start BASS output device: {0}", Bass.LastError);
                return false;
            }

            var info = Bass.Info;
            var sampleRate = info.SampleRate > 0 ? info.SampleRate : DEFAULT_SAMPLE_RATE;
            var channelCount = info.SpeakerCount > 0 ? info.SpeakerCount : DEFAULT_CHANNEL_COUNT;
            if (!CreateOutputGraph(sampleRate, channelCount, BassFlags.Default,
                    MINIMUM_READ_BLOCK_FRAMES))
            {
                return false;
            }

            if (!Bass.ChannelSetAttribute(OutputMixerHandle, ChannelAttribute.Buffer, 0))
            {
                YargLogger.LogFormatError("Failed to disable output mixer buffering: {0}", Bass.LastError);
                FreeOutputGraph();
                return false;
            }

            if (!Bass.ChannelPlay(OutputMixerHandle))
            {
                YargLogger.LogFormatError("Failed to start output mixer: {0}", Bass.LastError);
                FreeOutputGraph();
                return false;
            }

            return true;
        }

        public override IReadOnlyList<InputDeviceInfo> GetInputs() => _microphones.GetAllDevices();

        public override MicDevice? CreateInput(InputDeviceInfo input) => _microphones.CreateMic(input);

        protected override void StopOutput()
        {
            if (OutputMixerHandle != 0)
            {
                Bass.ChannelStop(OutputMixerHandle);
            }
            FreeOutputGraph();
        }
    }
}
