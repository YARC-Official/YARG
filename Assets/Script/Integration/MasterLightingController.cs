using System;
using System.IO;
using System.Net.Sockets;
using System.Timers;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Integration
{
    public class MasterLightingController : MonoBehaviour
    {
        [Serializable]
        public struct LightingMessage
        {
            public uint Header;

            public byte DatagramVersion;
            public PlatformByte Platform;
            public SceneIndexByte CurrentScene;
            public PauseStateType Paused;
            public VenueType Venue;

            public float BeatsPerMinute;
            public LightingType CurrentSongSection;
            public int CurrentGuitarNotes;
            public int CurrentBassNotes;
            public int CurrentDrumNotes;
            public int CurrentKeysNotes;
            public float CurrentVocalNote;
            public float CurrentHarmony0Note;
            public float CurrentHarmony1Note;
            public float CurrentHarmony2Note;

            public LightingEvent LightingCue;
            public PostProcessingType PostProcessing;
            public bool FogState;
            public LightingType StrobeState;
            public byte Performer;
            public byte Beat;
            public LightingType Keyframe;
            public bool BonusEffect;
        }

        public enum PlatformByte
        {
            Unknown,
            Windows,
            Linux,
            Mac,
        }

        public enum SceneIndexByte
        {
            Unknown,
            Menu,
            Gameplay,
            Score,
            Calibration,
        }

        public enum VenueType
        {
            None,
            Small,
            Large,
        }

        public enum PauseStateType
        {
            AtMenu,
            Unpaused,
            Paused,
        }

        private static UdpClient _sendClient = new();
        //Has to be at least 44 because of DMX, 88 should be enough... for now...
        private const float TARGET_FPS = 88f;
        private const float TIME_BETWEEN_CALLS = 1f / TARGET_FPS;
        private const int HEADER_BYTE = 0x59415247;
        private const int DATAGRAM_VERSION = 0;
        private Timer _timer;
        private LightingMessage _message = new LightingMessage();
        private static LightingEvent _currentLightingCue;
        private static MemoryStream _ms = new MemoryStream();
        private static BinaryWriter _writer = new BinaryWriter(_ms);

        // NYI - waiting for parser rewrite.
        // public static PerformerEvent CurrentPerformerEvent;
        public static PlatformByte MLCPlatform;
        public static PauseStateType MLCPaused;
        public static VenueType MLCVenue;
        public static SceneIndexByte MLCSceneIndex;

        public static int MLCCurrentGuitarNotes;
        public static int MLCCurrentBassNotes;
        public static int MLCCurrentDrumNotes;
        public static int MLCCurrentKeysNotes;

        public static float MLCCurrentVocalNote;
        public static float MLCCurrentHarmony0Note;
        public static float MLCCurrentHarmony1Note;
        public static float MLCCurrentHarmony2Note;

        public static bool MLCBonusFX;
        public static LightingType MLCCurrentSongSection;
        public static bool MLCFogState;
        public static LightingType MLCStrobeState;
        public static float MLCCurrentBPM;
        public static byte MLCCurrentBeat;
        public static LightingType MLCKeyframe;
        public static LightingEvent MLCCurrentLightingCue;
        public static PostProcessingType MLCPostProcessing;

        public static ushort MLCudpPort;
        public static string MLCudpIP;

        // Save some allocations by setting this up here.
        private static LightingEvent MenuLightingCue = new(LightingType.Menu, 0, 0);
        private static LightingEvent ScoreLightingCue = new(LightingType.Score, 0, 0);
        private static LightingEvent NoLightingCue = new(LightingType.NoCue, 0, 0);

        private void Start()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            MLCPlatform = PlatformByte.Windows;
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			MLCPlatform = PlatformByte.Mac;
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
			MLCPlatform = PlatformByte.Linux;
#endif

            _timer = new Timer(TIME_BETWEEN_CALLS * 1000);
            _timer.Elapsed += (sender, e) => Sender(_message);
            _timer.Start();
        }

        public static void Sender(LightingMessage message)
        {
            message.Header = HEADER_BYTE; // Y A R G

            message.DatagramVersion = DATAGRAM_VERSION;         // version 0 currently
            message.Platform = MLCPlatform;                     // Set by the Preprocessor Directive above.
            message.CurrentScene = MLCSceneIndex;               // gets set by the initializer.
            message.Paused = MLCPaused;                         // gets set by the GameplayMonitor.
            message.Venue = MLCVenue;                           // gets set on chart load by the GameplayMonitor.
            message.BeatsPerMinute = MLCCurrentBPM;             // gets set by the GameplayMonitor.
            message.CurrentSongSection = MLCCurrentSongSection; // gets set on lighting cue change.

            message.CurrentGuitarNotes = MLCCurrentGuitarNotes; // gets set by the GameplayMonitor.
            message.CurrentBassNotes = MLCCurrentBassNotes;     // gets set by the GameplayMonitor.
            message.CurrentDrumNotes = MLCCurrentDrumNotes;     // gets set by the GameplayMonitor.
            message.CurrentKeysNotes = MLCCurrentKeysNotes;     // gets set by the GameplayMonitor.

            message.CurrentVocalNote = MLCCurrentVocalNote;       // gets set by the GameplayMonitor.
            message.CurrentHarmony0Note = MLCCurrentHarmony0Note; // gets set by the GameplayMonitor.
            message.CurrentHarmony1Note = MLCCurrentHarmony1Note; // gets set by the GameplayMonitor.
            message.CurrentHarmony2Note = MLCCurrentHarmony2Note; // gets set by the GameplayMonitor.

            message.LightingCue = MLCCurrentLightingCue; // setter triggered by the GameplayMonitor.
            message.PostProcessing = MLCPostProcessing;  // setter triggered by the GameplayMonitor.
            message.FogState = MLCFogState;              // gets set by the GameplayMonitor.
            message.StrobeState = MLCStrobeState;        // gets set by the GameplayMonitor.
            message.Performer = 0x00;                    // Performer not parsed yet
            message.Beat = MLCCurrentBeat;               // gets set by the GameplayMonitor.
            message.Keyframe = MLCKeyframe;              // gets set on lighting cue change.
            message.BonusEffect = MLCBonusFX;            // gets set by the GameplayMonitor.

            SerializeAndSend(message);

            // Reset the keyframe and section after sending
            // Honestly, this iS a bit of a hack to have it here.
            MLCKeyframe = 0;
            MLCCurrentBeat =
                3; // I'm using 3 here as 'off' for the beatline due to the BeatlineType enum. This also changes the casting of it.
            MLCBonusFX = false;
        }

        public static void Initializer(Scene scene)
        {
            // Ignore the persistent scene
            if ((SceneIndex) scene.buildIndex == SceneIndex.Persistent) return;

            MLCPaused = PauseStateType.AtMenu;
            MLCVenue = VenueType.None;
            MLCCurrentBPM = 0;
            MLCCurrentSongSection = 0;

            MLCCurrentGuitarNotes = 0;
            MLCCurrentBassNotes = 0;
            MLCCurrentDrumNotes = 0;
            MLCCurrentKeysNotes = 0;

            MLCCurrentVocalNote = 0;
            MLCCurrentHarmony0Note = 0;
            MLCCurrentHarmony1Note = 0;
            MLCCurrentHarmony2Note = 0;

            MLCPostProcessing = 0;
            MLCFogState = false;
            MLCStrobeState = LightingType.Strobe_Off;
            MLCCurrentBeat = 0;
            MLCKeyframe = 0;
            MLCBonusFX = false;

            MLCCurrentLightingCue = NoLightingCue;

            switch ((SceneIndex) scene.buildIndex)
            {
                case SceneIndex.Gameplay:
                    MLCSceneIndex = SceneIndexByte.Gameplay;
                    break;

                case SceneIndex.Menu:
                    MLCCurrentLightingCue = MenuLightingCue;
                    MLCSceneIndex = SceneIndexByte.Menu;
                    break;

                case SceneIndex.Calibration:
                    MLCSceneIndex = SceneIndexByte.Calibration;
                    break;

                case SceneIndex.Score:
                    MLCCurrentLightingCue = ScoreLightingCue;
                    MLCSceneIndex = SceneIndexByte.Score;
                    break;

                default:
                    YargLogger.LogWarning("Unknown Scene loaded!");
                    MLCSceneIndex = SceneIndexByte.Unknown;
                    break;
            }
        }

        private void OnApplicationQuit()
        {
            YargLogger.LogInfo("Killing Lighting sender...");

            _timer?.Stop();
            _timer?.Dispose();

            if (_sendClient == null) return;

            // force send a blank packet to turn everything off.

            _message = new LightingMessage
            {
                Header = HEADER_BYTE, // Y A R G
                // Everything else is 0 or off
            };

            SerializeAndSend(_message);

            _sendClient.Dispose();
        }

        private static void SerializeAndSend(LightingMessage message)
        {
            if (!SettingsManager.Settings.EnableYALCYDatastream.Value) return;

            try
            {
                // Reset the MemoryStream's position to the beginning
                _ms.Position = 0;

                _writer.Write(message.Header);          //uint
                _writer.Write(message.DatagramVersion); //byte

                _writer.Write((byte) message.Platform);
                _writer.Write((byte) message.CurrentScene);
                _writer.Write((byte) message.Paused);
                _writer.Write((byte) message.Venue);
                _writer.Write(message.BeatsPerMinute); //float

                _writer.Write((byte) message.CurrentSongSection);
                _writer.Write((byte) message.CurrentGuitarNotes); // While .Write can do an int, the instruments
                _writer.Write((byte) message.CurrentBassNotes);   // are only 5 to 8 bits, so might as well save space.
                _writer.Write((byte) message.CurrentDrumNotes);
                _writer.Write((byte) message.CurrentKeysNotes);

                _writer.Write(message.CurrentVocalNote);    //float
                _writer.Write(message.CurrentHarmony0Note); //float
                _writer.Write(message.CurrentHarmony1Note); //float
                _writer.Write(message.CurrentHarmony2Note); //float

                _writer.Write((byte) message.LightingCue.Type);
                _writer.Write((byte) message.PostProcessing);
                _writer.Write(message.FogState); //bool
                _writer.Write((byte) message.StrobeState);
                _writer.Write(message.Performer); //byte
                _writer.Write(message.Beat);      //byte
                _writer.Write((byte) message.Keyframe);
                _writer.Write(message.BonusEffect); //bool

                // Get the buffer and send the data with the correct length
                _sendClient.Send(_ms.GetBuffer(), (int) _ms.Position, MLCudpIP, MLCudpPort);
            }
            catch (Exception ex)
            {
                YargLogger.LogError($"Error sending UDP packet: {ex.Message}");
            }
        }
    }
}
