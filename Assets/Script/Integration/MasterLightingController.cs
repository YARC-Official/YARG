using System;
using System.Collections.Generic;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay;
using Random = UnityEngine.Random;

namespace YARG.Integration
{
    public class MasterLightingController : MonoBehaviour
    {
        /**
        Real-life lighting integration works in 3 parts:
        1) This class, the Master lighting controller, which maintains the state of the lighting and stage effects.
        It listens for events from the venue track, sync (beat) track, etc, maintains a list of current lighting cues,
         fog state, etc, and broadcasts those events on change.

        2) Lighting Interpreters. These classes listen to the events from the Master Lighting Controller and translate them
        into the actual timing and light patterns, for example, interpreting flare_fast as 8 blue leds turning on.
        Currently there is only one lighting controller, the Stage Kit Interpreter (which attempts to make cues be
        as close to the Rock Band Stage Kit as possible), but in the future there could others.

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
                OnLightingEvent?.Invoke(value);
                PreviousLightingCue = _currentLightingCue;
                _currentLightingCue = value;
            }
        }

        public static LightingEvent PreviousLightingCue;

        public static FogState CurrentFogState
        {
            get => _currentFogState;
            set
            {
                OnFogState?.Invoke(value);
                PreviousFogState = _currentFogState;
                _currentFogState = value;
            }
        }

        public static FogState PreviousFogState = FogState.Off;

        public static StageKitStrobeSpeed CurrentStrobeState
        {
            get => _currentStrobeState;
            set
            {
                OnStrobeEvent?.Invoke(value);
                PreviousStrobeState = _currentStrobeState;
                _currentStrobeState = value;
            }
        }

        public static StageKitStrobeSpeed PreviousStrobeState = StageKitStrobeSpeed.Off;

        public static DrumNote CurrentDrumNote
        {
            get => _currentDrumNote;
            set
            {
                OnDrumEvent?.Invoke(value);
                _currentDrumNote = value;
            }
        }

        public static VocalNote CurrentVocalNote
        {
            get => _currentVocalNote;
            set
            {
                OnVocalsEvent?.Invoke(value);
                _currentVocalNote = value;
            }
        }

        public static Beatline CurrentBeatline
        {
            get => _currentBeatline;
            set
            {
                OnBeatLineEvent?.Invoke(value);
                _currentBeatline = value;
            }
        }

        public static bool CurrentBonusFXEvent
        {
            //BonusFX is a one-time event (fireworks, pyro, etc), so we don't need to keep track of it.
            set => OnBonusFXEvent?.Invoke();
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

                OnPause?.Invoke(value);
                _paused = value;
            }
        }

        public static bool LargeVenue
        {
            get => _largeVenue;
            set
            {
                OnLargeVenue?.Invoke(value);
                _largeVenue = value;
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
            switch (scene.buildIndex)
            {
                case (int) SceneIndex.Gameplay:
                    Destroy(_gameplayMonitor);
                    break;

                case (int) SceneIndex.Score:
                    break;

                case (int) SceneIndex.Menu:
                    break;

                default:
                    Debug.LogWarning("(Master Lighting Controller) Unknown Scene unloaded!");
                    break;
            }

            CurrentLightingCue = null;
            CurrentFogState = FogState.Off;
            CurrentStrobeState = StageKitStrobeSpeed.Off;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            switch (scene.buildIndex)
            {
                case (int) SceneIndex.Gameplay:
                    _gameplayMonitor = gameObject.AddComponent<GameplayLightingMonitor>();
                    break;

                case (int) SceneIndex.Score:
                    CurrentLightingCue = new LightingEvent(LightingType.Score, 0, 0);

                    break;
                case (int) SceneIndex.Menu:
                    CurrentLightingCue = new LightingEvent(LightingType.Menu, 0, 0);
                    break;

                default:
                    Debug.LogWarning("(Master Lighting Controller) Unknown Scene loaded!");
                    break;
            }
        }
    }

    public class GameplayLightingMonitor : GameplayBehaviour
    {
        private VenueTrack _venue;
        private SyncTrack _sync;
        private List<VocalsPhrase> _vocals;
        private InstrumentDifficulty<DrumNote> _drums;
        private int _eventIndex;
        private int _lightingIndex;
        private int _syncIndex;
        private int _vocalsIndex;
        private int _drumIndex;

        protected override void OnChartLoaded(SongChart chart)
        {
            //This should be read from the venue itself eventually, but for now, we'll just randomize it.
            MasterLightingController.LargeVenue = Random.Range(0, 1) == 1;
            _venue = chart.VenueTrack;
            _sync = chart.SyncTrack;
            _vocals = chart.Vocals.Parts[0].NotePhrases;
            chart.FourLaneDrums.Difficulties.TryGetValue(Difficulty.Expert, out _drums);
        }

        private void Update()
        {
            if (MasterLightingController.Paused != GameManager.Paused)
            {
                MasterLightingController.Paused = GameManager.Paused;
            }

            //drum events
            while (_drumIndex < _drums.Notes.Count && _drums.Notes[_drumIndex].Time <= GameManager.SongTime)
            {
                MasterLightingController.CurrentDrumNote = _drums.Notes[_drumIndex];
                _drumIndex++;
            }

            //End of vocal phrase. SilhouetteSpot is the only cue that uses vocals, listening to the end of the phrase.
            while (_vocalsIndex < _vocals.Count &&
                Math.Min(_vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1].TotalTimeEnd,
                    _vocals[_vocalsIndex].TimeEnd) <= GameManager.SongTime)
            {
                MasterLightingController.CurrentVocalNote = _vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1];
                _vocalsIndex++;
            }

            //beatline events
            while (_syncIndex < _sync.Beatlines.Count && _sync.Beatlines[_syncIndex].Time <= GameManager.SongTime)
            {
                MasterLightingController.CurrentBeatline = _sync.Beatlines[_syncIndex];
                _syncIndex++;
            }

            //The lighting cues from the venue track are handled here.
            while (_lightingIndex < _venue.Lighting.Count &&
                _venue.Lighting[_lightingIndex].Time <= GameManager.SongTime)
            {
                switch (_venue.Lighting[_lightingIndex].Type)
                {
                    case LightingType.Strobe_Off:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Off;
                        break;

                    case LightingType.Strobe_Fast:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Fast;
                        break;

                    case LightingType.Strobe_Medium:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Medium;
                        break;

                    case LightingType.Strobe_Slow:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Slow;
                        break;

                    case LightingType.Strobe_Fastest:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Fastest;
                        break;

                    default:
                        //Okay so this a bit odd. The stage kit never has the strobe on with a lighting cue.
                        //But the Strobe_Off event is almost never used, relying instead on the cue change to turn it off.
                        //So this technically should be in the stage kit lighting controller code but I don't want the
                        //stage kit reaching into this main lighting controller. Also, Each subclass of the lighting
                        //controller (dmx, stage kit, rgb, etc) could handle this differently but then we have to guess
                        //at how long the strobe should be on. So we'll just turn it off here.
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Off;
                        MasterLightingController.CurrentLightingCue = _venue.Lighting[_lightingIndex];
                        break;
                }

                _lightingIndex++;
            }

            //For "fogOn", "fogOff", and "BonusFx" events
            while (_eventIndex < _venue.Stage.Count && _venue.Stage[_eventIndex].Time <= GameManager.SongTime)
            {
                if (_venue.Stage[_eventIndex].Effect == StageEffect.FogOn)
                {
                    MasterLightingController.CurrentFogState = MasterLightingController.FogState.On;
                }

                if (_venue.Stage[_eventIndex].Effect == StageEffect.FogOff)
                {
                    MasterLightingController.CurrentFogState = MasterLightingController.FogState.Off;
                }

                if (_venue.Stage[_eventIndex].Effect == StageEffect.BonusFx)
                {
                    MasterLightingController.CurrentBonusFXEvent = true;
                }

                _eventIndex++;
            }
        }
    }
}
/*
    "I hope that after I die, people will say of me: 'That guy sure owed me a lot of money.'"

    - Jack Handey.
*/