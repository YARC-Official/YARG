using System;
using System.Collections.Generic;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     The capture for one physical mic device, shared by every mic on that device.
    ///     Some devices have multiple inputs (Channel 1, Channel 2, etc.) but we only
    ///     open the device once — each mic just reads its own channel from that one capture.
    /// </summary>
    internal sealed class RecordingSession : IDisposable
    {
        private readonly object              _lock = new();
        private readonly List<BassMicDevice> _mics = new();

        private readonly int             _deviceId;
        private          RecordingHandle _recordHandle;

        private bool _disposed;

        public int SampleRate   => _recordHandle.SampleRate;
        public int RecordPeriod => _recordHandle.RecordPeriod;
        public int Channels     => _recordHandle.Channels;

        private RecordingSession(int deviceId, RecordingHandle recordHandle)
        {
            _deviceId = deviceId;
            _recordHandle = recordHandle;
        }

#nullable enable
        public static RecordingSession? Create(int deviceId, int captureChannels)
#nullable disable
        {
            var session = new RecordingSession(deviceId, null);
            session._recordHandle = RecordingHandle.CreateRecordingHandle(session.OnRecordData, captureChannels);
            if (session._recordHandle == null)
            {
                return null;
            }

            if (!session._recordHandle.Start())
            {
                YargLogger.LogFormatError("Failed to start recording session for device [{0}]: {1}!",
                    deviceId, Bass.LastError);
                session._recordHandle.Dispose();
                return null;
            }

            return session;
        }

        public void AddMic(BassMicDevice mic)
        {
            lock (_lock)
            {
                _mics.Add(mic);
            }
        }

        public void RemoveMic(BassMicDevice mic)
        {
            lock (_lock)
            {
                _mics.Remove(mic);
            }
        }

        public bool IsChannelClaimed(int channel)
        {
            lock (_lock)
            {
                return AnyMicOnChannel(channel);
            }
        }

        private bool AnyMicOnChannel(int channel)
        {
            for (int i = 0; i < _mics.Count; ++i)
            {
                if (_mics[i].CaptureChannel == channel)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Pause()
        {
            return _recordHandle.Pause();
        }

        public bool Start()
        {
            return _recordHandle.Start();
        }

        public void FlushRecordBuffer()
        {
            int available = Bass.ChannelGetData(_recordHandle.Handle, IntPtr.Zero, (int) DataFlags.Available);

            if (Bass.ChannelGetData(_recordHandle.Handle, IntPtr.Zero, available) == -1)
            {
                YargLogger.LogFormatError("Failed to clear recording buffer for device [{0}]: {1}!",
                    _deviceId, Bass.LastError);
            }
        }

        private bool OnRecordData(int handle, IntPtr buffer, int length, IntPtr user)
        {
            lock (_lock)
            {
                for (int i = 0; i < _mics.Count; ++i)
                {
                    _mics[i].ProcessData(buffer, length);
                }
            }

            return true;
        }

        private void Dispose(bool disposing)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _mics.Clear();
            }

            _recordHandle?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RecordingSession()
        {
            Dispose(false);
        }
    }
}
