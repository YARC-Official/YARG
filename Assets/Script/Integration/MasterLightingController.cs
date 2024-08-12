using System;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Integration
{
    /*
        Real-life lighting integration works in 2 parts:
        1) This lighting controller (along with its gameplay monitor and initializer) builds and sends a data packet of whatever YARG is currently doing, to the network.
        2) YALCY reads this data packet and converts to various protocols (DMX, stage kit, etc) to control the lights.
   */

    public class MasterLightingController : MonoBehaviour
    {
        private enum ByteIndex
        {
            //header
            HeaderByte1,
            HeaderByte2,
            HeaderByte3,
            HeaderByte4,
            //Tech info
            DatagramVersion,
            Platform,
            //game info
            CurrentScene,
            PauseState,
            VenueSize,
            //song info
            BeatsPerMinute,
            SongSection,
            //instruments
            GuitarNotes,
            BassNotes,
            DrumsNotes,
            KeysNotes,
            VocalsNote,
            Harmony0Note,
            Harmony1Note,
            Harmony2Note,
            // Lighting information
            LightingCue,
            PostProcessing,
            FogState,
            StrobeState,
            Performer,
            Beat,
            Keyframe,
            BonusEffect,
        }

        public enum VocalHarmonyBytes
        {
            None = 0,
            Unpitched = 255,
            C6 = 84,
            B5 = 83,
            Bb5 = 82,
            A5 = 81,
            GSharp5 = 80, // G#5
            G5 = 79,
            FSharp5 = 78, // F#5
            F5 = 77,
            E5 = 76,
            Eb5 = 75,
            D5 = 74,
            CSharp5 = 73, // C#5
            C5 = 72,
            B4 = 71,
            Bb4 = 70,
            A4 = 69,
            GSharp4 = 68, // G#4
            G4 = 67,
            FSharp4 = 66, // F#4
            F4 = 65,
            E4 = 64,
            Eb4 = 63,
            D4 = 62,
            CSharp4 = 61, // C#4
            C4 = 60,
            B3 = 59,
            Bb3 = 58,
            A3 = 57,
            GSharp3 = 56, // G#3
            G3 = 55,
            FSharp3 = 54, // F#3
            F3 = 53,
            E3 = 52,
            Eb3 = 51,
            D3 = 50,
            CSharp3 = 49, // C#3
            C3 = 48,
            B2 = 47,
            Bb2 = 46,
            A2 = 45,
            GSharp2 = 44, // G#2
            G2 = 43,
            FSharp2 = 42, // F#2
            F2 = 41,
            E2 = 40,
            Eb2 = 39,
            D2 = 38,
            CSharp2 = 37, // C#2
            C2 = 36
        }

        private enum HeaderBytes
        {
            HeaderByte1 = 0x59, // Y
            HeaderByte2 = 0x41, // A
            HeaderByte3 = 0x52, // R
            HeaderByte4 = 0x47, // G
        }

        private enum DatagramVersionByte
        {
            Version = 0,
        }

        private enum PlatformByte
        {
            Unknown,
            Windows,
            Linux,
            Mac,
        }

        private enum SceneIndexByte
        {
            Unknown,
            Menu,
            Gameplay,
            Score,
            Calibration,
        }

        private enum PauseByte
        {
            Unpaused,
            Paused,
        }

        private enum VenueSizeByte
        {
            NoVenue,
            Small,
            Large,
        }

        private enum CueByte
        {
            NoCue = 0,
            Menu = 10,
            Score = 20,
            Intro = 30,
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

        private enum PostProcessingByte
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

        public enum FogState
        {
            Off,
            On,
        }

        public enum StrobeSpeedByte
        {
            Off,
            Slow,
            Medium,
            Fast,
            Fastest,
        }

        private enum BeatByte
        {
            Off,
            Measure,
            Strong,
            Weak,
        }

        private enum KeyFrameCueEByte
        {
            Off,
            KeyframeNext,
            KeyframePrevious,
            KeyframeFirst,
        }

        private enum BonusEffectByte
        {
            Off,
            On,
        }

        private enum SongSectionByte
        {
            None,
            Verse,
            Chorus,
        }

        private static readonly IPAddress
            IPAddress = IPAddress.Parse("255.255.255.255"); // "this" network's broadcast address
        private const int PORT = 36107;                     // Just punched some keys on the keyboard
        private static UdpClient _sendClient = new();

        //Has to be at least 44 because of DMX, 88 should be enough... for now...
        private const float TARGET_FPS = 88f;
        private const float TIME_BETWEEN_CALLS = 1f / TARGET_FPS;
        private static Timer _timer;

        private static byte[] _dataPacket = new byte[Enum.GetValues(typeof(ByteIndex)).Length];

        public static bool Paused
        {
            set =>
                _dataPacket[(int) ByteIndex.PauseState] = value ? (byte) PauseByte.Paused : (byte) PauseByte.Unpaused;
        }

        public static bool LargeVenue
        {
            set =>
                _dataPacket[(int) ByteIndex.VenueSize] =
                    value ? (byte) VenueSizeByte.Large : (byte) VenueSizeByte.Small;
        }

        public static PostProcessingEvent CurrentPostProcessing
        {
            set
            {
                _dataPacket[(int) ByteIndex.PostProcessing] = value.Type switch
                {
                    PostProcessingType.Default                   => (byte) PostProcessingByte.Default,
                    PostProcessingType.Bloom                     => (byte) PostProcessingByte.Bloom,
                    PostProcessingType.Bright                    => (byte) PostProcessingByte.Bright,
                    PostProcessingType.Contrast                  => (byte) PostProcessingByte.Contrast,
                    PostProcessingType.Mirror                    => (byte) PostProcessingByte.Mirror,
                    PostProcessingType.PhotoNegative             => (byte) PostProcessingByte.PhotoNegative,
                    PostProcessingType.Posterize                 => (byte) PostProcessingByte.Posterize,
                    PostProcessingType.BlackAndWhite             => (byte) PostProcessingByte.BlackAndWhite,
                    PostProcessingType.SepiaTone                 => (byte) PostProcessingByte.SepiaTone,
                    PostProcessingType.SilverTone                => (byte) PostProcessingByte.SilverTone,
                    PostProcessingType.Choppy_BlackAndWhite      => (byte) PostProcessingByte.ChoppyBlackAndWhite,
                    PostProcessingType.PhotoNegative_RedAndBlack => (byte) PostProcessingByte.PhotoNegativeRedAndBlack,
                    PostProcessingType.Polarized_BlackAndWhite   => (byte) PostProcessingByte.PolarizedBlackAndWhite,
                    PostProcessingType.Polarized_RedAndBlue      => (byte) PostProcessingByte.PolarizedRedAndBlue,
                    PostProcessingType.Desaturated_Red           => (byte) PostProcessingByte.DesaturatedRed,
                    PostProcessingType.Desaturated_Blue          => (byte) PostProcessingByte.DesaturatedBlue,
                    PostProcessingType.Contrast_Red              => (byte) PostProcessingByte.ContrastRed,
                    PostProcessingType.Contrast_Green            => (byte) PostProcessingByte.ContrastGreen,
                    PostProcessingType.Contrast_Blue             => (byte) PostProcessingByte.ContrastBlue,
                    PostProcessingType.Grainy_Film               => (byte) PostProcessingByte.GrainyFilm,
                    PostProcessingType.Grainy_ChromaticAbberation =>
                        (byte) PostProcessingByte.GrainyChromaticAbberation,
                    PostProcessingType.Scanlines               => (byte) PostProcessingByte.Scanlines,
                    PostProcessingType.Scanlines_BlackAndWhite => (byte) PostProcessingByte.ScanlinesBlackAndWhite,
                    PostProcessingType.Scanlines_Blue          => (byte) PostProcessingByte.ScanlinesBlue,
                    PostProcessingType.Scanlines_Security      => (byte) PostProcessingByte.ScanlinesSecurity,
                    PostProcessingType.Trails                  => (byte) PostProcessingByte.Trails,
                    PostProcessingType.Trails_Long             => (byte) PostProcessingByte.TrailsLong,
                    PostProcessingType.Trails_Desaturated      => (byte) PostProcessingByte.TrailsDesaturated,
                    PostProcessingType.Trails_Flickery         => (byte) PostProcessingByte.TrailsFlickery,
                    PostProcessingType.Trails_Spacey           => (byte) PostProcessingByte.TrailsSpacey,
                    _                                          => (byte) PostProcessingByte.Default,
                };
            }
        }

        //public static PerformerEvent CurrentPerformerEvent;

        public static int CurrentGuitarNotes
        {
            set => _dataPacket[(int) ByteIndex.GuitarNotes] = (byte) value;
        }

        public static int CurrentBassNotes
        {
            set => _dataPacket[(int) ByteIndex.BassNotes] = (byte) value;
        }

        public static int CurrentDrumNotes
        {
            set => _dataPacket[(int) ByteIndex.DrumsNotes] = (byte) value;
        }

        public static int CurrentKeysNotes
        {
            set => _dataPacket[(int) ByteIndex.KeysNotes] = (byte) value;
        }

        public static int CurrentVocalNote
        {
            set => _dataPacket[(int) ByteIndex.VocalsNote] = (byte) value;
        }

        public static int CurrentHarmony0Note
        {
            set => _dataPacket[(int) ByteIndex.Harmony0Note] = (byte) value;
        }

        public static int CurrentHarmony1Note
        {
            set => _dataPacket[(int) ByteIndex.Harmony1Note] = (byte) value;
        }

        public static int CurrentHarmony2Note
        {
            set => _dataPacket[(int) ByteIndex.Harmony2Note] = (byte) value;
        }

        public static int CurrentSongSection
        {
            set => _dataPacket[(int) ByteIndex.SongSection] = (byte) value;
        }

        public static FogState CurrentFogState
        {
            set => _dataPacket[(int) ByteIndex.FogState] = (byte) value;
        }

        public static StageKitStrobeSpeed CurrentStrobeState
        {
            set => _dataPacket[(int) ByteIndex.StrobeState] = (byte) value;
        }

        public static byte CurrentBPM
        {
            set => _dataPacket[(int) ByteIndex.BeatsPerMinute] = value;
        }

        public static Beatline CurrentBeat
        {
            set =>
                _dataPacket[(int) ByteIndex.Beat] = value.Type switch
                {
                    BeatlineType.Measure => (byte) BeatByte.Measure,
                    BeatlineType.Strong  => (byte) BeatByte.Strong,
                    BeatlineType.Weak    => (byte) BeatByte.Weak,
                    _                    => (byte) BeatByte.Off,
                };
        }

        public static LightingEvent CurrentLightingCue
        {
            set
            {
                //Keyframes are indicators and not really lighting cues themselves, also chorus and verse act more and modifiers and section labels and also not really lighting cues
                if (value.Type != LightingType.Keyframe_Next && value.Type != LightingType.Keyframe_Previous &&
                    value.Type != LightingType.Keyframe_First && value.Type != LightingType.Chorus &&
                    value.Type != LightingType.Verse)
                {
                    _dataPacket[(int) ByteIndex.LightingCue] = value.Type switch
                    {
                        LightingType.Default               => (byte) CueByte.Default,
                        LightingType.Dischord              => (byte) CueByte.Dischord,
                        LightingType.Frenzy                => (byte) CueByte.Frenzy,
                        LightingType.Harmony               => (byte) CueByte.Harmony,
                        LightingType.Intro                 => (byte) CueByte.Intro,
                        LightingType.Menu                  => (byte) CueByte.Menu,
                        LightingType.Score                 => (byte) CueByte.Score,
                        LightingType.Silhouettes           => (byte) CueByte.Silhouettes,
                        LightingType.Silhouettes_Spotlight => (byte) CueByte.SilhouettesSpotlight,
                        LightingType.Sweep                 => (byte) CueByte.Sweep,
                        LightingType.Searchlights          => (byte) CueByte.Searchlights,
                        LightingType.Stomp                 => (byte) CueByte.Stomp,
                        LightingType.Blackout_Fast         => (byte) CueByte.BlackoutFast,
                        LightingType.Blackout_Slow         => (byte) CueByte.BlackoutSlow,
                        LightingType.Blackout_Spotlight    => (byte) CueByte.BlackoutSpotlight,
                        LightingType.Cool_Automatic        => (byte) CueByte.CoolLoop,
                        LightingType.Cool_Manual           => (byte) CueByte.CoolManual,
                        LightingType.Flare_Fast            => (byte) CueByte.FlareFast,
                        LightingType.Flare_Slow            => (byte) CueByte.FlareSlow,
                        LightingType.Warm_Automatic        => (byte) CueByte.WarmLoop,
                        LightingType.Warm_Manual           => (byte) CueByte.WarmManual,
                        LightingType.BigRockEnding         => (byte) CueByte.BigRockEnding,
                        _                                  => (byte) CueByte.NoCue,
                    };
                }
                else if (value.Type is LightingType.Keyframe_Next or LightingType.Keyframe_Previous
                    or LightingType.Keyframe_First)
                {
                    _dataPacket[(int) ByteIndex.Keyframe] = value.Type switch
                    {
                        LightingType.Keyframe_Next     => (byte) KeyFrameCueEByte.KeyframeNext,
                        LightingType.Keyframe_Previous => (byte) KeyFrameCueEByte.KeyframePrevious,
                        LightingType.Keyframe_First    => (byte) KeyFrameCueEByte.KeyframeFirst,
                        _                              => _dataPacket[(int) ByteIndex.Keyframe]
                    };
                }
                else if (value.Type is LightingType.Verse or LightingType.Chorus)
                {
                    _dataPacket[(int) ByteIndex.SongSection] = value.Type switch
                    {
                        LightingType.Verse  => (byte) SongSectionByte.Verse,
                        LightingType.Chorus => (byte) SongSectionByte.Chorus,
                        _                   => _dataPacket[(int) ByteIndex.SongSection]
                    };
                }
            }
        }

        private void Start()
        {
            _dataPacket[(int) ByteIndex.HeaderByte1] = (byte) HeaderBytes.HeaderByte1;
            _dataPacket[(int) ByteIndex.HeaderByte2] = (byte) HeaderBytes.HeaderByte2;
            _dataPacket[(int) ByteIndex.HeaderByte3] = (byte) HeaderBytes.HeaderByte3;
            _dataPacket[(int) ByteIndex.HeaderByte4] = (byte) HeaderBytes.HeaderByte4;
            _dataPacket[(int) ByteIndex.DatagramVersion] = (byte) DatagramVersionByte.Version;
            _dataPacket[(int) ByteIndex.Platform] = SetPlatformByte();
            _dataPacket[(int) ByteIndex.Performer] = 0x00; //Performer not parsed yet

            _timer = new Timer(TIME_BETWEEN_CALLS * 1000);
            _timer.Elapsed += (sender, e) => Sender();
            _timer.Start();
        }

        private void Sender()
        {
            if (!SettingsManager.Settings.EnableYALCYDatastream.Value) return;
            try
            {
                _sendClient.Send(_dataPacket, _dataPacket.Length, IPAddress.ToString(), PORT);
                _dataPacket[(int) ByteIndex.BonusEffect] = (byte) BonusEffectByte.Off;
                _dataPacket[(int) ByteIndex.Keyframe] = (byte) KeyFrameCueEByte.Off;
                _dataPacket[(int) ByteIndex.Beat] = (byte) BeatByte.Off;
            }
            catch (Exception ex)
            {
                YargLogger.LogError($"Error sending UDP packet: {ex.Message}");
            }
        }

        private static byte SetPlatformByte()
        {
            var platform = Application.platform;
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return (byte) PlatformByte.Windows;

                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return (byte) PlatformByte.Mac;

                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return (byte) PlatformByte.Linux;

                default:
                    YargLogger.LogWarning("Running on an unknown platform");
                    return (byte) PlatformByte.Unknown;
            }
        }

        public static void FireBonusFXEvent()
        {
            _dataPacket[(byte) ByteIndex.BonusEffect] = (byte) BonusEffectByte.On;
        }

        public static void Initializer(Scene scene)
        {
            // Ignore the persistent scene
            if ((SceneIndex) scene.buildIndex == SceneIndex.Persistent) return;

            CurrentFogState = FogState.Off;
            CurrentStrobeState = StageKitStrobeSpeed.Off;
            CurrentBPM = 0;
            CurrentDrumNotes = 0;
            CurrentGuitarNotes = 0;
            CurrentKeysNotes = 0;
            CurrentBassNotes = 0;
            CurrentVocalNote = 0;
            CurrentHarmony0Note = 0;
            CurrentHarmony1Note = 0;
            CurrentHarmony2Note = 0;
            CurrentSongSection = 0;

            switch ((SceneIndex) scene.buildIndex)
            {
                case SceneIndex.Gameplay:
                    _dataPacket[(byte) ByteIndex.CurrentScene] = (byte) SceneIndexByte.Gameplay;
                    break;

                case SceneIndex.Menu:
                    CurrentLightingCue = new LightingEvent(LightingType.Menu, 0, 0);
                    _dataPacket[(byte) ByteIndex.CurrentScene] = (byte) SceneIndexByte.Menu;
                    break;

                case SceneIndex.Calibration:
                    _dataPacket[(byte) ByteIndex.CurrentScene] |= (byte) SceneIndexByte.Calibration;
                    break;

                case SceneIndex.Score:
                    CurrentLightingCue = new LightingEvent(LightingType.Score, 0, 0);
                    _dataPacket[(byte) ByteIndex.CurrentScene] |= (byte) SceneIndexByte.Score;
                    break;

                default:
                    YargLogger.LogWarning("Unknown Scene loaded!");
                    _dataPacket[(byte) ByteIndex.CurrentScene] |= (byte) SceneIndexByte.Unknown;
                    break;
            }
        }

        private void OnApplicationQuit()
        {
            YargLogger.LogInfo("Killing Lighting sender...");

            _timer?.Stop();
            _timer?.Dispose();

            if (_sendClient == null) return;

            Array.Clear(_dataPacket, 0, _dataPacket.Length);
            _dataPacket[(int) ByteIndex.HeaderByte1] = (byte) HeaderBytes.HeaderByte1;
            _dataPacket[(int) ByteIndex.HeaderByte2] = (byte) HeaderBytes.HeaderByte2;
            _dataPacket[(int) ByteIndex.HeaderByte3] = (byte) HeaderBytes.HeaderByte3;
            _dataPacket[(int) ByteIndex.HeaderByte4] = (byte) HeaderBytes.HeaderByte4;
            _sendClient.Send(_dataPacket, _dataPacket.Length, IPAddress.ToString(),
                PORT); //force send a blank packet to clear the lights
            Array.Clear(_dataPacket, 0, _dataPacket.Length);

            _sendClient.Dispose();
        }
    }
}
