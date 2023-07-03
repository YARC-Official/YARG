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
            _possibleMics.AddRange(GlobalVariables.AudioManager.GetAllInputDevices());
        }

        public static void RefreshInputs()
        {
            _possibleInputs.Clear();
            _possibleInputs.AddRange(InputSystem.devices);
        }

        public static void AddMicToProfile(YargPlayer yargPlayer, IMicDevice micDevice)
        {
            // Remove old mic input, if it was on
            if (yargPlayer.MicInput is not null)
            {
                yargPlayer.MicInput.MicDevice?.Dispose();
                yargPlayer.MicInput = null;
            }

            // Initialize new one
            micDevice.Initialize();
            yargPlayer.MicInput = new MicInput(micDevice);

            // AddInputToProfile<FiveFretInputStrategy>(profile, Mouse.current);
        }

        public static void AddInputToProfile<T>(YargPlayer yargPlayer, InputDevice inputDevice) where T : InputStrategy, new()
        {
            // TODO: Don't allow changing input device on a input strategy
            var inputStrategy = new T();
            yargPlayer.InputStrategy = inputStrategy;
        }
    }
}