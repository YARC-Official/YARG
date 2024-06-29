using System;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay;

namespace YARG.Integration
{
    public class MasterLightingController : MonoBehaviour
    {
        /*
        Real-life lighting integration works in 3 parts:
        1) This class, the Master lighting controller, which maintains the state of the lighting and stage effects.
        It listens for events from the venue track, sync (beat) track, etc, maintains a list of current lighting cues,
         fog state, etc, and broadcasts those events on change.

        2) Lighting Interpreters. These classes listen to the events from the Master Lighting Controller and translate them
        into the actual timing and light patterns, for example, interpreting flare_fast as 8 blue leds turning on.
        Currently there are two. The Stage Kit Interpreter (which uses its Cues and Primitives classes),
        that attempts to make cues be as close to the Rock Band Stage Kit as possible and the sACN Interpreter, which
        sets DMX channel values based on the lighting cues and other events happening.

        3) Hardware controllers. These classes listen to the Lighting Interpreters and translate the lighting cues into
        the actual hardware commands. Currently there are two hardware controllers, one for DMX and one for the Stage Kits.
        Hardware controllers are what is toggled on and off by the enable menu setttings.
        */

        public enum FogState
        {
            Off,
            On,
        }

        public enum InstrumentType
        {
            Drums,
            Guitar,
            Bass,
            Keys,
        }

        public static LightingEvent CurrentLightingCue
        {
            get => _currentLightingCue;
            set
            {
                PreviousLightingCue = _currentLightingCue;
                _currentLightingCue = value;
                OnLightingEvent?.Invoke(value);
            }
        }

        public static LightingEvent PreviousLightingCue;

        public static PostProcessingEvent CurrentPostProcessing
        {
            get => _currentPostProcessing;
            set
            {
                PreviousPostProcessing = _currentPostProcessing;
                _currentPostProcessing = value;
                OnPostProcessing?.Invoke(value);
            }
        }

        public static PostProcessingEvent PreviousPostProcessing;

        public static FogState CurrentFogState
        {
            get => _currentFogState;
            set
            {
                PreviousFogState = _currentFogState;
                _currentFogState = value;
                OnFogState?.Invoke(value);
            }
        }

        public static FogState PreviousFogState = FogState.Off;

        public static StageKitStrobeSpeed CurrentStrobeState
        {
            get => _currentStrobeState;
            set
            {
                PreviousStrobeState = _currentStrobeState;
                _currentStrobeState = value;
                OnStrobeEvent?.Invoke(value);
            }
        }

        public static StageKitStrobeSpeed PreviousStrobeState = StageKitStrobeSpeed.Off;

        public static int CurrentDrumNotes
        {
            get => _currentDrumNote;
            set
            {
                PreviousDrumNote = _currentDrumNote;
                _currentDrumNote = value;
                OnInstrumentEvent?.Invoke(InstrumentType.Drums, value);
            }
        }

        public static int PreviousDrumNote;

        public static int CurrentGuitarNotes
        {
            get => _currentGuitarNote;
            set
            {
                PreviousGuitarNote = _currentGuitarNote;
                _currentGuitarNote = value;
                OnInstrumentEvent?.Invoke(InstrumentType.Guitar, value);
            }
        }

        public static int PreviousGuitarNote;

        public static int CurrentKeysNotes
        {
            get => _currentKeysNote;
            set
            {
                PreviousKeysNote = _currentKeysNote;
                _currentKeysNote = value;
                OnInstrumentEvent?.Invoke(InstrumentType.Keys, value);
            }
        }

        public static int PreviousKeysNote;

        public static int CurrentBassNotes
        {
            get => _currentBassNote;
            set
            {
                PreviousBassNote = _currentBassNote;
                _currentBassNote = value;
                OnInstrumentEvent?.Invoke(InstrumentType.Bass, value);
            }
        }

        public static int PreviousBassNote;

        public static PerformerEvent CurrentPerformerEvent
        {
            get => _currentPerformerEvent;
            set
            {
                PreviousPerformerEvent = _currentPerformerEvent;
                _currentPerformerEvent = value;
                OnPerformerEvent?.Invoke(value);
            }
        }

        public static PerformerEvent PreviousPerformerEvent;

        public static VocalNote CurrentVocalNote
        {
            get => _currentVocalNote;
            set
            {
                _currentVocalNote = value;
                OnVocalsEvent?.Invoke(value);
            }
        }

        public static Beatline CurrentBeatline
        {
            get => _currentBeatline;
            set
            {
                _currentBeatline = value;
                OnBeatLineEvent?.Invoke(value);
            }
        }

        public static bool Paused
        {
            get => _paused;
            set
            {
                // On Pause, turn off the fog and strobe so people don't die, but leave the leds on, looks nice.
                if (value)
                {
                    CurrentFogState = FogState.Off;
                    CurrentStrobeState = StageKitStrobeSpeed.Off;
                }
                else
                {
                    CurrentFogState = PreviousFogState;
                    CurrentStrobeState = PreviousStrobeState;
                }

                _paused = value;
                OnPause?.Invoke(value);
            }
        }

        public static bool LargeVenue
        {
            get => _largeVenue;
            set
            {
                _largeVenue = value;
                OnLargeVenue?.Invoke(value);
            }
        }

        public static event Action<bool> OnPause;
        public static event Action OnBonusFXEvent;
        public static event Action<bool> OnLargeVenue;
        public static event Action<FogState> OnFogState;
        public static event Action<InstrumentType, int> OnInstrumentEvent;
        public static event Action<VocalNote> OnVocalsEvent;
        public static event Action<Beatline> OnBeatLineEvent;
        public static event Action<LightingEvent> OnLightingEvent;
        public static event Action<StageKitStrobeSpeed> OnStrobeEvent;
        public static event Action<PostProcessingEvent> OnPostProcessing;
        public static event Action<PerformerEvent> OnPerformerEvent;

        private static bool _paused;
        private static bool _largeVenue;
        private static Beatline _currentBeatline;
        private static int _currentDrumNote;
        private static FogState _currentFogState;
        private static VocalNote _currentVocalNote;
        private static LightingEvent _currentLightingCue;
        private static StageEffectEvent _currentStageEffect;
        private static StageKitStrobeSpeed _currentStrobeState;
        private static PostProcessingEvent _currentPostProcessing;
        private GameplayBehaviour _gameplayMonitor;
        private static int _currentGuitarNote;
        private static int _currentBassNote;
        private static PerformerEvent _currentPerformerEvent;
        private static int _currentKeysNote;

        public static void FireBonusFXEvent()
        {
            // This is a instantaneous event, so we don't need to keep track of the previous/current event.
            OnBonusFXEvent?.Invoke();
        }

        public static void Initializer(Scene scene)
        {
            switch ((SceneIndex) scene.buildIndex)
            {
                case SceneIndex.Gameplay:
                    //handled by the gameplay monitor
                    break;

                case SceneIndex.Score:
                    OnApplicationQuit();
                    CurrentLightingCue = new LightingEvent(LightingType.Score, 0, 0);
                    break;

                case SceneIndex.Menu:
                    OnApplicationQuit();
                    CurrentLightingCue = new LightingEvent(LightingType.Menu, 0, 0);
                    break;

                case SceneIndex.Calibration:
                    //turn off to not be distracting
                    OnApplicationQuit();
                    break;

                default:
                    YargLogger.LogWarning("Unknown Scene loaded!");
                    break;
            }
        }

        private static void OnApplicationQuit()
        {
            CurrentLightingCue = null;
            CurrentFogState = FogState.Off;
            CurrentStrobeState = StageKitStrobeSpeed.Off;
        }
    }
}
