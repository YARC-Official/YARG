using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace Minis
{
    /// <summary>
    /// A single channel output on a MIDI device.
    /// </summary>
    internal sealed class MidiChannel : IDisposable
    {
        private MidiBackend _backend;
        private MidiPort _port;
        private InputDevice _device;

        public readonly int channelNumber;
        public InputDevice device => _device;

        private readonly bool[] _activeNotes = new bool[128];

        public MidiChannel(MidiBackend backend, MidiPort port, int channel)
        {
            _backend = backend;
            _port = port;
            channelNumber = channel;

            var description = new InputDeviceDescription()
            {
                interfaceName = "Minis",
            };

            if (channel < 0)
            {
                description.product = _port.name + " (All Channels)";
            }
            else
            {
                description.product = _port.name + " Channel " + channel;
                description.capabilities = "{\"channel\":" + channel + "}";
            }

            _backend.QueueDeviceAdd(description, this);
        }

        // Only exists so we can avoid allocating an extra object for the add context
        void IDisposable.Dispose() {}

        public void OnAdded(InputDevice device)
        {
            _device = device;
        }

        public void OnRemoved()
        {
            _device = null;
        }

        public void ProcessNoteOn(byte note, byte velocity)
        {
            // Consecutive note ons need to have a note off inserted in-between
            if (_activeNotes[note])
                ProcessNoteOff(note);

            SendDeltaEvent(note, velocity);
            _activeNotes[note] = true;
        }

        public void ProcessNoteOff(byte note)
        {
            SendDeltaEvent(MidiDeviceState.NoteOffset + note, (byte)0);
            _activeNotes[note] = false;
        }

        public void ProcessControlChange(byte number, byte value)
        {
            SendDeltaEvent(MidiDeviceState.ControlOffset + number, value);
        }

        public void ProcessPitchBend(byte msb, byte lsb)
        {
            ushort value = (ushort)((msb << 7) | lsb);
            SendDeltaEvent(MidiDeviceState.PitchBendOffset, value);
        }

        private unsafe void SendDeltaEvent<T>(uint offset, T value)
            where T : unmanaged
        {
            if (_device == null)
                return;

            // DeltaStateEvent already includes one byte of state data
            int eventSize = sizeof(DeltaStateEvent) - 1 + sizeof(T);
            byte* _delta = stackalloc byte[eventSize];
            var delta = (DeltaStateEvent*)_delta;

            *delta = new DeltaStateEvent()
            {
                baseEvent = new InputEvent(DeltaStateEvent.Type, eventSize, _device.deviceId),
                stateFormat = MidiDeviceState.Format,
                stateOffset = offset
            };
            *(T*)delta->deltaState = value;

            _backend.QueueEvent(&delta->baseEvent);
        }
    }
}