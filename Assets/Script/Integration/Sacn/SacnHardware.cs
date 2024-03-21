using System;
using System.Collections.Generic;
using Haukcode.sACN;
using PlasticBand.Haptics;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Settings;
using Debug = UnityEngine.Debug;

namespace YARG.Integration.Sacn
{
    public class SacnHardware : MonoSingleton<SacnHardware>
    {
        private enum CueEnum
        {
            NoCue = 0,
            KeyframeNext = 5,
            KeyframePrevious = 6,
            KeyframeFirst = 7,
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

        private byte _cueValue;

        private void OnStrobeEvent(StageKitStrobeSpeed value)
        {
            // TODO: I'm honestly just guessing at these values. I don't have a DMX strobe light to test with
            // and don't know if every DMX strobe light uses the same values the same way.

            _dataPacket[_strobeChannel - 1] = value switch
            {
                StageKitStrobeSpeed.Off     => 0,
                StageKitStrobeSpeed.Slow    => 64,
                StageKitStrobeSpeed.Medium  => 127,
                StageKitStrobeSpeed.Fast    => 191,
                StageKitStrobeSpeed.Fastest => 255,
                _                           => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };

            _dataPacket[_cueChangeChannel - 1] = value switch
            {
                StageKitStrobeSpeed.Off     => (byte) CueEnum.StrobeOff,
                StageKitStrobeSpeed.Slow    => (byte) CueEnum.StrobeSlow,
                StageKitStrobeSpeed.Medium  => (byte) CueEnum.StrobeMedium,
                StageKitStrobeSpeed.Fast    => (byte) CueEnum.StrobeFast,
                StageKitStrobeSpeed.Fastest => (byte) CueEnum.StrobeFastest,
                _                           => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
        }

        private void OnBonusFXEvent()
        {
            _dataPacket[_cueChangeChannel - 1] = (byte) CueEnum.BonusEffect;
        }

        private void OnFogStateEvent(MasterLightingController.FogState fogState)
        {
            if (fogState == MasterLightingController.FogState.On)
            {
                _dataPacket[_fogChannel - 1] = 255;
                _dataPacket[_cueChangeChannel - 1] = (byte) CueEnum.FogOn;
            }
            else
            {
                _dataPacket[_fogChannel - 1] = 0;
                _dataPacket[_cueChangeChannel - 1] = (byte) CueEnum.FogOff;
            }
        }

        private void OnLightingEvent(LightingEvent newType)
        {
            SetCueChannel(newType.Type);
        }

        private void SetCueChannel(LightingType newType)
        {
            _cueValue = newType switch
            {
                LightingType.Chorus                => (byte) CueEnum.Chorus,
                LightingType.Default               => (byte) CueEnum.Default,
                LightingType.Dischord              => (byte) CueEnum.Dischord,
                LightingType.Frenzy                => (byte) CueEnum.Frenzy,
                LightingType.Harmony               => (byte) CueEnum.Harmony,
                LightingType.Intro                 => (byte) CueEnum.Intro,
                LightingType.Menu                  => (byte) CueEnum.Menu,
                LightingType.Score                 => (byte) CueEnum.Score,
                LightingType.Silhouettes           => (byte) CueEnum.Silhouettes,
                LightingType.Silhouettes_Spotlight => (byte) CueEnum.SilhouettesSpotlight,
                LightingType.Sweep                 => (byte) CueEnum.Sweep,
                LightingType.Searchlights          => (byte) CueEnum.Searchlights,
                LightingType.Stomp                 => (byte) CueEnum.Stomp,
                LightingType.Verse                 => (byte) CueEnum.Verse,
                LightingType.Blackout_Fast         => (byte) CueEnum.BlackoutFast,
                LightingType.Blackout_Slow         => (byte) CueEnum.BlackoutSlow,
                LightingType.Blackout_Spotlight    => (byte) CueEnum.BlackoutSpotlight,
                LightingType.Cool_Automatic        => (byte) CueEnum.CoolLoop,
                LightingType.Cool_Manual           => (byte) CueEnum.CoolManual,
                LightingType.Flare_Fast            => (byte) CueEnum.FlareFast,
                LightingType.Flare_Slow            => (byte) CueEnum.FlareSlow,
                LightingType.Keyframe_First        => (byte) CueEnum.KeyframeFirst,
                LightingType.Keyframe_Next         => (byte) CueEnum.KeyframeNext,
                LightingType.Keyframe_Previous     => (byte) CueEnum.KeyframePrevious,
                LightingType.Strobe_Fast           => (byte) CueEnum.StrobeFast,
                LightingType.Strobe_Fastest        => (byte) CueEnum.StrobeFastest,
                LightingType.Strobe_Medium         => (byte) CueEnum.StrobeMedium,
                LightingType.Strobe_Off            => (byte) CueEnum.StrobeOff,
                LightingType.Strobe_Slow           => (byte) CueEnum.StrobeSlow,
                LightingType.Warm_Automatic        => (byte) CueEnum.WarmLoop,
                LightingType.Warm_Manual           => (byte) CueEnum.WarmManual,
                LightingType.BigRockEnding         => (byte) CueEnum.BigRockEnding,
                _                                  => (byte) CueEnum.NoCue,
            };

            _dataPacket[_cueChangeChannel - 1] = _cueValue;
        }

        public void HandleEnabledChanged(bool isEnabled)
        {
            if (isEnabled)
            {
                if (_sendClient != null) return;

                Debug.Log("(sACN Hardware Controller) Starting sACN Controller...");

                MasterLightingController.OnFogState += OnFogStateEvent;
                MasterLightingController.OnStrobeEvent += OnStrobeEvent;
                MasterLightingController.OnBonusFXEvent += OnBonusFXEvent;
                MasterLightingController.OnLightingEvent += OnLightingEvent;
                StageKitInterpreter.OnLedEvent += HandleLedEvent;

                UpdateDMXChannelNumbers();

                _sendClient = new SACNClient(senderId: _acnSourceId, senderName: ACN_SOURCE_NAME,
                    localAddress: SACNCommon.GetFirstBindAddress().IPAddress);

                //Many DMX fixtures have a 'Master dimmer' channel that controls the overall brightness of the fixture.
                //Got to turn those on.
                foreach (int t in _dimmerChannels)
                {
                    _dataPacket[t - 1] = 255;
                }

                InvokeRepeating(nameof(Sender), 0, TIME_BETWEEN_CALLS);
            }
            else
            {
                SingletonDestroy();
            }
        }

        public void UpdateDMXChannelNumbers()
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

        protected override void SingletonDestroy()
        {
            if (_sendClient == null) return;

            Debug.Log("(sACN Hardware Controller) Killing Sacn Controller...");

            MasterLightingController.OnFogState -= OnFogStateEvent;
            MasterLightingController.OnStrobeEvent -= OnStrobeEvent;
            MasterLightingController.OnBonusFXEvent -= OnBonusFXEvent;
            MasterLightingController.OnLightingEvent -= OnLightingEvent;
            StageKitInterpreter.OnLedEvent -= HandleLedEvent;

            // A good controller will also turn everything off after not receiving a packet after 2.5 seconds.
            // But this doesn't hurt to do.
            for (int i = 0; i < _dataPacket.Length; i++)
            {
                _dataPacket[i] = 0; //turn everything off
            }

            CancelInvoke(nameof(Sender));

            //force send final packet.
            Sender();

            _sendClient.Dispose();
            _sendClient = null;
        }

        private void OnApplicationQuit()
        {
            SingletonDestroy();
        }

        private void Sender()
        {
            // Hardcoded to universe 1, as this is for non-professional use, I doubt anyone is running multiple universes.
            // Didn't want to confuse the user with settings for something they don't need. However, it's a simple change
            // if needed. Same goes for sending multicast vs singlecast. Sacn spec says multicast is the correct default
            // way to go but singlecast can be used if needed.
            _sendClient.SendMulticast(1, _dataPacket);
            // Debug.Log(_dataPacket[0]+ " " + _dataPacket[1]+ " " + _dataPacket[2]+ " " + _dataPacket[3]+ " " + _dataPacket[4]+ " " + _dataPacket[5]+ " " + _dataPacket[6]+ " " + _dataPacket[7]);
        }

        private void HandleLedEvent(StageKitLedColor color, byte led)
        {
            var colors = new List<int[]>();

            if ((color & StageKitLedColor.Blue) != 0)
            {
                colors.Add(_blueChannels);
            }

            if ((color & StageKitLedColor.Green) != 0)
            {
                colors.Add(_greenChannels);
            }

            if ((color & StageKitLedColor.Red) != 0)
            {
                colors.Add(_redChannels);
            }

            if ((color & StageKitLedColor.Yellow) != 0)
            {
                colors.Add(_yellowChannels);
            }

            foreach (int[] colorByte in colors)
            {
                for (int i = 0; i < 8; i++)
                {
                    byte bitmask = (byte) (1 << i);
                    bool isBitSet = (led & bitmask) != 0;
                    _dataPacket[colorByte[i] - 1] = isBitSet ? (byte) 255 : (byte) 0;
                }
            }
        }
    }
}
/*
    "If you ever drop your keys into a river of molten lava, let'em go...because man, they're gone.'

    - Jack Handey.
*/