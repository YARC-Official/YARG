using System;
using PlasticBand.Haptics;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Integration.StageKit;
using YARG.Settings;

namespace YARG.Integration.Sacn
{
    public class SacnInterpreter : MonoSingleton<SacnInterpreter>
    {
        // This interpreter basically has two sub-interpreters.
        // First is the Stage Kit Interpreter part which sets the 'basic' DMX channels to whatever the Stage Kit is doing.
        // Second is 'advanced' channels. This is more for external programs such as LightJams, QLC+, etc. to know what
        // YARG is doing and to be able to react to it.

        private enum LedEnum
        {
            Off = 0,
            On = 255,
        }

        private enum FogEnum
        {
            Off = 0,
            On = 255,
        }

        private enum StrobeEnum
        {
            Off = 0,
            Slow = 64,
            Medium = 127,
            Fast = 191,
            Fastest = 255,
        }

        private enum CueEnum
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
            BlackoutFast = 190,
            BlackoutSlow = 200,
            BlackoutSpotlight = 210,
            FlareSlow = 220,
            FlareFast = 230,
            BigRockEnding = 240,
        }

        private enum BeatlineEnum
        {
            Off = 0,
            Measure = 1,
            Strong = 11,
        }

        private enum BonusEffectEnum
        {
            Off = 0,
            BonusEffect = 2,
        }

        private enum KeyFrameCueEnum
        {
            Off = 0,
            KeyframeNext = 3,
            KeyframePrevious = 13,
            KeyframeFirst = 23,
        }

        private enum PostProcessingTypeEnum
        {
            Default = 0,

            // Basic effects
            Bloom = 4,
            Bright = 14,
            Contrast = 24,
            Mirror = 34,
            PhotoNegative = 44,
            Posterize = 54,

            // Color filters/effects
            BlackAndWhite = 64,
            SepiaTone = 74,
            SilverTone = 84,
            ChoppyBlackAndWhite = 94,
            PhotoNegativeRedAndBlack = 104,
            PolarizedBlackAndWhite = 114,
            PolarizedRedAndBlue = 124,
            DesaturatedRed = 134,
            DesaturatedBlue = 144,
            ContrastRed = 154,
            ContrastGreen = 164,
            ContrastBlue = 174,

            // Grainy
            GrainyFilm = 184,
            GrainyChromaticAbberation = 194,
            // Scanlines
            Scanlines = 204,
            ScanlinesBlackAndWhite = 214,
            ScanlinesBlue = 224,
            ScanlinesSecurity = 234,

            // Trails
            Trails = 244,
            TrailsLong = 252,
            TrailsDesaturated = 253,
            TrailsFlickery = 254,
            TrailsSpacey = 255,
        }

        private enum PerformerEnum
        {
            Off = 0,
            //Type

            Spotlight = 1,
            Singalong = 2,

            // Performer
            Guitar = 4,
            Bass = 8,
            Drums = 16,
            Vocals = 32,
            Keyboard = 64,
        }

        public static event Action<int, byte> OnChannelSet;

        private byte _cueValue;

        // Basic DMX channels
        // 8 per color to match the stageKit layout. Default channels, the user must change them in settings.
        public int[] DimmerChannels;
        public int[] RedChannels;
        public int[] GreenChannels;
        public int[] BlueChannels;
        public int[] YellowChannels;
        public int[] FogChannels;
        public int[] StrobeChannels;

        // Advanced DMX channels
        public int CueChangeChannel;
        public int KeyframeChannel;
        public int BeatlineChannel;
        public int BonusEffectChannel;
        public int PostProcessingChannel;
        public int PerformerChannel;

        // Instrument DMX channels
        public int DrumChannel;
        public int GuitarChannel;
        public int BassChannel;
        public int KeysChannel;
        //Currently no advanced vocals channel as it doesn't seem needed.

        public void Start()
        {
            ManageEventSubscription(true);

            AllChannelsOff();

            //Many DMX fixtures have a 'Master dimmer' channel that controls the overall brightness of the fixture.
            //Got to turn those on.
            for (int i = 0; i < 8; i++)
            {
                SetChannel(DimmerChannels[i], (byte)SettingsManager.Settings.DMXDimmerValues.Value[i]);
            }

            //Since the master controller comes up first, we miss its events until now.
            OnLightingEvent(MasterLightingController.CurrentLightingCue);

        }

        private void ManageEventSubscription(bool subscribe)
        {
            if (subscribe)
            {
                // Basic
                StageKitInterpreter.OnLedEvent += HandleLedEvent;

                // Advanced
                MasterLightingController.OnFogState += OnFogStateEvent;
                MasterLightingController.OnStrobeEvent += OnStrobeEvent;
                MasterLightingController.OnBonusFXEvent += OnBonusFXEvent;
                MasterLightingController.OnLightingEvent += OnLightingEvent;
                MasterLightingController.OnBeatLineEvent += OnBeatLineEvent;
                MasterLightingController.OnPostProcessing += OnPostProcessing;
                MasterLightingController.OnPerformerEvent += OnPerformersEvent;

                //Instruments
                MasterLightingController.OnInstrumentEvent += OnInstrumentEvent;
            }
            else
            {
                // Basic
                StageKitInterpreter.OnLedEvent -= HandleLedEvent;

                // Advanced
                MasterLightingController.OnFogState -= OnFogStateEvent;
                MasterLightingController.OnStrobeEvent -= OnStrobeEvent;
                MasterLightingController.OnBonusFXEvent -= OnBonusFXEvent;
                MasterLightingController.OnLightingEvent -= OnLightingEvent;
                MasterLightingController.OnBeatLineEvent -= OnBeatLineEvent;
                MasterLightingController.OnPostProcessing -= OnPostProcessing;
                MasterLightingController.OnPerformerEvent -= OnPerformersEvent;

                //Instruments
                MasterLightingController.OnInstrumentEvent -= OnInstrumentEvent;
            }
        }

        private void AllChannelsOff()
        {
            //Set all channels to off
            //Basic
            foreach (var t in FogChannels)
            {
                SetChannel(t, (byte) LedEnum.Off);
            }

            foreach (var t in StrobeChannels)
            {
                SetChannel(t, (byte) LedEnum.Off);
            }

            foreach (var t in BlueChannels)
            {
                SetChannel(t, (byte) LedEnum.Off);
            }

            foreach (var t in GreenChannels)
            {
                SetChannel(t, (byte) LedEnum.Off);
            }

            foreach (var t in RedChannels)
            {
                SetChannel(t, (byte) LedEnum.Off);
            }

            foreach (var t in YellowChannels)
            {
                SetChannel(t, (byte) LedEnum.Off);
            }

            //Advanced
            SetChannel(CueChangeChannel, (byte) CueEnum.NoCue);
            SetChannel(KeyframeChannel, (byte) KeyFrameCueEnum.Off);
            SetChannel(BeatlineChannel, (byte) BeatlineEnum.Off);
            SetChannel(BonusEffectChannel, (byte) BonusEffectEnum.Off);
            SetChannel(PostProcessingChannel, (byte) PostProcessingTypeEnum.Default);
            //NYI
            //SetChannel(_performerChannel, (byte) PerformerEnum.Off);

            //Instruments
            SetChannel(KeysChannel, (byte) FogEnum.Off);
            SetChannel(DrumChannel, (byte) FogEnum.Off);
            SetChannel(GuitarChannel, (byte) FogEnum.Off);
            SetChannel(BassChannel, (byte) FogEnum.Off);
        }

        private void OnFogStateEvent(MasterLightingController.FogState fogState)
        {
            if (fogState == MasterLightingController.FogState.On)
            {
                foreach (var t in FogChannels)
                {
                    SetChannel(t, (byte) FogEnum.On);
                }
            }
            else
            {
                foreach (var t in FogChannels)
                {
                    SetChannel(t, (byte) FogEnum.Off);
                }
            }
        }

        private void OnStrobeEvent(StageKitStrobeSpeed value)
        {
            // TODO: I'm honestly just guessing at these values. I don't have a DMX strobe light to test with
            // and don't know if every DMX strobe light uses the same values the same way.
            switch (value)
            {
                case StageKitStrobeSpeed.Off:
                    foreach (var t in StrobeChannels)
                    {
                        SetChannel(t, (byte) StrobeEnum.Off);
                    }
                    break;

                case StageKitStrobeSpeed.Slow:
                    foreach (var t in StrobeChannels)
                    {
                        SetChannel(t, (byte) StrobeEnum.Slow);
                    }
                    break;

                case StageKitStrobeSpeed.Medium:
                    foreach (var t in StrobeChannels)
                    {
                        SetChannel(t, (byte) StrobeEnum.Medium);
                    }
                    break;

                case StageKitStrobeSpeed.Fast:
                    foreach (var t in StrobeChannels)
                    {
                        SetChannel(t, (byte) StrobeEnum.Fast);
                    }
                    break;

                case StageKitStrobeSpeed.Fastest:
                    foreach (var t in StrobeChannels)
                    {
                        SetChannel(t, (byte) StrobeEnum.Fastest);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        private void OnBonusFXEvent()
        {
            SetChannel(BonusEffectChannel, (byte) BonusEffectEnum.BonusEffect);
        }

        private void OnPerformersEvent(PerformerEvent newEvent)
        {
            //TODO: Once YARG parses the PerformerEvent, this will need to be updated/changed to the master gameplay controller
            if (newEvent == null)
            {
                SetChannel(PerformerChannel, (byte) PerformerEnum.Off);
                return;
            }

            byte perf = 0;

            switch (newEvent.Type)
            {
                case PerformerEventType.Singalong:
                    perf += (int) PerformerEnum.Singalong;
                    break;

                case PerformerEventType.Spotlight:
                    perf += (int) PerformerEnum.Spotlight;
                    break;
            }

            switch (newEvent.Performers)
            {
                case Performer.Guitar:
                    perf += (int) PerformerEnum.Guitar;
                    break;

                case Performer.Bass:
                    perf += (int) PerformerEnum.Bass;
                    break;

                case Performer.Drums:
                    perf += (int) PerformerEnum.Drums;
                    break;

                case Performer.Vocals:
                    perf += (int) PerformerEnum.Vocals;
                    break;

                case Performer.Keyboard:
                    perf += (int) PerformerEnum.Keyboard;
                    break;
            }

            SetChannel(PerformerChannel, perf);
        }

        private void OnInstrumentEvent(MasterLightingController.InstrumentType instrument, int notesHit)
        {
            switch (instrument)
            {
                case MasterLightingController.InstrumentType.Keys:
                    SetChannel(KeysChannel, (byte) notesHit);
                    break;

                case MasterLightingController.InstrumentType.Drums:
                    SetChannel(DrumChannel, (byte) notesHit);
                    break;

                case MasterLightingController.InstrumentType.Guitar:
                    SetChannel(GuitarChannel, (byte) notesHit);
                    break;

                case MasterLightingController.InstrumentType.Bass:
                    SetChannel(BassChannel, (byte) notesHit);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(instrument), instrument, null);
            }
        }

        private void OnBeatLineEvent(Beatline newBeatline)
        {
            if (newBeatline.Type == BeatlineType.Measure)
            {
                SetChannel(BeatlineChannel, (int) BeatlineEnum.Measure);
            }

            if (newBeatline.Type == BeatlineType.Strong)
            {
                SetChannel(BeatlineChannel, (int) BeatlineEnum.Strong);
            }
        }

        private void OnLightingEvent(LightingEvent newType)
        {
            SetCueChannel(newType?.Type);
        }

        private void OnPostProcessing(PostProcessingEvent newType)
        {
            if (newType == null)
            {
                return;
            }

            var postProcessingType = newType.Type switch
            {
                PostProcessingType.Bloom                     => (byte) PostProcessingTypeEnum.Bloom,
                PostProcessingType.Bright                    => (byte) PostProcessingTypeEnum.Bright,
                PostProcessingType.Contrast                  => (byte) PostProcessingTypeEnum.Contrast,
                PostProcessingType.Mirror                    => (byte) PostProcessingTypeEnum.Mirror,
                PostProcessingType.PhotoNegative             => (byte) PostProcessingTypeEnum.PhotoNegative,
                PostProcessingType.Posterize                 => (byte) PostProcessingTypeEnum.Posterize,
                PostProcessingType.BlackAndWhite             => (byte) PostProcessingTypeEnum.BlackAndWhite,
                PostProcessingType.SepiaTone                 => (byte) PostProcessingTypeEnum.SepiaTone,
                PostProcessingType.SilverTone                => (byte) PostProcessingTypeEnum.SilverTone,
                PostProcessingType.Choppy_BlackAndWhite      => (byte) PostProcessingTypeEnum.ChoppyBlackAndWhite,
                PostProcessingType.PhotoNegative_RedAndBlack => (byte) PostProcessingTypeEnum.PhotoNegativeRedAndBlack,
                PostProcessingType.Polarized_BlackAndWhite   => (byte) PostProcessingTypeEnum.PolarizedBlackAndWhite,
                PostProcessingType.Polarized_RedAndBlue      => (byte) PostProcessingTypeEnum.PolarizedRedAndBlue,
                PostProcessingType.Desaturated_Red           => (byte) PostProcessingTypeEnum.DesaturatedRed,
                PostProcessingType.Desaturated_Blue          => (byte) PostProcessingTypeEnum.DesaturatedBlue,
                PostProcessingType.Contrast_Red              => (byte) PostProcessingTypeEnum.ContrastRed,
                PostProcessingType.Contrast_Green            => (byte) PostProcessingTypeEnum.ContrastGreen,
                PostProcessingType.Contrast_Blue             => (byte) PostProcessingTypeEnum.ContrastBlue,
                PostProcessingType.Grainy_Film               => (byte) PostProcessingTypeEnum.GrainyFilm,
                PostProcessingType.Grainy_ChromaticAbberation =>
                    (byte) PostProcessingTypeEnum.GrainyChromaticAbberation,
                PostProcessingType.Scanlines               => (byte) PostProcessingTypeEnum.Scanlines,
                PostProcessingType.Scanlines_BlackAndWhite => (byte) PostProcessingTypeEnum.ScanlinesBlackAndWhite,
                PostProcessingType.Scanlines_Blue          => (byte) PostProcessingTypeEnum.ScanlinesBlue,
                PostProcessingType.Scanlines_Security      => (byte) PostProcessingTypeEnum.ScanlinesSecurity,
                PostProcessingType.Trails                  => (byte) PostProcessingTypeEnum.Trails,
                PostProcessingType.Trails_Long             => (byte) PostProcessingTypeEnum.TrailsLong,
                PostProcessingType.Trails_Desaturated      => (byte) PostProcessingTypeEnum.TrailsDesaturated,
                PostProcessingType.Trails_Flickery         => (byte) PostProcessingTypeEnum.TrailsFlickery,
                PostProcessingType.Trails_Spacey           => (byte) PostProcessingTypeEnum.TrailsSpacey,
                _                                          => (byte) PostProcessingTypeEnum.Default,
            };

            SetChannel(PostProcessingChannel, postProcessingType);
        }

        private void SetCueChannel(LightingType? newType)
        {
            if (newType != LightingType.Keyframe_Next && newType != LightingType.Keyframe_Previous &&
                newType != LightingType.Keyframe_First)
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
                    LightingType.Warm_Automatic        => (byte) CueEnum.WarmLoop,
                    LightingType.Warm_Manual           => (byte) CueEnum.WarmManual,
                    LightingType.BigRockEnding         => (byte) CueEnum.BigRockEnding,
                    null                               => (byte) CueEnum.NoCue,
                    _                                  => (byte) CueEnum.NoCue,
                };

                SetChannel(CueChangeChannel, _cueValue);
            }
            else
            {
                switch (newType)
                {
                    case LightingType.Keyframe_Next:
                        SetChannel(KeyframeChannel, (byte) KeyFrameCueEnum.KeyframeNext);
                        break;
                    case LightingType.Keyframe_Previous:
                        SetChannel(KeyframeChannel, (byte) KeyFrameCueEnum.KeyframePrevious);
                        break;
                    case LightingType.Keyframe_First:
                        SetChannel(KeyframeChannel, (byte) KeyFrameCueEnum.KeyframeFirst);
                        break;
                }
            }
        }

        private void OnApplicationQuit()
        {
            ManageEventSubscription(false);
        }

        private void HandleLedEvent(StageKitLedColor color, byte led)
        {
            if ((color & StageKitLedColor.Blue) != 0)
            {
                SetLedBits(BlueChannels, led);
            }

            if ((color & StageKitLedColor.Green) != 0)
            {
                SetLedBits(GreenChannels, led);
            }

            if ((color & StageKitLedColor.Red) != 0)
            {
                SetLedBits(RedChannels, led);
            }

            if ((color & StageKitLedColor.Yellow) != 0)
            {
                SetLedBits(YellowChannels, led);
            }
        }

        private void SetLedBits(int[] colorChannel, byte led)
        {
            for (int i = 0; i < 8; i++)
            {
                byte bitmask = (byte) (1 << i);
                bool isBitSet = (led & bitmask) != 0;
                SetChannel(colorChannel[i], isBitSet ? (byte) LedEnum.On : (byte) LedEnum.Off);
            }
        }

        private static void SetChannel(int channel, byte value)
        {
            OnChannelSet?.Invoke(channel, value);
        }
    }
}
