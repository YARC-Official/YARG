using System.Collections.Generic;
using UnityEngine.InputSystem;
using YARG.Audio;

namespace YARG.Player.Input
{
    public static class DeviceContainer
    {
        private static readonly List<IMicDevice> _possibleMics = new();
        private static readonly List<InputDevice> _possibleInputs = new();

        public static IReadOnlyList<IMicDevice> PossibleMics => _possibleMics;
        public static IReadOnlyList<InputDevice> PossibleInputs => _possibleInputs;

        public static void RefreshMics()
        {
            _possibleMics.Clear();
            _possibleMics.AddRange(GameManager.AudioManager.GetAllInputDevices());
        }

        public static void RefreshInputs()
        {
            _possibleInputs.Clear();
            _possibleInputs.AddRange(InputSystem.devices);
        }

        public static void AddMicToProfile(Profile profile, IMicDevice micDevice)
        {
            // Remove old mic input, if it was on
            if (profile.MicInput is not null)
            {
                profile.MicInput.MicDevice?.Dispose();
                profile.MicInput = null;
            }

            // Initialize new one
            micDevice.Initialize();
            profile.MicInput = new MicInput(micDevice);

            // AddInputToProfile<FiveFretInputStrategy>(profile, Mouse.current);
        }

        public static void AddInputToProfile<T>(Profile profile, InputDevice inputDevice) where T : InputStrategy, new()
        {
            // TODO: Don't allow changing input device on a input strategy
            var inputStrategy = new T();
            profile.InputStrategy = inputStrategy;
        }
    }
}