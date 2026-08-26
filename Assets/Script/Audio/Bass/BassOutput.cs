#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Base class representing an audio output destination (shared system output or ASIO device).
    ///     Coordinates song connections, live sound effect mixing, microphone monitor streams, and latency calculations.
    /// </summary>
    internal abstract class BassOutput : IDisposable
    {
        private readonly HashSet<int> _monitors = new();
        private readonly object       _songLock = new();
        private          bool         _stopping;

        protected BassOutput(string name, BassOutputDevice device)
        {
            Name = name;
            Device = device;
        }

        public    string           Name       { get; }
        public    BassOutputDevice Device     { get; }
        protected bool             IsDisposed { get; private set; }

        internal int SampleRate { get; private set; }

        internal int ChannelCount { get; private set; }

        internal int MinimumBlockFrames { get; private set; }

        internal int OutputMixerHandle { get; private set; }

        public abstract   int    HeardLatencyMilliseconds { get; }
        internal abstract int    EndpointDelayFrames      { get; }
        internal abstract double SongPlaybackStartDelay   { get; }

        // When true (WASAPI Exclusive / ASIO), position tracking is calculated from hardware DAC pull callbacks
        // instead of BASS's software mixer timeline.
        internal virtual bool UsesIndependentClock => false;

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            foreach (int sourceHandle in new List<int>(_monitors))
            {
                DetachMonitor(sourceHandle);
            }

            Stop();
            DisposeResources();
            Device.Dispose();
        }

        public abstract bool Start();
        public abstract IReadOnlyList<InputDeviceInfo> GetInputs();
        public abstract MicDevice? CreateInput(InputDeviceInfo input);
        public virtual OutputBufferInfo? GetBufferInfo() => null;
        public virtual bool OpenControlPanel() => false;
        public event Action? RestartRequested;

        protected void RequestRestart() => RestartRequested?.Invoke();

        protected bool CreateOutputGraph(int sampleRate, int channelCount, BassFlags outputFlags,
            int minimumBlockFrames)
        {
            SampleRate = sampleRate;
            ChannelCount = channelCount;
            MinimumBlockFrames = minimumBlockFrames;
            _stopping = false;

            OutputMixerHandle = BassMix.CreateMixerStream(sampleRate, channelCount,
                BassFlags.Float | BassFlags.MixerNonStop | outputFlags);
            if (OutputMixerHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create output mixer: {0}", Bass.LastError);
                return false;
            }

            return true;
        }

        protected void FreeOutputGraph()
        {
            if (OutputMixerHandle != 0)
            {
                Bass.StreamFree(OutputMixerHandle);
                OutputMixerHandle = 0;
            }

            SampleRate = 0;
            ChannelCount = 0;
            MinimumBlockFrames = 0;
        }

        internal bool AttachSong(int volumeMixerHandle)
        {
            lock (_songLock)
            {
                if (_stopping || OutputMixerHandle == 0)
                {
                    return false;
                }

                Device.Use();
                var flags = BassFlags.MixerChanNoRampin | BassFlags.MixerChanPause;
                if (!BassMix.MixerAddChannel(OutputMixerHandle, volumeMixerHandle, flags))
                {
                    YargLogger.LogFormatError("Failed to add song to output mixer: {0}", Bass.LastError);
                    return false;
                }

                return true;
            }
        }

        public bool PlaySample(int sourceHandle, OutputChannel? outputChannel) =>
            AddToOutputMixer(sourceHandle, outputChannel, BassFlags.AutoFree | BassFlags.MixerChanNoRampin);

        public void SetSampleOutputChannel(int sourceHandle, OutputChannel? outputChannel)
        {
            var flags = outputChannel is BassOutputChannel bassOutputChannel
                ? bassOutputChannel.Flags
                : BassFlags.Default;
            BassMix.ChannelFlags(sourceHandle, flags, BassFlags.SpeakerFront);
        }

        public bool AttachMonitor(int sourceHandle, double volume)
        {
            bool isConnected = OutputMixerHandle != 0 && BassMix.ChannelGetMixer(sourceHandle) == OutputMixerHandle;
            if (isConnected)
            {
                _monitors.Add(sourceHandle);
                return SetMonitorVolume(sourceHandle, volume);
            }

            _monitors.Remove(sourceHandle);
            if (!SetMonitorVolume(sourceHandle, volume) || !AddToOutputMixer(sourceHandle, null))
            {
                return false;
            }

            _monitors.Add(sourceHandle);
            return true;
        }

        public void DetachMonitor(int sourceHandle)
        {
            if (!_monitors.Remove(sourceHandle))
            {
                return;
            }

            if (!BassMix.MixerRemoveChannel(sourceHandle) && Bass.LastError != Errors.Handle)
            {
                YargLogger.LogFormatError("Failed to remove source from output mixer: {0}", Bass.LastError);
            }
        }

        public bool SetMonitorVolume(int sourceHandle, double volume)
        {
            double effective = volume * 1.0;
            if (Bass.ChannelSetAttribute(sourceHandle, ChannelAttribute.Volume, effective))
            {
                return true;
            }

            YargLogger.LogFormatError("Failed to set monitor source volume: {0}", Bass.LastError);
            return false;
        }

        public virtual void SetVolume(double volume)
        {
        }

        public void Stop()
        {
            lock (_songLock)
            {
                if (_stopping)
                {
                    return;
                }

                _stopping = true;
            }

            StopOutput();
        }

        protected abstract void StopOutput();

        protected virtual void DisposeResources()
        {
        }

        private bool AddToOutputMixer(int sourceHandle, OutputChannel? outputChannel,
            BassFlags additionalFlags = BassFlags.Default)
        {
            var flags = BassFlags.MixerChanDownMix | additionalFlags;
            if (outputChannel is BassOutputChannel bassOutputChannel)
            {
                flags |= bassOutputChannel.Flags;
            }

            if (BassMix.MixerAddChannel(OutputMixerHandle, sourceHandle, flags) || Bass.LastError == Errors.Already)
            {
                return true;
            }

            YargLogger.LogFormatError("Failed to add source to output mixer: {0}", Bass.LastError);
            return false;
        }

        internal void DetachSong(int volumeMixerHandle)
        {
            lock (_songLock)
            {
                if (!BassMix.MixerRemoveChannel(volumeMixerHandle) && Bass.LastError != Errors.Handle)
                {
                    YargLogger.LogFormatError("Failed to remove song from output mixer: {0}", Bass.LastError);
                }
            }
        }
    }
}
