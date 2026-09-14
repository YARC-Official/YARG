using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.InputSystem.Layouts;

namespace Minis
{
    //
    // MIDI device assigner class that pairs a PlayerInput with a MIDI device
    //
    [RequireComponent(typeof(PlayerInput))]
    public sealed class MidiDeviceAssigner : MonoBehaviour
    {
        [SerializeField] int _channel = -1;
        [SerializeField] string _productName = null;

        bool TryPairing(InputDevice device)
        {
            // Check if it's a MIDI device.
            var midiDevice = device as Minis.MidiDevice;
            if (midiDevice == null) return false;

            // Channel matching
            if (_channel >= 0 && midiDevice.channel != _channel) return false;

            // Product name matching
            if (!string.IsNullOrEmpty(_productName) &&
                !device.description.product.Contains(_productName)) return false;

            // Pairing
            InputUser.PerformPairingWithDevice(device, GetComponent<PlayerInput>().user);

            return true;
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
          => TryPairing(device);

        void Start()
        {
            foreach (var device in InputSystem.devices)
                TryPairing(device);

            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDestroy()
          => InputSystem.onDeviceChange -= OnDeviceChange;
    }
}
