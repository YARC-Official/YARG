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
        Currently there is only one lighting controller, the Stage Kit Interpreter (which uses its Cues and Primitives classes),
        that attempts to make cues be as close to the Rock Band Stage Kit as possible but in the future there could others.

        3) Hardware controllers. These classes listen to the Lighting Interpreters and translate the lighting cues into
        the actual hardware commands. Currently there are two hardware controllers, one for DMX and one for the Stage Kits.
        */

        public enum FogState
        {
            Off,
            On,
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

        public static DrumNote CurrentDrumNote
        {
            get => _currentDrumNote;
            set
            {
                _currentDrumNote = value;
                OnDrumEvent?.Invoke(value);
            }
        }

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
                //On Pause, turn off the fog and strobe so people don't die, but leave the leds on, looks nice.
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
        public static event Action<DrumNote> OnDrumEvent;
        public static event Action<VocalNote> OnVocalsEvent;
        public static event Action<Beatline> OnBeatLineEvent;
        public static event Action<LightingEvent> OnLightingEvent;
        public static event Action<StageKitStrobeSpeed> OnStrobeEvent;

        private static bool _paused;
        private static bool _largeVenue;
        private static Beatline _currentBeatline;
        private static DrumNote _currentDrumNote;
        private static FogState _currentFogState;
        private static VocalNote _currentVocalNote;
        private static LightingEvent _currentLightingCue;
        private static StageEffectEvent _currentStageEffect;
        private static StageKitStrobeSpeed _currentStrobeState;

        private GameplayBehaviour _gameplayMonitor;

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            CurrentLightingCue = null;
            CurrentFogState = FogState.Off;
            CurrentStrobeState = StageKitStrobeSpeed.Off;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            switch ((SceneIndex) scene.buildIndex)
            {
                case SceneIndex.Gameplay:
                    break;

                case SceneIndex.Score:
                    CurrentLightingCue = new LightingEvent(LightingType.Score, 0, 0);
                    break;

                case SceneIndex.Menu:
                    CurrentLightingCue = new LightingEvent(LightingType.Menu, 0, 0);
                    break;

                default:
                    YargLogger.LogWarning("Unknown Scene loaded!");
                    break;
            }
        }

        public static void FireBonusFXEvent()
        {
            //This is a instantaneous event, so we don't need to keep track of it.
            OnBonusFXEvent?.Invoke();
        }
    }
}
/*
"Dad always thought laughter was the best medicine, which I guess is why several of us died of tuberculosis."

    -Jack Handey.
*/