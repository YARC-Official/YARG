using System.Collections.Generic;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Core.Engine.Vocals;
using YARG.Gameplay;

namespace YARG.Input
{
    public class MicInputContext
    {
        private readonly List<MicDevice> _devices;
        private readonly List<MicDevice> _connectedDevices;

        private readonly SerializedMic[] _slotIdentities;
        private bool _recording;
        private readonly HashSet<MicDevice> _startedDevices = new();
        private readonly bool _separateMicrophones;
        private readonly List<(GameInput Input, int Order)> _pending = new();

        public int MicrophoneCount => _devices.Count;

        public MicInputContext(List<MicDevice> devices, GameManager gameManager, bool separateMicrophones = false,
            IReadOnlyList<SerializedMic> sessionSlots = null)
        {
            _separateMicrophones = separateMicrophones;
            _connectedDevices = devices;
            _devices = separateMicrophones ? new List<MicDevice>() : devices;
            if (separateMicrophones)
            {
                int count = sessionSlots?.Count ?? devices.Count;
                _slotIdentities = new SerializedMic[count];
                for (int i = 0; i < count; i++)
                {
                    _slotIdentities[i] = sessionSlots == null ? devices[i].Serialize() : sessionSlots[i];
                    _devices.Add(null);
                }
                for (int i = 0; i < count; i++) RefreshSlot(i);
            }
        }

        /// <summary>
        /// Starts recording the output.
        /// </summary>
        public void Start()
        {
            Stop();
            for (int mic = 0; mic < _devices.Count; mic++)
            {
                var device = _separateMicrophones ? RefreshSlot(mic) : _devices[mic];
                if (device == null) continue;
                StartRecording(device);
            }
            _recording = true;
        }

        private void StartRecording(MicDevice device)
        {
            if (!_startedDevices.Add(device)) return;
            device.ClearOutputQueue();
            device.IsRecordingOutput = true;
        }

        private void RefreshTraditionalDevices()
        {
            _startedDevices.RemoveWhere(device =>
            {
                if (_connectedDevices.Contains(device)) return false;
                device.IsRecordingOutput = false;
                return true;
            });
            foreach (var device in _connectedDevices) StartRecording(device);
        }

        private MicDevice RefreshSlot(int slot)
        {
            var device = _devices[slot];
            if (device != null && _connectedDevices.Contains(device)) return device;

            if (device != null)
            {
                device.IsRecordingOutput = false;
                _startedDevices.Remove(device);
            }
            _devices[slot] = null;
            var identity = _slotIdentities[slot];
            foreach (var candidate in _connectedDevices)
            {
                if (_devices.Contains(candidate)) continue;
                var candidateIdentity = candidate.Serialize();
                if (candidateIdentity.BaseName != identity.BaseName || candidateIdentity.Channel != identity.Channel)
                    continue;

                // Reacquire only this song's existing slot, including after audio settings
                // recreate devices. New bindings cannot add singers partway through a replay.
                _devices[slot] = candidate;
                if (_recording)
                {
                    StartRecording(candidate);
                }
                return candidate;
            }
            return null;
        }

        /// <summary>
        /// Converts microphone frames to engine inputs, retaining session slots in Party Vocals.
        /// </summary>
        public IEnumerable<GameInput> GetInputsFromMic()
        {
            if (_recording && !_separateMicrophones) RefreshTraditionalDevices();
            _pending.Clear();
            for (int mic = 0; mic < _devices.Count; mic++)
            {
                var device = _separateMicrophones ? RefreshSlot(mic) : _devices[mic];
                if (device == null) continue;
                while (device.DequeueOutputFrame(out var frame))
                {
                    // frame.VoiceDetected will ALWAYS be true here, as it wouldn't be queued otherwise

                    // Queue it up!
                    GameInput gameInput;
                    if (!frame.IsHit)
                    {
                        gameInput = GameInput.Create(frame.Time, VocalsAction.Pitch, frame.PitchAsMidiNote);
                    }
                    else
                    {
                        gameInput = GameInput.Create(frame.Time, VocalsAction.Hit, true);
                    }

                    if (_separateMicrophones)
                    {
                        gameInput = new GameInput(gameInput.Time,
                            PartyVocalsInput.Pack(mic, gameInput.GetAction<VocalsAction>()), gameInput.Integer);
                        _pending.Add((gameInput, _pending.Count));
                    }
                    else
                    {
                        yield return gameInput;
                    }
                }
            }

            // Merge device queues before BasePlayer applies calibration and queues inputs.
            _pending.Sort((a, b) =>
            {
                int time = a.Input.Time.CompareTo(b.Input.Time);
                return time != 0 ? time : a.Order.CompareTo(b.Order);
            });
            foreach (var pending in _pending)
                yield return pending.Input;
        }

        /// <summary>
        /// Stops recording the output.
        /// </summary>
        public void Stop()
        {
            _recording = false;
            foreach (var device in _startedDevices)
            {
                device.IsRecordingOutput = false;
            }
            _startedDevices.Clear();
        }
    }
}
