using System;
using System.Threading;
using Minis.Native;
using UnityEngine;

using static Minis.Native.RtMidi;

namespace Minis
{
    internal enum MidiStatus
    {
        NoteOff = 0x8,
        NoteOn = 0x9,
        NotePressure = 0xA,
        ControlChange = 0xB,
        ProgramChange = 0xC,
        ChannelPressure = 0xD,
        PitchBend = 0xE,
        System = 0xF,
    }

    /// <summary>
    /// A MIDI device, as reported by RtMidi.
    /// </summary>
    internal unsafe sealed class MidiPort : IDisposable
    {
        private readonly MidiBackend _backend;

        private RtMidiInHandle _portHandle;
        public readonly string name;

        private MidiChannel _allChannels;
        private readonly MidiChannel[] _channels = new MidiChannel[16];

        private Thread _readThread;
        private volatile bool _keepReading = true;

        public MidiPort(MidiBackend backend, uint portNumber)
        {
            _backend = backend;

            _portHandle = rtmidi_in_create_default();
            if (_portHandle == null || _portHandle.IsInvalid)
                throw new Exception("Failed to create RtMidi handle!");

            name = rtmidi_get_port_name(_portHandle, portNumber);
            rtmidi_open_port(_portHandle, portNumber, "RtMidi Input");
            if (!_portHandle.Ok)
            {
                string error = _portHandle.ErrorMessage;
                _portHandle.Dispose();
                throw new Exception($"Failed to open RtMidi port {portNumber}: {error}");
            }

            _allChannels = new MidiChannel(_backend, this, -1);

            _readThread = new Thread(ReadThread) { IsBackground = true };
            _readThread.Start();
        }

        public void Dispose()
        {
            _keepReading = false;
            _readThread?.Join();
            _readThread = null;

            _portHandle?.Dispose();
            _portHandle = null;

            foreach (var channel in _channels)
                DisposeChannel(channel);

            DisposeChannel(_allChannels);
        }

        private void DisposeChannel(MidiChannel channel)
        {
            if (channel == null || channel.device == null)
                return;

            _backend.QueueDeviceRemove(channel.device);
        }

        private void ReadThread()
        {
            const int retryThreshold = 3;
            int retryCount = 0;

            const int bufferSize = 1024;
            byte* message = stackalloc byte[bufferSize];

            for (; _keepReading; Thread.Sleep(1))
            {
                UIntPtr tmpSize = (UIntPtr)bufferSize;
                double timestamp = rtmidi_in_get_message(_portHandle, message, ref tmpSize);
                uint size = (uint)tmpSize;

                if (!_portHandle.Ok)
                {
                    Debug.LogError($"Failed to read MIDI message: {_portHandle.ErrorMessage}");
                    if (++retryCount >= retryThreshold)
                        break;
                    continue;
                }

                if (size < 1)
                    continue;

                var status = (MidiStatus)(message[0] >> 4);
                byte arg = (byte)(message[0] & 0x0F);
                HandleStatus(status, arg, message + 1, size - 1);
            }
        }

        private void HandleStatus(MidiStatus status, byte arg, byte* buffer, uint length)
        {
            switch (status)
            {
                case MidiStatus.NoteOn:
                {
                    if (length < 2)
                        break;

                    byte channel = arg;
                    byte note = buffer[0];
                    byte velocity = buffer[1];

                    // Velocity 0 is equivalent to a note off
                    if (velocity == 0)
                        goto case MidiStatus.NoteOff;

                    _allChannels.ProcessNoteOn(note, velocity);
                    GetChannelDevice(channel).ProcessNoteOn(note, velocity);
                    break;
                }
                case MidiStatus.NoteOff:
                {
                    if (length < 2)
                        break;

                    byte channel = arg;
                    byte note = buffer[0];
                    // byte velocity = buffer[1];

                    _allChannels.ProcessNoteOff(note);
                    GetChannelDevice(channel).ProcessNoteOff(note);
                    break;
                }
                case MidiStatus.ControlChange:
                {
                    if (length < 2)
                        break;

                    int channel = arg;
                    byte control = buffer[0];
                    byte value = buffer[1];

                    _allChannels.ProcessControlChange(control, value);
                    GetChannelDevice(channel).ProcessControlChange(control, value);
                    break;
                }
                case MidiStatus.PitchBend:
                {
                    if (length < 2)
                        break;

                    int channel = arg;
                    byte lsb = buffer[0];
                    byte msb = buffer[1];

                    _allChannels.ProcessPitchBend(msb, lsb);
                    GetChannelDevice(channel).ProcessPitchBend(msb, lsb);
                    break;
                }
            }
        }

        private MidiChannel GetChannelDevice(int channel)
        {
            if (_channels[channel] == null)
                _channels[channel] = new MidiChannel(_backend, this, channel);

            return _channels[channel];
        }
    }
}
