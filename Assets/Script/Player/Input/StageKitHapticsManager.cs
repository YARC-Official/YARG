using System.Collections.Generic;
using PlasticBand.Haptics;
using UnityEngine.InputSystem;

namespace YARG.Input
{
    public static class StageKitHapticsManager
    {
        private static List<IStageKitHaptics> stageKits = new();

        public static void Initialize()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            foreach (var device in InputSystem.devices)
            {
                if (device is IStageKitHaptics stageKit)
                {
                    stageKits.Add(stageKit);
                }
            }
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is not IStageKitHaptics stageKit)
            {
                return;
            }

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Enabled:
                case InputDeviceChange.Reconnected:
                    if (!stageKits.Contains(stageKit))
                    {
                        stageKits.Add(stageKit);
                    }

                    break;

                case InputDeviceChange.Disabled:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Removed:
                    stageKits.Remove(stageKit);
                    break;
            }
        }

        public static void SetFogMachine(bool enabled)
        {
            foreach (var stageKit in stageKits)
            {
                stageKit.SetFogMachine(enabled);
            }
        }

        public static void SetStrobeSpeed(StageKitStrobeSpeed speed)
        {
            foreach (var stageKit in stageKits)
            {
                stageKit.SetStrobeSpeed(speed);
            }
        }

        public static void SetLeds(StageKitLedColor color, StageKitLed leds)
        {
            foreach (var stageKit in stageKits)
            {
                stageKit.SetLeds(color, leds);
            }
        }

        public static void SetRedLeds(StageKitLed leds) => SetLeds(StageKitLedColor.Red, leds);

        public static void SetYellowLeds(StageKitLed leds) => SetLeds(StageKitLedColor.Yellow, leds);

        public static void SetBlueLeds(StageKitLed leds) => SetLeds(StageKitLedColor.Blue, leds);

        public static void SetGreenLeds(StageKitLed leds) => SetLeds(StageKitLedColor.Green, leds);

        public static void Reset()
        {
            foreach (var stageKit in stageKits)
            {
                stageKit.ResetHaptics();
            }
        }
    }
}