using System;
using Haukcode.sACN;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Chart;
using YARG.Integration.StageKit;
using YARG.Settings;
using Debug = UnityEngine.Debug;

namespace YARG.Integration.Sacn
{
    public enum CueEnum
    {
        NoCue = 0,
        Menu = 10,
        Score = 20,
        Intro = 30,
        Verse = 40,
        Chorus = 50,
        CoolLoop = 60,
        WarmLoop = 70,
        CoolManual = 80,
        WarmManual = 90,
        Dischord = 100,
        Stomp = 110,
        Default = 120,
        Harmony = 130,
        Frenzy = 140,
        Silhouettes = 150,
        SilhouettesSpotlight = 160,
        Searchlights = 170,
        Sweep = 180,
        StrobeOff = 190,
        StrobeSlow = 191,
        StrobeMedium = 192,
        StrobeFast = 193,
        StrobeFastest = 194,
        BlackoutFast = 200,
        BlackoutSlow = 205,
        BlackoutSpotlight = 210,
        FlareSlow = 220,
        FlareFast = 225,
        BigRockEnding = 230,
        BonusEffect = 240,
        BonusEffectOptional = 245,
        FogOn = 250,
        FogOff = 255
    }

    public class SacnController : MonoSingleton<SacnController>
    {
        // DMX spec says 44 updates per second is the max
        private const float TARGET_FPS = 44f;
        private const float TIME_BETWEEN_CALLS = 1f / TARGET_FPS;

        // Each universe supports up to 512 channels
        private const int UNIVERSE_SIZE = 512;

        private const string ACN_SOURCE_NAME = "YARG";

        // A 128-bit (16 byte) UUID that translates to "KEEP PLAYING YARG!"
        private readonly Guid _acnSourceId = new Guid("{4B454550-504C-4159-494E-475941524721}");

        private SACNClient _sendClient;

        // DMX channels
        // 8 per color to match the stageKit layout. Default channels, the user must change them in settings.
        private int[] _dimmerChannels;
        private int[] _redChannels;
        private int[] _greenChannels;
        private int[] _blueChannels;
        private int[] _yellowChannels;
        private int _cueChangeChannel;
        private int _fogChannel;
        private int _strobeChannel;

        private readonly byte[] _dataPacket = new byte[UNIVERSE_SIZE];

        private byte _cueValue = 0;

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void HandleLightingTypeChange(LightingType newType)
        {
            _cueValue = newType switch
            {
                LightingType.Chorus => (byte)CueEnum.Chorus,
                LightingType.Default => (byte)CueEnum.Default,
                LightingType.Dischord => (byte)CueEnum.Dischord,
                LightingType.Frenzy => (byte)CueEnum.Frenzy,
                LightingType.Harmony => (byte)CueEnum.Harmony,
                LightingType.Intro => (byte)CueEnum.Intro,
                LightingType.Menu => (byte)CueEnum.Menu,
                LightingType.Score => (byte)CueEnum.Score,
                LightingType.Silhouettes => (byte)CueEnum.Silhouettes,
                LightingType.Silhouettes_Spotlight => (byte)CueEnum.SilhouettesSpotlight,
                LightingType.Sweep => (byte)CueEnum.Sweep,
                LightingType.Searchlights => (byte)CueEnum.Searchlights,
                LightingType.Stomp => (byte)CueEnum.Stomp,
                LightingType.Verse => (byte)CueEnum.Verse,
                LightingType.Blackout_Fast => (byte)CueEnum.BlackoutFast,
                LightingType.Blackout_Slow => (byte)CueEnum.BlackoutSlow,
                LightingType.Blackout_Spotlight => (byte)CueEnum.BlackoutSpotlight,
                LightingType.Cool_Automatic => (byte)CueEnum.CoolLoop,
                LightingType.Cool_Manual => (byte)CueEnum.CoolManual,
                LightingType.Flare_Fast => (byte)CueEnum.FlareFast,
                LightingType.Flare_Slow => (byte)CueEnum.FlareSlow,
                LightingType.Keyframe_First => (byte)CueEnum.Default,
                LightingType.Keyframe_Next => (byte)CueEnum.Default,
                LightingType.Keyframe_Previous => (byte)CueEnum.Default,
                LightingType.Strobe_Fast => (byte)CueEnum.StrobeFast,
                LightingType.Strobe_Fastest => (byte)CueEnum.StrobeFastest,
                LightingType.Strobe_Medium => (byte)CueEnum.StrobeMedium,
                LightingType.Strobe_Off => (byte)CueEnum.StrobeOff,
                LightingType.Strobe_Slow    => (byte)CueEnum.StrobeSlow,
                LightingType.Warm_Automatic => (byte)CueEnum.WarmLoop,
                LightingType.Warm_Manual => (byte)CueEnum.WarmManual,
                LightingType.BigRockEnding => (byte)CueEnum.BigRockEnding,
                _ => (byte)CueEnum.NoCue,
            };

            _dataPacket[_cueChangeChannel - 1] = _cueValue;
        }

        private void HandleStageEffectChange(StageEffect newEffect)
        {
            _cueValue = newEffect switch
            {
                StageEffect.FogOn   => (byte) CueEnum.FogOn,
                StageEffect.FogOff  => (byte) CueEnum.FogOff,
                StageEffect.BonusFx => (byte) CueEnum.BonusEffect,
                _                   => (byte)CueEnum.NoCue,
            };

            _dataPacket[_cueChangeChannel - 1] = _cueValue;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            switch (scene.name)
            {
                case "Gameplay":
                    StageKitGameplay.OnStageEffectChange -= HandleStageEffectChange;
                    StageKitGameplay.OnLightingTypeChange -= HandleLightingTypeChange;
                    break;

                default:
                    break;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            switch (scene.name)
            {
                case "Gameplay":
                    StageKitGameplay.OnStageEffectChange += HandleStageEffectChange;
                    StageKitGameplay.OnLightingTypeChange += HandleLightingTypeChange;
                    break;

                case "ScoreScreen":
                    _cueValue = (byte)CueEnum.Score;
                    break;

                case "MenuScene":
                    _cueValue = (byte)CueEnum.Menu;
                    break;

                default:
                    break;
            }
        }

        public void HandleEnabledChanged(bool enabled)
        {
            if (enabled)
            {
                if (_sendClient != null) return;

                Debug.Log("Starting Sacn Controller...");

                StageKitLightingController.Instance.OnLedSet += HandleLedEvent;
                StageKitLightingController.Instance.OnFogSet += HandleFogEvent;
                StageKitLightingController.Instance.OnStrobeSet += HandleStrobeEvent;

                UpdateDMXChannels();

                _sendClient = new SACNClient(senderId: _acnSourceId, senderName: ACN_SOURCE_NAME,
                    localAddress: SACNCommon.GetFirstBindAddress().IPAddress);

                InvokeRepeating(nameof(Sender), 0, TIME_BETWEEN_CALLS);

                //Many DMX fixtures have a 'Master dimmer' channel that controls the overall brightness of the fixture.
                //Got to turn those on.
                for (int i = 0; i < _dimmerChannels.Length; i++)
                {
                    _dataPacket[_dimmerChannels[i] - 1] = 255;
                }
            }
            else
            {
                OnDestroy();
            }
        }

        public void UpdateDMXChannels()
        {
            _dimmerChannels = SettingsManager.Settings.DMXDimmerChannels.Value;
            _redChannels = SettingsManager.Settings.DMXRedChannels.Value;
            _greenChannels = SettingsManager.Settings.DMXGreenChannels.Value;
            _blueChannels = SettingsManager.Settings.DMXBlueChannels.Value;
            _yellowChannels = SettingsManager.Settings.DMXYellowChannels.Value;
            _cueChangeChannel = SettingsManager.Settings.DMXCueChangeChannel.Value;
            _fogChannel = SettingsManager.Settings.DMXFogChannel.Value;
            _strobeChannel = SettingsManager.Settings.DMXStrobeChannel.Value;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            if (_sendClient == null) return;

            Debug.Log("Killing Sacn Controller...");

            // A good controller will also turn everything off after not receiving a packet after 2.5 seconds.
            // But this doesn't hurt to do.
            for (int i = 0; i < _dataPacket.Length; i++)
            {
                _dataPacket[i] = 0; //turn everything off
            }

            //force send final packet.
            Sender();

            _sendClient.Dispose();
            _sendClient = null;

            CancelInvoke(nameof(Sender));

            StageKitLightingController.Instance.OnLedSet -= HandleLedEvent;
            StageKitLightingController.Instance.OnFogSet -= HandleFogEvent;
            StageKitLightingController.Instance.OnStrobeSet -= HandleStrobeEvent;
        }

        private void HandleFogEvent(bool value)
        {
            _dataPacket[SettingsManager.Settings.DMXFogChannel.Value - 1] = value
                ? (byte) 255
                : (byte) 0;
        }

        private void HandleStrobeEvent(StageKitStrobeSpeed value)
        {
            // TODO: I'm honestly just guessing at these values. I don't have a DMX strobe light to test with.
            _dataPacket[SettingsManager.Settings.DMXStrobeChannel.Value - 1] = value switch
            {
                StageKitStrobeSpeed.Off     => 0,
                StageKitStrobeSpeed.Slow    => 64,
                StageKitStrobeSpeed.Medium  => 127,
                StageKitStrobeSpeed.Fast    => 191,
                StageKitStrobeSpeed.Fastest => 255,
                _ => throw new Exception("Unreachable.")
            };
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
                    SetChannelValues(_redChannels, ledIsSet);
                    break;

                case StageKitLedColor.Blue:
                    SetChannelValues(_blueChannels, ledIsSet);
                    break;

                case StageKitLedColor.Green:
                    SetChannelValues(_greenChannels, ledIsSet);
                    break;

                case StageKitLedColor.Yellow:
                    SetChannelValues(_yellowChannels, ledIsSet);
                    break;

                case StageKitLedColor.None:
                    // I'm not sure this is ever used, anywhere?
                    break;

                case StageKitLedColor.All:
                    SetChannelValues(_yellowChannels, ledIsSet);
                    SetChannelValues(_greenChannels, ledIsSet);
                    SetChannelValues(_blueChannels, ledIsSet);
                    SetChannelValues(_redChannels, ledIsSet);
                    break;

                default:
                    throw new Exception("Unreachable.");
            }
        }

        private void SetChannelValues(int[] channels, bool[] ledIsSet)
        {
            for (int i = 0; i < 8; i++)
            {
                _dataPacket[channels[i] - 1] = ledIsSet[i]
                    ? (byte) 255
                    : (byte) 0;
            }
        }

        private void Sender()
        {
            // Hardcoded to universe 1, as this is for non-professional use, I doubt anyone is running multiple universes.
            // Didn't want to confuse the user with settings for something they don't need. However, it's a simple change
            // if needed. Same goes for sending multicast vs singlecast. Sacn spec says multicast is the correct default
            // way to go but singlecast can be used if needed.
            _sendClient.SendMulticast(1, _dataPacket);
        }
    }
}