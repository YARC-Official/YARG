using System;
using System.IO;
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
    public class MasterLightingController : MonoBehaviour
    {
        [Serializable]
        public struct LightingMessage
        {
            public uint Header;

            public byte DatagramVersion;
            public PlatformByte Platform;
            public SceneIndexByte CurrentScene;
            public bool Paused;
            public bool LargeVenue;

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

            public LightingType LightingCue;
            public PostProcessingType PostProcessing;
            public bool FogState;
            public StageKitStrobeSpeed StrobeState;
            public byte Performer;
            public BeatlineType Beat;
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

        private static UdpClient _sendClient = new();

        //Has to be at least 44 because of DMX, 88 should be enough... for now...
        private const float TARGET_FPS = 88f;
        private const float TIME_BETWEEN_CALLS = 1f / TARGET_FPS;
        private static Timer _timer;

        // NYI - waiting for parser rewrite.
        // public static PerformerEvent CurrentPerformerEvent;
        public static PlatformByte MLCPlatform;
        public static bool MLCPaused;
        public static bool MLCLargeVenue;
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
        public static StageKitStrobeSpeed MLCStrobeState;
        public static float MLCCurrentBPM;
        public static BeatlineType MLCCurrentBeat;
        public static LightingType MLCKeyframe;
        public static LightingType MLCCurrentLightingCue;
        public static PostProcessingType MLCPostProcessing;

        public static ushort MLCudpPort;
        public static string MLCudpIP;

        private static LightingEvent _currentLightingCue;

        public static LightingMessage message = new LightingMessage();
        public static MemoryStream ms = new MemoryStream();
        public static BinaryWriter writer = new BinaryWriter(ms);

        public static LightingEvent CurrentLightingCue
        {
            get => _currentLightingCue;

            set
            {
                // Could probably move this into the gameplay monitor

                // This is for the debug menu
                _currentLightingCue = value;

                //Keyframes are indicators and not really lighting cues themselves, also chorus and verse act more as modifiers and section labels and also not really lighting cues, they can be stacked under a lighting cue.
                if (value.Type is not (LightingType.Keyframe_Next or LightingType.Keyframe_Previous
                    or LightingType.Keyframe_First or LightingType.Chorus or LightingType.Verse))
                {
                    MLCCurrentLightingCue = value.Type;
                    // might need a null check here = NoCue, testing needed
                }
                else if (value.Type is LightingType.Keyframe_Next or LightingType.Keyframe_Previous
                    or LightingType.Keyframe_First)
                {
                    MLCKeyframe = value.Type;
                    //might need an else here to keep keyframe at current value, testing needed
                }
                else if (value.Type is LightingType.Verse or LightingType.Chorus)
                {
                    MLCCurrentSongSection = value.Type;
                    //might need an else here to keep section at current value, testing needed
                }
            }
        }

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
            _timer.Elapsed += (sender, e) => Sender(message);
            _timer.Start();
        }

        public static void Sender(LightingMessage message)
        {
            message.Header = 0x59415247; // Y A R G

            message.DatagramVersion = 0;          // version 0 currently
            message.Platform = MLCPlatform;       // Set by the Preprocessor Directive above.
            message.CurrentScene = MLCSceneIndex; // gets set by the initializer.
            message.Paused = MLCPaused;           // gets set by the GameplayMonitor.
            message.LargeVenue = MLCLargeVenue;   // gets set on chart load by the GameplayMonitor.

            message.BeatsPerMinute = MLCCurrentBPM;             // gets set by the GameplayMonitor.
            message.LightingCue = MLCCurrentLightingCue;        // setter triggered by the GameplayMonitor.
            message.PostProcessing = MLCPostProcessing;         // setter triggered by the GameplayMonitor.
            message.FogState = MLCFogState;                     // gets set by the GameplayMonitor.
            message.StrobeState = MLCStrobeState;               // gets set by the GameplayMonitor.
            message.Performer = 0x00;                           // Performer not parsed yet
            message.Beat = MLCCurrentBeat;                      // gets set by the GameplayMonitor.
            message.Keyframe = MLCKeyframe;                     // gets set on lighting cue change.
            message.BonusEffect = MLCBonusFX;                   // gets set by the GameplayMonitor.
            message.CurrentSongSection = MLCCurrentSongSection; // gets set on lighting cue change.

            message.CurrentGuitarNotes = MLCCurrentGuitarNotes;   // gets set by the GameplayMonitor.
            message.CurrentBassNotes = MLCCurrentBassNotes;       // gets set by the GameplayMonitor.
            message.CurrentDrumNotes = MLCCurrentDrumNotes;       // gets set by the GameplayMonitor.
            message.CurrentKeysNotes = MLCCurrentKeysNotes;       // gets set by the GameplayMonitor.
            message.CurrentVocalNote = MLCCurrentVocalNote;       // gets set by the GameplayMonitor.
            message.CurrentHarmony0Note = MLCCurrentHarmony0Note; // gets set by the GameplayMonitor.
            message.CurrentHarmony1Note = MLCCurrentHarmony1Note; // gets set by the GameplayMonitor.
            message.CurrentHarmony2Note = MLCCurrentHarmony2Note; // gets set by the GameplayMonitor.

            SerializeAndSend(message);
        }

        public static void Initializer(Scene scene)
        {
            // Ignore the persistent scene
            if ((SceneIndex) scene.buildIndex == SceneIndex.Persistent) return;

            MLCFogState = false;
            MLCStrobeState = StageKitStrobeSpeed.Off;
            MLCCurrentBPM = 0;
            MLCCurrentDrumNotes = 0;
            MLCCurrentGuitarNotes = 0;
            MLCCurrentKeysNotes = 0;
            MLCCurrentBassNotes = 0;
            MLCCurrentVocalNote = 0;
            MLCCurrentHarmony0Note = 0;
            MLCCurrentHarmony1Note = 0;
            MLCCurrentHarmony2Note = 0;
            MLCCurrentSongSection = 0;

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
            YargLogger.LogInfo("Killing Lighting sender...");

            _timer?.Stop();
            _timer?.Dispose();

            if (_sendClient == null) return;

            // force send a blank packet to turn everything off.

            message.Header = 0x59415247; // Y A R G
            message.DatagramVersion = 0;
            message.Platform = 0;
            message.CurrentScene = 0;
            message.Paused = false;
            message.LargeVenue = false;
            message.BeatsPerMinute = 0;
            message.CurrentSongSection = 0;
            message.CurrentGuitarNotes = 0;
            message.CurrentBassNotes = 0;
            message.CurrentDrumNotes = 0;
            message.CurrentKeysNotes = 0;
            message.CurrentVocalNote = 0;
            message.CurrentHarmony0Note = 0;
            message.CurrentHarmony1Note = 0;
            message.CurrentHarmony2Note = 0;
            message.LightingCue = 0;
            message.PostProcessing = 0;
            message.FogState = false;
            message.StrobeState = 0;
            message.Performer = 0;
            message.Beat = 0;
            message.Keyframe = 0;
            message.BonusEffect = false;

            SerializeAndSend(message);

            _sendClient.Dispose();
        }

        private static void SerializeAndSend(LightingMessage message)
        {
            if (!SettingsManager.Settings.EnableYALCYDatastream.Value) return;

            try
            {
                // Reset the MemoryStream
                ms.SetLength(0);

                writer.Write(message.Header);
                writer.Write(message.DatagramVersion);
                writer.Write((byte) message.Platform);
                writer.Write((byte) message.CurrentScene);
                writer.Write(message.Paused);
                writer.Write(message.LargeVenue);

                writer.Write(message.BeatsPerMinute);
                writer.Write((byte) message.CurrentSongSection);
                writer.Write((byte) message.CurrentGuitarNotes); // while Write can do an int, the instruments
                writer.Write((byte) message.CurrentBassNotes);   // are only 5 to 8 bits, so might as well save space.
                writer.Write((byte) message.CurrentDrumNotes);
                writer.Write((byte) message.CurrentKeysNotes);
                writer.Write(message.CurrentVocalNote);
                writer.Write(message.CurrentHarmony0Note);
                writer.Write(message.CurrentHarmony1Note);
                writer.Write(message.CurrentHarmony2Note);

                writer.Write((byte) message.LightingCue);
                writer.Write((byte) message.PostProcessing);
                writer.Write(message.FogState);
                writer.Write((byte) message.StrobeState);
                writer.Write(message.Performer);
                writer.Write((byte) message.Beat);
                writer.Write((byte) message.Keyframe);
                writer.Write(message.BonusEffect);

                byte[] data = ms.ToArray();

                _sendClient.Send(data, data.Length, MLCudpIP, MLCudpPort);
            }
            catch (Exception ex)
            {
                YargLogger.LogError($"Error sending UDP packet: {ex.Message}");
            }
        }
    }
}