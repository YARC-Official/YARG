#nullable enable
using System;
using System.Collections.Generic;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Remembers what should be playing and keeps it plugged into the current speaker.
    ///     The manager decides which speaker to use. The speaker itself knows how to play.
    ///     This class is the switchboard in the middle. It holds all active songs and live mics,
    ///     moves them when the speaker changes, and unplugs them safely so nothing is lost.
    /// </summary>
    internal sealed class BassAudioRouter : IDisposable
    {
        private readonly HashSet<BassMonitor> _monitors = new();
        private readonly HashSet<BassSong>    _songs    = new();
        private          bool                 _disposed;
        private          ReadAheadStats       _finishedReadAheadStats;
        private          ulong                _observedUnderrunEvents;
        private          BassOutput?          _output;
        private          int                  _outputDeviceId = -1;
        private          double               _volume         = 1;

        public int HeardLatencyMilliseconds => _output?.HeardLatencyMilliseconds ?? 0;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Disconnect();
            foreach (var song in _songs)
            {
                song.Disposing -= RemoveSong;
            }

            _songs.Clear();
            foreach (var monitor in _monitors)
            {
                monitor.InvalidateOwner();
            }

            _monitors.Clear();
        }

        public bool Connect(BassOutput output, int deviceId)
        {
            _finishedReadAheadStats = default;
            _observedUnderrunEvents = 0;
            _output = output;
            _outputDeviceId = deviceId;
            output.SetVolume(_volume);

            if (!AttachSongs(output) || !AttachMonitors(deviceId))
            {
                DetachSongs();
                _output = null;
                _outputDeviceId = -1;
                return false;
            }

            ActivateSongs();
            return true;
        }

        public void Disconnect()
        {
            if (_output == null)
            {
                return;
            }

            DetachMonitors();
            DetachSongs();
            _output = null;
            _outputDeviceId = -1;
        }

        internal bool AddSong(BassSong song)
        {
            if (_output == null || !song.TryAttachOutput(_output))
            {
                return false;
            }

            _songs.Add(song);
            song.Disposing += RemoveSong;
            song.ActivateOutput();
            return true;
        }

        private void RemoveSong(BassSong song)
        {
            song.Disposing -= RemoveSong;
            if (_songs.Remove(song))
            {
                SaveReadAheadStats(song);
            }
        }

        public BassMonitor? RegisterMonitor(BassMonitorSource source, double volume, Action? attached = null,
            Action? detached = null)
        {
            if (HasMonitor(source.Handle))
            {
                YargLogger.LogFormatError("Monitor source {0} is already registered", source.Handle);
                return null;
            }

            var monitor = new BassMonitor(this, source, volume, attached, detached);
            _monitors.Add(monitor);
            if (_output == null)
            {
                return monitor;
            }

            if (TryAttachMonitor(monitor))
            {
                return monitor;
            }

            _monitors.Remove(monitor);
            monitor.InvalidateOwner();
            return null;
        }

        internal void Remove(BassMonitor monitor)
        {
            if (!_monitors.Contains(monitor))
            {
                return;
            }

            if (monitor.IsAttached)
            {
                _output?.DetachMonitor(monitor.Source.Handle);
                monitor.MarkDetached();
            }

            _monitors.Remove(monitor);
        }

        internal void SetMonitorVolume(BassMonitor monitor, double volume)
        {
            if (_monitors.Contains(monitor) && monitor.IsAttached)
            {
                _output?.SetMonitorVolume(monitor.Source.Handle, volume);
            }
        }

        public bool PlaySample(int sourceHandle, OutputChannel? outputChannel) =>
            _output?.PlaySample(sourceHandle, outputChannel) == true;

        public void SetSampleOutputChannel(int sourceHandle, OutputChannel? outputChannel) =>
            _output?.SetSampleOutputChannel(sourceHandle, outputChannel);

        public void SetVolume(double volume)
        {
            _volume = volume;
            _output?.SetVolume(volume);
        }

        internal ReadAheadStats GetReadAheadStats()
        {
            var total = _finishedReadAheadStats;
            foreach (var song in _songs)
            {
                total = AddReadAheadStats(total, song.GetReadAheadStats());
            }

            return total;
        }

        internal ulong TakeReadAheadUnderruns()
        {
            ulong underrunEvents = GetReadAheadStats().UnderrunEvents;
            ulong previous = _observedUnderrunEvents;
            _observedUnderrunEvents = underrunEvents;
            return underrunEvents >= previous ? underrunEvents - previous : underrunEvents;
        }

        private bool AttachSongs(BassOutput output)
        {
            foreach (var song in _songs)
            {
                if (!song.TryAttachOutput(output))
                {
                    return false;
                }
            }

            return true;
        }

        private void ActivateSongs()
        {
            foreach (var song in _songs)
            {
                song.ActivateOutput();
            }
        }

        private void DetachSongs()
        {
            foreach (var song in _songs)
            {
                SaveReadAheadStats(song);
                song.DetachOutput();
            }
        }

        private bool HasMonitor(int sourceHandle)
        {
            foreach (var monitor in _monitors)
            {
                if (monitor.Source.Handle == sourceHandle)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryAttachMonitor(BassMonitor monitor)
        {
            if (!monitor.Source.TryGetDevice(out int originalDevice))
            {
                return false;
            }

            if (!monitor.Source.TryMoveToDevice(_outputDeviceId))
            {
                return false;
            }

            if (!monitor.Source.ResetToLive())
            {
                monitor.Source.TryMoveToDevice(originalDevice);
                return false;
            }

            if (_output!.AttachMonitor(monitor.Source.Handle, monitor.Volume))
            {
                monitor.MarkAttached();
                return true;
            }

            monitor.Source.TryMoveToDevice(originalDevice);
            return false;
        }

        private bool AttachMonitors(int deviceId)
        {
            if (_monitors.Count == 0)
            {
                return true;
            }

            var snapshot = GetMonitorDevices();
            if (snapshot == null)
            {
                return false;
            }

            var moved = new List<(BassMonitor monitor, int original)>();
            var attached = new List<BassMonitor>();
            foreach ((var monitor, int original) in snapshot)
            {
                if (!monitor.Source.TryMoveToDevice(deviceId))
                {
                    break;
                }

                moved.Add((monitor, original));
                if (!monitor.Source.ResetToLive())
                {
                    break;
                }

                if (!_output!.AttachMonitor(monitor.Source.Handle, monitor.Volume))
                {
                    break;
                }

                monitor.MarkAttached();
                attached.Add(monitor);
            }

            if (attached.Count == snapshot.Count)
            {
                return true;
            }

            foreach (var monitor in attached)
            {
                _output?.DetachMonitor(monitor.Source.Handle);
                monitor.MarkDetached();
            }

            foreach ((var monitor, int original) in moved)
            {
                monitor.Source.TryMoveToDevice(original);
            }

            return false;
        }

        private List<(BassMonitor Monitor, int OriginalDevice)>? GetMonitorDevices()
        {
            var snapshot = new List<(BassMonitor Monitor, int OriginalDevice)>(_monitors.Count);
            foreach (var monitor in _monitors)
            {
                if (!monitor.Source.TryGetDevice(out int originalDevice))
                {
                    return null;
                }

                snapshot.Add((monitor, originalDevice));
            }

            return snapshot;
        }

        private void DetachMonitors()
        {
            if (_output == null)
            {
                return;
            }

            foreach (var monitor in _monitors)
            {
                if (!monitor.IsAttached)
                {
                    continue;
                }

                _output.DetachMonitor(monitor.Source.Handle);
                monitor.MarkDetached();
            }
        }

        private void SaveReadAheadStats(BassSong song)
        {
            var stats = song.GetReadAheadStats();
            if (stats.Size != 0)
            {
                _finishedReadAheadStats = AddFinishedReadAheadStats(_finishedReadAheadStats, stats);
            }
        }

        private static ReadAheadStats AddFinishedReadAheadStats(ReadAheadStats total, ReadAheadStats next)
        {
            if (next.State != default)
            {
                total.State = next.State;
            }

            if (next.LastError != 0)
            {
                total.LastError = next.LastError;
            }

            total.ProducedFrames += next.ProducedFrames;
            total.ConsumedFrames += next.ConsumedFrames;
            total.RequestedFrames += next.RequestedFrames;
            total.UnderrunFrames += next.UnderrunFrames;
            total.UnderrunEvents += next.UnderrunEvents;
            total.MaximumRenderNanoseconds = Math.Max(total.MaximumRenderNanoseconds, next.MaximumRenderNanoseconds);
            return total;
        }

        private static ReadAheadStats AddReadAheadStats(ReadAheadStats total, ReadAheadStats next)
        {
            if (next.State != default)
            {
                total.State = next.State;
            }

            if (next.LastError != 0)
            {
                total.LastError = next.LastError;
            }

            total.TargetFrames += next.TargetFrames;
            total.QueuedFrames += next.QueuedFrames;
            total.MinimumQueuedFrames += next.MinimumQueuedFrames;
            total.ProducedFrames += next.ProducedFrames;
            total.ConsumedFrames += next.ConsumedFrames;
            total.RequestedFrames += next.RequestedFrames;
            total.UnderrunFrames += next.UnderrunFrames;
            total.UnderrunEvents += next.UnderrunEvents;
            total.MaximumRenderNanoseconds = Math.Max(total.MaximumRenderNanoseconds, next.MaximumRenderNanoseconds);
            return total;
        }
    }
}