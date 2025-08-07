using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Integration
{
    public class DataStreamController :  MonoSingleton<DataStreamController>
    {
        [Serializable]
        public struct DataMessage
        {
            public uint Header;

            public byte           DatagramVersion;
            public PlatformByte   Platform;
            public SceneIndexByte CurrentScene;
            public PauseStateType Paused;
            public VenueType      VenueSize;

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

            public LightingType       LightingCue;
            public PostProcessingType PostProcessing;
            public bool               FogState;
            public LightingType       StrobeState;
            public byte               Performer;
            public byte               Beat;
            public LightingType       Keyframe;
            public bool               BonusEffect;
            public bool               AutoGenVenueTrack;
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
        private const  float         TARGET_FPS         = 88f;
        private const  float         TIME_BETWEEN_CALLS = 1f / TARGET_FPS;
        private        Timer         _timer;
        private        DataMessage   _message = new DataMessage();
        private static LightingEvent _currentLightingCue;

        // NYI - waiting for parser rewrite.
        // public static PerformerEvent CurrentPerformerEvent;
        public static PlatformByte   MLCPlatform;
        public static PauseStateType MLCPaused;
        public static VenueType      MLCVenueSize;
        public static SceneIndexByte MLCSceneIndex;

        public static int MLCCurrentGuitarNotes;
        public static int MLCCurrentBassNotes;
        public static int MLCCurrentDrumNotes;
        public static int MLCCurrentKeysNotes;

        public static float MLCCurrentVocalNote;
        public static float MLCCurrentHarmony0Note;
        public static float MLCCurrentHarmony1Note;
        public static float MLCCurrentHarmony2Note;

        public static bool               MLCBonusFX;
        public static LightingType       MLCCurrentSongSection;
        public static bool               MLCFogState;
        public static LightingType       MLCStrobeState;
        public static float              MLCCurrentBPM;
        public static byte               MLCCurrentBeat;
        public static LightingType       MLCKeyframe;
        public static LightingType       MLCCurrentLightingCue;
        public static PostProcessingType MLCPostProcessing;
        public static bool               MLCAutoGenVenueTrack;

        public static ushort MLCudpPort = 36107; //hardcoded for now.
        public static string MLCudpIP = "255.255.255.255"; // "this" network's broadcast address

        public static LightingEvent CurrentLightingCue
        {
            get => _currentLightingCue;

            set
            {
                // Could probably move this into the gameplay monitor

                // This is for the debug menu
                _currentLightingCue = value;

                //Keyframes are indicators and not really lighting cues themselves, also chorus and verse act more as modifiers and section labels and also not really lighting cues, they can be stacked under a lighting cue.
                if (value.Type is not (LightingType.KeyframeNext or LightingType.KeyframePrevious or LightingType.KeyframeFirst or LightingType.Chorus or LightingType.Verse))
                {
                    MLCCurrentLightingCue = value.Type;
                    // might need a null check here = NoCue, testing needed
                }
                else if (value.Type is LightingType.KeyframeNext or LightingType.KeyframePrevious
                    or LightingType.KeyframeFirst)
                {
                    MLCKeyframe = value.Type;
                    //might need an else here to keep keyframe at current value, testing needed
                }
                else if (value.Type is LightingType.Verse or LightingType.Chorus)
                {
                    MLCCurrentSongSection = value.Type;
                    //might need an else here to keep the section at current value, testing needed
                }
            }
        }

        // Datagram version history
        // v0 - inital release
        // v1 - added "HasVenueTrack?" byte. renamed 'venue' to 'venueSize'.
        public static void Sender(DataMessage message)
        {
            message.Header = 0x59415247; // Y A R G

            message.DatagramVersion = 1;                          // version 0 currently
            message.Platform = MLCPlatform;                       // Set by the Preprocessor Directive above.
            message.CurrentScene = MLCSceneIndex;                 // gets set by the initializer.
            message.Paused = MLCPaused;                           // gets set by the GameplayMonitor.
            message.VenueSize = MLCVenueSize;                     // gets set on chart load by the GameplayMonitor.
            message.BeatsPerMinute = MLCCurrentBPM;               // gets set by the GameplayMonitor.
            message.CurrentSongSection = MLCCurrentSongSection;   // gets set on lighting cue change.

            message.CurrentGuitarNotes = MLCCurrentGuitarNotes;   // gets set by the GameplayMonitor.
            message.CurrentBassNotes = MLCCurrentBassNotes;       // gets set by the GameplayMonitor.
            message.CurrentDrumNotes = MLCCurrentDrumNotes;       // gets set by the GameplayMonitor.
            message.CurrentKeysNotes = MLCCurrentKeysNotes;       // gets set by the GameplayMonitor.

            message.CurrentVocalNote = MLCCurrentVocalNote;       // gets set by the GameplayMonitor.
            message.CurrentHarmony0Note = MLCCurrentHarmony0Note; // gets set by the GameplayMonitor.
            message.CurrentHarmony1Note = MLCCurrentHarmony1Note; // gets set by the GameplayMonitor.
            message.CurrentHarmony2Note = MLCCurrentHarmony2Note; // gets set by the GameplayMonitor.

            message.LightingCue = MLCCurrentLightingCue;        // setter triggered by the GameplayMonitor.
            message.PostProcessing = MLCPostProcessing;         // setter triggered by the GameplayMonitor.
            message.FogState = MLCFogState;                     // gets set by the GameplayMonitor.
            message.StrobeState = MLCStrobeState;               // gets set by the GameplayMonitor.
            message.Performer = 0x00;                           // Performer isn't parsed yet
            message.Beat = MLCCurrentBeat;                      // gets set by the GameplayMonitor.
            message.Keyframe = MLCKeyframe;                     // gets set on lighting cue change.
            message.BonusEffect = MLCBonusFX;                   // gets set by the GameplayMonitor.

            message.AutoGenVenueTrack = MLCAutoGenVenueTrack;   // gets set on chart load by the GameplayMonitor.

            SerializeAndSend(message);

            // Reset the keyframe and section after sending
            // Honestly, this iS a bit of a hack to have it here.
            MLCKeyframe = 0;
            MLCCurrentBeat = 3; // I'm using 3 here as 'off' for the beatline due to the BeatlineType enum. This also changes the casting of it.
            MLCBonusFX = false;
        }

        public void HandleEnabledChanged(bool isEnabled)
        {
            if (isEnabled)
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                MLCPlatform = PlatformByte.Windows;
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			MLCPlatform = PlatformByte.Mac;
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
			MLCPlatform = PlatformByte.Linux;
#endif
                Initializer(SceneManager.GetActiveScene());
                _sendClient = new();
                _sendClient.Connect(new IPEndPoint(IPAddress.Parse(MLCudpIP), MLCudpPort));
                // start the sending timer
                _timer = new Timer(TIME_BETWEEN_CALLS * 1000);
                _timer.Elapsed += (sender, e) => Sender(_message);
                _timer.Start();

            }
            else
            {
                _sendClient?.Dispose();
                _timer?.Stop();
                _timer?.Dispose();
            }
        }
        public static void Initializer(Scene scene)
        {
            // Ignore the persistent scene
            if ((SceneIndex) scene.buildIndex == SceneIndex.Persistent) return;

            MLCPaused = PauseStateType.AtMenu;
            MLCVenueSize = VenueType.None;
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
            MLCStrobeState = LightingType.StrobeOff;
            MLCCurrentBeat = 0;
            MLCKeyframe = 0;
            MLCBonusFX = false;


            switch ((SceneIndex) scene.buildIndex)
            {
                case SceneIndex.Gameplay:
                    MLCSceneIndex = SceneIndexByte.Gameplay;
                    break;

                case SceneIndex.Menu:
                    CurrentLightingCue = new LightingEvent(LightingType.Menu, 0, 0);
                    MLCSceneIndex = SceneIndexByte.Menu;
                    break;

                case SceneIndex.Calibration:
                    MLCSceneIndex = SceneIndexByte.Calibration;
                    break;

                case SceneIndex.Score:
                    CurrentLightingCue = new LightingEvent(LightingType.Score, 0, 0);
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
            YargLogger.LogInfo("Killing Data Stream sender...");

            _timer?.Stop();
            _timer?.Dispose();

            if (_sendClient == null) return;

            // force send a blank packet to turn everything off.

            _message = new DataMessage
            {
                Header = 0x59415247, // Y A R G
                // Everything else is 0 or off
            };

            SerializeAndSend(_message);

            _sendClient.Dispose();
        }

        private static void SerializeAndSend(DataMessage message)
        {
            try
            {
                using var _ms = new MemoryStream(64);
                using var _writer = new BinaryWriter(_ms);

                // Reset the MemoryStream's position to the beginning
                _ms.SetLength(0);
                _ms.Position = 0;

                _writer.Write(message.Header); //uint - 4
                _writer.Write(message.DatagramVersion);

                _writer.Write((byte) message.Platform);
                _writer.Write((byte) message.CurrentScene);
                _writer.Write((byte)message.Paused);
                _writer.Write((byte)message.VenueSize);
                _writer.Write(message.BeatsPerMinute);      //float - 4

                _writer.Write((byte) message.CurrentSongSection);
                _writer.Write((byte) message.CurrentGuitarNotes); // While .Write can do an int, the instruments
                _writer.Write((byte) message.CurrentBassNotes);   // are only 5 to 8 bits, so might as well save space.
                _writer.Write((byte) message.CurrentDrumNotes);
                _writer.Write((byte) message.CurrentKeysNotes);

                _writer.Write(message.CurrentVocalNote);    //float
                _writer.Write(message.CurrentHarmony0Note); //float
                _writer.Write(message.CurrentHarmony1Note); //float
                _writer.Write(message.CurrentHarmony2Note); //float

                _writer.Write((byte) message.LightingCue);
                _writer.Write((byte) message.PostProcessing);
                _writer.Write(message.FogState); //bool
                _writer.Write((byte) message.StrobeState);
                _writer.Write(message.Performer); //byte
                _writer.Write(message.Beat); //byte
                _writer.Write((byte) message.Keyframe);
                _writer.Write(message.BonusEffect); //bool
                _writer.Write(message.AutoGenVenueTrack); //bool

                _sendClient.Send(_ms.GetBuffer(), (int) _ms.Position);
            }
            catch (Exception ex)
            {
                YargLogger.LogError($"Error sending UDP packet: {ex.Message}");
            }
        }
    }
}