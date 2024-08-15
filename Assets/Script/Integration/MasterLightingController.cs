using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
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
        [Serializable]
        public struct LightingMessage
        {
            public byte HeaderByte1;
            public byte HeaderByte2;
            public byte HeaderByte3;
            public byte HeaderByte4;

            public byte DatagramVersion;
            public byte Platform;
            public byte CurrentScene;
            public bool Paused;
            public bool LargeVenue;

            public float BeatsPerMinute;
            public byte CurrentSongSection;
            public byte CurrentGuitarNotes;
            public byte CurrentBassNotes;
            public byte CurrentDrumNotes;
            public byte CurrentKeysNotes;
            public byte CurrentVocalNote;
            public byte CurrentHarmony0Note;
            public byte CurrentHarmony1Note;
            public byte CurrentHarmony2Note;

            public byte LightingCue;
            public byte PostProcessing;
            public bool FogState;
            public byte StrobeState;
            public byte Performer;
            public byte Beat;
            public byte Keyframe;
            public bool BonusEffect;
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

        private static UdpClient _sendClient = new();

        //Has to be at least 44 because of DMX, 88 should be enough... for now...
        private const float TARGET_FPS = 88f;
        private const float TIME_BETWEEN_CALLS = 1f / TARGET_FPS;
        private static Timer _timer;

        // NYI - waiting for parser rewrite.
        // public static PerformerEvent CurrentPerformerEvent;
        public static bool MLCPaused;
        public static bool MLCLargeVenue;
        public static byte MLCSceneIndex;
        public static byte MLCCurrentGuitarNotes;
        public static byte MLCCurrentBassNotes;
        public static byte MLCCurrentDrumNotes;
        public static byte MLCCurrentKeysNotes;
        public static byte MLCCurrentVocalNote;
        public static byte MLCCurrentHarmony0Note;
        public static byte MLCCurrentHarmony1Note;
        public static byte MLCCurrentHarmony2Note;
        public static bool MLCBonusFX;
        public static byte MLCCurrentSongSection;
        public static bool MLCFogState;
        public static byte MLCStrobeState;
        public static float MLCCurrentBPM;
        public static byte MLCCurrentBeat;
        public static byte MLCKeyframe;
        public static byte MLCCurrentLightingCue;
        public static byte MLCPostProcessing;

        public static ushort MLCudpPort;
        public static string MLCudpIP;

        private static LightingEvent _currentLightingCue;

        public static LightingEvent CurrentLightingCue
        {
            get => _currentLightingCue;

            set
            {
                // Could probably move this into the gameplay monitor

                // This is for the debug menu
                _currentLightingCue = value;

                //Keyframes are indicators and not really lighting cues themselves, also chorus and verse act more as modifiers and section labels and also not really lighting cues, they can be stacked under a lighting cue.
                if (value.Type != LightingType.Keyframe_Next && value.Type != LightingType.Keyframe_Previous &&
                    value.Type != LightingType.Keyframe_First && value.Type != LightingType.Chorus &&
                    value.Type != LightingType.Verse)
                {
                    MLCCurrentLightingCue = (byte) value.Type;
                    // might need a null check here = NoCue
                }
                else if (value.Type is LightingType.Keyframe_Next or LightingType.Keyframe_Previous
                    or LightingType.Keyframe_First)
                {
                    MLCKeyframe = (byte) value.Type;
                    //might need an else here to keep keyframe at current value
                }
                else if (value.Type is LightingType.Verse or LightingType.Chorus)
                {
                    MLCCurrentSongSection = (byte) value.Type;
                    //might need an else here to keep section at current value
                }
            }
        }

        private void Start()
        {
            _timer = new Timer(TIME_BETWEEN_CALLS * 1000);
            _timer.Elapsed += (sender, e) => Sender();
            _timer.Start();
        }

        public static void Sender()
        {
            if (!SettingsManager.Settings.EnableYALCYDatastream.Value) return;

            var message = new LightingMessage
            {
                HeaderByte1 = 0x59, // Y
                HeaderByte2 = 0x41, // A
                HeaderByte3 = 0x52, // R
                HeaderByte4 = 0x47, // G

                DatagramVersion = 0,
                Platform = SetPlatformByte(),
                CurrentScene = MLCSceneIndex, // gets set by the initializer, below.
                Paused = MLCPaused,           // gets set by the GameplayMonitor.
                LargeVenue = MLCLargeVenue,   // gets set on chart load by the GameplayMonitor.

                BeatsPerMinute = MLCCurrentBPM,             // gets set by the GameplayMonitor.
                LightingCue = MLCCurrentLightingCue,        // setter triggered by the GameplayMonitor.
                PostProcessing = MLCPostProcessing,         // setter triggered by the GameplayMonitor.
                FogState = MLCFogState,                     // gets set by the GameplayMonitor.
                StrobeState = MLCStrobeState,               // gets set by the GameplayMonitor.
                Performer = 0x00,                           // Performer not parsed yet
                Beat = MLCCurrentBeat,                      // gets set by the GameplayMonitor.
                Keyframe = MLCKeyframe,                     // gets set on lighting cue change.
                BonusEffect = MLCBonusFX,                   // gets set by the GameplayMonitor.
                CurrentSongSection = MLCCurrentSongSection, // gets set on lighting cue change.

                CurrentGuitarNotes = MLCCurrentGuitarNotes,   // gets set by the GameplayMonitor.
                CurrentBassNotes = MLCCurrentBassNotes,       // gets set by the GameplayMonitor.
                CurrentDrumNotes = MLCCurrentDrumNotes,       // gets set by the GameplayMonitor.
                CurrentKeysNotes = MLCCurrentKeysNotes,       // gets set by the GameplayMonitor.
                CurrentVocalNote = MLCCurrentVocalNote,       // gets set by the GameplayMonitor.
                CurrentHarmony0Note = MLCCurrentHarmony0Note, // gets set by the GameplayMonitor.
                CurrentHarmony1Note = MLCCurrentHarmony1Note, // gets set by the GameplayMonitor.
                CurrentHarmony2Note = MLCCurrentHarmony2Note, // gets set by the GameplayMonitor.
            };

            SerializeAndSend(message);
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

        public static void Initializer(Scene scene)
        {
            // Ignore the persistent scene
            if ((SceneIndex) scene.buildIndex == SceneIndex.Persistent) return;

            MLCFogState = false;
            MLCStrobeState = (byte) StageKitStrobeSpeed.Off;
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
                    MLCSceneIndex = (byte) SceneIndexByte.Gameplay;
                    break;

                case SceneIndex.Menu:
                    CurrentLightingCue = new LightingEvent(LightingType.Menu, 0, 0);
                    MLCSceneIndex = (byte) SceneIndexByte.Menu;
                    break;

                case SceneIndex.Calibration:
                    MLCSceneIndex = (byte) SceneIndexByte.Calibration;
                    break;

                case SceneIndex.Score:
                    CurrentLightingCue = new LightingEvent(LightingType.Score, 0, 0);
                    MLCSceneIndex = (byte) SceneIndexByte.Score;
                    break;

                default:
                    YargLogger.LogWarning("Unknown Scene loaded!");
                    MLCSceneIndex = (byte) SceneIndexByte.Unknown;
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

            var message = new LightingMessage
            {
                HeaderByte1 = 0x59, // Y
                HeaderByte2 = 0x41, // A
                HeaderByte3 = 0x52, // R
                HeaderByte4 = 0x47, // G
                //everything else is 0
            };

            SerializeAndSend(message);

            _sendClient.Dispose();
        }

        private static void SerializeAndSend(LightingMessage message)
        {
            try
            {
                // serialize
                byte[] data;
                using (var ms = new MemoryStream())
                {
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(ms, message);
                    data = ms.ToArray();
                }

                _sendClient.Send(data, data.Length, MLCudpIP, MLCudpPort);
            }
            catch (Exception ex)
            {
                YargLogger.LogError($"Error sending UDP packet: {ex.Message}");
            }
        }
    }
}