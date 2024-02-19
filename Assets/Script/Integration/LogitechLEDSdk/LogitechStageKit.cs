using LedCSharp;
using PlasticBand.Haptics;
using UnityEngine;
using YARG.Integration.StageKit;
using YARG.Settings;

namespace YARG
{
    public class LogitechStageKit : MonoBehaviour
    {
        //This is a pretty basic implementation of the Logitech LED SDK. It 1:1 matches the Stage Kit's LEDs to the
        //keyboard's LEDs, doing nothing more. A more fancy setup would be expand on the light cues and take full
        //advantage of all the keys. For example, Flare_fast could use the entire keyboard to create the effect as no
        //other color is active at the same time.
        //Supports only keyboard right now. Mice, headsets, etc are not supported at this time.

        private keyboardNames[] _redChannels;
        private keyboardNames[] _greenChannels;
        private keyboardNames[] _blueChannels;
        private keyboardNames[] _yellowChannels;
        private keyboardNames[] _strobeChannels;
        private keyboardNames[] _fogChannels;

        private bool _isSetup = false;
        private int _redPercentage;
        private int _greenPercentage;
        private int _bluePercentage;

        private void Start()
        {
            if (SettingsManager.Settings.LogitechEnabled.Value)
            {
                SetupLogitech();
            }
        }

        private void Update()
        {
            //Listen to the settings change
            if (SettingsManager.Settings.LogitechEnabled.Value && !_isSetup)
            {
                SetupLogitech();
            }

            if (!SettingsManager.Settings.LogitechEnabled.Value && _isSetup)
            {
                OnDestroy();
            }
        }

        private void SetupLogitech()
        {
            Debug.Log("Logitech LED SDK is enabled, setting up.");
            //Next thing I'm going to do is abstract the lighting controller out of stage kit and into its own class
            //that can be used by any lighting system, at this is getting messy.
            StageKitLightingController.Instance.OnLedSet += HandleLedEvent;
            StageKitLightingController.Instance.OnFogSet += HandleFogEvent;
            StageKitLightingController.Instance.OnStrobeSet += HandleStrobeEvent;

            //The LogitechGSDK is a wrapper around the Logitech DLL. P/invoke? Ahhhhhhh!
            LogitechGSDK.LogiLedInit();
            LogitechGSDK.LogiLedSaveCurrentLighting();

            //Arrays of the keys that will be used for each color and effect. Eight keys for each color, matching the
            //Stage Kit's eight LEDs. Some liberties are taken with the strobe and fog keys :)
            _redChannels = new keyboardNames[]
            {
                keyboardNames.Y, keyboardNames.U, keyboardNames.I, keyboardNames.O, keyboardNames.P,
                keyboardNames.OPEN_BRACKET, keyboardNames.CLOSE_BRACKET, keyboardNames.BACKSLASH
            };

            _greenChannels = new keyboardNames[]
            {
                keyboardNames.G, keyboardNames.H, keyboardNames.J, keyboardNames.K, keyboardNames.L,
                keyboardNames.SEMICOLON, keyboardNames.APOSTROPHE, keyboardNames.ENTER
            };

            _blueChannels = new keyboardNames[]
            {
                keyboardNames.V, keyboardNames.B, keyboardNames.N, keyboardNames.M, keyboardNames.COMMA,
                keyboardNames.PERIOD, keyboardNames.FORWARD_SLASH, keyboardNames.RIGHT_SHIFT
            };

            _yellowChannels = new keyboardNames[]
            {
                keyboardNames.SIX, keyboardNames.SEVEN, keyboardNames.EIGHT, keyboardNames.NINE, keyboardNames.ZERO,
                keyboardNames.MINUS, keyboardNames.EQUALS, keyboardNames.BACKSPACE
            };

            _strobeChannels = new keyboardNames[]
            {
                keyboardNames.G_BADGE, keyboardNames.NUM_ASTERISK, keyboardNames.NUM_SLASH, keyboardNames.NUM_LOCK,
                keyboardNames.NUM_ONE, keyboardNames.NUM_TWO, keyboardNames.NUM_THREE, keyboardNames.NUM_FOUR,
                keyboardNames.NUM_FIVE, keyboardNames.NUM_SIX, keyboardNames.NUM_SEVEN, keyboardNames.NUM_EIGHT,
                keyboardNames.NUM_NINE, keyboardNames.NUM_ZERO, keyboardNames.NUM_MINUS, keyboardNames.NUM_PLUS,
                keyboardNames.NUM_ENTER, keyboardNames.NUM_PERIOD
            };

            _fogChannels = new keyboardNames[]
            {
                keyboardNames.G_LOGO, keyboardNames.ESC, keyboardNames.F1, keyboardNames.F2, keyboardNames.F3,
                keyboardNames.F4, keyboardNames.F5, keyboardNames.F6, keyboardNames.F7, keyboardNames.F8,
                keyboardNames.F9, keyboardNames.F10, keyboardNames.F11, keyboardNames.F12, keyboardNames.PRINT_SCREEN,
                keyboardNames.SCROLL_LOCK, keyboardNames.PAUSE_BREAK
            };

            _isSetup = true;
        }

        private void HandleStrobeEvent(StageKitStrobeSpeed value)
        {
            switch (value)
            {
                case StageKitStrobeSpeed.Off:
                    foreach (var key in _strobeChannels)
                    {
                        LogitechGSDK.LogiLedStopEffectsOnKey(key);
                    }
                    break;

                case StageKitStrobeSpeed.Slow:
                    foreach (var key in _strobeChannels)
                    {
                        LogitechGSDK.LogiLedFlashSingleKey(key, 100, 100, 100, LogitechGSDK.LOGI_LED_DURATION_INFINITE, 100);
                    }
                    break;

                case StageKitStrobeSpeed.Medium:
                    foreach (var key in _strobeChannels)
                    {
                        LogitechGSDK.LogiLedFlashSingleKey(key, 100, 100, 100, LogitechGSDK.LOGI_LED_DURATION_INFINITE, 75);
                    }
                    break;

                case StageKitStrobeSpeed.Fast:
                    foreach (var key in _strobeChannels)
                    {
                        LogitechGSDK.LogiLedFlashSingleKey(key, 100, 100, 100, LogitechGSDK.LOGI_LED_DURATION_INFINITE, 70 );
                    }
                    break;

                case StageKitStrobeSpeed.Fastest:
                    foreach (var key in _strobeChannels)
                    {
                        LogitechGSDK.LogiLedFlashSingleKey(key, 100, 100, 100, LogitechGSDK.LOGI_LED_DURATION_INFINITE, 55);
                    }
                    break;
            }
        }

        private void HandleFogEvent(bool value)
        {
            if (value)
            {
                foreach (var key in _fogChannels)
                {
                    //750ms pulse is arbitrary, it looks kinda like blowing smoke to me
                    LogitechGSDK.LogiLedPulseSingleKey(key,100,100,100,0,0,0, 750 ,true);
                }

            }
            else
            {
                foreach (var key in _fogChannels)
                {
                    LogitechGSDK.LogiLedStopEffectsOnKey(key);
                }
            }

        }

        private void HandleLedEvent(StageKitLedColor color, StageKitLed value)
        {
            bool[] ledIsSet = new bool[8];

            // Set the values of ledIsSet based on the StageKitLed enum
            for (int i = 0; i < 8; i++)
            {
                ledIsSet[i] = (value & (StageKitLed) (1 << i)) != 0;
            }

            // Handle the event based on color
            switch (color)
            {
                case StageKitLedColor.Red:
                    SetChannelValues(_redChannels, ledIsSet, StageKitLedColor.Red );
                    break;

                case StageKitLedColor.Blue:
                    SetChannelValues(_blueChannels, ledIsSet, StageKitLedColor.Blue);
                    break;

                case StageKitLedColor.Green:
                    SetChannelValues(_greenChannels, ledIsSet, StageKitLedColor.Green);
                    break;

                case StageKitLedColor.Yellow:
                    SetChannelValues(_yellowChannels, ledIsSet, StageKitLedColor.Yellow);
                    break;

                case StageKitLedColor.None:
                    // I'm not sure .None is ever used, anywhere?
                    break;

                case StageKitLedColor.All:
                    SetChannelValues(_yellowChannels, ledIsSet, StageKitLedColor.Yellow);
                    SetChannelValues(_greenChannels, ledIsSet, StageKitLedColor.Green);
                    SetChannelValues(_blueChannels, ledIsSet, StageKitLedColor.Blue);
                    SetChannelValues(_redChannels, ledIsSet, StageKitLedColor.Red);
                    break;

                default:
                    Debug.LogWarning("(LogitechSDK) Unknown color: " + color);
                    break;
            }
        }

        private static void SetChannelValues(keyboardNames[] channels, bool[] ledIsSet, StageKitLedColor color)
        {
            for (int i = 0; i < 8; i++)
            {
                switch (color)
                {
                    case StageKitLedColor.Red:
                        LogitechGSDK.LogiLedSetLightingForKeyWithKeyName(channels[i], ledIsSet[i] ? (byte) 255 : (byte) 0, 0, 0);
                        break;
                    case StageKitLedColor.Blue:
                        LogitechGSDK.LogiLedSetLightingForKeyWithKeyName(channels[i], 0, 0, ledIsSet[i] ? (byte) 255 : (byte) 0);
                        break;
                    case StageKitLedColor.Green:
                        LogitechGSDK.LogiLedSetLightingForKeyWithKeyName(channels[i], 0, ledIsSet[i] ? (byte) 255 : (byte) 0, 0);
                        break;
                    case StageKitLedColor.Yellow:
                        LogitechGSDK.LogiLedSetLightingForKeyWithKeyName(channels[i], ledIsSet[i] ? (byte) 255 : (byte) 0, ledIsSet[i] ? (byte) 255 : (byte) 0, 0);
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            Debug.Log("Tearing down Logitech LED SDK.");
            LogitechGSDK.LogiLedRestoreLighting();
            LogitechGSDK.LogiLedShutdown();
            StageKitLightingController.Instance.OnLedSet -= HandleLedEvent;
            StageKitLightingController.Instance.OnFogSet -= HandleFogEvent;
            StageKitLightingController.Instance.OnStrobeSet -= HandleStrobeEvent;
            _isSetup = false;
        }
    }
}
