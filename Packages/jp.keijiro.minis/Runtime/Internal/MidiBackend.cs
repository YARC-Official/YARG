using System;
using System.Collections.Generic;
using System.Threading;
using Minis.Native;
using UnityEngine;
using UnityEngine.InputSystem;

using static Minis.Native.RtMidi;

namespace Minis
{
    /// <summary>
    /// Manages RtMidi and the devices it reads.
    /// </summary>
    internal sealed class MidiBackend : CustomInputBackend<MidiChannel>
    {
        private RtMidiInHandle _rtMidi;

        // Track port count separately, for better handling of error scenarios
        private uint _lastPortCount = 0;
        private List<MidiPort> _ports = new List<MidiPort>();

        // Port scanning runs on its own thread: on iOS, CoreMIDI calls such as
        // MIDIGetNumberOfSources pump the current runloop while talking to
        // midiserver, which re-enters Unity's player loop when called from
        // onBeforeUpdate ("PlayerLoop internal function has been called
        // recursively") and corrupts input/rendering state. Message reads are
        // already threaded (see MidiPort.ReadThread); this matches that design.
        private Thread _scanThread;
        private volatile bool _keepScanning = true;

        // The RtMidi handle must be created before the base constructor runs:
        // CustomInputBackend's constructor subscribes to InputSystem callbacks,
        // so throwing afterwards (e.g. DllNotFoundException when the native
        // library is missing) leaves a half-constructed backend hooked into
        // onBeforeUpdate, spamming exceptions every frame.
        public static MidiBackend Create()
        {
            var rtMidi = rtmidi_in_create_default();
            if (rtMidi == null || rtMidi.IsInvalid)
                throw new Exception("Failed to create RtMidi handle!");
            if (!rtMidi.Ok)
            {
                string message = rtMidi.ErrorMessage;
                rtMidi.Dispose();
                throw new Exception($"Failed to create RtMidi handle: {message}");
            }

            return new MidiBackend(rtMidi);
        }

        private MidiBackend(RtMidiInHandle rtMidi)
        {
            _rtMidi = rtMidi;

            _scanThread = new Thread(ScanThread) { IsBackground = true };
            _scanThread.Start();
        }

        protected override void OnDispose()
        {
            _keepScanning = false;
            _scanThread?.Join();
            _scanThread = null;

            foreach (var port in _ports)
                port?.Dispose();
            _ports.Clear();

            _rtMidi?.Dispose();
            _rtMidi = null;
        }

        private void ScanThread()
        {
            for (; _keepScanning; Thread.Sleep(500))
            {
                try
                {
                    ScanPorts();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void ScanPorts()
        {
            // Check for port connections/disconnections
            uint portCount = rtmidi_get_port_count(_rtMidi);
            if (!_rtMidi.Ok)
            {
                Debug.LogError($"Failed to get RtMidi port count: {_rtMidi.ErrorMessage}");
            }
            else if (portCount != _lastPortCount)
            {
                // Update port count first so we don't repeatedly attempt to open ports that failed
                _lastPortCount = portCount;

                // Completely refresh all MIDI devices
                // Not ideal, but no sane way to track which devices have been added/removed
                foreach (var port in _ports)
                    port.Dispose();
                _ports.Clear();

                for (uint port = 0; port < portCount; port++)
                {
                    // Attempt port open 3 times
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            _ports.Add(new MidiPort(this, port));
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }
                }
            }
        }

        protected override MidiChannel OnDeviceAdded(InputDevice device, IDisposable context)
        {
            var channel = (MidiChannel)context;
            channel.OnAdded(device);
            return channel;
        }

        protected override void OnDeviceRemoved(MidiChannel channel)
        {
            channel.OnRemoved();
        }
    }
}